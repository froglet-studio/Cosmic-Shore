#!/usr/bin/env python3
"""Colour sets (unlockable palettes): each domain is two pigments, its rim and its face, and the rules every set keeps.

THE DEFINITIONS are Docs/VesselModels/colour_sets.json (first set: Dart Frog, 2026-11-02, Jade from Garrett's photo of a
green-and-bronze Dendrobates auratus). They are shaped in the Colour Sets lab (Docs/Studios/ColourSetsLab.html), which
embeds the same JSON. This tool READS them and writes nothing. The six-family truth (colour_domains.json) is untouched;
a set only borrows it to prove each rim still reads as its own family.

THE RULE (OKLab, x = [L, a, b]): lighting scales a colour's linear RGB, which moves it along a ray from black in OKLab,
so a PIGMENT is one paint from shadow to highlight: the colours with L0 <= L <= L1 whose chroma direction (a/L, b/L)
lies within rho of sat * (cos hue, sin hue). A domain is its rim pigment plus its face pigment. Every set keeps:
  1. the band: pigments of different domains are at least `margin` apart in OKLab (exact: at fixed L a pigment is a
     disc, so the gap between two is min over L_a, L_b of hypot(L_a - L_b, |c_a L_a - c_b L_b| - rho_a L_a - rho_b L_b));
  2. lighting-proof: their chroma directions are at least `pigment_gap` apart beyond both rhos, so no light or shadow
     turns one domain's pigment into another's;
  3. contrast: a domain's rim and face differ by `min_hue_contrast` degrees of hue and `min_lightness_contrast` of mid L;
  4. identity: each rim's swatch rests in its own family of the colour_domains.json truth, clear of the band;
  and each swatch is the pigment at its mid L, on screen (sRGB).
A set made for a VISION (its `vision` key: typical, protan, deutan, tritan, achromat, low_vision; VISIONS says how each
is modelled) also keeps
  5. communication: as that viewer sees the colours the palette asset actually paints (a rim at its brightest channel
     1, a face at its pigment's mid L) under the same light, every two domains' rims, every two domains' faces, and
     each domain's rim and face are at least `vision_margin` apart in OKLab (0.02 is about one just-noticeable step).

    python3 Tools/Build/colour_sets.py classify dart_frog '#85D0B1' '#76401B'   # which domain and pigment, if any
    python3 Tools/Build/colour_sets.py --report        # every pigment, the closest pairs, colour-blind separation
    python3 Tools/Build/colour_sets.py --check         # exit 1 on any broken rule, a stale swatch or a stale lab
    python3 Tools/Build/colour_sets.py --self-test     # proves --check names each failure it exists for
"""
import itertools
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import colour_domains  # noqa: E402  (the six-family truth, for rule 4)

DEFS = os.path.join(REPO, "Docs", "VesselModels", "colour_sets.json")
LAB = os.path.join(REPO, "Docs", "Studios", "ColourSetsLab.html")
DOMAINS = colour_domains.FAMILIES      # fire, gold, lime, jade, blue, ruby
ROLES = ("rim", "face")
GRID = 48                              # the gap search: a GRID x GRID sweep of (L_a, L_b), then local refinement
CVD = {   # Machado, Oliveira and Fernandes 2009, severity 1, on linear sRGB (the matrices the Colour Domains lab uses)
    "protan": ((0.152286, 1.052583, -0.204868), (0.114503, 0.786281, 0.099216), (-0.003882, -0.048116, 1.051998)),
    "deutan": ((0.367322, 0.860646, -0.227968), (0.280085, 0.672501, 0.047413), (-0.011820, 0.042940, 0.968881)),
    "tritan": ((1.255528, -0.076749, -0.178779), (-0.078411, 0.930809, 0.147602), (0.004733, 0.691367, 0.303900)),
}


def load(path=DEFS):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


# ----------------------------------------------------------------------------- colour maths
def to_linear(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def to_gamma(v):
    v = min(1.0, max(0.0, v))
    return 12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055


def oklab_linear(r, g, b):
    l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b
    m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b
    s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b
    l, m, s = (math.copysign(abs(v) ** (1 / 3), v) for v in (l, m, s))
    return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s)


def oklab(rgb):
    return oklab_linear(*(to_linear(min(1.0, max(0.0, v))) for v in rgb))


def linear_of(L, a, b):
    l, m, s = ((L + 0.3963377774 * a + 0.2158037573 * b) ** 3, (L - 0.1055613458 * a - 0.0638541728 * b) ** 3,
               (L - 0.0894841775 * a - 1.2914855480 * b) ** 3)
    return (4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s)


def in_gamut(lin, tol=1e-4):
    return all(-tol <= v <= 1 + tol for v in lin)


def hexof(rgb):
    return "#%02X%02X%02X" % tuple(max(0, min(255, round(v * 255))) for v in rgb)


def rgb_of_hex(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def hue_gap(h1, h2):
    return abs((h1 - h2 + 180) % 360 - 180)


# ----------------------------------------------------------------------------- pigments
def direction(p):
    """The pigment's chroma direction (a/L, b/L)."""
    h = math.radians(p["hue"])
    return p["sat"] * math.cos(h), p["sat"] * math.sin(h)


def at(p, L):
    """The pigment at lightness L, as OKLab."""
    ca, cb = direction(p)
    return L, ca * L, cb * L


def mid(p):
    return (p["L"][0] + p["L"][1]) / 2


def swatch(p):
    """The pigment at its mid L, on screen: (sRGB, whether it fits the gamut)."""
    lin = linear_of(*at(p, mid(p)))
    return tuple(to_gamma(v) for v in lin), in_gamut(lin)


def gamut_share(p, n=24):
    """Share of the pigment's centre ray that fits on screen (how much of the pigment a palette can use)."""
    L0, L1 = p["L"]
    return sum(in_gamut(linear_of(*at(p, L0 + (L1 - L0) * i / (n - 1)))) for i in range(n)) / n


def pigments(s):
    """(domain, role, pigment) for every pigment of a set, in domain order."""
    return [(d, r, s["domains"][d][r]) for d in DOMAINS for r in ROLES]


def contains(p, lab):
    L, a, b = lab
    if not (p["L"][0] <= L <= p["L"][1]) or L <= 1e-6:
        return False
    ca, cb = direction(p)
    return math.hypot(a / L - ca, b / L - cb) <= p["rho"]


def classify(s, rgb):
    """[(domain, role)] for every pigment of the set that holds this colour (empty when the set leaves it unclaimed)."""
    x = oklab(rgb)
    return [(d, r) for d, r, p in pigments(s) if contains(p, x)]


def _disc_gap(p, q, La, Lb, ca, cq):
    planar = math.hypot(ca[0] * La - cq[0] * Lb, ca[1] * La - cq[1] * Lb) - p["rho"] * La - q["rho"] * Lb
    return math.hypot(La - Lb, max(0.0, planar))


def gap(p, q):
    """The OKLab distance between two pigments (0 when they touch or overlap)."""
    ca, cq = direction(p), direction(q)
    (a0, a1), (b0, b1) = p["L"], q["L"]
    best = (math.inf, a0, b0)
    for i in range(GRID + 1):
        La = a0 + (a1 - a0) * i / GRID
        for j in range(GRID + 1):
            Lb = b0 + (b1 - b0) * j / GRID
            d = _disc_gap(p, q, La, Lb, ca, cq)
            if d < best[0]:
                best = (d, La, Lb)
    step_a, step_b = (a1 - a0) / GRID, (b1 - b0) / GRID
    for _ in range(30):
        d0, La, Lb = best
        for da, db in ((step_a, 0), (-step_a, 0), (0, step_b), (0, -step_b)):
            na, nb = min(a1, max(a0, La + da)), min(b1, max(b0, Lb + db))
            d = _disc_gap(p, q, na, nb, ca, cq)
            if d < best[0]:
                best = (d, na, nb)
        if best[0] == d0:
            step_a, step_b = step_a / 2, step_b / 2
    return best[0]


def pigment_gap(p, q):
    """How far apart two pigments' chroma directions are beyond both rhos (C/L units)."""
    ca, cq = direction(p), direction(q)
    return math.hypot(ca[0] - cq[0], ca[1] - cq[1]) - p["rho"] - q["rho"]


# ----------------------------------------------------------------------------- rule 5: what a viewer can tell apart
VISIONS = {   # how each vision is modelled, on the colour's linear RGB (the words the sets and the lab show)
    "typical": "typical trichromat vision (OKLab as is)",
    "protan": "protanopia, no long-wave (red) cones: Machado 2009, severity 1",
    "deutan": "deuteranopia, no medium-wave (green) cones: Machado 2009, severity 1",
    "tritan": "tritanopia, no short-wave (blue) cones: Machado 2009, severity 1",
    "achromat": "achromatopsia (rod monochromacy), no colour at all: relative luminance only (Rec. 709 Y)",
    "low_vision": "low vision (reduced contrast sensitivity, cataract): lightness kept, chromatic contrast halved",
}


def see_linear(lin, vision):
    """OKLab of a linear-RGB colour as `vision` sees it."""
    lin = tuple(min(1.0, max(0.0, v)) for v in lin)
    if vision in CVD:
        m = CVD[vision]
        lin = tuple(min(1.0, max(0.0, sum(m[i][k] * lin[k] for k in range(3)))) for i in range(3))
    elif vision == "achromat":
        y = 0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2]
        lin = (y, y, y)
    L, a, b = oklab_linear(*lin)
    if vision == "low_vision":
        a, b = a * 0.5, b * 0.5
    return L, a, b


def painted(p, role):
    """The colour a palette asset paints with pigment `p`, linear RGB (author_colour_set_assets.py): a RIM keeps the
    original field's brightness, which is a brightest channel of about 1 (bloom-sized), so it is the pigment's centre
    ray at peak 1 and its lightness comes from its hue; a FACE is the pigment at its own mid L."""
    lin = tuple(max(0.0, v) for v in linear_of(*at(p, mid(p))))
    if role == "rim":
        peak = max(lin) or 1.0
        lin = tuple(v / peak for v in lin)
    return lin


def vision_separation(s, vision=None):
    """(the smallest OKLab distance, which pair) between what must stay apart, as `vision` sees the painted colours at
    the same light: every two domains' rims (the trail, the HUD, a prism's edge, danger and free pickups), every two
    domains' faces (a prism's body), and each domain's rim against its own face (the prism reads as rim over face)."""
    vision = vision or s.get("vision", "typical")
    seen = {(d, r): see_linear(painted(s["domains"][d][r], r), vision) for d in DOMAINS for r in ROLES}
    rows = [(math.dist(seen[(a, r)], seen[(b, r)]), "%s/%s %ss" % (a, b, r))
            for r in ROLES for a, b in itertools.combinations(DOMAINS, 2)]
    rows += [(math.dist(seen[(d, "rim")], seen[(d, "face")]), "%s rim/face" % d) for d in DOMAINS]
    return min(rows)


# ----------------------------------------------------------------------------- the check
def set_problems(key, s, truth):
    errs = []
    for k in ("margin", "pigment_gap", "min_hue_contrast", "min_lightness_contrast"):
        if not isinstance(s.get(k), (int, float)):
            return ["%s: needs %s" % (key, k)]
    if sorted(s.get("domains", {})) != sorted(DOMAINS):
        return ["%s: must define exactly %s" % (key, ", ".join(DOMAINS))]
    for d in DOMAINS:
        for r in ROLES:
            p = s["domains"][d].get(r)
            if not p or any(f not in p for f in ("name", "hue", "sat", "L", "rho", "swatch")):
                errs.append("%s: %s.%s needs name, hue, sat, L [L0, L1], rho and swatch" % (key, d, r))
            elif not 0 < p["L"][0] < p["L"][1] <= 1:
                errs.append("%s: %s.%s L must be 0 < L0 < L1 <= 1" % (key, d, r))
    if errs:
        return errs
    every = pigments(s)
    for d, r, p in every:
        rgb, fits = swatch(p)
        if not fits:
            errs.append("%s: %s %s %s does not fit on screen at its mid L (lower sat or move L)" % (key, d, r, p["name"]))
        if hexof(rgb) != p["swatch"].upper():
            errs.append("%s: %s %s %s swatch says %s but the pigment gives %s" % (key, d, r, p["name"], p["swatch"], hexof(rgb)))
    for (d1, r1, p), (d2, r2, q) in itertools.combinations(every, 2):
        if d1 == d2:
            continue
        g = gap(p, q)
        if g < s["margin"] - 1e-4:
            errs.append("%s: %s (%s %s) and %s (%s %s) are %.3f apart, inside the %.2f band"
                        % (key, p["name"], d1, r1, q["name"], d2, r2, g, s["margin"]))
        pg = pigment_gap(p, q)
        if pg < s["pigment_gap"] - 1e-4:
            errs.append("%s: %s (%s %s) and %s (%s %s) are one paint under different light (directions %.3f apart, need %.2f)"
                        % (key, p["name"], d1, r1, q["name"], d2, r2, pg, s["pigment_gap"]))
    if "vision" in s:
        if s["vision"] not in VISIONS:
            errs.append("%s: vision %r is not one of %s" % (key, s["vision"], ", ".join(VISIONS)))
        elif not isinstance(s.get("vision_margin"), (int, float)):
            errs.append("%s: a set made for a vision needs vision_margin (rule 5)" % key)
        else:
            sep, where = vision_separation(s)
            if sep < s["vision_margin"] - 1e-4:
                errs.append("%s: as %s sees it, %s are %.3f apart (rule 5 needs %.3f)"
                            % (key, s["vision"], where, sep, s["vision_margin"]))
    for d in DOMAINS:
        rim, face = s["domains"][d]["rim"], s["domains"][d]["face"]
        dh = hue_gap(rim["hue"], face["hue"])
        if dh < s["min_hue_contrast"]:
            errs.append("%s: %s rim and face are %.0f degrees of hue apart (need %d)" % (key, d, dh, s["min_hue_contrast"]))
        dl = abs(mid(rim) - mid(face))
        if dl < s["min_lightness_contrast"] - 1e-9:
            errs.append("%s: %s rim and face are %.2f of lightness apart (need %.2f)" % (key, d, dl, s["min_lightness_contrast"]))
        fam, top, dist = colour_domains.classify(truth, swatch(rim)[0])
        if fam != d:
            errs.append("%s: %s rim %s %s reads as %s in the six-family truth" % (key, d, rim["name"], hexof(swatch(rim)[0]),
                        top if fam else "the band (nearest %s, %.4f from its border)" % (top, dist)))
    return errs


def lab_truth(path=LAB):
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as fh:
        m = re.search(r"const SETS_RAW = (\{.*?\});\n", fh.read())
    if not m:
        raise SystemExit("colour_sets: %s has no SETS_RAW" % os.path.relpath(path, REPO))
    return json.loads(m.group(1))


def lab_problems(defs, lab):
    errs = []
    for key, s in defs["sets"].items():
        theirs = lab.get(key)
        if theirs is None:
            errs.append("the lab has no set %s (Docs/Studios/ColourSetsLab.html SETS_RAW)" % key)
            continue
        for k in ("margin", "pigment_gap", "min_hue_contrast", "min_lightness_contrast", "vision", "vision_margin"):
            if theirs.get(k) != s.get(k):
                errs.append("the lab's %s %s is %s, colour_sets.json says %s" % (key, k, theirs.get(k), s[k]))
        for d, r, p in pigments(s):
            q = theirs.get("domains", {}).get(d, {}).get(r, {})
            for f in ("hue", "sat", "L", "rho", "swatch", "name"):
                if q.get(f) != p[f]:
                    errs.append("the lab's %s %s.%s %s is %s, colour_sets.json says %s" % (key, d, r, f, q.get(f), p[f]))
    return errs


def problems(defs, truth=None, lab=False):
    truth = colour_domains.load() if truth is None else truth
    errs = []
    for key, s in defs.get("sets", {}).items():
        errs += set_problems(key, s, truth)
    lab = lab_truth() if lab is False else lab
    if lab is not None:
        errs += lab_problems(defs, lab)
    return errs


# ----------------------------------------------------------------------------- the report
def simulate(rgb, kind):
    lin = [to_linear(v) for v in rgb]
    m = CVD[kind]
    return oklab_linear(*(min(1.0, max(0.0, sum(m[i][k] * lin[k] for k in range(3)))) for i in range(3)))


def report(defs):
    truth = colour_domains.load()
    for key, s in defs["sets"].items():
        print("%s (%s track)" % (s["name"], s["track"]))
        for d, r, p in pigments(s):
            rgb, _ = swatch(p)
            fam, top, _ = colour_domains.classify(truth, rgb)
            dark, light = (hexof(tuple(to_gamma(v) for v in linear_of(*at(p, L)))) for L in p["L"])
            print("  %-4s %-4s %-10s %s  shadow %s highlight %s  hue %3d sat %.3f L %.2f-%.2f rho %.3f  on screen %3.0f%%  truth: %s"
                  % (d, r, p["name"], hexof(rgb), dark, light, p["hue"], p["sat"], p["L"][0], p["L"][1], p["rho"],
                     gamut_share(p) * 100, fam or "band"))
        rows = []
        for (d1, r1, p), (d2, r2, q) in itertools.combinations(pigments(s), 2):
            if d1 != d2:
                rows.append((gap(p, q), pigment_gap(p, q), p["name"], q["name"]))
        print("  closest pigments (band %.2f, lighting %.2f):" % (s["margin"], s["pigment_gap"]))
        for g, pg, a, b in sorted(rows)[:5]:
            print("    %-10s %-10s %.3f apart" % (a, b, g))
        for g, pg, a, b in sorted(rows, key=lambda t: t[1])[:3]:
            print("    %-10s %-10s directions %.3f apart" % (a, b, pg))
        for d in DOMAINS:
            rim, face = s["domains"][d]["rim"], s["domains"][d]["face"]
            print("  %-4s %s over %s: hue %3.0f deg apart, lightness %.2f" % (d, rim["name"], face["name"],
                  hue_gap(rim["hue"], face["hue"]), abs(mid(rim) - mid(face))))
        print("  colour-blind separation (OKLab, swatches; a domain is told apart by its rim OR its face):")
        for kind in ("typical",) + tuple(CVD):
            sim = {d: [oklab(swatch(s["domains"][d][r])[0]) if kind == "typical" else simulate(swatch(s["domains"][d][r])[0], kind)
                       for r in ROLES] for d in DOMAINS}
            worst = min(((max(math.dist(sim[a][0], sim[b][0]), math.dist(sim[a][1], sim[b][1])), a, b)
                         for a, b in itertools.combinations(DOMAINS, 2)))
            print("    %-7s closest domains %s/%s, %.3f apart (%.1f JND)" % (kind, worst[1], worst[2], worst[0], worst[0] / 0.02))
        if "vision" in s:
            print("  made for %s (rule 5 needs %.3f)" % (VISIONS[s["vision"]], s["vision_margin"]))
        print("  rule 5 measure, painted colours, rims alone and faces alone (what each viewer can tell apart):")
        for kind in VISIONS:
            sep, where = vision_separation(s, kind)
            print("    %-10s %.3f (%4.1f JND), closest: %s%s" % (kind, sep, sep / 0.02, where,
                  "   <- made for" if s.get("vision") == kind else ""))
    print("which set to wear, by vision (rule 5 measure, JND):")
    keys = list(defs["sets"])
    print("    %-10s " % "" + " ".join("%13s" % k[:13] for k in keys))
    for kind in VISIONS:
        print("    %-10s " % kind + " ".join("%13.1f" % (vision_separation(defs["sets"][k], kind)[0] / 0.02) for k in keys))


# ----------------------------------------------------------------------------- the self-test
def self_test():
    """Each failure --check exists to catch, planted in memory; every one must be named."""
    defs, truth, lab = load(), colour_domains.load(), lab_truth()
    assert not problems(defs, truth, lab=None), "the shipped definitions must pass first"
    cases = []

    def planted(fn):
        d = json.loads(json.dumps(defs))
        fn(d["sets"]["dart_frog"]["domains"])
        return d
    cases.append(("two domains overlap", planted(lambda D: D["ruby"]["face"].update(D["jade"]["face"])), "band"))
    cases.append(("one paint under different light", planted(lambda D: D["blue"]["face"].update(hue=125, sat=0.25, L=[0.30, 0.48])), "one paint"))
    cases.append(("rim and face share a hue", planted(lambda D: D["jade"]["face"].update(hue=150)), "degrees of hue"))
    cases.append(("rim and face share a lightness", planted(lambda D: D["fire"]["face"].update(L=[0.42, 0.62])), "of lightness"))
    cases.append(("a rim left its family", planted(lambda D: D["gold"]["rim"].update(hue=130)), "six-family truth"))
    cases.append(("a stale swatch", planted(lambda D: D["jade"]["rim"].update(swatch="#00D4FF")), "swatch says"))
    vision_key = next((k for k, s in defs["sets"].items() if "vision" in s), None)
    if vision_key:
        def vplanted(fn):
            d = json.loads(json.dumps(defs))
            fn(d["sets"][vision_key])
            return d
        cases.append(("a vision set its viewer cannot read", vplanted(lambda S: S.update(vision_margin=1.0)), "rule 5"))
        cases.append(("an unknown vision", vplanted(lambda S: S.update(vision="x-ray")), "is not one of"))
        cases.append(("a vision set with no margin", vplanted(lambda S: S.pop("vision_margin")), "needs vision_margin"))
    bad = 0
    for label, d, needle in cases:
        hit = any(needle in e for e in problems(d, truth, lab=None))
        print("  %-36s %s" % (label, "caught" if hit else "MISSED"))
        bad += not hit
    if lab is not None:
        lb = json.loads(json.dumps(lab))
        lb["dart_frog"]["domains"]["lime"]["rim"]["hue"] += 4
        hit = any("lab's" in e for e in problems(defs, truth, lab=lb))
        print("  %-36s %s" % ("the lab embeds different pigments", "caught" if hit else "MISSED"))
        bad += not hit
    return bad


def main(argv):
    defs = load()
    if argv[:1] == ["classify"]:
        s = defs["sets"][argv[1]]
        for h in argv[2:]:
            hits = classify(s, rgb_of_hex(h))
            print("%s  %s" % ("#" + h.lstrip("#").upper(), ", ".join("%s %s (%s)" % (d, r, s["domains"][d][r]["name"]) for d, r in hits)
                              or "unclaimed by %s" % s["name"]))
        return 0
    if "--report" in argv:
        report(defs)
        return 0
    if "--self-test" in argv:
        bad = self_test()
        print("colour_sets --self-test:", "ok" if not bad else "%d planted problem(s) missed" % bad)
        return 1 if bad else 0
    if "--check" in argv:
        errs = problems(defs)
        for e in errs:
            print("  " + e)
        print("colour_sets --check:", "ok" if not errs else "%d problem(s)" % len(errs))
        return 1 if errs else 0
    print(__doc__)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
