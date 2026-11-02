"""The Honeyguide's ability-row icons, round 1 (2026-11-01): the four element cards of the new map and the thirteen
abilities a Ratel card can bank onto a trigger. Garrett, 2026-11-01: "so we will need an icon for each ability that can
take over". The map (HONEYGUIDE_LAB.md round 8; the Unity port wires these later):

  Time    Slip      passive: touch a trail of a domain the battery holds, nose along it, and zip to its end - the
                    hull (its REAL mesh, plan view, the grid's view A) at the head of the trail it is riding
  Space   Peck      RT base, always reduces opposing prism volume: a prism with a bite knocked out of it (the cut is
                    what the shots took, negative space; LESSONS 57) and the three shots of the burst going in
  Charge  Flit      LT base, always strikes vessels and fauna: the hull darting 18 u, one motion bar behind it, its
                    nose into a smaller rival hull (the opponent is the same hull, smaller; LESSONS 26)
  Mass    Hive      passive: the hive the Honeyguide plants in the cell - a honeycomb of shared walls, the centre
                    cell full, the next cell's honey (a ghost) where its walls are not built yet

RT, Space (each reduces opposing prism volume; what is destroyed is negative space or pieces apart):
  Genesis Lance        a lance thrown up the card, the prism on its line split, a slip lane laid behind it
  Little Bang Grenade  the lob: a grenade on a dashed arc, the burst where it lands
  Particle Harmonizer  a cone from its apex; the prisms inside it knocked clear and turned (solid islands in the beam)
  Fugacity Discharger  the discharge ring: an annulus between an inner and an outer reach, its outer edge jagged
  Sepia Drone          the drone on its orbit band, a zig-zag zap to the prism it is wearing down
  Thruster Swarm       the hull in quick repeated thrusts: two smaller ghosts behind it
  Hexapod Eyelet Jet   one long jet: a plume behind the hull, a prism broken into halves thrown off its line

LT, Charge (each strikes a rival vessel or creature; the jumps strike where they land):
  Hard Light Lasso     a loop round a small rival hull, the tether back to where it was thrown from
  Blink Drive          the hull's ghost and the hull ahead of it, no path between, the strike burst at its nose
  All Terrain Portals  the hull leaving a portal ring, the second ring ahead of it
  Rosen Bridge         the bridge: one arc from the hull to the far prism of its newest charge's domain
  FTL Chromatool       the swap: the hull and a prism, two arcs trading them
  Monte Cannon         three hops: three prisms, a hop arc landing on each, the last ending in an arrowhead

Before: none. The Honeyguide still draws the Sparrow's four icons (Sparrow_*.png), bound by guid on its HUD; the Unity
port binds these. Each is a single white sprite (the HUD tints a card by team and state); the card's queue of banked
uses (ticks) is the card's chrome, not the sprite.
"""
import math

import numpy as np

import mesh_silhouette
from canvas import SS, W_SECONDARY, band, disc, fill, ngon, rect, rotate, shift, stroke
from motifs import arrowhead, burst, prism, speed_wedge, trail

HULL = "Honeyguide"
FBX = "Assets/_Models/Vessel Models/Honeyguide.fbx"
HULL_GAP = {"gap": "the hull's real mesh: the beak, turret, wings and tail fan float clear of the body by design "
                   "(Docs/VesselModels/Honeyguide.md, the hovering assembly; vessel-model SKILL 0.5)"}
_lin = (np.arange(128 * SS) + 0.5) / SS
_GX, _GY = np.meshgrid(_lin, _lin)


def _dilate(mask, px):
    """Grow a mask by `px` icon px (as specs/grizzly.py)."""
    r = int(round(px * SS))
    out = mask.copy()
    for dx in range(-r, r + 1, max(1, r // 4)):
        for dy in range(-r, r + 1, max(1, r // 4)):
            out |= np.roll(np.roll(mask, dy, axis=0), dx, axis=1)
    return out


def _hull(box, cx, cy, roll=0.0, mirror=False, dilate=1.4, close=0.0):
    """The Honeyguide from above (the grid's view A, nose right), thickened for the 60 px card."""
    return mesh_silhouette.silhouette(FBX, pitch=0, yaw=0, box=box, cx=cx, cy=cy, dilate_px=dilate, close_px=close,
                                      roll=roll, mirror=mirror)


def _clear(top, under, px=5.0):
    """`under` cut clear of `top` (a secondary part never touches the focal one; LESSONS 50)."""
    return under & ~_dilate(top, px)


def _ellipse_ring(cx, cy, rx, ry, w, rot=0.0):
    pts = [(cx + rx * math.cos(math.radians(a)), cy + ry * math.sin(math.radians(a))) for a in range(0, 360, 6)]
    return stroke(rotate(pts, rot, cx, cy), w, closed=True)


def _split_prism(cx, cy, s, gap=7.0, angle=-35.0):
    """A prism cut in two along a line through its centre at `angle`, the halves pushed apart (a broken thing is its
    pieces apart; LESSONS 51)."""
    p = prism(cx, cy, s)
    a = math.radians(angle)
    nx, ny = -math.sin(a), math.cos(a)
    side = ((_GX - cx) * nx + (_GY - cy) * ny) < 0
    return shift(p & side, -nx * gap / 2, -ny * gap / 2) | shift(p & ~side, nx * gap / 2, ny * gap / 2)


# ------------------------------------------------------------------ the element row

def slip():
    """The hull at the head of the trail it is riding, up and to the right; the trail is its blocks (three)."""
    hull = _hull(60, 90, 38, roll=-34)
    lane = trail([(10, 118), (66, 62)], 15, seg=20, gap=8)
    return hull | _clear(hull, lane)


def peck():
    """A big prism with a bite knocked out of its right shoulder; the three shots of the burst going in."""
    body = prism(50, 68, 50) & ~fill([(66, 26), (100, 52), (86, 72), (58, 52)])
    shots = None
    for (x, y) in [(90, 22), (102, 40), (108, 60)]:
        sh = fill(rotate([(x - 10, y - 4), (x + 10, y - 4), (x + 6, y + 4), (x - 14, y + 4)], 35, x, y))
        shots = sh if shots is None else shots | sh
    return body | _clear(body, shots, 4)


def flit():
    """The hull darting right into a rival (mirrored: facing back), the strike burst between them."""
    hull = _hull(62, 40, 66)
    rival = _hull(38, 102, 66, mirror=True)
    hit = burst(78, 66, 10, spikes=7, inner=0.45, rot=0)
    return hull | _clear(hull, rival, 5) | _clear(hull | rival, hit, 3)


def hive():
    """A honeycomb of shared walls (one connected lattice), the centre cell full of honey, the next cell's honey a
    ghost above the comb where its walls are not built yet."""
    r = 19.0
    dx, dy = r * 1.5, r * math.sqrt(3) / 2
    cy = 66
    walls = None
    for (x, y) in [(0, 0), (dx, -dy), (dx, dy), (-dx, -dy), (-dx, dy), (0, 2 * dy)]:
        w = stroke(ngon(6, r, 64 + x, cy + y, rot=0), W_SECONDARY + 1, closed=True)
        walls = w if walls is None else walls | w
    honey = fill(ngon(6, r, 64, cy, rot=0))
    laid = stroke(ngon(6, r, 64, cy - 2 * dy, rot=0), W_SECONDARY + 1, closed=True) & ~_dilate(walls, 0.5)
    return [(walls | honey, 1.0), (laid, 0.6)]


# ------------------------------------------------------------------ RT (Space): reduces opposing prism volume

def genesis_lance():
    """The lance thrown up the card between the two halves of the prism it split; the slip lane laid on its line
    behind it (two blocks)."""
    a = -45.0
    spear = fill(rotate([(30, 59), (86, 59), (86, 53), (118, 64), (86, 75), (86, 69), (30, 69)], a, 64, 64))
    halves = _split_prism(96, 32, 16, gap=22, angle=a)
    lane = trail([(14, 114), (32, 96)], 13, seg=26, gap=7)
    return spear | _clear(spear, halves, 4) | _clear(spear, lane, 5)


def little_bang_grenade():
    """The lob: a dashed arc from the lower left, the grenade on it, the burst where it lands."""
    arc = band(72, 112, 60, 194, 250, w=W_SECONDARY + 2)
    for a in (212, 232):   # dashes: cut the arc
        x, y = 72 + 60 * math.cos(math.radians(a)), 112 + 60 * math.sin(math.radians(a))
        arc &= ~disc(x, y, 5)
    nade = fill(ngon(8, 16, 48, 56, rot=22.5)) | rect(43, 34, 53, 42)
    boom = burst(94, 88, 30, spikes=9, inner=0.5, rot=-90)
    return nade | _clear(nade, arc, 5) | _clear(nade, boom, 5)


def particle_harmonizer():
    """A cone from its apex at the left; the prisms inside it knocked clear and turned: solid islands in the beam."""
    cone = fill([(8, 64), (120, 18), (120, 110)])
    prisms = prism(72, 64, 15) | prism(104, 44, 10) | prism(104, 86, 10)
    return (cone & ~_dilate(prisms, 6)) | prisms


def fugacity_discharger():
    """The discharge round the Honeyguide: a ring between an inner and an outer reach, its outer edge a ragged
    blast (spike lengths varied: a blast, not a gear), the hull at its heart."""
    outer = burst(64, 64, 58, spikes=11, inner=0.7, rot=-90, jitter=(1.0, 0.78, 0.94, 0.84, 1.0, 0.88, 0.8))
    inner = disc(64, 64, 30)
    hull = _hull(34, 64, 64)
    return (outer & ~inner) | hull


def sepia_drone():
    """The drone (a round hull with a notch) on its orbit band; a zig-zag zap to the prism it is wearing down."""
    drone = disc(36, 34, 16) & ~fill([(36, 34), (58, 26), (58, 42)])
    orbit = band(64, 64, 52, 252, 400, w=W_SECONDARY)
    zap = stroke([(50, 46), (68, 58), (58, 66), (78, 80)], W_SECONDARY)
    target = prism(92, 96, 22) & ~fill([(74, 76), (94, 72), (86, 90)])
    return drone | _clear(drone | zap | target, orbit, 5) | _clear(drone | target, zap, 3) | target


def thruster_swarm():
    """The hull in quick repeated thrusts: two smaller ghosts behind it, each the same hull, spaced apart."""
    def h(box, cx):
        return _hull(box, cx, 64, dilate=2.2, close=2.0)
    return [(h(50, 92), 1.0), (h(40, 52) | h(30, 22), 0.6)]


def hexapod_eyelet_jet():
    """One long jet: a thick plume tapering back from the hull's tail; a prism broken into halves thrown above and
    below its line."""
    hull = _hull(50, 94, 64)
    plume = fill([(8, 58), (72, 54), (72, 74), (8, 70)])
    halves = _split_prism(36, 64, 22, gap=26, angle=0)
    return hull | (plume & ~_dilate(halves, 4)) | halves


# ------------------------------------------------------------------ LT (Charge): strikes vessels and fauna

def hard_light_lasso():
    """A noose round a rival hull, open where the rope leaves it, the rope curling back down to the lower left where
    it was thrown from (curved, so it is a rope and not a handle)."""
    rival = _hull(40, 80, 42, mirror=True)
    loop_pts = [(80 + 32 * math.cos(math.radians(t)), 42 + 26 * math.sin(math.radians(t))) for t in range(150, 480, 6)]
    loop = stroke(rotate(loop_pts, -15, 80, 42), W_SECONDARY + 2)
    rope = stroke([(52, 62), (44, 80), (30, 88), (22, 100), (12, 118)], W_SECONDARY + 2)
    return rival | _clear(rival, loop | rope, 5)


def blink_drive():
    """The hull's ghost at the left, the hull ahead of it with no path between, the strike burst at its nose."""
    hull = _hull(64, 72, 64)
    ghost = _hull(44, 28, 64)
    hit = burst(110, 64, 11, spikes=7, inner=0.45, rot=0)
    return [(hull | _clear(hull, hit, 4), 1.0), (_clear(hull, ghost, 6), 0.6)]


def all_terrain_portals():
    """The hull leaving a portal ring at the left; the second ring ahead of it."""
    r1 = _ellipse_ring(30, 64, 13, 40, W_SECONDARY + 2)
    r2 = _ellipse_ring(104, 64, 13, 40, W_SECONDARY + 2)
    hull = _hull(50, 64, 64)
    return hull | _clear(hull, r1 | r2, 5)


def rosen_bridge():
    """The bridge: one arc from the hull at the left to the far prism at the right."""
    hull = _hull(46, 28, 92, roll=-50)
    arch = band(66, 104, 50, 196, 344, w=W_SECONDARY + 2)
    far = prism(108, 96, 18)
    return hull | far | _clear(hull | far, arch, 5)


def ftl_chromatool():
    """The swap: the hull above, the prism below, two arcs trading their places."""
    hull = _hull(52, 64, 32)
    pr = prism(64, 96, 17)
    def arc(a0, a1):
        end = math.radians(a1)
        tx, ty = -math.sin(end), math.cos(end)
        ex, ey = 64 + 46 * math.cos(end), 64 + 46 * math.sin(end)
        return band(64, 64, 46, a0, a1, w=W_SECONDARY) | arrowhead((ex + tx * 11, ey + ty * 11), (tx, ty), 16)
    left = arc(150, 205)     # up from the prism's side to the hull's
    right = arc(-25, 30)     # down from the hull's side to the prism's
    return hull | pr | _clear(hull | pr, left | right, 5)


def monte_cannon():
    """Three hops between prisms: three prisms on a line, a hop arc landing on each, the last ending in an arrowhead."""
    ps = prism(18, 100, 15) | prism(64, 100, 15) | prism(110, 100, 15)
    hops = band(41, 92, 23, 190, 350, w=W_SECONDARY + 1) | band(87, 92, 23, 190, 322, w=W_SECONDARY + 1)
    head = arrowhead((108, 84), (0.55, 0.85), 16)
    return ps | hops | head


SUB = "Upgrades"
ICONS = [
    dict(name="Honeyguide_Flit", element="Charge", act="Flit (LT base): dart into a rival", draw=flit, exempt=HULL_GAP),
    dict(name="Honeyguide_Hive", element="Mass", act="Hive (passive): plant a hive", draw=hive),
    dict(name="Honeyguide_Peck", element="Space", act="Peck (RT base): a burst that knocks prism volume off", draw=peck),
    dict(name="Honeyguide_Slip", element="Time", act="Slip (passive): ride a trail to its end", draw=slip, exempt=HULL_GAP),
    # RT, Space
    dict(name="Honeyguide_GenesisLance", element="Space", act="Genesis Lance", draw=genesis_lance, subdir=SUB),
    dict(name="Honeyguide_LittleBangGrenade", element="Space", act="Little Bang Grenade", draw=little_bang_grenade, subdir=SUB),
    dict(name="Honeyguide_ParticleHarmonizer", element="Space", act="Particle Harmonizer", draw=particle_harmonizer, subdir=SUB),
    dict(name="Honeyguide_FugacityDischarger", element="Space", act="Fugacity Discharger", draw=fugacity_discharger, subdir=SUB),
    dict(name="Honeyguide_SepiaDrone", element="Space", act="Sepia Drone", draw=sepia_drone, subdir=SUB),
    dict(name="Honeyguide_ThrusterSwarm", element="Space", act="Thruster Swarm", draw=thruster_swarm, subdir=SUB, exempt=HULL_GAP),
    dict(name="Honeyguide_HexapodEyeletJet", element="Space", act="Hexapod Eyelet Jet", draw=hexapod_eyelet_jet, subdir=SUB, exempt=HULL_GAP),
    # LT, Charge
    dict(name="Honeyguide_HardLightLasso", element="Charge", act="Hard Light Lasso", draw=hard_light_lasso, subdir=SUB, exempt=HULL_GAP),
    dict(name="Honeyguide_BlinkDrive", element="Charge", act="Blink Drive", draw=blink_drive, subdir=SUB, exempt=HULL_GAP),
    dict(name="Honeyguide_AllTerrainPortals", element="Charge", act="All Terrain Portals", draw=all_terrain_portals, subdir=SUB, exempt=HULL_GAP),
    dict(name="Honeyguide_RosenBridge", element="Charge", act="Rosen Bridge", draw=rosen_bridge, subdir=SUB, exempt=HULL_GAP),
    dict(name="Honeyguide_FTLChromatool", element="Charge", act="FTL Chromatool", draw=ftl_chromatool, subdir=SUB, exempt=HULL_GAP),
    dict(name="Honeyguide_MonteCannon", element="Charge", act="Monte Cannon", draw=monte_cannon, subdir=SUB),
]

# The two trigger cards' STATES sheets (states.py): the base ability, then an upgrade the Ratel banked as the card's
# current icon. The queue of ticks under the icon (one per banked use, each in its ability's shade of the HUD blue) is
# the card's chrome and is not drawn by the kit yet: the Unity port adds it to the lockup.
CARDS = [
    dict(name="rt_space", element="Space", title="RT: Peck, or the upgrade banked on top of the queue",
         parts=dict(peck=peck, lance=genesis_lance, harmonizer=particle_harmonizer, jet=hexapod_eyelet_jet),
         states=[("base: Peck", [("peck", "white")]), ("current: Genesis Lance", [("lance", "white")]),
                 ("current: Particle Harmonizer", [("harmonizer", "white")]), ("current: Hexapod Eyelet Jet", [("jet", "white")])]),
    dict(name="lt_charge", element="Charge", title="LT: Flit, or the upgrade banked on top of the queue",
         parts=dict(flit=flit, lasso=hard_light_lasso, blink=blink_drive, monte=monte_cannon),
         states=[("base: Flit", [("flit", "white")]), ("current: Hard Light Lasso", [("lasso", "white")]),
                 ("current: Blink Drive", [("blink", "white")]), ("current: Monte Cannon", [("monte", "white")])]),
]
