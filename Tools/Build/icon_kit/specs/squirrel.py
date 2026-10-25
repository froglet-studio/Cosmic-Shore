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

from canvas import band, fill, rect, rotate, scale, stroke, translate
from motifs import hull, speed_wedge, trail_band

HULL = "Squirrel"
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/SquirrelHUDVariant.prefab"
PH = "Assets/_Graphics/{PLACEHOLDERS}/Icons/"

# The brilliant cut the objective glyphs share (author_objective_icons.py CRYSTAL), in icon px, 0.92 scale.
CRYSTAL = scale([(44.8, 12), (83.2, 12), (103.7, 44.8), (85.8, 96), (64, 122.9), (42.2, 96), (24.3, 44.8)], 0.9, 0.9, 64, 64)
CLEAVE = [(4, 96), (124, 30)]


def crystal_joust():
    """Candidate A of 2026-10-25: the fleet's own JOUST pictogram (objective_jousts.png - two lances meeting, the
    spark) with the CRYSTAL at the meeting point. The Jousts objective and the Crystal Joust ability are one act,
    so they wear one mark; the Jousts metric icon in ModeControlsLibrary is rebound to this sprite too."""
    gem = fill(scale(CRYSTAL, 0.5, 0.5, 64, 64))
    lances = stroke([(14, 28), (40, 64), (14, 100)], 12) | stroke([(114, 28), (88, 64), (114, 100)], 12)
    m = lances | gem
    # The spark: four marks that START on the gem's edge, so gem and spark are one mass (and one component).
    for ang in (62, 118, 242, 298):   # steep, so each spark clears the lance arm beside it (gate: gap)
        a = math.radians(ang)
        m |= stroke([(64 + 12 * math.cos(a), 64 + 12 * math.sin(a)), (64 + 44 * math.cos(a), 64 + 44 * math.sin(a))], 7)
    return m


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
