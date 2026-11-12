"""The Manta's four ability icons (Resources/ElementalAbilityMaps/Manta.asset), angular, one object each.

  Charge  Sting    the bomb the bay plants: an octagonal shell, a square neck, a zig-zag fuse, the spark
  Mass    Yawstery   the hard turn that LEAVES mass: a mitred right-angle band ending in an arrowhead, two
                   prisms growing behind the bend
  Space   Kabloom  the bloom, wider: one jagged blast ring round a small core
  Time    Soar     the Manta itself at speed: its top-view silhouette banked, one motion bar behind each wing

Before (author_manta_icon_placeholders.py, 2026-08): a round bomb, a bent arrow, a flower, three chevrons.
"""
import math

from canvas import W_PRIMARY, W_SECONDARY, disc, fill, ngon, rect, rotate, scale, shear, stroke
from motifs import arrowhead, burst, prism_lit, speed_wedge

HULL = "Manta"


def sting():
    body = fill(ngon(8, 37, 58, 80, rot=22.5))
    neck = rect(44, 38, 72, 50)
    fuse = stroke([(60, 42), (72, 30), (86, 38), (98, 24)], W_SECONDARY)
    spark = fill([(102, 6), (107, 17), (118, 20), (107, 24), (102, 34), (97, 24), (86, 20), (97, 17)])
    return body | neck | fuse | spark


def yawstery():
    # The band is the turn; the vertical leg behind the bend is what it LEAVES: prisms, growing.
    head = arrowhead((122, 36), (1, 0), 30, spread=0.72)
    band = stroke([(30, 58), (30, 36), (98, 36)], 14)
    p1 = prism_lit(30, 76, 12)
    p2 = prism_lit(30, 107, 15)
    return band | head | p1 | p2


def kabloom():
    # One blast, its hollow core the negative space that says "it went off HERE".
    outer = burst(64, 64, 58, spikes=11, inner=0.56, rot=-90)
    hole = fill(ngon(8, 13, 64, 64, rot=22.5))
    return outer & ~hole


def _manta_pts():
    """The Manta from above: a blunt head between two short lobes, wide delta wings with swept tips, a whip
    tail. Right half, mirrored. (Round 1's taller lobes read as EARS at 40 px - a fox, not a ray.)"""
    right = [(64, 24), (72, 20), (76, 26), (84, 32), (124, 40), (126, 48), (112, 60), (84, 70), (74, 84),
             (69, 98), (67, 124), (64, 124)]
    left = [(128 - x, y) for x, y in reversed(right[1:-1])]
    return right + left


def soar():
    pts = rotate(_manta_pts(), -16, 64, 64)
    pts = scale(pts, 0.88, 0.88, 64, 64)
    body = fill([(x + 8, y - 2) for x, y in pts])
    w1 = speed_wedge(14, 94, 30, 9)
    w2 = speed_wedge(20, 110, 24, 9)
    return body | w1 | w2


ICONS = [
    dict(name="Manta_Sting", element="Charge", act="Sting", draw=sting),
    dict(name="Manta_Yawstery", element="Mass", act="Yawstery", draw=yawstery,
         exempt={"components": "the band and its two prisms, each two close triangles (never the rhombus, Garrett 2026-11-01)"}),
    dict(name="Manta_Kabloom", element="Space", act="Kabloom", draw=kabloom),
    dict(name="Manta_Soar", element="Time", act="Soar", draw=soar),
]


# ==== round 2 (2026-11-01): the Manta through its real mesh, one card at a time ============================================
# MANTA_STING_KABLOOM.md: the kit is BUTTONLESS where it matters - skim prisms to ARM bombs (the bay, 3 -> 5 with Charge),
# graze a vessel or creature to PLANT one, touch a crystal to DETONATE (Kabloom). The HUD (MantaVesselHUDView) shows the
# bay (armed / capacity, the charge to the next) and the planted board (count, shortest fuse).
import os as _os

import mesh_silhouette
import toy_portraits
from canvas import empty, ring

FBX = "Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx"
TOP = dict(pitch=-90, yaw=90, dilate_px=1.0, close_px=3.0)   # from above, nose UP (the bind pose: wings swept back)
_SIL = {}


def manta(box, cx=64.0, cy=64.0, roll=0.0):
    def draw():
        key = (box, cx, cy, roll)
        if key not in _SIL:
            _SIL[key] = mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, roll=roll, **TOP)
        return _SIL[key].copy()
    return draw


def _clear_of(mask, *others, px=2.5):
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


def bomb(cx, cy, r, held=True, w=2.2):
    """A bomb: a round shell with a square neck on top (never a rhombus); held = solid, empty = an outline."""
    neck = rect(cx - r * 0.35, cy - r * 1.35, cx + r * 0.35, cy - r * 0.8)
    if held:
        return disc(cx, cy, r) | neck
    return ring(cx, cy, r - w / 2, w=w) | (neck & ~disc(cx, cy, r - w))


# ==== Round 3 (2026-11-07): the overhauled kit (#1803, #1804) - Sting, Decoy, Skim, Yawstery ====================================
# The round-2 Sting options (bay / plant / belly) drew the old bomb bay; the overhaul retired it: bombs now share ONE fuse,
# skim energy buys each plant, and the Mass and Time cards carry new abilities. Every card is redrawn against the new kit.
import importlib as _importlib

import numpy as _np


def _png(name):
    from PIL import Image
    from canvas import N
    root = _os.path.abspath(_os.path.join(_os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(_os.path.join(root, f"Assets/_Graphics/Icons/AbilityIcons/Manta/{name}.png")).convert("RGBA").split()[3]
    return _np.asarray(a.resize((N, N), Image.LANCZOS)) > 127


# The row as the HUD draws it today: the four shipped sprites in the prefab's slots (Charge, Mass, Space, Time) - the
# overhaul moved the abilities but not the sprites, so Mass still wears Yawstery and Time still wears Soar.
LOCKUP = dict(
    Charge=dict(parts=dict(i=lambda: _png("Manta_Sting")), state=[("i", "white")], gauge=0.5),
    Mass=dict(parts=dict(i=lambda: _png("Manta_Yawstery")), state=[("i", "white")], gauge=0.0),
    Space=dict(parts=dict(i=lambda: _png("Manta_Kabloom")), state=[("i", "white")], gauge=0.0),
    Time=dict(parts=dict(i=lambda: _png("Manta_Soar")), state=[("i", "white")], gauge=0.0),
)

_scarab = _importlib.import_module("specs.scarab")


def rival(box, cx, cy):
    """A rival Manta (danger): the same real hull, smaller, nose DOWN - coming the other way."""
    def draw():
        return _np.flipud(manta(box, cx=cx, cy=128 - cy)())
    return draw


def crystal(cx, cy, h):
    return _scarab.omni_crystal(cx, cy, h)


def crystal_faces(cx, cy, h):
    return [fill([(cx + x * h / 2, cy - y * h / 2) for x, y in f]) for f in _scarab._omni_faces()]


def decoy(cx, cy, h, frozen=2, w=None):
    """The decoy: the same crystal with ONE plate frozen - drawn as an outline, the tell a sharp eye catches. The face
    list carries the front and back plates (the same plate twice), so a plate is frozen by where it sits, not by index."""
    w = w if w is not None else max(1.6, h * 0.05)
    raw = _scarab._omni_faces()
    cen = [(sum(p[0] for p in f) / len(f), sum(p[1] for p in f) / len(f)) for f in raw]
    fx, fy = cen[frozen]
    out = empty()
    hollow = empty()
    for f, (x, y) in zip(raw, cen):
        m = fill([(cx + px * h / 2, cy - py * h / 2) for px, py in f])
        if math.hypot(x - fx, y - fy) < 0.08:
            hollow |= m
        else:
            out |= m
    return (out & ~hollow) | (hollow & ~toy_portraits.grown(~hollow, w))


def fuse_ring(cx, cy, r, frac, w=4.0):
    a0 = -90.0
    n = max(2, int(60 * frac))
    pts = [(cx + r * math.cos(math.radians(a0 + 360 * frac * t / n)), cy + r * math.sin(math.radians(a0 + 360 * frac * t / n)))
           for t in range(n + 1)]
    return stroke(pts, w)


# ---- Charge: Sting ----------------------------------------------------------------------------------------------------------
# Skimming a rival plants a bomb (paid for with a bomb of skim energy). Every bomb shares ONE fuse, reset by each plant, so
# they all go off together; Charge sets how long that fuse runs when a plant resets it. L5 Contagion: anything caught in a blast is
# bombed too. The plate is the shared FUSE (MantaVesselHUDView.SetFuse).

CH_MANTA = manta(50, cx=27, cy=100)
CH_RIVAL = rival(76, cx=80, cy=48)
BOMBS = [(62, 40), (98, 40), (80, 70)]


def ch_bombs(n=3, r=8.5):
    out = empty()
    for x, y in BOMBS[:n]:
        out |= bomb(x, y, r)
    return out


def ch_fuse(frac=0.65):
    # ONE fuse for all of them: a single cord joining the bombs, burnt back from the end
    pts = [BOMBS[0], (84, 48), BOMBS[1], (91, 48), BOMBS[2]]
    full = stroke(pts, w=2.6)
    return full


A_FUSE_GAUGE = {"one planted": (0.95, None), "three, one fuse": (0.6, None), "Charge 5: Contagion": (0.25, None)}


# A  the pack: a rival (danger) wearing three bombs (white) on one fuse - the plant the Manta just made, beside it.
def a_stuck(n):
    return lambda: _clear_of(ch_bombs(n), CH_RIVAL(), px=0.0) | ch_bombs(n)


def a_contagion():
    # the blast caught a second rival, which is bombed too
    return bomb(110, 100, 6.5)


A2_RIVAL = rival(32, cx=110, cy=104)

# B  the fuse: three bombs and one ring of fuse round all of them - the clock they share.
B_RING = (64, 64)


def b_bombs(n):
    pos = [(64, 40), (43, 76), (85, 76)][:n]
    out = empty()
    for x, y in pos:
        out |= bomb(x, y, 12.0)
    return out


# C  the bloom: the bombs going off together on the rival - three blooms at once, petals flying.
def c_blooms(n, r=15):
    out = empty()
    for x, y in BOMBS[:n]:
        out |= motifs_burst(x, y, r)
    return out


def motifs_burst(x, y, r):
    import motifs
    return motifs.burst(x, y, r, spikes=8, inner=0.5)


STING_BEFORE = "Assets/_Graphics/Icons/AbilityIcons/Manta/Manta_Sting.png"
CARDS = [
    dict(name="sting_a", group="sting", element="Charge", before=STING_BEFORE, gauge=A_FUSE_GAUGE,
         title="A  the pack: the Manta (domain) has bombed a rival (danger) three times; the plate is their ONE fuse",
         parts=dict(m=CH_MANTA, r=CH_RIVAL, b1=a_stuck(1), b3=a_stuck(3), r2=A2_RIVAL, c=a_contagion),
         states=[("one planted", [("m", "domain"), ("r", "danger"), ("b1", "white")]),
                 ("three, one fuse", [("m", "domain"), ("r", "danger"), ("b3", "white")]),
                 ("Charge 5: Contagion", [("m", "domain"), ("r", "danger"), ("b3", "white"), ("r2", "danger"), ("c", "white")])],
         lockup_state=1),
    dict(name="sting_b", group="sting", element="Charge", before=STING_BEFORE, gauge=A_FUSE_GAUGE,
         title="B  the shared fuse: the bombs (white) inside one ring of fuse that burns down for all of them at once",
         parts=dict(b1=lambda: b_bombs(1), b3=lambda: b_bombs(3), f9=lambda: fuse_ring(64, 64, 46, 0.95, w=5.0),
                    f6=lambda: fuse_ring(64, 64, 46, 0.6, w=5.0), f2=lambda: fuse_ring(64, 64, 46, 0.25, w=5.0)),
         states=[("one planted", [("b1", "white"), ("f9", "white")]),
                 ("three, one fuse", [("b3", "white"), ("f6", "white")]),
                 ("Charge 5: Contagion", [("b3", "white"), ("f2", "danger")])],
         lockup_state=1),
    dict(name="sting_c", group="sting", element="Charge", before=STING_BEFORE, gauge=A_FUSE_GAUGE,
         title="C  the bloom: every bomb on the rival (danger) goes off together - the moment the one fuse buys",
         parts=dict(m=CH_MANTA, r=CH_RIVAL, b3=a_stuck(3), x3=lambda: c_blooms(3), x3b=lambda: c_blooms(3, 19),
                    r2=A2_RIVAL),
         states=[("one planted", [("m", "domain"), ("r", "danger"), ("b3", "white", 0.5)]),
                 ("three, one fuse", [("m", "domain"), ("r", "danger", 0.6), ("x3", "white")]),
                 ("Charge 5: Contagion", [("m", "domain"), ("r", "danger", 0.6), ("x3b", "white"), ("r2", "danger")])],
         lockup_state=1),
]


# ---- Mass: Decoy ------------------------------------------------------------------------------------------------------------
# A crystal the Manta collects leaves a decoy where it was: the same crystal with its own element's effect frozen (the tell).
# A rival who takes it gets a cluster of danger prisms across its course. Mass is how long a decoy stands. L5 Omni Decoy: the
# omni crystal leaves one too (one of its four effects frozen); one elemental and one omni decoy at a time.
from motifs import prism_halves  # noqa: E402

D_MANTA = manta(54, cx=96, cy=34)


def d_real():
    return crystal(40, 82, 46)


def d_decoy():
    return decoy(40, 82, 46, frozen=0)


def d_trap():
    out = empty()
    for x, y, sz in ((22, 92, 9), (42, 104, 10), (62, 90, 9), (34, 74, 8), (56, 70, 8)):
        out |= prism_halves(x, y, sz, gap=2.0)
    return out


def d_omni():
    return decoy(102, 96, 30, frozen=3)


def d_pair_elemental():
    return decoy(34, 86, 38, frozen=0)


DECOY_BEFORE = "Assets/_Graphics/Icons/AbilityIcons/Manta/Manta_Yawstery.png"
DECOY_GAUGE = {"no decoy": (0.0, None), "a decoy standing": (0.7, None), "Mass 5: Omni Decoy": (0.7, None)}
CARDS += [
    dict(name="decoy_a", group="decoy", element="Mass", before=DECOY_BEFORE, gauge=DECOY_GAUGE,
         title="A  left behind: the Manta (domain) flies off; where the crystal was, its decoy - one plate frozen (the tell)",
         parts=dict(m=D_MANTA, c=d_real, d=d_decoy, o=d_omni, de=d_pair_elemental),
         states=[("no decoy", [("m", "domain"), ("c", "white", 0.5)]),
                 ("a decoy standing", [("m", "domain"), ("d", "white")]),
                 ("Mass 5: Omni Decoy", [("m", "domain"), ("de", "white"), ("o", "white")])],
         lockup_state=1),
    dict(name="decoy_b", group="decoy", element="Mass", before=DECOY_BEFORE, gauge=DECOY_GAUGE,
         title="B  the trap: the decoy (white, one plate frozen) and what a rival who takes it gets - danger prisms",
         parts=dict(d=lambda: decoy(64, 46, 52, frozen=0), t=lambda: _np.roll(d_trap(), (0, 26 * 8), axis=(0, 1))),
         states=[("no decoy", [("d", "white", 0.4)]),
                 ("a decoy standing", [("d", "white"), ("t", "danger", 0.5)]),
                 ("Mass 5: Omni Decoy", [("d", "white"), ("t", "danger")])],
         lockup_state=1),
    dict(name="decoy_c", group="decoy", element="Mass", before=DECOY_BEFORE, gauge=DECOY_GAUGE,
         title="C  real or not: the crystal (white) and its decoy side by side - only the frozen plate tells them apart",
         parts=dict(c=lambda: crystal(36, 64, 50), d=lambda: decoy(92, 64, 50, frozen=0),
                    o=lambda: decoy(92, 64, 50, frozen=3)),
         states=[("no decoy", [("c", "white")]),
                 ("a decoy standing", [("c", "white"), ("d", "white")]),
                 ("Mass 5: Omni Decoy", [("c", "white"), ("o", "white")])],
         lockup_state=1),
]


# ---- Space: Skim ------------------------------------------------------------------------------------------------------------
# Skimming gathers the energy that plants bombs (capped at one bomb's worth: skim, plant, skim, plant) and turns rival prisms
# EXPLOSIVE - destroyed, they take their prismscape neighbours with them. Hitting a prism loses all the energy. Space widens
# the skimmer. L5 Shield Breaker: a shielded rival prism's shield pops and it turns explosive too. The plate is the ENERGY
# (MantaVesselHUDView.SetEnergy), full = one bomb.
SK_MANTA = manta(60, cx=40, cy=70)


def sk_field(r):
    return ring(40, 70, r, w=3.0)


def sk_prisms():
    out = empty()
    for y in (30, 64, 98):
        out |= prism_halves(102, y, 12, gap=2.2)
    return out


def sk_sparks():
    # explosive: each prism wears a crown of short spikes standing off it - the "it will go off" mark, clear of the prism
    out = empty()
    for y in (30, 64, 98):
        for k in range(8):
            a = math.radians(22.5 + 45 * k)
            r0, r1 = 15.5, 21.0
            ux, uy = math.cos(a), math.sin(a)
            px, py = -uy, ux
            x0, y0 = 102 + ux * r0, y + uy * r0
            out |= fill([(102 + ux * r1, y + uy * r1), (x0 + px * 2.2, y0 + py * 2.2), (x0 - px * 2.2, y0 - py * 2.2)])
    return out


def sk_bomb(frac):
    """The bomb the energy buys, filling from the bottom - an outline until it is whole."""
    shell = bomb(98, 66, 22, held=False, w=4.0)
    solid = bomb(98, 66, 22)
    from canvas import rect as _rect
    level = _rect(0, 66 + 22 - 44 * frac, 128, 128)
    return shell | (solid & level)


ENERGY_GAUGE = {"skimming": (0.4, None), "a bomb's worth": (1.0, None), "Space 5: Shield Breaker": (0.6, None)}
SKIM_BEFORE = "Assets/_Graphics/Icons/AbilityIcons/Manta/Manta_Kabloom.png"
CARDS += [
    dict(name="skim_a", group="skim", element="Space", before=SKIM_BEFORE, gauge=ENERGY_GAUGE,
         title="A  explosive: the Manta (domain) skimming a line of rival prisms (danger), which turn explosive (white sparks)",
         parts=dict(m=SK_MANTA, p=sk_prisms, x=sk_sparks),
         states=[("skimming", [("m", "domain"), ("p", "danger")]),
                 ("a bomb's worth", [("m", "domain"), ("p", "danger"), ("x", "white")]),
                 ("Space 5: Shield Breaker", [("m", "domain"), ("p", "shielded"), ("x", "white")])],
         lockup_state=1),
    dict(name="skim_b", group="skim", element="Space", before=SKIM_BEFORE, gauge=ENERGY_GAUGE,
         title="B  the bomb it buys: the Manta (domain) and a bomb (white) filling with skim energy - whole is one plant",
         parts=dict(m=manta(56, cx=34, cy=66), b4=lambda: sk_bomb(0.4), b10=lambda: sk_bomb(1.0)),
         states=[("skimming", [("m", "domain"), ("b4", "white")]),
                 ("a bomb's worth", [("m", "domain"), ("b10", "white")]),
                 ("Space 5: Shield Breaker", [("m", "domain"), ("b10", "white")])],
         lockup_state=1),
    dict(name="skim_c", group="skim", element="Space", before=SKIM_BEFORE, gauge=ENERGY_GAUGE,
         title="C  the skimmer: the Manta (domain) inside its skim field (white ring), wider with Space",
         parts=dict(m=SK_MANTA, f=lambda: sk_field(38), f2=lambda: sk_field(46), p=sk_prisms),
         states=[("skimming", [("m", "domain"), ("f", "white"), ("p", "danger", 0.5)]),
                 ("a bomb's worth", [("m", "domain"), ("f", "white"), ("p", "danger")]),
                 ("Space 5: Shield Breaker", [("m", "domain"), ("f2", "white"), ("p", "shielded")])],
         lockup_state=1),
]


# ---- Time: Yawstery -----------------------------------------------------------------------------------------------------------
# Hard flat turns on the turn pair; Time makes the Manta faster (cruise x1.5, soaring x1.3 at Time 10). L5 Soar: boosting
# unlocks (the analog turn-boost and the boost button stay dark until Time 5).
from motifs import speed_wedge  # noqa: E402

Y_MANTA = manta(60, cx=80, cy=40)


def y_turn():
    # the flat turn: a hard corner in the trail behind it, not a curve
    return stroke([(18, 116), (18, 66), (50, 44)], w=7.0)


def y_marks(n):
    out = empty()
    for k in range(n):
        out |= speed_wedge(90 - 6 * k, 84 + 12 * k, 22 - 4 * k, 6.0)
    return out


def y_soar():
    # Soar: three swept chevrons ahead of the nose - the boost Time 5 unlocks
    out = empty()
    for k in range(2):
        y = 8 + k * 10
        out |= stroke([(66, y + 10), (80, y), (94, y + 10)], w=5.0)
    return out


YAWSTERY_BEFORE = "Assets/_Graphics/Icons/AbilityIcons/Manta/Manta_Soar.png"
Y_GAUGE = {"cruise": (None, None), "Time 3: faster": (None, None), "Time 5: Soar": (None, None)}
CARDS += [
    dict(name="yawstery_a", group="yawstery", element="Time", before=YAWSTERY_BEFORE,
         title="A  the flat turn: the Manta (domain) leaving a hard corner in its trail (white); Soar's chevrons at Time 5",
         parts=dict(m=manta(60, cx=80, cy=48), t=y_turn, s=y_soar),
         states=[("cruise", [("m", "domain"), ("t", "white", 0.5)]),
                 ("Time 3: faster", [("m", "domain"), ("t", "white")]),
                 ("Time 5: Soar", [("m", "domain"), ("t", "white"), ("s", "white")])],
         lockup_state=1),
    dict(name="yawstery_b", group="yawstery", element="Time", before=YAWSTERY_BEFORE,
         title="B  faster: the Manta (domain) with speed marks (white) that grow with Time; Soar's chevrons at Time 5",
         parts=dict(m=manta(66, cx=64, cy=56), w1=lambda: _np.roll(y_marks(1), (0, -26 * 8), axis=(0, 1)),
                    w3=lambda: _np.roll(y_marks(3), (0, -26 * 8), axis=(0, 1)), s=lambda: _np.roll(y_soar(), (-2 * 8, -16 * 8), axis=(0, 1))),
         states=[("cruise", [("m", "domain"), ("w1", "white")]),
                 ("Time 3: faster", [("m", "domain"), ("w3", "white")]),
                 ("Time 5: Soar", [("m", "domain"), ("w3", "white"), ("s", "white")])],
         lockup_state=1),
    dict(name="yawstery_c", group="yawstery", element="Time", before=YAWSTERY_BEFORE,
         title="C  the bank: the Manta (domain) rolled into the turn, flat wings tipped - the shipped Yawstery, on the real hull",
         parts=dict(m=manta(78, roll=-35.0), s=lambda: _np.roll(y_soar(), (-4 * 8, -16 * 8), axis=(0, 1))),
         states=[("cruise", [("m", "domain", 0.7)]),
                 ("Time 3: faster", [("m", "domain")]),
                 ("Time 5: Soar", [("m", "domain"), ("s", "white")])],
         lockup_state=1),
]


# ==== Round 3 in the game: the picks (Garrett, 2026-11-07: "do all of the icons ... treat each icon as an excellent and
# polished thing"; he reviews the roster at the end, so each card is the option recommended on its sheet) =====================
# Every card is PARTS in its slot's container (rebind `pair`), tinted and shown by MantaVesselHUDView from the executor:
#   Charge  A  the pack: the Manta, a rival wearing one bomb per planted bomb (up to three drawn), the plate the ONE fuse;
#              Contagion (Charge 5) adds a second rival the blast has bombed.
#   Mass    B  the trap: the decoy crystal (one plate frozen), the danger prisms it springs on a rival; at Mass 5 the omni
#              decoy stands beside it while one is out.
#   Space   A  explosive: the Manta skimming rival prisms that turn explosive as the energy gathers; the plate is the
#              energy (full = one bomb); Shield Breaker (Space 5) shows the shell knocked off the middle prism.
#   Time    A  the flat turn: the Manta and the hard corner its turn pair cuts; Soar's chevrons once Time 5 unlocks it.
#   Omni       the cash-out: an omni crystal sets every planted bomb off at once - three blooms.
PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}
VESSEL = "Assets/_Prefabs/Spacevessels/Manta.prefab"
VIEW = "2835891913121360755"            # MantaVesselHUDView on Manta.prefab (the row lives in the vessel prefab)
SLOT = dict(Charge="8900000000000001015", Mass="8900000000000001025", Space="8900000000000001035",
            Time="8900000000000001045")


def _bomb_k(k):
    return lambda: bomb(BOMBS[k][0], BOMBS[k][1], 9.5)


def _rival_clear():
    # the rival with a hairline cut round each bomb, so a white bomb never melts into the danger hull at 40 px
    out = CH_RIVAL()
    for x, y in BOMBS:
        out &= ~toy_portraits.grown(bomb(x, y, 9.5), 2.2)
    return out


def _rival2_clear():
    return A2_RIVAL() & ~toy_portraits.grown(a_contagion(), 1.6)


STING_PARTS = [("StingManta", "Manta_StingManta", "stingMantaIcon", CH_MANTA, "the Manta from above (domain)"),
               ("StingRival", "Manta_StingRival", "stingRivalIcon", _rival_clear,
                "the rival it bombed (danger; dim with nothing planted)"),
               ("StingBomb0", "Manta_StingBomb0", "stingBomb0Icon", _bomb_k(0), "the first bomb (white)"),
               ("StingBomb1", "Manta_StingBomb1", "stingBomb1Icon", _bomb_k(1), "the second bomb (white)"),
               ("StingBomb2", "Manta_StingBomb2", "stingBomb2Icon", _bomb_k(2), "the third bomb and on (white)"),
               ("StingContagionRival", "Manta_StingContagionRival", "stingContagionRivalIcon", _rival2_clear,
                "Contagion: a second rival caught in the blast (danger; Charge 5)"),
               ("StingContagionBomb", "Manta_StingContagionBomb", "stingContagionBombIcon", a_contagion,
                "Contagion: the bomb the blast gave it (white; Charge 5)")]


def _decoy_main():
    return decoy(56, 44, 54, frozen=0)


def _decoy_omni():
    # the omni decoy: smaller, up and to the right, a square plate frozen instead of the heart
    return decoy(106, 22, 34, frozen=4) & ~toy_portraits.grown(_decoy_main(), 2.5)


def _decoy_trap():
    out = empty()
    for x, y, sz in ((34, 100, 10), (58, 112, 11), (82, 98, 10), (46, 84, 8), (72, 82, 8)):
        out |= prism_halves(x, y, sz, gap=2.0)
    return out & ~toy_portraits.grown(_decoy_main(), 2.5)


DECOY_PARTS = [("DecoyCrystal", "Manta_DecoyCrystal", "decoyCrystalIcon", _decoy_main,
                "the decoy: the crystal with one plate frozen (white; dim with no decoy standing)"),
               ("DecoyOmni", "Manta_DecoyOmni", "decoyOmniIcon", _decoy_omni,
                "the omni decoy (white; only while one stands, Mass 5)"),
               ("DecoyTrap", "Manta_DecoyTrap", "decoyTrapIcon", _decoy_trap,
                "what a rival who takes it gets: danger prisms (danger)")]


def _skim_shell():
    # Shield Breaker: the shell knocked off - two broken arcs of the middle prism's shield flung out to the left
    from canvas import band
    return (band(102, 64, 27, 150, 200, w=3.2) | band(102, 64, 27, 210, 245, w=3.2)) & ~toy_portraits.grown(sk_sparks(), 1.2)


SKIM_PARTS = [("SkimManta", "Manta_SkimManta", "skimMantaIcon", SK_MANTA, "the Manta from above (domain)"),
              ("SkimPrisms", "Manta_SkimPrisms", "skimPrismsIcon", sk_prisms, "the rival prisms it skims (danger)"),
              ("SkimSparks", "Manta_SkimSparks", "skimSparksIcon", sk_sparks,
               "explosive: the spikes the skim leaves round each prism (white; brightens as the energy gathers)"),
              ("SkimShards", "Manta_SkimShards", "skimShardsIcon", _skim_shell,
               "Shield Breaker: a shell knocked off the prisms (white; Space 5)"),
              ("EnergyGauge", None, "energyFill", None, "the card's plate: the skim energy, full = one bomb")]

Y_MANTA_A = manta(60, cx=80, cy=48)
YAWSTERY_PARTS = [("YawsteryManta", "Manta_YawsteryManta", "yawsteryMantaIcon", Y_MANTA_A, "the Manta from above (domain)"),
                ("YawsteryTurn", "Manta_YawsteryTurn", "yawsteryTurnIcon", lambda: _clear_of(y_turn(), Y_MANTA_A(), px=2.0),
                 "the hard corner its turn pair cuts (white; brighter as Time speeds it up)"),
                ("YawsterySoar", "Manta_YawsterySoar", "yawsterySoarIcon", lambda: _clear_of(y_soar(), Y_MANTA_A(), px=2.0),
                 "Soar: the boost chevrons ahead of the nose (white; Time 5)")]

for element, parts in (("Charge", STING_PARTS), ("Mass", DECOY_PARTS), ("Space", SKIM_PARTS), ("Time", YAWSTERY_PARTS)):
    ICONS += [dict(name=i, element=element, act=act, draw=draw, subdir="Parts", family=False, exempt=PART_EXEMPT)
              for _n, i, _f, draw, act in parts if i]


def omni_cash_out():
    # the omni crystal at the heart, every planted bomb blooming round it at once
    import motifs as _m
    core = crystal(64, 70, 58)
    out = core
    for a, r in ((-90, 21), (30, 19), (150, 19)):
        ra = math.radians(a)
        out |= _m.burst(64 + 46 * math.cos(ra), 70 + 44 * math.sin(ra), r, spikes=8, inner=0.5) & ~toy_portraits.grown(core, 3.0)
    return out


ICONS += [dict(name="Manta_OmniCashOut", element="Omni", subdir="Core", family=False, draw=omni_cash_out,
               act="The OMNI crystal card: an omni crystal sets every planted bomb off together (one white sprite)",
               exempt={"components": "the crystal's plates and three blooms", "gap": "the blooms stand off the crystal",
                       "specks": "the omni crystal's small triangle plates, projected from its own mesh (as Scarab_BallForge)"})]

CARDS[:] = [c for c in CARDS if not c.get("group")]   # the losers' sheets go once the pick is made
_parts = lambda lst: {n: d for n, _i, _f, d, _a in lst if d}   # noqa: E731
CARDS += [
    dict(name="sting", element="Charge", before=STING_BEFORE, gauge=A_FUSE_GAUGE,
         title="Sting (Charge, picked A): the Manta (domain), a rival (danger) wearing a bomb per plant; the plate is the "
               "ONE fuse they share; Contagion bombs whoever the blast catches",
         parts=_parts(STING_PARTS),
         states=[("nothing planted", [("StingManta", "domain"), ("StingRival", "danger", 0.35)]),
                 ("one planted", [("StingManta", "domain"), ("StingRival", "danger"), ("StingBomb0", "white")]),
                 ("three, one fuse", [("StingManta", "domain"), ("StingRival", "danger"), ("StingBomb0", "white"),
                                      ("StingBomb1", "white"), ("StingBomb2", "white")]),
                 ("Charge 5: Contagion", [("StingManta", "domain"), ("StingRival", "danger"), ("StingBomb0", "white"),
                                          ("StingBomb1", "white"), ("StingBomb2", "white"),
                                          ("StingContagionRival", "danger"), ("StingContagionBomb", "white")])],
         lockup_state=2),
    dict(name="decoy", element="Mass", before=DECOY_BEFORE,
         title="Decoy (Mass, picked B): the decoy crystal (white, one plate frozen) and the danger prisms it springs; the "
               "omni decoy beside it at Mass 5",
         parts=_parts(DECOY_PARTS),
         states=[("no decoy", [("DecoyCrystal", "white", 0.3), ("DecoyTrap", "danger", 0.3)]),
                 ("a decoy standing", [("DecoyCrystal", "white"), ("DecoyTrap", "danger")]),
                 ("Mass 5: both standing", [("DecoyCrystal", "white"), ("DecoyOmni", "white"), ("DecoyTrap", "danger")])],
         lockup_state=1),
    dict(name="skim", element="Space", before=SKIM_BEFORE, gauge=ENERGY_GAUGE,
         title="Skim (Space, picked A): the Manta (domain) skimming rival prisms (danger) that turn explosive (white); the "
               "plate is the energy, full = one bomb; Shield Breaker knocks their shells off",
         parts=_parts(SKIM_PARTS),
         states=[("skimming", [("SkimManta", "domain"), ("SkimPrisms", "danger"), ("SkimSparks", "white", 0.45)]),
                 ("a bomb's worth", [("SkimManta", "domain"), ("SkimPrisms", "danger"), ("SkimSparks", "white")]),
                 ("Space 5: Shield Breaker", [("SkimManta", "domain"), ("SkimPrisms", "danger"), ("SkimSparks", "white"),
                                              ("SkimShards", "white")])],
         lockup_state=1),
    dict(name="yawstery", element="Time", before=YAWSTERY_BEFORE,
         title="Yawstery (Time, picked A): the Manta (domain) and the hard corner (white) its turn pair cuts; Soar's chevrons "
               "once Time 5 unlocks the boost",
         parts=_parts(YAWSTERY_PARTS),
         states=[("cruise", [("YawsteryManta", "domain"), ("YawsteryTurn", "white", 0.55)]),
                 ("Time 10: faster", [("YawsteryManta", "domain"), ("YawsteryTurn", "white")]),
                 ("Time 5: Soar", [("YawsteryManta", "domain"), ("YawsteryTurn", "white"), ("YawsterySoar", "white")])],
         lockup_state=1),
    dict(name="omni", element="Omni", title="The OMNI crystal card: the cash-out - every planted bomb blooms at once",
         parts=dict(i=omni_cash_out), states=[("an omni crystal collected", [("i", "white")])]),
]
LOCKUP = dict(
    Charge=dict(parts=_parts(STING_PARTS), state=CARDS[-5]["states"][2][1], gauge=0.6),
    Mass=dict(parts=_parts(DECOY_PARTS), state=CARDS[-4]["states"][1][1]),
    Space=dict(parts=_parts(SKIM_PARTS), state=CARDS[-3]["states"][0][1], gauge=0.45),
    Time=dict(parts=_parts(YAWSTERY_PARTS), state=CARDS[-2]["states"][1][1]),
    Omni=dict(parts=dict(i=omni_cash_out), state=[("i", "white")]),
)
REBIND = [
    dict(file=VESSEL, pair=dict(container=SLOT["Charge"], view=VIEW, after="fuseText",
                                children=[(n, i, f) for n, i, f, _d, _a in STING_PARTS])),
    dict(file=VESSEL, pair=dict(container=SLOT["Mass"], view=VIEW, after="fuseText",
                                children=[(n, i, f) for n, i, f, _d, _a in DECOY_PARTS])),
    dict(file=VESSEL, pair=dict(container=SLOT["Space"], view=VIEW, after="fuseText",
                                children=[(n, i, f) for n, i, f, _d, _a in SKIM_PARTS],
                                binding=dict(element=3, gauge="EnergyGauge"))),
    dict(file=VESSEL, pair=dict(container=SLOT["Time"], view=VIEW, after="fuseText",
                                children=[(n, i, f) for n, i, f, _d, _a in YAWSTERY_PARTS],
                                fields={"omniAbilitySprite": ("icon", "Manta_OmniCashOut")})),
]
