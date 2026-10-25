"""The Squirrel's ability-row icons (Resources/ElementalAbilityMaps/Squirrel.asset), angular, one object each.

  Charge  Crystal Joust  the fleet's joust pictogram (two lances meeting, the spark) with the crystal at the
                         meeting point (before: a SKULL placeholder, HuntIcon-PLACEHOLDER.png, which is also the
                         Jousts metric icon in ModeControlsLibrary.asset - rebound there too; round 2's cleaved
                         gem was a gem with a cut, the joust itself not in it)
  Mass    Boost Ring     KEPT - the artist's cross-section of eight prisms (BoostRingCrossSectionIcon.png) already
                         reads and is the omni card's shielded ring's twin; nothing here redraws it
  Space   Steal          KEPT - GENERATED in code (SquirrelVesselHUDView.EnsureGeneratedAbilityIcons: the reach
                         ring at the skimmer's live radius with the steal count under it); no sprite to draw
  Time    Skimming       the Squirrel centred in its skimmer sphere over a row of prisms (blocks) - a tinted PAIR:
                         hull + sphere + the prisms under it in the domain colour, the prisms ahead in the blue
                         (before: the SPARROW's boost icon, borrowed; round 2's chevron over a notched band)
  core    Drift          the hull sliding: the chevron turned off its course on a swept band
  core    Sharp Drift    the same hull broadside on a tighter, double band (before: DriftIconSelected /
                         CannonIconSelected placeholders - baked teal, and the sharp one a CANNON)

The core (drift) icons live in Core/ so the family gate compares the elemental icons only.
"""
import math

import mesh_silhouette
from canvas import band, empty, fill, ngon, rect, ring, rotate, scale, stroke, translate
from motifs import block, hull, speed_wedge, trail_band

HULL = "Squirrel"
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/SquirrelHUDVariant.prefab"
PH = "Assets/_Graphics/{PLACEHOLDERS}/Icons/"

# The brilliant cut the objective glyphs share (author_objective_icons.py CRYSTAL), in icon px, 0.92 scale.
CRYSTAL = scale([(44.8, 12), (83.2, 12), (103.7, 44.8), (85.8, 96), (64, 122.9), (42.2, 96), (24.3, 44.8)], 0.9, 0.9, 64, 64)
CLEAVE = [(4, 96), (124, 30)]


FBX = "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"
VIEW_D = dict(pitch=45, yaw=15, dilate_px=1.2)   # Garrett 2026-10-25: "D" of the twelve-view sheet


def squirrel(box, cx, cy, roll=0.0, dilate_px=None):
    """The Squirrel's REAL silhouette: its FBX mesh, parallel-projected from above tipped 45 and turned 15 (view D),
    thickened so the ribbed tail survives the 60 px card. Garrett 2026-10-25 on a hand-drawn version: "the
    fidelity is too low. this reads too abstractly." On this view the hull's nose-tail axis is horizontal."""
    kw = dict(VIEW_D)
    if dilate_px is not None:
        kw["dilate_px"] = dilate_px
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, roll=roll, **kw)


def squirrel_nose(box, cx, cy, roll=0.0):
    """Where the mesh's nose vertex lands for the same placement (the point the joust composes around)."""
    return mesh_silhouette.project_point(FBX, mesh_silhouette.nose_vertex(FBX), box=box, cx=cx, cy=cy, roll=roll,
                                         pitch=VIEW_D["pitch"], yaw=VIEW_D["yaw"])


HALF = dict(box=54, dilate_px=1.8)      # each hull of the pair: fitted to 54 px, the noses 8 px apart ("separate them a bit")


def joust_self():
    """The player's Squirrel, right-facing, in the LEFT half: nose at x 60."""
    return squirrel(HALF["box"], 33, 64, dilate_px=HALF["dilate_px"])


def joust_other():
    """The opponent, the same hull the same size mirrored, in the RIGHT half: nose at x 68, nose to nose."""
    return mesh_silhouette.silhouette(FBX, box=HALF["box"], cx=95, cy=64, dilate_px=HALF["dilate_px"], mirror=True,
                                      pitch=VIEW_D["pitch"], yaw=VIEW_D["yaw"])


def crystal_joust():
    """The joust: two Squirrels (the real mesh, view D), the same size, nose to nose. On the HUD they are TWO
    child Images the view tints separately - the player's in the pilot's domain colour, the opponent in the
    palette's danger colour, both snapping to the shared lime on a joust (SquirrelVesselHUDView.PaintJoustPair /
    JuiceImpact). This composite is the one white sprite the Jousts objective box shows (ModeControlsLibrary).
    Garrett 2026-10-25: "the two squirrels should be facing each other and the same size. the right facing
    squirrel should be the players domain color. the other squirrel can be the danger color (fire). when the
    player jousts then both can turn lime." """
    return joust_self() | joust_other()


SKIM_RING = dict(cx=56, cy=56, r=46, w=5)          # "keep the ring white, make it a bit thinner"          # B2's sphere, a touch left so prisms can be AHEAD of it
SKIM_BLOCKS = (14, 37, 60, 83, 106)                  # front-face centres along the bottom (B2's blocks, five of them)
SKIM_Y = 113
SKIM_BLOCK = dict(w=17, h=11, dp=6)
SKIM_REACH = SKIM_RING["cx"] + SKIM_RING["r"] * 0.55  # a prism centred beyond this is "in front of the skimmer"


def skim_self():
    """What the skim has made YOURS: the Squirrel centred in its skimmer sphere, and the prisms under / behind the
    sphere - tinted the pilot's domain colour on the HUD."""
    m = squirrel(62, SKIM_RING["cx"], SKIM_RING["cy"], dilate_px=1.5)
    for x in SKIM_BLOCKS:
        if x <= SKIM_REACH:
            m |= block(x, SKIM_Y, **SKIM_BLOCK)
    return m


def skim_ring():
    """The skimmer sphere itself: WHITE on the HUD, its own untinted part (Garrett: "keep the ring white")."""
    return ring(SKIM_RING["cx"], SKIM_RING["cy"], SKIM_RING["r"], SKIM_RING["w"])


def skim_ahead():
    """The prisms still AHEAD of the sphere - tinted the HUD's blue (ElementalBarsConfigSO.blueColor)."""
    m = empty()
    for x in SKIM_BLOCKS:
        if x > SKIM_REACH:
            m |= block(x, SKIM_Y, **SKIM_BLOCK)
    return m


def skimming():
    """Skimming (Time): the Squirrel in its skimmer sphere passing over a row of prisms. On the HUD this is a tinted
    PAIR (like the joust): hull + sphere + the prisms it has reached wear the domain colour, the prisms ahead the
    blue. Garrett 2026-10-25: "make the vessel and the prisms touching and behind the skimmer the player's domain
    color. make the prisms in front of the skimmer the blue." "i like b2 the best" - B2's sphere and blocks."""
    return skim_self() | skim_ring() | skim_ahead()


def drift():
    # The hull points up-right while it travels right: the slide is the broad arc under its tail.
    body = fill(rotate([(112, 50), (48, 18), (64, 50), (48, 82)], -32, 84, 50))
    sweep = band(28, 30, 64, 24, 108, 12)
    return body | sweep


def sharp_drift():
    # Broadside, and the arc tighter and doubled.
    body = fill(rotate([(116, 44), (44, 8), (62, 44), (44, 80)], -74, 86, 44))
    sweep = band(40, 22, 76, 30, 110, 12) | band(40, 22, 54, 36, 104, 8)
    return body | sweep


HALF_EXEMPT = {k: "half of the nose-to-nose pair; the pair is measured on Squirrel_CrystalJoust" for k in ("coverage", "box_fill", "gap", "in_box")}
ICONS = [
    dict(name="Squirrel_CrystalJoust", element="Charge", act="Crystal Joust", draw=crystal_joust, before=PH + "HuntIcon-PLACEHOLDER.png",
         exempt={"gap": "Garrett 2026-10-25 - the hulls are their real mesh; the rib gaps are the fidelity he asked for",
                 "coverage": "two real hulls nose to nose at 57 px each sit at 0.10; thicker than 1.8 px loses the ribs"}),
    dict(name="Squirrel_JoustSelf", element="Charge", act="Crystal Joust: the player's hull (domain colour)", draw=joust_self, subdir="Pair", family=False, exempt=HALF_EXEMPT),
    dict(name="Squirrel_JoustOther", element="Charge", act="Crystal Joust: the opponent (danger colour)", draw=joust_other, subdir="Pair", family=False, exempt=HALF_EXEMPT),
    dict(name="Squirrel_Skimming", element="Time", act="Skimming", draw=skimming, before="Assets/_Graphics/Design Assests/HUD UI/New_Sparrow/boost icon.png",
         exempt={"gap": "Garrett 2026-10-25 - the hull is its real mesh", "components": "five prisms, a sphere and a hull: the pair's halves are what the HUD draws", "specks": "the blocks' seam slivers"}),
    dict(name="Squirrel_SkimSelf", element="Time", act="Skimming: hull, sphere, prisms reached (domain colour)", draw=skim_self, subdir="Pair", family=False,
         exempt={"gap": "the real mesh", "components": "three prisms and a hull", "specks": "the blocks' seam slivers", "coverage": "half of a pair", "box_fill": "half of a pair", "in_box": "half of a pair"}),
    dict(name="Squirrel_SkimRing", element="Time", act="Skimming: the skimmer sphere (white)", draw=skim_ring, subdir="Pair", family=False,
         exempt={"coverage": "part of a trio", "thin": "a 5 px ring", "box_fill": "part of a trio", "in_box": "part of a trio"}),
    dict(name="Squirrel_SkimAhead", element="Time", act="Skimming: prisms ahead (blue)", draw=skim_ahead, subdir="Pair", family=False,
         exempt={"coverage": "half of a pair", "box_fill": "half of a pair", "in_box": "half of a pair", "components": "two prisms", "specks": "the blocks' seam slivers", "gap": "the seams", "thin": "the seams"}),
    dict(name="Squirrel_Drift", element="Core", act="Drift", draw=drift, subdir="Core", before=PH + "DriftIconSelected-PLACEHOLDER.png"),
    dict(name="Squirrel_SharpDrift", element="Core", act="Sharp Drift", draw=sharp_drift, subdir="Core", before=PH + "CannonIconSelected-PLACEHOLDER.png"),
]

REBIND = [
    # The Charge card's icon is a transparent container over the two tinted hulls (the Dolphin's jaw-pair arrangement);
    # the view's joustSelfIcon / joustOpponentIcon point at the children. The skull it wore is gone with the sprite.
    dict(file=VARIANT, pair=dict(container="4425814395415128621", view="7422230368815066413",
                                 children=[("JoustSelf", "Squirrel_JoustSelf", "joustSelfIcon"),
                                           ("JoustOther", "Squirrel_JoustOther", "joustOpponentIcon")])),
    dict(file="Assets/Resources/ModeControlsLibrary.asset", old="28ece425c4d02474e802223309bdbb11", icon="Squirrel_CrystalJoust", expect=1),
    # The Time card: a second tinted pair under the icon (the sprite it wore - the Sparrow's boost icon, then round 2's
    # chevron - goes with the container's sprite).
    dict(file=VARIANT, pair=dict(container="6559234619875545904", view="7422230368815066413", after="joustOpponentIcon",
                                 children=[("SkimSelf", "Squirrel_SkimSelf", "skimSelfIcon"),
                                           ("SkimRing", "Squirrel_SkimRing", None),
                                           ("SkimAhead", "Squirrel_SkimAhead", "skimAheadIcon")])),
    dict(file=VARIANT, old="41c9f6e2ec44f0c458351500f1bfacdc", icon="Squirrel_Drift", expect=1),          # DriftIconSelected-PLACEHOLDER
    dict(file=VARIANT, old="117829e3ccd2f4c439bc98b203359180", icon="Squirrel_SharpDrift", expect=1),     # CannonIconSelected-PLACEHOLDER
]
