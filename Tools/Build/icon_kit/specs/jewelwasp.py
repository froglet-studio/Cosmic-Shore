"""The Jewel Wasp's ability row (R_VesselActions/JEWEL_WASP.md §2; map Resources/ElementalAbilityMaps/JewelWasp.asset).

The wasp flew the Tiger Moth's HUD (ButterflyHUDVariant) as a stand-in, so its cards drew the Moth's dust and drop.
These are its own, on its own JewelWaspHUDVariant (`Tools/Build/author_jewel_wasp_hud.py`), each level-5 card the
binding's `upgradedSprite`, and two cards that SWAP while the wasp rides a creature (JewelWaspHUDView.SetInHost):

  Charge  Attack     in a creature, RT at a rival vessel: the creature's attack lands a Strike (Charge = its recharge,
                     3 s -> 0.8 s). A creature lunging at a rival hull, the hit between them.
          L5 Burst   leaving bursts the host: the wasp thrown clear of the blast its host became.
  Mass    Root       LT at a plant: the wasp nests at its heart and the plant grows (Mass = how fast, x2 -> x6). A plant
                     with the wasp at its foot, its tip an arrowhead: growing.
          L5 Overgrowth  every plant round it grows as fast: three plants, the middle one the tallest.
  Space   Sting      free, RT at a creature: the wasp flies the whole cell to its heart (range = the cell's diameter). The
                     wasp diving at a creature whose heart is cut out of it.
          in a host  Suction: RT at prisms, the creature's mouth draws the nearest rival prisms in (Space = reach, x1 -> x3).
          L5 Maw     the suction pulls from every side: the creature with prisms drawn in from all round.
  Time    Swim       free: the host the wasp takes is faster (Time = x1.5 -> x3 its own top speed). A creature with the
                     fleet's one motion bar under it.
          in a host  Dash: LT, the burst. The creature and its ghost behind it, a jump of speed.
          L5 Restless  no wait between stings or bursts: the wasp and two ghosts on its line, sting after sting.

Real geometry: the wasp is its own mesh (`JewelWasp.fbx`, top view, nose right); a creature is the Jacks' herd fish (a
lens body, a forked tail: the fleet's "creature" on a card); a prism is the two close triangles (`motifs.prism`; never
the rhombus, Garrett 2026-11-01); a rival vessel is the fleet hull (`motifs.hull`).
"""
import math

import numpy as np

import mesh_silhouette
import motifs
from canvas import SS, disc, empty, fill, rotate, stroke

HULL = "JewelWasp"
WASP = "Assets/_Models/Vessel Models/JewelWasp.fbx"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/JewelWasp/"
APART = {"components": "the act is between separate things: a creature and its target, the wasp and its host",
         "gap": "the wasp's real mesh: its wings and gaster float clear of the core (vessel-model SKILL 0.5)"}


def wasp(box, cx, cy, yaw=0.0, roll=0.0, mirror=False):
    """The wasp from above, nose right (its own mesh, dilated so the masts and clamps survive 40 px)."""
    return mesh_silhouette.silhouette(WASP, pitch=0, yaw=yaw, box=box, cx=cx, cy=cy, roll=roll, mirror=mirror,
                                      dilate_px=2.2)


def grow(mask, px):
    from PIL import Image, ImageFilter
    r = int(px * SS) * 2 + 1
    return np.asarray(Image.fromarray((mask * 255).astype("uint8")).filter(ImageFilter.MaxFilter(r))) > 127


def creature(cx, cy, length, height, ang=0.0, heart=0.0):
    """A creature: a lens body nosed right and a forked tail, turned `ang` degrees clockwise; `heart` > 0 cuts its heart
    crystal out of it (the possession's target)."""
    n, x0 = 20, cx - length * 0.22
    top = [(x0 + length * 0.72 * i / n - length * 0.28, cy - height / 2 * math.sin(math.pi * i / n)) for i in range(n + 1)]
    bot = [(x0 + length * 0.72 * i / n - length * 0.28, cy + height / 2 * math.sin(math.pi * i / n)) for i in range(n, -1, -1)]
    tx = x0 - length * 0.28
    tail = [(tx + 2, cy), (tx - length * 0.26, cy - height * 0.5), (tx - length * 0.17, cy), (tx - length * 0.26, cy + height * 0.5)]
    m = fill(rotate(top + bot, ang, cx, cy)) | fill(rotate(tail, ang, cx, cy))
    if heart > 0:
        hx, hy = cx - length * 0.02, cy
        hx, hy = rotate([(hx, hy)], ang, cx, cy)[0]
        m &= ~fill([(hx, hy - heart), (hx + heart * 0.7, hy), (hx, hy + heart), (hx - heart * 0.7, hy)])
    return m


def plant(base_x, base_y, height, lean=0.0, leaves=2, w=7.0):
    """A plant: a stem rising from base, leaves off it alternately, its tip an arrowhead (it grows)."""
    tip = (base_x + lean, base_y - height)
    m = stroke([(base_x, base_y), (base_x + lean * 0.4, base_y - height * 0.55), (tip[0], tip[1] + height * 0.12)], w=w)
    m |= motifs.arrowhead((tip[0], tip[1]), (lean * 0.15, -1.0), height * 0.26, spread=0.7)
    for k in range(leaves):
        t = 0.30 + 0.30 * k
        side = 1 if k % 2 == 0 else -1
        sx, sy = base_x + lean * 0.4 * t, base_y - height * t
        L = height * 0.34
        a = math.radians(-35 if side > 0 else -145)
        ex, ey = sx + L * math.cos(a), sy + L * math.sin(a)
        nx, ny = -(ey - sy) / L, (ex - sx) / L
        mid = ((sx + ex) / 2, (sy + ey) / 2)
        m |= fill([(sx, sy), (mid[0] + nx * L * 0.24, mid[1] + ny * L * 0.24), (ex, ey), (mid[0] - nx * L * 0.24, mid[1] - ny * L * 0.24)])
    return m


# ------------------------------------------------------------------ Charge
def attack():
    """Attack: a creature lunging up-right at a rival hull; the strike a burst between them."""
    c = creature(66, 70, 78, 42, ang=-34)
    rival = motifs.hull(103, 24, 15, heading=145)
    hit = motifs.burst(84, 50, 11, spikes=8, inner=0.45, rot=10) & ~grow(rival, 3)
    return c | ((rival | hit) & ~grow(c, 3))


def burst_l5():
    """Charge 5 Burst: the host burst open, the wasp thrown clear up and right."""
    blast = motifs.burst(46, 78, 34, spikes=10, inner=0.5, rot=8)
    w = wasp(58, 88, 36, yaw=-30)
    return w | (blast & ~grow(w, 4))


# ------------------------------------------------------------------ Mass
def root():
    """Root: a tall plant growing, the wasp nested at its foot."""
    p = plant(76, 118, 104, lean=-6, leaves=2, w=9)
    w = wasp(44, 34, 96, yaw=-20)
    return p | (w & ~grow(p, 3))


def overgrowth():
    """Mass 5 Overgrowth: three plants grow together, the middle one tallest."""
    m = plant(64, 118, 104, leaves=2, w=8)
    for x, h in ((26, 70), (102, 70)):
        m |= plant(x, 118, h, leaves=1, w=7) & ~grow(m, 3)
    return m


# ------------------------------------------------------------------ Space
def sting():
    """Sting: the wasp diving across the cell at a creature, its heart cut out of it (where the sting lands)."""
    w = wasp(58, 36, 36, yaw=35)
    c = creature(86, 88, 62, 36, ang=0, heart=8)
    return w | (c & ~grow(w, 3))


def suction():
    """Suction (in a creature): the creature's mouth drawing the nearest rival prisms in, smallest nearest the mouth."""
    c = creature(54, 64, 60, 40)
    p = empty()
    for x, s in ((80, 8), (96, 10), (112, 12)):
        p |= motifs.prism(x, 64, s)
    return c | (p & ~grow(c, 3))


def maw():
    """Space 5 Maw: the suction pulls from every side - prisms drawn in from all round the creature."""
    c = creature(64, 64, 58, 34)
    p = empty()
    for a in (0, 90, 180, 270):
        r = 44
        x, y = 64 + r * math.cos(math.radians(a)), 64 + r * math.sin(math.radians(a))
        p |= motifs.prism(x, y, 13)
    return c | (p & ~grow(c, 3))


# ------------------------------------------------------------------ Time
def swim():
    """Swim (free): the host the wasp takes is faster - a creature over the fleet's one motion bar."""
    c = creature(72, 54, 88, 50)
    bar = motifs.speed_wedge(18, 100, 88, 13, lean=0.0)
    return c | (bar & ~grow(c, 3))


def dash():
    """Dash (in a creature, LT): the burst - the creature leaps ahead of its own ghost."""
    c = creature(104, 64, 62, 58)
    ghost = creature(56, 64, 62, 58) & ~grow(c, 3)
    return [(c, 1.0), (ghost, 0.45)]


def restless():
    """Time 5 Restless: no wait between stings - the wasp and two ghosts on its line, one sting after another."""
    w = wasp(52, 92, 40, yaw=-28)
    g1 = wasp(42, 58, 70, yaw=-28) & ~grow(w, 2.5)
    g2 = wasp(34, 26, 96, yaw=-28) & ~grow(w | g1, 2.5)
    return [(w, 1.0), (g1, 0.55), (g2, 0.3)]


ICONS = [
    dict(name="JewelWasp_Attack", element="Charge", act="Attack: in a creature, RT at a rival vessel strikes it",
         draw=attack, exempt=APART),
    dict(name="JewelWasp_Burst", element="Charge", act="Charge 5 Burst: leaving bursts the host round its rivals",
         draw=burst_l5, exempt=APART),
    dict(name="JewelWasp_Root", element="Mass", act="Root: LT at a plant, the wasp nests and the plant grows",
         draw=root, exempt=APART),
    dict(name="JewelWasp_Overgrowth", element="Mass", act="Mass 5 Overgrowth: every plant near it grows as fast",
         draw=overgrowth, exempt=APART),
    dict(name="JewelWasp_Sting", element="Space", act="Sting: RT at a creature, the wasp flies to its heart",
         draw=sting, exempt=APART),
    dict(name="JewelWasp_Suction", element="Space", act="Suction: in a creature, RT draws the rival prisms in",
         draw=suction, exempt=APART),
    dict(name="JewelWasp_Maw", element="Space", act="Space 5 Maw: the suction pulls from every side",
         draw=maw, exempt=APART),
    dict(name="JewelWasp_Swim", element="Time", act="Swim: the host flies faster with Time",
         draw=swim, exempt=APART),
    dict(name="JewelWasp_Dash", element="Time", act="Dash: in a creature, LT is a burst of speed",
         draw=dash, exempt=APART),
    dict(name="JewelWasp_Restless", element="Time", act="Time 5 Restless: no wait between stings or bursts",
         draw=restless, exempt=APART),
]
