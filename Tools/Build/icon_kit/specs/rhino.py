"""The Rhino's ability-row icons (Resources/ElementalAbilityMaps/Rhino.asset), angular, one object each.

  Charge  Energize     hold both triggers to LIGHT the blade (the cooldown is its only limit); Charge scales how
                       many petals a sword hit takes from a rival, and at Charge 5 the sword POCKETS them instead
                       of knocking them loose as crystals (RHINO_ENERGY_SWORD.md, 2026-10-26)
  Mass    Afterimage   the unlit blade keeps leaving prisms in its own shape and pose for 5 s
  Space   Blade Reach  Space alone grows the blade, 24 -> 40; at Space 5 an energized blade pops super-shields
  Time    Ramp Spool   how fast the ramp winds the Rhino up to top speed; at Time 5 the Momentum ward

Before: four white-square PLACEHOLDER sprites. One card at a time with Garrett, options in the hull's whole lockup.

Charge is THE KATANA (Garrett 2026-10-28, round 3 option F). The blade follows the real sword: in the game it is the
ForceFieldSkimmer's capsule (mesh 10208, local scale (1.5, 24, 4.8)) standing off the nose (mount (0, -1, 20.7), raised
20 deg); the triggers swipe it (yaw + roll, up to 90 deg each way, RT - LT) and chop it down (pitch, up to 65 deg,
RT + LT). Rounds 1-2 drew it as a lozenge and a straight sword in the domain colour - "make it look more sword like and
not team colored. when lit make it the danger color. the idea is to follow the actual sword", then "more like a
katana." The HUD turns the card's katana with the live swipe (half the real angle), MIRRORS it so the edge leads the
swing ("it should flip the sword as well as rotate it so it is swiping with the sharp part"), and cross-fades it to
the straight blade seen from behind as both triggers chop ("the both triggers chop should remove the curve so it
looks like it is from behind"). White while dark, danger while lit.
"""
import math

import numpy as np
from PIL import Image

from canvas import GX, GY, N, empty, fill, rect, rotate

HULL = "Rhino"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Rhino/"


def _png_mask(rel):
    """A committed sprite's alpha as a kit mask (the lockup's siblings while they are still placeholders)."""
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def placeholder(name):
    return lambda: _png_mask(ICON_DIR + f"Rhino_{name}-PLACEHOLDER.png")


_BEFORE = dict(before=ICON_DIR + "Rhino_SwordEnergy-PLACEHOLDER.png", before_rev="90a5b7cf5", before_gauge_value=0.55)


# ---- the katana -------------------------------------------------------------------------------------------------
# A long shallow curve (sori) bending back toward the spine, one cutting edge on the convex side (the LEFT, in the
# sprite), the angled kissaki point (the edge turns up at the yokote and meets the spine), a small tsuba where the
# blade leaves the nose and a long tsuka. The pivot is the hilt: the view rotates, mirrors and foreshortens about it.
KATANA_HILT = (60.0, 100.0)  # the pivot, in card px (the view's katanaHilt): high enough for the long grip below it
KATANA_LEN = 92              # habaki to point (px)
KATANA_W = 12                # the blade's depth
KATANA_SORI = 14             # how far the point sits off the hilt's straight line (px): the curve
CARD_SWIPE = 0.5             # the card turns half the sword's real swipe (90 deg real -> 45), so it stays in the card
REAL_SWIPE = 90.0            # RhinoShieldSwipeConfig.swipeYawDegrees: a full single-trigger pull
REAL_CHOP = 65.0             # RhinoShieldSwipeConfig.chopPitchDegrees: both triggers fully pulled


def katana(angle=0.0, chop=0.0, tsuba=True, tsuka=True, length=KATANA_LEN, w=KATANA_W, sori=KATANA_SORI, hilt=KATANA_HILT, mirror=False):
    """The katana standing off the hilt: turned `angle` deg clockwise (the swipe, already scaled to the card),
    foreshortened by cos(`chop`), and `mirror`ed about the hilt so its edge (drawn on the left) leads a swipe right.
    `sori=0` is the blade seen from behind, as the chop shows it."""
    hx, hy = hilt
    fx = -1.0 if mirror else 1.0
    L = length * math.cos(math.radians(chop))
    k = min(0.17, 15.0 / max(L, 1.0))          # the kissaki's share of the blade
    steps = 24

    def centre(t):                              # the curve: off the line by sori * t^2, bending to the spine (+x)
        return sori * t * t * (L / length), -L * t

    def normal(t):                              # the unit vector toward the spine side at t
        dx, dy = 2 * sori * t * (L / length), -L
        n = math.hypot(dx, dy)
        return -dy / n, dx / n

    spine, edge = [], []
    for i in range(steps + 1):
        t = (1 - k) * i / steps
        cx, cy = centre(t)
        nx, ny = normal(t)
        hw = w / 2 * (1 - 0.25 * t)              # a slight taper toward the point
        spine.append((cx + nx * hw, cy + ny * hw))
        edge.append((cx - nx * hw, cy - ny * hw))
    # the kissaki: the spine runs on and dips to the point; the edge turns up at the yokote and runs straight to it
    tx, ty = centre(1.0)
    nx, ny = normal(1.0)
    hw = w / 2 * 0.75
    point = (tx + nx * hw * 0.6, ty + ny * hw * 0.6)
    sx, sy = centre(1 - k * 0.45)
    snx, sny = normal(1 - k * 0.45)
    spine.append((sx + snx * hw, sy + sny * hw))
    pts = spine + [point] + edge[::-1]
    m = fill(rotate([(hx + fx * x, hy + y) for x, y in pts], angle, hx, hy))
    if tsuba:                                   # the guard: a flat octagon across the blade's root
        g = [(-w * 0.95, 1), (-w * 0.7, -1.5), (w * 0.7, -1.5), (w * 0.95, 1), (w * 0.95, 5), (w * 0.7, 7.5),
             (-w * 0.7, 7.5), (-w * 0.95, 5)]
        m |= fill(rotate([(hx + x, hy + y) for x, y in g], angle, hx, hy))
    if tsuka:                                   # the grip: long, a touch narrower than the blade, its end cut square
        grip = [(-w * 0.42, 7), (w * 0.42, 7), (w * 0.38, 25), (-w * 0.38, 25)]
        m |= fill(rotate([(hx + x, hy + y) for x, y in grip], angle, hx, hy))
    return m


def katana_curved():
    return katana()


def katana_chop():
    return katana(sori=0.0)


CHOP_STRAIGHT01 = 0.75       # the view's katanaChopStraight01: the chop at which the blade shows from behind (sum 1.5 of
                             # 2, RhinoShieldSwipeConfig.stanceSumThreshold - the both-triggers energize stance)
CHOP_LENGTH01 = 0.8          # the view's katanaChopLength01: the blade's length on the card at a full chop (it tips away)


def katana_pose(swipe, chop01, facing=1.0):
    """What the HUD draws for a swipe (-1..1, RT - LT) and a chop (0..1): turned, mirrored when the swipe is to the
    right (at rest it keeps the side it last faced), shortened as it chops, and the straight blade from behind once
    both triggers are in. Mirrors RhinoVesselHUDView.KatanaPose so the sheet shows the HUD's states."""
    mirror = swipe > 0 or (swipe == 0 and facing < 0)
    ang = swipe * REAL_SWIPE * CARD_SWIPE
    length01 = 1.0 + (CHOP_LENGTH01 - 1.0) * chop01
    chop = math.degrees(math.acos(length01))
    straight = chop01 >= CHOP_STRAIGHT01
    return katana(ang, chop, mirror=mirror, sori=0.0 if straight else KATANA_SORI)


# (name, swipe -1..1, chop 0..1, lit, plate = EnergizeGauge01)
KATANA_STATES = [("at rest", 0.0, 0.0, False, 0.0),
                 ("RT: swiped right, edge leading", 1.0, 0.5, False, 0.0),
                 ("LT: swiped left, edge leading", -1.0, 0.5, False, 0.0),
                 ("half pull, right", 0.5, 0.25, False, 0.0),
                 ("both triggers: the chop, from behind", 0.0, 1.0, False, 0.55),
                 ("lit (still chopping)", 0.0, 1.0, True, 1.0),
                 ("lit, swiped right", 1.0, 0.5, True, 0.7),
                 ("cooling down", 0.0, 0.0, False, 0.0)]


def _katana_states():
    parts, states, gauge = {}, [], {}
    for k, (n, swipe, chop01, lit, g) in enumerate(KATANA_STATES):
        parts[f"k{k}"] = (lambda swipe=swipe, chop01=chop01: katana_pose(swipe, chop01))
        states.append((n, [(f"k{k}", "danger" if lit else "white")]))
        gauge[n] = (g, None)
    return parts, states, gauge


_KP, _KS, _KG = _katana_states()

PART_EXEMPT = {"coverage": "a part: the katana, slim by design", "box_fill": "a part", "in_box": "a part",
               "thin": "the katana is slim: Garrett 2026-10-28 'more like a katana'"}

ICONS = [
    dict(name="Rhino_Katana", element="Charge", act="Energize: the sword the Rhino swings, a katana at rest (composite; the HUD turns it live)",
         draw=katana_curved, exempt={"coverage": "a katana is slim (Garrett 2026-10-28)", "thin": "the katana is slim",
                                     "box_fill": "the blade stands on its hilt, off-centre by its curve",
                                     "in_box": "the blade runs the card's height, as the sword stands off the nose"}),
    dict(name="Rhino_KatanaCurved", element="Charge", act="Energize: the curved katana, the part the view turns and mirrors about the hilt",
         draw=katana_curved, subdir="Parts", family=False, exempt=PART_EXEMPT),
    dict(name="Rhino_KatanaChop", element="Charge", act="Energize: the katana seen from behind (no curve), the part the chop fades to",
         draw=katana_chop, subdir="Parts", family=False, exempt=PART_EXEMPT),
]

# The hull's other cards as the HUD draws them today (states.py options sheets show a candidate beside them).
LOCKUP = dict(
    Charge=dict(parts=dict(k=katana_curved), state=[("k", "white")]),
    Mass=dict(parts=dict(i=placeholder("Afterimage")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("BladeReach")), state=[("i", "white")]),
    Time=dict(parts=dict(i=placeholder("RampSpool")), state=[("i", "white")], gauge=0.55),
)

CARDS = [
    dict(name="energize", element="Charge", **_BEFORE,
         title="Energize: the katana turns with the live swipe (half the real angle), flips so the edge leads, shows the straight blade seen from behind once both triggers chop; white dark, danger lit",
         parts=_KP, states=_KS, gauge=_KG),
]

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/RhinoHUDVariant.prefab"
VIEW = "5161366343835098938"   # RhinoVesselHUDView on the variant

REBIND = [
    # The Charge card: the icon Image becomes a transparent container; the view GENERATES the katana under it (a pivot
    # at the hilt holding the curved blade and the straight one) from the two parts. The plate keeps its energize gauge.
    dict(file=VARIANT, pair=dict(container="7720000000000003015", view=VIEW, after="highlights", children=[],
                                 fields={"katanaCurvedSprite": ("icon", "Rhino_KatanaCurved"),
                                         "katanaChopSprite": ("icon", "Rhino_KatanaChop")})),
]


# ---- Mass: Afterimage options (2026-10-28) ------------------------------------------------------------------------
# The unlit blade keeps leaving PRISMS in its own shape and pose for 5 s (RHINO_SWORD_AFTERIMAGE.md): copies of the
# sword, built of prisms, hanging where it swung. A prism copy is the katana's blade cut across into blocks.
PRISM_PITCH = 13.0   # one prism's length along the blade (px)
PRISM_SEAM = 3.0     # the seam between prisms (px)


def prism_katana(angle, hilt=KATANA_HILT, length=KATANA_LEN, mirror=False):
    """The blade alone (no tsuba, no grip) at `angle`, cut across into prisms: an afterimage. Scaled with `length`
    (a copy further away is smaller), a touch wider than the sword so the blocks read."""
    hx, hy = hilt
    k = length / KATANA_LEN
    m = katana(angle, tsuba=False, tsuka=False, hilt=hilt, length=length, w=KATANA_W * 1.15 * k,
               sori=KATANA_SORI * k, mirror=mirror)
    a = math.radians(angle)
    along = (GX - hx) * math.sin(a) - (GY - hy) * math.cos(a)     # distance out along the blade from the hilt
    return m & ((along % (PRISM_PITCH * k)) >= PRISM_SEAM * max(k, 0.7))


# Mass is THE TRAIL (Garrett 2026-10-28, option H of K / H / I / J): the sword (white, as on the Charge card) swinging
# right past two prism copies of itself in the pilot's domain colour - they are the pilot's prisms - the copy nearer it
# brighter. Garrett on H: "the after image is flipped the wrong way": the copies are the sword where it WAS in the same
# swing, so they are mirrored with it, the edge leading right.
AFTER_BLADE_ANGLE = 20.0
AFTER_NEAR_ANGLE = -15.0
AFTER_FAR_ANGLE = -45.0
AFTER_NEAR_ALPHA = 0.8      # the view's afterimageNearAlpha
AFTER_FAR_ALPHA = 0.45      # the view's afterimageFarAlpha


def afterimage_blade():
    return katana(AFTER_BLADE_ANGLE, mirror=True)


def afterimage_near():
    return prism_katana(AFTER_NEAR_ANGLE, mirror=True)


def afterimage_far():
    return prism_katana(AFTER_FAR_ANGLE, mirror=True)


def afterimage():
    return afterimage_blade() | afterimage_near() | afterimage_far()


AFTER_LIT = [("far", "domain", AFTER_FAR_ALPHA), ("near", "domain", AFTER_NEAR_ALPHA), ("blade", "white")]
ICONS += [
    dict(name="Rhino_Afterimage", element="Mass", act="Afterimage: the sword swinging past two prism copies of itself (composite; the HUD draws the parts)",
         draw=afterimage, exempt={"components": "the sword and its prism copies", "specks": "the prism seams",
                                  "gap": "the prism seams", "thin": "the katana is slim", "in_box": "the sword runs the card's height"}),
    dict(name="Rhino_AfterimageBlade", element="Mass", act="Afterimage: the sword part (white)", draw=afterimage_blade,
         subdir="Parts", family=False, exempt=PART_EXEMPT),
    dict(name="Rhino_AfterimageNear", element="Mass", act="Afterimage: the nearer prism copy (domain colour)", draw=afterimage_near,
         subdir="Parts", family=False, exempt=dict(PART_EXEMPT, components="a copy built of prisms", specks="the prism seams", gap="the prism seams")),
    dict(name="Rhino_AfterimageFar", element="Mass", act="Afterimage: the further prism copy (domain colour, fainter)", draw=afterimage_far,
         subdir="Parts", family=False, exempt=dict(PART_EXEMPT, components="a copy built of prisms", specks="the prism seams", gap="the prism seams")),
]
LOCKUP["Mass"] = dict(parts=dict(blade=afterimage_blade, near=afterimage_near, far=afterimage_far), state=AFTER_LIT)

CARDS += [dict(name="afterimage", element="Mass", before=ICON_DIR + "Rhino_Afterimage-PLACEHOLDER.png", before_rev="80877373e",
               title="Afterimage: the sword (white) swinging past two prism copies of itself in your colour, shown while the copies are being laid",
               parts=dict(blade=afterimage_blade, near=afterimage_near, far=afterimage_far),
               states=[("idle: no afterimages", [("blade", "white")]),
                       ("laying (lit)", AFTER_LIT)])]

REBIND += [
    # The Mass card: the icon Image becomes a transparent container over three parts - the two prism copies (domain
    # colour, shown only while the afterimages are being laid) and the sword (white).
    dict(file=VARIANT, pair=dict(container="7720000000000003025", view=VIEW, after="highlights",
                                 children=[("AfterimageFar", "Rhino_AfterimageFar", "afterimageFarIcon"),
                                           ("AfterimageNear", "Rhino_AfterimageNear", "afterimageNearIcon"),
                                           ("AfterimageBlade", "Rhino_AfterimageBlade", "afterimageBladeIcon")])),
]


# ---- Space: Blade Reach options (2026-10-28) ------------------------------------------------------------------------
# Space alone sets the blade's length: 24 at rest -> 40 at Space 10 (ShieldSkimmerScaleDriver.UpdateLength, the
# Skimmer's live Space scale); at Space 5 an energized blade pops super-shields (Breaker). The card lays the katana
# FLAT, pointing out of the card the way the sword reaches, so it reads apart from the upright Charge card.
REACH_HILT = (30.0, 44.0)
REACH_MAX = 92.0                 # the blade at Space 10 (40 u)
REACH_REST = REACH_MAX * 24 / 40  # at rest (24 u)


def reach_len(space01):
    return REACH_REST + (REACH_MAX - REACH_REST) * space01


def flat_katana(length, w=15.0, tsuba=True, tsuka=True):
    return katana(90.0, hilt=REACH_HILT, length=length, w=w, sori=KATANA_SORI * length / KATANA_LEN, tsuba=tsuba,
                  tsuka=tsuka, mirror=True)


def reach_ghost():
    """The reach still to grow: the blade at Space 10, minus nothing - drawn faint under the live blade."""
    return flat_katana(REACH_MAX, tsuba=False, tsuka=False)


def reach_ticks(n=2):
    """O: two short posts across the blade's line, at the rest length and the full reach."""
    hx, hy = REACH_HILT
    m = empty()
    for L in (REACH_REST, REACH_MAX)[:n]:
        x = hx + L
        m |= fill([(x - 1.6, hy - 22), (x + 1.6, hy - 22), (x + 1.6, hy - 12), (x - 1.6, hy - 12)])
    return m


def reach_chevrons():
    """P: two chevrons beyond the tip, pointing on - 'it reaches further'."""
    hx, hy = REACH_HILT
    m = empty()
    for x in (hx + REACH_REST + 12, hx + REACH_REST + 30):
        m |= fill([(x, hy - 16), (x + 13, hy - 3), (x, hy + 10), (x - 6, hy + 10), (x + 7, hy - 3), (x - 6, hy - 16)])
    return m


SPACE_LEVELS = [("Space 0: rest reach (24)", 0.0), ("Space 5", 0.5), ("Space 10: full reach (40)", 1.0)]
# the plate fills with the Space level; one mark at Space 5, where Breaker (the level-5 upgrade) arrives
SPACE_MARKS = [0.5]


def reach_card(name, title, extra=None, ghost=True, plate=True):
    parts, states, gauge = {}, [], {}
    for k, (n, s01) in enumerate(SPACE_LEVELS):
        parts[f"b{k}"] = (lambda s01=s01: flat_katana(reach_len(s01)))
        layers = []
        if ghost:
            parts["ghost"] = reach_ghost
            layers.append(("ghost", "white", 0.3))
        if extra:
            parts["extra"] = extra
            layers.append(("extra", "white", 0.6))
        layers.append((f"b{k}", "white"))
        states.append((n, layers))
        gauge[n] = (s01 if plate else None, None)
    # Space 5 and up, energized: the blade lit, popping super-shields (Breaker)
    states.append(("Space 5+, energized: Breaker (lit)", ([("ghost", "white", 0.3)] if ghost else []) + [("b1", "danger")]))
    gauge["Space 5+, energized: Breaker (lit)"] = (0.5 if plate else None, None)
    d = dict(name=name, group="bladereach", lockup_state=1, element="Space",
             before=ICON_DIR + "Rhino_BladeReach-PLACEHOLDER.png", title=title, parts=parts, states=states, gauge=gauge)
    if plate:
        d.update(gauge_marks=SPACE_MARKS, gauge_marks_style="lines")
    return d


CARDS += [
    reach_card("space_option_n", "N - THE REACH: the katana laid flat, as long as the live blade (24 at rest -> 40 at Space 10), over a faint blade at full reach; the plate fills with the Space level, a mark at Space 5 (Breaker)"),
    reach_card("space_option_o", "O - THE MEASURE: N with two posts marking the rest length and the full reach, no faint blade", extra=lambda: reach_ticks(), ghost=False),
    reach_card("space_option_p", "P - REACHING ON: the katana at its live length with two chevrons pointing past its tip; no plate", extra=reach_chevrons, ghost=False, plate=False),
]
