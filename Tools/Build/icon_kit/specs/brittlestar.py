"""The Brittle Star's ability-row icons (Resources/ElementalAbilityMaps/BrittleStar.asset, R_VesselActions/BRITTLE_STAR.md).

  Charge  Charge Mode   Y toggles: five CORDS grow out of the five turrets; RT, analog, takes them from a 120 u WHIP (low
                        stiffness, waves travel down them) to an 80 u SABER (straighter, 40% of the way to rigid). They
                        slice prisms they sweep through (the Thresher's chain cut) and a cord across a rival hull is a
                        Strike. Plate: the spare-ring stock LT throws (0, half, full of two), lime while the mode is on.
  Mass    Creation      B toggles: a trail behind each of the five ring corners (five lines that twist as the ring turns);
                        the slowest mode (x0.5). Plate: the mode's eased switch.
  Space   Space Mode    X toggles: all five guns fire along their arms (stick in: they meet ahead; out: they splay 25 deg);
                        heat, overheat lays danger prisms. Plate: the heat, hot colour while overheated.
  Time    Speed Mode    A toggles: x6 -> x8 speed, the prisms stretch so the trail still connects. Plate: the eased switch.

Before (2026-11-01): the round-1 set - Ring Detonation (a pentagon of bars round a burst; the Charge card still wears it,
BRITTLE_STAR.md follow-up F12), Creation (five scattered blocks and a dashed ring), Destruction (the ring with speed
lines), Speed (the ring with trails). The Charge card goes first: it shows an ability the card no longer is.

Real geometry:
  the hull     BrittleStar.fbx seen NOSE-ON (yaw 90, pitch -90: a pentagon point up) - the ring of five arms round the
               cockpit, a turret at each corner. From above or the side it is a line: the ring stands across the nose.
  a prism      two close triangles (motifs.prism_halves; never the rhombus, the Time petal's shape); a sliced one has
               its cut along the cord's line, the halves apart.
"""
import math

import mesh_silhouette
from canvas import N, empty, fill, stroke

import numpy as np
from PIL import Image

HULL = "BrittleStar"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/BrittleStar/"
FBX = "Assets/_Models/Vessel Models/BrittleStar.fbx"
NOSE_ON = dict(pitch=-90, yaw=90, dilate_px=1.0)   # the ring face on, a corner up


def _png_mask(rel):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def shipped(name):
    return lambda: _png_mask(ICON_DIR + f"BrittleStar_{name}.png")


def _clear_of(mask, *others, px=2.5):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


_STARS = {}


def star(box, cx=64.0, cy=64.0):
    def draw():
        key = (box, cx, cy)
        if key not in _STARS:
            _STARS[key] = mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, **NOSE_ON)
        return _STARS[key].copy()
    return draw


ICONS = []
# The row as the HUD draws it today: four white sprites, each over its plate gauge (BrittleStarHUDView, one per card).
LOCKUP = dict(
    Charge=dict(parts=dict(i=shipped("RingDetonation")), state=[("i", "white")], gauge=1.0),
    Mass=dict(parts=dict(i=shipped("CreationMode")), state=[("i", "white")], gauge=0.0),
    Space=dict(parts=dict(i=shipped("DestructionMode")), state=[("i", "white")], gauge=0.3),
    Time=dict(parts=dict(i=shipped("SpeedMode")), state=[("i", "white")], gauge=0.0),
)
CARDS = []
REBIND = []


# ---- Charge: Charge Mode, the five cords ----------------------------------------------------------------------------------
# WHO: the Brittle Star nose-on (domain). WHAT: five cords out of its five turrets, a whip at rest and a saber on a full
# RT pull, slicing prisms.
HULL_BOX = 64.0
CORNER_R = 30.0                          # the turrets' radius at HULL_BOX (measured off the silhouette's corners)
CORNERS = [(64 + CORNER_R * math.cos(math.radians(-90 + 72 * k)), 64 + CORNER_R * math.sin(math.radians(-90 + 72 * k)))
           for k in range(5)]
C_STAR = star(HULL_BOX)


def _cord(k, length, wave, w=4.5, phase=0.0):
    """Cord k out of corner k, radially: `wave` px of sideways swing growing toward the tip (a whip), 0 = straight."""
    a = math.radians(-90 + 72 * k)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    x0, y0 = CORNERS[k]
    pts = []
    for t in np.linspace(0, 1, 24):
        off = wave * t * math.sin(t * 2.2 * math.pi + phase)
        pts.append((x0 + ux * length * t + px * off, y0 + uy * length * t + py * off))
    return stroke(pts, w=w)


def whips():
    out = empty()
    for k in range(5):
        out |= _cord(k, 30, 7, w=5.0, phase=0.6 * k)
    return _clear_of(out, C_STAR(), px=1.5) | (out & C_STAR())


def sabers(length=26):
    out = empty()
    for k in range(5):
        out |= _cord(k, length, 0, w=5.0)
    return out


SLICED = (64 + 50 * math.cos(math.radians(54)), 64 + 50 * math.sin(math.radians(54)), 12.0)   # on cord 2's line, cut along it


def sliced_prism():
    # The prism as two close triangles (never the rhombus: the Time petal's shape, Garrett 2026-11-01), its cut lying
    # along cord 2 - the cord has just sliced it, and the halves have come apart.
    cx, cy, s_ = SLICED
    a = math.radians(-90 + 72 * 2)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    w, g = s_ * 0.8, 2.6
    out = empty()
    for sgn in (-1, 1):
        ox, oy = cx + px * g * sgn, cy + py * g * sgn
        out |= fill([(ox + ux * s_, oy + uy * s_), (ox - ux * s_, oy - uy * s_), (ox + px * w * sgn, oy + py * w * sgn)])
    return out


def a_star():
    return C_STAR()


CHARGE_BEFORE = ICON_DIR + "BrittleStar_RingDetonation.png"
# Garrett picked C (2026-11-01: "i like C, but i don't but the CTA color as the on color is too much"): the whips at rest,
# the sabers as RT pulls - the HUD crossfades the two parts on the pull. The plate keeps the spare-ring stock in its
# resting colour: no lime for "on" (the lit cords already say it).
CHARGE_PARTS = [("ChargeWhips", "BrittleStar_ChargeWhips", "chargeWhipsIcon", whips,
                 "the five cords loose, a whip each (white; fades as RT pulls; dim with the mode off)"),
                ("ChargeSabers", "BrittleStar_ChargeSabers", "chargeSabersIcon", lambda: sabers(24),
                 "the five cords pulled straight, sabers (white; comes in as RT pulls)"),
                ("ChargeStar", "BrittleStar_ChargeStar", "chargeStarIcon", C_STAR, "the Brittle Star nose-on (domain)")]
ICONS += [dict(name=i, element="Charge", act="Charge Mode: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in CHARGE_PARTS]
CHARGE_GAUGE = {"Charge Mode on": (1.0, None), "RT pulled": (1.0, None), "off": (1.0, None)}
CHARGE_ON = [("whip", "white"), ("star", "domain")]
CARDS += [
    dict(name="charge", element="Charge", before=CHARGE_BEFORE, before_gauge=True, gauge=CHARGE_GAUGE,
         title="Charge Mode (Charge, picked C): the Brittle Star nose-on, a whip out of each turret; RT pulls them into "
               "sabers; the plate is the spare-ring stock, never lime",
         parts=dict(star=C_STAR, whip=whips, saber=lambda: sabers(24)),
         states=[("Charge Mode on", CHARGE_ON),
                 ("RT pulled", [("saber", "white"), ("star", "domain")]),
                 ("off", [("whip", "white", 0.25), ("star", "domain")])],
         lockup_state=0),
]
LOCKUP["Charge"] = dict(parts=dict(star=C_STAR, whip=whips), state=CHARGE_ON, gauge=1.0)
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/BrittleStarHUDVariant.prefab"
VIEW = "5875684227236902712"            # BrittleStarHUDView
CHARGE_ICON = "7700000000000001105"     # the Charge slot's icon Image (was BrittleStar_RingDetonation)
REBIND += [
    dict(file=VARIANT, pair=dict(container=CHARGE_ICON, view=VIEW, after="speedGauge",
                                 children=[(n, i, f) for n, i, f, _d, _a in CHARGE_PARTS])),
]


# ---- Mass: Creation Mode, the five corner trails (options, 2026-11-01) ----------------------------------------------------
# WHO: the Brittle Star (domain). WHAT: a line of prisms laid behind each of the five ring corners, twisting as the ring
# turns; Mass makes every block fatter (x and y x1 -> x2). A prism is a square with a negative-space X (never the rhombus).
def prism_sq(cx, cy, s, cut=None):
    """A square prism, side 2s, with a negative-space X (motifs.prism_square_x on the Tiger Moth branch)."""
    cut = cut if cut is not None else max(1.2, s * 0.28)
    sq = fill([(cx - s, cy - s), (cx + s, cy - s), (cx + s, cy + s), (cx - s, cy + s)])
    x = stroke([(cx - s, cy - s), (cx + s, cy + s)], w=cut) | stroke([(cx + s, cy - s), (cx - s, cy + s)], w=cut)
    return sq & ~x


def _rows(points_sizes, girth=1.0):
    out = empty()
    for x, y, s in points_sizes:
        out |= prism_sq(x, y, s * girth)
    return out


# A  the tunnel: seen from behind, the star small at the centre and each corner's line of prisms coming at you, bigger
#    the nearer they are.
A_BOX = 32.0
A_STAR = star(A_BOX)
A_CR = CORNER_R * A_BOX / HULL_BOX


def a_trails(girth=1.0):
    pts = []
    for k in range(5):
        a = math.radians(-90 + 72 * k + 8)              # a little turned: the ring twists the lines
        for j, (r, s) in enumerate([(A_CR + 10, 3.6), (A_CR + 24, 5.4), (A_CR + 41, 7.4)]):
            aj = a + math.radians(6 * j)
            pts.append((64 + r * math.cos(aj), 64 + r * math.sin(aj), s))
    return _clear_of(_rows(pts, girth), A_STAR(), px=2.0)


# B  the braid: the star nose-on at the right, flying right; its five lines of prisms trail left and twist round each
#    other (the ring turning as it builds).
B_BOX, B_CX = 44.0, 98.0
B_STAR = star(B_BOX, cx=B_CX)
B_CR = CORNER_R * B_BOX / HULL_BOX


def _braid_pts(k, n=3):
    out = []
    for j in range(n):
        t = (j + 1) / n
        x = B_CX - 24 - 27 * j
        th = math.radians(-90 + 72 * k) + t * 1.4
        y = 64 + B_CR * 0.95 * math.sin(th)
        out.append((x, y, math.cos(th)))
    return out


def b_trails(girth=1.0):
    pts = []
    for k in range(5):
        for x, y, depth in _braid_pts(k):
            pts.append((x, y, 4.6 + 1.0 * depth))
    return _clear_of(_rows(pts, girth), B_STAR(), px=2.0)


def b_lines():
    out = empty()
    for k in range(5):
        p = [(B_CX + B_CR * 0.0, 64 + B_CR * 0.95 * math.sin(math.radians(-90 + 72 * k)))]
        p = [(B_CX - 6, p[0][1])] + [(x, y) for x, y, _d in _braid_pts(k)]
        out |= stroke(p, w=1.6)
    return out


# C  the rake: the star nose-on at the top, flying up the card; a line of prisms falls straight back from each corner,
#    five lines, the slow mode's tidy build.
C_BOX, C_CY = 44.0, 30.0
C_STAR2 = star(C_BOX, cy=C_CY)
C_CR = CORNER_R * C_BOX / HULL_BOX


def c_trails(girth=1.0):
    pts = []
    for k in range(5):
        a = math.radians(-90 + 72 * k)
        x0, y0 = 64 + C_CR * math.cos(a), C_CY + C_CR * math.sin(a)
        if k == 0:
            continue                                     # the top corner's line runs behind the star
        for j in range(3):
            pts.append((64 + (x0 - 64) * (1 + 0.28 * j), y0 + 20 + 22 * j, 4.4 + 1.2 * j))
    return _clear_of(_rows(pts, girth), C_STAR2(), px=2.0)


MASS_BEFORE = ICON_DIR + "BrittleStar_CreationMode.png"
MASS_GAUGE = {"Creation on": (1.0, None), "Mass 10 (fat blocks)": (1.0, None), "off": (0.0, None)}


def _mass_states(star_key="star"):
    return [("Creation on", [("trail", "white"), (star_key, "domain")]),
            ("Mass 10 (fat blocks)", [("fat", "white"), (star_key, "domain")]),
            ("off", [("trail", "white", 0.25), (star_key, "domain")])]


CARDS += [
    dict(name="mass_a", group="mass", element="Mass", before=MASS_BEFORE, before_gauge=True, gauge=MASS_GAUGE,
         title="A  the tunnel: seen from behind, the star (domain) and each corner's line of prisms (white) coming at you",
         parts=dict(star=A_STAR, trail=a_trails, fat=lambda: a_trails(1.45)), states=_mass_states(), lockup_state=0),
    dict(name="mass_b", group="mass", element="Mass", before=MASS_BEFORE, before_gauge=True, gauge=MASS_GAUGE,
         title="B  the braid: the star (domain) flying right, its five lines of prisms (white) twisting round each other",
         parts=dict(star=B_STAR, trail=lambda: b_trails() | b_lines(), fat=lambda: b_trails(1.45) | b_lines()),
         states=_mass_states(), lockup_state=0),
    dict(name="mass_c", group="mass", element="Mass", before=MASS_BEFORE, before_gauge=True, gauge=MASS_GAUGE,
         title="C  the rake: the star (domain) flying up, a line of prisms (white) falling straight back from each corner",
         parts=dict(star=C_STAR2, trail=c_trails, fat=lambda: c_trails(1.4)), states=_mass_states(), lockup_state=0),
]


# ---- Mass in the game: A, the tunnel (Garrett picked A, 2026-11-01: "A is good") ------------------------------------------
# Three parts in the Mass slot's container, shown by BrittleStarHUDView.SetCreationTrails: the star (domain) always; the
# five lines of prisms (white, dim with the mode off), crossfading to the fat ones as Mass widens the blocks (x1 -> x2).
MASS_PARTS = [("MassTrails", "BrittleStar_MassTrails", "massTrailsIcon", a_trails,
               "each corner's line of prisms coming at you (white; dim with Creation off; fades as Mass widens the blocks)"),
              ("MassTrailsFat", "BrittleStar_MassTrailsFat", "massTrailsFatIcon", lambda: a_trails(1.45),
               "the same lines, the blocks fattened by Mass (white; comes in as Mass widens them)"),
              ("MassStar", "BrittleStar_MassStar", "massStarIcon", A_STAR, "the Brittle Star seen from behind (domain)")]
ICONS += [dict(name=i, element="Mass", act="Creation Mode: " + act, draw=draw, subdir="Parts", family=False,
               exempt={"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
                       "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part",
                       "specks": "a part"})
          for _n, i, _f, draw, act in MASS_PARTS]
for c in CARDS:
    if c["name"] == "mass_a":
        CARDS.append(dict(c, name="mass", group=None,
                          title="Creation Mode (Mass, picked A): seen from behind, the star (domain) and each corner's line "
                                "of prisms (white) coming at you; Mass fattens the blocks; the plate is the mode's switch"))
        break
CARDS[:] = [c for c in CARDS if c.get("group") != "mass"]   # the losers' sheets go once the owner picks
LOCKUP["Mass"] = dict(parts=dict(star=A_STAR, trail=a_trails), state=[("trail", "white"), ("star", "domain")], gauge=1.0)
MASS_ICON = "7700000000000001115"       # the Mass slot's icon Image (was BrittleStar_CreationMode)
REBIND += [
    dict(file=VARIANT, pair=dict(container=MASS_ICON, view=VIEW, after="chargeStarIcon",
                                 children=[(n, i, f) for n, i, f, _d, _a in MASS_PARTS])),
]


# ---- Space: Space Mode, the five guns (options, 2026-11-01) ---------------------------------------------------------------
# WHO: the Brittle Star (domain). WHAT: all five guns fire along their arms - stick out they splay 25 deg, stick in they meet
# ahead; heat climbs, and overheated the hull lays DANGER prisms. Space doubles every gun's range. A round is a little
# disc with a short streak behind it (never a rhombus); a danger prism is two close triangles.
def round_shot(x, y, r, dx, dy, tail=2.6):
    n = math.hypot(dx, dy) or 1.0
    ux, uy = dx / n, dy / n
    return disc(x, y, r) | stroke([(x - ux * r * tail, y - uy * r * tail), (x, y)], w=r * 0.9)


def prism_pair(cx, cy, s, gap=1.6):
    """A prism as two close triangles, cut along a vertical (motifs.prism_halves on the Tiger Moth branch)."""
    return (fill([(cx - gap / 2, cy - s), (cx - gap / 2, cy + s), (cx - gap / 2 - s * 0.9, cy)]) |
            fill([(cx + gap / 2, cy - s), (cx + gap / 2, cy + s), (cx + gap / 2 + s * 0.9, cy)]))


from canvas import disc  # noqa: E402  (the round's head)

# A  the fan: the star at the left flying right; a round out of each of its five turrets, the five splayed (stick out) or
#    meeting ahead (stick in). Overheated, a danger prism behind it.
SA_BOX, SA_CX = 56.0, 32.0
SA_STAR = star(SA_BOX, cx=SA_CX)
SA_CR = CORNER_R * SA_BOX / HULL_BOX


def _sa_corner_y(k):
    return 64 + SA_CR * math.sin(math.radians(-90 + 72 * k))


def sa_rounds(meet=False, reach=1.0):
    out = empty()
    for k in range(5):
        y0 = _sa_corner_y(k)
        for j, t in enumerate((0.3, 0.9)):
            x = SA_CX + 30 + (44 if reach <= 1 else 62) * t
            spread = (y0 - 64) * (1.0 + (0.9 if not meet else -0.8) * t)
            y = 64 + spread
            out |= round_shot(x, y, 6.0, x - SA_CX, spread - (y0 - 64), tail=1.8)
    return _clear_of(out, SA_STAR(), px=1.5)


def sa_hot():
    return prism_pair(SA_CX - 0.0, 108, 7.0)


# B  the tunnel (the Mass card's view): the star seen from behind, its five rounds flying away from you along the splayed
#    arms, smaller as they go.
SB_STAR = A_STAR


def sb_rounds(reach=1.0):
    out = empty()
    for k in range(5):
        a = math.radians(-90 + 72 * k)
        ux, uy = math.cos(a), math.sin(a)
        for j, (r, s) in enumerate([(A_CR + 15, 8.0), (A_CR + 31 * reach, 6.0), (A_CR + 44 * reach, 4.4)]):
            if r > 60:
                continue
            out |= disc(64 + ux * r, 64 + uy * r, s)
    return _clear_of(out, SB_STAR(), px=2.0)


def sb_hot():
    return prism_pair(64, 112, 6.5)


# C  the converge: the star at the left flying right, the five rounds' lines meeting at one point ahead (stick in), a
#    burst where they meet.
def sc_lines(reach=1.0):
    out = empty()
    tx = 100 if reach <= 1 else 122
    for k in range(5):
        y0 = _sa_corner_y(k)
        x0 = SA_CX + 8
        pts = [(x0 + (tx - x0) * t, y0 + (64 - y0) * t) for t in np.linspace(0.25, 0.9, 8)]
        out |= stroke(pts, w=3.4)
        x, y = pts[4]
        out |= round_shot(x, y, 6.0, tx - x0, 64 - y0, tail=1.2)
    return _clear_of(out, SA_STAR(), px=1.5)


SPACE_BEFORE = ICON_DIR + "BrittleStar_DestructionMode.png"
SPACE_GAUGE = {"Space Mode, stick out": (0.35, None), "stick in": (0.55, None), "Space 10 (range x2)": (0.35, None),
               "overheated": (1.0, (255, 82, 46, 255)), "off": (0.0, None)}


def _space_states(meet_key):
    return [("Space Mode, stick out", [("rounds", "white"), ("star", "domain")]),
            ("stick in", [(meet_key, "white"), ("star", "domain")]),
            ("Space 10 (range x2)", [("far", "white"), ("star", "domain")]),
            ("overheated", [("rounds", "white", 0.25), ("star", "domain")]),
            ("off", [("rounds", "white", 0.25), ("star", "domain")])]


CARDS += [
    dict(name="space_a", group="space", element="Space", before=SPACE_BEFORE, before_gauge=True, gauge=SPACE_GAUGE,
         title="A  the fan: the star (domain) flying right, a round (white) out of each turret, splayed (stick "
               "out) or meeting (stick in)",
         parts=dict(star=SA_STAR, rounds=sa_rounds, meet=lambda: sa_rounds(meet=True), far=lambda: sa_rounds(reach=2.0),
                    hot=sa_hot),
         states=_space_states("meet"), lockup_state=0),
    dict(name="space_b", group="space", element="Space", before=SPACE_BEFORE, before_gauge=True, gauge=SPACE_GAUGE,
         title="B  the tunnel: the star (domain) seen from behind, its five rounds (white) flying away along the arms",
         parts=dict(star=SB_STAR, rounds=sb_rounds, meet=lambda: sb_rounds(0.6), far=lambda: sb_rounds(1.35), hot=sb_hot),
         states=_space_states("meet"), lockup_state=0),
    dict(name="space_c", group="space", element="Space", before=SPACE_BEFORE, before_gauge=True, gauge=SPACE_GAUGE,
         title="C  the converge: the star (domain) flying right, the five guns' lines (white) meeting at one point ahead",
         parts=dict(star=SA_STAR, rounds=sc_lines, meet=sc_lines, far=lambda: sc_lines(2.0), hot=sa_hot),
         states=_space_states("meet"), lockup_state=0),
]


# ---- Time: Speed Mode (options, 2026-11-01) -------------------------------------------------------------------------------
# WHO: the Brittle Star (domain). WHAT: the fastest mode, x6 -> x8 of cruise with Time; the prisms it lays stretch so the
# trail still joins up. A stretched prism is a long rectangle with its negative-space X (never the rhombus).
def prism_long(cx, cy, hl, hh, cut=2.0):
    box_ = fill([(cx - hl, cy - hh), (cx + hl, cy - hh), (cx + hl, cy + hh), (cx - hl, cy + hh)])
    x = stroke([(cx - hl, cy - hh), (cx + hl, cy + hh)], w=cut) | stroke([(cx + hl, cy - hh), (cx - hl, cy + hh)], w=cut)
    return box_ & ~x


T_BOX, T_CX = 50.0, 96.0
T_STAR = star(T_BOX, cx=T_CX)


def t_trail(stretch=1.0):
    """The trail behind the star (left): short cruise blocks with gaps at 1, long joined blocks as Speed stretches them."""
    hl, gap = {1.0: (4.0, 7.0), 2.3: (9.0, 2.2), 3.0: (13.0, 1.6)}[stretch]
    out = empty()
    right = T_CX - 22
    while right > 12:
        left = max(6.0, right - 2 * hl)
        out |= prism_long((left + right) / 2, 64, (right - left) / 2, 8.0, cut=2.6)
        right = left - gap
    return _clear_of(out, T_STAR(), px=2.0)


def t_lines(n=3, length=30.0):
    out = empty()
    for y, l in list(zip((40, 88, 64), (length, length * 0.8, length * 1.2)))[:n]:
        if y == 64:
            continue
        out |= stroke([(T_CX - 26 - l, y), (T_CX - 26, y)], w=3.4)
    return out


# A  the stretch: the star flying right, its trail behind it; the gapped cruise blocks stretch into one joined line.
# B  the stretch + streaks: A with two speed streaks above and below the trail.
# C  the warp: seen from behind (the Mass card's view), streaks rushing out past the star from its centre.
def c_warp(length=1.0):
    out = empty()
    for k in range(10):
        a = math.radians(-90 + 36 * k + 18)
        r0, r1 = A_CR + 14, A_CR + 14 + 22 * length
        out |= stroke([(64 + r0 * math.cos(a), 64 + r0 * math.sin(a)),
                       (64 + min(r1, 62) * math.cos(a), 64 + min(r1, 62) * math.sin(a))], w=3.2)
    return _clear_of(out, A_STAR(), px=2.0)


TIME_BEFORE = ICON_DIR + "BrittleStar_SpeedMode.png"
TIME_GAUGE = {"Speed Mode on": (1.0, None), "Time 10 (x8)": (1.0, None), "off (cruise)": (0.0, None)}
CARDS += [
    dict(name="time_a", group="time", element="Time", before=TIME_BEFORE, before_gauge=True, gauge=TIME_GAUGE,
         title="A  the stretch: the star (domain) flying right; its gapped cruise blocks (white) stretch into one joined line",
         parts=dict(star=T_STAR, slow=t_trail, fast=lambda: t_trail(2.3), faster=lambda: t_trail(3.0)),
         states=[("Speed Mode on", [("fast", "white"), ("star", "domain")]),
                 ("Time 10 (x8)", [("faster", "white"), ("star", "domain")]),
                 ("off (cruise)", [("slow", "white", 0.4), ("star", "domain")])], lockup_state=0),
    dict(name="time_b", group="time", element="Time", before=TIME_BEFORE, before_gauge=True, gauge=TIME_GAUGE,
         title="B  the stretch and streaks: A, with two speed streaks (white) beside the joined line",
         parts=dict(star=T_STAR, slow=t_trail, fast=lambda: t_trail(2.3) | t_lines(), faster=lambda: t_trail(3.0) | t_lines(length=44)),
         states=[("Speed Mode on", [("fast", "white"), ("star", "domain")]),
                 ("Time 10 (x8)", [("faster", "white"), ("star", "domain")]),
                 ("off (cruise)", [("slow", "white", 0.4), ("star", "domain")])], lockup_state=0),
    dict(name="time_c", group="time", element="Time", before=TIME_BEFORE, before_gauge=True, gauge=TIME_GAUGE,
         title="C  the warp: the star (domain) seen from behind, streaks (white) rushing out past it",
         parts=dict(star=A_STAR, slow=lambda: c_warp(0.35), fast=c_warp, faster=lambda: c_warp(1.5)),
         states=[("Speed Mode on", [("fast", "white"), ("star", "domain")]),
                 ("Time 10 (x8)", [("faster", "white"), ("star", "domain")]),
                 ("off (cruise)", [("slow", "white", 0.4), ("star", "domain")])], lockup_state=0),
]


PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}


def _pick(group, winner, name, title, element, parts_spec, lockup, icon, after):
    """Ship the owner's pick: its parts into the slot's container, its states card, the losers' sheets gone."""
    global ICONS, CARDS, REBIND
    ICONS += [dict(name=i, element=element, act=act, draw=draw, subdir="Parts", family=False, exempt=PART_EXEMPT)
              for _n, i, _f, draw, act in parts_spec]
    for c in CARDS:
        if c["name"] == winner:
            CARDS.append(dict(c, name=name, group=None, title=title))
            break
    CARDS[:] = [c for c in CARDS if c.get("group") != group]
    LOCKUP[element] = lockup
    REBIND += [dict(file=VARIANT, pair=dict(container=icon, view=VIEW, after=after,
                                            children=[(n, i, f) for n, i, f, _d, _a in parts_spec]))]


# ---- Space in the game: A, the fan (Garrett picked A, 2026-11-01: "a is good") ----------------------------------------
# Four parts, shown by BrittleStarHUDView.SetGuns: the star (domain); the rounds splayed (stick pushed), meeting (stick
# centred), and splayed at Space's doubled range; dim with the mode off or the guns overheated.
SPACE_PARTS = [("SpaceRounds", "BrittleStar_SpaceRounds", "spaceRoundsIcon", sa_rounds,
                "Space Mode: the five rounds splayed (white; the stick pushed)"),
               ("SpaceRoundsMeet", "BrittleStar_SpaceRoundsMeet", "spaceRoundsMeetIcon", lambda: sa_rounds(meet=True),
                "Space Mode: the five rounds meeting ahead (white; the stick centred)"),
               ("SpaceRoundsFar", "BrittleStar_SpaceRoundsFar", "spaceRoundsFarIcon", lambda: sa_rounds(reach=2.0),
                "Space Mode: the rounds splayed at Space's doubled range (white)"),
               ("SpaceStar", "BrittleStar_SpaceStar", "spaceStarIcon", SA_STAR, "Space Mode: the Brittle Star (domain)")]
SPACE_ICON = "7700000000000001125"      # the Space slot's icon Image (was BrittleStar_DestructionMode)
_pick("space", "space_a", "space",
      "Space Mode (Space, picked A): the star (domain) flying right, a round (white) out of each turret, splayed or "
      "meeting; the plate is the heat, hot while overheated", "Space", SPACE_PARTS,
      dict(parts=dict(star=SA_STAR, rounds=sa_rounds), state=[("rounds", "white"), ("star", "domain")], gauge=0.35),
      SPACE_ICON, "massStarIcon")

# ---- Time in the game: A, the stretch (Garrett picked A, 2026-11-01: "a is good") -------------------------------------
# Four parts, shown by BrittleStarHUDView.SetSpeedTrail: the star (domain); the gapped cruise blocks (dim, mode off); the
# stretched joined line (mode on), longer as Time raises the speed x6 -> x8.
TIME_PARTS = [("TimeTrailCruise", "BrittleStar_TimeTrailCruise", "timeTrailCruiseIcon", t_trail,
               "Speed Mode: the cruise trail, short blocks with gaps (white, dim; the mode off)"),
              ("TimeTrailFast", "BrittleStar_TimeTrailFast", "timeTrailFastIcon", lambda: t_trail(2.3),
               "Speed Mode: the blocks stretched into one joined line (white; the mode on)"),
              ("TimeTrailFaster", "BrittleStar_TimeTrailFaster", "timeTrailFasterIcon", lambda: t_trail(3.0),
               "Speed Mode: stretched further at Time's x8 (white)"),
              ("TimeStar", "BrittleStar_TimeStar", "timeStarIcon", T_STAR, "Speed Mode: the Brittle Star (domain)")]
TIME_ICON = "7700000000000001135"       # the Time slot's icon Image (was BrittleStar_SpeedMode)
_pick("time", "time_a", "time",
      "Speed Mode (Time, picked A): the star (domain) flying right, its gapped cruise blocks (white) stretched into one "
      "joined line; the plate is the mode's switch", "Time", TIME_PARTS,
      dict(parts=dict(star=T_STAR, fast=lambda: t_trail(2.3)), state=[("fast", "white"), ("star", "domain")], gauge=1.0),
      TIME_ICON, "spaceStarIcon")


# ---- the OMNI crystal card (options, 2026-11-01) ---------------------------------------------------------------------------
# BRITTLE_STAR.md 3.5: every crystal, the omni one included, adds ONE SPARE RING (up to two); throwing the ring (LT) needs a
# spare, and the thrown ring detonates (the five-fold blast). The card is VesselHUDView.omniAbilitySprite: ONE sprite,
# white (the lockup tints nothing on the omni card).
from canvas import ngon  # noqa: E402
import motifs  # noqa: E402


def spare_ring(cx, cy, r, w=3.4):
    """A ring: the pentagon outline, a point up (the support ring without its hull)."""
    return stroke(ngon(5, r, cx, cy, rot=-90), w=w, closed=True)


O_A_STAR = star(56.0, cx=50, cy=72)
O_C_STAR = star(56.0, cy=46)
O_B_STAR = star(38.0, cx=24)


def omni_a():
    # A  the spare: the star with a new ring floating up beside it - the crystal's gift.
    return O_A_STAR() | spare_ring(98, 34, 20, w=4.2)


def omni_b():
    # B  what the spare buys: the star, its ring thrown ahead, and the ring's five-fold blast.
    ring = spare_ring(64, 64, 15, w=4.0)
    trail_ = stroke([(38, 64), (46, 64)], w=3.0)
    blast = motifs.burst(102, 64, 24, spikes=10, inner=0.5, rot=-90)
    return O_B_STAR() | ring | _clear_of(trail_, O_B_STAR(), px=2) | blast


def omni_c():
    # C  the stock: the star and the two spare rings it can hold, stacked under it.
    return O_C_STAR() | spare_ring(42, 102, 16, w=4.0) | spare_ring(86, 102, 16, w=4.0)


OMNI_BEFORE = None
CARDS += [
    dict(name="omni_a", group="omni", element="Omni", title="A  the spare: a new ring (white) floating up beside the star",
         parts=dict(i=omni_a), states=[("a crystal collected", [("i", "white")])]),
    dict(name="omni_b", group="omni", element="Omni",
         title="B  what it buys: the star, its ring thrown ahead, the ring's five-fold blast",
         parts=dict(i=omni_b), states=[("a crystal collected", [("i", "white")])]),
    dict(name="omni_c", group="omni", element="Omni", title="C  the stock: the star and the two spare rings it can hold",
         parts=dict(i=omni_c), states=[("a crystal collected", [("i", "white")])]),
]


# ---- the OMNI card in the game: C, the stock (Garrett picked C, 2026-11-01: "I like that c can display the resource
# amount held") -------------------------------------------------------------------------------------------------------------
# The card is ONE sprite tinted one colour, so the count is drawn by SHAPE: a spare held is a solid pentagon, an empty
# slot a thin outline. Three sprites (0, 1, 2 spares); BrittleStarHUDView.SetSpareRings swaps them
# (VesselHUDView.SetOmniAbilitySprite). The view's omniAbilitySprite is the start state, no spare.
def stock_ring(cx, cy, r, held):
    pts = ngon(5, r, cx, cy, rot=-90)
    return fill(pts) if held else stroke(pts, w=2.2, closed=True)


def omni_stock(n):
    return lambda: O_C_STAR() | stock_ring(42, 102, 16, n >= 1) | stock_ring(86, 102, 16, n >= 2)


OMNI_STOCK = [f"BrittleStar_OmniStock{n}" for n in range(3)]
ICONS += [dict(name=OMNI_STOCK[n], element="Omni", subdir="Core", family=False, draw=omni_stock(n),
               act=f"The OMNI crystal card: the star and its spare-ring stock, {n} of 2 held (one white sprite)",
               exempt={"components": "the star and its two ring slots", "gap": "the slots apart from the star",
                       "thin": "an empty slot is an outline on purpose"})
          for n in range(3)]
CARDS[:] = [c for c in CARDS if c.get("group") != "omni"]
CARDS += [dict(name="omni", element="Omni",
               title="The OMNI crystal card (picked C): the star and its two spare-ring slots, solid when held",
               parts={f"s{n}": omni_stock(n) for n in range(3)},
               states=[("no spare", [("s0", "white")]), ("1 spare", [("s1", "white")]), ("2 spares (full)", [("s2", "white")])])]
LOCKUP["Omni"] = dict(parts=dict(i=omni_stock(1)), state=[("i", "white")])
for r in REBIND:
    if r["pair"]["container"] == TIME_ICON:
        r["pair"]["fields"] = {"omniAbilitySprite": ("icon", OMNI_STOCK[0]),
                               **{f"omniStock{n}Sprite": ("icon", OMNI_STOCK[n]) for n in range(3)}}


# ==== the LEVEL-5 states (options, 2026-11-03) ==============================================================================
# Shombith designed the four upgrades on 2026-11-03 (Docs/Studios/BRITTLE_STAR.md "Level-5 upgrades"). Garrett's rule for an
# upgrade on a card (LESSONS 46, 47, 67, 72): never a level mark - every ability has an L5 - but the ACT it unlocks, drawn on
# its real target, as a STATE the HUD swaps in from the real upgrade flag (IsUpgradeActive), and a part that appears only
# once the upgrade is live. Each round below: the card as it is today, then three candidates, in the whole lockup.
from canvas import band, rotate  # noqa: E402


# ---- Charge 5, Thrown Sabers: with the cords rigid, LT cuts all five off and throws them along their lines ------------------
def _saber_seg(k, r0, r1, tumble=0.0, w=5.0):
    """A straight saber along corner k's radial from r0 to r1, turned `tumble` degrees about its own middle."""
    a = math.radians(-90 + 72 * k)
    p0 = (64 + r0 * math.cos(a), 64 + r0 * math.sin(a))
    p1 = (64 + r1 * math.cos(a), 64 + r1 * math.sin(a))
    if tumble:
        mx, my = (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2
        p0, p1 = rotate([p0, p1], tumble, mx, my)
    return stroke([p0, p1], w=w)


def thrown_free():
    # A  cut free: the five sabers parted from their turrets by a clear gap, flying straight out along their lines.
    out = empty()
    for k in range(5):
        out |= _saber_seg(k, CORNER_R + 8, CORNER_R + 28)
    return out


def thrown_tumble():
    # B  thrown: the five sabers out past their turrets, each turned as it tumbles end over end.
    out = empty()
    for k in range(5):
        out |= _saber_seg(k, CORNER_R + 8, CORNER_R + 28, tumble=38)
    return out


def thrown_one():
    # C  one away: four sabers still on their turrets, the fifth (lower right) cut off and tumbling out past them.
    out = empty()
    for k in range(5):
        if k == 2:
            out |= _saber_seg(k, CORNER_R + 14, CORNER_R + 31, tumble=42)
        else:
            out |= _cord(k, 22, 0, w=5.0)
    return out


CHARGE5_GAUGE = {"today: RT pulled (sabers)": (1.0, None), "Charge 5: sabers rigid": (1.0, None)}
for key, title, draw in (
        ("a", "A  cut free: the five sabers (white) parted from their turrets, flying straight out", thrown_free),
        ("b", "B  thrown: the five sabers (white) out past their turrets, tumbling", thrown_tumble),
        ("c", "C  one away: four sabers on, the fifth cut off and tumbling out", thrown_one)):
    CARDS.append(dict(name=f"charge5_{key}", group="charge5", element="Charge", gauge=CHARGE5_GAUGE,
                      before=None, title=title,
                      parts=dict(star=C_STAR, saber=lambda: sabers(24), thrown=draw),
                      states=[("today: RT pulled (sabers)", [("saber", "white"), ("star", "domain")]),
                              ("Charge 5: sabers rigid", [("thrown", "white"), ("star", "domain")])],
                      lockup_state=1))


# ---- Mass 5, Shielded Trail: every ordinary prism the hull lays arrives shielded -------------------------------------------
# The blocks stay WHITE and the SHAPE says shielded: the first draft tinted them the domain's shielded colour, and on this
# card's plate (full blue while Creation is on) the Jade shielded blue all but vanished at 40 px.
def _halves_radial(cx, cy, s, ang, gap=1.8):
    """The fleet's prism (motifs.prism: the diamond cut along its long axis into two close triangles), its long axis
    along `ang` (radians) - never the whole rhombus (the Time petal's shape)."""
    ux, uy = math.cos(ang), math.sin(ang)
    px, py = -uy, ux
    out = empty()
    for sgn in (-1, 1):
        ox, oy = cx + px * gap / 2 * sgn, cy + py * gap / 2 * sgn
        out |= fill([(ox + ux * s * 1.25, oy + uy * s * 1.25), (ox - ux * s * 1.25, oy - uy * s * 1.25),
                     (ox + px * s * 0.95 * sgn, oy + py * s * 0.95 * sgn)])
    return out


def _gilded(cx, cy, s, ang):
    """A square block whose OUTER side is pointed (the Tiger Moth's Gilded Wake: 'octahedrons on the outside but keep them
    square edge on the interior'), with the negative-space X kept on its square part."""
    ux, uy = math.cos(ang), math.sin(ang)
    px, py = -uy, ux
    P = lambda a, b: (cx + ux * a + px * b, cy + uy * a + py * b)
    body = fill([P(-s, -s), P(s * 0.55, -s), P(s * 1.45, 0), P(s * 0.55, s), P(-s, s)])
    cut = max(1.2, s * 0.28)
    x = stroke([P(-s, -s), P(s * 0.55, s)], w=cut) | stroke([P(-s, s), P(s * 0.55, -s)], w=cut)
    return body & ~x


def _framed(cx, cy, s):
    """A square prism with an armour frame: the X'd square inset inside a solid rim."""
    outer = fill([(cx - s, cy - s), (cx + s, cy - s), (cx + s, cy + s), (cx - s, cy + s)])
    g = max(1.3, s * 0.22)
    inner_s = s - g * 1.6
    hole = fill([(cx - s + g, cy - s + g), (cx + s - g, cy - s + g), (cx + s - g, cy + s - g), (cx - s + g, cy + s - g)])
    return (outer & ~hole) | prism_sq(cx, cy, max(1.0, inner_s))


def _a_trail_pts():
    pts = []
    for k in range(5):
        a = math.radians(-90 + 72 * k + 8)
        for j, (r, s) in enumerate([(A_CR + 10, 3.6), (A_CR + 24, 5.4), (A_CR + 41, 7.4)]):
            aj = a + math.radians(6 * j)
            pts.append((64 + r * math.cos(aj), 64 + r * math.sin(aj), s, aj))
    return pts


def shielded_trails(shape, girth=1.0):
    out = empty()
    for x, y, s, aj in _a_trail_pts():
        s *= girth
        out |= (_halves_radial(x, y, s, aj) if shape == "halves" else _gilded(x, y, s, aj) if shape == "gilded"
                else _framed(x, y, s))
    return _clear_of(out, A_STAR(), px=2.0)


MASS5_GAUGE = {"today: Creation on": (1.0, None), "Mass 5: shielded": (1.0, None), "Mass 10 + 5": (1.0, None)}
for key, shape, title in (
        ("a", "halves", "A  the fleet's shielded prism: every block the two close triangles (white)"),
        ("b", "gilded", "B  gilded: every block pointed on its outer side (the Tiger Moth's Gilded Wake; white)"),
        ("c", "framed", "C  armoured: every block framed in a solid rim (white)")):
    CARDS.append(dict(name=f"mass5_{key}", group="mass5", element="Mass", gauge=MASS5_GAUGE, before=None, title=title,
                      parts=dict(star=A_STAR, trail=a_trails, sh=(lambda sh=shape: shielded_trails(sh)),
                                 shfat=(lambda sh=shape: shielded_trails(sh, 1.45))),
                      states=[("today: Creation on", [("trail", "white"), ("star", "domain")]),
                              ("Mass 5: shielded", [("sh", "white"), ("star", "domain")]),
                              ("Mass 10 + 5", [("shfat", "white"), ("star", "domain")])],
                      lockup_state=1))


# ---- Space 5, Explosive Rounds: a round a prism stops bursts, breaking the prisms round it ----------------------------------
SB_AT = (106.0, 64.0, 19.0)      # where the meeting rounds converge ahead of the hull


def burst_broken():
    # B  on its real target: a burst where the rounds meet, the prism it broke a negative-space pair of halves inside it.
    cx, cy, r = SB_AT
    b = motifs.burst(cx, cy, r, spikes=9, inner=0.55, rot=-90)
    hole = prism_pair(cx, cy, r * 0.46, gap=3.2)
    return b & ~hole


def burst_plain():
    # C  the burst alone where the rounds meet.
    cx, cy, r = SB_AT
    return motifs.burst(cx, cy, r, spikes=9, inner=0.55, rot=-90)


def rounds_bursting(meet=False):
    # A  every round a burst: the lead round of each gun's pair drawn as a small blast.
    out = empty()
    for k in range(5):
        y0 = _sa_corner_y(k)
        for j, t in enumerate((0.3, 0.9)):
            x = SA_CX + 30 + 44 * t
            spread = (y0 - 64) * (1.0 + (0.9 if not meet else -0.8) * t)
            y = 64 + spread
            out |= (motifs.burst(x, y, 7.5, spikes=7, inner=0.5, rot=-90) if j == 1
                    else round_shot(x, y, 6.0, x - SA_CX, spread - (y0 - 64), tail=1.8))
    return _clear_of(out, SA_STAR(), px=1.5)


def _clear_burst(rounds_draw):
    return lambda: _clear_of(rounds_draw(), motifs.burst(*SB_AT, spikes=9, inner=0.55, rot=-90), px=2.0)


SPACE5_GAUGE = {"today: stick in": (0.55, None), "Space 5: stick in": (0.55, None), "Space 5: stick out": (0.35, None)}
CARDS += [
    dict(name="space5_a", group="space5", element="Space", gauge=SPACE5_GAUGE, before=None,
         title="A  every round a burst: each gun's lead round (white) drawn as a small blast",
         parts=dict(star=SA_STAR, meet=lambda: sa_rounds(meet=True), bmeet=lambda: rounds_bursting(True),
                    bout=rounds_bursting),
         states=[("today: stick in", [("meet", "white"), ("star", "domain")]),
                 ("Space 5: stick in", [("bmeet", "white"), ("star", "domain")]),
                 ("Space 5: stick out", [("bout", "white"), ("star", "domain")])], lockup_state=1),
    dict(name="space5_b", group="space5", element="Space", gauge=SPACE5_GAUGE, before=None,
         title="B  on its target: a burst (white) where the rounds meet, the prism it broke a pair of halves cut out of it",
         parts=dict(star=SA_STAR, meet=lambda: sa_rounds(meet=True), cmeet=_clear_burst(lambda: sa_rounds(meet=True)),
                    cout=_clear_burst(sa_rounds), burst=burst_broken),
         states=[("today: stick in", [("meet", "white"), ("star", "domain")]),
                 ("Space 5: stick in", [("cmeet", "white"), ("burst", "white"), ("star", "domain")]),
                 ("Space 5: stick out", [("cout", "white"), ("burst", "white"), ("star", "domain")])], lockup_state=1),
    dict(name="space5_c", group="space5", element="Space", gauge=SPACE5_GAUGE, before=None,
         title="C  the burst alone (white) where the rounds meet",
         parts=dict(star=SA_STAR, meet=lambda: sa_rounds(meet=True), cmeet=_clear_burst(lambda: sa_rounds(meet=True)),
                    cout=_clear_burst(sa_rounds), burst=burst_plain),
         states=[("today: stick in", [("meet", "white"), ("star", "domain")]),
                 ("Space 5: stick in", [("cmeet", "white"), ("burst", "white"), ("star", "domain")]),
                 ("Space 5: stick out", [("cout", "white"), ("burst", "white"), ("star", "domain")])], lockup_state=1),
]


# ---- Time 5, Speed Roll: in Speed Mode the right stick rolls the hull and pulls the nose -------------------------------------
T_CR = 25.0 + 6.0                # the roll arc's radius round the star (T_BOX 50 -> the star's radius 25)


def roll_arc(a0=-150.0, a1=-62.0, w=4.6):
    """An arc over the star round its nose axis, an arrowhead at its leading end: the hull ROLLING."""
    arc = band(T_CX, 64, T_CR, a0, a1, w=w)
    ta = math.radians(a1)
    tip = (T_CX + T_CR * math.cos(ta + 0.22), 64 + T_CR * math.sin(ta + 0.22))
    tangent = (-math.sin(ta), math.cos(ta))
    return arc | motifs.arrowhead(tip, tangent, 10.0, spread=0.7)


def _rect_along(cx, cy, hl, hh, ang, cut=2.6):
    c, s = math.cos(ang), math.sin(ang)
    P = lambda a, b: (cx + c * a - s * b, cy + s * a + c * b)
    box_ = fill([P(-hl, -hh), P(hl, -hh), P(hl, hh), P(-hl, hh)])
    x = stroke([P(-hl, -hh), P(hl, hh)], w=cut) | stroke([P(hl, -hh), P(-hl, hh)], w=cut)
    return box_ & ~x


def bent_trail():
    """The stretched trail swept round a curve into the star: the hull has turned."""
    out = empty()
    # an arc of radius R centred below-left, ending at the star's tail heading right
    R, end = 70.0, (T_CX - 22, 64.0)
    ccx, ccy = end[0], end[1] + R
    for k in range(4):
        th = math.radians(-90 - 15 - 22 * k)          # walking back round the arc from the star
        x, y = ccx + R * math.cos(th), ccy + R * math.sin(th)
        out |= _rect_along(x, y, 9.0, 7.0, th + math.pi / 2)
    return _clear_of(out, T_STAR(), px=2.0)


TIME5_GAUGE = {"today: Speed Mode on": (1.0, None), "Time 5: Speed Mode on": (1.0, None)}
CARDS += [
    dict(name="time5_a", group="time5", element="Time", gauge=TIME5_GAUGE, before=None,
         title="A  the roll: an arc (white) over the star round its nose, the stretched trail kept",
         parts=dict(star=T_STAR, fast=lambda: t_trail(2.3), roll=roll_arc),
         states=[("today: Speed Mode on", [("fast", "white"), ("star", "domain")]),
                 ("Time 5: Speed Mode on", [("fast", "white"), ("roll", "white"), ("star", "domain")])], lockup_state=1),
    dict(name="time5_b", group="time5", element="Time", gauge=TIME5_GAUGE, before=None,
         title="B  the turn: the stretched trail (white) swept round a curve into the star",
         parts=dict(star=T_STAR, fast=lambda: t_trail(2.3), bent=bent_trail),
         states=[("today: Speed Mode on", [("fast", "white"), ("star", "domain")]),
                 ("Time 5: Speed Mode on", [("bent", "white"), ("star", "domain")])], lockup_state=1),
    dict(name="time5_c", group="time5", element="Time", gauge=TIME5_GAUGE, before=None,
         title="C  roll and turn: the swept trail and the roll arc together",
         parts=dict(star=T_STAR, fast=lambda: t_trail(2.3), bent=bent_trail, roll=roll_arc),
         states=[("today: Speed Mode on", [("fast", "white"), ("star", "domain")]),
                 ("Time 5: Speed Mode on", [("bent", "white"), ("roll", "white"), ("star", "domain")])], lockup_state=1),
]


# ==== the LEVEL-5 states in the game (Shombith picked B A C C, 2026-11-03) ===============================================
# Each is a part that appears only once its upgrade is live (BrittleStarHUDView.SetAbilityUpgraded keeps the four flags):
#   Charge 5  B  the sabers thrown, tumbling past turrets  ChargeSabersFree takes the sabers' alpha
#   Mass 5    A  every block the fleet's shielded prism    MassTrailsShielded(Fat) take the trails' alpha
#   Space 5   C  a burst where the rounds meet             SpaceBurst, and the rounds swap to copies cleared round it
#   Time 5    C  the trail swept round a turn + the roll   TimeTrailTurn takes the stretched trail's alpha; TimeRoll with it
# Draw order: appended children draw over the existing ones; none of these overlaps the star.
def _rounds_clear(draw):
    return lambda: _clear_of(draw(), burst_plain(), px=2.0)


L5_PARTS = {
    "Charge": [("ChargeSabersFree", "BrittleStar_ChargeSabersFree", "chargeSabersFreeIcon", thrown_tumble,
                "Charge 5 Thrown Sabers: the five sabers thrown out past their turrets, tumbling (white; the sabers' alpha)")],
    "Mass": [("MassTrailsShielded", "BrittleStar_MassTrailsShielded", "massTrailsShieldedIcon",
              lambda: shielded_trails("halves"),
              "Mass 5 Shielded Trail: every block the fleet's shielded prism, two close triangles (white)"),
             ("MassTrailsShieldedFat", "BrittleStar_MassTrailsShieldedFat", "massTrailsShieldedFatIcon",
              lambda: shielded_trails("halves", 1.45), "Mass 5 Shielded Trail with Mass's fat blocks (white)")],
    "Space": [("SpaceBurst", "BrittleStar_SpaceBurst", "spaceBurstIcon", burst_plain,
               "Space 5 Explosive Rounds: the burst where the rounds meet (white; the rounds' alpha)")],
    "Time": [("TimeTrailTurn", "BrittleStar_TimeTrailTurn", "timeTrailTurnIcon", bent_trail,
              "Time 5 Speed Roll: the stretched trail swept round a turn (white; the stretched trail's alpha)"),
             ("TimeRoll", "BrittleStar_TimeRoll", "timeRollIcon", roll_arc,
              "Time 5 Speed Roll: the roll arc over the star (white; the stretched trail's alpha)")],
}
# Space 5 swaps the round parts' SPRITES to copies cleared round the burst (sprite fields, not children).
SPACE_CLEAR = [("BrittleStar_SpaceRoundsBurst", "spaceRoundsBurstSprite", sa_rounds),
               ("BrittleStar_SpaceRoundsMeetBurst", "spaceRoundsMeetBurstSprite", lambda: sa_rounds(meet=True)),
               ("BrittleStar_SpaceRoundsFarBurst", "spaceRoundsFarBurstSprite", lambda: sa_rounds(reach=2.0))]
for element, parts in L5_PARTS.items():
    ICONS += [dict(name=i, element=element, act=act, draw=draw, subdir="Parts", family=False, exempt=PART_EXEMPT)
              for _n, i, _f, draw, act in parts]
ICONS += [dict(name=i, element="Space", act="Space 5 Explosive Rounds: the rounds cleared round the burst (white)",
               draw=_rounds_clear(draw), subdir="Parts", family=False, exempt=PART_EXEMPT)
          for i, _f, draw in SPACE_CLEAR]

_L5_SLOTS = {"Charge": (CHARGE_ICON, "chargeStarIcon"), "Mass": (MASS_ICON, "massStarIcon"),
             "Space": (SPACE_ICON, "spaceStarIcon"), "Time": (TIME_ICON, "timeStarIcon")}
for element, parts in L5_PARTS.items():
    container, after = _L5_SLOTS[element]
    step = dict(file=VARIANT, pair=dict(container=container, view=VIEW, after=after,
                                        children=[(n, i, f) for n, i, f, _d, _a in parts]))
    if element == "Space":
        step["pair"]["fields"] = {f: ("icon", i) for i, f, _d in SPACE_CLEAR}
    REBIND.append(step)

# The shipped cards' states sheets gain their level-5 state.
for c in CARDS:
    if c.get("group"):
        continue
    if c["name"] == "charge":
        c["parts"] = dict(c["parts"], free=thrown_tumble)
        c["states"] = c["states"] + [("Charge 5: RT pulled (Thrown Sabers)", [("free", "white"), ("star", "domain")])]
        c["gauge"] = dict(c["gauge"], **{"Charge 5: RT pulled (Thrown Sabers)": (1.0, None)})
    elif c["name"] == "mass":
        c["parts"] = dict(c["parts"], sh=lambda: shielded_trails("halves"))
        c["states"] = c["states"] + [("Mass 5: Shielded Trail", [("sh", "white"), ("star", "domain")])]
        c["gauge"] = dict(c["gauge"], **{"Mass 5: Shielded Trail": (1.0, None)})
    elif c["name"] == "space":
        c["parts"] = dict(c["parts"], cmeet=_rounds_clear(lambda: sa_rounds(meet=True)), burst=burst_plain)
        c["states"] = c["states"] + [("Space 5: Explosive Rounds", [("cmeet", "white"), ("burst", "white"),
                                                                     ("star", "domain")])]
        c["gauge"] = dict(c["gauge"], **{"Space 5: Explosive Rounds": (0.55, None)})
    elif c["name"] == "time":
        c["parts"] = dict(c["parts"], turn=bent_trail, roll=roll_arc)
        c["states"] = c["states"] + [("Time 5: Speed Roll", [("turn", "white"), ("roll", "white"), ("star", "domain")])]
        c["gauge"] = dict(c["gauge"], **{"Time 5: Speed Roll": (1.0, None)})

# The owner picked (Shombith, 2026-11-03: "B A C C"); the losers' option sheets go.
CARDS[:] = [c for c in CARDS if c.get("group") not in ("charge5", "mass5", "space5", "time5")]

