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


def motes(x0, x1, y0, y1, n=14, seed=7, s=3.2, tone=None):
    """The dust: small diamonds scattered down a column (a fixed scatter, the same every render). `tone` 0, 1 or 2 keeps
    only every third mote from that one: the dust is made of three colours (Garrett, 2026-11-01: "make a dust made of
    danger color, team domain color, team shielded domain color"), the same three the game's motes wear
    (ButterflyDustField.ResolveMotePalette), so each colour is its own part and the HUD tints them apart."""
    rng = np.random.default_rng(seed)
    out = empty()
    for i in range(n):
        x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
        if tone is None or i % 3 == tone:
            out |= fill([(x, y - s), (x + s, y), (x, y + s), (x - s, y)])
    return out


DUST_TONES = ("danger", "domain", "shielded")   # the three colours of the dust, mote by mote


S_MOTH = moth(76, 54, 30)
S_RIVAL = (54.0, 98.0)
S_RING = (98.0, 16.0, 22.0, 7.0)         # the Toll Gate wormhole, top right, clear of the moth


def dust_moth():
    return S_MOTH()


def dust_rival():
    return dart(*S_RIVAL, s=2.6)


def dust_motes_tone(tone):
    return lambda: _clear_of(motes(16, 96, 60, 126, n=22, s=4.6, tone=tone), S_MOTH(), dust_rival(), px=2.5)


dust_motes_danger, dust_motes_domain, dust_motes = (dust_motes_tone(t) for t in range(3))


def dust_ring():
    cx, cy, rx, ry = S_RING
    return _clear_of(_oval(cx, cy, rx, ry, 4.0) | _oval(cx, cy + 2.0, rx * 0.55, ry * 0.5, 3.0), S_MOTH(), px=2.5)


DUST_PARTS = [("ScaleDustRing", "TigerMoth_ScaleDustRing", "scaleDustRingIcon", dust_ring,
               "Charge 5 Toll Gate: the wormhole (domain; danger on a toll)"),
              ("ScaleDustMotesDanger", "TigerMoth_ScaleDustMotesDanger", "scaleDustMotesDangerIcon", dust_motes_danger,
               "the dust, its danger third (danger)"),
              ("ScaleDustMotesDomain", "TigerMoth_ScaleDustMotesDomain", "scaleDustMotesDomainIcon", dust_motes_domain,
               "the dust, its domain third (domain)"),
              ("ScaleDustMotes", "TigerMoth_ScaleDustMotes", "scaleDustMotesIcon", dust_motes,
               "the dust, its shielded third (shielded)"),
              ("ScaleDustRival", "TigerMoth_ScaleDustRival", "scaleDustRivalIcon", dust_rival, "a rival (grey; danger on a hit)"),
              ("ScaleDustMoth", "TigerMoth_ScaleDustMoth", "scaleDustMothIcon", dust_moth, "the Tiger Moth, wings shut (domain)")]
ICONS += [dict(name=i, element="Charge", act="Scale Dust: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in DUST_PARTS]
DUST_MOTES = dict(dust_d=dust_motes_danger, dust_p=dust_motes_domain, dust=dust_motes)


def dust_tones(a=None):
    return [(k, c) + ((a,) if a is not None else ()) for k, c in zip(("dust_d", "dust_p", "dust"), DUST_TONES)]


DUST_ON = dust_tones() + [("moth", "domain"), ("rival", GREY)]
CARDS += [
    dict(name="scaledust", element="Charge", before=ICON_DIR + "Butterfly_ScaleDust.png",
         title="Scale Dust (Charge, picked A): the moth (domain) over its dust (danger, domain and shielded motes), a "
               "rival grey until the dust hits it, red then; at Charge 5 the Toll Gate wormhole as a ring",
         parts=dict(moth=dust_moth, rival=dust_rival, ring=dust_ring, **DUST_MOTES),
         states=[("Dust mode", DUST_ON),
                 ("a hit", dust_tones() + [("moth", "domain"), ("rival", "danger")]),
                 ("Charge 5: Toll Gate", DUST_ON + [("ring", "domain")]),
                 ("a rival pays the toll", DUST_ON + [("ring", "danger")]),
                 ("Mass mode (no dust)", dust_tones(0.25) + [("moth", "domain", 0.4), ("rival", GREY, 0.4)])],
         lockup_state=0),
]
LOCKUP["Charge"] = dict(parts=dict(moth=dust_moth, rival=dust_rival, **DUST_MOTES), state=DUST_ON)
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


KEY_HH, KEY_CORE, KEY_GAP = 5.5, 12.0, 3.5   # half-height, the bare key's half-width, the square cut between core and end


def _key_hw(i):
    return 54.0 - 6.0 * i                    # the wide keys taper a little toward the bottom: the wake behind


# Garrett, 2026-11-01: "make the base have more square edges. including the cut out. once gilded then change them to
# octahedrons on the outside but keep them square edge on the interior." So every key is a rectangle and the cut between
# its core and its ends is a straight vertical gap; under Gilded Wake the ends' OUTER edge comes to a point (the
# prism's octahedral tip), their inner edge staying square against the cut.
def keys_core():
    return sum_masks(rect(64 - KEY_CORE, y - KEY_HH, 64 + KEY_CORE, y + KEY_HH) for y in KEYS_Y)


def _ends(pointed):
    out = empty()
    for i, y in enumerate(KEYS_Y):
        hw = _key_hw(i)
        for sgn in (-1, 1):
            x0, x1 = 64 + sgn * (KEY_CORE + KEY_GAP), 64 + sgn * hw
            if pointed:
                tip = x1 - sgn * KEY_HH
                out |= fill([(x0, y - KEY_HH), (tip, y - KEY_HH), (x1, y), (tip, y + KEY_HH), (x0, y + KEY_HH)])
            else:
                out |= rect(min(x0, x1), y - KEY_HH, max(x0, x1), y + KEY_HH)
    return out


def keys_wide():
    # The width Mass adds: each key's two ends, square, cut clear of the core so the HUD can light them apart.
    return _ends(False)


def keys_wide_gilded():
    # Gilded Wake (Mass 5): the same ends with an octahedral point on the outside, square against the cut.
    return _ends(True)


def sum_masks(masks):
    out = empty()
    for m in masks:
        out |= m
    return out


# Garrett picked A (2026-10-31, "actually. i like mass a"). The plate keeps its mode fill (wingEnergyGauge); the HUD
# fades the key ends with the eased mode (ButterflyHUDView.SetMassMode) and turns the keys shielded under Gilded Wake.
MASS_PARTS = [("MassKeyEnds", "TigerMoth_MassKeyEnds", "massKeyEndsIcon", keys_wide,
               "the width Mass mode adds, each key's two square ends (domain; faded in Dust mode; hidden under Gilded Wake)"),
              ("MassKeyEndsGilded", "TigerMoth_MassKeyEndsGilded", "massKeyEndsGildedIcon", keys_wide_gilded,
               "Gilded Wake: the ends pointed on the outside, square inside (shielded; faded in Dust mode)"),
              ("MassKeys", "TigerMoth_MassKeys", "massKeysIcon", keys_core,
               "the bare keys every mode lays (domain; shielded under Gilded Wake)"),
              ("MassMoth", "TigerMoth_MassMoth", "massMothIcon", M_SPREAD, "the Tiger Moth from above, wings spread (domain)")]
ICONS += [dict(name=i, element="Mass", act="Mass / Dust mode: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in MASS_PARTS]
MASS_ON = [("wide", "domain"), ("core", "domain"), ("moth", "domain")]
CARDS += [
    dict(name="mass", element="Mass", before=ICON_DIR + "Butterfly_SpreadWings.png", before_gauge=True,
         gauge={"Mass mode": (1.0, None), "Dust mode": (0.0, None), "Mass 5: Gilded Wake": (1.0, None)},
         title="Mass / Dust mode (Mass, picked A): the moth from above, wings spread, laying wide piano keys; Dust mode "
               "fades their ends to the bare keys; Gilded Wake lays them shielded",
         parts=dict(moth=M_SPREAD, core=keys_core, wide=keys_wide, gilded=keys_wide_gilded),
         states=[("Mass mode", MASS_ON),
                 ("Dust mode", [("wide", "domain", 0.2), ("core", "domain"), ("moth", "domain")]),
                 ("Mass 5: Gilded Wake", [("gilded", "shielded"), ("core", "shielded"), ("moth", "domain")])],
         lockup_state=0),
]
LOCKUP["Mass"] = dict(parts=dict(wide=keys_wide, core=keys_core, moth=M_SPREAD), state=MASS_ON, gauge=1.0)
MASS_ICON = "7700000000000001115"       # the Mass slot's icon Image (was Butterfly_SpreadWings)
REBIND += [
    dict(file=VARIANT, pair=dict(container=MASS_ICON, view=VIEW, after="wingEnergyGauge",
                                 children=[(n, i, f) for n, i, f, _d, _a in MASS_PARTS])),
]


# ---- Space: Dust Reach ------------------------------------------------------------------------------------------------
# The mechanic (BUTTERFLY.md 3.3, kept by the Tiger Moth): passive, lives in Dust mode. Space is the dust capsule's
# LENGTH (60 -> 150 across Space 0..10), and the capsule is what the dust does to MASS: own plain prisms it touches grow /
# turn dangerous / turn shielded; rival prisms are destroyed / shrunk / stolen. L5 Diamond Dust: 6% of own plain prisms
# super-shield instead. Side on like the Charge card (the dust hangs below the hull), but the moth small and high so the
# column is LONG, and prisms in it instead of a rival ship: the Charge card is the dust on pilots, this one on mass.
R_MOTH = moth(52, 54, 20)
R_OWN = (36.0, 80.0, 16.0)               # your prism the dust has tended (shielded)
R_RIVAL = (78.0, 102.0, 18.0)            # a rival prism the dust has broken (grey: no team)
R_DIAMOND = (98.0, 58.0, 14.0)           # Space 5 Diamond Dust: your mass super-shielded (the stella octangula)


def r_own():
    return prism_lit(*R_OWN)


def r_rival_shards():
    # A rival prism broken in three: the diamond cut along two lines and its pieces pushed apart.
    cx, cy, sz = R_RIVAL
    whole = prism_lit(cx, cy, sz)
    out = empty()
    cuts = [rect(0, 0, cx - 1.5, cy - 1.5), rect(cx + 1.5, 0, 128, cy + 2.5), rect(0, cy + 1.5, cx - 1.5, 128)]
    shifts = [(-5, -5), (6, -1), (-3, 6)]
    for c, (dx, dy) in zip(cuts, shifts):
        piece = whole & c
        out |= np.roll(np.roll(piece, int(dy * N / 128), 0), int(dx * N / 128), 1)
    return out


def r_diamond():
    from specs.rhino import stella        # the fleet's super-shield, as the Rhino's Blade Reach card draws it
    return _clear_of(stella(R_DIAMOND[0], R_DIAMOND[1], R_DIAMOND[2]), R_MOTH(), px=2.5)


def r_motes_tone(tone):
    return lambda: _clear_of(motes(20, 108, 40, 126, n=24, seed=21, s=4.4, tone=tone), R_MOTH(), r_own(),
                             r_rival_shards(), r_diamond(), px=3.0)


r_motes_danger, r_motes_domain, r_motes = (r_motes_tone(t) for t in range(3))

REACH_PARTS = [("DustReachMotesDanger", "TigerMoth_DustReachMotesDanger", "dustReachMotesDangerIcon", r_motes_danger,
                "the dust, its danger third (danger)"),
               ("DustReachMotesDomain", "TigerMoth_DustReachMotesDomain", "dustReachMotesDomainIcon", r_motes_domain,
                "the dust, its domain third (domain)"),
               ("DustReachMotes", "TigerMoth_DustReachMotes", "dustReachMotesIcon", r_motes,
                "the dust, its shielded third (shielded)"),
               ("DustReachRival", "TigerMoth_DustReachRival", "dustReachRivalIcon", r_rival_shards,
                "a rival prism the dust broke (grey)"),
               ("DustReachOwn", "TigerMoth_DustReachOwn", "dustReachOwnIcon", r_own,
                "your prism the dust tended (shielded)"),
               ("DustReachDiamond", "TigerMoth_DustReachDiamond", "dustReachDiamondIcon", r_diamond,
                "Space 5 Diamond Dust: a super-shielded prism (white), hidden before the upgrade"),
               ("DustReachMoth", "TigerMoth_DustReachMoth", "dustReachMothIcon", R_MOTH,
                "the Tiger Moth, wings shut (domain)")]
ICONS += [dict(name=i, element="Space", act="Dust Reach: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in REACH_PARTS]
REACH_MOTES = dict(dust_d=r_motes_danger, dust_p=r_motes_domain, dust=r_motes)
SPACE_ON = dust_tones() + [("own", "shielded"), ("rival", GREY), ("moth", "domain")]
# Garrett, 2026-11-01, on the options (A recommended): the dust in its three colours, and "use a supershielded prism icon
# that only comes in with the upgrade".
CARDS += [
    dict(name="dustreach", element="Space", before=ICON_DIR + "Butterfly_Wingreach.png",
         title="Dust Reach (Space, A): the moth high, its dust hanging long below it in its three colours, your prism "
               "in it shielded and a rival's broken; Space 5 Diamond Dust brings in a super-shielded prism",
         parts=dict(own=r_own, rival=r_rival_shards, diamond=r_diamond, moth=R_MOTH, **REACH_MOTES),
         states=[("Dust mode", SPACE_ON),
                 ("Space 5: Diamond Dust", SPACE_ON + [("diamond", "white")]),
                 ("Mass mode (no dust)", dust_tones(0.25) + [("own", "shielded", 0.4), ("rival", GREY, 0.4),
                                                             ("moth", "domain", 0.4)])],
         lockup_state=0),
]
LOCKUP["Space"] = dict(parts=dict(own=r_own, rival=r_rival_shards, moth=R_MOTH, **REACH_MOTES), state=SPACE_ON)
SPACE_ICON = "7700000000000001125"      # the Space slot's icon Image (was Butterfly_Wingreach)
REBIND += [
    dict(file=VARIANT, pair=dict(container=SPACE_ICON, view=VIEW, after="wingEnergyGauge",
                                 children=[(n, i, f) for n, i, f, _d, _a in REACH_PARTS])),
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
next(r for r in REBIND if r["pair"]["container"] == CHARGE_ICON)["pair"]["fields"] = {"omniAbilitySprite": ("icon", "TigerMoth_Bloom")}
