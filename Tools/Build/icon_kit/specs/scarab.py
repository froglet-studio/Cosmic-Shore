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


def _half(front):
    """One half of the bubble, split at the START PLANE through the hull (a GROOVE-wide knock-out), the hull knocked
    out. The front half is the PUSH every shove claims; the back half is the PULL Charge 5 (Backdraft) adds - the
    plate's mirror image, dragging what is behind forward through the ship (2026-10-29)."""
    g = GROOVE / 2
    cut = bar(OX + g, 128, 0, 128) if front else bar(0, OX - g, 0, 128)
    return _clear_of(_bubble() & cut, blast_hull(), px=2.5)


def blast_field():
    """The push at rest / recharging: the front half of the volume, from the hull to the plate."""
    return _half(True)


def blast_field_back():
    """Backdraft (Charge 5): the pull - the volume's back half, behind the hull. Shown only while upgraded."""
    return _half(False)


def blast_field_shattered():
    """The push as it lands: the claimed prisms cut out of it as forward-flung slivers."""
    return blast_field() & ~(shards(84, 42) | shards(84, 86))


def blast_field_back_shattered():
    """The pull as it lands: the prisms behind cut out as slivers pointing FORWARD - dragged through the ship."""
    return blast_field_back() & ~(shards(38, 42) | shards(38, 86))


def blast_prisms():
    """Whole prisms outside the punch's reach (HUD blue)."""
    return _clear_of(prism(120, 64, 7.0) | prism(26, 13, 7.0) | prism(98, 115, 7.0), blast_plate())


def cavitation_blast():
    """The composite at rest (push only): the card's fallback and the fleet sheet's tile."""
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
    dict(name="Scarab_BlastFieldShattered", element="Charge", act="Shove: the push with the claimed prisms cut out as forward-flung slivers (white, as the punch lands)",
         draw=blast_field_shattered, subdir="Parts", family=False, exempt=BLAST_EXEMPT),
    dict(name="Scarab_BlastFieldBack", element="Charge", act="Shove, Charge 5 Backdraft: the pull, the volume's back half (white; grey while recharging)",
         draw=blast_field_back, subdir="Parts", family=False, exempt=BLAST_EXEMPT),
    dict(name="Scarab_BlastFieldBackShattered", element="Charge", act="Shove, Charge 5 Backdraft: the pull with the prisms behind cut out as slivers pointing forward",
         draw=blast_field_back_shattered, subdir="Parts", family=False, exempt=BLAST_EXEMPT),
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
_BLAST_PARTS = dict(field=blast_field, shattered=blast_field_shattered, back=blast_field_back,
                    backshattered=blast_field_back_shattered, plate=blast_plate, hull=blast_hull, prisms=blast_prisms)
CARDS += [dict(name="cavitation", element="Charge", before=ICON_DIR + "Scarab_CavitationBlast.png", before_rev="927dd7ab2",
               title="Shove (2026-10-29): the push to scale in front of the Scarab; Charge 5 Backdraft adds the pull behind it "
                     "(the mirrored half); as it lands the prisms inside shatter forward, then it greys while recharging",
               parts=_BLAST_PARTS,
               states=[("ready (push)", [("field", "white")] + BLAST_TOP),
                       ("ready, Charge 5 Backdraft (push + pull)", [("back", "white"), ("field", "white")] + BLAST_TOP),
                       ("Backdraft landing (lit)", [("backshattered", "white"), ("shattered", "white")] + BLAST_TOP),
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
                                           ("BlastHull", "Scarab_BlastHull", "blastHullIcon"),
                                           ("BlastFieldBack", "Scarab_BlastFieldBack", "blastFieldBackIcon"),
                                           ("BlastFieldBackShattered", "Scarab_BlastFieldBackShattered",
                                            "blastShatteredBackIcon")],
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


# ---- The kit change of 2026-10-29 (Garrett) --------------------------------------------------------------------------
# "The space ability should be the balls that are produced on the membrane. ball forge should be the omnicrystal ability
# and the drift ability should be displayed alongside the core 5 just like the squirrel does." So the Space card is
# NUCLEUS SEEDING (ScarabNucleusSeeder: balls of your domain studding the nucleus membrane, Space sizing them x1 -> x4),
# the Ball Forge is the OMNI crystal card's art (VesselHUDView.omniAbilitySprite; the card's upper plate already wears the
# crystal's emblem, so the lower plate is what the crystal BECOMES), and the Drift (LT, ScarabDriftAction) gets a core
# card. The F1/F2/F3 Ball Forge options of 2026-10-28 are retired with the Space slot they were drawn for.
OMNI_FBX = "Assets/_Models/OmniCrystalExport1_8-21-25.fbx"
OMNI_RING = 0.78        # face normals within this cosine of the view axis: the pentagon, its 5 squares, its 5 triangles
OMNI_GAP = 0.78         # each plate drawn at this fraction of itself about its own centre - the explosion, made legible


@functools.lru_cache(None)
def _omni_faces(ring=OMNI_RING):
    """The omni crystal's outer plate faces as seen down a pentagon's axis, in mesh units: the exploded
    rhombicosidodecahedron (author_omni_crystal_triangles.py - 12 pentagons, 30 squares, 20 triangles), its
    pentagon facing the viewer, as OmniCrystal_Active.png draws it."""
    polys = [np.asarray(q, dtype=float) for q in mesh_silhouette._polys(OMNI_FBX)]

    def normal(q):
        n = np.cross(q[1] - q[0], q[2] - q[0])
        n /= np.linalg.norm(n)
        return n if n @ q.mean(0) > 0 else -n

    outer = [q for q in polys if abs(normal(q) @ (q.mean(0) / np.linalg.norm(q.mean(0)))) > 0.97]
    axis = next(q.mean(0) for q in outer if len(q) == 5)
    axis = axis / np.linalg.norm(axis)
    u = np.cross([0.0, 0.0, 1.0] if abs(axis[2]) < 0.9 else [1.0, 0.0, 0.0], axis)
    u /= np.linalg.norm(u)
    v = np.cross(axis, u)
    faces = []
    for q in outer:
        if normal(q) @ axis < ring:
            continue
        c = q.mean(0)
        q = c + (q - c) * OMNI_GAP
        faces.append(np.stack([q @ u, q @ v], axis=1))
    span = max(np.abs(f).max() for f in faces)
    return [f / span for f in faces]          # unit radius


def omni_crystal(cx, cy, h, ring=OMNI_RING):
    """The OMNI crystal from its own mesh: the plates around one pentagon (the pentagon, its five squares and the five
    triangles between them), each plate separated from the next - the crystal's explosion is its identity. h is the
    mark's diameter; a lower `ring` takes more of the front hemisphere (0.0: all of it)."""
    out = empty()
    for f in _omni_faces(ring):
        out |= fill([(cx + x * h / 2, cy - y * h / 2) for x, y in f])
    return out



# Space: Nucleus Seeding. The nucleus membrane is a wide white cap across the card; a ball of the pilot's domain sits
# EMBEDDED in it (the seeder sinks it 40% of its radius, ScarabNucleusFieldConfig.embedSinkFraction) - the cap is drawn
# OVER the ball, so however big the ball the membrane hides what is sunk. Space grows the seeded ball x1 -> x4
# (ScarabBallForge.SizeScaleFor): the ball part is drawn at its x4 size and the HUD scales it by size / 4 about its own
# centre, which sits on the membrane, so the ball always reads as half-swallowed.
SEED_MAX = 4.0
SEED_R_MAX = 40.0                      # the ball's radius at Space 10 (x4); Space 1 is 10 px
SEED_SURFACE = 70.0                    # the membrane's top at the card's centre line
SEED_NUCLEUS_R = 120.0                 # the nucleus's radius on the card: a gentle arc
SEED_BALL_CY = SEED_SURFACE + 2.0      # the ball's centre: just under the surface
SEED_CONTAINER_UNITS = 80.0


def seed_membrane():
    """The nucleus: a disc so large only its top arc crosses the card - the membrane the balls are seeded in."""
    return disc(64.0, SEED_SURFACE + SEED_NUCLEUS_R, SEED_NUCLEUS_R) & bar(6, 122, 6, 122)


def seed_ball():
    """The seeded ball at Space 10 (x4), centred on the card (the HUD places and scales it)."""
    return disc(64.0, 64.0, SEED_R_MAX)


def _seed_ball_at(scale):
    r = SEED_R_MAX * scale / SEED_MAX
    return disc(64.0, SEED_BALL_CY, r)


def nucleus_seeding(scale=1.0):
    """The composite: the ball at the live Space size, its sunk half under the membrane (a knock-out between)."""
    ball = _seed_ball_at(scale)
    return _clear_of(ball, seed_membrane(), px=3.0) | seed_membrane()


SEED_EXEMPT = dict(BLAST_EXEMPT)
ICONS += [
    dict(name="Scarab_NucleusSeeding", element="Space",
         act="Nucleus Seeding: a ball of your domain embedded in the nucleus membrane (composite at Space 5; the HUD draws the parts)",
         draw=lambda: nucleus_seeding(2.5), exempt={"components": "the membrane and the ball", "gap": "the knock-out along the membrane"}),
    dict(name="Scarab_SeedMembrane", element="Space", act="Nucleus Seeding: the membrane (white)",
         draw=seed_membrane, subdir="Parts", family=False, exempt=SEED_EXEMPT),
    dict(name="Scarab_SeedBall", element="Space", act="Nucleus Seeding: the seeded ball at Space 10 (domain colour; the HUD scales it by size / 4)",
         draw=seed_ball, subdir="Parts", family=False, exempt=SEED_EXEMPT),
]
LOCKUP["Space"] = dict(parts=dict(ball=lambda: _seed_ball_at(1.0), membrane=seed_membrane),
                       state=[("ball", "domain"), ("membrane", "white")])
CARDS += [dict(name="seeding", element="Space", before=ICON_DIR + "Scarab_BallForge-PLACEHOLDER.png", before_rev="927dd7ab2",
               title="Nucleus Seeding (Space, 2026-10-29): a ball of your domain embedded in the nucleus membrane; Space grows "
                     "the balls you seed x1 -> x4 (Space 5 Core Breaker: they destroy super-shielded prisms)",
               parts=dict(ball1=lambda: _seed_ball_at(1.0), ball5=lambda: _seed_ball_at(2.5),
                          ball10=lambda: _seed_ball_at(SEED_MAX), membrane=seed_membrane),
               states=[("Space 1", [("ball1", "domain"), ("membrane", "white")]),
                       ("Space 5", [("ball5", "domain"), ("membrane", "white")]),
                       ("Space 10 (x4)", [("ball10", "domain"), ("membrane", "white")])])]
REBIND += [
    # The Space card: the old Ball Forge icon Image becomes the container over the ball (point-anchored at its centre on
    # the membrane, so the HUD can scale it) and the membrane drawn over it. The forge's energy ring and its tint retire
    # with the Space slot (the energy economy is off; a crystal always forges).
    dict(file=VARIANT, pair=dict(container="4990196459091587280", view=VIEW, after="energyRing",
                                 children=[("SeedBall", "Scarab_SeedBall", "seedBallIcon",
                                            (0, round((64.0 - SEED_BALL_CY) * SEED_CONTAINER_UNITS / 128.0, 3),
                                             SEED_CONTAINER_UNITS, SEED_CONTAINER_UNITS)),
                                           ("SeedMembrane", "Scarab_SeedMembrane", "seedMembraneIcon")],
                                 binding=dict(element=3, gauge=None),
                                 fields={"seedIcon": "{fileID: 4990196459091587280}", "energyRing": None, "ballIcon": None,
                                         "energyFillingColor": None, "energyReadyColor": None,
                                         "energyTweenDuration": None, "energyReadyPunchScale": None})),
    dict(file=VARIANT, deactivate="6877628874251343197"),
]


# The OMNI crystal card: Ball Forge. Its upper plate is the crystal's own emblem (the lockup draws it on every hull), so
# the lower plate is what the crystal BECOMES - the ball, wearing the crystal's plates (SCARAB_CRYSTAL_MORPH: the
# crystal's body carried onto the ball's hull). One white sprite; a forged ball is the omni ability and no element sizes
# it.
def ball_forge():
    r = 50.0
    return disc(64.0, 64.0, r) & omni_crystal(64.0, 64.0, 2 * r * 1.12, ring=0.0)


# The Drift core card (LT, ScarabDriftAction): the Scarab turned off its course with the course line streaking behind -
# a drift is the heading leaving the course (SCARAB.md 3.3). One white sprite; the base view presses it with LT.
DRIFT_ANGLE = 35.0


def scarab_drift():
    hull = mesh_silhouette.silhouette(FBX, box=82, cx=78.0, cy=64.0, roll=-DRIFT_ANGLE, **VIEW_PLAN)
    wake = empty()
    for x0, x1, dy in ((8, 46, 0.0), (14, 42, -14.0), (14, 42, 14.0)):
        wake |= stroke([(x0, 64 + dy), (x1, 64 + dy)], w=6.0)
    return _clear_of(wake, hull, px=3.0) | hull


ICONS += [
    dict(name="Scarab_BallForge", element="Space", act="Ball Forge (the OMNI crystal card): the ball the crystal becomes, wearing its plates",
         draw=ball_forge, subdir="Core", family=False,
         exempt={"components": "the crystal's plates", "gap": "the gaps between the plates", "specks": "the rim plates",
                 "thin": "the rim plates"}),
    dict(name="Scarab_Drift", element="Core", act="Drift: the Scarab turned off its course, the course streaking behind it",
         draw=scarab_drift, subdir="Core", family=False,
         exempt={"components": "the hull and three streaks", "gap": "the real mesh", "specks": "the mandibles"}),
]
REBIND += [
    # The Drift core card, cloned from the Time card's button (OverheatButton), and the omni card's Ball Forge art.
    dict(file=VARIANT, core=dict(view=VIEW, donor="2527741218184756275", name="Drift", ability=1, input=2,
                                 icon="Scarab_Drift", omni="Scarab_BallForge")),
]
