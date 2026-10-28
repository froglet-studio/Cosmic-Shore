#!/usr/bin/env python3
"""kite_mote_colors - the Wrasse's four lasso elements as four shades of LIME, chosen geometrically.

    python3 Tools/Build/kite_mote_colors.py              # derive, print the report, write the asset and the lab
    python3 Tools/Build/kite_mote_colors.py --check      # exit 1 if the asset or the lab drifted from the derivation
    python3 Tools/Build/kite_mote_colors.py --self-test  # prove the boundary and the checks fire

Garrett, 2026-10-21: "make all four of the lasso elements different hues, saturations and values within the lime green
color family (lime). make a 3d boundary that defines the space as distinct from jade, ruby, gold, blue (neutral), and
fire (danger). form the largest tetrahedron within that space that makes four distinct values within that boundary and
assign them to each element."

THE SPACE is OKLab (L = value, a/b = hue and saturation as an angle and a radius), the perceptual space in which a
Euclidean distance is a visible difference (~0.02 is a just-noticeable one).

THE FAMILIES are anchored on the colours the game already draws (Docs/VesselModels/COLOR.md, Docs/PALETTE.md), read
live from the assets so the boundary follows the palette:
    lime  the CTA signal (OriginalColorSetSO DarkCTA, brightest channel at 1), BrightCTA (= the HUD ladder's lime),
          and the kite view's own Lime (KiteNavigatorView)
    jade / ruby / gold   each domain's UI colour (TrailHighlightColor) and its signal colour (domain_colors.py)
    blue  the Blue domain's colour, the HUD ladder's blue, the vessel body and glass (vessel_colors.py) - AND every grey:
          "grey (desaturated or valued down) belongs to it alone", so Blue's distance is also a colour's chroma
    fire  the danger signal (OriginalColorSetSO Danger), the HUD ladder's fire, the engine ember (vessel_colors.py)

THE BOUNDARY is every sRGB colour whose distance to the nearest lime anchor, plus MARGIN, is no more than its distance
to the nearest anchor of every other family: lime's cell of the families' Voronoi partition, shrunk by MARGIN (0.05,
about 2.5 just-noticeable differences) so nothing on its edge could be mistaken for a neighbour, and clipped to what a
screen can show (the sRGB cube). Inside that, a lime is GREEN-YELLOW (Garrett, 2026-10-21, after the first cut: "pull
time [and] space away from jade and charge away from gold" - "it has to have green in the yellow and yellow in the
green. it can not have any blue or orange at all"): red and green only, blue 0; green leading red; red between a
quarter and three quarters of green, i.e. an HSV hue of 75-105 degrees, a quarter of the way in from pure yellow and
from pure green (GREEN_YELLOW_HUES). With blue at 0 the space is a curved SHEET (hue x value) in OKLab, not a volume:
saturation still differs from corner to corner, but only as hue and value carry it.

THE TETRAHEDRON is the largest-volume one (OKLab volume) with its four corners in the boundary AND four distinct
hues, saturations and values: every pair of corners differs by APART in value (L), in saturation (chroma) and in hue
(the arc between them at their mean chroma). Without that rule the largest one can put two corners at one hue
(#00ff00 and #a5ff95, 142 and 141 degrees, in the first cut). APART is the largest of APART_STEPS that fits: on the
green-yellow sheet value, saturation and hue move together, and 0.02 (one just-noticeable difference) is what fits.
On a sheet the tetrahedron is nearly flat; its corners are the sheet's most spread four.
Searched over the boundary's surface on a 1/64 sRGB grid (the maximum of a volume, linear in each corner, sits on the
surface), from seeded restarts, then each corner climbed continuously to 1/1536. Its corners are the four colours.

THE ASSIGNMENT reads each corner's character: the lowest value is Mass (weight), the highest is Charge (the bright
spark), and of the other two the cooler hue (toward Jade) is Space and the warmer is Time.

It writes KiteNavigatorConfig.asset's `elementColors` (sRGB, Charge, Mass, Space, Time - index = Element - 1), the
Wrasse Lasso Lab's LIME_E line, and the palette's `EnvironmentColors.ElementalCrystalColors` (OriginalColorSetSO.asset):
the four elemental CRYSTALS wear the same four shades (Garrett, 2026-10-27: "the mass crystals and omnicrystals are the
hardest to distinguish. shift the colors on each element to match the color shifts we used for the wrasse's 2d
elements"). The Mass shade is the darkest corner, so Mass and the omni's lime differ in value as well as hue; since
2026-10-28 the omni itself wears all four shades, each on its element's shapes (Docs/PALETTE.md §2.12,
author_omni_crystal_triangles.py). Tools/Build/author_elemental_crystal_colors.py carries them onto the crystals' own
materials. The tail,
the saved-boost dots and every other CTA lime stay the palette's lime.
"""
import math
import os
import random
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(HERE, "vessel_model"))
sys.path.insert(0, os.path.join(REPO, ".claude", "skills", "vessel-studio"))
import domain_colors  # noqa: E402  (the domains' UI and signal colours, read the way the studios read them)
import vessel_colors  # noqa: E402  (the vessel body, glass and engine)

PALETTE = os.path.join(REPO, "Assets", "_SO_Assets", "Color Palettes", "OriginalColorSetSO.asset")
LADDER = os.path.join(REPO, "Assets", "Resources", "ElementalBarsConfig.asset")
VIEW = os.path.join(REPO, "Assets", "_Scripts", "Controller", "Vessel", "Crew", "KiteNavigatorView.cs")
ASSET = os.path.join(REPO, "Assets", "_SO_Assets", "VesselActions", "KiteNavigator", "KiteNavigatorConfig.asset")
LAB = os.path.join(REPO, "Docs", "Studios", "WrasseLassoLab.html")

MARGIN = 0.05          # OKLab: a corner is at least this much nearer lime than any other family
# Garrett, 2026-10-21: "it has to have green in the yellow and yellow in the green. it can not have any blue or orange at
# all." So a lime is red and green only (blue 0), green leads red (no orange), and red is a real share of green: its HSV
# hue, 120 - 60 r/g, sits strictly inside yellow (60) .. green (120), in this window.
GREEN_YELLOW_HUES = (75.0, 105.0)
APART_STEPS = (0.03, 0.025, 0.02)   # OKLab: the most distinct that fits - every pair of corners differs by APART
APART = APART_STEPS[0]               # in value, in saturation AND in hue (derive() sets it)
GRID = 64              # the search grid, steps per sRGB channel (only the blue-0 face is searched)
RESTARTS = 64
SEED = 7
ELEMENTS = ("Charge", "Mass", "Space", "Time")   # index = Element - 1, the HUD's order
NUM = r"(-?[\d.]+(?:e-?\d+)?)"


# ----------------------------------------------------------------------------- colour
def to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def oklab(rgb):
    r, g, b = (to_linear(max(0.0, min(1.0, v))) for v in rgb)
    l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b
    m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b
    s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b
    l, m, s = (v ** (1 / 3) for v in (l, m, s))
    return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s)


def lch(lab):
    return lab[0], math.hypot(lab[1], lab[2]), math.degrees(math.atan2(lab[2], lab[1])) % 360


def hexof(rgb):
    return "#%02x%02x%02x" % tuple(max(0, min(255, round(v * 255))) for v in rgb)


def peak1(c):
    p = max(c)
    return tuple(v / p for v in c) if p > 1e-6 else (1.0, 1.0, 1.0)


# ----------------------------------------------------------------------------- the families, read live
def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def yaml_color(text, key):
    m = re.search(r"^\s*%s: \{r: %s, g: %s, b: %s" % (re.escape(key), NUM, NUM, NUM), text, re.M)
    if not m:
        raise SystemExit("kite_mote_colors: no %s colour found" % key)
    return tuple(float(x) for x in m.groups())


def families():
    palette, ladder, view = read(PALETTE), read(LADDER), read(VIEW)
    m = re.search(r"static readonly Color Lime = new\(%sf, %sf, %sf" % (NUM, NUM, NUM), view)
    if not m:
        raise SystemExit("kite_mote_colors: KiteNavigatorView's Lime constant not found")
    kite_lime = tuple(float(x) for x in m.groups())
    dom = domain_colors.asset_colors(palette)
    scheme = vessel_colors.SCHEME
    fam = {
        "lime": [peak1(yaml_color(palette, "DarkCTA")), yaml_color(palette, "BrightCTA"), yaml_color(ladder, "limeColor"),
                 kite_lime],
        "jade": [dom["jade"], peak1(dom["jade"])],
        "ruby": [dom["ruby"], peak1(dom["ruby"])],
        "gold": [dom["gold"], peak1(dom["gold"])],
        "blue": [dom["blue"], peak1(dom["blue"]), yaml_color(ladder, "blueColor"),
                 scheme["body"]["rim"], scheme["body"]["face"], scheme["window"]["rim"], scheme["window"]["face"]],
        "fire": [peak1(yaml_color(palette, "Danger")), yaml_color(ladder, "fireColor"),
                 scheme["engine"]["rim"], scheme["engine"]["face"]],
    }
    return {k: [oklab(c) for c in v] for k, v in fam.items()}, fam


def nearest(lab, anchors):
    return min(math.dist(lab, a) for a in anchors)


def family_distances(lab, fam):
    out = {k: nearest(lab, v) for k, v in fam.items()}
    out["blue"] = min(out["blue"], math.hypot(lab[1], lab[2]))   # every grey is Blue's
    return out


def green_yellow(rgb):
    """Red and green only, green leading, the hue inside GREEN_YELLOW_HUES (no blue, no orange, no pure yellow or green)."""
    r, g, b = rgb
    if b > 1e-9 or g <= 1e-9 or r > g:
        return False
    hue = 120.0 - 60.0 * r / g
    return GREEN_YELLOW_HUES[0] - 0.01 <= hue <= GREEN_YELLOW_HUES[1] + 0.01   # 0.01 degrees: the colours' 4-place rounding


def in_lime(rgb, fam, margin=MARGIN):
    if any(v < 0.0 or v > 1.0 for v in rgb) or not green_yellow(rgb):
        return False
    d = family_distances(oklab(rgb), fam)
    return all(d["lime"] + margin <= d[k] for k in d if k != "lime")


# ----------------------------------------------------------------------------- the tetrahedron
def volume(p):
    (ax, ay, az), (bx, by, bz), (cx, cy, cz), (dx, dy, dz) = p
    ux, uy, uz = bx - ax, by - ay, bz - az
    vx, vy, vz = cx - ax, cy - ay, cz - az
    wx, wy, wz = dx - ax, dy - ay, dz - az
    return abs(ux * (vy * wz - vz * wy) - uy * (vx * wz - vz * wx) + uz * (vx * wy - vy * wx)) / 6.0


def apart(lab_a, lab_b):
    """Two colours differ by APART in value, in saturation and in hue."""
    la, ca, ha = lch(lab_a)
    lb, cb, hb = lch(lab_b)
    dh = abs((ha - hb + 180) % 360 - 180)
    return abs(la - lb) >= APART and abs(ca - cb) >= APART and 0.5 * (ca + cb) * math.radians(dh) >= APART


def distinct(labs):
    return all(apart(labs[i], labs[j]) for i in range(len(labs)) for j in range(i + 1, len(labs)))


def region(fam):
    """The grid points inside the boundary, and the ones on its surface (a neighbour outside, or the screen's edge)."""
    n = GRID
    inside = {}
    for i in range(n + 1):
        for j in range(n + 1):
            for k in range(n + 1):
                c = (i / n, j / n, k / n)
                if green_yellow(c):
                    inside[(i, j, k)] = in_lime(c, fam)
    pts = [key for key, ok in inside.items() if ok]
    surface = [key for key in pts
               if any(not inside.get((key[0] + dx, key[1] + dy, key[2] + dz), False)
                      for dx, dy, dz in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)))]
    return pts, surface


def largest_tetrahedron(fam):
    """Coordinate ascent from seeded restarts: each corner in turn moves to the surface point that first removes the
    pairs' shortfall from APART, then most grows the volume. The constrained problem has several local optima, hence the
    restarts; the best distinct one is refined."""
    n = GRID
    pts, surface = region(fam)
    rgb = [(i / n, j / n, k / n) for i, j, k in surface]
    lab = [oklab(c) for c in rgb]
    pol = [lch(c) for c in lab]
    count = len(lab)

    def shortfall(q, others):
        lq, cq, hq = pol[q]
        out = 0.0
        for o in others:
            lo, co, ho = pol[o]
            dh = abs((hq - ho + 180) % 360 - 180)
            out += (max(0.0, APART - abs(lq - lo)) + max(0.0, APART - abs(cq - co))
                    + max(0.0, APART - 0.5 * (cq + co) * math.radians(dh)))
        return out

    rng = random.Random(SEED)
    best_v, best = -1.0, None
    for _ in range(RESTARTS):
        idx = rng.sample(range(count), 4)
        for _ in range(40):
            moved = False
            for slot in range(4):
                others = [idx[j] for j in range(4) if j != slot]
                (ax, ay, az), b, c = (lab[o] for o in others)
                ux, uy, uz = b[0] - ax, b[1] - ay, b[2] - az
                vx, vy, vz = c[0] - ax, c[1] - ay, c[2] - az
                nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx

                def score(q):
                    x, y, z = lab[q]
                    return -1e3 * shortfall(q, others) + abs((x - ax) * nx + (y - ay) * ny + (z - az) * nz)

                cur = score(idx[slot])
                k = max(range(count), key=score)
                if score(k) > cur + 1e-12:
                    idx[slot], moved = k, True
            if not moved:
                break
        labs = [lab[i] for i in idx]
        if not distinct(labs):
            continue
        v = volume(labs)
        if v > best_v:
            best_v, best = v, [rgb[i] for i in idx]
    if best is None:
        raise SystemExit("kite_mote_colors: no four distinct corners fit in the boundary")
    return refine(best, fam), len(pts), len(surface)


def refine(corners, fam):
    """Climb each corner continuously (26 directions, halving steps) while it stays in the boundary."""
    corners = [list(c) for c in corners]
    dirs = [(dx, dy, dz) for dx in (-1, 0, 1) for dy in (-1, 0, 1) for dz in (-1, 0, 1) if (dx, dy, dz) != (0, 0, 0)]
    step = 1.0 / (2 * GRID)
    while step >= 1.0 / 1536:
        improved = True
        while improved:
            improved = False
            for slot in range(4):
                labs = [oklab(c) for c in corners]
                cur = volume(labs)
                for d in dirs:
                    cand = [min(1.0, max(0.0, corners[slot][a] + d[a] * step)) for a in range(3)]
                    if not in_lime(cand, fam):
                        continue
                    trial = labs[:slot] + [oklab(cand)] + labs[slot + 1:]
                    if volume(trial) > cur + 1e-12 and distinct(trial):
                        corners[slot], cur, labs, improved = cand, volume(trial), trial, True
        step /= 2
    return [tuple(round(v, 4) for v in c) for c in corners]


def assign(corners):
    """Mass the lowest value, Charge the highest; of the other two, Space the cooler hue (toward Jade), Time the warmer."""
    by_l = sorted(corners, key=lambda c: oklab(c)[0])
    mass, charge = by_l[0], by_l[-1]
    a, b = by_l[1], by_l[2]
    space, time = (a, b) if lch(oklab(a))[2] > lch(oklab(b))[2] else (b, a)
    return [charge, mass, space, time]   # index = Element - 1


def derive():
    """The four colours, at the most distinct APART that fits."""
    global APART
    fam, _ = families()
    for step in APART_STEPS:
        APART = step
        try:
            corners, n_in, n_surface = largest_tetrahedron(fam)
        except SystemExit:
            continue
        return fam, assign(corners), n_in, n_surface
    raise SystemExit("kite_mote_colors: no four distinct corners fit at any of %s" % (APART_STEPS,))


# ----------------------------------------------------------------------------- where they land
def asset_block(colors):
    return "  elementColors:\n" + "".join("  - {r: %s, g: %s, b: %s, a: 1}\n" % tuple(fmt(v) for v in c) for c in colors)


def fmt(v):
    return str(int(v)) if float(v).is_integer() else ("%.4f" % v).rstrip("0")


ASSET_RE = re.compile(r"  elementColors:\n(?:  - \{r: [^\n]*\}\n){4}")


def write_asset(text, colors):
    block = asset_block(colors)
    if ASSET_RE.search(text):
        return ASSET_RE.sub(lambda _: block, text, count=1)
    m = re.search(r"^  moteSize: .*\n", text, re.M)
    if not m:
        raise SystemExit("kite_mote_colors: KiteNavigatorConfig.asset has no moteSize line to follow")
    return text[:m.end()] + block + text[m.end():]


PALETTE_RE = re.compile(r"    ElementalCrystalColors:\n(?:    - \{r: [^\n]*\}\n){4}")


def palette_block(colors):
    return "    ElementalCrystalColors:\n" + "".join(
        "    - {r: %s, g: %s, b: %s, a: 1}\n" % tuple(fmt(v) for v in c) for c in colors)


def write_palette(text, colors):
    block = palette_block(colors)
    if PALETTE_RE.search(text):
        return PALETTE_RE.sub(lambda _: block, text, count=1)
    m = re.search(r"^    ElementalCrystalDimming: .*\n", text, re.M)
    if not m:
        raise SystemExit("kite_mote_colors: OriginalColorSetSO.asset has no ElementalCrystalDimming line to follow")
    return text[:m.end()] + block + text[m.end():]


def palette_colors(text):
    """The four crystal colours the palette holds, or None when it holds none."""
    m = PALETTE_RE.search(text)
    if not m:
        return None
    return [tuple(float(x) for x in c) for c in re.findall(r"\{r: %s, g: %s, b: %s" % (NUM, NUM, NUM), m.group(0))]


LAB_RE = re.compile(r"  const LIME_E = \[[^\]]*\];[^\n]*\n")


def lab_line(colors):
    return ("  const LIME_E = [%s]; // GENERATED by Tools/Build/kite_mote_colors.py: the game's four lime shades, "
            "index = Element - 1\n" % ", ".join("'%s'" % hexof(c) for c in colors))


def write_lab(text, colors):
    line = lab_line(colors)
    if LAB_RE.search(text):
        return LAB_RE.sub(lambda _: line, text, count=1)
    m = re.search(r"^  const ECOL = .*\n", text, re.M)
    if not m:
        raise SystemExit("kite_mote_colors: the lab has no ECOL line to follow")
    return text[:m.end()] + line + text[m.end():]


def check(asset_text, lab_text, colors, fam, palette_text=None):
    errs = []
    if palette_text is not None:
        have = palette_colors(palette_text)
        if have is None:
            errs.append("OriginalColorSetSO.asset has no ElementalCrystalColors (run Tools/Build/kite_mote_colors.py)")
        else:
            for e, h, w in zip(ELEMENTS, have, colors):
                if max(abs(a - b) for a, b in zip(h, w)) > 0.002:
                    errs.append("OriginalColorSetSO.asset crystal %s is %s, the derivation says %s" % (e, hexof(h), hexof(w)))
    m = ASSET_RE.search(asset_text)
    if not m:
        errs.append("KiteNavigatorConfig.asset has no elementColors (run Tools/Build/kite_mote_colors.py)")
    else:
        have = [tuple(float(x) for x in c) for c in re.findall(r"\{r: %s, g: %s, b: %s" % (NUM, NUM, NUM), m.group(0))]
        for e, h, w in zip(ELEMENTS, have, colors):
            if max(abs(a - b) for a, b in zip(h, w)) > 0.002:
                errs.append("KiteNavigatorConfig.asset %s is %s, the derivation says %s" % (e, hexof(h), hexof(w)))
    if lab_line(colors) not in lab_text:
        errs.append("WrasseLassoLab.html's LIME_E is not the derivation's (run Tools/Build/kite_mote_colors.py)")
    for e, c in zip(ELEMENTS, colors):
        if not in_lime(c, fam, MARGIN - 1e-6):
            errs.append("%s %s lies outside the lime boundary" % (e, hexof(c)))
    labs = [oklab(c) for c in colors]
    for i in range(4):
        for j in range(i + 1, 4):
            if not apart(labs[i], labs[j]):
                errs.append("%s and %s are not %.2f apart in value, saturation and hue" % (ELEMENTS[i], ELEMENTS[j], APART))
    return errs


def report(fam, colors, n_in, n_surface):
    print("kite_mote_colors: the lime boundary holds %d of %d grid colours (%d on its surface), margin %.2f OKLab"
          % (n_in, (GRID + 1) ** 3, n_surface, MARGIN))
    labs = [oklab(c) for c in colors]
    print("  largest tetrahedron: OKLab volume %.3g, every pair %.3f apart in value, saturation and hue" % (volume(labs), APART))
    for e, c, lab in zip(ELEMENTS, colors, labs):
        L, C, h = lch(lab)
        d = family_distances(lab, fam)
        near = min((k for k in d if k != "lime"), key=lambda k: d[k])
        print("  %-6s %s  sRGB (%.4f, %.4f, %.4f)  L %.3f  C %.3f  hue %3.0f  (lime %.3f, nearest other %s %.3f)"
              % (e, hexof(c), c[0], c[1], c[2], L, C, h, d["lime"], near, d[near]))
    pairs = [(math.dist(labs[i], labs[j]), ELEMENTS[i], ELEMENTS[j]) for i in range(4) for j in range(i + 1, 4)]
    d, a, b = min(pairs)
    print("  closest pair: %s / %s, %.3f OKLab apart" % (a, b, d))


def self_test():
    fam, raw = families()
    bad = []
    for name, c in (("half red, full green", (0.5, 1.0, 0.0)), ("a darker green-yellow", (0.45, 0.85, 0.0))):
        if not in_lime(c, fam):
            bad.append("%s %s is not lime" % (name, hexof(c)))
    for name, c in (("Jade", raw["jade"][1]), ("Ruby", raw["ruby"][1]), ("Gold", raw["gold"][1]),
                    ("the Blue domain", raw["blue"][1]), ("the danger red", raw["fire"][0]), ("mid grey", (0.5, 0.5, 0.5)),
                    ("white", (1.0, 1.0, 1.0)), ("dark olive (DarkCTA raw)", (0.28125, 0.5, 0.078125)),
                    ("pure yellow (no green in it)", (1.0, 1.0, 0.0)), ("pure green (no yellow in it)", (0.0, 1.0, 0.0)),
                    ("the first cut's mint (blue in it)", (0.7539, 0.9844, 0.5781)), ("orange", (1.0, 0.6, 0.0)),
                    ("the CTA lime (a touch of blue)", raw["lime"][0])):
        if in_lime(c, fam):
            bad.append("%s %s is inside the lime boundary" % (name, hexof(c)))
    _, colors, _, _ = derive()
    asset, lab = write_asset(read(ASSET), colors), write_lab(read(LAB), colors)
    palette = write_palette(read(PALETTE), colors)
    if check(asset, lab, colors, fam, palette):
        bad.append("a freshly written asset, lab and palette fail the check: %s" % check(asset, lab, colors, fam, palette))
    if write_asset(asset, colors) != asset or write_lab(lab, colors) != lab or write_palette(palette, colors) != palette:
        bad.append("writing is not idempotent")
    if not check(asset, lab, colors, fam, PALETTE_RE.sub(lambda _: palette_block([(0.4862745, 0.9882354, 0.0)] + colors[1:]),
                                                         palette, count=1)):
        bad.append("a palette crystal colour set to the omni's lime passes")
    if not check(asset, lab, colors, fam, PALETTE_RE.sub("", palette, count=1)):
        bad.append("a palette with no crystal colours passes")
    tampered = ASSET_RE.sub(lambda _: asset_block([(0.6, 0.2, 0.2)] + colors[1:]), asset, count=1)
    if not check(tampered, lab, colors, fam):
        bad.append("a tampered asset colour passes")
    if not check(asset, lab.replace(hexof(colors[0]), "#ffffff", 1), colors, fam):
        bad.append("a tampered lab colour passes")
    if not check(asset, lab, [(0.5, 0.5, 0.5)] + colors[1:], fam):
        bad.append("a grey element colour passes the boundary check")
    if not any("apart" in e for e in check(asset, lab, [colors[0], colors[0]] + colors[2:], fam)):
        bad.append("two equal element colours pass the distinctness check")
    if distinct([oklab(c) for c in ((0.0, 1.0, 0.0), (0.6484, 1.0, 0.5833))]):
        bad.append("#00ff00 and #a5ff95 (one hue) count as distinct")
    if assign(list(reversed(colors))) != colors:
        bad.append("the assignment depends on the order the corners were found in")
    print("kite_mote_colors self-test: " + ("ok" if not bad else "FAILED"))
    for b in bad:
        print("  -", b)
    return 1 if bad else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    fam, colors, n_in, n_surface = derive()
    report(fam, colors, n_in, n_surface)
    if "--check" in argv:
        errs = check(read(ASSET), read(LAB), colors, fam, read(PALETTE))
        for e in errs:
            print("FAIL:", e)
        print("kite_mote_colors --check:", "ok" if not errs else "%d problem(s)" % len(errs))
        return 1 if errs else 0
    for path, fn in ((ASSET, write_asset), (LAB, write_lab), (PALETTE, write_palette)):
        old = read(path)
        new = fn(old, colors)
        if new != old:
            with open(path, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(new)
            print("wrote", os.path.relpath(path, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
