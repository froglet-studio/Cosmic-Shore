"""The Orca's ability-row icons (Resources/ElementalAbilityMaps/Orca.asset, Crew/ORCA_CREW.md), its own since 2026-11-13.

The Orca (4 seat) flew the Sparrow's HUD variant under its own controller until now, so its cards drew the Sparrow's
rocket count and stance hull. Each card is one element's seat and one picture; its level-5 upgrade is a second sprite
on the same card (VesselHUDView.AbilityIconBinding.upgradedSprite, swapped by the base view when the upgrade unlocks).

  Charge  Flown Missiles   a fly-by-wire missile on its hand-flown S path, nose on a rival (danger is the HUD's
                           tint, not drawn here). Charge 5 REARM: a second missile racked under it, a magazine refilling.
  Mass    Danger Shield    the Orca from above inside a shell of prisms open ahead. Mass 5 BULWARK: two shells.
  Space   Burst            a missile bursting, the blast's ring round it. Space 5 CHAIN BURST: two bursts, the second
                           set off inside the first's ring.
  Time    Boost Tank       the Orca from above with a speed wedge behind it and the tank (a bar) beside it.
                           Time 5 AFTERBURNER: the wedge with a refill chevron feeding the tank while it burns.

The hull is Orca.fbx from above (the hovering assembly, its outrigger turrets out to each side). No option round
(Garrett 2026-11-07: build the recommended option with full polish; he reviews the roster at the end).
"""
import math

import numpy as np

import mesh_silhouette
import motifs
import toy_portraits
from canvas import band, disc, empty, fill, ring, rotate, stroke

HULL = "Orca"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Orca/"
FBX = "Assets/_Models/Vessel Models/Orca.fbx"
SPARROW = "Assets/_Graphics/Icons/AbilityIcons/Sparrow/"


def _clear_of(mask, *others, px=3.0):
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


def orca(cx=64.0, cy=64.0, box=52.0, heading=0.0):
    """The Orca from above, nose RIGHT, turned `heading` deg clockwise; its floating parts closed a little so the
    assembly holds as one hull at 40 px."""
    return mesh_silhouette.silhouette(FBX, pitch=0.0, yaw=heading, box=box, cx=cx, cy=cy, dilate_px=1.4, close_px=2.0)


def missile(x, y, length=34.0, heading=0.0, w=8.0):
    """A flown missile: a slim body with a pointed nose and two swept tail fins standing well clear of the body (so it
    reads as a missile and not a blade), nose along `heading` (deg clockwise, 0 = right), its centre at (x, y)."""
    h = length / 2
    nose = w * 1.3
    body = [(x + h, y), (x + h - nose, y - w / 2), (x - h + 2, y - w / 2), (x - h + 2, y + w / 2), (x + h - nose, y + w / 2)]
    fin = w * 0.9
    fins = [(x - h + fin * 1.6, y - w / 2), (x - h, y - w / 2 - fin), (x - h, y + w / 2 + fin), (x - h + fin * 1.6, y + w / 2)]
    return fill(rotate(body, heading, x, y)) | fill(rotate(fins, heading, x, y))


def s_path(pts, w=5.0, seg=11.0, gap=6.0):
    return motifs.trail(pts, w=w, seg=seg, gap=gap)


# ---- Charge: Flown Missiles / Rearm -------------------------------------------------------------------------------
PATH = [(8, 118), (20, 114), (32, 106), (42, 96)]


def flown_missiles():
    m = missile(76, 50, 80, heading=-40, w=16)
    return m | _clear_of(s_path(PATH, w=11.0, seg=12.0, gap=6.0), m, px=4)


def rearm():
    m = missile(82, 38, 60, heading=-40, w=14)
    racked = missile(44, 88, 60, heading=-40, w=14)
    return m | racked


# ---- Mass: Danger Shield / Bulwark --------------------------------------------------------------------------------
def _shell(r, w=7.0, open_deg=46.0, pieces=7):
    """A shell of prism plates round (64, 64), open ahead (the right), cut into `pieces` plates so it reads as prisms."""
    out = empty()
    span = 360.0 - 2 * open_deg
    step = span / pieces
    for i in range(pieces):
        a0 = open_deg + i * step + 3.0
        out |= band(64.0, 64.0, r, a0, a0 + step - 6.0, w=w)
    return out


def danger_shield():
    return orca(64, 64, 52) | _shell(52.0, w=9.0)


def bulwark():
    return orca(64, 64, 38) | _shell(34.0, w=7.0) | _shell(50.0, w=7.0, pieces=9)


# ---- Space: Burst / Chain Burst ------------------------------------------------------------------------------------
def burst_icon():
    core = motifs.burst(72.0, 56.0, 30.0, spikes=8, inner=0.6)
    m = missile(30, 96, 38, heading=-40, w=9)
    rg = ring(72.0, 56.0, 46.0, w=8.0) & ~toy_portraits.grown(m, 4)
    return core | rg | m


def chain_burst():
    a = motifs.burst(44.0, 78.0, 22.0, spikes=8, inner=0.6)
    b = motifs.burst(88.0, 40.0, 24.0, spikes=8, inner=0.6, rot=12.0)
    ra = ring(44.0, 78.0, 34.0, w=8.0)
    return a | b | _clear_of(ra, b, px=4)


# ---- Time: Boost Tank / Afterburner --------------------------------------------------------------------------------
def _tank(fill01, x0=10.0, y0=92.0, x1=118.0, y1=116.0):
    from canvas import chamfered_rect
    outline = chamfered_rect(x0, y0, x1, y1, 4.0) & ~chamfered_rect(x0 + 6, y0 + 6, x1 - 6, y1 - 6, 2.0)
    level = chamfered_rect(x0 + 11, y0 + 11, x0 + 11 + (x1 - x0 - 22) * fill01, y1 - 11, 0.5)
    return outline | level


def boost_tank():
    hull = orca(78, 46, 60)
    wedge = _clear_of(motifs.speed_wedge(16, 46, 34, 14), hull, px=4)
    return hull | wedge | _tank(0.6)


def afterburner():
    """Burning AND refilling: a long double flame behind the hull, and the tank full with a chevron feeding it."""
    hull = orca(84, 40, 50)
    flame = _clear_of(motifs.speed_wedge(16, 32, 44, 11) | motifs.speed_wedge(20, 50, 34, 11), hull, px=4)
    feed = motifs.arrowhead((64.0, 92.0), (0.0, 1.0), 18.0, spread=0.8)
    return hull | flame | _clear_of(_tank(1.0), feed, px=3) | feed


ICONS = [
    dict(name="Orca_FlownMissiles", element="Charge", act="Flown Missiles", draw=flown_missiles),
    dict(name="Orca_Rearm", element="Charge", act="Charge 5 Rearm", draw=rearm),
    dict(name="Orca_DangerShield", element="Mass", act="Danger Shield", draw=danger_shield,
         exempt={"gap": "the hull's real mesh: a hovering assembly of floating parts by design (vessel-model SKILL 0.5)",
                 "components": "a shell of separate prism plates by design"}),
    dict(name="Orca_Bulwark", element="Mass", act="Mass 5 Bulwark", draw=bulwark,
         exempt={"gap": "the hull's real mesh: a hovering assembly of floating parts by design (vessel-model SKILL 0.5)",
                 "components": "two shells of separate prism plates by design"}),
    dict(name="Orca_Burst", element="Space", act="Burst", draw=burst_icon),
    dict(name="Orca_ChainBurst", element="Space", act="Space 5 Chain Burst", draw=chain_burst),
    dict(name="Orca_BoostTank", element="Time", act="Boost Tank", draw=boost_tank,
         exempt={"gap": "the hull's real mesh: a hovering assembly of floating parts by design (vessel-model SKILL 0.5)"}),
    dict(name="Orca_Afterburner", element="Time", act="Time 5 Afterburner", draw=afterburner,
         exempt={"gap": "the hull's real mesh: a hovering assembly of floating parts by design (vessel-model SKILL 0.5)",
                 "components": "the hull's floating parts, two flame marks, the tank and its feed chevron"}),
]
