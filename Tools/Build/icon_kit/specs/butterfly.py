"""The Butterfly HUD's ability-row icons, flown by the TIGER MOTH since 2026-10-29 (R_VesselActions/TIGER_MOTH.md: "the
Butterfly's airframe, wings, dust, wake, camera and HUD, with the Drop as its Time ability"; the map is
Resources/ElementalAbilityMaps/TigerMoth.asset, the HUD ButterflyHUDVariant.prefab, the icons Butterfly_*.png).

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
as "does not read" and the Fold as a half-read; the Fold is now the wrong ability altogether, so the Time card goes first.

Real geometry:
  the hull     Butterfly.fbx (nose +x in the top view at pitch 0). From the SIDE (pitch -90: upright, +z projects
               above the centre) skinned into the `rig|fold` take, the wings shut over the back - the Drop's own pose
               and a butterfly's at-rest read.
  a prism      the fleet's diamond (motifs.prism_lit).
  a wormhole   the mouth ring every wormhole draws (WormholeMouth), neutral by default: white.
"""
import math

import mesh_silhouette
from canvas import N, disc, empty, fill, ring, stroke
from motifs import prism_lit

import numpy as np
from PIL import Image

HULL = "Butterfly"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Butterfly/"
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


# ---- Time: the Drop -----------------------------------------------------------------------------------------------------
# WHO: the moth, wings shut (domain). WHAT: the fall straight down its own down, clipping through what is in the way.
# The plate keeps the fleet's recharge veil (the wait this drop paid); the options dim the parts while it recharges.
def streaks(xs_y0_y1):
    def draw():
        out = empty()
        for x, y0, y1 in xs_y0_y1:
            out |= stroke([(x, y0), (x, y1)], w=4.5)
        return out
    return draw


# A: THE FALL. The moth low on the card, three fall streaks above it: the wings shut and it drops.
A_MOTH = moth(88, 60, 76)
A_STREAKS = streaks([(40, 4, 30), (60, 2, 38), (80, 6, 26)])


def a_streaks():
    return _clear_of(A_STREAKS(), A_MOTH())


# B: THROUGH. The moth in the middle of a row of prisms, the middle prism showing through it (white outline over the
# hull): it is incorporeal and clips through mass. Streaks above.
B_MOTH = moth(82, 62, 72)
B_ROW = [(10, 82, 15), (116, 82, 15)]


def b_prisms():
    out = empty()
    for x, y, s in B_ROW:
        out |= prism_lit(x, y, s)
    return _clear_of(out, B_MOTH())


def b_ghost_prism():
    import toy_portraits
    whole = prism_lit(58, 82, 19)
    return whole & toy_portraits.grown(~whole, 3.5)   # the outline: the prism less itself shrunk by 3.5 px


def b_streaks():
    return _clear_of(streaks([(42, 2, 24), (62, 0, 30), (82, 4, 22)])(), B_MOTH())


# C: THE DROP AND THE TURN. Where it started (a dim moth, top), the line it fell (dashes), and the moth turned nose-down
# at the landing, flying on along that line. A full drop leaves a wormhole mouth at each end.
C_TOP = (64.0, 28.0)
C_BOT = (64.0, 100.0)
C_GHOST = moth(54, C_TOP[0], C_TOP[1])
C_LANDED = moth(54, C_BOT[0], C_BOT[1], roll=90)


def c_line():
    out = empty()
    for y0, y1 in ((56, 62), (66, 72)):
        out |= stroke([(C_TOP[0], y0), (C_TOP[0], y1)], w=4.5)
    return out


def c_mouths():
    return _clear_of(ring(C_TOP[0], C_TOP[1], 27, w=4.5) | ring(C_BOT[0], C_BOT[1], 27, w=4.5), C_GHOST(), C_LANDED())


def _dim(state, a=0.3):
    return [(k, c, a * (x[0] if x else 1.0)) for k, c, *x in state]


A_LIT = [("streaks", "white"), ("moth", "domain")]
B_LIT = [("row", "blue"), ("streaks", "white"), ("moth", "domain"), ("ghost", "white")]
C_LIT = [("ghost", "domain", 0.4), ("line", "white"), ("landed", "domain")]
TIME_BEFORE = ICON_DIR + "Butterfly_Fold.png"
CARDS += [
    dict(name="drop_a", group="drop", element="Time", before=TIME_BEFORE,
         title="A  the fall: the moth, wings shut (domain), dropping; three fall streaks above it (white)",
         parts=dict(moth=A_MOTH, streaks=a_streaks),
         states=[("ready", [("moth", "domain")] + [("streaks", "white", 0.6)]),
                 ("dropping (LT held)", A_LIT),
                 ("recharging (veil)", _dim(A_LIT))],
         lockup_state=1),
    dict(name="drop_b", group="drop", element="Time", before=TIME_BEFORE,
         title="B  through: the moth falling through a row of prisms, the one it is inside showing through it (it clips "
               "through everything)",
         parts=dict(moth=B_MOTH, row=b_prisms, streaks=b_streaks, ghost=b_ghost_prism),
         states=[("ready", [("row", "blue"), ("moth", "domain"), ("ghost", "white")]),
                 ("dropping (LT held)", B_LIT),
                 ("recharging (veil)", _dim(B_LIT))],
         lockup_state=1),
    dict(name="drop_c", group="drop", element="Time", before=TIME_BEFORE,
         title="C  the drop and the turn: where it started (dim), the line it fell, the moth turned nose-down at the "
               "landing; a full drop leaves a wormhole mouth at each end",
         parts=dict(ghost=C_GHOST, line=c_line, landed=C_LANDED, mouths=c_mouths),
         states=[("ready", C_LIT),
                 ("a full drop: the wormhole", C_LIT + [("mouths", "white")]),
                 ("recharging (veil)", _dim(C_LIT))],
         lockup_state=0),
]
