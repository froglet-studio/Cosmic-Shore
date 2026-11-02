#!/usr/bin/env python3
"""kite_mote_colors - the four elements' shades of LIME: one lime at four lightnesses.

    python3 Tools/Build/kite_mote_colors.py              # print the ladder, write the asset, the lab and the palette
    python3 Tools/Build/kite_mote_colors.py --check      # exit 1 if any of the three drifted from the ladder
    python3 Tools/Build/kite_mote_colors.py --self-test  # prove the checks fire

THE LADDER (Garrett, 2026-10-31, picked in the Lime Element Shades page, https://claude.ai/artifact/9EiMAiant8FR6wXbtesyTP):
"keep them similar hues so they don't have the ugly multitone interactions when grouped. also our eyes process lightness
faster than frequency ... so we might get more ability to distinguish them quickly, especially the wrasse, if they are
different lightnesses ... make charge the darkest because its plasma lights it up. lets distribute the other three
along a lightness axis within a yellow-green hue within the LIME family."

  - ONE lime: red and green only (no blue, no orange), red:green fixed at an HSV hue of LADDER_HSV_HUE (84). Scaling a
    colour's linear RGB keeps its OKLab hue exactly and scales its OKLab L by the cube root, so the four shades are that
    one lime at four brightnesses: one hue, no multitone clash when they are grouped.
  - FOUR lightnesses, evenly spaced in OKLab L from LADDER_L[0] (0.40) to LADDER_L[1] (0.902, the top of that lime),
    about 8.4 just-noticeable differences apart.
  - THE ORDER, darkest first: LADDER_ORDER = Charge, Mass, Space, Time. Charge is the darkest (its plasma lights it up).
  - IN LIME: every shade must rest in Lime in the colour source of truth (Docs/VesselModels/colour_domains.json,
    Tools/Build/colour_domains.py), clear of the band. Charge sits 0.2 JND inside it.

It writes KiteNavigatorConfig.asset's `elementColors` (the Wrasse's kite view motes; sRGB, Charge, Mass, Space, Time -
index = Element - 1), the Wrasse Lasso Lab's LIME_E line, and the palette's `EnvironmentColors.ElementalCrystalColors`
(OriginalColorSetSO.asset), which every other lime element reads: the four elemental crystals
(author_elemental_crystal_colors.py, author_mass_crystal_look.py), the omni crystal's element shapes
(author_omni_crystal_triangles.py, Docs/PALETTE.md §2.12) and the elemental converter's animation
(author_elemental_converter.py). Run those after this. The tail, the saved-boost dots and every other CTA lime stay
the palette's lime.

HISTORY: from 2026-10-21 to 2026-10-31 the shades were the corners of the largest tetrahedron in a nearest-anchor lime
boundary, four hues 15 degrees apart with Charge the LIGHTEST; COLOR.md §5b records that rule. The boundary helpers
below (families, in_lime, apart) remain for crystal_bloom_preview.py --propose.
"""
import math
import os
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
LADDER_HSV_HUE = 84                      # the one lime: red = (120 - 84) / 60 = 0.6 of green, blue 0 (sRGB)
LADDER_L = (0.40, 0.902)                 # OKLab L of the darkest and the lightest rung, evenly spaced between
LADDER_ORDER = ("Charge", "Mass", "Space", "Time")   # darkest to lightest
PICKED = {"Charge": "#2E5300", "Mass": "#508900", "Space": "#73C200", "Time": "#99FF00"}   # as the page saved it
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


# ----------------------------------------------------------------------------- boundary helpers (crystal_bloom_preview)
def apart(lab_a, lab_b):
    """Two colours differ by APART in value, in saturation and in hue (the retired rule; crystal_bloom_preview --propose)."""
    la, ca, ha = lch(lab_a)
    lb, cb, hb = lch(lab_b)
    dh = abs((ha - hb + 180) % 360 - 180)
    return abs(la - lb) >= APART and abs(ca - cb) >= APART and 0.5 * (ca + cb) * math.radians(dh) >= APART


# ----------------------------------------------------------------------------- the ladder
def gamma(v):
    v = max(0.0, min(1.0, v))
    return 12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055


def ladder():
    """The four colours, index = Element - 1 (Charge, Mass, Space, Time), each rounded to 4 places."""
    base = (to_linear((120.0 - LADDER_HSV_HUE) / 60.0), 1.0, 0.0)
    top = oklab(tuple(gamma(v) for v in base))[0]
    lo, hi = LADDER_L
    hi = min(hi, top)
    by_name = {}
    for rung, name in enumerate(LADDER_ORDER):
        L = lo + (hi - lo) * rung / 3
        k = (L / top) ** 3                         # OKLab L scales with the cube root of linear RGB
        by_name[name] = tuple(round(gamma(k * v), 4) for v in base)
    return [by_name[e] for e in ELEMENTS]


def derive():
    return ladder()


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
    errs += ladder_errors(colors)
    return errs


def ladder_errors(colors):
    """The ladder's own promises: in Lime (the source of truth), one hue, evenly spaced lightness, the picked order."""
    import colour_domains   # here, not at the top: colour_domains imports this module
    errs, defs = [], colour_domains.load()
    for e, c in zip(ELEMENTS, colors):
        fam, top, dist = colour_domains.classify(defs, c)
        if fam != "lime":
            errs.append("%s %s is not in Lime (%s) in the colour source of truth" % (e, hexof(c),
                        "in the band" if top == "lime" else "in " + top))
    labs = {e: lch(oklab(c)) for e, c in zip(ELEMENTS, colors)}
    hues = [labs[e][2] for e in ELEMENTS]
    if max(hues) - min(hues) > 0.5:
        errs.append("the four shades are not one hue (%.1f to %.1f degrees)" % (min(hues), max(hues)))
    ls = [labs[e][0] for e in LADDER_ORDER]
    if any(b <= a for a, b in zip(ls, ls[1:])):
        errs.append("the shades are not darkest to lightest in the order %s" % ", ".join(LADDER_ORDER))
    steps = [b - a for a, b in zip(ls, ls[1:])]
    if max(steps) - min(steps) > 0.003:
        errs.append("the lightness steps are not even (%s)" % ", ".join("%.3f" % v for v in steps))
    return errs


def report(colors):
    import colour_domains
    defs = colour_domains.load()
    print("kite_mote_colors: one lime (HSV hue %d), OKLab L %.3f to %.3f, darkest to lightest %s"
          % (LADDER_HSV_HUE, LADDER_L[0], LADDER_L[1], ", ".join(LADDER_ORDER)))
    for e, c in zip(ELEMENTS, colors):
        L, C, h = lch(oklab(c))
        fam, top, dist = colour_domains.classify(defs, c)
        print("  %-6s %s  sRGB (%.4f, %.4f, %.4f)  L %.3f  C %.3f  hue %3.0f  %s"
              % (e, hexof(c).upper(), c[0], c[1], c[2], L, C, h,
                 ("in Lime, %.1f JND inside its border" % ((dist - defs["margin"] / 2) / 0.02)) if fam == "lime" else "NOT IN LIME"))


def self_test():
    bad = []
    colors = ladder()
    for e, c in zip(ELEMENTS, colors):
        if hexof(c).upper() != PICKED[e]:
            bad.append("the ladder's %s is %s, the page saved %s" % (e, hexof(c).upper(), PICKED[e]))
    asset, lab = write_asset(read(ASSET), colors), write_lab(read(LAB), colors)
    palette = write_palette(read(PALETTE), colors)
    if check(asset, lab, colors, None, palette):
        bad.append("a freshly written asset, lab and palette fail the check: %s" % check(asset, lab, colors, None, palette))
    if write_asset(asset, colors) != asset or write_lab(lab, colors) != lab or write_palette(palette, colors) != palette:
        bad.append("writing is not idempotent")
    planted = [
        ("a palette crystal colour set to the omni's lime", (asset, lab, colors, PALETTE_RE.sub(lambda _: palette_block([(0.4862745, 0.9882354, 0.0)] + colors[1:]), palette, count=1))),
        ("a palette with no crystal colours", (asset, lab, colors, PALETTE_RE.sub("", palette, count=1))),
        ("a tampered asset colour", (ASSET_RE.sub(lambda _: asset_block([(0.6, 0.2, 0.2)] + colors[1:]), asset, count=1), lab, colors, None)),
        ("a tampered lab colour", (asset, lab.replace(hexof(colors[0]), "#ffffff", 1), colors, None)),
        ("a grey element colour (not in Lime)", (asset, lab, [(0.5, 0.5, 0.5)] + colors[1:], None)),
        ("two shades of different hue", (asset, lab, [colors[0], (0.25, 1.0, 0.0)] + colors[2:], None)),
        ("Charge lighter than Mass", (asset, lab, [colors[1], colors[0]] + colors[2:], None)),
        ("uneven lightness steps", (asset, lab, colors[:3] + [(0.5, 0.8333, 0.0)], None)),
    ]
    for label, (a, l, c, pal) in planted:
        if not check(a, l, c, None, pal):
            bad.append("%s passes the check" % label)
    print("kite_mote_colors self-test: " + ("ok" if not bad else "FAILED"))
    for b in bad:
        print("  -", b)
    return 1 if bad else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    colors = derive()
    report(colors)
    if "--check" in argv:
        errs = check(read(ASSET), read(LAB), colors, None, read(PALETTE))
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
