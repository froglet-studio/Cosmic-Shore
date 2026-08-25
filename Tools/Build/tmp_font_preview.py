#!/usr/bin/env python3
"""
Render text using a GENERATED TMP font asset, outside Unity.

Deliberately reads the .asset file -- atlas pixels, glyph rects, face metrics --
rather than the source TTF, so what it proves is the shipped asset: the SDF
encoding, the packing, the metrics tables and the fallback-free glyph coverage.
It reproduces TMP_SDF.shader's alpha rule, not FreeType's rasteriser.

Requires: numpy, Pillow.
"""
import re, numpy as np


class TMPFontAsset:
    def __init__(self, path):
        t = open(path, encoding='utf-8', errors='replace').read()
        g = lambda k, d='0': (re.search(rf"^  {k}: (-?[\d.]+)", t, re.M) or
                              re.match(r"(-?[\d.]+)", d)).group(1)
        f = lambda k: float(re.search(rf"^    {k}: (-?[\d.E-]+)", t, re.M).group(1))
        self.name        = re.search(r"^  m_Name: (.*)$", t, re.M).group(1).strip()
        self.point_size  = f('m_PointSize')
        self.padding     = float(g('m_AtlasPadding'))
        self.line_height = f('m_LineHeight')
        self.ascent      = f('m_AscentLine')
        self.descent     = f('m_DescentLine')
        self.cap         = f('m_CapLine')
        w = int(float(g('m_AtlasWidth'))); h = int(float(g('m_AtlasHeight')))
        self.atlases = [np.frombuffer(bytes.fromhex(m), dtype=np.uint8).reshape(h, w)
                        for m in re.findall(r"^  _typelessdata: ([0-9a-f]+)$", t, re.M)]
        self.glyphs = {}
        for b in re.findall(
                r"- m_Index: (\d+)\n    m_Metrics:\n      m_Width: (-?[\d.E-]+)\n"
                r"      m_Height: (-?[\d.E-]+)\n      m_HorizontalBearingX: (-?[\d.E-]+)\n"
                r"      m_HorizontalBearingY: (-?[\d.E-]+)\n      m_HorizontalAdvance: (-?[\d.E-]+)\n"
                r"    m_GlyphRect:\n      m_X: (-?\d+)\n      m_Y: (-?\d+)\n"
                r"      m_Width: (-?\d+)\n      m_Height: (-?\d+)\n    m_Scale: [\d.]+\n"
                r"    m_AtlasIndex: (\d+)", t):
            self.glyphs[int(b[0])] = dict(
                w=float(b[1]), h=float(b[2]), bx=float(b[3]), by=float(b[4]), adv=float(b[5]),
                rx=int(b[6]), ry=int(b[7]), rw=int(b[8]), rh=int(b[9]), ai=int(b[10]))
        self.chars = {int(u): int(i) for u, i in re.findall(
            r"- m_ElementType: 1\n    m_Unicode: (\d+)\n    m_GlyphIndex: (\d+)", t)}

    def has(self, ch):
        return ord(ch) in self.chars

    def advance(self, ch, size, tracking_em=0.0):
        gi = self.chars.get(ord(ch))
        if gi is None:
            return 0.0
        return self.glyphs[gi]['adv'] * size / self.point_size + tracking_em * size


def draw_text(img, font, text, x, baseline, size, tracking_em=0.0, rgb=(255, 255, 255)):
    """Composite text onto an HxWx3 float image. Returns the advanced pen x."""
    k    = size / font.point_size
    grad = font.padding + 1.0
    pen  = float(x)
    for ch in text:
        gi = font.chars.get(ord(ch))
        if gi is None:                       # missing glyph: advance a space, draw nothing
            pen += font.advance(' ', size) + tracking_em * size
            continue
        g = font.glyphs[gi]
        if g['rw'] and g['rh']:
            p   = int(font.padding)
            atl = font.atlases[g['ai']]
            # atlas window = tight rect + padding, in Unity bottom-up rows
            ax0, ay0 = g['rx'] - p, g['ry'] - p
            aw, ah   = g['rw'] + 2 * p, g['rh'] + 2 * p
            win = atl[ay0:ay0 + ah, ax0:ax0 + aw].astype(np.float32) / 255.0
            win = np.flipud(win)             # -> top-down for image space

            # destination box, in pixels
            dx0 = pen + (g['bx'] - p) * k
            dy0 = baseline - (g['by'] + p) * k
            dw, dh = aw * k, ah * k
            ix0, iy0 = int(np.floor(dx0)), int(np.floor(dy0))
            ix1, iy1 = int(np.ceil(dx0 + dw)), int(np.ceil(dy0 + dh))
            ix0c, iy0c = max(ix0, 0), max(iy0, 0)
            ix1c, iy1c = min(ix1, img.shape[1]), min(iy1, img.shape[0])
            if ix1c > ix0c and iy1c > iy0c:
                px = (np.arange(ix0c, ix1c) + 0.5 - dx0) / k      # in atlas-window px
                py = (np.arange(iy0c, iy1c) + 0.5 - dy0) / k
                sx = np.clip(px - 0.5, 0, aw - 1.001)
                sy = np.clip(py - 0.5, 0, ah - 1.001)
                x0 = sx.astype(int); y0 = sy.astype(int)
                fx = (sx - x0)[None, :];    fy = (sy - y0)[:, None]
                a = (win[np.ix_(y0,     x0    )] * (1 - fx) * (1 - fy) +
                     win[np.ix_(y0,     x0 + 1)] * fx       * (1 - fy) +
                     win[np.ix_(y0 + 1, x0    )] * (1 - fx) * fy +
                     win[np.ix_(y0 + 1, x0 + 1)] * fx       * fy)
                # TMP_SDF alpha rule: signed distance in atlas px -> target px -> 1px AA ramp
                sd  = (a - 0.5) * 2.0 * grad * k
                cov = np.clip(sd + 0.5, 0.0, 1.0)[:, :, None]
                dst = img[iy0c:iy1c, ix0c:ix1c, :]
                img[iy0c:iy1c, ix0c:ix1c, :] = dst * (1 - cov) + np.array(rgb, np.float32) * cov
        pen += g['adv'] * k + tracking_em * size
    return pen


def measure(font, text, size, tracking_em=0.0):
    return sum(font.advance(c, size, tracking_em) for c in text)


def save(img, path):
    from PIL import Image
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(path)
    return path
