"""The ONE drawing engine for ability-row icons (Docs/Icons/ABILITY_ICON_SKILL_PLAN.md §3).

Angular, zero corner radius, butt caps, mitred joins (Garrett 2026-10-24: "angular"; Docs/STYLE_FOUNDATION.md
§5). A shape is a boolean mask over a supersampled grid in ICON PIXELS (0..128, y DOWN, like every
author_*_icons.py before it); shapes union with `|`, cut with `& ~`. `render` area-averages the grid down to
the 128 px sprite so every edge is anti-aliased, and writes pure white with the shape in alpha - the card tints.

Nothing here knows about a hull. Motifs (the shared pictograms) live in motifs.py; a hull's icons are a spec in
specs/<hull>.py that composes them.
"""
import math

import numpy as np

SIZE = 128                 # the sprite
SS = 8                     # supersample factor
N = SIZE * SS
MARGIN = 12                # the optical box is 104 px: 12 .. 116
BOX = (MARGIN, SIZE - MARGIN)
# The language's stroke weights, in icon px (ICON_LANGUAGE.md).
W_PRIMARY = 11.0
W_SECONDARY = 7.0
W_MIN = 6.0

_lin = (np.arange(N) + 0.5) / SS            # pixel centres, in icon px
GX, GY = np.meshgrid(_lin, _lin)            # y DOWN (row index grows downward)


def empty():
    return np.zeros(GX.shape, dtype=bool)


# ---------------------------------------------------------------------------- shapes

def fill(pts):
    """Even-odd fill of a closed polygon [(x, y), ...] in icon px."""
    inside = empty()
    n = len(pts)
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        if y0 == y1:
            continue
        crosses = (y0 > GY) != (y1 > GY)
        xint = x0 + (GY - y0) * (x1 - x0) / (y1 - y0)
        inside ^= crosses & (GX < xint)
    return inside


def ngon(n, r, cx=64.0, cy=64.0, rot=0.0):
    """Regular n-gon points; rot in degrees, 0 = +x, angles run clockwise on screen (y down)."""
    return [(cx + r * math.cos(math.radians(rot + i * 360.0 / n)),
             cy + r * math.sin(math.radians(rot + i * 360.0 / n))) for i in range(n)]


def disc(cx, cy, r, n=96):
    """A disc, as a 96-gon: round things are allowed, round CORNERS are not."""
    return fill(ngon(n, r, cx, cy))


def _quad(a, b, w):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    ln = math.hypot(dx, dy)
    if ln < 1e-9:
        return None
    nx, ny = -dy / ln * w * 0.5, dx / ln * w * 0.5
    return [(ax + nx, ay + ny), (bx + nx, by + ny), (bx - nx, by - ny), (ax - nx, ay - ny)]


def stroke(pts, w=W_PRIMARY, closed=False, miter_limit=3.0):
    """Polyline stroke: butt-capped quads with MITRED joins (bevelled past miter_limit * w)."""
    mask = empty()
    pts = list(pts)
    segs = list(zip(pts, pts[1:])) + ([(pts[-1], pts[0])] if closed else [])
    for a, b in segs:
        q = _quad(a, b, w)
        if q:
            mask |= fill(q)
    idx = range(1, len(pts) - 1) if not closed else range(len(pts))
    h = w * 0.5
    for i in idx:
        p0, v, p1 = pts[i - 1], pts[i], pts[(i + 1) % len(pts)]
        d0 = (v[0] - p0[0], v[1] - p0[1])
        d1 = (p1[0] - v[0], p1[1] - v[1])
        l0, l1 = math.hypot(*d0), math.hypot(*d1)
        if l0 < 1e-9 or l1 < 1e-9:
            continue
        d0 = (d0[0] / l0, d0[1] / l0)
        d1 = (d1[0] / l1, d1[1] / l1)
        cross = d0[0] * d1[1] - d0[1] * d1[0]
        if abs(cross) < 1e-6:
            continue
        s = -1.0 if cross > 0 else 1.0          # the OUTER side of the turn
        n0 = (-d0[1] * s * h, d0[0] * s * h)
        n1 = (-d1[1] * s * h, d1[0] * s * h)
        c0 = (v[0] + n0[0], v[1] + n0[1])
        c1 = (v[0] + n1[0], v[1] + n1[1])
        bis = (n0[0] + n1[0], n0[1] + n1[1])
        bl = math.hypot(*bis)
        poly = [v, c0, c1]
        if bl > 1e-9:
            cos_half = bl / (2 * h)
            ml = h / max(cos_half, 1e-6)
            if ml <= miter_limit * w:
                m = (v[0] + bis[0] / bl * ml, v[1] + bis[1] / bl * ml)
                poly = [v, c0, m, c1]
        mask |= fill(poly)
    return mask


def ring(cx, cy, r, w=W_PRIMARY, n=96):
    """An annulus of mean radius r and width w."""
    return disc(cx, cy, r + w / 2, n) & ~disc(cx, cy, r - w / 2, n)


def band(cx, cy, r, a0, a1, w=W_PRIMARY, step=3.0):
    """An annular sector (a thick arc with BUTT ends) from a0 to a1 degrees, clockwise on screen."""
    n = max(2, int(abs(a1 - a0) / step))
    outer = [(cx + (r + w / 2) * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
              cy + (r + w / 2) * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    inner = [(cx + (r - w / 2) * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
              cy + (r - w / 2) * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n, -1, -1)]
    return fill(outer + inner)


def rect(x0, y0, x1, y1):
    return fill([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])


def chamfered_rect(x0, y0, x1, y1, c):
    """A rectangle with the style guide's corner sliver on two OPPOSITE corners (top-left / bottom-right)."""
    return fill([(x0 + c, y0), (x1, y0), (x1, y1 - c), (x1 - c, y1), (x0, y1), (x0, y0 + c)])


# ---------------------------------------------------------------------------- point transforms

def rotate(pts, deg, cx=64.0, cy=64.0):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def translate(pts, dx, dy):
    return [(x + dx, y + dy) for x, y in pts]


def scale(pts, sx, sy=None, cx=64.0, cy=64.0):
    sy = sx if sy is None else sy
    return [(cx + (x - cx) * sx, cy + (y - cy) * sy) for x, y in pts]


def shear(pts, kx, cy=64.0):
    """Lean: x += kx * (y - cy). Motion is a SHAPE (a lean), not speed lines."""
    return [(x + kx * (y - cy), y) for x, y in pts]


def mirror_x(pts, cx=64.0):
    return [(2 * cx - x, y) for x, y in pts]


# ---------------------------------------------------------------------------- mask transforms

def shift(mask, dx, dy):
    """Translate a MASK by whole icon px (for ghosts and repeats)."""
    out = empty()
    sx, sy = int(round(dx * SS)), int(round(dy * SS))
    src_y = slice(max(0, -sy), N - max(0, sy))
    src_x = slice(max(0, -sx), N - max(0, sx))
    dst_y = slice(max(0, sy), N - max(0, -sy))
    dst_x = slice(max(0, sx), N - max(0, -sx))
    out[dst_y, dst_x] = mask[src_y, src_x]
    return out


# ---------------------------------------------------------------------------- render

def alpha_of(mask):
    """Area-average the supersampled mask down to a 128 x 128 float alpha in 0..1."""
    return mask.reshape(SIZE, SS, SIZE, SS).mean(axis=(1, 3))


def render(layers):
    """layers: a mask, or [(mask, alpha), ...] composited in order (a 0.5 layer is a GHOST).
    Returns a 128 x 128 x 4 uint8 RGBA: pure white, shape in alpha."""
    from PIL import Image
    if not isinstance(layers, list):
        layers = [(layers, 1.0)]
    a = np.zeros((SIZE, SIZE), dtype=np.float64)
    for mask, la in layers:
        top = alpha_of(mask) * la
        a = top + a * (1.0 - top)
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.clip(np.round(a * 255), 0, 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")
