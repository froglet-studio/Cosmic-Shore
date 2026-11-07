#!/usr/bin/env python3
"""The MATTE: a painting over a flat key colour becomes the pilot on transparency, with no fringe and no specks.

A painter (an image model, a scan of an artist's board) delivers the pilot on one flat colour the brief named
(brief.key_for: green #00FF00, or blue for a green coat, IVY's leaves or a Preservationist's green jacket; a spec
may name magenta for a non-Ruby pilot that can take neither). The matte:

  1. ESTIMATES the key from the border ring of the image (seeded by the brief's key: the boots touch the bottom
     edge, so an unseeded ring would count the figure) (the brief asks for an edge-to-edge flat colour, so the
     ring is the background; a model's slight gradient or noise is measured there too and becomes the tolerance);
  2. computes the DIFFERENCE KEY: a pixel blended over a pure green (or blue, or magenta) key carries that much
     excess of the key's channel over the others, so alpha = 1 - excess / the key's own excess (0.5 for a half
     fur edge, 1 for the coat, 0 for the background). The ring's OKLab spread (plus a margin) gates it: a pixel
     within the background cluster is nothing whatever its channels, so a model's noise never becomes a 2 %
     haze. The difference key acts only in the EDGE BAND (within BAND of a background pixel): a jade-teal seam
     light in the middle of a coat has a little green excess too, and without the band it came out 4 % see-through
     (Delmira, round 1). A grey key has no excess and falls back to an OKLab distance ramp;
  3. DECONTAMINATES the edge: a semi-transparent pixel is the pilot's colour blended over the key, so the key's
     share is removed (F = (C - (1 - a) K) / a) and the key's hue is suppressed in the fringe (green never
     brighter than the other channels there), which is what kills the lime halo a plain crop leaves;
  4. CLEANS: keeps the largest connected component plus any piece at least 2 % of its size (a thrown rope, a
     separate feather), drops the specks a model scatters near the figure, and fills pinholes smaller than a
     fraction of the figure, so no light leaks through a fur edge.

A source that already carries its own alpha (an artist's PNG) skips 1-3 and takes only the cleaning: key="alpha".

    python3 Tools/Build/pilot_art/matte.py <source.png> --out <rgba.png> [--key #00FF00]   # try the matte alone
    python3 Tools/Build/pilot_art/matte.py --self-test

Deterministic: the same source bytes and numbers give the same RGBA, which is what author.py --check holds.
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import measure  # noqa: E402

RING = 0.02           # the border ring the key is estimated from, as a fraction of each edge
RAMP_MARGIN = 0.035   # OKLab distance added to the ring's spread: where the background gate fully opens
RAMP_WIDTH = 0.30     # a GREY key only: OKLab distance from the gate to full alpha (a chroma key uses the difference)
DOMINANT = 0.25       # a key channel within this of the key's brightest channel is one of its dominant channels
BAND = 0.010          # the edge band where the difference key acts, as a fraction of the image height (13 px at 1280)
KEEP_SHARE = 0.02     # a component at least this share of the largest is kept (a rope end, a feather)
HOLE_SHARE = 0.002    # a hole smaller than this share of the figure is filled (pinholes in fur)
FRINGE_PX = 3         # how far inside the edge the hue suppression reaches


def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_oklab(rgb):
    """sRGB 0..1 (…x3) -> OKLab (Bjorn Ottosson 2020), the space the colour domains use."""
    lin = srgb_to_linear(np.clip(rgb, 0.0, 1.0))
    r, g, b = lin[..., 0], lin[..., 1], lin[..., 2]
    l_ = np.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
    m_ = np.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
    s_ = np.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)
    L = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_
    a = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_
    bb = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_
    return np.stack([L, a, bb], axis=-1)


def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def ring_mask(h, w, frac=RING):
    t = max(1, int(round(min(h, w) * frac)))
    m = np.zeros((h, w), dtype=bool)
    m[:t, :] = m[-t:, :] = True
    m[:, :t] = m[:, -t:] = True
    return m


def estimate_key(rgb, hint=None):
    """The key colour as the border ring's median, and the ring's spread (its 99th-percentile OKLab distance from
    that median). With a hint (the brief's key), the ring pixels within a generous distance of the hint are used,
    so a figure that touches the edge does not pull the estimate."""
    h, w = rgb.shape[:2]
    ring = rgb[ring_mask(h, w)]
    if hint is not None:
        d = np.linalg.norm(to_oklab(ring) - to_oklab(hint), axis=-1)
        near = ring[d < 0.25]
        if len(near) >= 0.2 * len(ring):
            ring = near
    key = np.median(ring, axis=0).astype(np.float32)
    spread = float(np.percentile(np.linalg.norm(to_oklab(ring) - to_oklab(key), axis=-1), 99))
    return key, spread


def _smoothstep(lo, hi, x):
    t = np.clip((x - lo) / max(hi - lo, 1e-6), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def key_channels(key):
    """The key's dominant channels (green: [1]; magenta: [0, 2]; blue: [2]) and the others. A grey key: ([], [])."""
    mx = float(key.max())
    dom = [i for i in range(3) if key[i] >= mx - DOMINANT]
    oth = [i for i in range(3) if i not in dom]
    if not oth:
        return [], []
    return dom, oth


def alpha_from_key(rgb, key, spread):
    d = np.linalg.norm(to_oklab(rgb) - to_oklab(key), axis=-1)
    gate = _smoothstep(spread, spread + RAMP_MARGIN, d)          # 0 inside the background cluster, 1 past it
    dom, oth = key_channels(key)
    if not dom:
        return (gate * _smoothstep(spread + RAMP_MARGIN, spread + RAMP_MARGIN + RAMP_WIDTH, d)).astype(np.float32)
    key_excess = float(key[dom].min() - key[oth].max())
    excess = rgb[..., dom].min(axis=-1) - rgb[..., oth].max(axis=-1)
    diff = 1.0 - np.clip(excess / max(key_excess, 1e-3), 0.0, 1.0)
    keyed = gate * diff
    band = _dilate(keyed < 0.02, max(2, int(round(BAND * rgb.shape[0]))))
    return np.where(band, keyed, gate).astype(np.float32)


def _dilate(mask, px):
    out = mask.copy()
    for _ in range(px):
        p = np.pad(out, 1, constant_values=False)
        out = (p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
               | p[:-2, :-2] | p[:-2, 2:] | p[2:, :-2] | p[2:, 2:])
    return out


def decontaminate(rgb, alpha, key):
    """Remove the key's share from every semi-transparent pixel, and suppress the key's hue in the fringe."""
    out = rgb.copy()
    semi = (alpha > 0.0) & (alpha < 1.0)
    a = alpha[semi][:, None]
    out[semi] = np.clip((rgb[semi] - (1.0 - a) * key[None, :]) / np.maximum(a, 1e-3), 0.0, 1.0)
    # The fringe band: within FRINGE_PX of the alpha edge, inside the figure.
    solid = alpha > 0.5
    eroded = solid.copy()
    for _ in range(FRINGE_PX):
        p = np.pad(eroded, 1, constant_values=False)
        eroded = p[1:-1, 1:-1] & p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
    # Only semi-transparent pixels: an opaque edge pixel is the pilot's own colour (jade trim is green-rich), and
    # capping it dulled real trim (review 2026-11-07).
    fringe = (alpha > 0.0) & (alpha < 1.0) & ~eroded
    dom, oth = key_channels(key)                                  # green: [1]; magenta: [0, 2]; blue: [2]
    if dom:                                                       # a chroma key, not a grey
        cap = out[..., oth].mean(axis=-1)
        for c in dom:
            chan = out[..., c]
            out[..., c] = np.where(fringe, np.minimum(chan, cap + 0.02), chan)
    return out


def _fill_from_neighbours(rgb, holes):
    """Paint each hole pixel with the mean of its known 8-neighbours, growing inward until every hole is painted."""
    out = rgb.copy()
    known = ~holes
    while not known.all():
        p = np.pad(out, ((1, 1), (1, 1), (0, 0)), mode="edge")
        pk = np.pad(known, 1, constant_values=False)
        acc = np.zeros_like(out)
        cnt = np.zeros(out.shape[:2], dtype=np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            k = pk[1 + dy:pk.shape[0] - 1 + dy, 1 + dx:pk.shape[1] - 1 + dx]
            acc += p[1 + dy:p.shape[0] - 1 + dy, 1 + dx:p.shape[1] - 1 + dx] * k[..., None]
            cnt += k
        grow = ~known & (cnt > 0)
        if not grow.any():
            break
        out[grow] = acc[grow] / cnt[grow][:, None]
        known = known | grow
    return out


def clean(alpha):
    """Keep the figure (largest component + pieces >= KEEP_SHARE of it), drop specks, fill small holes."""
    solid = alpha > 0.5
    labels, sizes = measure.components(solid)
    if not sizes:
        return alpha
    keep = np.zeros(len(sizes) + 1, dtype=bool)
    keep[1] = True
    for i, s in enumerate(sizes[1:], start=2):
        keep[i] = s >= KEEP_SHARE * sizes[0]
    figure = keep[labels]
    out = np.where(figure | (alpha <= 0.5), alpha, 0.0).astype(np.float32)
    # Specks in the semi-transparent range: anything > 0 not within 2 px of the figure goes.
    near = figure.copy()
    for _ in range(2):
        p = np.pad(near, 1, constant_values=False)
        near = p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    out = np.where(near, out, 0.0).astype(np.float32)
    # Holes: background components not touching the border, below HOLE_SHARE of the figure.
    bg_labels, bg_sizes = measure.components(~(out > 0.5))
    if bg_sizes:
        h, w = out.shape
        border_ids = set(np.unique(np.concatenate([bg_labels[0], bg_labels[-1], bg_labels[:, 0], bg_labels[:, -1]])))
        floor = HOLE_SHARE * sizes[0]
        fill = np.zeros(len(bg_sizes) + 1, dtype=bool)
        for i, s in enumerate(bg_sizes, start=1):
            fill[i] = (i not in border_ids) and s < floor
        out = np.where(fill[bg_labels], 1.0, out).astype(np.float32)
    return out


def key_matte(rgb, key_hex=None):
    """RGB float HxWx3 over a key colour -> (RGBA float HxWx4, info)."""
    hint = hex_rgb(key_hex) if key_hex else None
    key, spread = estimate_key(rgb, hint)
    raw = alpha_from_key(rgb, key, spread)
    alpha = clean(raw)
    filled = (alpha >= 1.0) & (raw < 0.5)                # pinholes clean() closed: their colour is still the key's
    colour = decontaminate(rgb, alpha, key)
    if filled.any():
        colour = _fill_from_neighbours(colour, filled)
    rgba = np.concatenate([colour, alpha[..., None]], axis=-1).astype(np.float32)
    info = dict(key=[float(c) for c in key], spread=spread)
    return rgba, info


def from_alpha(rgba):
    """A source with its own alpha: only the cleaning."""
    out = rgba.copy()
    out[..., 3] = clean(rgba[..., 3])
    return out, dict(key="alpha", spread=0.0)


def matte_file(path, key_hex=None):
    img = measure.load(path)
    if key_hex == "alpha":
        return from_alpha(img)
    return key_matte(img[..., :3], key_hex)


def save(rgba, path):
    Image.fromarray((np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(path)


def self_test():
    failures = []
    rng = np.random.default_rng(7)
    h, w = 320, 256
    key = hex_rgb("#00FF00")
    rgb = np.broadcast_to(key, (h, w, 3)).astype(np.float32).copy()
    rgb += rng.normal(0, 0.01, rgb.shape).astype(np.float32)        # a model's noise on the flat key
    # A grey figure with a soft green-blended edge, a speck, and a pinhole.
    fig = np.zeros((h, w), dtype=bool)
    fig[40:300, 90:170] = True
    rgb[fig] = (0.45, 0.40, 0.35)
    rgb[40:300, 170:174] = 0.5 * np.array([0.45, 0.40, 0.35]) + 0.5 * key      # a half-blended fringe
    rgb[20:24, 20:24] = (0.4, 0.4, 0.4)                                        # a speck
    rgb[150:152, 120:122] = key                                                # a pinhole in the figure
    rgba, info = key_matte(np.clip(rgb, 0, 1), "#00FF00")
    a = rgba[..., 3]
    if not (a[100, 120] > 0.99 and a[10, 200] < 0.01):
        failures.append(f"alpha wrong: figure {a[100, 120]:.2f}, background {a[10, 200]:.2f}")
    if not (0.2 < a[100, 171] < 0.8):
        failures.append(f"the blended fringe is not semi-transparent: {a[100, 171]:.2f}")
    if a[21, 21] != 0.0:
        failures.append("the speck survived")
    if a[150, 120] < 0.99:
        failures.append("the pinhole was not filled")
    if abs(rgba[150, 120, :3] - np.array([0.45, 0.40, 0.35])).max() > 0.03:
        failures.append(f"the filled pinhole kept the key's colour: {rgba[150, 120, :3]}")
    c = rgba[100, 171, :3]
    if c[1] > max(c[0], c[2]) + 0.05:
        failures.append(f"the fringe kept the green: {c}")
    if abs(rgba[100, 120, :3] - np.array([0.45, 0.40, 0.35])).max() > 0.03:
        failures.append("an opaque pixel's colour changed")
    m = measure.metrics(rgba)
    if m["islands"] != 0 or m["halo"] > 0.15:
        failures.append(f"the matte is not clean: islands {m['islands']}, halo {m['halo']:.3f}")
    # A blue key with a ruby-trimmed figure keeps the trim; a green key keeps jade teal seam lights.
    rgb2 = np.broadcast_to(hex_rgb("#0000FF"), (h, w, 3)).astype(np.float32).copy()
    rgb2[fig] = (0.45, 0.40, 0.35)
    rgb2[100:120, 100:160] = hex_rgb("#C800C3")
    rgba2, _ = key_matte(rgb2, "#0000FF")
    if rgba2[110, 130, 3] < 0.99:
        failures.append(f"a blue key ate the ruby trim: alpha {rgba2[110, 130, 3]:.2f}")
    rgb3 = np.broadcast_to(key, (h, w, 3)).astype(np.float32).copy()
    rgb3[fig] = (0.45, 0.40, 0.35)
    rgb3[100:120, 100:160] = hex_rgb("#0EC0B6")          # jade trim in the middle of the coat
    rgb3[200:220, 90:94] = hex_rgb("#0EC0B6")            # jade trim on the coat's edge
    rgba3, _ = key_matte(rgb3, "#00FF00")
    if rgba3[110, 130, 3] < 0.999:
        failures.append(f"a green key made interior jade trim see-through: alpha {rgba3[110, 130, 3]:.3f}")
    if rgba3[210, 91, 3] < 0.9:
        failures.append(f"a green key ate jade trim on the edge: alpha {rgba3[210, 91, 3]:.2f}")
    if abs(rgba3[110, 130, :3] - hex_rgb("#0EC0B6")).max() > 0.01:
        failures.append(f"the fringe cap dulled opaque jade trim: {rgba3[110, 130, :3]}")
    # A magenta key DOES eat ruby trim on the figure's edge: the reason brief.key_for never pairs them.
    rgb4 = np.broadcast_to(hex_rgb("#FF00FF"), (h, w, 3)).astype(np.float32).copy()
    rgb4[fig] = (0.45, 0.40, 0.35)
    rgb4[200:220, 90:94] = hex_rgb("#C800C3")
    if key_matte(rgb4, "#FF00FF")[0][210, 91, 3] > 0.9:
        failures.append("the negative control passed: a magenta key should eat ruby trim on the edge")
    # An alpha source: only the cleaning.
    src = np.zeros((h, w, 4), dtype=np.float32)
    src[fig, :3] = 0.5
    src[fig, 3] = 1.0
    src[5:8, 5:8, 3] = 1.0
    out, _ = from_alpha(src)
    if out[6, 6, 3] != 0.0 or out[100, 120, 3] != 1.0:
        failures.append("from_alpha did not clean or kept the figure wrong")
    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else "OK (14 checks)")
    return 1 if failures else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    paths = [a for a in argv if not a.startswith("--")]
    key = argv[argv.index("--key") + 1] if "--key" in argv else "#00FF00"   # always seeded (see 1. above)
    out = argv[argv.index("--out") + 1] if "--out" in argv else None
    if not paths or not out:
        print(__doc__)
        return 2
    rgba, info = matte_file(paths[0], key)
    save(rgba, out)
    print(f"key {info['key']} spread {info['spread']:.3f} -> {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
