"""The Serpent's ability-row icons (Resources/ElementalAbilityMaps/Serpent.asset).

  Charge  Scope               LT opens a magnified eyepiece down the nose, one fixed 12 degree zoom; Charge sets the
                              SNIPER's recovery (12 s -> 5.4 s). Charge 5 HUNTER'S SIGHT: rivals leave afterimages.
  Mass    Seed Wall           stopping or cloaking leaves a seed that grows a herringbone wall from stolen prisms;
                              Mass sizes the bricks; Mass 5 LOCKDOWN seals each hole as its four bricks close
  Space   Cloak / Sniper      RT: cloak unscoped, the sniper shot scoped (one slot, two pictures); the round breaks
                              super-shields from the start; Space sets the wall's steal reach; Space 5 SHIELD STEAL
  Time    Solid Fuel Pellets  each press burns a quarter-tank pellet; overlapping burns stack; Time sets the burn length.
                              KEPT as the shipped art, mirrored to fly right (Garrett, 2026-10-30): PelletSlots.png is
                              author_serpent_pellet_icon.py's, Line_1..4 are flipped, so the kit draws nothing for it.

Before: three PLACEHOLDER sprites (author_hull_icon_placeholders.py) and the shipped pellet-slot art on Time. One card
at a time with Garrett, options in the hull's whole lockup.

The hull is the Serpent's own mesh (SerpentExport.fbx, Serpent.prefab) from straight above, nose RIGHT (pitch 0,
yaw 0: the round head at +x, the arms trailing - the grid on 2026-10-29).
"""
import math

import mesh_silhouette
from canvas import N, disc, empty, fill, ring, stroke
from specs.rhino import stella

import numpy as np
from PIL import Image

HULL = "Serpent"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Serpent/"
FBX = "Assets/_Models/Vessel Models/SerpentExport.fbx"
VIEW_PLAN = dict(pitch=0, yaw=0, dilate_px=0.8)


def _png_mask(rel):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def placeholder(name):
    return lambda: _png_mask(ICON_DIR + f"Serpent_{name}-PLACEHOLDER.png")


def serpent(box, cx, cy):
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, **VIEW_PLAN)


def _clear_of(mask, *others, px=2.5):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


ICONS = []
LOCKUP = dict(
    Charge=dict(parts=dict(i=placeholder("Scope")), state=[("i", "white")]),
    Mass=dict(parts=dict(i=placeholder("SeedWall")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("SniperShot")), state=[("i", "white")]),
    Time=dict(parts=dict(i=lambda: _png_mask("Assets/_Graphics/Design Assests/HUD UI/Serpent Fuel/PelletSlots.png")),
              state=[("i", "white")]),
)
CARDS = []
REBIND = []


# ---- The recut (Garrett, 2026-10-30) ------------------------------------------------------------------------------
# "Space should be the cloak and sniper shot abilities. the icon should change when the scope is held so the glyph can
# stay in the same spot. charge is the scope. ... Charge L5 upgrade is instead of pierce it causes the scope to produce
# short lived after images that the serpent can snipe instead. (much like hawkeye in marvel rivals)"
#   CHARGE = the Scope (LT): one fixed 12 degree zoom; Charge sets the sniper's recovery. L5 Hunter's Sight: while
#            scoped, rivals leave afterimages (ScopeAfterimages: one per 0.25 s, each standing 1 s) a round can strip.
#   SPACE  = Cloak / Sniper Shot (RT): TWO pictures in one slot, swapped by SerpentVesselHUDView.SetScoped - the cloak
#            unscoped, the sniper while the scope is held. The round takes every hostile prism in its cone, super-
#            shielded included, from the start.
CY = 64.0
BEAM_W = 4.5


def beam(x0, x1, cy=CY, w=BEAM_W):
    return fill([(x0, cy - w / 2), (x1, cy - w / 2), (x1, cy + w / 2), (x0, cy + w / 2)])


def brick(x, y, l=14.0, w=7.0):
    return fill([(x - l / 2, y - w / 2), (x + l / 2, y - w / 2), (x + l / 2, y + w / 2), (x - l / 2, y + w / 2)])


def outline(mask, px=2.5):
    """A solid's rim: the mask less itself eroded by px (the CloakGhost look - a Fresnel outline, no body)."""
    import toy_portraits
    return mask & toy_portraits.grown(~mask, px)


# --- the eyepiece (SniperScopeOverlay: the CHARGE petal, ScopePetalGeometry.Outline, apex down) and its reticle: the
# sniper cone (1.5 degree half-angle) at its true size for the fixed 12 degree zoom, tan(1.5) / tan(6) = 0.25 of the
# petal's half-side. The ring's radius is the measurement; stroke, posts and dot are widened to read at 128.
SCOPE_CONE = 1.5
SCOPE_FOV = 12.0
PETAL = [(0.5, 0.0), (1.0, 0.684356), (0.817204, 1.0), (0.182796, 1.0), (0.0, 0.684356)]
PETAL_SIDE = 112.0


def petal_pts(side=PETAL_SIDE, cx=64.0, cy=64.0):
    return [(cx + (u - 0.5) * side, cy + (0.5 - v) * side) for u, v in PETAL]


def eyepiece_frame(w=7.0):
    return stroke(petal_pts(), w=w, closed=True)


def ring_r(fov=SCOPE_FOV):
    return min(0.512, math.tan(math.radians(SCOPE_CONE)) / math.tan(math.radians(fov / 2))) * PETAL_SIDE / 2


def reticle(r, cx=64.0, cy=64.0, gap=4.0, arm=9.0, w=4.0):
    out = ring(cx, cy, r, w=w) | disc(cx, cy, 2.6)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        a, b = r + w / 2 + gap, r + w / 2 + gap + arm
        out |= stroke([(cx + dx * a, cy + dy * a), (cx + dx * b, cy + dy * b)], w=w)
    return out


# --- a rival and its afterimages: a plain dart (any hull), solid where it is, outlines where it was.
def dart(x, y, s=1.0):
    return fill([(x + 11 * s, y), (x - 8 * s, y - 8 * s), (x - 3 * s, y), (x - 8 * s, y + 8 * s)])


AFTER_XS = (40.0, 58.0, 76.0)       # oldest -> newest afterimage; the rival itself flies on, out of the frame's middle
RIVAL_X = 94.0
AFTER_Y = 54.0


def rival():
    return dart(RIVAL_X, AFTER_Y)


def afterimages():
    out = empty()
    for x in AFTER_XS:
        out |= outline(dart(x, AFTER_Y), px=2.2)
    return out


# ---- Charge: the Scope, and Hunter's Sight (L5) ---------------------------------------------------------------------
# A: THE EYEPIECE. The petal and the reticle at the 12 degree zoom's true size. Hunter's Sight: a rival flying through
# the eyepiece trailing three afterimages, the reticle on the oldest - the one the Serpent shoots.
def a_eyepiece_reticle():
    return reticle(ring_r(), cy=72.0) & fill(petal_pts())


def a_hunter_reticle():
    return reticle(ring_r() * 0.75, cx=AFTER_XS[0], cy=AFTER_Y, gap=2.5, arm=5.0, w=3.0) & fill(petal_pts())


def a_hunter_rival():
    return _clear_of(rival() & fill(petal_pts()), eyepiece_frame(), px=2.0)


def a_hunter_after():
    return _clear_of(afterimages(), a_hunter_reticle(), px=1.5)


# B: THE SIGHTLINE. The Serpent at the left and its 12 degree view. Hunter's Sight: the rival and its afterimages in it.
B_HULL = dict(box=40.0, cx=22.0, cy=64.0)


def b_hull():
    return serpent(**B_HULL)


def b_wedge(fov=SCOPE_FOV):
    x0 = B_HULL["cx"] + B_HULL["box"] / 2 - 2
    t = math.tan(math.radians(fov / 2))
    out = empty()
    for sgn in (1, -1):
        out |= stroke([(x0, 64.0), (124.0, 64.0 - sgn * (124.0 - x0) * t)], w=4.5)
    return _clear_of(out, b_hull(), px=2.0)


def _b_shift(mask):
    from canvas import shift
    return shift(mask, 4.0, 64.0 - AFTER_Y)


def b_rival():
    return _clear_of(_b_shift(rival()), b_wedge(), px=1.5)


def b_after():
    return _clear_of(_b_shift(afterimages()), b_wedge(), px=1.5)


# Garrett picked A (2026-10-31). The card is parts so the HUD can show Hunter's Sight at Charge 5: below it the frame and
# the centred reticle; at it the reticle moves onto the oldest afterimage (danger) with the rival and its afterimages.
CARDS += [
    dict(name="scope", element="Charge", before=ICON_DIR + "Serpent_Scope-PLACEHOLDER.png",
         title="Scope (Charge, picked A): the Charge-petal eyepiece and the reticle at the 12 deg zoom's true size; "
               "Charge 5 Hunter's Sight: a rival trailing afterimages, the reticle on the oldest",
         parts=dict(frame=eyepiece_frame, ret=a_eyepiece_reticle, hret=a_hunter_reticle, rival=a_hunter_rival,
                    after=a_hunter_after),
         states=[("Charge 1-4: the scope", [("frame", "white"), ("ret", "white")]),
                 ("Charge 5 Hunter's Sight", [("frame", "white"), ("after", "white", 0.6), ("rival", "white"),
                                              ("hret", "danger")])]),
]


# ---- Space: Cloak / Sniper Shot, one slot and two pictures ----------------------------------------------------------
# Unscoped the right trigger cloaks: the Serpent leaves its super-shielded seed and flies on as an outline (what the
# cloaked pilot sees, CloakGhost). Scoped it fires: the round's line clears every brick AND the super-shielded prism.
# Both pictures keep the same footprint so the trigger glyph never moves.
def cloak_hull():
    return outline(serpent(box=58.0, cx=74.0, cy=64.0), px=3.0)


def cloak_seed():
    return stella(26.0, 64.0, 18.0)


def cloak_trail():
    out = empty()
    for x in (44.0, 52.0):
        out |= disc(x, 64.0, 2.4)
    return out


SN_COLS = (14.0, 34.0, 54.0, 74.0, 94.0, 114.0)
SN_ROWS = (28.0, 46.0, 64.0, 82.0, 100.0)


def sniper_block():
    out = empty()
    for j, y in enumerate(SN_ROWS):
        if y == CY:
            continue
        for x in SN_COLS:
            out |= brick(x + (10.0 if j % 2 else 0.0), y, l=16.0, w=8.0)
    return out & fill([(4, 18), (124, 18), (124, 110), (4, 110)])


def sniper_line():
    return _clear_of(beam(2.0, 126.0, w=3.0), stella(64.0, CY, 16.0), px=3.0)


def sniper_stella():
    return stella(64.0, CY, 16.0, broken=True)


def sniper_shooter_hull():
    return serpent(box=40.0, cx=22.0, cy=CY)


def sniper_shooter_beam():
    return _clear_of(beam(44.0, 126.0), sniper_shooter_stella(), px=2.0)


def sniper_shooter_stella():
    return stella(96.0, CY, 18.0, broken=True)


def sniper_shooter_shards():
    out = empty()
    for x in (60.0, 76.0, 116.0):
        for dx, dy, r in ((-4.0, -5.0, 2.6), (3.0, 4.5, 2.2), (7.0, -2.5, 1.8)):
            cx, cy = x + dx, CY + dy
            out |= fill([(cx - r, cy), (cx, cy - r), (cx + r, cy), (cx, cy + r)])
    return out


# Garrett picked B (2026-10-31). Both pictures are parts in the one slot; SerpentVesselHUDView.SetScoped shows one set.
CARDS += [
    dict(name="cloaksniper", element="Space", before=ICON_DIR + "Serpent_SniperShot-PLACEHOLDER.png",
         title="Cloak / Sniper Shot (Space, picked B): unscoped, the Serpent's cloaked outline leaving its super-shielded "
               "seed; scope held, its round clearing bricks and the super-shielded prism on its line",
         parts=dict(hull=cloak_hull, seed=cloak_seed, trail=cloak_trail, shull=sniper_shooter_hull,
                    beam=sniper_shooter_beam, star=sniper_shooter_stella, shards=sniper_shooter_shards),
         states=[("unscoped: CLOAK", [("seed", "shielded"), ("trail", "domain", 0.5), ("hull", "domain")]),
                 ("scope held: SNIPER", [("beam", "white"), ("shards", "blue"), ("star", "shielded"),
                                         ("shull", "domain")])]),
]


# ---- Mass: Seed Wall, and the OMNI crystal card (2026-10-29) -------------------------------------------------------
# Garrett, 2026-10-29: "use the actual dimensions of the wall. only show the shielded prisms in the omnicrystal icon.
# use the base prisms and danger prisms to denote the upgrade with their actual positioning and scales." Every number
# is SerpentWallLattice's at rest (SeedWallAction.asset): short side 3, bricks 3 x 6 (aspect 2) on a square lattice at
# pitch 5 x short = 15, long axis UP where i + j is even and RIGHT where it is odd; a shielded brick is the rhombus with
# semi-axes 3 x its half-extents (9 along the long axis, 4.5 across); a Lockdown panel is the tilted square in the cell
# between four bricks (CellPanel: side 10.6 = 2 x (apothem 6.04 - clearance 0.75), turned atan(1/2) = 26.6 degrees in
# an even cell, the mirror in an odd one). Mass scales all of it together, so the card's proportions hold at any level.
# The Mass card shows the wall as it is BUILT (base bricks, the pilot's domain) and Lockdown as the danger panels in
# every closed hole; the omni card shows what a crystal does to it - every brick shielded, nothing else.
WALL_SHORT = 3.0
WALL_ASPECT = 2.0
WALL_REACH = 3.0                       # OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE
WALL_PITCH = (WALL_REACH * (WALL_ASPECT + 1) * 0.5 + 0.5) * WALL_SHORT      # 15
LOCK_CLEARANCE = 0.25 * WALL_SHORT
WALL_SITES = [(i, j) for j in (1, 0, -1) for i in (-1, 0, 1)]
WALL_CELLS = [(i, j) for j in (0, -1) for i in (-1, 0)]


def _wall_px(k):
    return lambda x, y: (64.0 + x * k, 64.0 - y * k)


def _up(i, j):
    return (i + j) % 2 == 0


def _brick(i, j, k):
    P = _wall_px(k)
    cx, cy = i * WALL_PITCH, j * WALL_PITCH
    hx, hy = (WALL_SHORT / 2, WALL_SHORT) if _up(i, j) else (WALL_SHORT, WALL_SHORT / 2)
    return fill([P(cx - hx, cy - hy), P(cx + hx, cy - hy), P(cx + hx, cy + hy), P(cx - hx, cy + hy)])


def _rhombus(i, j, k):
    """A SHIELDED brick: the old rhombus cut in half along its long axis, two close triangles (Garrett, 2026-11-01: never
    the rhombus for a prism; the Time petal owns it)."""
    P = _wall_px(k)
    cx, cy = i * WALL_PITCH, j * WALL_PITCH
    hl, hs = WALL_REACH * WALL_SHORT * WALL_ASPECT / 2, WALL_REACH * WALL_SHORT / 2
    a, b = ((0, hl), (hs, 0)) if _up(i, j) else ((hl, 0), (0, hs))
    g = 0.16 * hs                                     # half the cut, in wall units, along the short axis
    bn = (b[0] / hs * g, b[1] / hs * g)
    out = None
    for sg in (1, -1):
        ox, oy = cx + sg * bn[0], cy + sg * bn[1]
        tri = fill([P(ox + a[0], oy + a[1]), P(ox + sg * b[0], oy + sg * b[1]), P(ox - a[0], oy - a[1])])
        out = tri if out is None else out | tri
    return out


def _panel(i, j, k):
    P = _wall_px(k)
    pitch = WALL_PITCH
    apothem = ((WALL_ASPECT + 1) * pitch - WALL_REACH * WALL_ASPECT * WALL_SHORT) / (2 * math.sqrt(WALL_ASPECT ** 2 + 1))
    side = 2 * (apothem - LOCK_CLEARANCE)
    tilt = math.degrees(math.atan2(1, WALL_ASPECT))
    ang = math.radians(tilt if (i + j) % 2 == 0 else 90 - tilt)
    cx, cy = (i + 0.5) * pitch, (j + 0.5) * pitch
    pts = []
    for dx, dy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        x, y = dx * side / 2, dy * side / 2
        pts.append(P(cx + x * math.cos(ang) - y * math.sin(ang), cy + x * math.sin(ang) + y * math.cos(ang)))
    return fill(pts)


# The Mass card: nine sites (the seed at the centre), 3.2 px a unit, so the wall's 36 units fill the card edge to edge.
MASS_K = 3.5


def mass_bricks():
    m = empty()
    for i, j in WALL_SITES:
        if (i, j) != (0, 0):
            m |= _brick(i, j, MASS_K)
    return m


def mass_seed():
    return _brick(0, 0, MASS_K)


def mass_panels():
    m = empty()
    for i, j in WALL_CELLS:
        m |= _panel(i, j, MASS_K)
    return m


def seed_wall():
    """The composite at Mass 5: the base wall and every closed hole sealed."""
    return mass_bricks() | mass_seed() | mass_panels()


# The omni card: the same nine sites, every one SHIELDED (the rhombi nearly touch, half a short side of air between
# them); 2.4 px a unit so the 48 units the shields span fill the box.
OMNI_K = 2.4


def wall_shielded():
    m = empty()
    for i, j in WALL_SITES:
        m |= _rhombus(i, j, OMNI_K)
    return m


WALL_EXEMPT = {"at40": "a part: the HUD draws the wall whole; the bricks are their real size", "components": "the wall's bricks", "gap": "the lattice's real spacing", "coverage": "the base wall is sparse",
               "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}
ICONS += [
    dict(name="Serpent_SeedWall", element="Mass",
         act="Seed Wall / Lockdown: the wall's base bricks round the seed at their real size and spacing, every closed hole "
             "sealed with its danger panel (composite; the HUD shows the panels only at Mass 5)",
         draw=seed_wall, exempt={"components": "the wall's bricks and panels", "gap": "the lattice's real spacing",
                                 "in_box": "the wall runs to the card's edge at its real spacing",
                                 "coverage": "the panels are their real size: the sealed wall is dense"}),
    dict(name="Serpent_SeedWallBricks", element="Mass", act="Seed Wall: the base bricks (domain colour)", draw=mass_bricks,
         subdir="Parts", family=False, exempt=WALL_EXEMPT),
    dict(name="Serpent_SeedWallSeed", element="Mass", act="Seed Wall: the seed, the centre brick (white)", draw=mass_seed,
         subdir="Parts", family=False, exempt=WALL_EXEMPT),
    dict(name="Serpent_SeedWallPanels", element="Mass", act="Lockdown: the danger panels in the four closed holes (danger)",
         draw=mass_panels, subdir="Parts", family=False, exempt=WALL_EXEMPT),
    dict(name="Serpent_WallShield", element="Mass", act="The OMNI crystal card: the wall with every brick shielded",
         draw=wall_shielded, subdir="Core", family=False,
         exempt={"components": "the wall's shielded bricks", "gap": "the half short side of air between shields"}),
]
LOCKUP["Mass"] = dict(parts=dict(b=mass_bricks, s=mass_seed), state=[("b", "domain"), ("s", "white")])
CARDS += [
    dict(name="seedwall", element="Mass", before=ICON_DIR + "Serpent_SeedWall-PLACEHOLDER.png",
         title="Seed Wall (Mass): the wall's base bricks round the seed at real size and spacing; Mass 5 Lockdown seals a "
               "hole with its danger panel as its fourth brick closes",
         parts=dict(bricks=mass_bricks, seed=mass_seed, panels=mass_panels),
         states=[("Mass 1-4: the wall as built", [("bricks", "domain"), ("seed", "white")]),
                 ("Mass 5 Lockdown: closed holes sealed", [("panels", "danger"), ("bricks", "domain"), ("seed", "white")])]),
    dict(name="omni", element="Omni", before=ICON_DIR + "Serpent_SeedWall-PLACEHOLDER.png",
         title="The OMNI crystal card: a crystal beams to the wall and shields every brick - the shielded wall, nothing else",
         parts=dict(wall=wall_shielded), states=[("a crystal collected", [("wall", "white")])]),
]

# The Serpent's view lives on the VESSEL (Serpent.prefab) and reaches the HUD variant's Images through stripped proxies
# (author_hull_ability_rows.py): the parts go into the variant under the Mass icon (a `pair` with no view), then a
# `proxy` step points the view's fields at them and sets the omni card's art.
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/SerpentHUDVariant.prefab"
VARIANT_GUID = "af7b78e6bb3a8394f88f24bb0e31667a"
VESSEL = "Assets/_Prefabs/Spacevessels/Serpent.prefab"
VESSEL_HUD_INSTANCE = 4033090122440962496
VIEW = "5470374086122723936"           # SerpentVesselHUDView on Serpent.prefab
MASS_ICON = "7710000000000002025"      # the Mass slot's icon Image in the variant (author_hull_ability_rows.slot_ids)
REBIND += [
    dict(file=VARIANT, pair=dict(container=MASS_ICON,
                                 children=[("SeedWallPanels", "Serpent_SeedWallPanels", None),
                                           ("SeedWallBricks", "Serpent_SeedWallBricks", None),
                                           ("SeedWallSeed", "Serpent_SeedWallSeed", None)])),
    dict(file=VESSEL, proxy=dict(view=VIEW, instance=VESSEL_HUD_INSTANCE, source_file=VARIANT, source_guid=VARIANT_GUID,
                                 after="pipEmptyColor",
                                 refs=[("seedWallPanelsIcon", MASS_ICON, "SeedWallPanels"),
                                       ("seedWallBricksIcon", MASS_ICON, "SeedWallBricks"),
                                       ("seedWallSeedIcon", MASS_ICON, "SeedWallSeed")],
                                 fields={"omniAbilitySprite": ("icon", "Serpent_WallShield")})),
]


# ---- The Scope and Cloak / Sniper cards in the game (picked 2026-10-31) --------------------------------------------
PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}


def _part(name, element, act, draw):
    return dict(name=name, element=element, act=act, draw=draw, subdir="Parts", family=False, exempt=PART_EXEMPT)


ICONS += [
    _part("Serpent_ScopeFrame", "Charge", "Scope: the eyepiece (white)", eyepiece_frame),
    _part("Serpent_ScopeReticle", "Charge", "Scope: the centred reticle (white; hidden at Charge 5)", a_eyepiece_reticle),
    _part("Serpent_ScopeAfterimages", "Charge", "Hunter's Sight: the rival's afterimages (white, 60%)", a_hunter_after),
    _part("Serpent_ScopeRival", "Charge", "Hunter's Sight: the rival (white)", a_hunter_rival),
    _part("Serpent_ScopeHunterReticle", "Charge", "Hunter's Sight: the reticle on the oldest afterimage (danger)",
          a_hunter_reticle),
    _part("Serpent_CloakHull", "Space", "Cloak: the cloaked outline (domain)", cloak_hull),
    _part("Serpent_CloakTrail", "Space", "Cloak: the trail (domain, 50%)", cloak_trail),
    _part("Serpent_CloakSeed", "Space", "Cloak: the super-shielded seed (shielded)", cloak_seed),
    _part("Serpent_SniperHull", "Space", "Sniper: the Serpent (domain)", sniper_shooter_hull),
    _part("Serpent_SniperBeam", "Space", "Sniper: the round's line (white)", sniper_shooter_beam),
    _part("Serpent_SniperShards", "Space", "Sniper: the cleared bricks (blue)", sniper_shooter_shards),
    _part("Serpent_SniperStar", "Space", "Sniper: the super-shielded prism, broken (shielded)", sniper_shooter_stella),
]
LOCKUP["Charge"] = dict(parts=dict(f=eyepiece_frame, r=a_eyepiece_reticle), state=[("f", "white"), ("r", "white")])
LOCKUP["Space"] = dict(parts=dict(h=cloak_hull, t=cloak_trail, s=cloak_seed),
                       state=[("s", "shielded"), ("t", "domain", 0.5), ("h", "domain")])

CHARGE_ICON = "7710000000000002015"    # the Charge slot's icon Image in the variant
SPACE_ICON = "7710000000000002035"     # the Space slot's icon Image in the variant
SCOPE_PARTS = [("ScopeFrame", "Serpent_ScopeFrame", None), ("ScopeReticle", "Serpent_ScopeReticle", "scopeReticleIcon"),
               ("ScopeAfterimages", "Serpent_ScopeAfterimages", "scopeAfterimagesIcon"),
               ("ScopeRival", "Serpent_ScopeRival", "scopeRivalIcon"),
               ("ScopeHunterReticle", "Serpent_ScopeHunterReticle", "scopeHunterReticleIcon")]
CLOAKSNIPER_PARTS = [("CloakTrail", "Serpent_CloakTrail", "cloakTrailIcon"),
                     ("CloakSeed", "Serpent_CloakSeed", "cloakSeedIcon"),
                     ("CloakHull", "Serpent_CloakHull", "cloakHullIcon"),
                     ("SniperShards", "Serpent_SniperShards", "sniperShardsIcon"),
                     ("SniperBeam", "Serpent_SniperBeam", "sniperBeamIcon"),
                     ("SniperStar", "Serpent_SniperStar", "sniperStarIcon"),
                     ("SniperHull", "Serpent_SniperHull", "sniperHullIcon")]
REBIND += [
    dict(file=VARIANT, pair=dict(container=CHARGE_ICON, children=[(n, i, None) for n, i, _ in SCOPE_PARTS])),
    dict(file=VARIANT, pair=dict(container=SPACE_ICON, children=[(n, i, None) for n, i, _ in CLOAKSNIPER_PARTS])),
    dict(file=VESSEL, proxy=dict(view=VIEW, instance=VESSEL_HUD_INSTANCE, source_file=VARIANT, source_guid=VARIANT_GUID,
                                 after="pipEmptyColor",
                                 refs=[(f, CHARGE_ICON, n) for n, _, f in SCOPE_PARTS if f]
                                      + [(f, SPACE_ICON, n) for n, _, f in CLOAKSNIPER_PARTS]))
]
