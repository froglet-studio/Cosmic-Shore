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
    Mass=None,     # the Switch card, below
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


# ---- Mass: Switch (2026-10-28, S1 "thread") -------------------------------------------------------------------------
# The switch (SCARAB.md section 5, ScarabSwitch.cs): a bare low-poly RING in the pilot's domain prism material, planted
# 150 ahead along the course; ANY ball that threads its mouth pays the placer (a charge back, and a scarab-wing dais
# rises round the spent ring). Mass grows the ring: ringRadius 24 x switchScale 1 -> 2.5 (PlaceSwitchAction.asset).
# One round (S1 thread / S2 face-on / S3 pays); Garrett: "go with S1 and move on".
#
# The ring is seen at an angle (its plane across the path), a white ball through its mouth with its wake coming in
# from the left BEHIND the ring's near arc - the ball came through (left to right, LESSONS 55). Faceted like the toy
# rings (ToyFactory.AddSwitchRing). To scale (LESSONS 56), with the RING held at one bold size (LESSONS 58): Mass shows
# as the ball SHRINKING inside it - the mouth growing against the match ball it swallows (ball radius 7,
# AstroLeagueBall.prefab, scale 14 x 0.5). The HUD scales the ball part by 1 / switchScale about its own centre.
RING_R = 24.0
RING_MAX = 2.5
BALL_R = 7.0
RING_PX = 50.0            # the ring's radius on the card, FIXED
RING_SIDES = 12
RING_W = 10.0
RING_CX, RING_CY, RING_SQ = 70.0, 64.0, 0.5
BALL_CX = RING_CX + 4.0
CONTAINER_UNITS = 80.0    # the Mass icon's RectTransform (80 x 80) over the 128 px card


def ball_px(scale):
    return RING_PX * BALL_R / (RING_R * scale)       # Mass 1: 14.6 px; Mass 10 (x2.5): 5.8 px


def _ring_pts(cx, cy, r_px, squash, sides=RING_SIDES):
    return [(cx + r_px * squash * math.sin(2 * math.pi * (k + 0.5) / sides), cy - r_px * math.cos(2 * math.pi * (k + 0.5) / sides))
            for k in range(sides)]


import functools   # noqa: E402  (each part is built once: the knock-outs cost seconds)
import math   # noqa: E402  (the Mass section's trig)


def toy_grown(m, px):
    import toy_portraits
    return toy_portraits.grown(m, px)


@functools.lru_cache(None)
def _ring():
    """(near, far): the faceted ring split down its vertical axis - the left arc is the near side."""
    m = stroke(_ring_pts(RING_CX, RING_CY, RING_PX, RING_SQ), w=RING_W, closed=True)
    xs = (np.arange(N) / (N / 128.0))[None, :]
    near = m & (xs < RING_CX)
    return near, m & ~near


def switch_near():
    return _ring()[0]


@functools.lru_cache(None)
def _ball_at(cx, scale):
    return disc(cx, RING_CY, ball_px(scale))


def switch_ball(scale=1.0):
    """The ball where it sits on the card, the near arc knocked out of it."""
    return _ball_at(BALL_CX, scale) & ~toy_grown(switch_near(), 2.5)


def switch_ball_part():
    """The ball part as the HUD holds it: CENTRED on the card, so the view scales it about its own centre (the child
    is point-anchored at the ball's offset; REBIND below)."""
    return _ball_at(64.0, 1.0)


def switch_far():
    return _clear_of(_ring()[1], _ball_at(BALL_CX, 1.0))


@functools.lru_cache(None)
def switch_wake():
    """Three streaks from the left, behind the near arc, ending short of the SMALLEST ball (Mass 10) so the ball
    covers their ends at every size."""
    end = BALL_CX - ball_px(RING_MAX) - 3.0
    wake = empty()
    for dy, x0 in ((-7.0, 16.0), (0.0, 7.0), (7.0, 16.0)):     # inside the 116 px optical box
        y = RING_CY + dy
        wake |= fill([(x0, y - 2.5), (end, y - 2.5), (end, y + 2.5), (x0, y + 2.5)])
    return wake & ~toy_grown(switch_near(), 2.5)


def switch_thread():
    """The composite at Mass 1: the card's fallback and the fleet sheet's tile."""
    return switch_far() | switch_wake() | switch_ball() | switch_near()


SWITCH_EXEMPT = BLAST_EXEMPT
ICONS += [
    dict(name="Scarab_Switch", element="Mass",
         act="Switch: a white ball threading your ring left to right, its wake behind the near arc (composite; the HUD draws the parts)",
         draw=switch_thread, exempt={"components": "the ring's two arcs, the wake and the ball",
                                     "gap": "the knock-outs that put the near arc in front"}),
    dict(name="Scarab_SwitchNear", element="Mass", act="Switch: the ring's near arc (domain colour)", draw=switch_near,
         subdir="Parts", family=False, exempt=SWITCH_EXEMPT),
    dict(name="Scarab_SwitchFar", element="Mass", act="Switch: the ring's far arc (domain colour)", draw=switch_far,
         subdir="Parts", family=False, exempt=SWITCH_EXEMPT),
    dict(name="Scarab_SwitchWake", element="Mass", act="Switch: the ball's wake (white)", draw=switch_wake,
         subdir="Parts", family=False, exempt=SWITCH_EXEMPT),
    dict(name="Scarab_SwitchBall", element="Mass", act="Switch: the ball at Mass 1, centred (white; the HUD scales it by 1 / switchScale)",
         draw=switch_ball_part, subdir="Parts", family=False, exempt=SWITCH_EXEMPT),
]

SWITCH_LIT = [("far", "domain"), ("wake", "white"), ("ball", "white"), ("near", "domain")]
LOCKUP["Mass"] = dict(parts=dict(far=switch_far, wake=switch_wake, ball=switch_ball, near=switch_near), state=SWITCH_LIT)
CARDS += [dict(name="switch", element="Mass", before=ICON_DIR + "Scarab_Switch-PLACEHOLDER.png", before_rev="6127b5d6c",
               title="Switch: a white ball threading your ring; Mass grows the ring, so the ball shrinks inside it (to scale: "
                     "ring 24 -> 60, ball 7); no switch banked: dim, under the fleet's cooldown veil",
               parts=dict(far=switch_far, wake=switch_wake, near=switch_near, ball=switch_ball,
                          ball10=lambda: switch_ball(RING_MAX)),
               states=[("Mass 1: a switch banked", SWITCH_LIT),
                       ("Mass 10: the ring 2.5x", [("far", "domain"), ("wake", "white"), ("ball10", "white"), ("near", "domain")]),
                       ("no switch banked (recharging)", [(k, c, 0.3) for k, c in SWITCH_LIT])])]

_U = CONTAINER_UNITS / 128.0
REBIND += [
    # The Mass card: the icon Image becomes a transparent container over four parts, in draw order - the far arc
    # (domain), the wake (white), the ball (white; point-anchored at its own centre so the view scales it), the near
    # arc (domain). The charge-count colour ramp on the icon itself is retired with it.
    dict(file=VARIANT, pair=dict(container="6611959372928416812", view=VIEW, after="switchIcon",
                                 children=[("SwitchFar", "Scarab_SwitchFar", "switchFarIcon"),
                                           ("SwitchWake", "Scarab_SwitchWake", "switchWakeIcon"),
                                           ("SwitchBall", "Scarab_SwitchBall", "switchBallIcon",
                                            (round((BALL_CX - 64.0) * _U, 3), 0, CONTAINER_UNITS, CONTAINER_UNITS)),
                                           ("SwitchNear", "Scarab_SwitchNear", "switchNearIcon")],
                                 fields={"switchChargeColors": "[]"})),
]


# ---- Space: Ball Forge options (2026-10-28) -------------------------------------------------------------------------
# Ball Forge (SCARAB.md 4.1, SCARAB_CRYSTAL_MORPH.md): the skimmer touches an OMNI crystal and the crystal BECOMES a
# ball, in place, at rest - its own body carried onto the ball's hull - and the hull strikes it. Space grows the ball
# it mints: ScarabBallForge.BallSizeScale x1 -> x4, stamped at forge time. The ball wears the pilot's domain for life
# (4.2), so it is the domain colour; the crystal is white. LESSONS 59: the BALL is held at one bold size and Space shows
# as the crystal SHRINKING against it (1 / ball scale) - the ball a crystal makes grows.
FORGE_MAX = 4.0
BALL_PX = 30.0


def omni_crystal(cx, cy, h):
    """The omni crystal as a gem: a tall, narrow hexagonal bipyramid seen side on - long points, short shoulders -
    its centre seam and upper facet seams cut through it (a squat one reads as a cube)."""
    w = h * 0.3
    sh = h * 0.08
    body = fill([(cx, cy - h / 2), (cx + w, cy - sh), (cx + w, cy + sh), (cx, cy + h / 2),
                 (cx - w, cy + sh), (cx - w, cy - sh)])
    seam = max(1.6, h * 0.04)
    seams = (stroke([(cx, cy - h / 2 + seam * 2), (cx, cy + h / 2 - seam * 2)], w=seam)
             | stroke([(cx - w, cy - sh), (cx, cy + sh * 0.5), (cx + w, cy - sh)], w=seam))
    return body & ~seams


def _crystal_h(scale):
    return 2 * BALL_PX / scale          # Space 1: as tall as the ball is wide; Space 10 (x4): a quarter


@functools.lru_cache(None)
def f1(scale):
    """F1 BECOMES: the crystal at the left, a chevron, the ball it becomes at the right (left to right)."""
    crystal = omni_crystal(26.0, 64.0, _crystal_h(scale))
    chev = stroke([(46, 52), (56, 64), (46, 76)], w=6.0)
    ball = disc(88.0, 64.0, BALL_PX)
    return dict(crystal=crystal, chev=chev, ball=ball)


@functools.lru_cache(None)
def f2(scale):
    """F2 IN PLACE: the ball with the crystal it was standing in front of it, knocked out of it - the crystal becomes
    the ball where it stood."""
    ball = disc(64.0, 64.0, BALL_PX * 1.5)
    crystal = omni_crystal(64.0, 64.0, _crystal_h(scale) * 1.3)
    return dict(ball=_clear_of(ball, crystal, px=3.0), crystal=crystal)


@functools.lru_cache(None)
def f3(scale):
    """F3 THE HULL IT WEARS: the ball (domain) wearing the crystal's facets - seams cut across it in the crystal's
    pattern (SCARAB_CRYSTAL_MORPH: the crystal's body carried onto the ball's hull), and a white crystal beside it at
    the live Space size."""
    r = BALL_PX * 1.4
    cx = 74.0
    ball = disc(cx, 64.0, r)
    seam = 3.0
    facets = (stroke([(cx - r, 64 - r * 0.25), (cx, 64 - r * 0.05), (cx + r, 64 - r * 0.25)], w=seam)
              | stroke([(cx, 64 - r * 0.05), (cx, 64 + r)], w=seam)
              | stroke([(cx - r * 0.7, 64 - r * 0.7), (cx, 64 - r * 0.05), (cx + r * 0.7, 64 - r * 0.7)], w=seam))
    crystal = omni_crystal(18.0, 64.0, _crystal_h(scale) * 0.7)
    return dict(ball=ball & ~facets, crystal=crystal)


def _forge_card(name, title, fn, layers, lit):
    keys = {k for k, _c in layers}
    parts = {f"{k}{t}": (lambda k=k, sc=sc: fn(sc)[k]) for k in keys for t, sc in (("1", 1.0), ("10", FORGE_MAX))}
    at = lambda sfx: [(k + sfx, c) for k, c in layers]
    return dict(name=name, group="ballforge", lockup_state=0, element="Space",
                before=ICON_DIR + "Scarab_BallForge-PLACEHOLDER.png", title=title, parts=parts,
                states=[("Space 1", at("1")), ("Space 10: the ball 4x (the crystal smaller against it)", at("10")),
                        ("forging (lit)", [(k + "1", *rest) for k, *rest in lit])])


CARDS += [
    _forge_card("space_option_f1",
                "F1 - BECOMES: the omni crystal (white) at the left, a chevron, the ball it becomes in your colour; Space "
                "grows the ball, so the crystal shrinks against it (x1 -> x4)",
                f1, [("crystal", "white"), ("chev", "white"), ("ball", "domain")],
                [("crystal", "white", 0.4), ("chev", "white"), ("ball", "domain")]),
    _forge_card("space_option_f2",
                "F2 - IN PLACE: the ball in your colour with the crystal it was, cut out of it in white - the crystal "
                "becomes the ball where it stood",
                f2, [("ball", "domain"), ("crystal", "white")], [("ball", "domain"), ("crystal", "white", 0.4)]),
    _forge_card("space_option_f3",
                "F3 - THE HULL IT WEARS: the ball in your colour wearing the crystal's facets (the morph carries the "
                "crystal's body onto it), a white crystal beside it at the live Space size",
                f3, [("crystal", "white"), ("ball", "domain")], [("crystal", "white", 0.4), ("ball", "domain")]),
]
