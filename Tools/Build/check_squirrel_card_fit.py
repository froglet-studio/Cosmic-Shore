#!/usr/bin/env python3
"""Measure the Squirrel ability row's two GENERATED readouts against the things they have to fit.

Both live inside a lockup card whose geometry belongs to a shared style asset, and both are laid
out in the ICON's own authored units while being DRAWN at the lockup's kerning - so every number in
them is a relationship between three files, and none of the three knows about the other two. This
is a READER: it writes nothing and only ever reports.

What it proves, and why each one is silent if it breaks:

  1. The Mass card's tunnel accent stays inside the Boost Ring sprite's own HOLE. The hole radius is
     MEASURED off the shipped PNG rather than assumed, because the whole reason the two colours can
     sit on one card is that they never touch a pixel (Docs/PALETTE.md 4.3: two saturated hues are
     separated, never blended). Overlap does not error - it muddies.

  2. Every tunnel ring clears a pixel when drawn, and the gap between neighbours does too. A ring
     that goes sub-pixel simply stops being visible, which reads as a SHORTER tunnel rather than as
     a broken one.

  3. The steal count fits FOUR digits, measured off the shipped font's own advance table. A wrapped
     or ellipsised number is a wrong reading that looks deliberate.

  4. The steal count sits below the reach ring at its MAXIMUM radius (so it can never compete with
     the ring) and still lands clear of the ability plate's bottom edge, above the control chip.
     Overhanging the plate is not clipped by anything, so it would just quietly collide with the
     chip.

Usage: check_squirrel_card_fit.py [--check | --self-test]
"""
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is wrong: {ROOT}"

VIEW = "Assets/_Scripts/UI/View/SquirrelVesselHUDView.cs"
GRAPHIC = "Assets/_Scripts/UI/View/PerspectiveTunnelGraphic.cs"
STYLE = "Assets/Resources/AbilityLockupStyle.asset"
PREFAB = "Assets/_Prefabs/UI Elements/VesselHUD/SquirrelHUDVariant.prefab"
SPRITE = "Assets/_Graphics/Design Assests/HUD UI/Squirrel/BoostRingCrossSectionIcon.png"
FONT = "Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/ALDRICH-REGULAR SDF.asset"

# Drawn-unit floors. Stated here rather than inline so a retune can see what it is trading against.
MIN_RING_THICKNESS_PX = 0.85    # under this a feathered band stops reading as a line at all
MIN_RING_GAP_PX = 1.25          # under this two rings read as one thick one
PLATE_CLEARANCE_PX = 2.0        # air between the count's bottom and the plate's bottom edge
DIGITS_REQUIRED = 4


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace") as f:
        return f.read()


def csfloat(src, name, what):
    m = re.search(r"\b" + re.escape(name) + r"\s*=\s*(-?[\d.]+)f?\s*;", src)
    if not m:
        raise SystemExit(f"could not read {name} from {what}")
    return float(m.group(1))


def csint(src, name, what):
    m = re.search(r"\b" + re.escape(name) + r"\s*=\s*(-?\d+)\s*;", src)
    if not m:
        raise SystemExit(f"could not read {name} from {what}")
    return int(m.group(1))


def yamlfloat(src, name, what):
    m = re.search(r"^\s*" + re.escape(name) + r":\s*(-?[\d.]+)\s*$", src, re.M)
    if not m:
        raise SystemExit(f"could not read {name} from {what}")
    return float(m.group(1))


def measure_sprite_hole():
    """Radius, as a fraction of the sprite's half-extent, of the empty middle of the ring art."""
    from PIL import Image
    im = Image.open(os.path.join(ROOT, SPRITE)).convert("RGBA")
    w, h = im.size
    px = im.load()
    cx, cy = (w - 1) / 2.0, (h - 1) / 2.0
    half = min(w, h) / 2.0
    # The smallest radius at which the art starts. Sampled per-pixel; the art is a clean annulus.
    inner = 1.0
    for y in range(h):
        for x in range(w):
            if px[x, y][3] > 96:
                r = math.hypot(x - cx, y - cy) / half
                if r < inner:
                    inner = r
    return inner, (w, h)


def measure_widest_digit():
    """(advance, pointSize) for the widest digit in the shipped font atlas."""
    t = read(FONT)
    cmap = {int(u): int(g) for u, g in
            re.findall(r"m_Unicode:\s*(\d+)\s*\n\s*m_GlyphIndex:\s*(\d+)", t)}
    glyphs = {}
    for m in re.finditer(
            r"m_Index:\s*(\d+)\s*\n\s*m_Metrics:\s*\n\s*m_Width:\s*[-\d.]+\s*\n"
            r"\s*m_Height:\s*[-\d.]+\s*\n\s*m_HorizontalBearingX:\s*[-\d.]+\s*\n"
            r"\s*m_HorizontalBearingY:\s*[-\d.]+\s*\n\s*m_HorizontalAdvance:\s*([-\d.]+)", t):
        glyphs[int(m.group(1))] = float(m.group(2))
    advances = [glyphs[cmap[u]] for u in range(48, 58) if cmap.get(u) in glyphs]
    if len(advances) != 10:
        raise SystemExit("could not read all ten digit advances from the font asset")
    point = yamlfloat(t, "m_PointSize", FONT)
    return max(advances), point


def icon_size_delta():
    """The Mass icon's authored rect, read through the prefab's own tubeCooldownIcon reference."""
    t = read(PREFAB)
    m = re.search(r"^\s*tubeCooldownIcon:\s*\{fileID:\s*(\d+)\}", t, re.M)
    if not m:
        raise SystemExit("tubeCooldownIcon is not bound in the prefab")
    image_id = m.group(1)
    docs = t.split("--- ")
    go = None
    for d in docs:
        if d.split("\n", 1)[0].strip().endswith("&" + image_id):
            g = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", d)
            go = g.group(1) if g else None
    if not go:
        raise SystemExit(f"could not find the Image component {image_id}")
    for d in docs:
        if d.split("\n", 1)[0].strip().startswith("!u!224") and \
                f"m_GameObject: {{fileID: {go}}}" in d:
            s = re.search(r"m_SizeDelta:\s*\{x:\s*([\d.]+),\s*y:\s*([\d.]+)\}", d)
            if s:
                return float(s.group(1)), float(s.group(2))
    raise SystemExit("could not find the Mass icon's RectTransform")


def gather():
    """Every number the four checks need, read from the five files that own them."""
    view = read(VIEW)
    graphic = read(GRAPHIC)
    style = read(STYLE)

    icon_w, icon_h = icon_size_delta()
    hole01, sprite_px = measure_sprite_hole()
    widest, point = measure_widest_digit()
    icon_box = yamlfloat(style, "iconBoxSize", STYLE)

    return dict(
        front_r=csfloat(view, "tunnelFrontRadius", VIEW),
        rings=csint(view, "tunnelRings", VIEW),
        depth_step=csfloat(view, "tunnelDepthStep", VIEW),
        front_t=csfloat(view, "tunnelFrontThickness", VIEW),
        reach_max=csfloat(view, "reachRingMaxRadius", VIEW),
        reach_min=csfloat(view, "reachRingMinRadius", VIEW),
        font_size=csfloat(view, "stealCountFontSize", VIEW),
        count_gap=csfloat(view, "stealCountGap", VIEW),
        count_h=csfloat(view, "stealCountHeight", VIEW),
        min_t=csfloat(graphic, "minThickness", GRAPHIC),
        feather=csfloat(graphic, "feather", GRAPHIC),
        icon_box=icon_box,
        cell_h=yamlfloat(style, "abilityCellHeight", STYLE),
        chip_gap=yamlfloat(style, "chipGap", STYLE),
        icon_w=icon_w,
        icon_h=icon_h,
        kern=icon_box / icon_w,
        hole01=hole01,
        hole_r=hole01 * (icon_w / 2.0),
        sprite_px=sprite_px,
        widest=widest,
        point=point,
    )


def evaluate(p, verbose=True):
    """The four relationships. Pure in p, so --self-test can perturb one at a time."""
    out = []
    fails = []

    def say(s):
        out.append(s)

    say(f"lockup     iconBoxSize {p['icon_box']:g}  plate height {p['cell_h']:g}  "
        f"chip gap {p['chip_gap']:g}")
    say(f"icon       authored {p['icon_w']:g}x{p['icon_h']:g}  -> kerned x{p['kern']:.3f}")
    say(f"sprite     {p['sprite_px'][0]}x{p['sprite_px'][1]}  art starts at r {p['hole01']:.3f} "
        f"= {p['hole_r']:.2f} authored units")
    say(f"font       widest digit advance {p['widest']:.3f} @ {p['point']:g}pt")

    # 1 - the tunnel's outermost edge, feather included, inside the sprite's hole.
    tunnel_edge = p["front_r"] + p["feather"]
    say(f"\n[1] tunnel outer edge {tunnel_edge:.2f} vs sprite hole {p['hole_r']:.2f} authored")
    if tunnel_edge > p["hole_r"]:
        fails.append(f"the tunnel reaches {tunnel_edge:.2f} into ring art that starts at "
                     f"{p['hole_r']:.2f} - the danger tint and the team accent would blend")

    # 2 - every ring clears a pixel, and so does every gap between neighbours.
    say("[2] rings (authored radius / drawn thickness / drawn gap to the next)")
    prev = None
    for k in range(p["rings"]):
        project = 1.0 / (1.0 + k * p["depth_step"])
        r = p["front_r"] * project
        th = max(p["min_t"], p["front_t"] * project)
        drawn_t = th * p["kern"]
        gap = "" if prev is None else f"{(prev - r) * p['kern']:.2f}px"
        say(f"     ring {k}: r {r:6.2f}   t {drawn_t:5.2f}px   gap {gap}")
        if drawn_t < MIN_RING_THICKNESS_PX:
            fails.append(f"tunnel ring {k} draws {drawn_t:.2f}px thick, under the "
                         f"{MIN_RING_THICKNESS_PX}px floor - it would not read as a ring")
        if prev is not None and (prev - r) * p["kern"] < MIN_RING_GAP_PX:
            fails.append(f"tunnel rings {k-1} and {k} are {(prev - r) * p['kern']:.2f}px apart, "
                         f"under the {MIN_RING_GAP_PX}px floor - they would read as one band")
        prev = r

    # 3 - four digits fit the count's box, measured off the font.
    per_digit = p["widest"] * p["font_size"] / p["point"]
    say(f"\n[3] count at {p['font_size']:g}pt: one digit advances {per_digit:.2f} authored")
    for n in (DIGITS_REQUIRED, DIGITS_REQUIRED + 1):
        w = per_digit * n
        say(f"     {n} digits = {w:.1f} of the {p['icon_w']:g} box ({w / p['icon_w']:.0%})")
        if n == DIGITS_REQUIRED and w > p["icon_w"]:
            fails.append(f"{n} digits need {w:.1f} of an {p['icon_w']:g} box - the count would "
                         f"overflow")

    # 4 - the count is below the ring's maximum and clear of the plate's bottom.
    top = p["reach_max"] + p["count_gap"]
    bottom_drawn = (top + p["count_h"]) * p["kern"]
    plate_half = p["cell_h"] / 2.0
    say(f"\n[4] count box: top {top:.1f} authored (ring max {p['reach_max']:g}, "
        f"rest {p['reach_min']:g}), bottom {bottom_drawn:.2f}px vs plate half-height "
        f"{plate_half:g}px")
    if p["count_gap"] < p["feather"]:
        fails.append(f"the count's gap {p['count_gap']:g} is under the ring's feather "
                     f"{p['feather']:g} - the number would touch the ring at full Space")
    room = plate_half - bottom_drawn
    say(f"     clearance below the count: {room:.2f}px (chip starts {p['chip_gap']:g}px below that)")
    if room < PLATE_CLEARANCE_PX:
        fails.append(f"the count's bottom is {room:.2f}px from the plate's edge, under the "
                     f"{PLATE_CLEARANCE_PX}px floor - it would crowd the control chip")

    if verbose:
        print("\n".join(out))
    return fails


# Each control names the check it must trip, so a control that fires the WRONG check is a failure
# too - the thing that makes a gate trustworthy is watching it fail for the stated reason.
CONTROLS = [
    ("tunnel over the ring art", dict(front_r=24.0), "would blend"),
    ("a far ring gone sub-pixel", dict(front_t=0.9, min_t=0.1), "would not read as a ring"),
    ("rings crowded together", dict(depth_step=0.08), "read as one band"),
    ("count sized for three digits", dict(font_size=30.0), "would overflow"),
    ("count touching the ring", dict(count_gap=0.0), "would touch the ring"),
    ("count pushed onto the chip", dict(count_h=30.0), "crowd the control chip"),
]


def self_test():
    base = gather()
    ok = True
    if evaluate(base, verbose=False):
        print("SELF-TEST FAIL: the shipped numbers do not pass")
        return 1
    print("shipped numbers pass; now the negative controls\n")
    for name, patch, expect in CONTROLS:
        p = dict(base)
        p.update(patch)
        fails = evaluate(p, verbose=False)
        hit = any(expect in f for f in fails)
        print(f"  {'FIRED ' if hit else 'MISSED'}  {name}")
        if not hit:
            ok = False
            for f in fails:
                print(f"            (got instead: {f})")
    print()
    if not ok:
        print("SELF-TEST FAIL: a control did not trip its own check")
        return 1
    print(f"SELF-TEST PASS - all {len(CONTROLS)} controls fired")
    return 0


def main():
    if "--self-test" in sys.argv:
        return self_test()
    fails = evaluate(gather())
    print()
    if fails:
        for f in fails:
            print("FAIL: " + f)
        return 1
    print("PASS - all four relationships hold")
    return 0


if __name__ == "__main__":
    sys.exit(main())
