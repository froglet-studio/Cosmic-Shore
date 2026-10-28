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


AFTER_ANGLES = (-50.0, -25.0)  # the copies left behind, the blade swinging right past them


def after_trail(n=2, angles=AFTER_ANGLES):
    def draw():
        m = empty()
        for ang in angles[-n:]:
            m |= prism_katana(ang)
        return m
    return draw


def after_blade(angle=0.0, mirror=True):
    return lambda: katana(angle, mirror=mirror)


# Option H: the sword now, white, swinging right past two prism copies of itself in the domain colour.
# Option I: the same without the sword - only the afterimages, three prism copies fanned on the arc.
# Option J: one copy, square behind the sword (the stance it was in 5 s ago), the sword turned away from it.
MASS_OPTIONS = [
    ("mass_option_k", "K - LEFT BEHIND: the sword (white) up front, two prism copies of it receding behind the Rhino as it flies on, smaller and fainter with distance",
     dict(blade=lambda: katana(0.0, hilt=(36.0, 106.0), length=88), c1=lambda: prism_katana(0.0, hilt=(66.0, 98.0), length=76),
          c2=lambda: prism_katana(0.0, hilt=(94.0, 88.0), length=62)),
     [("idle: no afterimages", [("blade", "white")]),
      ("laying (lit)", [("c2", "domain", 0.5), ("c1", "domain", 0.8), ("blade", "white")])],
     [("c2", "domain", 0.5), ("c1", "domain", 0.8), ("blade", "white")]),
    ("mass_option_h", "H - THE TRAIL: the sword (white) swinging right past two prism copies of itself (your colour), the copy nearer it brighter",
     dict(blade=after_blade(20.0), c1=lambda: prism_katana(-45.0), c2=lambda: prism_katana(-15.0)),
     [("idle: no afterimages", [("blade", "white")]),
      ("laying (lit)", [("c1", "domain", 0.45), ("c2", "domain", 0.8), ("blade", "white")])],
     [("c1", "domain", 0.45), ("c2", "domain", 0.8), ("blade", "white")]),
    ("mass_option_i", "I - THE AFTERIMAGES ALONE: three prism copies fanned on the swing's arc, oldest faintest; no sword",
     dict(c1=lambda: prism_katana(-40.0), c2=lambda: prism_katana(0.0), c3=lambda: prism_katana(40.0)),
     [("idle: the copies dim", [("c1", "domain", 0.25), ("c2", "domain", 0.35), ("c3", "domain", 0.45)]),
      ("laying (lit)", [("c1", "domain", 0.4), ("c2", "domain", 0.7), ("c3", "domain")])],
     [("c1", "domain", 0.4), ("c2", "domain", 0.7), ("c3", "domain")]),
    ("mass_option_j", "J - THE ECHO: one prism copy standing where the sword was, the sword swung away from it",
     dict(blade=after_blade(35.0), c1=lambda: prism_katana(-10.0)),
     [("idle: no afterimage", [("blade", "white")]),
      ("laying (lit)", [("c1", "domain"), ("blade", "white")])],
     [("c1", "domain"), ("blade", "white")]),
]


def mass_card(name, title, parts, states, shown):
    return dict(name=name, group="afterimage", lockup_state=1, element="Mass",
                before=ICON_DIR + "Rhino_Afterimage-PLACEHOLDER.png", title=title, parts=parts, states=states)


CARDS += [mass_card(*o[:4], o[4]) for o in MASS_OPTIONS]
