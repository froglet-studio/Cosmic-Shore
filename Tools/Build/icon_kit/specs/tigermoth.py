"""The Tiger Moth's ability-row icons (R_VesselActions/TIGER_MOTH.md; map Resources/ElementalAbilityMaps/TigerMoth.asset).
The Tiger Moth flies the HUD the Butterfly design left behind: ButterflyHUDVariant.prefab / ButterflyHUDView, whose
shipped sprites are Butterfly_*.png. Those names are the airframe's history (BUTTERFLY.md); the vessel is the Tiger Moth,
so this spec, its sheets and every new part are named TigerMoth (a Butterfly may come back one day as its own hull).

  Charge  Scale Dust     Dust mode: while a rival stays in the dust capsule hanging below the hull, it SHREDS one petal
                         of one random element they hold, every 1 s at Charge 0 -> 0.1 s at Charge 10. Allies it
                         touches are cleansed. L5 Toll Gate (the Drop's wormhole tolls rivals, domain hue).
  Mass    Mass / Dust    RT toggles: Mass mode lays a WIDE wake (5x -> 20x at Mass 15), Dust mode narrows it and turns the
                         dust on. Plate: full in Mass mode, empty in Dust mode. L5 Gilded Wake (Mass mode lays shielded).
  Space   Dust Reach     the capsule's LENGTH (60 -> 150); own mass it touches grows / turns dangerous / shields, rival
                         mass is destroyed / shrunk / stolen. L5 Diamond Dust.
  Time    Drop           LT, held: the wings SHUT and the hull falls along its own down (900 u/s), incorporeal (it clips
                         through everything), no trail; let go and it pitches 90 deg nose-down and flies on along the line
                         it fell. The recharge is paid by distance (a hop 2 s, 900 u the full 20 s); only a full drop
                         leaves a pair of wormhole mouths, one where it started and one where it landed. L5 Afterburn.

Before (2026-10-31): the round-1 pure-python 1-bit set - Scale Dust (a wing blob and three dots), Spread Wings (two
blobs), Wingreach (a blob and two arcs), Fold (ring -> ring). The audit (ABILITY_ICON_SKILL_PLAN.md §2) has Scale Dust
as "does not read" and the Fold as a half-read; the Fold is the retired Butterfly's ability, so the Time card goes first.

Real geometry:
  the hull     Butterfly.fbx, the Tiger Moth's airframe (nose +x in the top view at pitch 0). From the SIDE (pitch -90:
               upright, +z projects above the centre) skinned into the `rig|fold` take, the wings shut over the back -
               the Drop's own pose.
  a prism      the fleet's diamond (motifs.prism_lit).
  a wormhole   the mouth the drop leaves where it started, seen edge on (an oval), neutral (white) by default and the
               domain hue once Charge 5 Toll Gate makes it toll rivals.
"""
import math

import mesh_silhouette
from canvas import N, disc, empty, fill, ring, stroke
from motifs import prism_lit

import numpy as np
from PIL import Image

HULL = "TigerMoth"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Butterfly/"   # the shipped sprites the HUD carries today
FBX = "Assets/_Models/Vessel Models/Butterfly.fbx"
SHUT = dict(pitch=-90, yaw=0, dilate_px=1.0, take="rig|fold")   # side on, wings shut over the back, upright


def _png_mask(rel):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def shipped(name):
    return lambda: _png_mask(ICON_DIR + f"Butterfly_{name}.png")


def _clear_of(mask, *others, px=2.5):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


def moth(box, cx, cy, roll=0.0):
    return lambda: mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, roll=roll, **SHUT)


ICONS = []
# The row as the HUD draws it today: four white sprites; the Mass plate is the mode, the Time plate the fleet's veil.
LOCKUP = dict(
    Charge=dict(parts=dict(i=shipped("ScaleDust")), state=[("i", "white")]),
    Mass=dict(parts=dict(i=shipped("SpreadWings")), state=[("i", "white")], gauge=1.0),
    Space=dict(parts=dict(i=shipped("Wingreach")), state=[("i", "white")]),
    Time=dict(parts=dict(i=shipped("Fold")), state=[("i", "white")]),
)
CARDS = []
REBIND = []


# ---- Time: the Drop ---------------------------------------------------------------------------------------------------
# WHO: the moth, wings shut (domain). WHAT: the fall straight down its own down, out of the wormhole it leaves behind.
# Round 1 drew A the fall, B through the prisms, C the drop and the turn; Garrett picked A (2026-10-31: "A looks good
# lets add a wormhole behind on A"). The plate keeps the fleet's recharge veil; the parts dim while it recharges.
MOTH = moth(84, 60, 82)
MOUTH = (60.0, 14.0, 34.0, 10.0)         # the mouth seen edge on: centre, half-width, half-height


def drop_moth():
    return MOTH()


def _oval(cx, cy, rx, ry, w):
    pts = [(cx + rx * math.cos(t), cy + ry * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 72, endpoint=False)]
    return stroke(pts, w=w, closed=True)


def drop_mouth():
    # Two nested rims, the inner one lower: a tunnel seen from just above, so it reads as a hole, not a halo.
    cx, cy, rx, ry = MOUTH
    return _oval(cx, cy, rx, ry, 4.5) | _oval(cx, cy + 3.0, rx * 0.55, ry * 0.5, 3.5)


def drop_streaks():
    out = empty()
    for x, y0, y1 in ((44, 30, 46), (60, 30, 52), (76, 30, 44)):
        out |= stroke([(x, y0), (x, y1)], w=4.5)
    return _clear_of(out, MOTH(), drop_mouth())


def _dim(state, a=0.3):
    return [(k, c, a * (x[0] if x else 1.0)) for k, c, *x in state]


DROP_PARTS = [("DropMouth", "TigerMoth_DropMouth", "dropMouthIcon", drop_mouth,
               "the wormhole it leaves (white; the domain hue at Charge 5 Toll Gate)"),
              ("DropStreaks", "TigerMoth_DropStreaks", "dropStreaksIcon", drop_streaks, "the fall (white, full while dropping)"),
              ("DropMoth", "TigerMoth_DropMoth", "dropMothIcon", drop_moth, "the Tiger Moth, wings shut (domain)")]
ICONS += [dict(name=i, element="Time", act="Drop: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in DROP_PARTS]
DROP_READY = [("mouth", "white", 0.6), ("streaks", "white", 0.6), ("moth", "domain")]
DROP_LIT = [("mouth", "white"), ("streaks", "white"), ("moth", "domain")]
CARDS += [
    dict(name="drop", element="Time", before=ICON_DIR + "Butterfly_Fold.png",
         title="Drop (Time, picked A + the wormhole): the moth, wings shut (domain), falling out of the wormhole it leaves "
               "behind (white; the domain hue at Charge 5); the fall streaks between",
         parts=dict(mouth=drop_mouth, streaks=drop_streaks, moth=drop_moth),
         states=[("ready", DROP_READY),
                 ("dropping (LT held)", DROP_LIT),
                 ("Charge 5: Toll Gate", [("mouth", "domain"), ("streaks", "white", 0.6), ("moth", "domain")]),
                 ("recharging (veil)", _dim(DROP_READY))],
         lockup_state=0),
]
LOCKUP["Time"] = dict(parts=dict(mouth=drop_mouth, streaks=drop_streaks, moth=drop_moth), state=DROP_READY)
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/ButterflyHUDVariant.prefab"
VIEW = "5875684227236902712"            # ButterflyHUDView
TIME_ICON = "7700000000000001135"       # the Time slot's icon Image (was Butterfly_Fold)
REBIND += [
    dict(file=VARIANT, pair=dict(container=TIME_ICON, view=VIEW, after="wingEnergyGauge",
                                 children=[(n, i, f) for n, i, f, _d, _a in DROP_PARTS])),
]
