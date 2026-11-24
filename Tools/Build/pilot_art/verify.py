#!/usr/bin/env python3
"""The GATES for a pilot portrait: measurements of the 220x280 sprite against the painted set's envelope.

Every gate is a number from measure.py, calibrated on 2026-11-07 against the 32 references
(Docs/Pilots/art/style_envelope.json): the thresholds sit just outside the range the set spans, so a new portrait
fails only on what would make it the odd one out in the Hangar. Two references fail on purpose and prove the gates
measure: Dozer (the crouching frog, height 0.671) and Echo (the winged mouse, 0.739) fail `height` and `top`.

Gates are a FLOOR, not a judgement (the icon kit's lesson 3): the sheet, read by a person, is the judgement.
A spec may exempt a gate with a reason (exempt=(("width", "the wings"),)); the sheet prints the exemption.

    python3 Tools/Build/pilot_art/verify.py <png> [<png>...]        # report, exit 1 on any fail
    python3 Tools/Build/pilot_art/verify.py --table <png> [...]     # the numbers, no verdict
    python3 Tools/Build/pilot_art/verify.py --refs                  # the 32 references (30 pass, Dozer and Echo fail height)
    python3 Tools/Build/pilot_art/verify.py --self-test             # negative controls: every gate fires on a planted defect
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import measure  # noqa: E402

SIZE = measure.SIZE

# name -> (test(metrics) -> bool, the rule in words with the set's numbers)
GATES = [
    ("size", lambda m: (m["w"], m["h"]) == SIZE, f"{SIZE[0]}x{SIZE[1]} RGBA"),
    ("height", lambda m: 0.85 <= m["height"] <= 1.0, "silhouette height 0.85-1.00 of the frame (set p10 0.874; Dozer 0.671 and Echo 0.739 are the outliers)"),
    ("bottom", lambda m: m["bottom"] >= 0.98, "boots on the bottom edge: bottom >= 0.98 (set min 0.989)"),
    ("top", lambda m: m["top"] <= 0.15, "head near the top: top <= 0.15 (set p90 0.126)"),
    ("centred", lambda m: abs(m["head_cx"] - 0.5) <= 0.20 and abs(m["torso_cx"] - 0.5) <= 0.20,
     "head and torso centres within 0.5 +/- 0.20 (set: head 0.32-0.62, torso 0.31-0.57; the winged and the shelled sit left)"),
    ("width", lambda m: 0.35 <= m["width"] <= 1.0, "silhouette width 0.35-1.00 (set 0.40-0.99)"),
    ("coverage", lambda m: 0.15 <= m["coverage"] <= 0.55, "silhouette covers 0.15-0.55 of the frame (set 0.167-0.531)"),
    ("alpha_clean", lambda m: m["islands"] == 0 and m["soft"] <= 3.0, "no specks (measure.py islands); soft edge <= 3.0 px per edge px (set max 2.5)"),
    ("no_halo", lambda m: m["halo"] <= 0.15, "no coloured fringe: halo <= 0.15 (set max 0.111)"),
    ("value", lambda m: 0.10 <= m["lum_p50"] <= 0.60 and m["lum_p95"] >= 0.50 and m["lum_p5"] <= 0.30,
     "median luminance 0.10-0.60, a highlight (p95 >= 0.50), a shadow (p5 <= 0.30) (set 0.135-0.534 / >= 0.544 / <= 0.252)"),
    ("saturation", lambda m: 0.10 <= m["sat"] <= 0.75, "mean saturation 0.10-0.75 (set 0.116-0.687)"),
    ("accent", lambda m: 0.01 <= m["accent"] <= 0.70, "saturated share 0.01-0.70 (set 0.016-0.659): one accent, not a rainbow"),
    ("thumb", lambda m: m["thumb_keep"] >= 0.95, "the figure survives the Hangar's 44x56: thumb_keep >= 0.95 (set min 0.979)"),
    ("upright", lambda m: (m["torso_cx"] - m["torso_w"] / 2 - 0.2) <= m["head_cx"] <= (m["torso_cx"] + m["torso_w"] / 2 + 0.2),
     "the head sits over the body: head centre within the torso span widened by 0.2"),
]
NAMES = [g[0] for g in GATES]


def verify(img, exempt=()):
    """img: RGBA float array or a path. Returns (ok, [(gate, passed, exempt_reason or None)], metrics)."""
    if isinstance(img, str):
        img = measure.load(img)
    m = measure.metrics(img)
    ex = dict(exempt)
    for gate, reason in ex.items():
        if gate not in NAMES:
            raise ValueError(f"exemption names no gate: {gate!r} (gates: {', '.join(NAMES)})")
        if not str(reason).strip():
            raise ValueError(f"exemption of {gate!r} gives no reason")
    rows = []
    ok = True
    for name, test, _ in GATES:
        if m.get("empty"):
            passed = name == "size" and (m["w"], m["h"]) == SIZE
        else:
            passed = bool(test(m))
        reason = ex.get(name)
        if not passed and reason is None:
            ok = False
        rows.append((name, passed, reason))
    return ok, rows, m


def report(path, exempt=()):
    ok, rows, m = verify(path, exempt)
    fails = [f"{n}{' (exempt: ' + r + ')' if r else ''}" for n, p, r in rows if not p]
    print(f"{'PASS' if ok else 'FAIL'}  {os.path.basename(path)}" + (f"  fails: {', '.join(fails)}" if fails else ""))
    return ok


def rules():
    return "\n".join(f"  {n:<12} {words}" for n, _, words in GATES)


def self_test():
    failures = []

    def figure(h_frac=0.985, bottom=0.995, cx=0.49, width=0.4, lum=(0.1, 0.3, 0.8), sat_tint=(0.6, 0.5, 0.4)):
        W, H = SIZE
        img = np.zeros((H, W, 4), dtype=np.float32)
        fh = int(round(h_frac * H))
        y1 = int(round(bottom * H))
        y0 = y1 - fh
        fw = int(round(width * W))
        x0 = int(round(cx * W - fw / 2))
        img[y0:y1, x0:x0 + fw, 3] = 1.0
        # A gradient so there is a shadow, a mid and a highlight, and one saturated trim band (the accent).
        g = np.linspace(lum[0], lum[2], fh)[:, None]
        base = np.array(sat_tint, dtype=np.float32)[None, None, :]
        img[y0:y1, x0:x0 + fw, :3] = np.clip(base * g[..., None] / 0.5, 0, 1)
        band_y = y0 + fh // 2
        img[band_y:band_y + max(2, fh // 20), x0:x0 + fw, :3] = (0.85, 0.05, 0.75)
        return img

    good = figure()
    ok, rows, m = verify(good)
    if not ok:
        failures.append("the control figure fails: " + ", ".join(n for n, p, _ in rows if not p))
    plants = {
        "height": figure(h_frac=0.5, bottom=0.995),
        "bottom": figure(h_frac=0.8, bottom=0.85),
        "top": figure(h_frac=0.7, bottom=0.995),
        "centred": figure(cx=0.25),
        "width": figure(width=0.2),
        "coverage": figure(width=0.9, h_frac=0.9),
        "value": figure(lum=(0.3, 0.35, 0.4)),
        "saturation": figure(sat_tint=(0.5, 0.5, 0.5)),
    }
    for gate, img in plants.items():
        _, rows, _ = verify(img)
        if dict((n, p) for n, p, _ in rows)[gate]:
            failures.append(f"planted defect not caught by {gate}")
    # Specks -> alpha_clean; a green fringe -> no_halo; a 1-px line -> thumb; wrong size -> size; a head off the body -> upright.
    specks = good.copy()
    for y, x in ((5, 5), (20, 200), (270, 10)):
        specks[y:y + 6, x:x + 6, 3] = 1.0
    if dict((n, p) for n, p, _ in verify(specks)[1])["alpha_clean"]:
        failures.append("specks not caught by alpha_clean")
    fringe = good.copy()
    m_ = fringe[..., 3] > 0.5
    pad = np.pad(m_, 1)
    edge = m_ & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    ring = np.pad(edge, 1)[2:, 1:-1] & ~m_
    fringe[ring, 3] = 0.5
    fringe[ring, :3] = (0.0, 1.0, 0.0)
    if dict((n, p) for n, p, _ in verify(fringe)[1])["no_halo"]:
        failures.append("a green fringe not caught by no_halo")
    line = np.zeros((SIZE[1], SIZE[0], 4), dtype=np.float32)
    line[3:279, 108:109, 3] = 1.0
    line[3:279, 108:109, :3] = 0.5
    if dict((n, p) for n, p, _ in verify(line)[1])["thumb"]:
        failures.append("a hairline figure not caught by thumb")
    small = np.zeros((100, 100, 4), dtype=np.float32)
    if dict((n, p) for n, p, _ in verify(small)[1])["size"]:
        failures.append("a wrong size not caught")
    off = good.copy()
    off[:, :, 3] = 0.0
    off[150:278, 20:120, 3] = 1.0           # a body at the left
    off[3:40, 150:200, 3] = 1.0             # a head far to the right
    off[..., :3] = good[..., :3]
    if dict((n, p) for n, p, _ in verify(off)[1])["upright"]:
        failures.append("a head off the body not caught by upright")
    # An exemption must name a real gate and carry a reason.
    for bad in ((("widht", "typo"),), (("width", " "),)):
        try:
            verify(good, exempt=bad)
            failures.append(f"a bad exemption was accepted: {bad}")
        except ValueError:
            pass
    # An exemption turns a fail into a pass with its reason kept.
    ok2, rows2, _ = verify(plants["width"], exempt=(("width", "a test"),))
    if not ok2 or not any(n == "width" and r == "a test" for n, _, r in rows2):
        failures.append("an exemption did not pass with its reason")
    # The references: 30 of 32 pass; Dozer (crouching) and Echo (winged) are short, so they fail `height` and `top`.
    expected = {"Portrait_Dozer_Placeholder.png": ["height", "top"], "Portrait_Echo_Placeholder.png": ["height", "top"]}
    for p in measure.references():
        _, rows3, _ = verify(p)
        fails = [n for n, ok3, _ in rows3 if not ok3]
        if fails != expected.get(os.path.basename(p), []):
            failures.append(f"{os.path.basename(p)}: fails {fails}, expected {expected.get(os.path.basename(p), [])}")
    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else f"OK ({len(GATES)} gates, {len(plants) + 5} planted defects, 32 references)")
    return 1 if failures else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--rules" in argv:
        print(rules())
        return 0
    paths = measure.references() if "--refs" in argv else [a for a in argv if not a.startswith("--")]
    if not paths:
        print(__doc__)
        print(rules())
        return 2
    if "--table" in argv:
        print(measure.table([(os.path.basename(p), measure.metrics(measure.load(p))) for p in paths]))
        return 0
    bad = sum(0 if report(p) else 1 for p in paths)
    print(f"{len(paths) - bad} pass, {bad} fail")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
