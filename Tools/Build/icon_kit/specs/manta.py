"""The Manta's four ability icons (Resources/ElementalAbilityMaps/Manta.asset), angular, one object each.

  Charge  Sting    the bomb the bay plants: an octagonal shell, a square neck, a zig-zag fuse, the spark
  Mass    Yastri   the hard turn that LEAVES mass: a mitred right-angle band ending in an arrowhead, two
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


def yastri():
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
    dict(name="Manta_Yastri", element="Mass", act="Yastri", draw=yastri,
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


# A  the bay: the Manta from above, its bomb bay under it as slots - solid when armed, an outline when empty; Charge adds
#    the fourth and fifth slots.
A_MANTA = manta(86, cy=42)


def a_bay(armed, capacity):
    out = empty()
    xs = {3: (36, 64, 92), 4: (22, 50, 78, 106), 5: (16, 40, 64, 88, 112)}[capacity]
    for i, x in enumerate(xs):
        out |= bomb(x, 106, 10.0, held=i < armed)
    return out


# B  the plant: the Manta grazing past a rival (danger) on the right, a bomb stuck to the rival, a fuse ring round it.
B_MANTA = manta(84, cx=46, cy=70)


def b_rival():
    return fill([(106, 18), (124, 56), (106, 47), (88, 56)])   # a generic hull, nose up (the danger colour)


def b_bomb():
    return bomb(106, 76, 10.0)


def b_fuse(frac=0.65):
    a0 = -90.0
    pts = [(106 + 18 * math.cos(math.radians(a0 + 360 * frac * t)), 76 + 18 * math.sin(math.radians(a0 + 360 * frac * t)))
           for t in [i / 40 for i in range(41)]]
    return stroke(pts, W_SECONDARY)


# C  the belly: the Manta from above, its armed bombs carried under its body as solid shells, empty bays as outlines.
C_MANTA = manta(112, cy=56)


def c_belly(armed, capacity):
    pos = [(64, 92), (46, 102), (82, 102), (30, 112), (98, 112)][:capacity]
    out = empty()
    for i, (x, y) in enumerate(pos):
        out |= bomb(x, y, 8.5, held=i < armed)
    return _clear_of(out, C_MANTA(), px=2.0) | (out & ~toy_portraits.grown(C_MANTA(), 0.5))


STING_BEFORE = "Assets/_Graphics/Icons/AbilityIcons/Manta/Manta_Sting.png"
STING_GAUGE = {"empty bay": (0.4, None), "2 of 3 armed": (0.7, None), "Charge 15: 4 of 5": (0.2, None)}
LOCKUP = dict(
    Charge=dict(parts=dict(i=lambda: _png("Manta_Sting")), state=[("i", "white")], gauge=0.5),
    Mass=dict(parts=dict(i=lambda: _png("Manta_Yastri")), state=[("i", "white")], gauge=0.0),
    Space=dict(parts=dict(i=lambda: _png("Manta_Kabloom")), state=[("i", "white")], gauge=0.0),
    Time=dict(parts=dict(i=lambda: _png("Manta_Soar")), state=[("i", "white")], gauge=0.0),
)


def _png(name):
    import numpy as np
    from PIL import Image
    from canvas import N
    root = _os.path.abspath(_os.path.join(_os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(_os.path.join(root, f"Assets/_Graphics/Icons/AbilityIcons/Manta/{name}.png")).convert("RGBA").split()[3]
    return np.asarray(a.resize((N, N), Image.LANCZOS)) > 127


CARDS = [
    dict(name="sting_a", group="sting", element="Charge", before=STING_BEFORE, before_gauge=True, gauge=STING_GAUGE,
         title="A  the bay: the Manta (domain) from above, its bomb bay under it - solid shells armed, outlines empty",
         parts=dict(m=A_MANTA, b0=lambda: a_bay(0, 3), b2=lambda: a_bay(2, 3), b4=lambda: a_bay(4, 5)),
         states=[("empty bay", [("m", "domain"), ("b0", "white")]), ("2 of 3 armed", [("m", "domain"), ("b2", "white")]),
                 ("Charge 15: 4 of 5", [("m", "domain"), ("b4", "white")])], lockup_state=1),
    dict(name="sting_b", group="sting", element="Charge", before=STING_BEFORE, before_gauge=True, gauge=STING_GAUGE,
         title="B  the plant: the Manta (domain) grazing a rival (danger), a bomb (white) stuck to it, its fuse burning",
         parts=dict(m=B_MANTA, r=b_rival, b=b_bomb, f=b_fuse),
         states=[("empty bay", [("m", "domain"), ("r", "danger", 0.5)]),
                 ("2 of 3 armed", [("m", "domain"), ("r", "danger"), ("b", "white"), ("f", "white")]),
                 ("Charge 15: 4 of 5", [("m", "domain"), ("r", "danger"), ("b", "white"), ("f", "white")])], lockup_state=1),
    dict(name="sting_c", group="sting", element="Charge", before=STING_BEFORE, before_gauge=True, gauge=STING_GAUGE,
         title="C  the belly: the Manta (domain) from above, its armed bombs (white) carried under it, empty bays outlined",
         parts=dict(m=C_MANTA, b0=lambda: c_belly(0, 3), b2=lambda: c_belly(2, 3), b4=lambda: c_belly(4, 5)),
         states=[("empty bay", [("m", "domain"), ("b0", "white")]), ("2 of 3 armed", [("m", "domain"), ("b2", "white")]),
                 ("Charge 15: 4 of 5", [("m", "domain"), ("b4", "white")])], lockup_state=1),
]
