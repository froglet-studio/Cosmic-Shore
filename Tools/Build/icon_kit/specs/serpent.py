"""The Serpent's ability-row icons (Resources/ElementalAbilityMaps/Serpent.asset).

  Charge  Sniper Shot         scoped, RT fires one hitscan round down the scope's line that destroys what it hits,
                              SUPER-SHIELDED mass included; Charge sets the recovery (12 s at rest -> 5.4 s at Charge 10).
                              Charge 5 PIERCE: the round carries through up to three prisms.
  Mass    Seed Wall           stopping or cloaking leaves a seed that grows a herringbone wall from stolen prisms;
                              Mass sizes the bricks; Mass 5 LOCKDOWN seals the holes with danger panels on a crystal
  Space   Scope               LT opens a magnified eyepiece down the nose; Space sets the magnification; Space 5 DEEP
                              FOCUS one more step (and the walls steal shielded prisms)
  Time    Solid Fuel Pellets  each press burns a quarter-tank pellet; overlapping burns stack; Time sets the burn length

Before: three PLACEHOLDER sprites (author_hull_icon_placeholders.py) and the shipped pellet-slot art on Time. One card
at a time with Garrett, options in the hull's whole lockup.

The hull is the Serpent's own mesh (SerpentExport.fbx, Serpent.prefab) from straight above, nose RIGHT (pitch 0,
yaw 0: the round head at +x, the arms trailing - the grid on 2026-10-29).
"""
import math

import mesh_silhouette
from canvas import N, chamfered_rect, disc, empty, fill, ring, stroke
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
    Charge=dict(parts=dict(i=placeholder("SniperShot")), state=[("i", "white")]),
    Mass=dict(parts=dict(i=placeholder("SeedWall")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("Scope")), state=[("i", "white")]),
    Time=dict(parts=dict(i=lambda: _png_mask("Assets/_Graphics/Design Assests/HUD UI/Serpent Fuel/PelletSlots.png")),
              state=[("i", "white")]),
)
CARDS = []
REBIND = []


# ---- Charge: Sniper Shot options (2026-10-29) ----------------------------------------------------------------------
# WHO gains WHAT against WHAT: the Serpent, from range, deletes ONE thing nothing else but the Rhino's energised blade
# can touch - a SUPER-SHIELDED prism (the game's stella octangula, StellatedOctahedronMeshGenerator, drawn by the
# Rhino's spec). The round is hitscan: a straight white line, there and gone. Charge 5 PIERCE carries it through three.
# The recovery (12 s -> 5.4 s) is the fleet's cooldown veil, which the HUD already sweeps (SerpentHUDController).
# States: ready (the target whole, no round), fired (the round and the target shattered), recharging (the veil), and
# Pierce fired (three shattered). Super-shielded is drawn in the "shielded" colour, as the Rhino's Space card does.
CY = 64.0
BEAM_W = 4.5


def beam(x0, x1, cy=CY, w=BEAM_W):
    return fill([(x0, cy - w / 2), (x1, cy - w / 2), (x1, cy + w / 2), (x0, cy + w / 2)])


# A: SHOOTER. The Serpent small at the left, nose right; the round from its nose to the prism at the right.
A_HULL = dict(box=50.0, cx=28.0, cy=CY)
A_PRISM = (94.0, CY, 42.0)


def a_hull():
    return serpent(**A_HULL)


def a_prism(broken=False):
    x, y, r = A_PRISM
    return _clear_of(stella(x, y, r * (0.78 if broken else 1.0), broken=broken), a_beam(), a_hull())


def a_beam():
    return _clear_of(beam(A_HULL["cx"] + A_HULL["box"] / 2 + 2, 124.0), a_hull(), px=2.0)


def a_pierce():
    out = empty()
    for x in (66.0, 88.0, 110.0):
        out |= stella(x, CY, 22.0 * 0.78, broken=True)
    return _clear_of(out, a_beam(), a_hull())


# B: THE ROUND. No hull: the round itself, a long slug with its tracer, punching the prism apart (the act, close up).
def b_round():
    slug = fill([(16.0, CY - 7), (44.0, CY - 7), (54.0, CY), (44.0, CY + 7), (16.0, CY + 7)])
    tracer = empty()
    for dy, x0 in ((-15.0, 6.0), (15.0, 6.0)):
        tracer |= stroke([(x0, CY + dy), (32.0, CY + dy)], w=5.0)
    return slug | tracer


def b_prism(broken=False):
    return _clear_of(stella(92.0, CY, 50.0 * (0.8 if broken else 1.0), broken=broken), b_round(), px=3.0)


def b_through():
    return _clear_of(beam(52.0, 124.0, w=4.0), b_round(), px=2.0)


def b_pierce():
    out = empty()
    for x in (74.0, 96.0, 118.0):
        out |= stella(x, CY, 22.0 * 0.8, broken=True)
    return _clear_of(out, b_round(), b_through())


# C: THE HOLE. The prism big in the centre, the round's line through it as negative space - a clean hole punched clean
# through a shield nothing else can open. Pierce: three prisms in a row, the line through all three.
def c_line():
    return beam(4.0, 124.0, w=6.0)


def c_prism(holed=False):
    s = stella(64.0, CY, 70.0)
    return _clear_of(s, c_line(), px=3.0) if holed else s


def c_pierce():
    out = empty()
    for x in (24.0, 64.0, 104.0):
        out |= stella(x, CY, 32.0)
    return _clear_of(out, c_line(), px=3.0)


# (state, which parts)
def _card(key, title, parts, states):
    return dict(name=f"snipershot_{key}", group="snipershot", lockup_state=1, element="Charge",
                before=ICON_DIR + "Serpent_SniperShot-PLACEHOLDER.png", title=title, parts=parts, states=states)


CARDS += [
    _card("a", "A  Shooter: the Serpent at the left, its hitscan round to a super-shielded prism that shatters; "
               "Pierce: three shattered on the line",
          dict(hull=a_hull, beam=a_beam, whole=lambda: a_prism(False), broken=lambda: a_prism(True), pierce=a_pierce),
          [("ready: the target whole", [("whole", "shielded"), ("hull", "domain")]),
           ("fired: the round, the sun shattered", [("beam", "white"), ("broken", "shielded"), ("hull", "domain")]),
           ("Charge 5 Pierce: three in one shot", [("beam", "white"), ("pierce", "shielded"), ("hull", "domain")])]),
    _card("b", "B  The round: the slug and its tracer punching a super-shielded prism apart, close up; Pierce: on through three",
          dict(round=b_round, through=b_through, whole=lambda: b_prism(False), broken=lambda: b_prism(True), pierce=b_pierce),
          [("ready: the target whole", [("round", "white"), ("whole", "shielded")]),
           ("fired: the sun shattered", [("round", "white"), ("broken", "shielded")]),
           ("Charge 5 Pierce: three in one shot", [("round", "white"), ("through", "white"), ("pierce", "shielded")])]),
    _card("c", "C  The hole: a super-shielded prism with the round's line punched clean through it as negative space; "
               "Pierce: through three",
          dict(whole=lambda: c_prism(False), holed=lambda: c_prism(True), pierce=c_pierce),
          [("ready: the target whole", [("whole", "shielded")]),
           ("fired: holed clean through", [("holed", "shielded")]),
           ("Charge 5 Pierce: three in one shot", [("pierce", "shielded")])]),
]


# ---- Space: Scope options (2026-10-29) ----------------------------------------------------------------------------
# WHO gains WHAT: the Serpent sees FARTHER - LT opens an eyepiece down its own nose, magnified by the trigger's depth.
# Every number is the shipped instrument's (SniperScopeOverlay, SniperScopeActionSO): the eyepiece is the CHARGE
# petal (ScopePetalGeometry.Outline, apex at the flower's centre, so pointing DOWN on screen), and the reticle ring is
# the sniper cone (1.5 degree half-angle) at its true size inside it, tan(1.5) / tan(fov / 2) of the petal's half-side,
# capped where the posts would leave the petal (0.51). Full zoom at rest is fov 22, Space 10 is 11, and Deep Focus
# multiplies the zoom depth by 1.6: 13.8 at rest, 6.9 at Space 10. The unscoped end is the player's own fov (60 here).
# The ring's RADIUS is the measurement; its stroke, the posts' gap and length and the dot are widened to read at 128.
SCOPE_CONE = 1.5
PETAL = [(0.5, 0.0), (1.0, 0.684356), (0.817204, 1.0), (0.182796, 1.0), (0.0, 0.684356)]
PETAL_SIDE = 112.0
FOV_FLIGHT, FOV_REST, FOV_DEEP, FOV_DEEP10 = 60.0, 22.0, 13.75, 6.875


def scope_ring01(fov):
    return min(0.512, math.tan(math.radians(SCOPE_CONE)) / math.tan(math.radians(fov / 2)))


def petal_pts(side=PETAL_SIDE, cx=64.0, cy=64.0):
    return [(cx + (u - 0.5) * side, cy + (0.5 - v) * side) for u, v in PETAL]


def eyepiece_frame(w=7.0):
    return stroke(petal_pts(), w=w, closed=True)


def reticle(r, cx=64.0, cy=64.0, gap=4.0, arm=9.0, w=4.0):
    out = ring(cx, cy, r, w=w) | disc(cx, cy, 2.6)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        a, b = r + w / 2 + gap, r + w / 2 + gap + arm
        out |= stroke([(cx + dx * a, cy + dy * a), (cx + dx * b, cy + dy * b)], w=w)
    return out


# A: THE EYEPIECE. The petal frame and the cone reticle at its true size for the zoom: the ring grows as you zoom.
def a_scope(fov):
    return reticle(max(3.5, scope_ring01(fov) * PETAL_SIDE / 2)) & fill(petal_pts())


# B: THE VIEW. The same petal, and a super-shielded prism seen through it at the size the zoom makes it: apparent size
# goes as 1 / tan(fov / 2), so Deep Focus draws it 1.6 times larger. Its size at rest is chosen; the ratios are not.
def b_target(fov):
    r = 20.0 * math.tan(math.radians(FOV_REST / 2)) / math.tan(math.radians(fov / 2))
    return _clear_of(stella(64.0, 60.0, r), eyepiece_frame(), px=3.0)


# C: THE REACH. The Serpent at the left, nose right, and its view wedge at the true field of view: the flight view's
# 60 degrees, full zoom's 22, Deep Focus's 13.8. The narrower the wedge, the farther it sees.
C_HULL = dict(box=44.0, cx=24.0, cy=64.0)


def c_hull():
    return serpent(**C_HULL)


def c_wedge(fov):
    x0 = C_HULL["cx"] + C_HULL["box"] / 2 - 2
    out = empty()
    for sgn in (1, -1):
        t = math.tan(math.radians(fov / 2))
        x1 = min(124.0, x0 + 60.0 / max(t, 1e-3))
        out |= stroke([(x0, 64.0), (x1, 64.0 - sgn * (x1 - x0) * t)], w=5.0)
    return _clear_of(out, c_hull(), px=2.0)


def _scope_card(key, title, parts, states):
    return dict(name=f"scope_{key}", group="scope", lockup_state=1, element="Space",
                before=ICON_DIR + "Serpent_Scope-PLACEHOLDER.png", title=title, parts=parts, states=states)


CARDS += [
    _scope_card("a", "A  The eyepiece: the Charge-petal eyepiece and the cone reticle at its true size; the ring grows "
                     "as the zoom deepens (rest 22 deg, Deep Focus 13.8, Space 10 + Deep Focus 6.9)",
                dict(frame=eyepiece_frame, rest=lambda: a_scope(FOV_REST), deep=lambda: a_scope(FOV_DEEP),
                     deep10=lambda: a_scope(FOV_DEEP10)),
                [("full zoom at rest (22 deg)", [("frame", "white"), ("rest", "white")]),
                 ("Space 5 Deep Focus (13.8 deg)", [("frame", "white"), ("deep", "white")]),
                 ("Space 10 + Deep Focus (6.9 deg)", [("frame", "white"), ("deep10", "white")])]),
    _scope_card("b", "B  The view: the eyepiece with a super-shielded prism at the size the zoom shows it; "
                     "Deep Focus draws it 1.6x larger",
                dict(frame=eyepiece_frame, flight=lambda: b_target(FOV_FLIGHT), rest=lambda: b_target(FOV_REST),
                     deep=lambda: b_target(FOV_DEEP)),
                [("trigger off (flight view, 60 deg)", [("frame", "white"), ("flight", "shielded")]),
                 ("full zoom at rest (22 deg)", [("frame", "white"), ("rest", "shielded")]),
                 ("Space 5 Deep Focus (13.8 deg)", [("frame", "white"), ("deep", "shielded")])]),
    _scope_card("c", "C  The reach: the Serpent and its view wedge at the true field of view, narrowing from the "
                     "flight view to full zoom to Deep Focus",
                dict(hull=c_hull, flight=lambda: c_wedge(FOV_FLIGHT), rest=lambda: c_wedge(FOV_REST),
                     deep=lambda: c_wedge(FOV_DEEP)),
                [("trigger off (flight view, 60 deg)", [("hull", "domain"), ("flight", "white")]),
                 ("full zoom at rest (22 deg)", [("hull", "domain"), ("rest", "white")]),
                 ("Space 5 Deep Focus (13.8 deg)", [("hull", "domain"), ("deep", "white")])]),
]


# ---- Time: Solid Fuel Pellets options (2026-10-29) ----------------------------------------------------------------
# WHO gains WHAT: the Serpent spends its tank in four fixed pellets (a quarter each), and burns that overlap stack -
# one is 3x cruise, four at once 9x. Time sets how long a pellet BURNS (x1 at rest to x1.6 at Time 10), never how fast.
# No Time 5 upgrade is authored. The card is also the tank's gauge: the HUD fills four pip Images (boostPip1-4,
# SerpentVesselHUDView.PaintPips), so every option is four pellets that fill one by one.
# A is the shipped art as it stands (PelletSlots + Line_1..4, the BEFORE row); B and C redraw it.
PELLET_YS = (28.0, 52.0, 76.0, 100.0)
TIME_BURN = (1.0, 1.6)


# B: THE PELLETS. Four solid-fuel grains stacked, nose right; a burning grain trails its plume, whose length is its
# burn time (x1 at rest, x1.6 at Time 10). Several burning at once = the overlap.
def pellet(i):
    y = PELLET_YS[i]
    return chamfered_rect(70.0, y - 8.0, 120.0, y + 8.0, 4.0)


def pellets(which):
    out = empty()
    for i in which:
        out |= pellet(i)
    return out


def plume(i, burn=1.0):
    y = PELLET_YS[i]
    x1 = 64.0
    x0 = x1 - 34.0 * burn
    out = empty()
    for dy, k in ((0.0, 1.0), (-5.0, 0.7), (5.0, 0.55)):
        out |= stroke([(x1 - (x1 - x0) * k, y + dy), (x1, y + dy)], w=3.5)
    return out


def plumes(which, burn=1.0):
    out = empty()
    for i in which:
        out |= plume(i, burn)
    return out


# C: THE OVERLAP. The Serpent nose right; behind it one exhaust streak per burning pellet, the streaks stacked - one
# pellet, then four overlapping. The pellets in hand are four ticks under the hull.
C2_HULL = dict(box=56.0, cx=84.0, cy=56.0)


def c2_hull():
    return serpent(**C2_HULL)


def c2_streaks(n, burn=1.0):
    x1 = C2_HULL["cx"] - C2_HULL["box"] / 2 + 4
    out = empty()
    for i in range(n):
        dy = (i - (n - 1) / 2) * 9.0
        out |= stroke([(x1 - 46.0 * burn, 56.0 + dy), (x1, 56.0 + dy)], w=5.0)
    return _clear_of(out, c2_hull(), px=2.0)


def c2_ticks(n):
    out = empty()
    for i in range(n):
        x = 34.0 + i * 20.0
        out |= chamfered_rect(x, 104.0, x + 15.0, 114.0, 3.0)
    return out


def _fuel_card(key, title, parts, states):
    return dict(name=f"pellets_{key}", group="pellets", lockup_state=1, element="Time",
                before="Assets/_Graphics/Design Assests/HUD UI/Serpent Fuel/PelletSlots.png", title=title,
                parts=parts, states=states)


CARDS += [
    _fuel_card("b", "B  The pellets: four solid-fuel grains, each burning one trailing its plume; plume length is the "
                    "burn time (x1.6 at Time 10)",
               dict(all4=lambda: pellets(range(4)), left2=lambda: pellets((2, 3)), b01=lambda: plumes((0, 1)),
                    b01t=lambda: plumes((0, 1), TIME_BURN[1]), b0123=lambda: plumes(range(4)),
                    empty01=lambda: pellets((0, 1)), empty0123=lambda: pellets(range(4))),
               [("four in hand", [("all4", "white")]),
                ("two burning, overlapped", [("empty01", "white", 0.25), ("b01", "white"), ("left2", "white")]),
                ("Time 10: burns 1.6x longer", [("empty01", "white", 0.25), ("b01t", "white"), ("left2", "white")]),
                ("all four at once (9x)", [("empty0123", "white", 0.25), ("b0123", "white")])]),
    _fuel_card("c", "C  The overlap: the Serpent with one exhaust streak per burning pellet, the pellets in hand as "
                    "four ticks below",
               dict(hull=c2_hull, t4=lambda: c2_ticks(4), t3=lambda: c2_ticks(3), t0=lambda: c2_ticks(4),
                    s1=lambda: c2_streaks(1), s1t=lambda: c2_streaks(1, TIME_BURN[1]), s4=lambda: c2_streaks(4)),
               [("four in hand", [("hull", "domain"), ("t4", "white")]),
                ("one burning (3x)", [("hull", "domain"), ("s1", "white"), ("t3", "white")]),
                ("Time 10: burns 1.6x longer", [("hull", "domain"), ("s1t", "white"), ("t3", "white")]),
                ("all four at once (9x)", [("hull", "domain"), ("s4", "white"), ("t0", "white", 0.25)])]),
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
    P = _wall_px(k)
    cx, cy = i * WALL_PITCH, j * WALL_PITCH
    hl, hs = WALL_REACH * WALL_SHORT * WALL_ASPECT / 2, WALL_REACH * WALL_SHORT / 2
    a, b = ((0, hl), (hs, 0)) if _up(i, j) else ((hl, 0), (0, hs))
    return fill([P(cx + a[0], cy + a[1]), P(cx + b[0], cy + b[1]), P(cx - a[0], cy - a[1]), P(cx - b[0], cy - b[1])])


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
