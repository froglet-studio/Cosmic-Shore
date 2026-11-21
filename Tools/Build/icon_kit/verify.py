"""The legibility gates for ability-row icons (Docs/Icons/ABILITY_ICON_SKILL_PLAN.md §6).

    python3 Tools/Build/icon_kit/verify.py Assets/_Graphics/Icons/AbilityIcons/Manta/*.png   # report + exit 1 on a fail
    python3 Tools/Build/icon_kit/verify.py --table Assets/_Graphics/Icons/AbilityIcons/*/*.png  # the numbers, no verdict
    python3 Tools/Build/icon_kit/verify.py --self-test                                       # negative controls

Every gate is a MEASUREMENT of the 128 px sprite, so it runs on hand-drawn and generated art alike. The thresholds
were calibrated on 2026-10-24 against the fleet (verify.py --table over all 55): the sets Garrett called decent
(Thresher, Shrike, Starlings) pass; the ones that do not read fail on the gate that names WHY.
numpy only - no scipy, so the morphology is plain slicing.
"""
import os
import sys

import numpy as np
from PIL import Image

SIZE = 128
MARGIN = 6                  # the optical box is 116 px: 6 .. 122 (the fleet draws to ~4; the lockup scales by authored size)
BOX_MIN_FILL = 0.80         # the silhouette's long axis fills >= 80 % of the optical box
COVERAGE = (0.10, 0.45)     # alpha mass as a fraction of the canvas (the Thresher set: 0.12-0.16; a blob: > 0.45)
THIN_K = 2                  # opening radius: features under 2*K+1 = 5 px (2.3 px at 60) are "thin"
THIN_MAX = 0.35             # share of the mass that is thin
GAP_K = 2                   # background features under 5 px are "tight gaps"
GAP_MAX = 0.25              # share of the silhouette's BOUNDARY that borders a tight gap
MAX_COMPONENTS = 4
DUST = 4                    # a component under 4 px is anti-aliasing dust, ignored
SPECK = 0.015               # a component under 1.5 % of the mass is a speck (a ray, a dot, a mote)
AT40_KEEP = 0.85            # mass retained when drawn at 40 px (relative coverage)
PAIR_IOU_MAX = 0.75         # two icons of one hull must differ at 24 px


def load_alpha(path):
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im, dtype=np.float64)[..., 3] / 255.0
    rgb = np.asarray(im)[..., :3]
    return a, rgb, im.size


def _erode(m, k):
    out = m.copy()
    for _ in range(k):
        p = np.pad(out, 1, constant_values=False)
        out = (p[1:-1, 1:-1] & p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
               & p[:-2, :-2] & p[:-2, 2:] & p[2:, :-2] & p[2:, 2:])
    return out


def _dilate(m, k):
    out = m.copy()
    for _ in range(k):
        p = np.pad(out, 1, constant_values=False)
        out = (p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
               | p[:-2, :-2] | p[:-2, 2:] | p[2:, :-2] | p[2:, 2:])
    return out


def opening(m, k):
    return _dilate(_erode(m, k), k)


def components(m):
    """4-connected labelling; returns a list of component sizes, largest first."""
    h, w = m.shape
    seen = np.zeros_like(m, dtype=bool)
    sizes = []
    ys, xs = np.nonzero(m)
    for y0, x0 in zip(ys, xs):
        if seen[y0, x0]:
            continue
        stack = [(y0, x0)]
        seen[y0, x0] = True
        n = 0
        while stack:
            y, x = stack.pop()
            n += 1
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < h and 0 <= xx < w and m[yy, xx] and not seen[yy, xx]:
                    seen[yy, xx] = True
                    stack.append((yy, xx))
        sizes.append(n)
    return sorted(sizes, reverse=True)


def measure(path):
    a, rgb, size = load_alpha(path)
    m = a >= 0.5
    mass = float(m.sum())
    r = {"name": os.path.basename(path)[:-4], "size": size, "mass": mass}
    r["mass_px"] = int(mass)
    r["white"] = bool(size == (SIZE, SIZE)) and bool((rgb[a > 0.02] >= 250).all()) if mass else False
    r["coverage"] = mass / float(a.size)
    if mass == 0:
        r.update(bbox=(0, 0, 0, 0), box_fill=0.0, in_box=False, thin=1.0, gap=0.0,
                 components=[], at40=0.0)
        return r
    ys, xs = np.nonzero(a > 0.1)
    bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    r["bbox"] = bbox
    long_axis = max(bbox[2] - bbox[0], bbox[3] - bbox[1])
    r["box_fill"] = long_axis / float(SIZE - 2 * MARGIN)
    r["in_box"] = bbox[0] >= MARGIN - 1 and bbox[1] >= MARGIN - 1 and bbox[2] <= SIZE - MARGIN + 1 and bbox[3] <= SIZE - MARGIN + 1
    thin = m & ~opening(m, THIN_K)
    r["thin"] = float(thin.sum()) / mass
    # Tight gaps: background inside the bounding box that an opening of GAP_K removes, counted as the
    # share of the ICON mass that borders such a gap (so one hairline slit between two slabs counts).
    bg = ~m
    tight = bg & ~opening(bg, GAP_K)
    tight[:bbox[1], :] = False
    tight[bbox[3]:, :] = False
    tight[:, :bbox[0]] = False
    tight[:, bbox[2]:] = False
    boundary = m & ~_erode(m, 1)
    r["gap"] = float((boundary & _dilate(tight, 1)).sum()) / max(1.0, float(boundary.sum()))
    r["components"] = [c for c in components(m) if c >= DUST]
    small = Image.fromarray((a * 255).astype(np.uint8)).resize((40, 40), Image.LANCZOS)
    a40 = np.asarray(small, dtype=np.float64) / 255.0
    m40 = a40 >= 0.5
    r["at40"] = (float(m40.sum()) / m40.size) / r["coverage"]
    r["components40"] = components(m40)
    return r


def judge(r):
    """Returns the list of failed gate names (empty = passes)."""
    f = []
    if not r["white"]:
        f.append("white128")
    if not (COVERAGE[0] <= r["coverage"] <= COVERAGE[1]):
        f.append("coverage")
    if r["box_fill"] < BOX_MIN_FILL:
        f.append("box_fill")
    if not r["in_box"]:
        f.append("in_box")
    if r["thin"] > THIN_MAX:
        f.append("thin")
    if r["gap"] > GAP_MAX:
        f.append("gap")
    big = [c for c in r["components"] if c >= SPECK * r["mass"]] if r["mass"] else []
    if len(big) > MAX_COMPONENTS:
        f.append("components")
    if len(r["components"]) != len(big):
        f.append("specks")
    if r["at40"] < AT40_KEEP:
        f.append("at40")
    return f


def pair_iou(path_a, path_b, px=24):
    def mask(p):
        a, _, _ = load_alpha(p)
        im = Image.fromarray((a * 255).astype(np.uint8)).resize((px, px), Image.LANCZOS)
        return np.asarray(im) >= 128
    a, b = mask(path_a), mask(path_b)
    u = float((a | b).sum())
    return float((a & b).sum()) / u if u else 0.0


def judge_family(paths):
    """The icons of one hull must be told apart at 24 px."""
    fails = []
    for i in range(len(paths)):
        for j in range(i + 1, len(paths)):
            iou = pair_iou(paths[i], paths[j])
            if iou > PAIR_IOU_MAX:
                fails.append((os.path.basename(paths[i]), os.path.basename(paths[j]), iou))
    return fails


def fmt(r, fails):
    comps = "+".join(str(c) for c in r["components"][:5]) + ("…" if len(r["components"]) > 5 else "")
    return (f"{r['name']:<34} cov {r['coverage']:.2f}  box {r['box_fill']:.2f}{'' if r['in_box'] else '!'}  "
            f"thin {r['thin']:.2f}  gap {r['gap']:.2f}  comps {len(r['components'])}[{comps}]  at40 {r['at40']:.2f}  "
            f"{'ok' if not fails else 'FAIL ' + ','.join(fails)}")


def run(paths, table=False, family=True):
    bad = 0
    for p in paths:
        r = measure(p)
        fails = [] if table else judge(r)
        bad += bool(fails)
        print(fmt(r, fails))
    if family and not table:
        by_dir = {}
        for p in paths:
            by_dir.setdefault(os.path.dirname(p), []).append(p)
        for d, ps in by_dir.items():
            for a, b, iou in judge_family(ps):
                print(f"FAIL distinct: {a} vs {b} IoU {iou:.2f} > {PAIR_IOU_MAX}")
                bad += 1
    return bad


def self_test():
    """Negative controls: a synthetic icon per gate, each must fail exactly that gate."""
    import tempfile
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import canvas as C
    cases = {
        "coverage": C.disc(64, 64, 10),                                   # a dot
        "box_fill": C.disc(64, 64, 40),                                   # a disc that fills 69 % of the box
        "in_box": C.rect(2, 20, 126, 108),                                # overflows the margin
        "thin": C.ring(64, 64, 48, 3) | C.ring(64, 64, 36, 3) | C.ring(64, 64, 24, 3) | C.ring(64, 64, 12, 3),   # hairline rings
        "gap": C.rect(8, 8, 62, 120) | C.rect(65, 8, 120, 120),           # two slabs with a 3 px slit
        "components": C.empty(),
        "specks": C.rect(14, 30, 114, 98) | C.disc(20, 110, 2.5),         # a slab and a speck
    }
    for i in range(7):
        cases["components"] |= C.disc(20 + i * 14, 64, 6.5)              # seven dots
    ok = True
    with tempfile.TemporaryDirectory() as td:
        for gate, mask in cases.items():
            p = os.path.join(td, f"{gate}.png")
            C.render(mask).save(p)
            fails = judge(measure(p))
            hit = gate in fails
            print(f"self-test {gate:<11} {'fires' if hit else 'DID NOT FIRE'}  ({', '.join(fails) or 'passes'})")
            ok &= hit
        good = C.rect(10, 44, 118, 92) | C.fill([(10, 44), (64, 8), (118, 44)])
        p = os.path.join(td, "good.png")
        C.render(good).save(p)
        fails = judge(measure(p))
        print(f"self-test positive    {'passes' if not fails else 'FAILS ' + ','.join(fails)}")
        ok &= not fails
    return 0 if ok else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    table = "--table" in argv
    paths = [a for a in argv if a.endswith(".png")]
    if not paths:
        print(__doc__)
        return 2
    bad = run(paths, table=table)
    if not table:
        print(f"{len(paths) - bad}/{len(paths)} pass")
    return 1 if bad and not table else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
