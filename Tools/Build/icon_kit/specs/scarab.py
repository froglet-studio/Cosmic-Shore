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
from canvas import N, chamfered_rect, disc, empty, fill, rotate, stroke
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


# ---- Mass: Switch options (2026-10-28) ------------------------------------------------------------------------------
# The switch (SCARAB.md section 5, ScarabSwitch.cs): a bare low-poly RING in the pilot's domain prism material, planted
# 150 ahead along the course; ANY ball that threads its mouth pays the placer (a charge back, and a scarab-wing dais
# rises round the spent ring). Mass grows the ring: ringRadius 24 x switchScale 1 -> 2.5 (PlaceSwitchAction.asset).
# To scale (LESSONS 56): ball radius 7 (AstroLeagueBall.prefab, scale 14 x 0.5), hull 10.6 long. One switch, recharged
# in 60 s (the fleet's cooldown veil on this card). The ring is seen at an angle - a tall ellipse, its plane across the
# ball's path - so a ball crossing left to right (LESSONS 55) passes THROUGH it: the ring's near arc drawn over the
# ball in a knock-out, its far arc behind. Faceted like the toy rings (ToyFactory.AddSwitchRing), not round.
RING_R = 24.0
RING_MAX = 2.5
BALL_R = 7.0
RING_PX = 50.0            # the ring's radius on the card, FIXED: one bold mass (LESSONS 58); Mass shows as the ball
                          # SHRINKING inside it - the mouth growing against the ball it has to swallow, to scale
RING_SIDES = 12
RING_W = 10.0


def ball_px(scale):
    return RING_PX * BALL_R / (RING_R * scale)       # Mass 1: 14.6 px; Mass 10 (x2.5): 5.8 px


def _ring_pts(cx, cy, r_px, squash, sides=RING_SIDES):
    return [(cx + r_px * squash * math.sin(2 * math.pi * (k + 0.5) / sides), cy - r_px * math.cos(2 * math.pi * (k + 0.5) / sides))
            for k in range(sides)]


def ring_halves(cx, cy, r_px, squash, w=RING_W):
    """(near, far): the faceted ring split down its vertical axis - the left arc is the near side."""
    m = stroke(_ring_pts(cx, cy, r_px, squash), w=w, closed=True)
    xs = (np.arange(N) / (N / 128.0))[None, :]
    near = m & (xs < cx)
    return near, m & ~near


import functools   # noqa: E402  (each part set is built once: the knock-outs cost seconds)
import math   # noqa: E402  (the Mass section's trig)


# S1: THREAD - the ring seen at an angle (its plane across the path), a white ball through its mouth with its wake
# coming in from the left BEHIND the ring's near arc: the ball came through. The ring is yours (domain).
@functools.lru_cache(None)
def s1(scale):
    cx, cy, sq = 70.0, 64.0, 0.5
    near, far = ring_halves(cx, cy, RING_PX, sq)
    rb = ball_px(scale)
    bx = cx + 4.0
    ball = disc(bx, cy, rb)
    wake = empty()
    for dy, x0 in ((-7.0, 14.0), (0.0, 4.0), (7.0, 14.0)):
        y = cy + dy
        wake |= fill([(x0, y - 2.5), (bx - rb - 3.0, y - 2.5), (bx - rb - 3.0, y + 2.5), (x0, y + 2.5)])
    near_k = toy_grown(near, 2.5)
    return dict(near=near, far=_clear_of(far, ball), ball=ball & ~near_k, wake=wake & ~near_k & ~toy_grown(ball, 2.0))


# S2: FACE-ON - the ring as the toy ring it is, square to the eye, the ball in its mouth.
@functools.lru_cache(None)
def s2(scale):
    ring = stroke(_ring_pts(64.0, 64.0, RING_PX, 1.0), w=RING_W, closed=True)
    return dict(ring=ring, ball=disc(64.0, 64.0, ball_px(scale)))


# S3: PAYS - S1 at Mass 1 and, as the ball threads, the dais's first wing pair and its sun rising off the ring's far
# side: the payout (a charge back, the rosette) the ring exists for.
@functools.lru_cache(None)
def s3_parts():
    cx, cy, sq = 40.0, 64.0, 0.5
    near, far = ring_halves(cx, cy, RING_PX * 0.86, sq)
    rb = ball_px(1.0) * 0.86
    ball = disc(cx + 3.0, cy, rb)
    sun = prism(106, 64, 11.0)
    wings = stroke([(cx + 24, 30), (86, 24), (106, 44)], w=7.0) | stroke([(cx + 24, 98), (86, 104), (106, 84)], w=7.0)
    return dict(near=near, far=_clear_of(far, ball), ball=ball & ~toy_grown(near, 2.5),
                wings=_clear_of(wings, sun, far, near), sun=sun)


def toy_grown(m, px):
    import toy_portraits
    return toy_portraits.grown(m, px)


def _scaled(fn, scale, key):
    return lambda: fn(scale)[key]


def _switch_card(name, title, parts, states):
    return dict(name=name, group="switch", lockup_state=0, element="Mass",
                before=ICON_DIR + "Scarab_Switch-PLACEHOLDER.png", title=title, parts=parts, states=states)


def _three(key1, key10, layers):
    """Mass 1 / Mass 10 / recharging rows over the same layer list."""
    def at(sfx, alpha=None):
        return [(k + sfx, c) if alpha is None else (k + sfx, c, alpha) for k, c in layers]
    return [("Mass 1: a switch banked", at(key1)), ("Mass 10: the mouth 2.5x (the ball smaller in it)", at(key10)),
            ("recharging (the veil)", at(key1, 0.3))]


CARDS += [
    _switch_card("mass_option_s1",
                 "S1 - THREAD: your ring at an angle, a white ball through its mouth, its wake coming in BEHIND the near "
                 "arc; Mass grows the mouth, so the ball shrinks inside it (to scale: ring 24 -> 60, ball 7)",
                 {f"{k}{t}": _scaled(s1, sc, k) for k in ("near", "far", "ball", "wake") for t, sc in (("1", 1.0), ("10", RING_MAX))},
                 _three("1", "10", [("far", "domain"), ("wake", "white"), ("ball", "white"), ("near", "domain")])),
    _switch_card("mass_option_s2",
                 "S2 - FACE-ON: the toy ring square to the eye, the ball in its mouth; Mass shrinks the ball inside it",
                 {f"{k}{t}": _scaled(s2, sc, k) for k in ("ring", "ball") for t, sc in (("1", 1.0), ("10", RING_MAX))},
                 _three("1", "10", [("ring", "domain"), ("ball", "white")])),
    _switch_card("mass_option_s3",
                 "S3 - PAYS: S1 at Mass 1, and as the ball threads, the dais's first wings and sun rise off the ring",
                 dict(near=lambda: s3_parts()["near"], far=lambda: s3_parts()["far"], ball=lambda: s3_parts()["ball"],
                      wings=lambda: s3_parts()["wings"], sun=lambda: s3_parts()["sun"]),
                 [("a switch banked", [("far", "domain"), ("ball", "white"), ("near", "domain")]),
                  ("threaded (lit): it pays", [("far", "domain"), ("ball", "white"), ("near", "domain"),
                                               ("wings", "white"), ("sun", "shielded")]),
                  ("recharging (the veil)", [("far", "domain", 0.3), ("ball", "white", 0.3), ("near", "domain", 0.3)])]),
]
