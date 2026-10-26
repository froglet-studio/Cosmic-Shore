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
    dict(name="Manta_Yastri", element="Mass", act="Yastri", draw=yastri),
    dict(name="Manta_Kabloom", element="Space", act="Kabloom", draw=kabloom),
    dict(name="Manta_Soar", element="Time", act="Soar", draw=soar),
]
