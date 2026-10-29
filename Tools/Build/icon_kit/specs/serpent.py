"""The Serpent's ability-row icons (Resources/ElementalAbilityMaps/Serpent.asset).

  Charge  Sniper Shot         scoped, RT fires one hitscan round down the scope's line that destroys what it hits,
                              SUPER-SHIELDED mass included; Charge sets the recovery (12 s at rest -> 5.4 s at Charge 10).
                              Charge 5 PIERCE: the round carries through up to three prisms.
  Mass    Seed Wall           stopping or cloaking leaves a seed that grows a herringbone wall from stolen prisms;
                              Mass sizes the bricks; Mass 5 LOCKDOWN seals the holes with danger panels on a crystal
  Space   Scope               LT opens a magnified eyepiece down the nose; Space sets the magnification; Space 5 DEEP
                              FOCUS one more step (and the walls steal shielded prisms)
  Time    Solid Fuel Pellets  each press burns a quarter-tank pellet; overlapping burns stack; Time sets the burn length

Before: three PLACEHOLDER sprites (author_hull_icon_placeholders.py) and the shipped pellet-slot art on Time. One card
at a time with Garrett, options in the hull's whole lockup.

The hull is the Serpent's own mesh (SerpentExport.fbx, Serpent.prefab) from straight above, nose RIGHT (pitch 0,
yaw 0: the round head at +x, the arms trailing - the grid on 2026-10-29).
"""
import mesh_silhouette
from canvas import N, empty, fill, stroke
from specs.rhino import stella

import numpy as np
from PIL import Image

HULL = "Serpent"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Serpent/"
FBX = "Assets/_Models/Vessel Models/SerpentExport.fbx"
VIEW_PLAN = dict(pitch=0, yaw=0, dilate_px=0.8)


def _png_mask(rel):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def placeholder(name):
    return lambda: _png_mask(ICON_DIR + f"Serpent_{name}-PLACEHOLDER.png")


def serpent(box, cx, cy):
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, **VIEW_PLAN)


def _clear_of(mask, *others, px=2.5):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


ICONS = []
LOCKUP = dict(
    Charge=dict(parts=dict(i=placeholder("SniperShot")), state=[("i", "white")]),
    Mass=dict(parts=dict(i=placeholder("SeedWall")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("Scope")), state=[("i", "white")]),
    Time=dict(parts=dict(i=lambda: _png_mask("Assets/_Graphics/Design Assests/HUD UI/Serpent Fuel/PelletSlots.png")),
              state=[("i", "white")]),
)
CARDS = []
REBIND = []


# ---- Charge: Sniper Shot options (2026-10-29) ----------------------------------------------------------------------
# WHO gains WHAT against WHAT: the Serpent, from range, deletes ONE thing nothing else but the Rhino's energised blade
# can touch - a SUPER-SHIELDED prism (the game's stella octangula, StellatedOctahedronMeshGenerator, drawn by the
# Rhino's spec). The round is hitscan: a straight white line, there and gone. Charge 5 PIERCE carries it through three.
# The recovery (12 s -> 5.4 s) is the fleet's cooldown veil, which the HUD already sweeps (SerpentHUDController).
# States: ready (the target whole, no round), fired (the round and the target shattered), recharging (the veil), and
# Pierce fired (three shattered). Super-shielded is drawn in the "shielded" colour, as the Rhino's Space card does.
CY = 64.0
BEAM_W = 4.5


def beam(x0, x1, cy=CY, w=BEAM_W):
    return fill([(x0, cy - w / 2), (x1, cy - w / 2), (x1, cy + w / 2), (x0, cy + w / 2)])


# A: SHOOTER. The Serpent small at the left, nose right; the round from its nose to the prism at the right.
A_HULL = dict(box=50.0, cx=28.0, cy=CY)
A_PRISM = (94.0, CY, 42.0)


def a_hull():
    return serpent(**A_HULL)


def a_prism(broken=False):
    x, y, r = A_PRISM
    return _clear_of(stella(x, y, r * (0.78 if broken else 1.0), broken=broken), a_beam(), a_hull())


def a_beam():
    return _clear_of(beam(A_HULL["cx"] + A_HULL["box"] / 2 + 2, 124.0), a_hull(), px=2.0)


def a_pierce():
    out = empty()
    for x in (66.0, 88.0, 110.0):
        out |= stella(x, CY, 22.0 * 0.78, broken=True)
    return _clear_of(out, a_beam(), a_hull())


# B: THE ROUND. No hull: the round itself, a long slug with its tracer, punching the prism apart (the act, close up).
def b_round():
    slug = fill([(16.0, CY - 7), (44.0, CY - 7), (54.0, CY), (44.0, CY + 7), (16.0, CY + 7)])
    tracer = empty()
    for dy, x0 in ((-15.0, 6.0), (15.0, 6.0)):
        tracer |= stroke([(x0, CY + dy), (32.0, CY + dy)], w=5.0)
    return slug | tracer


def b_prism(broken=False):
    return _clear_of(stella(92.0, CY, 50.0 * (0.8 if broken else 1.0), broken=broken), b_round(), px=3.0)


def b_through():
    return _clear_of(beam(52.0, 124.0, w=4.0), b_round(), px=2.0)


def b_pierce():
    out = empty()
    for x in (74.0, 96.0, 118.0):
        out |= stella(x, CY, 22.0 * 0.8, broken=True)
    return _clear_of(out, b_round(), b_through())


# C: THE HOLE. The prism big in the centre, the round's line through it as negative space - a clean hole punched clean
# through a shield nothing else can open. Pierce: three prisms in a row, the line through all three.
def c_line():
    return beam(4.0, 124.0, w=6.0)


def c_prism(holed=False):
    s = stella(64.0, CY, 70.0)
    return _clear_of(s, c_line(), px=3.0) if holed else s


def c_pierce():
    out = empty()
    for x in (24.0, 64.0, 104.0):
        out |= stella(x, CY, 32.0)
    return _clear_of(out, c_line(), px=3.0)


# (state, which parts)
def _card(key, title, parts, states):
    return dict(name=f"snipershot_{key}", group="snipershot", lockup_state=1, element="Charge",
                before=ICON_DIR + "Serpent_SniperShot-PLACEHOLDER.png", title=title, parts=parts, states=states)


CARDS += [
    _card("a", "A  Shooter: the Serpent at the left, its hitscan round to a super-shielded prism that shatters; "
               "Pierce: three shattered on the line",
          dict(hull=a_hull, beam=a_beam, whole=lambda: a_prism(False), broken=lambda: a_prism(True), pierce=a_pierce),
          [("ready: the target whole", [("whole", "shielded"), ("hull", "domain")]),
           ("fired: the round, the sun shattered", [("beam", "white"), ("broken", "shielded"), ("hull", "domain")]),
           ("Charge 5 Pierce: three in one shot", [("beam", "white"), ("pierce", "shielded"), ("hull", "domain")])]),
    _card("b", "B  The round: the slug and its tracer punching a super-shielded prism apart, close up; Pierce: on through three",
          dict(round=b_round, through=b_through, whole=lambda: b_prism(False), broken=lambda: b_prism(True), pierce=b_pierce),
          [("ready: the target whole", [("round", "white"), ("whole", "shielded")]),
           ("fired: the sun shattered", [("round", "white"), ("broken", "shielded")]),
           ("Charge 5 Pierce: three in one shot", [("round", "white"), ("through", "white"), ("pierce", "shielded")])]),
    _card("c", "C  The hole: a super-shielded prism with the round's line punched clean through it as negative space; "
               "Pierce: through three",
          dict(whole=lambda: c_prism(False), holed=lambda: c_prism(True), pierce=c_pierce),
          [("ready: the target whole", [("whole", "shielded")]),
           ("fired: holed clean through", [("holed", "shielded")]),
           ("Charge 5 Pierce: three in one shot", [("pierce", "shielded")])]),
]
