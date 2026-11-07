"""The Urchin's four ability icons (Resources/ElementalAbilityMaps/Urchin.asset), angular, one object each.

  Charge  Chain Spikes     the volley: the spiked ball, two spikes already flying off it (kept close to the
                           shipped star, which read; redrawn angular so the family matches)
  Mass    Trail Rider      the Urchin ON the ribbon: the ball sitting on a notched trail band
  Space   Track Projector  the rail thrown out ahead: a track receding to the horizon off the hull's nose
  Time    Slip             letting go: the ball lifted off the band, its ghost still on it

Urchin_ChargeRing.png is NOT an ability icon - it is the hold-charge GAUGE ring the HUD draws round the
Charge card (author_urchin_hud.py) - and stays as it is.
Before (author_urchin_hud.py placeholders, 2026-10): a 12-point star, a ball on a dotted line, a ladder,
three overlapping circles.
"""
from canvas import disc, fill, rect, shift  # noqa: F401
from motifs import hull, rail, spiked_ball, trail_band

HULL = "Urchin"


def chain_spikes():
    ball = spiked_ball(56, 72, 28, spikes=12, spike=0.6, rot=15, width=0.4)
    # Two spikes already flying: darts leaving the ball up and to the right.
    s1 = fill([(92, 36), (122, 8), (104, 44)])
    s2 = fill([(100, 60), (121, 48), (110, 72)])
    return ball | s1 | s2


def trail_rider():
    band = trail_band()
    ball = spiked_ball(64, 58, 24, spikes=12, spike=0.58, rot=15, width=0.4)
    return band | ball


def track_projector():
    # The track as a road to the horizon off the nose: one solid taper, the single track's groove cut down it.
    nose = hull(64, 110, 18, heading=-90)
    track = fill([(64 - 36, 92), (64 + 36, 92), (64 + 9, 8), (64 - 9, 8)])
    for y0, y1, w in ((72, 88, 9), (48, 60, 7), (28, 37, 5.5)):
        track &= ~rect(64 - w / 2, y0, 64 + w / 2, y1)
    return nose | track


def slip():
    band = trail_band(y0=100, y1=116, notches=4)
    ghost = spiked_ball(38, 76, 19, spikes=12, spike=0.58, rot=15, width=0.4)
    lifted = spiked_ball(86, 40, 19, spikes=12, spike=0.58, rot=15, width=0.4)
    return [(band | lifted, 1.0), (ghost, 0.5)]


ICONS = [
    dict(name="Urchin_ChainSpikes", element="Charge", act="Chain Spikes", draw=chain_spikes),
    dict(name="Urchin_TrailRider", element="Mass", act="Trail Rider", draw=trail_rider),
    dict(name="Urchin_TrackProjector", element="Space", act="Track Projector", draw=track_projector),
    dict(name="Urchin_Slip", element="Time", act="Slip", draw=slip),
]


# ==== Round 6 (2026-11-07): the four cards redrawn through the REAL hull, one card at a time ====================================
# The hull: urchan_shapekey_with_animations.fbx seen NOSE-ON (yaw 90): the spiked ball with its six pods round it - the
# two guns left and right, the four engine pods on the diagonals. From above or the side the pods stack and it reads
# as a blob; nose-on it is unmistakably the Urchin.
import math as _math
import os as _os

import mesh_silhouette
import numpy as _np
import toy_portraits
from PIL import Image as _Image
from canvas import N, empty, stroke
from motifs import prism_halves

FBX = "Assets/_Models/Vessel Models/urchan_shapekey_with_animations.fbx"
NOSE_ON = dict(pitch=0, yaw=90, dilate_px=1.0)
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Urchin/"
_SIL = {}


def urchin(box, cx=64.0, cy=64.0):
    def draw():
        key = (box, cx, cy)
        if key not in _SIL:
            _SIL[key] = mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, **NOSE_ON)
        return _SIL[key].copy()
    return draw


def _png(name):
    root = _os.path.abspath(_os.path.join(_os.path.dirname(__file__), "..", "..", "..", ".."))
    a = _Image.open(_os.path.join(root, ICON_DIR + name + ".png")).convert("RGBA").split()[3]
    return _np.asarray(a.resize((N, N), _Image.LANCZOS)) > 127


def _clear_of(mask, *others, px=2.0):
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


def spike(x0, y0, deg, length, w=5.0):
    """One spike in flight: a long thin dart from (x0, y0) along `deg` (0 = +x, clockwise on screen), point first."""
    a = _math.radians(deg)
    ux, uy = _math.cos(a), _math.sin(a)
    px, py = -uy, ux
    tip = (x0 + ux * length, y0 + uy * length)
    return fill([tip, (x0 + px * w / 2, y0 + py * w / 2), (x0 - px * w / 2, y0 - py * w / 2)])


# The row as the HUD draws it today (UrchinVesselHUDView): Charge's plate is the AMMO, Mass's the riding flag.
LOCKUP = dict(
    Charge=dict(parts=dict(i=lambda: _png("Urchin_ChainSpikes")), state=[("i", "white")], gauge=0.7),
    Mass=dict(parts=dict(i=lambda: _png("Urchin_TrailRider")), state=[("i", "white")], gauge=0.0),
    Space=dict(parts=dict(i=lambda: _png("Urchin_TrackProjector")), state=[("i", "white")], gauge=0.0),
    Time=dict(parts=dict(i=lambda: _png("Urchin_Slip")), state=[("i", "white")], gauge=0.0),
)


# ---- Charge: Chain Spikes ---------------------------------------------------------------------------------------------------
# What it does: a TAP is an aimed shotgun out of both guns; a HOLD charges a burst in every direction. Each spike that hits a
# prism steals it and sprays the next generation out of it (a cascade); Charge is how far each spike flies and how deep the
# chain runs. L5 Overcharge: one generation deeper AND no decay (the last generation reaches as far as the first).
# The plate stays the AMMO (the ride refills it); the held charge keeps its own ring round the icon.

# A  the cascade: the Urchin low-left (domain) throws one spike into a rival prism (danger), which sprays the next generation.
A_URCHIN = urchin(62, cx=34, cy=92)
A_HIT = (84, 42)


def _spray(cx, cy, degs, length, w=7.0, r0=0.0):
    out = empty()
    for d in degs:
        a = _math.radians(d)
        out |= spike(cx + r0 * _math.cos(a), cy + r0 * _math.sin(a), d, length, w)
    return out


def a_first():
    return spike(60, 68, -45, 30, 9.0)


def a_prism():
    return prism_halves(A_HIT[0], A_HIT[1], 13.0, gap=2.2)


def a_gen2(overcharged=False):
    return _spray(A_HIT[0], A_HIT[1], (-110, -20, 60), 34 if overcharged else 22, 7.0, r0=15)


def a_gen3():
    # Overcharge's extra generation: the spray reaches as far as the first throw, and runs one link deeper.
    return _spray(A_HIT[0], A_HIT[1], (-65, 20), 30, 6.0, r0=15)


# B  the burst: the Urchin centred (domain), the held charge thrown out in every direction - more spikes the longer the hold.
B_URCHIN = urchin(58)


def b_burst(n, length, r0=34.0, w=8.0):
    out = empty()
    for k in range(n):
        d = -90 + k * 360.0 / n + (180.0 / n if n % 4 == 0 else 0)
        a = _math.radians(d)
        out |= spike(64 + r0 * _math.cos(a), 64 + r0 * _math.sin(a), d, length, w)
    return _clear_of(out, B_URCHIN(), px=2.0)


# C  the chain: a straight run - the Urchin at the left, spike, prism, a SHORTER spike, prism, shorter still: the decay you read.
C_URCHIN = urchin(44, cx=24, cy=64)


def c_chain(overcharged=False):
    out = empty()
    x = 48.0
    lengths = (16, 16, 16) if overcharged else (16, 11, 6)
    for i, L in enumerate(lengths):
        out |= spike(x, 64, 0, L, 9.0 - i * 0.6)
        x += L + 9.0
        out |= prism_halves(x, 64, 9.0 - i * 0.8, gap=2.0)
        x += 10.0
    return out


CHAIN_BEFORE = ICON_DIR + "Urchin_ChainSpikes.png"
CHAIN_GAUGE = {"tap / at rest": (0.7, None), "a held burst": (0.4, None), "Charge 5: Overcharge": (0.4, None)}
CARDS = [
    dict(name="chain_a", group="chain", element="Charge", before=CHAIN_BEFORE, before_gauge=True, gauge=CHAIN_GAUGE,
         title="A  the cascade: the Urchin (domain) throws a spike into a rival prism (danger) that sprays the next generation",
         parts=dict(u=A_URCHIN, s=a_first, p=a_prism, g2=a_gen2, g2o=lambda: a_gen2(True), g3=a_gen3),
         states=[("tap / at rest", [("u", "domain"), ("s", "white"), ("p", "danger"), ("g2", "white", 0.5)]),
                 ("a held burst", [("u", "domain"), ("s", "white"), ("p", "danger"), ("g2", "white")]),
                 ("Charge 5: Overcharge", [("u", "domain"), ("s", "white"), ("p", "danger"), ("g2o", "white"), ("g3", "white")])],
         lockup_state=1),
    dict(name="chain_b", group="chain", element="Charge", before=CHAIN_BEFORE, before_gauge=True, gauge=CHAIN_GAUGE,
         title="B  the burst: the Urchin (domain) face-on, the held charge thrown out all round - more spikes the longer the hold",
         parts=dict(u=B_URCHIN, b4=lambda: b_burst(4, 18), b8=lambda: b_burst(8, 24), b12=lambda: b_burst(12, 30, w=7.0)),
         states=[("tap / at rest", [("u", "domain"), ("b4", "white")]),
                 ("a held burst", [("u", "domain"), ("b8", "white")]),
                 ("Charge 5: Overcharge", [("u", "domain"), ("b12", "white")])],
         lockup_state=1),
    dict(name="chain_c", group="chain", element="Charge", before=CHAIN_BEFORE, before_gauge=True, gauge=CHAIN_GAUGE,
         title="C  the chain: spike, prism, a shorter spike, prism - the decay; Overcharge runs a link further at full length",
         parts=dict(u=C_URCHIN, c=c_chain, co=lambda: c_chain(True)),
         states=[("tap / at rest", [("u", "domain"), ("c", "white", 0.5)]),
                 ("a held burst", [("u", "domain"), ("c", "white")]),
                 ("Charge 5: Overcharge", [("u", "domain"), ("co", "white")])],
         lockup_state=1),
]


# ---- Mass: Trail Rider ------------------------------------------------------------------------------------------------------
# What it does (passive): fly into a trail and latch on; your own prisms GROW under you as you pass, a rival's are STOLEN; the
# ride refills the ammo (double over shielded prisms). Mass is how much each prism grows per pass. L5 Reinforced Wake: the
# prisms you grow arrive shielded. The plate is the RIDING flag (binary: off or on). The trail runs left to right under the
# hull (behind it on the left, ahead on the right), the convention the shipped card used.
M_URCHIN = urchin(56, cx=64, cy=44)
M_LIFT = urchin(48, cx=64, cy=34)
ROW_Y = 98
ROW_X = (12, 38, 64, 90, 116)


def _row(sizes, xs=ROW_X, y=ROW_Y):
    out = empty()
    for x, s in zip(xs, sizes):
        if s:
            out |= prism_halves(x, y, s, gap=2.0)
    return out


def _prism_at(i, s):
    return lambda: prism_halves(ROW_X[i], ROW_Y, s, gap=2.0)


# A  grow: behind the Urchin its own prisms are bigger than the ones ahead - it grew them passing over.
def a_behind():
    return _row((14, 14, 0, 0, 0))


def a_under():
    return _row((0, 0, 12, 0, 0))


def a_ahead():
    return _row((0, 0, 0, 9, 9))


# B  steal: riding a rival's trail - ahead it is theirs (danger), under and behind it is now yours (domain).
def b_mine():
    return _row((11, 11, 11, 0, 0))


def b_theirs():
    return _row((0, 0, 0, 11, 11))


# C  one pass: the prism under the Urchin, its old size outlined inside the grown one - the volume a pass adds.
C_PRISM = (64, 98)


def c_grown(s=20):
    return prism_halves(C_PRISM[0], C_PRISM[1], s, gap=2.6)


def c_was(s=11):
    return prism_halves(C_PRISM[0], C_PRISM[1], s, gap=2.6)


def c_grown_ring(s=20, was=11):
    return c_grown(s) & ~toy_portraits.grown(prism_halves(C_PRISM[0], C_PRISM[1], was, gap=2.6), 2.5)


M_URCHIN_C = urchin(52, cx=64, cy=38)

RIDER_BEFORE = ICON_DIR + "Urchin_TrailRider.png"
RIDER_GAUGE = {"off the trail": (0.0, None), "riding": (1.0, None), "Mass 5: Reinforced Wake": (1.0, None)}
CARDS += [
    dict(name="rider_a", group="rider", element="Mass", before=RIDER_BEFORE, before_gauge=True, gauge=RIDER_GAUGE,
         title="A  grow: the Urchin (domain) riding its trail - the prisms behind it (left) are bigger than the ones ahead",
         parts=dict(u=M_URCHIN, l=M_LIFT, b=a_behind, n=a_under, a=a_ahead),
         states=[("off the trail", [("l", "domain"), ("b", "domain", 0.5), ("n", "domain", 0.5), ("a", "domain", 0.5)]),
                 ("riding", [("u", "domain"), ("b", "domain"), ("n", "white"), ("a", "domain", 0.6)]),
                 ("Mass 5: Reinforced Wake", [("u", "domain"), ("b", "shielded"), ("n", "white"), ("a", "domain", 0.6)])],
         lockup_state=1),
    dict(name="rider_b", group="rider", element="Mass", before=RIDER_BEFORE, before_gauge=True, gauge=RIDER_GAUGE,
         title="B  steal: riding a rival trail - ahead it is theirs (danger), under and behind it the Urchin has made it its own",
         parts=dict(u=M_URCHIN, l=M_LIFT, m=b_mine, t=b_theirs, all=lambda: _row((11, 11, 11, 11, 11))),
         states=[("off the trail", [("l", "domain"), ("all", "danger", 0.6)]),
                 ("riding", [("u", "domain"), ("m", "domain"), ("t", "danger")]),
                 ("Mass 5: Reinforced Wake", [("u", "domain"), ("m", "shielded"), ("t", "danger")])],
         lockup_state=1),
    dict(name="rider_c", group="rider", element="Mass", before=RIDER_BEFORE, before_gauge=True, gauge=RIDER_GAUGE,
         title="C  one pass: the prism under the Urchin, grown - its old size the solid core, what this pass added the rim",
         parts=dict(u=M_URCHIN_C, w=c_was, r=c_grown_ring),
         states=[("off the trail", [("u", "domain", 0.6), ("w", "domain", 0.6)]),
                 ("riding", [("u", "domain"), ("w", "domain"), ("r", "white")]),
                 ("Mass 5: Reinforced Wake", [("u", "domain"), ("w", "domain"), ("r", "shielded")])],
         lockup_state=1),
]


# ---- Space: Track Projector -------------------------------------------------------------------------------------------------
# What it does (LT): lay a straight stretch of single-track trail out ahead - a rail where the map has none, ridden at friendly
# speed, grown as you pass, launched off at the far end. Space is how LONG it is (4 -> 8 s of projection); 20 s cooldown (the
# card's veil). L5 Long Haul: a hundred units further. The hull nose-on reads the same from behind, so here the Urchin is
# seen from BEHIND and the rail runs away from us to the horizon.
from motifs import speed_wedge

S_URCHIN = urchin(50, cx=64, cy=100)
VANISH = (64, 4)


def _receding(n, near_y=70, far_y=8, s_near=17.0, s_far=6.0, full=8):
    """n prisms of a rail that holds `full` at full length, from just ahead of the hull toward the horizon: each one
    smaller and closer to the next, so the stretch reads as running away from us."""
    out = empty()
    for i in range(n):
        t = i / (full - 1)
        y = near_y + (far_y - near_y) * (1 - (1 - t) ** 1.6)
        s = s_near + (s_far - s_near) * t
        out |= prism_halves(64, y, s, gap=max(1.4, s * 0.2), vertical=False)
    return _clear_of(out, S_URCHIN(), px=2.0)


# B  laid out flat: the Urchin at the left, the rail straight out to the right, its length the bracket over it.
SB_URCHIN = urchin(40, cx=22, cy=70)


def b_rail(n):
    out = empty()
    for i in range(n):
        out |= prism_halves(50 + i * 14, 70, 6.5, gap=1.8)
    return out


def b_span(n):
    x1 = 50 + (n - 1) * 14 + 6
    return stroke([(44, 46), (44, 40), (x1, 40), (x1, 46)], w=4.0)


# C  the launch: the rail ahead and the Urchin leaving its far end, two speed marks - the payoff a rail is laid for.
C_RAIL_URCHIN = urchin(36, cx=98, cy=30)


def c_rail(n=4):
    out = empty()
    for i in range(n):
        x, y = 68 - (n - 1 - i) * 16, 62 + (n - 1 - i) * 16
        out |= prism_halves(x, y, 9.0, gap=2.0)
    return out


def c_marks():
    return speed_wedge(56, 52, 18, 5.0, lean=0.0) | speed_wedge(64, 62, 14, 5.0)


TRACK_BEFORE = ICON_DIR + "Urchin_TrackProjector.png"
CARDS += [
    dict(name="track_a", group="track", element="Space", before=TRACK_BEFORE,
         title="A  the rail ahead: the Urchin (domain) from behind, its projected track running away to the horizon",
         parts=dict(u=S_URCHIN, r3=lambda: _receding(3), r5=lambda: _receding(5), r8=lambda: _receding(8)),
         states=[("recharging", [("u", "domain"), ("r3", "white", 0.4)]),
                 ("laid (Space 3)", [("u", "domain"), ("r5", "white")]),
                 ("Space 5: Long Haul", [("u", "domain"), ("r8", "white")])],
         lockup_state=1),
    dict(name="track_b", group="track", element="Space", before=TRACK_BEFORE,
         title="B  laid out flat: the rail straight out from the Urchin, its length the bracket over it",
         parts=dict(u=SB_URCHIN, r3=lambda: b_rail(3), r4=lambda: b_rail(4), r5=lambda: b_rail(5),
                    k4=lambda: b_span(4), k5=lambda: b_span(5)),
         states=[("recharging", [("u", "domain"), ("r3", "white", 0.4)]),
                 ("laid (Space 3)", [("u", "domain"), ("r4", "white"), ("k4", "white")]),
                 ("Space 5: Long Haul", [("u", "domain"), ("r5", "white"), ("k5", "white")])],
         lockup_state=1),
    dict(name="track_c", group="track", element="Space", before=TRACK_BEFORE,
         title="C  the launch: a rail (white) climbing to the Urchin (domain), which flies off its far end",
         parts=dict(u=C_RAIL_URCHIN, r=c_rail, r5=lambda: c_rail(5), m=c_marks),
         states=[("recharging", [("u", "domain", 0.5), ("r", "white", 0.4)]),
                 ("laid (Space 3)", [("u", "domain"), ("r", "white")]),
                 ("Space 5: Long Haul", [("u", "domain"), ("r5", "white")])],
         lockup_state=1),
]


# ---- Time: Slip -------------------------------------------------------------------------------------------------------------
# What it does (B): let go of the trail and phase out for a moment, so you clear the mass you were in instead of being
# re-latched by it. Time is how long the ghost lasts (0.6 -> 1.6 s). L5 Slipstream: a rival's trail no longer slows you, so
# a raid runs at your own speed.
T_ROW = (14, 38, 62, 86, 110)


def t_row(colour_split=None):
    out = empty()
    for x in T_ROW:
        out |= prism_halves(x, 104, 10.0, gap=2.0)
    return out


# A  let go: the Urchin lifted off the trail, its ghost still on it where it was.
TA_GHOST = urchin(44, cx=40, cy=72)
TA_LIFTED = urchin(48, cx=88, cy=36)


def ta_ghost():
    return _clear_of(TA_GHOST(), TA_LIFTED(), px=2.5)


# B  phase through: the Urchin's ghost INSIDE a wall of prisms it is passing through - the mass it would have latched to.
TB_URCHIN = urchin(56)


def tb_wall():
    out = empty()
    for y in (24, 48, 72, 96):
        for x in (24, 104):
            out |= prism_halves(x, y, 10.0, gap=2.0)
    return out


def tb_through():
    # the same wall, but its middle column runs right through the ghost - drawn over it, because the ghost is not there
    out = empty()
    for y in (20, 44, 68, 92, 116):
        out |= prism_halves(64, y, 9.0, gap=2.0)
    return out


# C  the clock: the ghost with the phase's time as an arc round it, shrinking.
TC_URCHIN = urchin(54)


def tc_arc(frac):
    from canvas import band
    return band(64, 64, 46, -90, -90 + 360 * frac, w=7.0)


def t_marks():
    return speed_wedge(30, 24, 26, 6.0) | speed_wedge(38, 40, 18, 6.0)


SLIP_BEFORE = ICON_DIR + "Urchin_Slip.png"
CARDS += [
    dict(name="slip_a", group="slip", element="Time", before=SLIP_BEFORE,
         title="A  let go: the Urchin (domain) lifted off the trail, its ghost (domain, faint) still on it",
         parts=dict(g=ta_ghost, u=TA_LIFTED, r=t_row, m=t_marks),
         states=[("riding", [("g", "domain"), ("r", "domain")]),
                 ("slipping", [("g", "domain", 0.35), ("u", "domain"), ("r", "domain")]),
                 ("Time 5: Slipstream", [("g", "domain", 0.35), ("u", "domain"), ("r", "danger"), ("m", "white")])],
         lockup_state=1),
    dict(name="slip_b", group="slip", element="Time", before=SLIP_BEFORE,
         title="B  phase through: the Urchin's ghost (domain, faint) passing through the trail it would have latched to",
         parts=dict(u=TB_URCHIN, w=tb_through, ug=lambda: _clear_of(TB_URCHIN(), tb_through(), px=1.5)),
         states=[("riding", [("u", "domain"), ("w", "domain", 0.6)]),
                 ("slipping", [("ug", "domain", 0.4), ("w", "white")]),
                 ("Time 5: Slipstream", [("ug", "domain", 0.4), ("w", "danger")])],
         lockup_state=1),
    dict(name="slip_c", group="slip", element="Time", before=SLIP_BEFORE,
         title="C  the clock: the ghost (domain, faint) with the phase's time running down round it",
         parts=dict(u=TC_URCHIN, a=lambda: tc_arc(0.7), a5=lambda: tc_arc(1.0)),
         states=[("riding", [("u", "domain")]),
                 ("slipping", [("u", "domain", 0.4), ("a", "white")]),
                 ("Time 5: Slipstream", [("u", "domain", 0.4), ("a5", "white")])],
         lockup_state=1),
]


# ==== Round 6 in the game: the picks (Garrett, 2026-11-07: "do all of the icons ... treat each icon as an excellent and
# polished thing"; he reviews the roster at the end, so each card is the option recommended on its sheet) =====================
#   Charge  A  the cascade: the Urchin throws a spike into a rival prism that sprays the next generation; Overcharge
#              (Charge 5) runs the spray to full length and one generation deeper. The plate stays the AMMO, the held
#              charge keeps its ring.
#   Mass    B  steal: the Urchin riding a rival's trail - theirs (danger) ahead, made its own (domain) under and behind;
#              off the trail it lifts clear; Reinforced Wake (Mass 5) puts a shield rim on what it made. Plate: RIDING.
#   Space   A  the rail ahead: the Urchin from behind, its projected track running to the horizon; Long Haul (Space 5)
#              runs it further. The fleet's cooldown veil shows the recharge.
#   Time    A  let go: riding, the Urchin sits on its trail; slipping, it is lifted clear with its ghost left behind;
#              Slipstream (Time 5) turns the trail a rival's and adds the speed it keeps there.
#   Omni       the omni crystal itself (the Urchin's omni crystal carries no bespoke effect: it feeds all four elements).
import importlib as _importlib  # noqa: E402

_scarab = _importlib.import_module("specs.scarab")
PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/UrchinHUDVariant.prefab"
VIEW = "7781000000000000002"            # UrchinVesselHUDView on the variant
SLOT = dict(Charge="7781000000000001105", Mass="7781000000000001115", Space="7781000000000001125",
            Time="7781000000000001135")


def _chain_spike():
    return _clear_of(a_first(), A_URCHIN(), a_prism(), px=1.6)


def _chain_prism():
    return a_prism()


CHAIN_PARTS = [("ChainUrchin", "Urchin_ChainUrchin", "chainUrchinIcon", A_URCHIN, "the Urchin nose-on (domain)"),
               ("ChainSpike", "Urchin_ChainSpike", "chainSpikeIcon", _chain_spike, "the spike it throws (white)"),
               ("ChainPrism", "Urchin_ChainPrism", "chainPrismIcon", _chain_prism, "the rival prism it steals (danger)"),
               ("ChainSpray", "Urchin_ChainSpray", "chainSprayIcon", a_gen2,
                "the next generation sprayed out of it, decaying (white; below Charge 5)"),
               ("ChainSprayOver", "Urchin_ChainSprayOver", "chainSprayOverIcon", lambda: a_gen2(True),
                "Overcharge: the spray at full length (white; Charge 5)"),
               ("ChainDeeper", "Urchin_ChainDeeper", "chainDeeperIcon",
                lambda: _clear_of(a_gen3(), a_gen2(True), px=1.4), "Overcharge: one generation deeper (white; Charge 5)")]


RIDER_PARTS = [("RiderUrchin", "Urchin_RiderUrchin", "riderUrchinIcon", M_URCHIN, "the Urchin on the trail (domain)"),
               ("RiderLifted", "Urchin_RiderLifted", "riderLiftedIcon", M_LIFT, "the Urchin off the trail (domain)"),
               ("RiderMine", "Urchin_RiderMine", "riderMineIcon", b_mine,
                "the trail under and behind it, made its own (domain)"),
               ("RiderTheirs", "Urchin_RiderTheirs", "riderTheirsIcon", b_theirs, "the rival's trail ahead (danger)"),
               ("RiderShield", "Urchin_RiderShield", "riderShieldIcon",
                lambda: toy_portraits.grown(b_mine(), 3.4) & ~toy_portraits.grown(b_mine(), 1.6),
                "Reinforced Wake: a shield rim round what it made (white; Mass 5)")]

TRACK_PARTS = [("TrackUrchin", "Urchin_TrackUrchin", "trackUrchinIcon", S_URCHIN, "the Urchin from behind (domain)"),
               ("TrackRail", "Urchin_TrackRail", "trackRailIcon", lambda: _receding(5), "the projected rail (white)"),
               ("TrackLongHaul", "Urchin_TrackLongHaul", "trackLongHaulIcon",
                lambda: _receding(8) & ~toy_portraits.grown(_receding(5), 1.0),
                "Long Haul: the rail run on toward the horizon (white; Space 5)")]

SLIP_PARTS = [("SlipRiding", "Urchin_SlipRiding", "slipRidingIcon", TA_GHOST, "the Urchin riding its trail (domain)"),
              ("SlipGhost", "Urchin_SlipGhost", "slipGhostIcon", ta_ghost,
               "its ghost left on the trail while it slips (domain, faint)"),
              ("SlipLifted", "Urchin_SlipLifted", "slipLiftedIcon", TA_LIFTED, "the Urchin lifted clear (domain)"),
              ("SlipTrail", "Urchin_SlipTrail", "slipTrailIcon", lambda: _clear_of(t_row(), TA_GHOST(), px=1.5),
               "the trail it rides (domain; a rival's, danger, once Slipstream is live)"),
              ("SlipStream", "Urchin_SlipStream", "slipStreamIcon", lambda: _clear_of(t_marks(), TA_LIFTED(), px=2.0),
               "Slipstream: the speed it keeps over a rival's trail (white; Time 5)")]

for element, parts in (("Charge", CHAIN_PARTS), ("Mass", RIDER_PARTS), ("Space", TRACK_PARTS), ("Time", SLIP_PARTS)):
    ICONS += [dict(name=i, element=element, act=act, draw=draw, subdir="Parts", family=False, exempt=PART_EXEMPT)
              for _n, i, _f, draw, act in parts]


def omni_crystal_card():
    return _scarab.omni_crystal(64, 64, 104)


ICONS += [dict(name="Urchin_OmniCrystal", element="Omni", subdir="Core", family=False, draw=omni_crystal_card,
               act="The OMNI crystal card: the omni crystal, which feeds all four of the Urchin's elements (one white sprite)",
               exempt={"components": "the crystal's plates, projected from its own mesh",
                       "specks": "the omni crystal's small triangle plates (as Scarab_BallForge)"})]

CARDS[:] = [c for c in CARDS if not c.get("group")]
_parts = lambda lst: {n: d for n, _i, _f, d, _a in lst}   # noqa: E731
CHAIN_ON = [("ChainUrchin", "domain"), ("ChainSpike", "white"), ("ChainPrism", "danger"), ("ChainSpray", "white")]
RIDING = [("RiderUrchin", "domain"), ("RiderMine", "domain"), ("RiderTheirs", "danger")]
SLIP_RIDING = [("SlipRiding", "domain"), ("SlipTrail", "domain")]
CARDS += [
    dict(name="chain", element="Charge", before=CHAIN_BEFORE, gauge=CHAIN_GAUGE,
         title="Chain Spikes (Charge, picked A): the Urchin (domain) throws a spike into a rival prism (danger) that sprays "
               "the next generation; Overcharge runs it full length and a generation deeper; the plate is the ammo",
         parts=_parts(CHAIN_PARTS),
         states=[("tap / at rest", CHAIN_ON), ("a held burst", CHAIN_ON),
                 ("Charge 5: Overcharge", [("ChainUrchin", "domain"), ("ChainSpike", "white"), ("ChainPrism", "danger"),
                                           ("ChainSprayOver", "white"), ("ChainDeeper", "white")])],
         lockup_state=0),
    dict(name="rider", element="Mass", before=RIDER_BEFORE, gauge=RIDER_GAUGE,
         title="Trail Rider (Mass, picked B): riding a rival's trail - theirs ahead (danger), made its own under and behind "
               "(domain); Reinforced Wake rims what it made; the plate is riding",
         parts=_parts(RIDER_PARTS),
         states=[("off the trail", [("RiderLifted", "domain"), ("RiderMine", "domain", 0.4), ("RiderTheirs", "danger", 0.4)]),
                 ("riding", RIDING),
                 ("Mass 5: Reinforced Wake", RIDING + [("RiderShield", "white")])],
         lockup_state=1),
    dict(name="track", element="Space", before=TRACK_BEFORE,
         title="Track Projector (Space, picked A): the Urchin (domain) from behind, its projected rail (white) running to "
               "the horizon; Long Haul runs it further",
         parts=_parts(TRACK_PARTS),
         states=[("recharging", [("TrackUrchin", "domain"), ("TrackRail", "white", 0.4)]),
                 ("ready", [("TrackUrchin", "domain"), ("TrackRail", "white")]),
                 ("Space 5: Long Haul", [("TrackUrchin", "domain"), ("TrackRail", "white"), ("TrackLongHaul", "white")])],
         lockup_state=1),
    dict(name="slip", element="Time", before=SLIP_BEFORE,
         title="Slip (Time, picked A): riding, the Urchin sits on its trail; slipping, it lifts clear and leaves its ghost; "
               "Slipstream keeps its speed over a rival's trail",
         parts=_parts(SLIP_PARTS),
         states=[("riding", SLIP_RIDING),
                 ("slipping", [("SlipGhost", "domain", 0.35), ("SlipLifted", "domain"), ("SlipTrail", "domain")]),
                 ("Time 5: Slipstream", [("SlipGhost", "domain", 0.35), ("SlipLifted", "domain"), ("SlipTrail", "danger"),
                                         ("SlipStream", "white")])],
         lockup_state=1),
    dict(name="omni", element="Omni", title="The OMNI crystal card: the omni crystal",
         parts=dict(i=omni_crystal_card), states=[("an omni crystal collected", [("i", "white")])]),
]
LOCKUP = dict(
    Charge=dict(parts=_parts(CHAIN_PARTS), state=CHAIN_ON, gauge=0.7),
    Mass=dict(parts=_parts(RIDER_PARTS), state=RIDING, gauge=1.0),
    Space=dict(parts=_parts(TRACK_PARTS), state=[("TrackUrchin", "domain"), ("TrackRail", "white")]),
    Time=dict(parts=_parts(SLIP_PARTS), state=SLIP_RIDING),
    Omni=dict(parts=dict(i=omni_crystal_card), state=[("i", "white")]),
)
REBIND = [
    dict(file=VARIANT, pair=dict(container=SLOT["Charge"], view=VIEW, after="chargeRing",
                                 children=[(n, i, f) for n, i, f, _d, _a in CHAIN_PARTS])),
    dict(file=VARIANT, pair=dict(container=SLOT["Mass"], view=VIEW, after="chargeRing",
                                 children=[(n, i, f) for n, i, f, _d, _a in RIDER_PARTS])),
    dict(file=VARIANT, pair=dict(container=SLOT["Space"], view=VIEW, after="chargeRing",
                                 children=[(n, i, f) for n, i, f, _d, _a in TRACK_PARTS])),
    dict(file=VARIANT, pair=dict(container=SLOT["Time"], view=VIEW, after="chargeRing",
                                 children=[(n, i, f) for n, i, f, _d, _a in SLIP_PARTS],
                                 fields={"omniAbilitySprite": ("icon", "Urchin_OmniCrystal")})),
]
