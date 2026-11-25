"""The Thresher's ability row (R_VesselActions/THRESHER.md §3), redrawn through the REAL hull in parts the HUD drives.

The roster pass (2026-11-07): every card draws the shark from `Thresher.fbx` with its whip tail cut where the chain takes
over (the tail "hangs along the chain like its last links"), the chain as round links, and the morning-star ball. Picks
made by the session on Garrett's go-ahead to choose (one composition per card, drawn straight to the final):

  Charge  Wrecking Ball  the shark (domain) towing the ball into a rival prism (danger). Cool, the ball is white and the
                         prism stands; at smash speed the ball is red-hot (danger) and the prism splits round a white
                         blast. Charge 5 Magnet: two field arcs in front of the ball. The plate stays the ball's heat.
  Mass    Mass Mode      the ball laying its own prisms (domain) behind it. Off (breaking), the ball is white and the row
                         is faint; on, the ball wears the domain and the row is solid. Mass 5 Armoured Trail: white
                         shield rims on the row.
  Space   Winch          the chain's rigidity: released it SAGS (faint links bowed out), pulled it runs straight and the
                         ball swings (a white swing arc behind it). Space 5 Breaker: a super-shielded prism (a square
                         with a negative-space X) ahead of the swinging ball. The plate stays the chain's banked energy.
  Time    Plant          the ball stopped as a pivot (white) with the shark (domain) on the orbit round it: faint while
                         flying free, solid with the chain to the pivot while planted. Time 5 is open (no upgrade yet).
  Omni    Windmill       the ball circling the shark (from above, nose up), round the ship (one white sprite).

Before: Thresher_WreckingBall / _MassMode / _Winch / _Plant (2026-10-13 own-vessel pass); they stay as the slot sprites
under the parts, which the view hides by alpha.
"""
import math
import os

import mesh_silhouette
import numpy as np
import toy_portraits
from canvas import band, disc, empty, shift
from motifs import burst, prism_halves, prism_square_x, spiked_ball
from PIL import Image

HULL = "Thresher"
FBX = "Assets/_Models/Vessel Models/Thresher.fbx"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Thresher/"
_S = {}


def _shark(box, cx, cy, roll=0.0, pitch=-90, yaw=0, mirror=False):
    k = (box, cx, cy, roll, pitch, yaw, mirror)
    if k not in _S:
        _S[k] = mesh_silhouette.silhouette(FBX, pitch=pitch, yaw=yaw, box=box, cx=cx, cy=cy, roll=roll, dilate_px=1.0,
                                           mirror=mirror)
    return _S[k].copy()


def _cut(m, ux, uy, keep):
    """Keep the front `keep` of the hull along its nose axis (ux, uy): the whip tail is cut where the chain begins."""
    yy, xx = np.mgrid[0:m.shape[0], 0:m.shape[1]] / 8.0
    p = (xx - 64) * ux + (yy - 64) * uy
    ys, xs = np.nonzero(m)
    pm = (xs / 8 - 64) * ux + (ys / 8 - 64) * uy
    cut = pm.max() - (pm.max() - pm.min()) * keep
    return m & (p >= cut), (64 + ux * cut, 64 + uy * cut)


def _place(m, tip, bx, by):
    ys, xs = np.nonzero(m)
    cx, cy = (xs.min() + xs.max()) / 16, (ys.min() + ys.max()) / 16
    return shift(m, bx - cx, by - cy), (tip[0] + bx - cx, tip[1] + by - cy)


def shark_side(box, bx, by, roll, keep=0.5, mirror=False):
    """The shark side-on (nose right, or left when mirrored), turned `roll` clockwise, tail cut; body centred on (bx, by).
    Returns (mask, the point where the chain leaves the cut tail)."""
    m = _shark(box, 64, 64, roll=roll, mirror=mirror)
    a = math.radians(roll + (180 if mirror else 0))
    return _place(*_cut(m, math.cos(a), math.sin(a), keep), bx, by)


def shark_top(box, bx, by, roll, keep=0.55):
    """The shark from above (nose along `roll`, clockwise from +x), tail cut."""
    m = _shark(box, 64, 64, roll=roll, pitch=0)
    a = math.radians(roll)
    return _place(*_cut(m, math.cos(a), math.sin(a), keep), bx, by)


def ball(cx, cy, r):
    """The morning star: a core with ten short spikes (ThresherBallForm)."""
    return spiked_ball(cx, cy, r, spikes=10, spike=0.5, rot=10, width=0.36)


def links(p0, p1, n, r=3.6):
    out = empty()
    for i in range(n):
        t = (i + 0.5) / n
        out |= disc(p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, r)
    return out


def bez_links(p0, p1, p2, n, r=3.6):
    out = empty()
    for i in range(n):
        t = (i + 0.5) / n
        out |= disc((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                    (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1], r)
    return out


def grown(m, px):
    return toy_portraits.grown(m, px)


def _png(name):
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, ICON_DIR + name + ".png")).convert("RGBA").split()[3]
    return np.asarray(a.resize((1024, 1024), Image.LANCZOS)) > 127


# ---- Charge: Wrecking Ball -------------------------------------------------------------------------------------------------
C_SH = lambda: shark_side(150, 80, 40, -28)   # noqa: E731
C_BALL = (30, 96)


def c_shark():
    return C_SH()[0]


def c_chain():
    return links(C_SH()[1], (34, 80), 3)


def c_ball():
    return ball(*C_BALL, 18)


def c_prism():
    return prism_halves(90, 100, 15)


def c_split():
    return prism_halves(94, 100, 15, gap=12)


def c_blast():
    return burst(94, 100, 24, spikes=9, inner=0.5, rot=8) & ~grown(c_split(), 2.4)


def c_magnet():
    out = empty()
    for r in (28, 36):
        out |= band(C_BALL[0], C_BALL[1], r, -50, 20, w=4.5)
    return out & ~grown(c_chain(), 2) & ~grown(c_shark(), 2)


# ---- Mass: Mass Mode --------------------------------------------------------------------------------------------------------
M_SH = lambda: shark_side(124, 42, 30, 30, mirror=True)   # noqa: E731
M_BALL = (60, 72)
M_PR = [(84, 90), (100, 104), (114, 117)]


def m_shark():
    return M_SH()[0]


def m_chain():
    return links(M_SH()[1], (M_BALL[0] + 2, M_BALL[1] - 15), 2, 3.4)


def m_ball():
    return ball(*M_BALL, 15)


def m_row():
    out = empty()
    for i, (x, y) in enumerate(M_PR):
        out |= prism_halves(x, y, 12 - i * 1.5, gap=1.8)
    return out


def m_shield():
    p = m_row()
    return grown(p, 4.0) & ~grown(p, 2.2)


# ---- Space: Winch -----------------------------------------------------------------------------------------------------------
S_SH = lambda: shark_side(150, 46, 26, 0, mirror=True)   # noqa: E731
S_BALL = (82, 98)


def s_shark():
    return S_SH()[0]


def _s_end():
    return S_BALL[0] - 6, S_BALL[1] - 17


def s_slack():
    t, e = S_SH()[1], _s_end()
    return bez_links(t, (t[0] + 30, (t[1] + e[1]) / 2 + 6), e, 6)


def s_rigid():
    return links(S_SH()[1], _s_end(), 5)


def s_ball():
    return ball(*S_BALL, 16)


def s_swing():
    t = S_SH()[1]
    r = math.hypot(S_BALL[0] - t[0], S_BALL[1] - t[1])
    a = math.degrees(math.atan2(S_BALL[1] - t[1], S_BALL[0] - t[0]))
    return band(t[0], t[1], r, a + 22, a + 58, w=5) & ~grown(s_ball(), 3)


def s_breaker():
    return prism_square_x(110, 80, 12, cut=3.0)


# ---- Time: Plant ------------------------------------------------------------------------------------------------------------
T_C, T_R, T_A = (62, 68), 42, -55


def _t_pos(r=T_R):
    a = math.radians(T_A)
    return T_C[0] + r * math.cos(a), T_C[1] + r * math.sin(a)


def t_shark():
    return shark_top(92, *_t_pos(), T_A + 90)[0]


def t_ball():
    return ball(*T_C, 14)


def t_orbit():
    return band(T_C[0], T_C[1], T_R, T_A + 30, T_A + 330, w=6) & ~grown(t_shark(), 3)


def t_chain():
    return links(_t_pos(17), _t_pos(T_R - 13), 2)


def t_ward():
    """Time 5 Anchor Ward: a white ward arc outside the orbit, between the pivot and the danger prism it turns."""
    return band(T_C[0], T_C[1], 52, 108, 162, w=7)


def t_danger():
    """Time 5 Anchor Ward: the arena's danger prism the ward turns away, outside the arc."""
    a = math.radians(135)
    return prism_square_x(T_C[0] + 64 * math.cos(a), T_C[1] + 64 * math.sin(a), 11, cut=2.6)


# ---- Omni: Windmill ---------------------------------------------------------------------------------------------------------
def windmill():
    hull = shark_top(118, 64, 66, -90, keep=0.55)[0]
    ring = band(64, 64, 50, -200, 60, w=8) & ~grown(hull, 3)
    a = math.radians(-45)
    b = ball(64 + 50 * math.cos(a), 64 + 50 * math.sin(a), 13)
    return hull | (ring & ~grown(b, 3)) | b


PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}

WRECK_PARTS = [("WreckShark", "Thresher_WreckShark", "wreckSharkIcon", c_shark, "the shark towing (domain)"),
               ("WreckChain", "Thresher_WreckChain", "wreckChainIcon", c_chain, "the chain (white)"),
               ("WreckBall", "Thresher_WreckBall", "wreckBallIcon", c_ball, "the ball (white; danger at smash speed)"),
               ("WreckPrism", "Thresher_WreckPrism", "wreckPrismIcon", c_prism, "the rival prism ahead (danger)"),
               ("WreckSplit", "Thresher_WreckSplit", "wreckSplitIcon", c_split, "the prism split by a smash (danger)"),
               ("WreckBlast", "Thresher_WreckBlast", "wreckBlastIcon", c_blast, "the smash's blast (white)"),
               ("WreckMagnet", "Thresher_WreckMagnet", "wreckMagnetIcon", c_magnet,
                "Magnet: the field that draws the ball (white; Charge 5)")]
MASS_PARTS = [("MassShark", "Thresher_MassShark", "massSharkIcon", m_shark, "the shark towing (domain)"),
              ("MassChain", "Thresher_MassChain", "massChainIcon", m_chain, "the chain (white)"),
              ("MassBall", "Thresher_MassBall", "massBallIcon", m_ball, "the ball (white; domain in Mass Mode)"),
              ("MassRow", "Thresher_MassRow", "massRowIcon", m_row, "the prisms it lays (domain; faint off Mass Mode)"),
              ("MassShield", "Thresher_MassShield", "massShieldIcon", m_shield,
               "Armoured Trail: the row's shield rims (white; Mass 5)")]
WINCH_PARTS = [("WinchShark", "Thresher_WinchShark", "winchSharkIcon", s_shark, "the shark (domain)"),
               ("WinchSlack", "Thresher_WinchSlack", "winchSlackIcon", s_slack, "the chain slack, sagging (white, faint)"),
               ("WinchRigid", "Thresher_WinchRigid", "winchRigidIcon", s_rigid, "the chain pulled rigid (white)"),
               ("WinchBall", "Thresher_WinchBall", "winchBallIcon", s_ball, "the ball (white)"),
               ("WinchSwing", "Thresher_WinchSwing", "winchSwingIcon", s_swing, "the ball's swing (white; rigid)"),
               ("WinchBreaker", "Thresher_WinchBreaker", "winchBreakerIcon", s_breaker,
                "Breaker: a super-shielded prism in the ball's path (white; Space 5)")]
PLANT_PARTS = [("PlantShark", "Thresher_PlantShark", "plantSharkIcon", t_shark, "the shark on its orbit (domain)"),
               ("PlantOrbit", "Thresher_PlantOrbit", "plantOrbitIcon", t_orbit, "the orbit (white; faint flying free)"),
               ("PlantChain", "Thresher_PlantChain", "plantChainIcon", t_chain, "the chain to the pivot (white; planted)"),
               ("PlantBall", "Thresher_PlantBall", "plantBallIcon", t_ball, "the ball stopped as a pivot (white)"),
               ("PlantWard", "Thresher_PlantWard", "plantWardIcon", t_ward,
                "Anchor Ward: the ward round the planted orbit (white; Time 5)"),
               ("PlantDanger", "Thresher_PlantDanger", "plantDangerIcon", t_danger,
                "Anchor Ward: the danger prism the ward turns away (danger; Time 5)")]

ICONS = []
for element, parts in (("Charge", WRECK_PARTS), ("Mass", MASS_PARTS), ("Space", WINCH_PARTS), ("Time", PLANT_PARTS)):
    ICONS += [dict(name=i, element=element, act=act, draw=draw, subdir="Parts", family=False, exempt=PART_EXEMPT)
              for _n, i, _f, draw, act in parts]
ICONS += [dict(name="Thresher_Windmill", element="Omni", subdir="Core", family=False, draw=windmill,
               act="The OMNI crystal card: Windmill - the ball circling the shark across its flight (one white sprite)",
               exempt={"thin": "the shark's slim body and fins", "specks": "the fin tips"})]

_parts = lambda lst: {n: d for n, _i, _f, d, _a in lst}   # noqa: E731
WRECK_COOL = [("WreckShark", "domain"), ("WreckChain", "white"), ("WreckBall", "white"), ("WreckPrism", "danger")]
WRECK_HOT = [("WreckShark", "domain"), ("WreckChain", "white"), ("WreckBall", "danger"), ("WreckSplit", "danger"),
             ("WreckBlast", "white")]
MASS_ON = [("MassShark", "domain"), ("MassChain", "white"), ("MassBall", "domain"), ("MassRow", "domain")]
WINCH_RIGID = [("WinchShark", "domain"), ("WinchRigid", "white"), ("WinchBall", "white"), ("WinchSwing", "white")]
PLANTED = [("PlantShark", "domain"), ("PlantOrbit", "white"), ("PlantChain", "white"), ("PlantBall", "white")]

CARDS = [
    dict(name="wreck", element="Charge", before=ICON_DIR + "Thresher_WreckingBall.png", before_gauge=True,
         gauge={"cruising": (0.3, None), "smash speed": (1.0, None), "Charge 5: Magnet": (1.0, None)},
         title="Wrecking Ball (Charge): the shark (domain) tows the ball into a rival prism (danger); at smash speed the ball "
               "is red-hot and the prism splits; Magnet draws it; the plate is the ball's heat",
         parts=_parts(WRECK_PARTS),
         states=[("cruising", WRECK_COOL), ("smash speed", WRECK_HOT),
                 ("Charge 5: Magnet", WRECK_HOT + [("WreckMagnet", "white")])],
         lockup_state=1),
    dict(name="massmode", element="Mass", before=ICON_DIR + "Thresher_MassMode.png",
         title="Mass Mode (Mass): the ball lays its own prisms behind it; off, the ball breaks and the row is faint; "
               "Armoured Trail rims the row",
         parts=_parts(MASS_PARTS),
         states=[("breaking (Mass Mode off)", [("MassShark", "domain"), ("MassChain", "white"), ("MassBall", "white"),
                                               ("MassRow", "domain", 0.35)]),
                 ("Mass Mode", MASS_ON),
                 ("Mass 5: Armoured Trail", MASS_ON + [("MassShield", "white")])],
         lockup_state=1),
    dict(name="winch", element="Space", before=ICON_DIR + "Thresher_Winch.png", before_gauge=True,
         gauge={"slack": (0.2, None), "pulled rigid": (0.6, None), "Space 5: Breaker": (0.6, None)},
         title="Winch (Space): released the chain sags; pulled it runs rigid and the ball swings; Breaker cracks a "
               "super-shielded prism; the plate is the chain's banked energy",
         parts=_parts(WINCH_PARTS),
         states=[("slack", [("WinchShark", "domain"), ("WinchSlack", "white", 0.7), ("WinchBall", "white")]),
                 ("pulled rigid", WINCH_RIGID),
                 ("Space 5: Breaker", WINCH_RIGID + [("WinchBreaker", "white")])],
         lockup_state=1),
    dict(name="plant", element="Time", before=ICON_DIR + "Thresher_Plant.png",
         title="Plant (Time): the ball stopped as a pivot with the shark on the orbit round it; faint flying free; "
               "Anchor Ward turns the arena's danger prisms while planted",
         parts=_parts(PLANT_PARTS),
         states=[("flying free", [("PlantShark", "domain"), ("PlantOrbit", "white", 0.35), ("PlantBall", "white")]),
                 ("planted", PLANTED),
                 ("Time 5: Anchor Ward", PLANTED + [("PlantWard", "white"), ("PlantDanger", "danger")])],
         lockup_state=1),
    dict(name="omni", element="Omni", title="The OMNI crystal card: Windmill",
         parts=dict(i=windmill), states=[("an omni crystal collected", [("i", "white")])]),
]
LOCKUP = dict(
    Charge=dict(parts=_parts(WRECK_PARTS), state=WRECK_COOL, gauge=0.3),
    Mass=dict(parts=_parts(MASS_PARTS), state=MASS_ON),
    Space=dict(parts=_parts(WINCH_PARTS), state=WINCH_RIGID, gauge=0.6),
    Time=dict(parts=_parts(PLANT_PARTS), state=PLANTED),
    Omni=dict(parts=dict(i=windmill), state=[("i", "white")]),
)

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/ThresherHUDVariant.prefab"
VIEW = "5875684227236902712"            # ThresherHUDView on the variant
SLOT = dict(Charge="7700000000000001105", Mass="7700000000000001115", Space="7700000000000001125",
            Time="7700000000000001135")
REBIND = [
    dict(file=VARIANT, pair=dict(container=SLOT["Charge"], view=VIEW, after="chainColor",
                                 children=[(n, i, f) for n, i, f, _d, _a in WRECK_PARTS])),
    dict(file=VARIANT, pair=dict(container=SLOT["Mass"], view=VIEW, after="chainColor",
                                 children=[(n, i, f) for n, i, f, _d, _a in MASS_PARTS])),
    dict(file=VARIANT, pair=dict(container=SLOT["Space"], view=VIEW, after="chainColor",
                                 children=[(n, i, f) for n, i, f, _d, _a in WINCH_PARTS])),
    dict(file=VARIANT, pair=dict(container=SLOT["Time"], view=VIEW, after="chainColor",
                                 children=[(n, i, f) for n, i, f, _d, _a in PLANT_PARTS],
                                 fields={"omniAbilitySprite": ("icon", "Thresher_Windmill")})),
]
