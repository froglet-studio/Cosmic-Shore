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
               "Charge 5 Toll Gate: the wormhole it leaves (domain; danger on a toll)"),
              ("DropStreaks", "TigerMoth_DropStreaks", "dropStreaksIcon", drop_streaks, "the fall (white, full while dropping)"),
              ("DropMoth", "TigerMoth_DropMoth", "dropMothIcon", drop_moth, "the Tiger Moth, wings shut (domain)")]
ICONS += [dict(name=i, element="Time", act="Drop: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in DROP_PARTS]
# Garrett, 2026-10-31, on the built card: "the portal should only appear once the upgrade comes", as "a colored ring",
# and "when an enemy goes through the charge upgraded portal make it red". The wormhole is drawn only at Charge 5 Toll
# Gate (the drop leaves a neutral pair before it, which the card does not draw), in the domain colour; a toll flashes it
# danger.
DROP_READY = [("streaks", "white", 0.6), ("moth", "domain")]
DROP_LIT = [("streaks", "white"), ("moth", "domain")]
CARDS += [
    dict(name="drop", element="Time", before=ICON_DIR + "Butterfly_Fold.png",
         title="Drop (Time, picked A): the moth, wings shut (domain), falling; at Charge 5 Toll Gate the wormhole it "
               "leaves behind appears as a ring in the domain colour, red as a rival pays through it",
         parts=dict(mouth=drop_mouth, streaks=drop_streaks, moth=drop_moth),
         states=[("ready", DROP_READY),
                 ("dropping (LT held)", DROP_LIT),
                 ("Charge 5: Toll Gate", [("mouth", "domain")] + DROP_READY),
                 ("a rival pays the toll", [("mouth", "danger")] + DROP_READY),
                 ("recharging (veil)", _dim(DROP_READY))],
         lockup_state=0),
]
LOCKUP["Time"] = dict(parts=dict(streaks=drop_streaks, moth=drop_moth), state=DROP_READY)
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
# shredInterval (1 s at Charge 0 -> 0.1 s at Charge 10). Charge 5 is Toll Gate: the Drop's wormhole tolls rivals.
# Round 1 drew A the shred, B the rate, C the capsule; Garrett picked A (2026-10-31): "we don't need the element to be
# seen coming out. Show a grey ship and turn it Red when you hit then it will look different then space", and "when
# charge is upgraded, show a colored ring" - the same wormhole ring the Time card wears, red as a rival pays through it.
from canvas import rect                  # noqa: E402

GREY = (150, 154, 170, 255)              # a rival the dust has not reached: no team, no threat yet


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


S_MOTH = moth(76, 54, 30)
S_RIVAL = (54.0, 98.0)
S_RING = (98.0, 16.0, 22.0, 7.0)         # the Toll Gate wormhole, top right, clear of the moth


def dust_moth():
    return S_MOTH()


def dust_rival():
    return dart(*S_RIVAL, s=2.6)


def dust_motes():
    return _clear_of(motes(16, 96, 60, 126, n=22, s=4.6), S_MOTH(), dust_rival(), px=2.5)


def dust_ring():
    cx, cy, rx, ry = S_RING
    return _clear_of(_oval(cx, cy, rx, ry, 4.0) | _oval(cx, cy + 2.0, rx * 0.55, ry * 0.5, 3.0), S_MOTH(), px=2.5)


DUST_PARTS = [("ScaleDustRing", "TigerMoth_ScaleDustRing", "scaleDustRingIcon", dust_ring,
               "Charge 5 Toll Gate: the wormhole (domain; danger on a toll)"),
              ("ScaleDustMotes", "TigerMoth_ScaleDustMotes", "scaleDustMotesIcon", dust_motes, "the dust (shielded)"),
              ("ScaleDustRival", "TigerMoth_ScaleDustRival", "scaleDustRivalIcon", dust_rival, "a rival (grey; danger on a hit)"),
              ("ScaleDustMoth", "TigerMoth_ScaleDustMoth", "scaleDustMothIcon", dust_moth, "the Tiger Moth, wings shut (domain)")]
ICONS += [dict(name=i, element="Charge", act="Scale Dust: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in DUST_PARTS]
DUST_ON = [("dust", "shielded"), ("moth", "domain"), ("rival", GREY)]
CARDS += [
    dict(name="scaledust", element="Charge", before=ICON_DIR + "Butterfly_ScaleDust.png",
         title="Scale Dust (Charge, picked A): the moth (domain) over its dust (shielded), a rival grey until the dust "
               "hits it, red then; at Charge 5 the Toll Gate wormhole as a ring (domain, red as a rival pays)",
         parts=dict(dust=dust_motes, moth=dust_moth, rival=dust_rival, ring=dust_ring),
         states=[("Dust mode", DUST_ON),
                 ("a hit", [("dust", "shielded"), ("moth", "domain"), ("rival", "danger")]),
                 ("Charge 5: Toll Gate", DUST_ON + [("ring", "domain")]),
                 ("a rival pays the toll", DUST_ON + [("ring", "danger")]),
                 ("Mass mode (no dust)", [("dust", "shielded", 0.25), ("moth", "domain", 0.4), ("rival", GREY, 0.4)])],
         lockup_state=0),
]
LOCKUP["Charge"] = dict(parts=dict(dust=dust_motes, moth=dust_moth, rival=dust_rival), state=DUST_ON)
CHARGE_ICON = "7700000000000001105"     # the Charge slot's icon Image (was Butterfly_ScaleDust)
REBIND += [
    dict(file=VARIANT, pair=dict(container=CHARGE_ICON, view=VIEW, after="wingEnergyGauge",
                                 children=[(n, i, f) for n, i, f, _d, _a in DUST_PARTS])),
]


# ---- Mass: Mass / Dust mode --------------------------------------------------------------------------------------------
# The mechanic (BUTTERFLY.md 3.2, kept by the Tiger Moth): RT TOGGLES. Mass mode spreads the wings and lays a WIDE wake of
# piano keys (5x at Mass 0 -> 20x at Mass 15), dust off; Dust mode shuts the wings, narrows the wake to the bare key and
# turns the dust on. The plate is the mode fill (full in Mass mode, empty in Dust mode). L5 Gilded Wake: Mass mode lays
# SHIELDED keys. Seen from ABOVE, nose up, because width is the one thing the side view cannot show; the moth is the
# hull in its two real poses (bind pose: wings spread; `rig|fold`: wings shut).
TOP = dict(pitch=0, yaw=0, dilate_px=1.0, roll=-90)                 # from above, nose up
M_SPREAD = lambda: mesh_silhouette.silhouette(FBX, box=80, cx=64, cy=32, **TOP)
M_SHUT = lambda: mesh_silhouette.silhouette(FBX, box=58, cx=64, cy=32, take="rig|fold", **TOP)
KEYS_Y = (78.0, 97.0, 116.0)


def _key(y, hw, hh=5.5):
    return fill([(64 - hw, y), (64 - hw + hh, y - hh), (64 + hw - hh, y - hh), (64 + hw, y),
                 (64 + hw - hh, y + hh), (64 - hw + hh, y + hh)])


def keys_core():
    return _clear_of(sum_masks(_key(y, 12) for y in KEYS_Y), M_SPREAD(), px=2.5)


def keys_wide():
    # The width Mass adds: each key's two ends, cut clear of the core so the HUD can light them apart.
    out = sum_masks(_key(y, 54 - 6 * i) for i, y in enumerate(KEYS_Y))
    return _clear_of(out, keys_core(), M_SPREAD(), px=3.0)


def sum_masks(masks):
    out = empty()
    for m in masks:
        out |= m
    return out


# B: the wings are the mode - the spread moth over the wide keys, the shut moth over the bare keys in its dust.
def b_spread():
    return M_SPREAD()


def b_shut():
    return M_SHUT()


def b_dust():
    rng = np.random.default_rng(9)
    out = empty()
    for _ in range(14):
        x, y = 64 + rng.uniform(-26, 26), 30 + rng.uniform(-24, 26)
        out |= fill([(x, y - 3.2), (x + 3.2, y), (x, y + 3.2), (x - 3.2, y)])
    return _clear_of(out, M_SHUT(), px=3.0)




# C: the width - one wide key with arrows pushing its ends out (Mass widens the wake), the moth spread above it.
def c_key():
    return _key(84, 34, 7)


def c_arrows():
    out = empty()
    for sgn in (-1, 1):
        x0, x1 = 64 + sgn * 40, 64 + sgn * 58
        out |= stroke([(x0, 84), (x1, 84)], w=4.5)
        out |= fill([(x1 + sgn * 4, 84), (x1 - sgn * 5, 76), (x1 - sgn * 5, 92)])
    return out


def c_trail():
    return sum_masks(_key(y, 10, 4.5) for y in (104, 120))


MASS_BEFORE = ICON_DIR + "Butterfly_SpreadWings.png"
MASS_GAUGE = {"Mass mode": (1.0, None), "Dust mode": (0.0, None), "Mass 5: Gilded Wake": (1.0, None)}
CARDS += [
    dict(name="mass_a", group="mass", element="Mass", before=MASS_BEFORE, before_gauge=True, gauge=MASS_GAUGE,
         title="A  the wide wake: the moth from above, wings spread, laying wide piano keys; Dust mode drops their ends "
               "and keeps the bare keys; Gilded Wake lays them shielded",
         parts=dict(moth=M_SPREAD, core=keys_core, wide=keys_wide),
         states=[("Mass mode", [("wide", "domain"), ("core", "domain"), ("moth", "domain")]),
                 ("Dust mode", [("wide", "domain", 0.2), ("core", "domain"), ("moth", "domain")]),
                 ("Mass 5: Gilded Wake", [("wide", "shielded"), ("core", "shielded"), ("moth", "domain")])],
         lockup_state=0),
    dict(name="mass_b", group="mass", element="Mass", before=MASS_BEFORE, before_gauge=True, gauge=MASS_GAUGE,
         title="B  the wings are the mode: Mass mode the moth spread over wide keys; Dust mode the moth shut, the keys "
               "bare and the dust round it (the two real poses, swapped by the HUD)",
         parts=dict(spread=b_spread, shut=b_shut, dust=b_dust, core=keys_core, wide=keys_wide),
         states=[("Mass mode", [("wide", "domain"), ("core", "domain"), ("spread", "domain")]),
                 ("Dust mode", [("dust", "shielded"), ("core", "domain"), ("shut", "domain")]),
                 ("Mass 5: Gilded Wake", [("wide", "shielded"), ("core", "shielded"), ("spread", "domain")])],
         lockup_state=0),
    dict(name="mass_c", group="mass", element="Mass", before=MASS_BEFORE, before_gauge=True, gauge=MASS_GAUGE,
         title="C  the width: one wide key with its ends pushed out, the moth spread above; Dust mode the arrows go "
               "and the key is bare",
         parts=dict(moth=M_SPREAD, key=c_key, arrows=c_arrows, trail=c_trail),
         states=[("Mass mode", [("arrows", "white"), ("key", "domain"), ("trail", "domain"), ("moth", "domain")]),
                 ("Dust mode", [("trail", "domain"), ("key", "domain", 0.3), ("moth", "domain")]),
                 ("Mass 5: Gilded Wake", [("arrows", "white"), ("key", "shielded"), ("trail", "shielded"),
                                          ("moth", "domain")])],
         lockup_state=0),
]


# ---- the OMNI crystal card: the bloom -----------------------------------------------------------------------------------
# BUTTERFLY.md 3.3a (the Tiger Moth keeps it): flying through an omni crystal sets off a 900-unit BLOOM of the dust - a
# sphere of motes in the pilot's shielded tier - that strips rivals' petals (ejected as crystals, a scored hit), cleanses
# the pilot and every ally it reaches, kills rival lifeform hearts, and rolls the dust's own outcomes on every prism.
# The card is VesselHUDView.omniAbilitySprite: ONE sprite, white (the lockup tints nothing on the omni card). Garrett,
# 2026-10-31: "You are still forgetting to show the omnicrystal totem as part of this exercise."
# Garrett picked A (2026-10-31): "omni A looks good but make the moth to scale in the explosion". At true scale the moth
# (span 21.35) in a 900-unit bloom is 1.2% of its width, about 1.5 px on this canvas and nothing on a 40 px card, so it
# is drawn as small as it still reads (box 24, about 8 px on the HUD) and the bloom fills the card to its rim.
O_MOTH = moth(24, 64, 64)


def annulus_motes(r0, r1, n, seed=11, s=4.2):
    rng = np.random.default_rng(seed)
    out = empty()
    for _ in range(n):
        a = rng.uniform(0, 2 * math.pi)
        r = math.sqrt(rng.uniform(r0 * r0, r1 * r1))
        x, y = 64 + r * math.cos(a), 64 + r * math.sin(a)
        out |= fill([(x, y - s), (x + s, y), (x, y + s), (x - s, y)])
    return out


def bloom_dust():
    # The motes on a golden-angle spiral, so the sphere reads evenly filled with no clumps hiding the moth.
    out = empty()
    n, r0, r1, s = 34, 22.0, 54.0, 5.0
    for i in range(n):
        r = math.sqrt(r0 * r0 + (r1 * r1 - r0 * r0) * (i + 0.5) / n)
        a = i * math.pi * (3 - math.sqrt(5))
        x, y = 64 + r * math.cos(a), 64 + r * math.sin(a)
        out |= fill([(x, y - s), (x + s, y), (x, y + s), (x - s, y)])
    return _clear_of(out, O_MOTH(), px=3.0)


def bloom():
    return O_MOTH() | bloom_dust()


ICONS += [dict(name="TigerMoth_Bloom", element="Omni", subdir="Core", family=False, draw=bloom,
               act="The OMNI crystal card: the bloom, a 900-unit sphere of the dust round the moth (one white sprite)",
               exempt={"components": "the bloom's motes", "gap": "the air between motes", "specks": "the motes"})]
CARDS += [
    dict(name="omni", element="Omni",
         title="The OMNI crystal card (picked A): the bloom, the moth small at the heart of a sphere of its dust",
         parts=dict(moth=O_MOTH, dust=bloom_dust), states=[("a crystal collected", [("dust", "white"), ("moth", "white")])]),
]
LOCKUP["Omni"] = dict(parts=dict(i=bloom), state=[("i", "white")])
REBIND[-1]["pair"]["fields"] = {"omniAbilitySprite": ("icon", "TigerMoth_Bloom")}
