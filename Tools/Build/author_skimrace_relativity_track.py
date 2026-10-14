#!/usr/bin/env python3
"""Author Skim Race intensity 4 - "Relativity" - into MinigameSkimRace.unity.

A closed space curve that threads the cell's nucleus five times a lap, built from two pieces:

  * FIVE CORE PASSES - straight chords through the nucleus cage. Each starts from a strut of a
    tensegrity icosahedron (two parallel struts per axis, X split along Z, Y along X, Z along Y)
    and is then tilted off its axis and pushed 130-185 u from the centre, so the five chords cross
    the cage at five different places, like string art - never all at one point. Every pass is
    still a near-miss with the others (>= 139 u), and racers on different lobes keep seeing each
    other cut across the core.
  * FIVE LOBES - one petal per pair of consecutive passes, leaving along one chord and returning
    along the next. Every lobe has its own character: reach 700 -> 1070 u, apex turn radius
    215 -> 405 u (a hairpin, a sweeper and three between), petal fullness, out-of-plane warp and
    apex skew all differ, so no two lobes ride alike.

The parameter tables below were found by a cross-entropy search (offsets, tilts, half-lengths,
reach, fullness, warp, skew) that held strand clearance, turn radius, lap length, centre miss and
reach to the asserts below while pulling the lobes' turn radii and reaches onto a LADDER - the
first, fully symmetric version had six identical petals and six passes within 64 u of the centre,
and was rejected in review for exactly that ("repetition of curvature, piled up in the center").
The asserts now hold the line: MIN_LOBE_REACH_SPREAD, MIN_LOBE_TURN_SPREAD, MIN_CENTRE_MISS.

ONE PASS SNAKES (SNAKE): every lobe turns the same way round in the pilot's frame, so pass 4 bows
110 u sideways across its floor AGAINST that turn - right, left, right through lobe 3, the bow and
lobe 4 - and the lap no longer only turns one way (asserted: MIN_COUNTER_TURN_RUN).

MARKERS MARK CRYSTALS: the wide marker block says "a crystal appears near here", so waypoints are
laid with a knot at every crystal anchor and only those waypoints are listed in the track's
markedWaypoints (I1-I3 list none = every waypoint marked, unchanged).

The ribbon lies flat in each lobe's plane (its floor). Each core pass rolls the ribbon about the
direction of travel onto the next lobe's floor - here 2, 74, -74, 75 and 12 degrees: some passes
keep the floor, others turn it into a wall (Escher's Relativity). Authored as per-waypoint ribbon
normals (SpawnableWaypointTrack.waypointUps).

Crystals: one anchor on every core pass (its point of closest approach to the centre) and each
pass-to-pass span divided evenly at ~ANCHOR_SPACING, so a long lobe carries more crystals than a
short one. Crystals per lap is written to the track's crystalsPerLap (the waypoint list is a dense
spline sample, not one point per crystal); target = crystals per lap x LAPS.

The race starts at lobe 0's apex: the whole knot is rotated so that apex sits just ahead of the
shared spawn stack at (700, y, -200), heading +Z with the ribbon horizontal.

Everything is validated BEFORE a byte is written, on the prisms as SpawnableWaypointTrack.Spawn
actually lays them (12 u Catmull-Rom spacing, the same up interpolation):

  python3 Tools/Build/author_skimrace_relativity_track.py           # write
  python3 Tools/Build/author_skimrace_relativity_track.py --check   # fail on drift (CI)
  python3 Tools/Build/author_skimrace_relativity_track.py --report  # numbers only

Writes: the scene's SpawnableWaypointTrack (intensity-4 waypoints, spline flag, ribbon normals,
crystals per lap), its CrystalManager (intensity-4 anchors), and the baked copy of the track
component in Assets/_Prefabs/Environment/Spawners/SkimRaceWaypointTrack.prefab (the card's mode
preview reads that bake - see author_preview_tracks.py).
"""
import math
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCENE = os.path.join(ROOT, "Assets/_Scenes/Multiplayer Scenes/MinigameSkimRace.unity")
BAKE = os.path.join(ROOT, "Assets/_Prefabs/Environment/Spawners/SkimRaceWaypointTrack.prefab")
INTENSITY = 4

# ---------------------------------------------------------------- the design (tune here)
# Five core passes. Each is a straight chord through the nucleus cage: it starts from one strut of a
# tensegrity icosahedron (axis, which side of the axis it is offset to, direction of travel) and is
# then TILTED off that axis (a rotation vector, radians) and pushed OFFSET units from the centre, so
# the five chords cross the cage at six different places instead of piling up on the centre point.
# HALF is the chord's half-length; the lobe takes over where the chord ends.
#            axis side travel  tilt (rotation vector, rad)          offset   half
PASSES = [
    ("X",  1,  1, (-0.2618, -0.0922, -0.0055), 130.1, 297.6),
    ("Z", -1, -1, (0.1565, -0.0831, -0.2380), 185.5, 281.6),
    ("Y",  1,  1, (-0.1169, -0.0153, 0.2720), 174.2, 328.6),
    ("X", -1, -1, (-0.0442, 0.0741, -0.1904), 146.7, 352.3),
    ("Z",  1,  1, (0.1017, -0.0057, -0.0391), 175.2, 363.8),
]
# Five lobes, lobe i joining pass i's exit to pass i+1's entry: a petal in the plane of the two
# passes, r = base + (REACH - base) * sin(pi g)^FULL. Every lobe has its own character, so no two
# turns ride alike: REACH (how far out), FULL (0.33 = a pointed petal with a tight apex, 0.6 = a
# full round one), WARP (how far the lobe bows out of its plane, x REACH) and SKEW (the apex slides
# toward the exit or the entry arm, so the turn tightens early or late).
#          reach   full   warp    skew
LOBES = [
    (668.2, 0.567, -0.006, -0.118),
    (978.6, 0.484, -0.154, 0.218),
    (901.5, 0.541, -0.056, 0.062),
    (738.4, 0.528, 0.055, -0.211),
    (836.3, 0.474, 0.078, -0.074),
]
# One pass is not straight: it SNAKES round the other chords - bowing one way, then the other -
# so the core has a stretch of alternating curvature (every lobe is a long turn the same way round;
# without this the lap only ever turns one way). Displacement off the chord:
#   PERIODS 0.5:  A * sin^3(pi u)                      - one bow
#   otherwise:    A * sin(2 pi PERIODS u) * sin^2(pi u) - an S
# u = 0..1 along the pass; zero offset, slope and curvature at both ends, so the lobes still meet
# a straight line. SIDE is
# the bend direction about the pass, in degrees from the incoming floor's normal (90 = sideways
# across the floor, 0 = up off it). HALF_EXTRA lengthens that pass so the snake has room; the two
# lobes either side of it reach that much further too, so their petals keep the same room to turn.
#        pass  amplitude  periods  side   half_extra
SNAKE = (4,    110.0,     0.5,     270.0, 60.0)
WAYPOINT_SPACING = 70.0
ANCHOR_SPACING = 470.0  # target arc between crystal anchors; each pass->pass span is divided evenly
ANCHOR_LIFT = 0.0       # crystal anchors sit this far above the ribbon, along its normal (the local floor)
LAPS = 2

# ---------------------------------------------------------------- what the track must satisfy
SPAWN = (700.0, 0.0, -200.0)  # the scene's PlayerOrigin stack (y = -20..20), facing +Z
MIN_STRAND_CLEARANCE = 100.0  # prism centre to prism centre, strands >= 480 u apart along the track
MIN_CENTRE_MISS = 100.0       # no pass comes nearer the cell centre than this (the core must not pile up)
MIN_CURVATURE_RADIUS = 200.0  # Squirrel: 300 u/s at 120 deg/s turns on a 143 u circle, before lag
MIN_LOBE_REACH_SPREAD = 200.0 # farthest lobe reach - nearest lobe reach: the lobes must not repeat
MIN_LOBE_TURN_SPREAD = 60.0   # widest lobe turn radius - tightest: nor may their turns
MIN_COUNTER_TURN_RUN = 150.0  # the lap must hold a stretch at least this long turning AGAINST its
MAX_COUNTER_TURN_R = 400.0    # dominant direction (pilot's frame), at least this tightly - the snake
MAX_SPLINE_DEVIATION = 3.0    # laid ribbon vs the analytic curve
MAX_UP_STEP_DEG = 25.0        # ribbon roll between consecutive waypoints (the runtime interpolates)
LAP_RANGE = (10500.0, 13500.0)
NUCLEUS_R = 330.0             # a pass counts as threading the nucleus once it is inside this radius

AXES = {"X": (1.0, 0.0, 0.0), "Y": (0.0, 1.0, 0.0), "Z": (0.0, 0.0, 1.0)}
OFFSET_AXIS = {"X": "Z", "Y": "X", "Z": "Y"}   # tensegrity: which axis each strut pair is split along


# ---------------------------------------------------------------- vector helpers
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, k): return (a[0] * k, a[1] * k, a[2] * k)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a): return math.sqrt(dot(a, a))
def unit(a):
    n = norm(a)
    return mul(a, 1.0 / n) if n > 1e-12 else (0.0, 1.0, 0.0)
def lerp(a, b, t): return add(a, mul(sub(b, a), t))
def rotate_about(v, axis, ang):
    """Rodrigues: v rotated by ang (radians) about unit axis."""
    c, s = math.cos(ang), math.sin(ang)
    return add(add(mul(v, c), mul(cross(axis, v), s)), mul(axis, dot(axis, v) * (1 - c)))
def smoothstep(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def rotvec(v, w):
    """v rotated by the rotation vector w (axis * angle)."""
    ang = norm(w)
    return v if ang < 1e-12 else rotate_about(v, mul(w, 1.0 / ang), ang)


def strut(i):
    """(offset point, unit direction, half-length) of core pass i."""
    axis, side, travel, tilt, offset, half = PASSES[i % len(PASSES)]
    d = rotvec(mul(AXES[axis], float(travel)), tilt)
    o = mul(unit(rotvec(mul(AXES[OFFSET_AXIS[axis]], float(side)), tilt)), offset)
    if i % len(PASSES) == SNAKE[0]:
        half += SNAKE[4]
    return o, d, half


def snake_offset(i, u, floor_in, d):
    """Lateral displacement of pass i at u (0..1) - zero except on the SNAKE pass."""
    k, amp, periods, side_deg, _ = SNAKE
    if i != k or amp == 0.0:
        return (0.0, 0.0, 0.0)
    side = rotate_about(floor_in, d, math.radians(side_deg))
    if periods == 0.5:   # a single bow round the neighbouring chords: sin^3 keeps the ends straight
        return mul(side, amp * math.sin(math.pi * u) ** 3)
    return mul(side, amp * math.sin(2 * math.pi * periods * u) * math.sin(math.pi * u) ** 2)


def lobe_normal(i):
    """Plane normal of lobe i (between pass i and pass i+1): e1 x e2, e1 = out arm, e2 = in arm."""
    _, d0, _ = strut(i)
    _, d1, _ = strut(i + 1)
    return unit(cross(d0, mul(d1, -1.0)))


def slerp_dir(a, b, f):
    om = math.acos(max(-1.0, min(1.0, dot(a, b))))
    s = math.sin(om)
    return add(mul(a, math.sin((1 - f) * om) / s), mul(b, math.sin(f * om) / s))


def floor_normals():
    """Each lobe's floor (its plane normal, sign chosen so the pass into it rolls <= 90 degrees) and
    the signed roll each pass makes about its own direction of travel to get there."""
    n = len(PASSES)
    floors = [lobe_normal(0)]
    for i in range(1, n):
        nl = lobe_normal(i)
        floors.append(nl if dot(nl, floors[-1]) >= 0 or abs(dot(nl, floors[-1])) < 1e-9 else mul(nl, -1.0))
    rolls = []
    for i in range(n):
        _, d, _ = strut(i)
        a, b = floors[i - 1], floors[i]
        rolls.append(math.atan2(dot(cross(a, b), d), dot(a, b)))
    return floors, rolls


# ---------------------------------------------------------------- the analytic curve
def dense_curve(step=3.0, lobe_samples=1400):
    """[(point, normal, tag)] around one lap starting at pass 0's entry. tag = ('S'|'L', i, param)."""
    out = []
    n = len(PASSES)
    floors, rolls = floor_normals()
    for i in range(n):
        o0, d0, h0 = strut(i)
        o1, d1, h1 = strut(i + 1)
        # core pass i: roll from lobe i-1's floor to lobe i's floor about the travel axis
        m = int(2 * h0 / step)
        for k in range(m):
            u = k / m
            p = add(add(o0, mul(d0, -h0 + 2 * h0 * u)), snake_offset(i, u, floors[i - 1], d0))
            out.append((p, rotate_about(floors[i - 1], d0, rolls[i] * smoothstep(u)), ("S", i, u)))
        # lobe i: a petal from pass i's exit (along d0) back into pass i+1 (along d1)
        e1, e2 = d0, mul(d1, -1.0)
        nl = lobe_normal(i)
        reach, full, warp, skew = LOBES[i]
        if SNAKE[4] and i in ((SNAKE[0] - 1) % n, SNAKE[0]):
            reach += SNAKE[4]   # the snake pass is longer: its two lobes keep their room to turn
        for k in range(lobe_samples):
            # f eases in and out: r - base grows like sin^FULL, so uniform steps would leave the dense
            # reference coarse exactly where the lobe meets the pass.
            f = 0.5 * (1 - math.cos(math.pi * k / lobe_samples))
            g = f + skew * math.sin(math.pi * f) * f * (1 - f) * 4   # monotone for |skew| < 0.25
            sg = smoothstep(g)
            r = h0 + (h1 - h0) * sg + (reach - 0.5 * (h0 + h1)) * max(math.sin(math.pi * g), 0.0) ** full
            p = add(add(mul(slerp_dir(e1, e2, g), r), lerp(o0, o1, sg)), mul(nl, warp * reach * math.sin(math.pi * g)))
            out.append((p, floors[i], ("L", i, g)))
    return out


def arc_lengths(pts):
    s = [0.0]
    for k in range(1, len(pts) + 1):
        s.append(s[-1] + norm(sub(pts[k % len(pts)], pts[k - 1])))
    return s


def tangents(pts):
    n = len(pts)
    return [unit(sub(pts[(k + 1) % n], pts[k - 1])) for k in range(n)]


def sample_at(pts, s_tab, s):
    """Linear interpolation of a dense closed polyline at arc length s (index-tracking caller-free)."""
    total = s_tab[-1]
    s %= total
    lo, hi = 0, len(pts)
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if s_tab[mid] <= s:
            lo = mid
        else:
            hi = mid
    seg = s_tab[lo + 1] - s_tab[lo]
    t = (s - s_tab[lo]) / seg if seg > 0 else 0.0
    return lo, lerp(pts[lo], pts[(lo + 1) % len(pts)], t)


def resample_open(pts, step):
    s = [0.0]
    for k in range(1, len(pts)):
        s.append(s[-1] + norm(sub(pts[k], pts[k - 1])))
    out, j = [], 0
    x = 0.0
    while x <= s[-1]:
        while s[j + 1] < x:
            j += 1
        seg = s[j + 1] - s[j]
        out.append(lerp(pts[j], pts[j + 1], (x - s[j]) / seg if seg > 0 else 0.0))
        x += step
    return out


def circle_radius(a, b, c):
    ab, bc, ac = norm(sub(b, a)), norm(sub(c, b)), norm(sub(c, a))
    area = norm(cross(sub(b, a), sub(c, a))) / 2
    return ab * bc * ac / (4 * area) if area > 1e-9 else 1e9


def project_normal(nrm, tan):
    return unit(sub(nrm, mul(tan, dot(nrm, tan))))


def build():
    curve = dense_curve()
    pts = [c[0] for c in curve]
    nrms = [c[1] for c in curve]
    tans = tangents(pts)
    nrms = [project_normal(nv, tv) for nv, tv in zip(nrms, tans)]
    s_tab = arc_lengths(pts)
    lap = s_tab[-1]

    # Lobe 0's apex - its farthest point from the centre, where the tangent is square to the radius -
    # is the start line.
    lobe0 = [k for k, c in enumerate(curve) if c[2][0] == "L" and c[2][1] == 0]
    apex_k = max(lobe0, key=lambda k: norm(pts[k]))
    s0 = s_tab[apex_k]

    # Rigid rotation: apex radial -> +X, apex tangent -> +Z (so the ribbon is horizontal at the start).
    a = unit(pts[apex_k])
    t = unit(sub(tans[apex_k], mul(a, dot(tans[apex_k], a))))
    b = cross(t, a)
    rot = lambda v: (dot(a, v), dot(b, v), dot(t, v))
    pts = [rot(p) for p in pts]
    nrms = [rot(v) for v in nrms]
    tans = [rot(v) for v in tans]
    if nrms[apex_k][1] < 0:   # the start floor faces up (sign of a ribbon normal is cosmetic)
        nrms = [mul(v, -1.0) for v in nrms]

    # Anchors: one on every core pass, at its point of closest approach to the centre (the chord's
    # midpoint - the pickup sits inside the weave), and each pass-to-pass span (the lobe between
    # them) divided evenly at ~ANCHOR_SPACING. A long lobe carries more crystals than a short one, so
    # crystal density is even round the lap. Listed from the first one ahead of the start line.
    mids = [s_tab[next(k for k, c in enumerate(curve) if c[2][0] == "S" and c[2][1] == i and c[2][2] >= 0.5)]
            for i in range(len(PASSES))]
    anchor_abs = []
    for i in range(len(mids)):
        a0, a1 = mids[i], mids[(i + 1) % len(mids)] + (lap if i == len(mids) - 1 else 0.0)
        k = max(2, int(round((a1 - a0) / ANCHOR_SPACING)))
        anchor_abs += [a0 + (a1 - a0) * j / k for j in range(k)]
    anchor_s = sorted(((x - s0) % lap) for x in anchor_abs)
    anchor_s = [x for x in anchor_s if x > 1e-6] + [x for x in anchor_s if x <= 1e-6]
    # Waypoints: knots at the start apex and at every crystal anchor, each span between knots divided
    # evenly at ~WAYPOINT_SPACING. Every anchor is therefore a waypoint, and ONLY those waypoints are
    # marked (the wide marker block says "a crystal appears near here" - marking every waypoint of a
    # dense spline would say it everywhere).
    knots = [0.0] + [x for x in anchor_s if x > 1e-6]
    waypoint_s, marked = [], []
    for j, k0 in enumerate(knots):
        k1 = knots[j + 1] if j + 1 < len(knots) else lap
        if j > 0:
            marked.append(len(waypoint_s))
        m = max(1, int(round((k1 - k0) / WAYPOINT_SPACING)))
        waypoint_s += [k0 + (k1 - k0) * q / m for q in range(m)]
    if any(x <= 1e-6 for x in anchor_s):   # an anchor exactly on the start line marks waypoint 0
        marked.append(0)
    waypoints, ups = [], []
    for x in waypoint_s:
        k, p = sample_at(pts, s_tab, s0 + x)
        waypoints.append(p)
        ups.append(project_normal(nrms[k], tans[k]))

    anchors = []
    for x in anchor_s:
        k, p = sample_at(pts, s_tab, s0 + x)
        anchors.append(add(p, mul(project_normal(nrms[k], tans[k]), ANCHOR_LIFT)))
    anchor_tags = [curve[sample_at(pts, s_tab, s0 + x)[0]][2] for x in anchor_s]

    # Per-lobe character, measured (the variety asserts read these).
    lobe_reach, lobe_turn = [], []
    for i in range(len(LOBES)):
        idx = [k for k, c in enumerate(curve) if c[2][0] == "L" and c[2][1] == i]
        lobe_reach.append(max(norm(pts[k]) for k in idx))
        q = resample_open([pts[k] for k in idx], 12.0)
        lobe_turn.append(min(circle_radius(q[k - 3], q[k], q[k + 3]) for k in range(3, len(q) - 3)))

    return {"pts": pts, "s_tab": s_tab, "lap": lap, "waypoints": waypoints, "ups": ups,
            "anchors": anchors, "anchor_tags": anchor_tags, "curve": curve,
            "lobe_reach": lobe_reach, "lobe_turn": lobe_turn, "marked": marked}


# ---------------------------------------------------------------- SpawnableWaypointTrack, ported
def catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return tuple(0.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3)
                 for a, b, c, d in zip(p0, p1, p2, p3))


def spline_point(ps, seg, t):
    n = len(ps)
    return catmull(ps[(seg - 1) % n], ps[seg], ps[(seg + 1) % n], ps[(seg + 2) % n], t)


def lay(waypoints, ups, spacing=12.0):
    """[(position, forward, up)] exactly as SpawnableWaypointTrack.Spawn lays a spline intensity."""
    out = []
    n = len(waypoints)
    for seg in range(n):
        length, prev = 0.0, spline_point(waypoints, seg, 0.0)
        for k in range(1, 21):
            cur = spline_point(waypoints, seg, k / 20)
            length += norm(sub(cur, prev))
            prev = cur
        blocks = max(1, int(round(length / spacing)))
        for i in range(blocks):
            t = i / blocks
            pos = spline_point(waypoints, seg, t)
            look = spline_point(waypoints, seg, (i + 1) / blocks) if i < blocks - 1 else spline_point(waypoints, (seg + 1) % n, 0.0)
            up = spline_point(ups, seg, t)
            fwd = unit(sub(look, pos))
            out.append((pos, fwd, project_normal(up, fwd)))
    return out


# ---------------------------------------------------------------- validation
def validate(d):
    errs = []
    laid = lay(d["waypoints"], d["ups"])
    pos = [b[0] for b in laid]
    n = len(pos)

    # 1. strand clearance on the laid prisms (cells for speed)
    gap = int(480 / 12)
    cell = 80.0
    grid = {}
    for k, p in enumerate(pos):
        grid.setdefault(tuple(int(math.floor(c / cell)) for c in p), []).append(k)
    best = (1e9, None)
    for k, p in enumerate(pos):
        cx, cy, cz = (int(math.floor(c / cell)) for c in p)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for j in grid.get((cx + dx, cy + dy, cz + dz), ()):
                        if min(abs(j - k), n - abs(j - k)) < gap:
                            continue
                        dd = norm(sub(pos[j], p))
                        if dd < best[0]:
                            best = (dd, (k, j))
    clearance = best[0]
    if clearance < MIN_STRAND_CLEARANCE:
        errs.append(f"strand clearance {clearance:.1f} < {MIN_STRAND_CLEARANCE} (prisms {best[1]})")

    # 2. curvature radius of the laid centre line (three points 36 u apart)
    min_r = 1e9
    for k in range(n):
        a, b, c = pos[k - 3], pos[k], pos[(k + 3) % n]
        ab, bc, ac = norm(sub(b, a)), norm(sub(c, b)), norm(sub(c, a))
        area = norm(cross(sub(b, a), sub(c, a))) / 2
        if area > 1e-6:
            min_r = min(min_r, ab * bc * ac / (4 * area))
    if min_r < MIN_CURVATURE_RADIUS:
        errs.append(f"tightest turn radius {min_r:.0f} < {MIN_CURVATURE_RADIUS}")

    # 3. the spline reproduces the analytic curve
    dense = d["pts"]
    dev = 0.0
    hint = 0
    for p in pos[::5]:
        bestd = 1e9
        for k in range(len(dense)):
            dd = norm(sub(dense[k], p))
            if dd < bestd:
                bestd = dd
        dev = max(dev, bestd)
    if dev > MAX_SPLINE_DEVIATION:
        errs.append(f"laid ribbon strays {dev:.2f} u from the curve (> {MAX_SPLINE_DEVIATION})")

    # 4. the ribbon roll between waypoints is small enough to interpolate
    ups, wps = d["ups"], d["waypoints"]
    step = max(math.degrees(math.acos(max(-1.0, min(1.0, dot(ups[k], ups[(k + 1) % len(ups)])))))
               for k in range(len(ups)))
    if step > MAX_UP_STEP_DEG:
        errs.append(f"ribbon rolls {step:.1f} deg between waypoints (> {MAX_UP_STEP_DEG})")
    ortho = max(abs(dot(b[1], b[2])) for b in laid)
    if ortho > 1e-3:
        errs.append(f"laid up not perpendicular to forward ({ortho:.4f})")

    # 5. the start: lobe apex just ahead of the spawn stack, heading +Z, ribbon horizontal
    w0 = wps[0]
    fwd0 = laid[0][1]
    up0 = laid[0][2]
    if not (abs(w0[0] - SPAWN[0]) < 60 and abs(w0[1]) < 5 and 0 < w0[2] - SPAWN[2] < 260):
        errs.append(f"start waypoint {fmt_vec(w0)} is not just ahead of the spawn stack {SPAWN}")
    if fwd0[2] < 0.97:
        errs.append(f"start heading {fmt_vec(fwd0)} is not +Z")
    if up0[1] < 0.97:
        errs.append(f"start ribbon normal {fmt_vec(up0)} is not +Y")

    # 6. lap, anchors, nucleus passes
    lap = d["lap"]
    if not LAP_RANGE[0] <= lap <= LAP_RANGE[1]:
        errs.append(f"lap {lap:.0f} outside {LAP_RANGE}")
    anchors = d["anchors"]
    far = max(min(norm(sub(a, p)) for p in pos) for a in anchors)
    if far > ANCHOR_LIFT + 8.0:
        errs.append(f"an anchor sits {far:.1f} u off the laid ribbon")
    cores = [t for t in d["anchor_tags"] if t[0] == "S"]
    if len(cores) != len(PASSES):
        errs.append(f"{len(cores)} anchors on core passes, want one per pass ({len(PASSES)})")
    a0 = anchors[0]
    ahead = dot(sub(a0, w0), fwd0)
    if not 0 < ahead < 600:
        errs.append(f"the first crystal {fmt_vec(a0)} is not just past the start line ({ahead:.0f} u)")

    marked = d["marked"]
    if sorted(marked) != sorted(set(marked)) or len(marked) != len(anchors):
        errs.append(f"{len(marked)} marked waypoints for {len(anchors)} crystal anchors - one marker per crystal")
    off = max(min(norm(sub(wps[m], a)) for m in marked) for a in anchors) if marked else 1e9
    if off > 1.0:
        errs.append(f"a crystal anchor is {off:.1f} u from every marked waypoint - markers must sit on the crystals")

    # 7. the core threads the nucleus once per pass, without piling up on the centre
    nucleus_hits = sum(1 for k in range(n) if norm(pos[k]) < NUCLEUS_R and norm(pos[k - 1]) >= NUCLEUS_R)
    if nucleus_hits != len(PASSES):
        errs.append(f"{nucleus_hits} passes inside r={NUCLEUS_R:.0f} per lap, want {len(PASSES)}")
    centre_miss = min(norm(p) for p in pos)
    if centre_miss < MIN_CENTRE_MISS:
        errs.append(f"a pass comes {centre_miss:.0f} u from the centre (< {MIN_CENTRE_MISS}) - the core piles up")

    # 8. no two lobes alike
    reach_spread = max(d["lobe_reach"]) - min(d["lobe_reach"])
    turn_spread = max(d["lobe_turn"]) - min(d["lobe_turn"])
    if reach_spread < MIN_LOBE_REACH_SPREAD:
        errs.append(f"lobe reaches span only {reach_spread:.0f} u (< {MIN_LOBE_REACH_SPREAD}) - the lobes repeat")
    if turn_spread < MIN_LOBE_TURN_SPREAD:
        errs.append(f"lobe turn radii span only {turn_spread:.0f} u (< {MIN_LOBE_TURN_SPREAD}) - the turns repeat")

    # 9. it does not only turn one way: yaw in the pilot's frame (ribbon up = floor), the dominant
    # sense, and the longest run turning against it at <= MAX_COUNTER_TURN_R
    yaw = []
    for k in range(n):
        h = 6
        kv = sub(add(pos[k - h], pos[(k + h) % n]), mul(pos[k], 2))
        yaw.append(dot(kv, cross(laid[k][2], laid[k][1])) / (12 * h) ** 2)
    dominant = 1.0 if sum(1 for y in yaw if y > 0) >= n / 2 else -1.0
    best_run = run = 0
    for k in range(2 * n):
        y = yaw[k % n] * -dominant
        run = run + 1 if y >= 1.0 / MAX_COUNTER_TURN_R else 0
        best_run = max(best_run, min(run, n))
    counter_run = best_run * 12.0
    if counter_run < MIN_COUNTER_TURN_RUN:
        errs.append(f"the longest counter-turn is {counter_run:.0f} u (< {MIN_COUNTER_TURN_RUN}) - the lap only turns one way")
    counter_r = 1.0 / max(max(y * -dominant for y in yaw), 1e-9)

    stats = {"prisms": n, "clearance": clearance, "min_turn_radius": min_r, "spline_dev": dev,
             "up_step": step, "lap": lap, "waypoints": len(wps), "max_r": max(norm(p) for p in pos),
             "core_passes": nucleus_hits, "centre_miss": centre_miss, "crystals": len(anchors),
             "lobe_reach": [round(x) for x in d["lobe_reach"]], "lobe_turn": [round(x) for x in d["lobe_turn"]],
             "counter_run": counter_run, "counter_r": counter_r}
    return errs, stats


# ---------------------------------------------------------------- YAML
def fnum(x):
    x = round(x, 2)
    if x == 0:
        return "0"
    s = f"{x:.2f}".rstrip("0").rstrip(".")
    return s


def fmt_vec(v):
    return f"{{x: {fnum(v[0])}, y: {fnum(v[1])}, z: {fnum(v[2])}}}"


def fmt_unit(v):
    v = unit(v)
    return "{x: %s, y: %s, z: %s}" % tuple(fnum4(c) for c in v)


def fnum4(x):
    x = round(x, 4)
    if x == 0:
        return "0"
    return f"{x:.4f}".rstrip("0").rstrip(".")


def int_list_hex(vals):
    return "".join(v.to_bytes(4, "little", signed=True).hex() for v in vals)


def hex_int_list(h):
    return [int.from_bytes(bytes.fromhex(h[i:i + 8]), "little", signed=True) for i in range(0, len(h), 8)]


def parse_sets(block):
    """'  - positions:\n    - {..}\n' runs -> list of raw text chunks per set (keeps formatting)."""
    sets = re.split(r"(?m)^  - positions:", block)[1:]
    return ["  - positions:" + s for s in sets]


def render_set(vecs, fmt):
    if not vecs:
        return "  - positions: []\n"
    return "  - positions:\n" + "".join(f"    - {fmt(v)}\n" for v in vecs)


def replace_set(text, key, next_key, index, new_set, count=4):
    m = re.search(rf"(?ms)^  {key}:\n(.*?)(?=^  {next_key}:)", text)
    if not m:
        raise SystemExit(f"no '{key}:' list followed by '{next_key}:'")
    sets = parse_sets(m.group(1))
    if len(sets) != count:
        raise SystemExit(f"'{key}' has {len(sets)} sets, expected {count}")
    sets[index] = new_set
    return text[:m.start(1)] + "".join(sets) + text[m.end(1):]


def patch_track(text, d):
    """Patch one SpawnableWaypointTrack component body (scene or bake)."""
    text = replace_set(text, "waypoints", "useSplinePerIntensity", INTENSITY - 1,
                       render_set(d["waypoints"], fmt_vec))

    m = re.search(r"(?m)^  useSplinePerIntensity: ([0-9a-f]*)\n", text)
    spl = hex_int_list(m.group(1))
    while len(spl) < 4:
        spl.append(0)
    spl[INTENSITY - 1] = 1
    tail = text[m.end():]
    # The new fields, in declaration order, right after useSplinePerIntensity. Replace them if present.
    tail = re.sub(r"(?ms)\A  waypointUps:\n.*?(?=^  crystalsPerLap:)", "", tail)
    tail = re.sub(r"(?m)\A  waypointUps: \[\]\n", "", tail)
    tail = re.sub(r"(?m)\A  crystalsPerLap: [0-9a-f]*\n", "", tail)
    ups_sets = [[] for _ in range(4)]
    ups_sets[INTENSITY - 1] = d["ups"]
    per_lap = [0, 0, 0, 0]
    per_lap[INTENSITY - 1] = len(d["anchors"])
    block = (f"  useSplinePerIntensity: {int_list_hex(spl)}\n"
             "  waypointUps:\n" + "".join(render_set(s, fmt_unit) for s in ups_sets) +
             f"  crystalsPerLap: {int_list_hex(per_lap)}\n")
    return text[:m.start()] + block + tail


def patch_markers(text, d):
    """markedWaypoints (declared after waypointDomain): only intensity 4 marks a subset."""
    sets = [[] for _ in range(4)]
    sets[INTENSITY - 1] = sorted(d["marked"])
    block = "  markedWaypoints:\n" + "".join(
        f"  - indices: {int_list_hex(x)}\n" if x else "  - indices: []\n" for x in sets)
    text = re.sub(r"(?m)^  markedWaypoints:\n(?:  - indices: [^\n]*\n)*", "", text)
    m = re.search(r"(?m)^  waypointDomain: .*\n", text)
    if not m:
        raise SystemExit("no waypointDomain field to anchor markedWaypoints after")
    return text[:m.end()] + block + text[m.end():]


def track_doc_span(text):
    """(start, end) of the SpawnableWaypointTrack MonoBehaviour document."""
    guid = re.search(r"guid: ([0-9a-f]{32})",
                     open(os.path.join(ROOT, "Assets/_Scripts/Controller/Environment/MiniGameObjects/"
                                             "SpawnableWaypointTrack.cs.meta")).read()).group(1)
    docs = [m.start() for m in re.finditer(r"(?m)^--- !u!", text)] + [len(text)]
    hits = [(a, b) for a, b in zip(docs, docs[1:]) if f"guid: {guid}, type: 3}}" in text[a:b]]
    if len(hits) != 1:
        raise SystemExit(f"expected one SpawnableWaypointTrack, found {len(hits)}")
    return hits[0]


def doc_span_with(text, key):
    docs = [m.start() for m in re.finditer(r"(?m)^--- !u!", text)] + [len(text)]
    hits = [(a, b) for a, b in zip(docs, docs[1:]) if key in text[a:b]]
    if len(hits) != 1:
        raise SystemExit(f"expected one document with {key!r}, found {len(hits)}")
    return hits[0]


def render_scene(text, d):
    a, b = track_doc_span(text)
    text = text[:a] + patch_markers(patch_track(text[a:b], d), d) + text[b:]
    a, b = doc_span_with(text, "listOfCrystalPositions:")
    body = replace_set(text[a:b], "listOfCrystalPositions", "anchorJitterRadius", INTENSITY - 1,
                       render_set(d["anchors"], fmt_vec))
    text = text[:a] + body + text[b:]
    a, b = doc_span_with(text, "lapsPerIntensity:")
    laps = hex_int_list(re.search(r"lapsPerIntensity: ([0-9a-f]+)", text[a:b]).group(1))
    if laps[INTENSITY - 1] != LAPS:
        raise SystemExit(f"lapsPerIntensity[{INTENSITY - 1}] is {laps[INTENSITY - 1]}, the design assumes {LAPS}")
    return text


def render_bake(text, d):
    a, b = track_doc_span(text)
    return text[:a] + patch_markers(patch_track(text[a:b], d), d) + text[b:]


def track_body(text):
    a, b = track_doc_span(text)
    body = text[a:b]
    return body[body.index("  waypoints:"):body.index("  previewIntensityLevel:")]


def main():
    check = "--check" in sys.argv
    report = "--report" in sys.argv
    d = build()
    errs, st = validate(d)
    print(f"Relativity (Skim Race I{INTENSITY}): lap {st['lap']:.0f} u, {st['waypoints']} waypoints, "
          f"{st['prisms']} prisms, {st['crystals']} crystals/lap x {LAPS} = {st['crystals'] * LAPS}")
    print(f"  strand clearance {st['clearance']:.1f} u, tightest turn r {st['min_turn_radius']:.0f} u, "
          f"spline dev {st['spline_dev']:.2f} u, max roll/waypoint {st['up_step']:.1f} deg, "
          f"reach {st['max_r']:.0f} u, nucleus passes/lap {st['core_passes']}, nearest the centre {st['centre_miss']:.0f} u")
    print(f"  lobe reach {st['lobe_reach']}  lobe turn radius {st['lobe_turn']}")
    print(f"  counter-turn (the snake): {st['counter_run']:.0f} u against the lap's dominant turn, tightest r {st['counter_r']:.0f} u")
    if errs:
        for e in errs:
            print("ERROR:", e)
        sys.exit(1)
    if report:
        return

    out = {}
    scene = open(SCENE, encoding="utf-8").read()
    out[SCENE] = (scene, render_scene(scene, d))
    bake = open(BAKE, encoding="utf-8").read()
    new_bake = render_bake(bake, d)
    out[BAKE] = (bake, new_bake)
    if track_body(out[SCENE][1]) != track_body(new_bake):
        sys.exit("ERROR: the baked track component no longer matches the scene's - re-bake it first")

    drift = [p for p, (old, new) in out.items() if old != new]
    rel = lambda p: os.path.relpath(p, ROOT)
    if check:
        if drift:
            for p in drift:
                print("DRIFT:", rel(p))
            sys.exit(1)
        print("check OK")
        return
    for p in drift:
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(out[p][1])
        print("wrote", rel(p))
    if not drift:
        print("no files written (already current)")


if __name__ == "__main__":
    main()
