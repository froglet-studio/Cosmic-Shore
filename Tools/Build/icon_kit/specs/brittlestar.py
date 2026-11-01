"""The Brittle Star's ability-row icons (Resources/ElementalAbilityMaps/BrittleStar.asset, R_VesselActions/BRITTLE_STAR.md).

  Charge  Charge Mode   Y toggles: five CORDS grow out of the five turrets; RT, analog, takes them from a 120 u WHIP (low
                        stiffness, waves travel down them) to an 80 u SABER (straighter, 40% of the way to rigid). They
                        slice prisms they sweep through (the Thresher's chain cut) and a cord across a rival hull is a
                        Strike. Plate: the spare-ring stock LT throws (0, half, full of two), lime while the mode is on.
  Mass    Creation      B toggles: a trail behind each of the five ring corners (five lines that twist as the ring turns);
                        the slowest mode (x0.5). Plate: the mode's eased switch.
  Space   Space Mode    X toggles: all five guns fire along their arms (stick in: they meet ahead; out: they splay 25 deg);
                        heat, overheat lays danger prisms. Plate: the heat, hot colour while overheated.
  Time    Speed Mode    A toggles: x6 -> x8 speed, the prisms stretch so the trail still connects. Plate: the eased switch.

Before (2026-11-01): the round-1 set - Ring Detonation (a pentagon of bars round a burst; the Charge card still wears it,
BRITTLE_STAR.md follow-up F12), Creation (five scattered blocks and a dashed ring), Destruction (the ring with speed
lines), Speed (the ring with trails). The Charge card goes first: it shows an ability the card no longer is.

Real geometry:
  the hull     BrittleStar.fbx seen NOSE-ON (yaw 90, pitch -90: a pentagon point up) - the ring of five arms round the
               cockpit, a turret at each corner. From above or the side it is a line: the ring stands across the nose.
  a prism      two close triangles (motifs.prism_halves; never the rhombus, the Time petal's shape); a sliced one has
               its cut along the cord's line, the halves apart.
"""
import math

import mesh_silhouette
from canvas import N, empty, fill, stroke

import numpy as np
from PIL import Image

HULL = "BrittleStar"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/BrittleStar/"
FBX = "Assets/_Models/Vessel Models/BrittleStar.fbx"
NOSE_ON = dict(pitch=-90, yaw=90, dilate_px=1.0)   # the ring face on, a corner up


def _png_mask(rel):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def shipped(name):
    return lambda: _png_mask(ICON_DIR + f"BrittleStar_{name}.png")


def _clear_of(mask, *others, px=2.5):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


def star(box, cx=64.0, cy=64.0):
    return lambda: mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, **NOSE_ON)


ICONS = []
# The row as the HUD draws it today: four white sprites, each over its plate gauge (BrittleStarHUDView, one per card).
LOCKUP = dict(
    Charge=dict(parts=dict(i=shipped("RingDetonation")), state=[("i", "white")], gauge=1.0),
    Mass=dict(parts=dict(i=shipped("CreationMode")), state=[("i", "white")], gauge=0.0),
    Space=dict(parts=dict(i=shipped("DestructionMode")), state=[("i", "white")], gauge=0.3),
    Time=dict(parts=dict(i=shipped("SpeedMode")), state=[("i", "white")], gauge=0.0),
)
CARDS = []
REBIND = []


# ---- Charge: Charge Mode, the five cords ----------------------------------------------------------------------------------
# WHO: the Brittle Star nose-on (domain). WHAT: five cords out of its five turrets, a whip at rest and a saber on a full
# RT pull, slicing prisms.
HULL_BOX = 64.0
CORNER_R = 30.0                          # the turrets' radius at HULL_BOX (measured off the silhouette's corners)
CORNERS = [(64 + CORNER_R * math.cos(math.radians(-90 + 72 * k)), 64 + CORNER_R * math.sin(math.radians(-90 + 72 * k)))
           for k in range(5)]
C_STAR = star(HULL_BOX)


def _cord(k, length, wave, w=4.5, phase=0.0):
    """Cord k out of corner k, radially: `wave` px of sideways swing growing toward the tip (a whip), 0 = straight."""
    a = math.radians(-90 + 72 * k)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    x0, y0 = CORNERS[k]
    pts = []
    for t in np.linspace(0, 1, 24):
        off = wave * t * math.sin(t * 2.2 * math.pi + phase)
        pts.append((x0 + ux * length * t + px * off, y0 + uy * length * t + py * off))
    return stroke(pts, w=w)


def whips():
    out = empty()
    for k in range(5):
        out |= _cord(k, 30, 7, w=5.0, phase=0.6 * k)
    return _clear_of(out, C_STAR(), px=1.5) | (out & C_STAR())


def sabers(length=26):
    out = empty()
    for k in range(5):
        out |= _cord(k, length, 0, w=5.0)
    return out


SLICED = (64 + 50 * math.cos(math.radians(54)), 64 + 50 * math.sin(math.radians(54)), 12.0)   # on cord 2's line, cut along it


def sliced_prism():
    # The prism as two close triangles (never the rhombus: the Time petal's shape, Garrett 2026-11-01), its cut lying
    # along cord 2 - the cord has just sliced it, and the halves have come apart.
    cx, cy, s_ = SLICED
    a = math.radians(-90 + 72 * 2)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    w, g = s_ * 0.8, 2.6
    out = empty()
    for sgn in (-1, 1):
        ox, oy = cx + px * g * sgn, cy + py * g * sgn
        out |= fill([(ox + ux * s_, oy + uy * s_), (ox - ux * s_, oy - uy * s_), (ox + px * w * sgn, oy + py * w * sgn)])
    return out


def a_star():
    return C_STAR()


CHARGE_BEFORE = ICON_DIR + "BrittleStar_RingDetonation.png"
# Garrett picked C (2026-11-01: "i like C, but i don't but the CTA color as the on color is too much"): the whips at rest,
# the sabers as RT pulls - the HUD crossfades the two parts on the pull. The plate keeps the spare-ring stock in its
# resting colour: no lime for "on" (the lit cords already say it).
CHARGE_PARTS = [("ChargeWhips", "BrittleStar_ChargeWhips", "chargeWhipsIcon", whips,
                 "the five cords loose, a whip each (white; fades as RT pulls; dim with the mode off)"),
                ("ChargeSabers", "BrittleStar_ChargeSabers", "chargeSabersIcon", lambda: sabers(24),
                 "the five cords pulled straight, sabers (white; comes in as RT pulls)"),
                ("ChargeStar", "BrittleStar_ChargeStar", "chargeStarIcon", C_STAR, "the Brittle Star nose-on (domain)")]
ICONS += [dict(name=i, element="Charge", act="Charge Mode: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in CHARGE_PARTS]
CHARGE_GAUGE = {"Charge Mode on": (1.0, None), "RT pulled": (1.0, None), "off": (1.0, None)}
CHARGE_ON = [("whip", "white"), ("star", "domain")]
CARDS += [
    dict(name="charge", element="Charge", before=CHARGE_BEFORE, before_gauge=True, gauge=CHARGE_GAUGE,
         title="Charge Mode (Charge, picked C): the Brittle Star nose-on, a whip out of each turret; RT pulls them into "
               "sabers; the plate is the spare-ring stock, never lime",
         parts=dict(star=C_STAR, whip=whips, saber=lambda: sabers(24)),
         states=[("Charge Mode on", CHARGE_ON),
                 ("RT pulled", [("saber", "white"), ("star", "domain")]),
                 ("off", [("whip", "white", 0.25), ("star", "domain")])],
         lockup_state=0),
]
LOCKUP["Charge"] = dict(parts=dict(star=C_STAR, whip=whips), state=CHARGE_ON, gauge=1.0)
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/BrittleStarHUDVariant.prefab"
VIEW = "5875684227236902712"            # BrittleStarHUDView
CHARGE_ICON = "7700000000000001105"     # the Charge slot's icon Image (was BrittleStar_RingDetonation)
REBIND += [
    dict(file=VARIANT, pair=dict(container=CHARGE_ICON, view=VIEW, after="speedGauge",
                                 children=[(n, i, f) for n, i, f, _d, _a in CHARGE_PARTS])),
]
