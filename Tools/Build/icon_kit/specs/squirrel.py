"""The Squirrel's ability-row icons (Resources/ElementalAbilityMaps/Squirrel.asset), angular, one object each.

  Charge  Crystal Joust  the fleet's joust pictogram (two lances meeting, the spark) with the crystal at the
                         meeting point (before: a SKULL placeholder, HuntIcon-PLACEHOLDER.png, which is also the
                         Jousts metric icon in ModeControlsLibrary.asset - rebound there too; round 2's cleaved
                         gem was a gem with a cut, the joust itself not in it)
  Mass    Boost Ring     KEPT - the artist's cross-section of eight prisms (BoostRingCrossSectionIcon.png) already
                         reads and is the omni card's shielded ring's twin; nothing here redraws it
  Space   Steal          KEPT - GENERATED in code (SquirrelVesselHUDView.EnsureGeneratedAbilityIcons: the reach
                         ring at the skimmer's live radius with the steal count under it); no sprite to draw
  Time    Skimming       the hull skimming the trail: the chevron low over a ribbon of blocks, one motion bar
                         (before: the SPARROW's boost icon, borrowed)
  core    Drift          the hull sliding: the chevron turned off its course on a swept band
  core    Sharp Drift    the same hull broadside on a tighter, double band (before: DriftIconSelected /
                         CannonIconSelected placeholders - baked teal, and the sharp one a CANNON)

The core (drift) icons live in Core/ so the family gate compares the elemental icons only.
"""
import math

from canvas import band, empty, fill, ngon, rect, rotate, scale, stroke, translate
from motifs import hull, speed_wedge, trail_band

HULL = "Squirrel"
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/SquirrelHUDVariant.prefab"
PH = "Assets/_Graphics/{PLACEHOLDERS}/Icons/"

# The brilliant cut the objective glyphs share (author_objective_icons.py CRYSTAL), in icon px, 0.92 scale.
CRYSTAL = scale([(44.8, 12), (83.2, 12), (103.7, 44.8), (85.8, 96), (64, 122.9), (42.2, 96), (24.3, 44.8)], 0.9, 0.9, 64, 64)
CLEAVE = [(4, 96), (124, 30)]


def squirrel_top(cx=64, cy=64, L=112, heading=-90, ribs=((20, 24), (40, 16))):
    """The Squirrel's OWN silhouette, from above: a slim wedge body with the canopy, two forward-swept wings ending
    in engine pods, and the ribbed tail (a spine with chevron ribs). Proportions measured off its FBX top view
    (python3 Tools/Build/icon_kit/hull_view.py "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx":
    4.5 wide x 6.45 long, body 3.3, tail 2.7+). Nose at -y in its own frame; heading -90 = nose up, 0 = nose right.
    Two ribs by default - three fuse into a fishbone blur at 40 px. Garrett 2026-10-25: "it should read as the
    squirrel silhouette vs a generic one"."""
    k = L / 100.0

    def P(pts):
        return rotate([(cx + x * k, cy + y * k) for x, y in pts], heading + 90, cx, cy)

    body = fill(P([(0, -50), (6, -42), (8, -20), (7, 4), (0, 10), (-7, 4), (-8, -20), (-6, -42)]))
    wings = empty()
    for sgn in (1, -1):
        wings |= fill(P([(sgn * 6, -6), (sgn * 6, -22), (sgn * 30, -46), (sgn * 40, -46), (sgn * 40, -30), (sgn * 14, -2)]))
    spine = stroke(P([(0, 6), (0, 50)]), 7 * k)
    rb = empty()
    for y, w in ribs:
        rb |= stroke(P([(-w, y - w * 0.55), (0, y), (w, y - w * 0.55)]), 7 * k)
    return body | wings | spine | rb


def crystal_joust():
    """The joust: the Squirrel punching THROUGH the creature at its nose - the creature (a lifeform's hexagonal
    body) split by the channel the hull cut, the Squirrel solid across it, its wings clear of the halves.
    Chosen 2026-10-25 from four placements (halves flung apart / at the nose / bigger / already passed)."""
    me = squirrel_top(56, 64, 100, heading=0)
    body = fill(ngon(6, 26, 96, 64, rot=0))
    channel = rect(0, 55, 128, 73)
    return me | (body & ~channel)


def skimming():
    ribbon = trail_band(y0=98, y1=118)
    body = hull(70, 58, 36, heading=0)
    bar = speed_wedge(14, 58, 22, 9)
    return ribbon | body | bar


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


ICONS = [
    dict(name="Squirrel_CrystalJoust", element="Charge", act="Crystal Joust", draw=crystal_joust, before=PH + "HuntIcon-PLACEHOLDER.png"),
    dict(name="Squirrel_Skimming", element="Time", act="Skimming", draw=skimming, before="Assets/_Graphics/Design Assests/HUD UI/New_Sparrow/boost icon.png"),
    dict(name="Squirrel_Drift", element="Core", act="Drift", draw=drift, subdir="Core", before=PH + "DriftIconSelected-PLACEHOLDER.png"),
    dict(name="Squirrel_SharpDrift", element="Core", act="Sharp Drift", draw=sharp_drift, subdir="Core", before=PH + "CannonIconSelected-PLACEHOLDER.png"),
]

REBIND = [
    dict(file=VARIANT, old="28ece425c4d02474e802223309bdbb11", icon="Squirrel_CrystalJoust", expect=1),   # the skull
    dict(file="Assets/Resources/ModeControlsLibrary.asset", old="28ece425c4d02474e802223309bdbb11", icon="Squirrel_CrystalJoust", expect=1),
    dict(file=VARIANT, old="f0197e0a2ce38654ea3d882fff64967a", icon="Squirrel_Skimming", expect=1),       # the Sparrow's boost icon
    dict(file=VARIANT, old="41c9f6e2ec44f0c458351500f1bfacdc", icon="Squirrel_Drift", expect=1),          # DriftIconSelected-PLACEHOLDER
    dict(file=VARIANT, old="117829e3ccd2f4c439bc98b203359180", icon="Squirrel_SharpDrift", expect=1),     # CannonIconSelected-PLACEHOLDER
]
