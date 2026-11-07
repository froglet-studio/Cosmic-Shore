#!/usr/bin/env python3
"""Measure a portrait the way the finish and the gates see it, and the ENVELOPE of the 32 painted references.

Every number the kit frames to (finish.py) or gates on (verify.py) is a MEASUREMENT of a 220x280 RGBA sprite, so
a hand-painted portrait, a model's painting and a procedural one are judged the same way. The envelope is the
reference set measured on 2026-11-07 (Docs/Pilots/art/style_envelope.json): the range the painted set spans, with
the outliers named, so a threshold in verify.py is argued from it instead of guessed.

    python3 Tools/Build/pilot_art/measure.py --refs            # table of the 32 references + write the envelope json
    python3 Tools/Build/pilot_art/measure.py <png> [<png>...]  # the numbers of any portrait(s)
    python3 Tools/Build/pilot_art/measure.py --self-test

Metrics (fractions of the frame unless said):
    top, bottom, left, right   the silhouette's bounding box (alpha > 0.5)
    height, width              its extent
    head_cx, head_w            the centre and width of the HEAD BAND (the top 18 % of the silhouette's height)
    torso_cx, torso_w          the same for the TORSO BAND (30-60 % down the silhouette)
    coverage                   silhouette pixels / frame pixels
    soft                       semi-transparent pixels (0.02 < a < 0.98) per silhouette-edge pixel: how soft the matte is
    halo                       the mean chroma shift of the soft pixels against the opaque rim: a coloured fringe
    lum_p5, lum_p50, lum_p95   luminance percentiles of the opaque pixels (sRGB, 0..1)
    sat                        mean HSV saturation of the opaque pixels
    accent                     share of opaque pixels with sat > 0.6 (the glow lines and the one accent)
    thumb_keep                 silhouette mass kept at the Hangar's 44x56 (box-filtered, > 0.5) over the full-size mass
    islands                    silhouette components beyond the largest, with more than 0.05 % of the frame each
"""
import json
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
REFERENCE_DIR = "Assets/_Graphics/Pilots"
ENVELOPE = "Docs/Pilots/art/style_envelope.json"
SIZE = (220, 280)                 # what the game binds (references/CONTRACT.md)
THUMB = (44, 56)                  # the Hangar's pilot card portrait (author_menu_hangar.py: TLr(10, 10, 44, 56))
HEAD_BAND = (0.0, 0.18)
TORSO_BAND = (0.30, 0.60)
ALPHA = 0.5


def load(path):
    """RGBA float32 in 0..1, HxWx4."""
    im = Image.open(path).convert("RGBA")
    return np.asarray(im, dtype=np.float32) / 255.0


def silhouette(img, thr=ALPHA):
    return img[..., 3] > thr


def components(mask):
    """Connected components (4-neighbour) of a boolean mask: a label array and the sizes, largest first.
    Pure numpy/python: scanline runs merged with union-find, fast enough for a 1024 px source."""
    h, w = mask.shape
    parent = {}

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    labels = np.zeros((h, w), dtype=np.int32)
    runs_prev = []          # (x0, x1, label) of the previous row
    nxt = 1
    for y in range(h):
        row = mask[y]
        if not row.any():
            runs_prev = []
            continue
        d = np.diff(np.concatenate(([0], row.astype(np.int8), [0])))
        starts = np.nonzero(d == 1)[0]
        ends = np.nonzero(d == -1)[0]
        runs = []
        for x0, x1 in zip(starts, ends):
            lab = nxt
            parent[lab] = lab
            nxt += 1
            for px0, px1, plab in runs_prev:
                if px0 < x1 and x0 < px1:
                    union(plab, lab)
            runs.append((x0, x1, lab))
            labels[y, x0:x1] = lab
        runs_prev = runs
    if nxt == 1:
        return labels, []
    # Flatten the labels to their roots.
    roots = np.zeros(nxt, dtype=np.int32)
    for lab in range(1, nxt):
        roots[lab] = find(lab)
    flat = roots[labels]
    ids, counts = np.unique(flat[flat > 0], return_counts=True)
    order = np.argsort(-counts)
    remap = np.zeros(nxt, dtype=np.int32)
    for rank, i in enumerate(order):
        remap[ids[i]] = rank + 1
    return remap[flat], [int(c) for c in counts[order]]


def band(mask, ys, lo, hi):
    y0, y1 = ys.min(), ys.max() + 1
    b0 = y0 + int(round((y1 - y0) * lo))
    b1 = max(b0 + 1, y0 + int(round((y1 - y0) * hi)))
    cols = np.nonzero(mask[b0:b1].any(axis=0))[0]
    if len(cols) == 0:
        return 0.5, 0.0
    return float((cols.min() + cols.max() + 1) / 2), float(cols.max() - cols.min() + 1)


def metrics(img):
    h, w = img.shape[:2]
    a = img[..., 3]
    m = a > ALPHA
    out = dict(w=w, h=h)
    if not m.any():
        out.update(empty=True, coverage=0.0)
        return out
    ys, xs = np.nonzero(m)
    out.update(top=float(ys.min() / h), bottom=float((ys.max() + 1) / h),
               left=float(xs.min() / w), right=float((xs.max() + 1) / w))
    out["height"] = out["bottom"] - out["top"]
    out["width"] = out["right"] - out["left"]
    hc, hw = band(m, ys, *HEAD_BAND)
    tc, tw = band(m, ys, *TORSO_BAND)
    out.update(head_cx=hc / w, head_w=hw / w, torso_cx=tc / w, torso_w=tw / w)
    out["coverage"] = float(m.mean())
    # Edge pixels: silhouette pixels with a non-silhouette 4-neighbour.
    pad = np.pad(m, 1)
    edge = m & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    n_edge = max(1, int(edge.sum()))
    soft = (a > 0.02) & (a < 0.98)
    out["soft"] = float(soft.sum() / n_edge)
    rgb = img[..., :3]
    # Halo: the soft pixels' mean colour against the opaque rim's, as a chroma distance (0 = the same tint).
    if soft.any():
        soft_c = rgb[soft].mean(axis=0)
        rim_c = rgb[edge].mean(axis=0)
        out["halo"] = float(np.abs((soft_c - soft_c.mean()) - (rim_c - rim_c.mean())).sum())
    else:
        out["halo"] = 0.0
    px = rgb[m]
    lum = 0.2126 * px[:, 0] + 0.7152 * px[:, 1] + 0.0722 * px[:, 2]
    p5, p50, p95 = np.percentile(lum, (5, 50, 95))
    out.update(lum_p5=float(p5), lum_p50=float(p50), lum_p95=float(p95))
    mx = px.max(axis=1)
    mn = px.min(axis=1)
    sat = np.where(mx > 1e-4, (mx - mn) / np.maximum(mx, 1e-4), 0.0)
    out["sat"] = float(sat.mean())
    out["accent"] = float((sat > 0.6).mean())
    # The Hangar thumbnail: box-filter the alpha to 44x56 and keep what survives a 0.5 threshold.
    thumb = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).resize(THUMB, Image.BOX), dtype=np.float32) / 255.0
    full_mass = m.mean()
    out["thumb_keep"] = float((thumb > ALPHA).mean() / full_mass) if full_mass > 0 else 0.0
    _, sizes = components(m)
    floor = 0.0005 * h * w
    out["islands"] = int(sum(1 for s in sizes[1:] if s > floor))
    out["empty"] = False
    return out


KEYS = ["top", "bottom", "left", "right", "height", "width", "head_cx", "head_w", "torso_cx", "torso_w", "coverage",
        "soft", "halo", "lum_p5", "lum_p50", "lum_p95", "sat", "accent", "thumb_keep", "islands"]


def table(rows):
    head = f"{'portrait':<28}" + "".join(f"{k:>9}" for k in KEYS)
    lines = [head]
    for name, m in rows:
        if m.get("empty"):
            lines.append(f"{name:<28} (empty)")
            continue
        lines.append(f"{name:<28}" + "".join(f"{m[k]:>9.3f}" if isinstance(m[k], float) else f"{m[k]:>9}" for k in KEYS))
    return "\n".join(lines)


def references():
    d = os.path.join(ROOT, REFERENCE_DIR)
    return sorted(os.path.join(d, f) for f in os.listdir(d) if f.endswith(".png"))


def envelope(rows):
    env = {}
    for k in KEYS:
        vals = sorted(float(m[k]) for _, m in rows if not m.get("empty"))
        env[k] = dict(min=vals[0], p10=float(np.percentile(vals, 10)), median=float(np.median(vals)),
                      p90=float(np.percentile(vals, 90)), max=vals[-1])
    return env


def self_test():
    failures = []
    # 1. A centred box has the obvious numbers; two boxes are two components; a speck is not an island.
    img = np.zeros((280, 220, 4), dtype=np.float32)
    img[28:252, 66:154, 3] = 1.0
    img[28:252, 66:154, :3] = 0.5
    m = metrics(img)
    if abs(m["top"] - 0.1) > 1e-6 or abs(m["bottom"] - 0.9) > 1e-6 or abs(m["head_cx"] - 0.5) > 1e-6:
        failures.append(f"box metrics wrong: {m}")
    if m["islands"] != 0:
        failures.append("a single box reported islands")
    img[10:20, 10:20, 3] = 1.0               # 100 px island (> 0.05 % of 61,600 = 31 px)
    if metrics(img)["islands"] != 1:
        failures.append("an island was not counted")
    img[10:20, 10:20, 3] = 0.0
    img[5:7, 5:7, 3] = 1.0                   # a 4 px speck: below the floor
    if metrics(img)["islands"] != 0:
        failures.append("a speck was counted as an island")
    # 2. Components: an L and a dot.
    mk = np.zeros((10, 10), dtype=bool)
    mk[1:8, 1:3] = True
    mk[6:8, 1:7] = True
    mk[0, 9] = True
    lab, sizes = components(mk)
    if len(sizes) != 2 or sizes[0] != int(mk[1:8, 1:3].sum() + mk[6:8, 3:7].sum()) or sizes[1] != 1:
        failures.append(f"components wrong: {sizes}")
    if lab[0, 9] != 2 or lab[7, 6] != 1:
        failures.append("component labels not ranked largest first")
    # 3. A soft fringe raises `soft`; a green fringe on a grey figure raises `halo`.
    img = np.zeros((280, 220, 4), dtype=np.float32)
    img[28:252, 66:154, 3] = 1.0
    img[28:252, 66:154, :3] = 0.5
    hard = metrics(img)
    img[28:252, 154:160, 3] = 0.5
    img[28:252, 154:160, :3] = 0.5
    softer = metrics(img)
    if not softer["soft"] > hard["soft"]:
        failures.append("a soft fringe did not raise `soft`")
    img[28:252, 154:160, :3] = (0.0, 1.0, 0.0)
    if not metrics(img)["halo"] > softer["halo"] + 0.5:
        failures.append("a green fringe did not raise `halo`")
    # 4. The references all measure, and the envelope has every key.
    rows = [(os.path.basename(p), metrics(load(p))) for p in references()[:3]]
    env = envelope(rows)
    if set(env) != set(KEYS):
        failures.append("envelope keys drifted")
    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else "OK (4 checks)")
    return 1 if failures else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--refs" in argv:
        rows = [(os.path.basename(p).replace(".png", ""), metrics(load(p))) for p in references()]
        print(table(rows))
        env = envelope(rows)
        print()
        print(f"{'metric':<12}{'min':>9}{'p10':>9}{'median':>9}{'p90':>9}{'max':>9}")
        for k, v in env.items():
            print(f"{k:<12}{v['min']:>9.3f}{v['p10']:>9.3f}{v['median']:>9.3f}{v['p90']:>9.3f}{v['max']:>9.3f}")
        out = os.path.join(ROOT, ENVELOPE)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        with open(out, "w", encoding="utf-8") as f:
            json.dump(dict(measured="2026-11-07", references=[n for n, _ in rows], size=SIZE, thumb=THUMB,
                           envelope=env, portraits={n: m for n, m in rows}), f, indent=1)
        print("\nwrote", ENVELOPE)
        return 0
    paths = [a for a in argv if not a.startswith("--")]
    if not paths:
        print(__doc__)
        return 2
    print(table([(os.path.basename(p), metrics(load(p))) for p in paths]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
