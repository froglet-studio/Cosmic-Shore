#!/usr/bin/env python3
"""Author Skim Race intensity 4 - "Relativity" - into MinigameSkimRace.unity.

The track is a closed space curve with S6 symmetry (a 60-degree rotoreflection about the
(1,1,1) diagonal, the symmetry of a cyclohexane chair), built from two pieces:

  * SIX CORE PASSES - the six struts of a tensegrity icosahedron (two parallel struts per
    axis, X struts offset along Z, Y along X, Z along Y). Struts never meet: perpendicular
    struts pass CORE_GAP apart, parallel ones 2 x CORE_GAP. The whole weave sits inside the
    nucleus cage, so every lap threads the cell's centre six times and every pass is a
    near-miss with three others - whoever is on them.
  * SIX LOBES - one rose petal per pair of consecutive passes, leaving along one axis and
    returning along a perpendicular one (a 270-degree turn). The petals point at six of the
    twelve cuboctahedron directions: a three-petal flower toward (1,1,1) and its inverse
    toward (-1,-1,-1), visited alternately, so each pass through the nucleus flips you from
    one flower to the other and every lobe is in view of the core.

The ribbon lies flat in each lobe's plane. Consecutive lobe planes are perpendicular, so each
core pass rolls the ribbon a quarter turn (alternating left/right - the symmetry is improper,
so the handedness must alternate): what was the floor on one lobe is a wall on the next. That
is Escher's Relativity, three orthogonal gravities, and it is authored as per-waypoint ribbon
normals (SpawnableWaypointTrack.waypointUps) - world up is undefined on a vertical pass.

Crystals: one anchor every 1/24 of a lap, phased so one sits on every core pass and every
lobe apex (core, out-arm, apex, in-arm per lobe). Target = 24 x 2 laps = 48, set through the
track's crystalsPerLap (the waypoint list is a dense spline sample, not one point per crystal).

The race starts at a lobe apex: the whole knot is rotated so lobe 0's apex sits just ahead of
the shared spawn stack at (700, y, -200), heading +Z with the ribbon horizontal, so the grid
lines up above and below the road and the first turn dives into the nucleus.

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
CORE_GAP = 64.0        # perpendicular-strut miss distance at the core (centre line to centre line)
STRUT_HALF = 300.0     # each core pass runs +-STRUT_HALF along its axis
LOBE_R = 655.0         # petal apex radius (before the strut-offset blend)
LOBE_SHAPE = 0.5       # r(th) = L + (R - L) * sin(2 th)^SHAPE; 0.5 = finite curvature at the joint
WAYPOINT_SPACING = 100.0
ANCHORS_PER_LAP = 24
ANCHOR_LIFT = 0.0      # crystal anchors sit this far above the ribbon, along its normal (the local floor)
LAPS = 2

# The strut sequence: (axis, offset sign, travel sign). Consecutive axes differ (every lobe is a
# 270-degree petal), the six lobe quadrants are distinct, and the sequence is invariant under the
# 3-fold rotation (x,y,z)->(z,x,y) shifted by two and under inversion shifted by three: S6.
SEQUENCE = [("X", 1, 1), ("Z", -1, -1), ("Y", 1, 1), ("X", -1, -1), ("Z", 1, 1), ("Y", -1, -1)]

# ---------------------------------------------------------------- what the track must satisfy
SPAWN = (700.0, 0.0, -200.0)  # the scene's PlayerOrigin stack (y = -20..20), facing +Z
MIN_STRAND_CLEARANCE = 60.0   # prism centre to prism centre, strands >= 480 u apart along the track
MIN_CURVATURE_RADIUS = 200.0  # Squirrel: 300 u/s at 120 deg/s turns on a 143 u circle, before lag
MAX_SPLINE_DEVIATION = 3.0    # laid ribbon vs the analytic curve
MAX_UP_STEP_DEG = 25.0        # ribbon roll between consecutive waypoints (the runtime interpolates)
LAP_RANGE = (10000.0, 12500.0)

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


def strut(i):
    axis, off, travel = SEQUENCE[i % len(SEQUENCE)]
    return mul(AXES[OFFSET_AXIS[axis]], off * CORE_GAP), mul(AXES[axis], float(travel))


def lobe_normal(i):
    """Plane normal of lobe i (between pass i and pass i+1): e1 x e2, e1 = out arm, e2 = in arm."""
    _, d0 = strut(i)
    _, d1 = strut(i + 1)
    return unit(cross(d0, mul(d1, -1.0)))


# ---------------------------------------------------------------- the analytic curve
def dense_curve(step=2.0):
    """[(point, normal, tag)] around one lap starting at pass 0's entry. tag = ('S'|'L', i, param)."""
    out = []
    n = len(SEQUENCE)
    for i in range(n):
        o0, d0 = strut(i)
        o1, d1 = strut(i + 1)
        # core pass i: roll from lobe i-1's floor to lobe i's floor about the travel axis
        n_in, n_out = lobe_normal(i - 1), lobe_normal(i)
        roll = math.atan2(dot(cross(n_in, n_out), d0), dot(n_in, n_out))
        m = int(round(2 * STRUT_HALF / step))
        for k in range(m):
            u = k / m
            p = add(o0, mul(d0, -STRUT_HALF + 2 * STRUT_HALF * u))
            out.append((p, rotate_about(n_in, d0, roll * smoothstep(u)), ("S", i, u)))
        # lobe i: rose petal from pass i's exit (along d0) back into pass i+1 (along d1)
        e1, e2 = d0, mul(d1, -1.0)
        nl = lobe_normal(i)
        m = 1600
        for k in range(m):
            # th eases in and out (th ~ v^2 at both ends): r - L grows like sqrt(th) at the joints,
            # so uniform th would leave the dense reference coarse exactly where the lobe meets the pass.
            th = (math.pi / 4) * (1 - math.cos(math.pi * k / m))
            r = STRUT_HALF + (LOBE_R - STRUT_HALF) * max(math.sin(2 * th), 0.0) ** LOBE_SHAPE
            w = smoothstep(th / (math.pi / 2))
            p = add(mul(add(mul(e1, math.cos(th)), mul(e2, math.sin(th))), r), lerp(o0, o1, w))
            out.append((p, nl, ("L", i, th)))
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

    # Lobe 0's apex (th = pi/4) is the start line.
    apex_k = next(k for k, c in enumerate(curve) if c[2][0] == "L" and c[2][1] == 0 and c[2][2] >= math.pi / 4)
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

    # Waypoints: uniform arc length from the apex.
    count = int(round(lap / WAYPOINT_SPACING))
    waypoints, ups = [], []
    for j in range(count):
        k, p = sample_at(pts, s_tab, s0 + lap * j / count)
        waypoints.append(p)
        ups.append(project_normal(nrms[k], tans[k]))

    # Anchors: every lap/24 phased on the core-pass midpoints, listed from the first one ahead of the
    # start line - which lands on the start apex itself (the asymmetric offset blend puts the apex
    # anchor ~12 u past th = pi/4), so the race opens with a launch crystal and a lap's last crystal
    # is lobe 0's out-arm, just after the sixth nucleus pass.
    strut_mid0 = s_tab[next(k for k, c in enumerate(curve) if c[2][0] == "S" and c[2][1] == 0 and c[2][2] >= 0.5)]
    spacing = lap / ANCHORS_PER_LAP
    phase = (strut_mid0 - s0) % spacing
    anchor_s = sorted(((phase + spacing * j) % lap) for j in range(ANCHORS_PER_LAP))
    anchor_s = [x for x in anchor_s if x > 1e-6] + [x for x in anchor_s if x <= 1e-6]
    anchors = []
    for x in anchor_s:
        k, p = sample_at(pts, s_tab, s0 + x)
        anchors.append(add(p, mul(project_normal(nrms[k], tans[k]), ANCHOR_LIFT)))
    anchor_tags = [curve[sample_at(pts, s_tab, s0 + x)[0]][2] for x in anchor_s]

    return {"pts": pts, "s_tab": s_tab, "lap": lap, "waypoints": waypoints, "ups": ups,
            "anchors": anchors, "anchor_tags": anchor_tags, "curve": curve,
            "symmetry_axis": rot(unit((1.0, 1.0, 1.0)))}


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
    if len(anchors) != ANCHORS_PER_LAP:
        errs.append(f"{len(anchors)} anchors, want {ANCHORS_PER_LAP}")
    far = max(min(norm(sub(a, p)) for p in pos) for a in anchors)
    if far > ANCHOR_LIFT + 8.0:
        errs.append(f"an anchor sits {far:.1f} u off the laid ribbon")
    cores = [t for t in d["anchor_tags"] if t[0] == "S"]
    if len(cores) != len(SEQUENCE):
        errs.append(f"{len(cores)} anchors on core passes, want one per pass ({len(SEQUENCE)})")
    a0 = anchors[0]
    if d["anchor_tags"][0][1] != 0 or norm(sub(a0, w0)) > 40 or a0[2] < w0[2]:
        errs.append(f"the first crystal {fmt_vec(a0)} is not on the start apex, just past the start line")
    nucleus_hits = sum(1 for k in range(n) if norm(pos[k]) < 120 and norm(pos[k - 1]) >= 120)
    if nucleus_hits != len(SEQUENCE):
        errs.append(f"{nucleus_hits} passes inside r=120 per lap, want {len(SEQUENCE)}")

    stats = {"prisms": n, "clearance": clearance, "min_turn_radius": min_r, "spline_dev": dev,
             "up_step": step, "lap": lap, "waypoints": len(wps), "max_r": max(norm(p) for p in pos),
             "core_passes": nucleus_hits}
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
    per_lap[INTENSITY - 1] = ANCHORS_PER_LAP
    block = (f"  useSplinePerIntensity: {int_list_hex(spl)}\n"
             "  waypointUps:\n" + "".join(render_set(s, fmt_unit) for s in ups_sets) +
             f"  crystalsPerLap: {int_list_hex(per_lap)}\n")
    return text[:m.start()] + block + tail


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
    text = text[:a] + patch_track(text[a:b], d) + text[b:]
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
    return text[:a] + patch_track(text[a:b], d) + text[b:]


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
          f"{st['prisms']} prisms, {ANCHORS_PER_LAP} crystals/lap x {LAPS} = {ANCHORS_PER_LAP * LAPS}")
    print(f"  strand clearance {st['clearance']:.1f} u, tightest turn r {st['min_turn_radius']:.0f} u, "
          f"spline dev {st['spline_dev']:.2f} u, max roll/waypoint {st['up_step']:.1f} deg, "
          f"reach {st['max_r']:.0f} u, nucleus passes/lap {st['core_passes']}")
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
