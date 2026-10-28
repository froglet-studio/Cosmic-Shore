"""The Rhino's ability-row icons (Resources/ElementalAbilityMaps/Rhino.asset), angular, one object each.

  Charge  Energize     hold both triggers to LIGHT the blade (the cooldown is its only limit); Charge scales how
                       many petals a sword hit takes from a rival, and at Charge 5 the sword POCKETS them instead
                       of knocking them loose as crystals (RHINO_ENERGY_SWORD.md, 2026-10-26)
  Mass    Afterimage   the unlit blade keeps leaving prisms in its own shape and pose for 5 s
  Space   Blade Reach  Space alone grows the blade, 24 -> 40; at Space 5 an energized blade pops super-shields
  Time    Ramp Spool   how fast the ramp winds the Rhino up to top speed; at Time 5 the Momentum ward

Before: four white-square PLACEHOLDER sprites. One card at a time with Garrett, options in the hull's whole lockup.

The blade: in the game it is the ForceFieldSkimmer's capsule standing off the nose. A capsule is round, and the icon
language is angular, so the blade is drawn as a long chamfered lozenge - the capsule's outline cut flat.
"""
import math

import numpy as np
from PIL import Image

from canvas import N, empty, fill, rect, rotate

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


_BEFORE = dict(before=ICON_DIR + "Rhino_SwordEnergy-PLACEHOLDER.png", before_gauge_value=0.55)


# ---- the sword, as the game draws it (Garrett 2026-10-28: "make it look more sword like and not team colored. when lit
# make it the danger color. the idea is to follow the actual sword"). Measured on Rhino.prefab: the ForceFieldSkimmer
# instance draws Unity's built-in CAPSULE (mesh 10208) at local scale (1.5, 24, 4.8) - a 48-unit blade, 4.8 deep, its
# two hemispherical caps stretched into 12-unit points - standing up off the nose (mount (0, -1, 20.7), raised 20 deg).
# From the chase camera it stands upright; the triggers swipe it (yaw + roll, up to 90 deg each way, RT - LT) and chop
# it down (pitch, up to 65 deg, RT + LT). So the card is that blade in its real profile (10 : 1, the caps faceted:
# the language is angular), upright at rest and TURNED with the live swipe - neutral while dark, danger while lit.
HILT = (64.0, 118.0)      # where the blade leaves the nose: the card's pivot, as the mount is the sword's
SWORD_LEN = 102           # the blade's length on the card at rest (px), hilt to tip
SWORD_W = 17             # its width: the capsule's 10 : 1 drawn a little fuller, so it reads at 60 px
CARD_SWIPE = 45.0 / 90.0 # the card turns half the sword's real swipe (90 deg real -> 45 on the card), so it stays in


def sword(angle=0.0, chop=0.0, guard=True, length=SWORD_LEN, w=SWORD_W):
    """The blade standing off the hilt, turned `angle` degrees clockwise from upright (the swipe, already scaled to the
    card) and foreshortened by cos(`chop`) (the chop tips it toward the camera): straight sides, a long faceted point
    (the capsule's stretched cap), the root square at the hilt; `guard` adds the crossbar where it meets the nose."""
    hx, hy = HILT
    L = length * math.cos(math.radians(chop))
    tip = min(L * 0.3, w * 1.6)
    pts = [(-w / 2, 0), (-w / 2, -(L - tip)), (-w * 0.3, -(L - tip * 0.35)), (0, -L), (w * 0.3, -(L - tip * 0.35)),
           (w / 2, -(L - tip)), (w / 2, 0)]
    m = fill(rotate([(hx + x, hy + y) for x, y in pts], angle, hx, hy))
    if guard:
        g = [(-w * 1.25, 1), (w * 1.25, 1), (w * 1.25, 9), (-w * 1.25, 9)]
        grip = [(-w * 0.28, 9), (w * 0.28, 9), (w * 0.28, 15), (-w * 0.28, 15)]
        m |= fill(rotate([(hx + x, hy + y) for x, y in g], angle, hx, hy))
        m |= fill(rotate([(hx + x, hy + y) for x, y in grip], angle, hx, hy))
    return m


# Round 3 (Garrett 2026-10-28: "more like a katana."): the same blade on the same pivot, drawn as a katana - a long
# shallow curve (sori) bending back toward the spine, one cutting edge on the convex side, the angled kissaki point
# (the edge turns up at the yokote and meets the spine), a small tsuba where the blade leaves the nose and a long tsuka.
KATANA_HILT = (60.0, 100.0)  # the pivot sits higher than the straight sword's so the long grip fits under it
KATANA_LEN = 92              # habaki to point (px)
KATANA_W = 12                # the blade's depth: a katana is slimmer than the capsule's 10 : 1
KATANA_SORI = 14             # how far the point sits off the hilt's straight line (px): the curve


def katana(angle=0.0, chop=0.0, tsuba=True, tsuka=True, length=KATANA_LEN, w=KATANA_W, sori=KATANA_SORI, hilt=KATANA_HILT):
    hx, hy = hilt
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
    m = fill(rotate([(hx + x, hy + y) for x, y in pts], angle, hx, hy))
    if tsuba:                                   # the guard: a flat octagon across the blade's root
        g = [(-w * 0.95, 1), (-w * 0.7, -1.5), (w * 0.7, -1.5), (w * 0.95, 1), (w * 0.95, 5), (w * 0.7, 7.5),
             (-w * 0.7, 7.5), (-w * 0.95, 5)]
        m |= fill(rotate([(hx + x, hy + y) for x, y in g], angle, hx, hy))
    if tsuka:                                   # the grip: long, a touch narrower than the blade, its end cut square
        grip = [(-w * 0.42, 9), (w * 0.42, 9), (w * 0.38, 25), (-w * 0.38, 25)]
        m |= fill(rotate([(hx + x, hy + y) for x, y in grip], angle, hx, hy))
    return m


# The states the sheet shows: (name, swipe deg, chop deg, lit, plate = EnergizeGauge01).
SWORD_STATES = [("at rest: upright", 0.0, 0.0, False, 0.0),
                ("swiped right (RT)", 70.0, 0.0, False, 0.0),
                ("swiped left (LT)", -70.0, 0.0, False, 0.0),
                ("both triggers: the chop, charging", 0.0, 50.0, False, 0.55),
                ("lit", 0.0, 0.0, True, 1.0),
                ("lit, swiped right", 70.0, 0.0, True, 0.7),
                ("cooling down", 0.0, 0.0, False, 0.0)]


def sword_card(name, title, guard, draw=None):
    draw = draw or (lambda ang, chop: sword(ang, chop, guard=guard))
    parts, states, gauge = {}, [], {}
    for k, (n, ang, chop, lit, g) in enumerate(SWORD_STATES):
        parts[f"s{k}"] = (lambda ang=ang, chop=chop: draw(ang * CARD_SWIPE, chop))
        states.append((n, [(f"s{k}", "danger" if lit else "white")]))
        gauge[n] = (g, None)
    return dict(name=name, group="energize", lockup_state=4, element="Charge", **_BEFORE, title=title,
                parts=parts, states=states, gauge=gauge)


ICONS = []

# The hull's other cards as the HUD draws them today (states.py options sheets show a candidate beside them).
LOCKUP = dict(
    Mass=dict(parts=dict(i=placeholder("Afterimage")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("BladeReach")), state=[("i", "white")]),
    Time=dict(parts=dict(i=placeholder("RampSpool")), state=[("i", "white")], gauge=0.55),
)

CARDS = [
    sword_card("energize_option_f", "F - THE KATANA: curved, one edge, the angled point, a small tsuba and the long grip; turned with the live swipe; white dark, danger lit", None,
               lambda a, c: katana(a, c)),
    sword_card("energize_option_g", "G - THE KATANA, BLADE AND TSUBA: F without the grip, so the blade runs the card's full height", None,
               lambda a, c: katana(a, c, tsuka=False, length=100, hilt=(60.0, 114.0))),
    sword_card("energize_option_d", "D - THE SWORD: the blade on its hilt, turned about the hilt with the live swipe and shortened by the chop; white dark, danger lit", True),
    sword_card("energize_option_e", "E - THE BLADE ALONE: D without the guard - only what the game draws, the blade off the nose", False),
]
