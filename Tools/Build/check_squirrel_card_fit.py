#!/usr/bin/env python3
"""Measure the Squirrel Space card's GENERATED readout against the things it has to fit.

The reach ring and the steal count live inside a lockup card whose geometry belongs to a shared
style asset, and both are laid out in the ICON's own authored units while being DRAWN at the
lockup's kerning - so every number in them is a relationship between three files, and none of the
three knows about the other two. This is a READER: it writes nothing and only ever reports.

What it proves, and why each one is silent if it breaks:

  1. THE WHOLE READOUT FITS THE BOX AN AUTHORED ICON DRAWS IN. This is the check the card was
     reported for: the lockup kerns an icon's RECT and cannot see what a generated child draws
     inside it, so a ring at a radius that nearly fills the box and a count hung off the plate
     below make the card read as bigger than its four neighbours - which is the same failure mode
     as the un-kerned core card, arriving from the other direction. Nothing clips it and nothing
     errors; it just looks wrong beside the other four.

  2. The ring clears a pixel when drawn, and the count never touches it at any Space level - the
     ring is a live measurement that grows, so the one place the two can collide is at full Space.

  3. The steal count fits FOUR digits, measured off the shipped font's own advance table. A wrapped
     or ellipsised number is a wrong reading that looks deliberate.

  4. The count still lands clear of the ability plate's bottom edge, above the control chip. Check 1
     subsumes this at the shipped numbers and it is kept as the absolute floor: the plate is the
     thing that actually collides with something, where the icon box is a rule about how the card
     READS.

Usage: check_squirrel_card_fit.py [--check | --self-test]
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is wrong: {ROOT}"

VIEW = "Assets/_Scripts/UI/View/SquirrelVesselHUDView.cs"
RING = "Assets/_Scripts/UI/View/ScopeRingGraphic.cs"
STYLE = "Assets/Resources/AbilityLockupStyle.asset"
PREFAB = "Assets/_Prefabs/UI Elements/VesselHUD/SquirrelHUDVariant.prefab"
FONT = "Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/ALDRICH-REGULAR SDF.asset"

# Drawn-unit floors. Stated here rather than inline so a retune can see what it is trading against.
MIN_RING_THICKNESS_PX = 0.85    # under this a feathered band stops reading as a line at all
PLATE_CLEARANCE_PX = 2.0        # air between the count's bottom and the plate's bottom edge
DIGITS_REQUIRED = 4


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8-sig", errors="replace") as f:
        return f.read()


def csfloat(src, name, what):
    m = re.search(r"\b" + re.escape(name) + r"\s*=\s*(-?[\d.]+)f?\s*;", src)
    if not m:
        raise SystemExit(f"could not read {name} from {what}")
    return float(m.group(1))


def yamlfloat(src, name, what):
    m = re.search(r"^\s*" + re.escape(name) + r":\s*(-?[\d.]+)\s*$", src, re.M)
    if not m:
        raise SystemExit(f"could not read {name} from {what}")
    return float(m.group(1))


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


def authored_icon_size():
    """The row's authored icon rect, read through the prefab's own Mass icon reference.

    The Space icon is GENERATED at the same 80x80 the four authored ones measure, so the authored
    one is what the generated rect has to match - read it rather than retyping it, since the kern
    this check divides by is the lockup's own iconBoxSize over exactly this number."""
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
    """Every number the four checks need, read from the four files that own them."""
    view = read(VIEW)
    ring = read(RING)
    style = read(STYLE)

    icon_w, icon_h = authored_icon_size()
    widest, point = measure_widest_digit()
    icon_box = yamlfloat(style, "iconBoxSize", STYLE)

    return dict(
        centre_y=csfloat(view, "reachRingCenterY", VIEW),
        reach_max=csfloat(view, "reachRingMaxRadius", VIEW),
        reach_min=csfloat(view, "reachRingMinRadius", VIEW),
        reach_t=csfloat(view, "reachRingThickness", VIEW),
        font_size=csfloat(view, "stealCountFontSize", VIEW),
        count_gap=csfloat(view, "stealCountGap", VIEW),
        count_h=csfloat(view, "stealCountHeight", VIEW),
        feather=csfloat(ring, "feather", RING),
        icon_box=icon_box,
        cell_h=yamlfloat(style, "abilityCellHeight", STYLE),
        chip_gap=yamlfloat(style, "chipGap", STYLE),
        icon_w=icon_w,
        icon_h=icon_h,
        kern=icon_box / icon_w,
        widest=widest,
        point=point,
    )


def evaluate(p, verbose=True):
    """The four relationships. Pure in p, so --self-test can perturb one at a time."""
    out = []
    fails = []

    def say(s):
        out.append(s)

    # Every extent below is in the ICON's authored units, measured from the middle of its box.
    ring_top = p["centre_y"] + p["reach_max"] + p["feather"]
    ring_bottom_max = p["centre_y"] - p["reach_max"] - p["feather"]
    ring_bottom_rest = p["centre_y"] - p["reach_min"] - p["feather"]
    count_top = p["centre_y"] - p["reach_max"] - p["count_gap"]
    count_bottom = count_top - p["count_h"]
    half_box = p["icon_h"] / 2.0

    say(f"lockup     iconBoxSize {p['icon_box']:g}  plate height {p['cell_h']:g}  "
        f"chip gap {p['chip_gap']:g}")
    say(f"icon       authored {p['icon_w']:g}x{p['icon_h']:g}  -> kerned x{p['kern']:.3f}")
    say(f"font       widest digit advance {p['widest']:.3f} @ {p['point']:g}pt")
    say(f"\nreadout    (authored units, from the middle of the icon's box)")
    say(f"     ring lifted to y {p['centre_y']:+g}, r {p['reach_min']:g} at rest -> "
        f"{p['reach_max']:g} at full Space")
    say(f"     ring     top {ring_top:+7.2f}   bottom {ring_bottom_max:+7.2f} (full) / "
        f"{ring_bottom_rest:+7.2f} (rest)")
    say(f"     count    top {count_top:+7.2f}   bottom {count_bottom:+7.2f}")

    # 1 - the whole readout inside the box an authored icon draws in.
    say(f"\n[1] readout span {count_bottom:+.2f} .. {ring_top:+.2f} vs the icon's own "
        f"+/-{half_box:g}")
    if ring_top > half_box:
        fails.append(f"the ring reaches {ring_top:.2f} above the icon's centre, past its own "
                     f"{half_box:g} half-box - the card would read as bigger than its neighbours")
    if count_bottom < -half_box:
        fails.append(f"the count reaches {count_bottom:.2f} below the icon's centre, past its own "
                     f"{half_box:g} half-box - the card would read as bigger than its neighbours")

    # 2 - the ring reads as a ring, and the count never touches it.
    drawn_t = p["reach_t"] * p["kern"]
    say(f"[2] ring draws {drawn_t:.2f}px thick; count has {ring_bottom_max - count_top:.2f} "
        f"of gap under the ring at its widest")
    if drawn_t < MIN_RING_THICKNESS_PX:
        fails.append(f"the reach ring draws {drawn_t:.2f}px thick, under the "
                     f"{MIN_RING_THICKNESS_PX}px floor - it would not read as a ring")
    if p["count_gap"] < p["feather"]:
        fails.append(f"the count's gap {p['count_gap']:g} is under the ring's feather "
                     f"{p['feather']:g} - the number would touch the ring at full Space")

    # 3 - four digits fit the count's box, measured off the font.
    per_digit = p["widest"] * p["font_size"] / p["point"]
    say(f"\n[3] count at {p['font_size']:g}pt: one digit advances {per_digit:.2f} authored")
    for n in (DIGITS_REQUIRED, DIGITS_REQUIRED + 1):
        w = per_digit * n
        say(f"     {n} digits = {w:.1f} of the {p['icon_w']:g} box ({w / p['icon_w']:.0%})")
        if n == DIGITS_REQUIRED and w > p["icon_w"]:
            fails.append(f"{n} digits need {w:.1f} of an {p['icon_w']:g} box - the count would "
                         f"overflow")

    # 4 - the absolute floor: clear of the plate, above the chip.
    plate_half = p["cell_h"] / 2.0
    room = plate_half - abs(count_bottom) * p["kern"]
    say(f"\n[4] count bottom {abs(count_bottom) * p['kern']:.2f}px vs plate half-height "
        f"{plate_half:g}px - clearance {room:.2f}px "
        f"(chip starts {p['chip_gap']:g}px below that)")
    if room < PLATE_CLEARANCE_PX:
        fails.append(f"the count's bottom is {room:.2f}px from the plate's edge, under the "
                     f"{PLATE_CLEARANCE_PX}px floor - it would crowd the control chip")

    if verbose:
        print("\n".join(out))
    return fails


# Each control names the check it must trip, so a control that fires the WRONG check is a failure
# too - the thing that makes a gate trustworthy is watching it fail for the stated reason.
CONTROLS = [
    # The shipped-before-this-pass numbers: ring centred at 0 and a count hung off the plate.
    ("the first cut's centred ring", dict(centre_y=0.0, reach_max=34.0, reach_min=20.0,
                                          count_gap=1.0),
     "read as bigger than its neighbours"),
    ("ring lifted out of the box", dict(centre_y=26.0), "read as bigger than its neighbours"),
    ("a hairline ring", dict(reach_t=0.5), "would not read as a ring"),
    ("count touching the ring", dict(count_gap=0.0), "would touch the ring"),
    ("count sized for three digits", dict(font_size=30.0), "would overflow"),
    ("count pushed onto the chip", dict(centre_y=-20.0, count_h=30.0),
     "crowd the control chip"),
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
