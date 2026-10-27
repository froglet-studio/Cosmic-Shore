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
from motifs import burst, prism

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


# ---- the blade
def _axis(x0, y0, heading):
    h = math.radians(heading)
    return (math.cos(h), -math.sin(h)), (math.sin(h), math.cos(h))     # along (screen y down), across


def blade(x0, y0, length, heading=45.0, w=18.0):
    """The sword: a long straight blade from its root at (x0, y0) toward `heading` (0 = right, 90 = up) - the root cut
    square where it leaves the nose, the tip a long point - the capsule made angular, drawn as the blade it is."""
    (ux, uy), (vx, vy) = _axis(x0, y0, heading)
    tip = w * 1.6                                       # the point's run
    pts = [(0, w / 2), (length - tip, w / 2), (length, 0), (length - tip, -w / 2), (0, -w / 2)]
    return fill([(x0 + ux * t + vx * s, y0 + uy * t + vy * s) for t, s in pts])


def bolts(x0, y0, length, heading=45.0, w=18.0):
    """The energized blade's crackle: two short jagged bolts, one each side of the blade, a hand's width off it."""
    from canvas import stroke
    (ux, uy), (vx, vy) = _axis(x0, y0, heading)
    out = empty()
    for side, t0 in ((1, 0.2), (-1, 0.42)):
        off = side * (w / 2 + 9)
        zig = [(0.0, 0), (0.09, 9), (0.17, -1), (0.26, 8), (0.34, 0)]
        pts = [(x0 + ux * length * (t0 + t) + vx * (off + side * a), y0 + uy * length * (t0 + t) + vy * (off + side * a))
               for t, a in zig]
        out |= stroke(pts, 6.0)
    return out


BLADE = dict(x0=16, y0=112, length=132, heading=45.0, w=20.0)


# A - THE LIT BLADE: the sword alone, corner to corner; lit, it crackles. Unlit (charging, cooling) it is a ghost.
def energize_blade():
    return blade(**BLADE)


def energize_bolts():
    return bolts(**BLADE)


# B - THE STEAL: the blade cuts across a rival's three petals; below Charge 5 they burst loose as crystals (danger,
# flung off the cut), at Charge 5 the sword pockets them - the same crystals, now yours (the domain colour).
STEAL_BLADE = dict(x0=8, y0=120, length=132, heading=45.0, w=22.0)


def steal_blade():
    return blade(**STEAL_BLADE)


def steal_loose():
    """The petals knocked loose: three crystals flung off the cut, away from the blade."""
    m = empty()
    for x, y, s in ((96, 74, 15), (64, 34, 12), (116, 106, 10)):
        m |= prism(x, y, s)
    return m


# C - THE STRIKE: the lit blade's tip into a burst, one rival petal knocked out of it.
def strike_blade():
    return blade(6, 122, 92, 45.0, 22.0)


def strike_burst():
    return burst(84, 44, 40, spikes=10, inner=0.5, rot=12) & ~fill([(84, 32), (96, 44), (84, 56), (72, 44)])


def strike_petal():
    """The petal knocked out of the strike: a crystal flung clear of the burst."""
    return prism(116, 14, 11)


ICONS = []

# The hull's other cards as the HUD draws them today (states.py options sheets show a candidate beside them).
LOCKUP = dict(
    Mass=dict(parts=dict(i=placeholder("Afterimage")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("BladeReach")), state=[("i", "white")]),
    Time=dict(parts=dict(i=placeholder("RampSpool")), state=[("i", "white")], gauge=0.55),
)

# Energize's states: (name, plate = EnergizeGauge01, lit). The card is pressed while lit and veiled while cooling.
ENERGIZE_STATES = [("charging: both triggers held", 0.55, False),
                   ("lit", 1.0, True),
                   ("let go: the lit tail drains", 0.4, True),
                   ("cooling down", 0.0, False)]

_BEFORE = dict(before=ICON_DIR + "Rhino_SwordEnergy-PLACEHOLDER.png", before_gauge_value=0.55)

CARDS = [
    dict(name="energize_option_a", group="energize", lockup_state=1, element="Charge", **_BEFORE,
         title="A - THE LIT BLADE: the sword alone, a ghost until it is lit; lit, it crackles",
         parts=dict(b=energize_blade, c=energize_bolts),
         states=[(n, [("b", "domain")] + [("c", "domain")] if lit else [("b", "domain", 0.6)]) for n, _g, lit in ENERGIZE_STATES],
         gauge={n: (g, None) for n, g, _l in ENERGIZE_STATES}),
    dict(name="energize_option_b", group="energize", lockup_state=0, element="Charge", **_BEFORE,
         title="B - THE STEAL: the blade cuts a rival's petals loose as crystals (theirs); at Charge 5 they turn yours",
         parts=dict(b=steal_blade, loose=steal_loose),
         states=[(n, [("loose", "danger"), ("b", "domain") if lit else ("b", "domain", 0.6)]) for n, _g, lit in ENERGIZE_STATES]
                + [("Charge 5: the sword pockets them", [("loose", "domain"), ("b", "domain")])],
         gauge={**{n: (g, None) for n, g, _l in ENERGIZE_STATES}, "Charge 5: the sword pockets them": (1.0, None)}),
    dict(name="energize_option_c", group="energize", lockup_state=1, element="Charge", **_BEFORE,
         title="C - THE STRIKE: the lit blade's tip into a burst, a rival's petal knocked out of it as a crystal",
         parts=dict(b=strike_blade, burst=strike_burst, p=strike_petal),
         states=[(n, ([("burst", "domain"), ("p", "danger"), ("b", "domain")] if lit else [("b", "domain", 0.6)])) for n, _g, lit in ENERGIZE_STATES],
         gauge={n: (g, None) for n, g, _l in ENERGIZE_STATES}),
]
