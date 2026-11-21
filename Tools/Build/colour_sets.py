#!/usr/bin/env python3
"""Colour sets: six domains, each a rim pigment and a face pigment, and the rules that keep every domain reading as its
four-letter name (Fire, Gold, Lime, Jade, Blue, Ruby) whatever the set's inspiration.

THE DEFINITIONS are Docs/VesselModels/colour_sets.json. They are shaped in the Colour Sets lab
(Docs/Studios/ColourSetsLab.html), which embeds the same JSON, and made with the /colour-set skill
(.claude/skills/colour-set/SKILL.md, Tools/Build/colour_set_maker.py). This tool READS them and writes nothing. The
six-family truth (colour_domains.json) is untouched; a set borrows it to prove each domain still reads as its family.

A PIGMENT (OKLab, x = [L, a, b]): lighting scales a colour's linear RGB, which moves it along a ray from black in OKLab,
so a pigment is one paint from shadow to highlight: the colours with L0 <= L <= L1 whose chroma direction (a/L, b/L) lies
within rho of sat * (cos hue, sin hue). A domain is its rim pigment (the prism's fresnel edge, the trail, the HUD, the
signals) plus its face pigment (the prism's body, most of what a player sees of it).

THE RULES every set keeps (Garrett, 2026-11-10: "Lime, Fire, Blue, Jade, Ruby, Gold, those must be distinguishable and
have rules that govern the territory of their regions in color space; it must be obvious to the player based on a
connection between their look and their four letter name"):
  1. the band: pigments of different domains are at least `margin` apart in OKLab (exact: at fixed L a pigment is a
     disc, so the gap between two is min over L_a, L_b of hypot(L_a - L_b, |c_a L_a - c_b L_b| - rho_a L_a - rho_b L_b));
  2. lighting-proof: their chroma directions are at least `pigment_gap` apart beyond both rhos, so no light or shadow
     turns one domain's pigment into another's;
  3. form: a domain's rim and face differ by `min_lightness_contrast` of mid L, so a prism reads as a solid (a set or
     its style may also ask for `min_hue_contrast` degrees of hue between them);
  4. the name: every rim reads as its own family of the colour_domains.json truth, clear of the band, in every way the
     game shows it: its swatch (the pigment at mid L), as LIGHT (its centre ray at brightest channel 1, the prism's
     edge and the crystals) and as PAINT (the same numbers read as sRGB: the HUD, the score panels and the vessel tails
     read TrailHighlightColor that way). Lime's rim also reads Lime at the free crystal's body brightness (DarkCTA);
  5. communication (a set made for a `vision` only): as that viewer sees the colours the palette asset paints (a rim at
     its brightest channel 1, as light and, the way the HUD shows it, as paint; a face at its pigment's mid L), every two
     domains' rims, every two domains' faces and each domain's rim and face are at least `vision_margin` apart in OKLab
     (0.02 is about one just-noticeable step);
  6. the voice: a face is most of a prism, so it says the domain's name or keeps quiet. Each face is one of
       family - its swatch rests in its own family of the truth, clear of the band;
       paint   - it is its rim's own paint: the two pigments share a direction (their cones overlap), so the face is the
                 rim in shadow, as Photic does;
       quiet   - its whole pigment lies at or below the night line (laws.night_line, L 0.40). Below it the truth holds only
                 Fire and Blue, so a dark face cannot name another domain; it reads as night under a rim that carries
                 the name, as Nocturne does.
     a set made for a vision keeps it as its viewer sees it (below);
  7. apart: as the game paints them, every two rims stay laws.rim_separation apart (OKLab, typical vision), both as
     light and as paint, so no two domains' edges, trails or HUD colours are confused.
A set made for another `vision` (protan, deutan, tritan, achromat, low_vision; Garrett, 2026-11-10: "we want the
colour-blind sets to fulfill their version of the same criteria") keeps rules 4 and 6 AS ITS VIEWER SEES THEM, judged
against the game's own colours as that viewer has learned them (the six-family truth's anchors through the same vision
model, `vision_prototypes`): a rim, as light and on the HUD, must look more like its own family's colours than like any
other family's (`name_margin`, default 0); a face is its family's (the same test), its rim's paint, or quiet in that
viewer's lightness. Rule 5 is its rule 7: its rims apart as light and on the HUD, for its viewer.
A set may name a `style` (colour_sets.json `styles`: Photic, Nocturne, Nacre), whose rules it keeps on top of these.
Each swatch is the pigment at its mid L, on screen (sRGB). `examples` are worked sets the skill teaches from; they keep
every rule and are never written to the game. PhoticColorSetSO, the hand-tuned original, is measured here as the
reference (`photic_reference`), not defined.

    python3 Tools/Build/colour_sets.py classify nocturne '#4BD5D4' '#480135'   # which domain and pigment, if any
    python3 Tools/Build/colour_sets.py --report        # every pigment, its readings and voice, the closest pairs
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
import colour_domains  # noqa: E402  (the six-family truth, for rules 4 and 6)

DEFS = os.path.join(REPO, "Docs", "VesselModels", "colour_sets.json")
LAB = os.path.join(REPO, "Docs", "Studios", "ColourSetsLab.html")
DOMAINS = colour_domains.FAMILIES      # fire, gold, lime, jade, blue, ruby
ROLES = ("rim", "face")
GRID = 48                              # the gap search: a GRID x GRID sweep of (L_a, L_b), then local refinement
LAWS = {"night_line": 0.40, "rim_separation": 0.10, "crystal_body": 0.5, "name_margin": 0.0}   # colour_sets.json `laws` wins
VOICES = ("family", "paint", "quiet")
STYLE_KEYS = {"name", "idea", "faces", "min_hue_contrast", "max_hue_contrast", "face_band", "rim_L", "face_L",
              "face_chroma", "rim_brightness"}
CVD = {   # Machado, Oliveira and Fernandes 2009, severity 1, on linear sRGB (the matrices the Colour Domains lab uses)
    "protan": ((0.152286, 1.052583, -0.204868), (0.114503, 0.786281, 0.099216), (-0.003882, -0.048116, 1.051998)),
    "deutan": ((0.367322, 0.860646, -0.227968), (0.280085, 0.672501, 0.047413), (-0.011820, 0.042940, 0.968881)),
    "tritan": ((1.255528, -0.076749, -0.178779), (-0.078411, 0.930809, 0.147602), (0.004733, 0.691367, 0.303900)),
}


def load(path=DEFS):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def laws(defs):
    return dict(LAWS, **defs.get("laws", {}))


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


# ----------------------------------------------------------------------------- rule 4: what the game shows of a rim
def unit_ray(p):
    """The pigment's centre ray in linear RGB at brightest channel 1: what the palette writer paints a rim field with,
    before the original field's brightness scales it."""
    lin = tuple(max(0.0, v) for v in linear_of(*at(p, mid(p))))
    peak = max(lin) or 1.0
    return tuple(v / peak for v in lin)


def readings(p, domain=None, lw=LAWS):
    """[(how, sRGB)]: every way the game shows a rim pigment. `light` is the centre ray at peak 1 as light (the prism's
    edge, a crystal); `paint` is the same numbers taken as sRGB, which is how the HUD, the score panels and the vessel
    tails read SO_ColorSet.TrailHighlightColor. Lime's rim also paints the free crystal's body (DarkCTA) at
    `crystal_body` of peak."""
    u = unit_ray(p)
    hud = tuple(p["hud"]) if "hud" in p else u     # a measured palette (Photic) tunes its HUD colour apart from its rim
    out = [("swatch", swatch(p)[0]), ("light", tuple(to_gamma(v) for v in u)), ("paint", hud)]
    if domain == "lime":
        out.append(("crystal body", tuple(to_gamma(v * lw["crystal_body"]) for v in u)))
    return out


def family_of(truth, rgb):
    """(family or None for the band, the family that scores highest, JND beyond the band's edge; negative inside it)."""
    fam, top, dist = colour_domains.classify(truth, tuple(min(1.0, max(0.0, v)) for v in rgb))
    return fam, top, (dist - truth["margin"] / 2) / 0.02


# ----------------------------------------------------------------------------- rule 6: the voice of a face
def voices(s, d, truth, lw=LAWS, vision=None):
    """Which of the three voices domain d's face keeps: family, paint, quiet (rule 6), as `vision` sees it (the set's
    own viewer by default; typical vision uses the six-family truth, any other vision the anchors it has learned)."""
    vision = vision or s.get("vision", "typical")
    rim, face = s["domains"][d]["rim"], s["domains"][d]["face"]
    out = []
    if vision == "typical":
        if family_of(truth, swatch(face)[0])[0] == d:
            out.append("family")
    elif name_margin(linear_of(*at(face, mid(face))), d, vision, truth)[0] >= 0:
        out.append("family")
    if math.dist(direction(face), direction(rim)) <= rim["rho"] + face["rho"] + 1e-9:
        out.append("paint")
    top = face["L"][1] if vision == "typical" else see_linear(linear_of(*at(face, face["L"][1])), vision)[0]
    if top <= lw["night_line"] + 1e-9:
        out.append("quiet")
    return out


# ----------------------------------------------------------------------------- rules 4 and 6 for another viewer
_PROTOS = {}


def vision_prototypes(truth, vision):
    """{family: [OKLab]}: each family's anchors in the six-family truth (the game's own colours of that family: the
    danger red, the CTA lime, the domains' UI and signal colours, the hull's neutrals, black and white for Blue) as
    `vision` sees them. A viewer learns a family's name from these colours, whatever their eyes make of them."""
    key = (id(truth), vision)
    if key not in _PROTOS:
        _PROTOS[key] = {f: [see_linear(tuple(to_linear(v) for v in a["rgb"]), vision)
                            for a in truth["families"][f]["anchors"]] for f in DOMAINS}
    return _PROTOS[key]


def name_margin(lin, d, vision, truth):
    """(how much nearer a linear colour, as `vision` sees it, sits to domain d's anchors than to any other family's
    (OKLab; positive: it looks like its own name), the family it is nearest otherwise)."""
    P = vision_prototypes(truth, vision)
    x = see_linear(lin, vision)
    own = min(math.dist(x, p) for p in P[d])
    other, who = min((math.dist(x, p), f) for f in DOMAINS if f != d for p in P[f])
    return other - own, who


def vision_readings(p):
    """[(how, linear RGB)]: a rim as the game shows it, as light (the prism edge, a crystal) and on the HUD (the same
    numbers read as sRGB), for a viewer model to see."""
    u = unit_ray(p)
    hud = tuple(p["hud"]) if "hud" in p else u
    return [("light", u), ("HUD", tuple(to_linear(v) for v in hud))]


# ----------------------------------------------------------------------------- rule 7: rims apart as painted
def rims_apart(s):
    """(the smallest OKLab distance between two domains' rims, which pair and reading): as light and as paint."""
    rows = []
    for how in ("light", "paint"):
        seen = {d: oklab(dict(readings(s["domains"][d]["rim"]))[how]) for d in DOMAINS}
        rows += [(math.dist(seen[a], seen[b]), "%s/%s rims as %s" % (a, b, how))
                 for a, b in itertools.combinations(DOMAINS, 2)]
    return min(rows)


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
    domains' faces (a prism's body), every two domains' rims as the HUD paints them (the floats read as sRGB), and each
    domain's rim against its own face (the prism reads as rim over face)."""
    vision = vision or s.get("vision", "typical")
    seen = {(d, r): see_linear(painted(s["domains"][d][r], r), vision) for d in DOMAINS for r in ROLES}
    hud = {d: see_linear(tuple(to_linear(v) for v in unit_ray(s["domains"][d]["rim"])), vision) for d in DOMAINS}
    rows = [(math.dist(seen[(a, r)], seen[(b, r)]), "%s/%s %ss" % (a, b, r))
            for r in ROLES for a, b in itertools.combinations(DOMAINS, 2)]
    rows += [(math.dist(hud[a], hud[b]), "%s/%s rims on the HUD" % (a, b)) for a, b in itertools.combinations(DOMAINS, 2)]
    rows += [(math.dist(seen[(d, "rim")], seen[(d, "face")]), "%s rim/face" % d) for d in DOMAINS]
    return min(rows)


# ----------------------------------------------------------------------------- styles
def style_problems(key, s, style, sname, truth, lw):
    """The rules a set keeps for naming a style (colour_sets.json `styles`)."""
    errs, D = [], s["domains"]
    unknown = sorted(set(style) - STYLE_KEYS)
    if unknown:
        return ["style %s: unknown rule(s) %s" % (sname, ", ".join(unknown))]
    for d in DOMAINS:
        rim, face = D[d]["rim"], D[d]["face"]
        if "faces" in style:
            v = voices(s, d, truth, lw)
            if not set(v) & set(style["faces"]):
                errs.append("%s: %s face %s is %s, but the %s style wants a face that is %s"
                            % (key, d, face["name"], "/".join(v) or "loud (no voice)", sname, " or ".join(style["faces"])))
        dh = hue_gap(rim["hue"], face["hue"])
        if dh < style.get("min_hue_contrast", 0) - 1e-9:
            errs.append("%s: %s rim and face are %.0f degrees of hue apart; the %s style wants %d"
                        % (key, d, dh, sname, style["min_hue_contrast"]))
        if dh > style.get("max_hue_contrast", 180) + 1e-9:
            errs.append("%s: %s rim and face are %.0f degrees of hue apart; the %s style allows %d"
                        % (key, d, dh, sname, style["max_hue_contrast"]))
        for k, p, what in (("rim_L", rim, "rim"), ("face_L", face, "face")):
            if k in style and not style[k][0] - 1e-9 <= mid(p) <= style[k][1] + 1e-9:
                errs.append("%s: %s %s %s sits at mid L %.2f; the %s style keeps %ss at %.2f-%.2f"
                            % (key, d, what, p["name"], mid(p), sname, what, style[k][0], style[k][1]))
        if "face_chroma" in style:
            c = face["sat"] * mid(face)
            if not style["face_chroma"][0] - 1e-9 <= c <= style["face_chroma"][1] + 1e-9:
                errs.append("%s: %s face %s has chroma %.3f; the %s style keeps faces at %.3f-%.3f"
                            % (key, d, face["name"], c, sname, style["face_chroma"][0], style["face_chroma"][1]))
    if "face_band" in style:
        mids = [mid(D[d]["face"]) for d in DOMAINS]
        if max(mids) - min(mids) > style["face_band"] + 1e-9:
            errs.append("%s: the faces span mid L %.2f-%.2f; the %s style keeps them within %.2f of each other"
                        % (key, min(mids), max(mids), sname, style["face_band"]))
    if "rim_brightness" in style:
        k = s.get("rim_brightness", 1.0)
        if not style["rim_brightness"][0] - 1e-9 <= k <= style["rim_brightness"][1] + 1e-9:
            errs.append("%s: rim_brightness %.2f; the %s style keeps it at %.2f-%.2f"
                        % (key, k, sname, style["rim_brightness"][0], style["rim_brightness"][1]))
    return errs


# ----------------------------------------------------------------------------- the check
def set_problems(key, s, truth, defs=None):
    defs = defs or {}
    lw = laws(defs)
    errs = []
    for k in ("margin", "pigment_gap", "min_lightness_contrast"):
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
    if "waive" in s:
        errs.append("%s: no rule is waived; a set made for a vision keeps rules 4 and 6 as its viewer sees them" % key)
    viewer = s.get("vision", "typical")
    style = None
    if "style" in s:
        style = defs.get("styles", {}).get(s["style"])
        if style is None:
            errs.append("%s: style %r is not one of %s" % (key, s["style"], ", ".join(defs.get("styles", {})) or "none"))
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
        if dh < s.get("min_hue_contrast", 0) - 1e-9:
            errs.append("%s: %s rim and face are %.0f degrees of hue apart (need %d)" % (key, d, dh, s["min_hue_contrast"]))
        dl = abs(mid(rim) - mid(face))
        if dl < s["min_lightness_contrast"] - 1e-9:
            errs.append("%s: %s rim and face are %.2f of lightness apart (need %.2f)" % (key, d, dl, s["min_lightness_contrast"]))
        if viewer == "typical":
            for how, rgb in readings(rim, d, lw):
                fam, top, clear = family_of(truth, rgb)
                if fam != d:
                    errs.append("%s: %s rim %s as %s %s reads as %s in the six-family truth (rule 4)" % (
                        key, d, rim["name"], how, hexof(rgb), top if fam else "the band (nearest %s)" % top))
        elif viewer in VISIONS:
            need = s.get("name_margin", lw["name_margin"])
            for how, lin in vision_readings(rim):
                m, who = name_margin(lin, d, viewer, truth)
                if m < need - 1e-4:
                    errs.append("%s: as %s sees it, the %s rim %s %s looks more like %s's colours than its own "
                                "(margin %.3f, rule 4 needs %.3f)" % (key, viewer, d, rim["name"], how, who, m, need))
        if not voices(s, d, truth, lw):
            if viewer == "typical":
                fam, top, _ = family_of(truth, swatch(face)[0])
                errs.append("%s: %s face %s %s is loud and not its family's (it reads as %s): make it a %s colour, its "
                            "rim's own paint, or quiet (L1 <= %.2f, rule 6)" % (key, d, face["name"], hexof(swatch(face)[0]),
                                                                               fam or "the band", d.capitalize(),
                                                                               lw["night_line"]))
            elif viewer in VISIONS:
                m, who = name_margin(linear_of(*at(face, mid(face))), d, viewer, truth)
                errs.append("%s: as %s sees it, the %s face %s is loud and looks like %s's colours (margin %.3f): make "
                            "it look like its own family, its rim's paint, or quiet (rule 6)" % (key, viewer, d, face["name"], who, m))
    if viewer == "typical":
        sep, where = rims_apart(s)
        if sep < lw["rim_separation"] - 1e-4:
            errs.append("%s: %s are %.3f apart (rule 7 needs %.3f)" % (key, where, sep, lw["rim_separation"]))
    if style is not None:
        errs += style_problems(key, s, style, s["style"], truth, lw)
    return errs


def lab_truth(path=LAB):
    """{"sets": ..., "examples": ...} as the lab embeds them, or None when the page is absent."""
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    m = re.search(r"const SETS_RAW = (\{.*?\});\n", text)
    if not m:
        raise SystemExit("colour_sets: %s has no SETS_RAW" % os.path.relpath(path, REPO))
    e = re.search(r"const EXAMPLES_RAW = (\{.*?\});\n", text)
    r = re.search(r"const REFERENCE_RAW = (\{.*?\});\n", text)
    an = re.search(r"const ANCHORS = (\{.*?\});\n", text)
    out = {"sets": json.loads(m.group(1)), "examples": json.loads(e.group(1)) if e else {},
           "reference": json.loads(r.group(1)) if r else None}
    if an:
        out["anchors"] = json.loads(an.group(1))
    return out


def lab_problems(defs, lab):
    errs = []
    if "anchors" in lab:
        truth = colour_domains.load()
        want = {f: [a["rgb"] for a in truth["families"][f]["anchors"]] for f in DOMAINS}
        if lab["anchors"] != want:
            errs.append("the lab's ANCHORS differ from colour_domains.json's anchors (rules 4 and 6 for another viewer)")
    for group in ("sets", "examples"):
        for key, s in defs.get(group, {}).items():
            theirs = lab.get(group, {}).get(key)
            if theirs is None:
                errs.append("the lab has no %s %s (Docs/Studios/ColourSetsLab.html %s_RAW)"
                            % (group[:-1], key, group.upper()))
                continue
            for k in ("margin", "pigment_gap", "min_hue_contrast", "min_lightness_contrast", "vision", "vision_margin",
                      "style", "rim_brightness"):
                if theirs.get(k) != s.get(k):
                    errs.append("the lab's %s %s is %s, colour_sets.json says %s" % (key, k, theirs.get(k), s.get(k)))
            for d, r, p in pigments(s):
                q = theirs.get("domains", {}).get(d, {}).get(r, {})
                for f in ("hue", "sat", "L", "rho", "swatch", "name"):
                    if q.get(f) != p[f]:
                        errs.append("the lab's %s %s.%s %s is %s, colour_sets.json says %s" % (key, d, r, f, q.get(f), p[f]))
    return errs


def reference_problems(lab):
    """The lab's copy of Photic must be what PhoticColorSetSO measures today."""
    theirs = (lab.get("reference") or {}).get("photic")
    if theirs is None:
        return ["the lab has no REFERENCE_RAW photic (Docs/Studios/ColourSetsLab.html)"]
    mine, errs = photic_reference(), []
    for d, r, p in pigments(mine):
        q = theirs.get("domains", {}).get(d, {}).get(r, {})
        for f in ("hue", "sat", "L", "rho", "hud"):
            if q.get(f) != p.get(f):
                errs.append("the lab's Photic %s.%s %s is %s, PhoticColorSetSO measures %s" % (d, r, f, q.get(f), p.get(f)))
    return errs


def problems(defs, truth=None, lab=False):
    truth = colour_domains.load() if truth is None else truth
    errs = []
    for name, style in defs.get("styles", {}).items():
        unknown = sorted(set(style) - STYLE_KEYS)
        if unknown:
            errs.append("style %s: unknown rule(s) %s" % (name, ", ".join(unknown)))
        for v in style.get("faces", []):
            if v not in VOICES:
                errs.append("style %s: face voice %r is not one of %s" % (name, v, ", ".join(VOICES)))
    for group in ("sets", "examples"):
        for key, s in defs.get(group, {}).items():
            errs += set_problems(key, s, truth, defs)
    dup = sorted(set(defs.get("sets", {})) & set(defs.get("examples", {})))
    if dup:
        errs.append("%s is both a set and an example" % ", ".join(dup))
    lab = lab_truth() if lab is False else lab
    if lab is not None:
        errs += lab_problems(defs, lab)
        if "reference" in lab:
            errs += reference_problems(lab)
    return errs


# ----------------------------------------------------------------------------- the reference: Photic, measured
def pigment_of_linear(name, lin, span=0.10, rho=0.035):
    """A pigment whose centre ray passes through linear colour `lin` (HDR allowed: its brightest channel is taken as 1
    when it is above 1, the way the screen clips it), its L range `span` either side of that colour's L."""
    peak = max(lin)
    lin = tuple(max(0.0, v) / peak for v in lin) if peak > 1 else tuple(max(0.0, v) for v in lin)
    L, a, b = oklab_linear(*lin)
    hue = math.degrees(math.atan2(b, a)) % 360
    p = {"name": name, "hue": round(hue), "sat": round(math.hypot(a, b) / L, 3),
         "L": [round(max(0.02, L - span), 2), round(min(1.0, L + span), 2)], "rho": rho}
    p["swatch"] = hexof(swatch(p)[0])
    return p


def photic_reference():
    """PhoticColorSetSO, the hand-tuned original, in pigments: each domain's prism rim (InsideBlockColor) over its prism
    face (OutsideBlockColor); Fire is the danger rim (EnvironmentColors.Danger) over its own paint in shadow (Photic
    paints no Fire face); Lime is BrightCTA over DarkCTA, one paint at two brightnesses. Photic tuned its HUD colours
    apart from its prism rims, so each rim carries `hud`, the numbers the HUD reads (TrailHighlightColor, brightest
    channel 1; the danger and CTA signals), which rule 4's paint reading and rule 7 use. Every spread is 0.035, the
    spread most sets use."""
    import author_colour_set_assets as writer   # deferred: the writer imports this module
    orig = writer.read_original()
    def signal(c):
        peak = max(c[:3]) or 1.0
        return [round(v / peak, 4) for v in c[:3]]
    D = {}
    for block, dom in writer.DOMAIN_SETS:
        D[dom] = {"rim": pigment_of_linear("rim", orig[block]["InsideBlockColor"][:3]),
                  "face": pigment_of_linear("face", orig[block]["OutsideBlockColor"][:3])}
        D[dom]["rim"]["hud"] = signal(orig[block]["TrailHighlightColor"])
    env = orig["EnvironmentColors"]
    danger = pigment_of_linear("rim", env["Danger"][:3])
    danger["hud"] = signal(env["Danger"])
    shade = {k: v for k, v in danger.items() if k != "hud"}
    shade.update(name="face", L=[0.22, 0.38])
    shade["swatch"] = hexof(swatch(shade)[0])
    D["fire"] = {"rim": danger, "face": shade}
    D["lime"] = {"rim": pigment_of_linear("rim", env["BrightCTA"][:3]), "face": pigment_of_linear("face", env["DarkCTA"][:3])}
    D["lime"]["rim"]["hud"] = signal(env["DarkCTA"])            # GetCtaSignalColor
    names = {"jade": ("Azure", "Deep azure"), "ruby": ("Violet", "Deep violet"), "gold": ("Amber", "Umber"),
             "blue": ("Periwinkle", "Indigo"), "fire": ("Danger red", "Ember"), "lime": ("CTA lime", "Olive lime")}
    for d in DOMAINS:
        D[d]["rim"]["name"], D[d]["face"]["name"] = names[d]
    return {"name": "Photic (measured)", "margin": 0.05, "pigment_gap": 0.05, "min_lightness_contrast": 0.12,
            "domains": {d: D[d] for d in DOMAINS}}


# ----------------------------------------------------------------------------- the report
def simulate(rgb, kind):
    lin = [to_linear(v) for v in rgb]
    m = CVD[kind]
    return oklab_linear(*(min(1.0, max(0.0, sum(m[i][k] * lin[k] for k in range(3)))) for i in range(3)))


def describe(key, s, truth, lw, defs):
    """Every pigment, its readings and voice, the closest pairs and the rules, for one set."""
    style = s.get("style")
    print("%s (%s)%s" % (s["name"], s.get("track", "example"), "  style: %s" % style if style else ""))
    for d, r, p in pigments(s):
        rgb, _ = swatch(p)
        dark, light = (hexof(tuple(to_gamma(v) for v in linear_of(*at(p, L)))) for L in p["L"])
        extra = ""
        if r == "rim":
            extra = "  " + "  ".join("%s %s %s" % (how, hexof(c), (lambda f: "%s%+.1f" % (f[0], f[2]) if f[0] else "band~%s" % f[1])(family_of(truth, c)))
                                     for how, c in readings(p, d, lw)[1:])
        else:
            extra = "  voice: %s" % ("/".join(voices(s, d, truth, lw)) or "LOUD")
        print("  %-4s %-4s %-12s %s  shadow %s highlight %s  hue %3d sat %.3f L %.2f-%.2f rho %.3f%s"
              % (d, r, p["name"], hexof(rgb), dark, light, p["hue"], p["sat"], p["L"][0], p["L"][1], p["rho"], extra))
    rows = []
    for (d1, r1, p), (d2, r2, q) in itertools.combinations(pigments(s), 2):
        if d1 != d2:
            rows.append((gap(p, q), pigment_gap(p, q), p["name"], q["name"]))
    print("  closest pigments (band %.2f, lighting %.2f):" % (s["margin"], s["pigment_gap"]))
    for g, pg, a, b in sorted(rows)[:4]:
        print("    %-12s %-12s %.3f apart" % (a, b, g))
    for g, pg, a, b in sorted(rows, key=lambda t: t[1])[:2]:
        print("    %-12s %-12s directions %.3f apart" % (a, b, pg))
    sep, where = rims_apart(s)
    print("  rule 7, the rims as painted: closest %s, %.3f (%.1f JND, need %.1f)" % (where, sep, sep / 0.02, lw["rim_separation"] / 0.02))
    for d in DOMAINS:
        rim, face = s["domains"][d]["rim"], s["domains"][d]["face"]
        print("  %-4s %s over %s: hue %3.0f deg apart, lightness %.2f" % (d, rim["name"], face["name"],
              hue_gap(rim["hue"], face["hue"]), abs(mid(rim) - mid(face))))
    if "vision" in s:
        print("  made for %s (rule 5 needs %.3f)" % (VISIONS[s["vision"]], s["vision_margin"]))
    viewer = s.get("vision", "typical")
    if viewer != "typical" and viewer in VISIONS:
        print("  rules 4 and 6 as %s sees them (name margin: how much nearer its own family's colours, OKLab):" % viewer)
        for d in DOMAINS:
            ms = ["%s %+.3f (else %s)" % ((how,) + name_margin(lin, d, viewer, truth)) for how, lin in vision_readings(s["domains"][d]["rim"])]
            print("    %-4s rim %s   face voice %s" % (d, "  ".join(ms), "/".join(voices(s, d, truth, lw)) or "LOUD"))
    errs = set_problems(key, s, truth, defs)
    print("  rules: %s" % ("all kept" if not errs else "%d broken" % len(errs)))


def report(defs):
    truth, lw = colour_domains.load(), laws(defs)
    print("Photic, the reference, measured from PhoticColorSetSO (not defined here; its rules are reported, not enforced):")
    ref = photic_reference()
    describe("photic", ref, truth, lw, {})
    for e in set_problems("photic", ref, truth):
        print("    " + e)
    for group in ("sets", "examples"):
        for key, s in defs.get(group, {}).items():
            describe(key, s, truth, lw, defs)
    print("rule 5 measure, painted colours (JND), by vision:")
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

    def planted(fn, key="dart_frog"):
        d = json.loads(json.dumps(defs))
        fn(d["sets"][key])
        return d

    def dom(fn):
        return planted(lambda S: fn(S["domains"]))
    cases.append(("two domains overlap", dom(lambda D: D["ruby"]["face"].update(D["jade"]["face"])), "band"))
    cases.append(("one paint under different light", dom(lambda D: D["blue"]["face"].update(hue=125, sat=0.25, L=[0.30, 0.38])), "one paint"))
    cases.append(("rim and face share a lightness", dom(lambda D: D["fire"]["face"].update(L=[0.48, 0.66])), "of lightness"))
    cases.append(("a rim left its family", dom(lambda D: D["gold"]["rim"].update(hue=130)), "rule 4"))
    cases.append(("a gold the HUD shows as fire", dom(lambda D: D["gold"]["rim"].update(hue=76, sat=0.2)), "as paint"))
    cases.append(("a stale swatch", dom(lambda D: D["jade"]["rim"].update(swatch="#00D4FF")), "swatch says"))
    cases.append(("a loud face of another family", dom(lambda D: D["gold"]["face"].update(hue=260, sat=0.12, L=[0.70, 0.84])), "rule 6"))
    cases.append(("two rims too alike to tell apart",
                  json.loads(json.dumps(dict(defs, laws=dict(laws(defs), rim_separation=0.5)))), "rule 7"))
    cases.append(("an unknown style", planted(lambda S: S.update(style="baroque")), "is not one of"))
    cases.append(("a style's rule broken", planted(lambda S: S.update(style="nocturne"), key="nacre"), "style"))
    cases.append(("a set that tries to waive a rule", planted(lambda S: S.update(waive={"voice": "because it would look nicer this way"})), "no rule is waived"))
    vision_key = next((k for k, s in defs["sets"].items() if "vision" in s), None)
    if vision_key:
        cases.append(("a vision set its viewer cannot read", planted(lambda S: S.update(vision_margin=1.0), vision_key), "rule 5"))
        cases.append(("an unknown vision", planted(lambda S: S.update(vision="x-ray"), vision_key), "is not one of"))
        cases.append(("a vision set with no margin", planted(lambda S: S.pop("vision_margin"), vision_key), "needs vision_margin"))
        prot = next((k for k, s in defs["sets"].items() if s.get("vision") == "protan"), None)
        if prot:
            cases.append(("a protan's Gold that reads as Fire", planted(lambda S: S["domains"]["gold"]["rim"].update(hue=40, sat=0.3), prot), "looks more like"))
            cases.append(("a protan's loud face", planted(lambda S: S["domains"]["blue"]["face"].update(hue=60, sat=0.2, L=[0.72, 0.82]), prot), "is loud and looks like"))
    ex_key = next(iter(defs.get("examples", {})), None)
    if ex_key:
        d = json.loads(json.dumps(defs))
        d["examples"][ex_key]["domains"]["lime"]["rim"].update(hue=60)
        cases.append(("an example that breaks a rule", d, ex_key))
    bad = 0
    for label, d, needle in cases:
        hit = any(needle in e for e in problems(d, truth, lab=None))
        print("  %-38s %s" % (label, "caught" if hit else "MISSED"))
        bad += not hit
    if lab is not None:
        lb = json.loads(json.dumps(lab))
        lb["sets"]["dart_frog"]["domains"]["lime"]["rim"]["hue"] += 4
        hit = any("lab's" in e for e in problems(defs, truth, lab=lb))
        print("  %-38s %s" % ("the lab embeds different pigments", "caught" if hit else "MISSED"))
        bad += not hit
        if lab.get("reference"):
            lb = json.loads(json.dumps(lab))
            lb["reference"]["photic"]["domains"]["gold"]["rim"]["hue"] += 3
            hit = any("lab's Photic" in e for e in problems(defs, truth, lab=lb))
            print("  %-38s %s" % ("the lab's Photic drifted", "caught" if hit else "MISSED"))
            bad += not hit
        if ex_key:
            lb = json.loads(json.dumps(lab))
            lb["examples"].pop(ex_key, None)
            hit = any("lab has no example" in e for e in problems(defs, truth, lab=lb))
            print("  %-38s %s" % ("the lab lacks an example", "caught" if hit else "MISSED"))
            bad += not hit
    return bad


def main(argv):
    defs = load()
    if argv[:1] == ["classify"]:
        group = "sets" if argv[1] in defs["sets"] else "examples"
        s = defs[group][argv[1]]
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
