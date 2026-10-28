"""The Scarab's ability-row icons (Resources/ElementalAbilityMaps/Scarab.asset).

  Charge  Cavitation Blast  the mantis-shrimp punch that rides the committed right-stick dash: a swept PLATE (a disc
                            across the dash, its face normal the dash) sweeping from the hull, MIRRORED through the hull
                            so it reaches as far behind as ahead; everything it claims - prisms, fauna, a rival's
                            elements - is thrown the way the punch travelled, so mass BEHIND is dragged forward through
                            the ship. Charge shortens its cooldown (x1 -> x0.5); Charge 5, Cavitation Shear, destroys
                            SHIELDED prisms outright instead of only shedding their shields (ScarabCavitationBlast.cs).
  Mass    Switch            a ring the balls thread; Mass grows its radius
  Space   Ball Forge        fly through an omni crystal to mint a ball; Space grows it (x1 -> x4)
  Time    Throttle          analog right-trigger thrust along the nose; Time raises its top speed; Time 5 Snap Dash

Before: four PLACEHOLDER sprites (author_hull_icon_placeholders.py), three of them tinted as gauges by ScarabHUDView
(the blast ready / spent in two colours, the switch by charge count, the forge filling / ready). One card at a time
with Garrett, options in the hull's whole lockup.

The hull is the Scarab's own mesh from straight above, nose RIGHT (pitch 0, yaw 0; the horn and mandibles at +x - the
grid on 2026-10-28), flying across the card (LESSONS 55: the act runs across, clear of the plate's marks).
"""
import mesh_silhouette
from canvas import N, chamfered_rect, disc, empty, fill, rotate
from motifs import prism

import numpy as np
from PIL import Image

HULL = "Scarab"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Scarab/"
FBX = "Assets/_Models/Vessel Models/Scarab.fbx"
VIEW_PLAN = dict(pitch=0, yaw=0, dilate_px=0.8)


def _png_mask(rel):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def placeholder(name):
    return lambda: _png_mask(ICON_DIR + f"Scarab_{name}-PLACEHOLDER.png")


def scarab(box=60, cx=64.0, cy=64.0):
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, **VIEW_PLAN)


def _clear_of(mask, *others, px=2.5):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


ICONS = []
LOCKUP = dict(
    Charge=dict(parts=dict(i=placeholder("CavitationBlast")), state=[("i", "white")]),
    Mass=dict(parts=dict(i=placeholder("Switch")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("BallForge")), state=[("i", "white")]),
    Time=dict(parts=dict(i=placeholder("Throttle")), state=[("i", "white")]),
)
CARDS = []
REBIND = []


# ---- Charge: Cavitation Blast (2026-10-28, A3 "the bubble") --------------------------------------------------------
# Four rounds with Garrett: an edge-on plate and thrown prisms -> "show the relative size of the volume affected
# relative to the scarab" -> "whole prisms outside and shattered prisms that convey the destructive direction inside"
# -> "lets not have a box ... more flat ... negative space prisms inside" -> "more cohesive with the rest of our
# polished icons based on A" -> A3, "looks good enough. lets ship it".
#
# TO SCALE, measured from the prefabs: hull radius r = 4.5 (ScarabHull's SphereCollider; ScarabCavitationBlast.
# MeasureHullRadius takes the largest hull collider, scale 1), plate radius R = radiusPerVesselRadius 10 x r = 45
# (Scarab.prefab), length = lengthPerRadius 1.2 x R = 54 ahead, mirrored (mirrorAboutStartPlane 1,
# AOEScarabCavitation.prefab) 54 behind: from above the swept volume is 2.4R = 108 long by 2R = 90 across, centred on
# the hull, whose mesh is 10.6 long - the punch is ~10 Scarabs long, and the card says so.
#
# The look (what the polished Sparrow / Rhino cards share): ONE bold white mass, parts separated by knock-out gaps,
# grey only for "not ready". The volume is a white bubble - square at the front against the plate (a knock-out groove
# between them), rounded off behind where the mirror half fades into the wake - the hull in domain colour in its own
# knock-out, the prisms the punch claims cut out of it as NEGATIVE space: three forward-pointing slivers in a chevron,
# so the break reads as flung the way the punch travels. Whole prisms (HUD blue) sit OUTSIDE the volume, untouched.
CY = 64.0
HULL_R = 4.5
PLATE_R = 10 * HULL_R                 # 45
REACH = 1.2 * PLATE_R                 # 54 each way
HULL_EXTENT = 10.58                   # the mesh's longest side (nose axis), what silhouette() fits to `box`
HULL_NOSE_OFFSET = 0.47               # the mesh bbox centre sits this far ahead of the hull's origin
PLATE_W = 4.8                         # 1.5 px at 40 px: one pixel on the grid lost mass at the at40 gate (0.81)
PLATE_X1 = 105.6                      # the plate's front, 33 x 3.2: the volume's true front edge (106) on the 40 px grid
GROOVE = 3.0
K = 92 / (2 * REACH)                  # icon px per metre: the volume 92 long (14..106), 77 across; the hull ~9 px
OX = 60.0                             # the hull's origin on the card


def bar(x0, x1, y0, y1):
    return fill([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])


def _x(m):
    return OX + m * K


HALF = PLATE_R * K


def blast_hull():
    return scarab(box=HULL_EXTENT * K, cx=_x(HULL_NOSE_OFFSET), cy=CY)


def blast_plate():
    x = PLATE_X1
    return bar(x - PLATE_W, x, CY - HALF, CY + HALF)


def _bubble():
    x0, x1, h = _x(-REACH), PLATE_X1 - PLATE_W - GROOVE, HALF
    r = h * 0.55
    m = fill([(x0 + r, CY - h), (x1, CY - h), (x1, CY + h), (x0 + r, CY + h)])
    m |= fill([(x0, CY - h + r), (x0 + r + 1, CY - h + r), (x0 + r + 1, CY + h - r), (x0, CY + h - r)])
    return m | disc(x0 + r, CY - h + r, r) | disc(x0 + r, CY + h - r, r)


def shards(cx, cy, s=12.0):
    """A prism the punch claimed, broken and flung the way the punch travels (+x): three slivers, each a wedge
    pointing forward, fanned in a chevron with the middle one furthest ahead - the break reads as a direction,
    where a burst of four quarters reads as an explosion in place."""
    m = empty()
    for dx, dy in ((-4.0, -8.0), (4.0, 0.0), (-4.0, 8.0)):
        x, y = cx + dx, cy + dy
        m |= fill([(x - s * 0.6, y - 3.2), (x + s * 0.7, y), (x - s * 0.6, y + 3.2)])
    return m


def blast_field():
    """The volume at rest / recharging: the bubble, the hull knocked out of it."""
    return _clear_of(_bubble(), blast_hull(), px=2.5)


def blast_field_shattered():
    """The volume as the punch lands: the claimed prisms cut out of it as forward-flung slivers."""
    return blast_field() & ~(shards(72, 42) | shards(72, 86))


def blast_prisms():
    """Whole prisms outside the punch's reach (HUD blue)."""
    return _clear_of(prism(120, 64, 7.0) | prism(26, 13, 7.0) | prism(98, 115, 7.0), blast_plate())


def cavitation_blast():
    """The composite at rest: the card's fallback and the fleet sheet's tile."""
    return blast_prisms() | blast_field() | blast_plate() | blast_hull()


BLAST_EXEMPT = {"components": "a part", "box_fill": "a part", "in_box": "a part", "coverage": "a part",
                "thin": "a part", "specks": "a part", "gap": "a part"}
ICONS += [
    dict(name="Scarab_CavitationBlast", element="Charge",
         act="Cavitation Blast: the punch's volume to scale around the Scarab, the plate at its front, whole prisms outside (composite; the HUD draws the parts)",
         draw=cavitation_blast, exempt={"components": "the volume, the plate, the hull and the prisms",
                                        "specks": "the true-scale hull and the prisms outside",
                                        "gap": "the knock-out groove before the plate and the margin round the hull",
                                        "in_box": "the volume runs the card's width, to scale"}),
    dict(name="Scarab_BlastField", element="Charge", act="Cavitation Blast: the volume, the hull knocked out (white; grey while recharging)",
         draw=blast_field, subdir="Parts", family=False, exempt=BLAST_EXEMPT),
    dict(name="Scarab_BlastFieldShattered", element="Charge", act="Cavitation Blast: the volume with the claimed prisms cut out as forward-flung slivers (white, as the punch lands)",
         draw=blast_field_shattered, subdir="Parts", family=False, exempt=BLAST_EXEMPT),
    dict(name="Scarab_BlastPlate", element="Charge", act="Cavitation Blast: the plate, the volume's front edge (white; grey while recharging)",
         draw=blast_plate, subdir="Parts", family=False, exempt=BLAST_EXEMPT),
    dict(name="Scarab_BlastHull", element="Charge", act="Cavitation Blast: the Scarab at true scale (domain colour)",
         draw=blast_hull, subdir="Parts", family=False, exempt=BLAST_EXEMPT),
    dict(name="Scarab_BlastPrisms", element="Charge", act="Cavitation Blast: whole prisms outside the volume (HUD blue)",
         draw=blast_prisms, subdir="Parts", family=False, exempt=BLAST_EXEMPT),
]

BLAST_TOP = [("prisms", "blue"), ("hull", "domain"), ("plate", "white")]
LOCKUP["Charge"] = dict(parts=dict(field=blast_field, prisms=blast_prisms, hull=blast_hull, plate=blast_plate),
                        state=[("field", "white")] + BLAST_TOP)
_BLAST_PARTS = dict(field=blast_field, shattered=blast_field_shattered, plate=blast_plate, hull=blast_hull,
                    prisms=blast_prisms)
CARDS += [dict(name="cavitation", element="Charge", before=ICON_DIR + "Scarab_CavitationBlast-PLACEHOLDER.png", before_rev="1ed330395",
               title="Cavitation Blast: the punch's volume to scale around the Scarab; as it lands the prisms inside shatter forward, "
                     "then it greys while recharging (Charge 5 Cavitation Shear shatters shielded prisms the same way)",
               parts=_BLAST_PARTS,
               states=[("ready", [("field", "white")] + BLAST_TOP),
                       ("punching (lit): what it holds shatters", [("shattered", "white")] + BLAST_TOP),
                       ("recharging", [("field", "white", 0.3), ("prisms", "blue"), ("hull", "domain"), ("plate", "white", 0.3)])])]

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/ScarabHUDVariant.prefab"
VIEW = "7983934951929746178"   # ScarabHUDView on the variant
REBIND += [
    # The Charge card: the icon Image becomes a transparent container over five parts, in draw order - the whole prisms
    # outside (blue), the volume and the shattered volume (white; one shown at a time), the plate (white), the hull
    # (domain). The ready / spent tints (orange / grey) on the icon itself are retired with it.
    dict(file=VARIANT, pair=dict(container="3855639277037614910", view=VIEW, after="blastIcon",
                                 children=[("BlastPrisms", "Scarab_BlastPrisms", "blastPrismsIcon"),
                                           ("BlastField", "Scarab_BlastField", "blastFieldIcon"),
                                           ("BlastFieldShattered", "Scarab_BlastFieldShattered", "blastShatteredIcon"),
                                           ("BlastPlate", "Scarab_BlastPlate", "blastPlateIcon"),
                                           ("BlastHull", "Scarab_BlastHull", "blastHullIcon")],
                                 fields={"blastReadyColor": None, "blastSpentColor": None})),
]
