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


# ---- Charge: Scale Dust -------------------------------------------------------------------------------------------------
# The mechanic (TIGER_MOTH.md 1.3): in Dust mode a capsule hangs BELOW the hull, drawn as falling motes in the shielded
# tier of the pilot's domain; while a rival stays inside it, it shreds ONE petal of ONE random element they hold every
# shredInterval (1 s at Charge 0 -> 0.1 s at Charge 10). The petal is stolen. Charge is the RATE. WHO: the moth (domain),
# wings shut as Dust mode folds them; WHAT: the rival (danger) in its dust, a petal coming off. No dust (Mass mode): dim.
import toy_portraits                     # noqa: E402  (the petal shapes come from the HUD's own sprites)
from canvas import rect                  # noqa: E402


def dart(x, y, s=1.0):
    """A rival (any vessel): the fleet's plain dart, nose right."""
    return fill([(x + 11 * s, y), (x - 8 * s, y - 8 * s), (x - 3 * s, y), (x - 8 * s, y + 8 * s)])


def motes(x0, x1, y0, y1, n=14, seed=7, s=3.2):
    """The dust: small diamonds scattered down a column (a fixed scatter, the same every render)."""
    rng = np.random.default_rng(seed)
    out = empty()
    for _ in range(n):
        x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
        out |= fill([(x, y - s), (x + s, y), (x, y + s), (x - s, y)])
    return out


S_MOTH = moth(76, 54, 28)
S_RIVAL = (52.0, 96.0)


def s_rival():
    return dart(*S_RIVAL, s=2.6)


def s_dust():
    return _clear_of(motes(16, 92, 58, 126, n=22, s=4.6), S_MOTH(), s_rival(), px=2.5)


# A: THE SHRED. One petal coming off the rival in the dust, flying out to the right.
def a_petal():
    return toy_portraits.petal("Space", (90.0, 86.0), -30.0, 44.0)


# B: THE RATE. Petals coming off one after another, each a different element (one random element per shred): the
# trail of them is the rate Charge raises.
def b_petals(n):
    def draw():
        out = empty()
        spots = [("Mass", (86.0, 92.0), -15.0, 30.0), ("Time", (102.0, 66.0), -40.0, 28.0), ("Charge", (112.0, 36.0), -65.0, 26.0)]
        for el, tip, d, L in spots[:n]:
            out |= toy_portraits.petal(el, tip, d, L)
        return out
    return draw


# C: THE CAPSULE. The dust as the capsule it is (a rounded column hanging under the hull, outlined), the rival inside
# it, a petal leaving through its wall.
def c_capsule():
    import toy_portraits as tp
    body = rect(20, 56, 86, 112) | disc(53, 56, 33) | disc(53, 112, 33)
    body &= rect(0, 46, 128, 126)
    edge = body & tp.grown(~body, 4.0)
    return _clear_of(edge, S_MOTH(), px=2.0)


def c_petal():
    return toy_portraits.petal("Space", (94.0, 86.0), -25.0, 40.0)


S_LIT = [("dust", "shielded"), ("moth", "domain"), ("rival", "danger")]
CHARGE_BEFORE = ICON_DIR + "Butterfly_ScaleDust.png"
CARDS += [
    dict(name="dust_a", group="dust", element="Charge", before=CHARGE_BEFORE,
         title="A  the shred: the moth (domain) over its dust (shielded), a rival (danger) in it, one petal coming off (white)",
         parts=dict(moth=S_MOTH, dust=s_dust, rival=s_rival, petal=a_petal),
         states=[("shredding", S_LIT + [("petal", "white")]),
                 ("dust on, nobody in it", [("dust", "shielded"), ("moth", "domain")]),
                 ("Mass mode (no dust)", [("dust", "shielded", 0.25), ("moth", "domain", 0.4)])],
         lockup_state=0),
    dict(name="dust_b", group="dust", element="Charge", before=CHARGE_BEFORE,
         title="B  the rate: petals of different elements coming off one after another; Charge 10 sheds three to Charge 0's one",
         parts=dict(moth=S_MOTH, dust=s_dust, rival=s_rival, p1=b_petals(1), p3=b_petals(3)),
         states=[("Charge 0: one a second", S_LIT + [("p1", "white")]),
                 ("Charge 10: ten a second", S_LIT + [("p3", "white")]),
                 ("Mass mode (no dust)", [("dust", "shielded", 0.25), ("moth", "domain", 0.4)])],
         lockup_state=1),
    dict(name="dust_c", group="dust", element="Charge", before=CHARGE_BEFORE,
         title="C  the capsule: the dust drawn as the capsule hanging under the hull (shielded), the rival inside, a petal "
               "leaving through its wall",
         parts=dict(moth=S_MOTH, cap=c_capsule, rival=s_rival, petal=c_petal),
         states=[("shredding", [("cap", "shielded"), ("moth", "domain"), ("rival", "danger"), ("petal", "white")]),
                 ("dust on, nobody in it", [("cap", "shielded"), ("moth", "domain")]),
                 ("Mass mode (no dust)", [("cap", "shielded", 0.25), ("moth", "domain", 0.4)])],
         lockup_state=0),
]
