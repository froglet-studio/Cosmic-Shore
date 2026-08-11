#!/usr/bin/env python3
"""Analytic budget for SpawnableWildlifeCage. Keep in sync with the C# generator.

The arena is a THREE-LAYER JAIL: three concentric cages at a fixed 1050 / 600 / 200, with a
very wide empty room between each pair, and one tier of wildlife locked in each room. Unlike
Ribcage, intensity never changes the SHELL COUNT (each shell walls in a tier of creature) -
it changes how tightly each cage is woven and, from intensity 3, its SHAPE: the cages become
BOXES (square rail grids with corner posts, "the boxing ring") - one at intensity 3, all three
at intensity 4. The WILDLIFE ROSTER is identical at every intensity, so this table carries the
entire difficulty curve.

Counts are exact loop arithmetic - the icosahedral subdivision, the shared-edge dedupe and the
per-segment prism walk are all simulated the same way the C# does, not estimated. Volume uses
E[k^3] ~ 1.04 for Jit(s, 0.2) (one uniform factor on all three axes).

Emits the per-intensity baselines that Tools/Build/author_wildlife_liberation_assets.py turns
into the four CellConfigDataSO PhaseThresholds blocks, and the fauna band radii that its
FaunaConfigurationSO assets are authored from - so the pens and the walls cannot drift.
"""
import math

# ── Arena (mirrors SpawnableWildlifeCage) ────────────────────────────────────
SHELL_RADII = [1050.0, 600.0, 200.0]     # outer / middle / core
SHELL_COUNT = 3
OUTER_R = SHELL_RADII[0]
BAND_WALL_CLEARANCE = 60.0

BAR_STEP = 34.0
BAR_LEN = 26.0
BAR_THICK = 4.2
POST = 7.5
DANGER_EVERY_CORE = 11

# Player spawn ring: outside the outer cage, inside the 1200u membrane.
SPAWN_RING_RADIUS = 1150

GEODESIC, BOXED = 0, 1

# [intensity-1][shell] -> (form, frequency). Mirrors SpawnableWildlifeCage.ShellPlans.
SHELL_PLANS = [
    [(GEODESIC,  5), (GEODESIC,  4), (GEODESIC,  3)],
    [(GEODESIC,  7), (GEODESIC,  5), (GEODESIC,  4)],
    [(BOXED,    14), (GEODESIC,  7), (GEODESIC,  5)],
    [(BOXED,    18), (BOXED,    18), (BOXED,    12)],
]
# The box frequencies are much higher than the geodesic ones on purpose: a cube face grid at
# frequency f contributes 12f^2 segments against a geodesic's 30f^2, and the box is smaller
# (corners on the radius => faces at 0.577*r). Matching frequencies would make the "harder"
# intensities lighter AND more open than the easy ones.

JIT = ((1.2) ** 4 - (0.8) ** 4) / (4 * 0.2)

# PhaseThresholds = measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md §18).
BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000,
                   rev=11200, rxv=8000, fev=57600, fxv=48000)

BAR_VOL = BAR_THICK * BAR_THICK * BAR_LEN
POST_VOL = POST ** 3


# ── Fauna bands (the pens) ───────────────────────────────────────────────────
def band_inner(shell):
    """Inner radius of the room belonging to `shell` - the shell below it, plus clearance."""
    return SHELL_RADII[shell + 1] + BAND_WALL_CLEARANCE if shell + 1 < SHELL_COUNT else 0.0


def band_outer(shell):
    """Outer radius of that room - just inside `shell`'s own wall."""
    return SHELL_RADII[shell] - BAND_WALL_CLEARANCE


# The OPEN WATER outside the outer cage, between it and the 1200u membrane. A fourth room, and
# the reason it exists: with wildlife only inside the cages, every fight converged on the middle
# of the arena. Stocking the open water means there is something to shoot from the moment you
# spawn (the player ring is at 1150, inside this band) and the action starts spread out.
OPEN_WATER_INNER = 1090.0
OPEN_WATER_OUTER = 1180.0
ROOM_OPEN_WATER = 3


def room_band(room):
    """(inner, outer) for any room index, including the open water."""
    if room == ROOM_OPEN_WATER:
        return OPEN_WATER_INNER, OPEN_WATER_OUTER
    return band_inner(room), band_outer(room)


ROOM_COUNT = SHELL_COUNT + 1


# ── Geodesic frame ───────────────────────────────────────────────────────────
def _icosa():
    t = (1 + math.sqrt(5)) / 2
    verts = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0),
             (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
             (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    verts = [_norm(v) for v in verts]
    faces = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11),
             (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
             (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
             (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    return verts, faces


def _norm(v):
    m = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2)
    return (v[0] / m, v[1] / m, v[2] / m)


def _key(p):
    """Same 0.5u quantized key the C# dedupe uses."""
    return (round(p[0] * 2), round(p[1] * 2), round(p[2] * 2))


def _dist(a, b):
    return math.dist(a, b)


def geodesic_frame(radius, f):
    """Unique bar segments + node points, mirroring BuildGeodesicFrame."""
    verts, faces = _icosa()
    segments, seen = [], set()
    nodes, node_seen = [], set()

    for (ia, ib, ic) in faces:
        a, b, c = verts[ia], verts[ib], verts[ic]
        grid = {}
        for i in range(f + 1):
            for j in range(f - i + 1):
                wa, wb, wc = (f - i - j) / f, i / f, j / f
                p = _norm((a[0] * wa + b[0] * wb + c[0] * wc,
                           a[1] * wa + b[1] * wb + c[1] * wc,
                           a[2] * wa + b[2] * wb + c[2] * wc))
                grid[(i, j)] = (p[0] * radius, p[1] * radius, p[2] * radius)

        for i in range(f + 1):
            for j in range(f - i + 1):
                p = grid[(i, j)]
                k = _key(p)
                if k not in node_seen:
                    node_seen.add(k)
                    nodes.append((p, 1.0))
                if j + 1 <= f - i:
                    _add(segments, seen, p, grid[(i, j + 1)])
                if i + 1 <= f - j:
                    _add(segments, seen, p, grid[(i + 1, j)])
                if i + 1 <= f and j + 1 <= f - i:
                    _add(segments, seen, grid[(i + 1, j)], grid[(i, j + 1)])

    return segments, nodes


def _add(segments, seen, a, b):
    ka, kb = _key(a), _key(b)
    if ka == kb:
        return
    pair = (ka, kb) if ka < kb else (kb, ka)
    if pair in seen:
        return
    seen.add(pair)
    segments.append((a, b))


# ── Boxed frame ──────────────────────────────────────────────────────────────
def _face_point(axis, sign, u, v, i, j, f, e):
    p = [0.0, 0.0, 0.0]
    p[axis] = sign * e
    p[u] = -e + 2 * e * i / f
    p[v] = -e + 2 * e * j / f
    return tuple(p)


def boxed_frame(radius, f):
    """Unique rail segments + corner posts, mirroring BuildBoxedFrame."""
    e = radius / math.sqrt(3)
    segments, seen = [], set()
    nodes, node_seen = [], set()

    for axis in range(3):
        u, v = (axis + 1) % 3, (axis + 2) % 3
        for sign in (-1, 1):
            for i in range(f + 1):
                for j in range(f + 1):
                    p = _face_point(axis, sign, u, v, i, j, f, e)
                    if i in (0, f) or j in (0, f):
                        k = _key(p)
                        if k not in node_seen:
                            node_seen.add(k)
                            corner = i in (0, f) and j in (0, f)
                            nodes.append((p, 1.9 if corner else 1.15))
                    if j < f:
                        _add(segments, seen, p, _face_point(axis, sign, u, v, i, j + 1, f, e))
                    if i < f:
                        _add(segments, seen, p, _face_point(axis, sign, u, v, i + 1, j, f, e))

    return segments, nodes


# ── Per-shell totals ─────────────────────────────────────────────────────────
def shell_rows(intensity, shell):
    form, f = SHELL_PLANS[max(1, min(intensity, 4)) - 1][shell]
    radius = SHELL_RADII[shell]
    is_core = shell == SHELL_COUNT - 1

    segments, nodes = (geodesic_frame(radius, f) if form == GEODESIC
                       else boxed_frame(radius, f))

    bars = sum(max(1, round(_dist(a, b) / BAR_STEP)) for a, b in segments)
    danger = 0
    if is_core:
        idx = 0
        for a, b in segments:
            for _ in range(max(1, round(_dist(a, b) / BAR_STEP))):
                if idx % DANGER_EVERY_CORE == 0:
                    danger += 1
                idx += 1

    node_vol = sum((POST * scale) ** 3 for _, scale in nodes)
    opening = (sum(_dist(a, b) for a, b in segments) / len(segments)) if segments else 0.0

    return {
        "form": "box" if form == BOXED else "geo",
        "freq": f,
        "radius": radius,
        "segments": len(segments),
        "bars": bars - danger,
        "danger": danger,
        "nodes": len(nodes),
        "node_vol": node_vol,
        "opening": opening,
        "count": bars + len(nodes),
        "volume": (bars * BAR_VOL + node_vol) * JIT,
    }


def cumulative(intensity):
    """Baseline for an INTENSITY (1..4). Returns (count, volume, danger)."""
    n = v = d = 0
    for shell in range(SHELL_COUNT):
        r = shell_rows(intensity, shell)
        n += r["count"]
        v += r["volume"]
        d += r["danger"]
    return n, v, d


def phase_thresholds(n, v):
    return dict(
        RestlessEnter=n + BLOB_DELTAS['re'], RestlessExit=n + BLOB_DELTAS['rx'],
        FrenzyEnter=n + BLOB_DELTAS['fe'], FrenzyExit=n + BLOB_DELTAS['fx'],
        RestlessEnterVolume=round(v + BLOB_DELTAS['rev']),
        RestlessExitVolume=round(v + BLOB_DELTAS['rxv']),
        FrenzyEnterVolume=round(v + BLOB_DELTAS['fev']),
        FrenzyExitVolume=round(v + BLOB_DELTAS['fxv']))


# ── The wildlife roster (the real objective, and the real collider budget) ───
#
# One entry per SPECIES PER ROOM - the spawner runs one loop per FaunaConfigurationSO, so each
# of these becomes its own asset, banded to its room. Populations are the seed FLOOR and the
# hard CAP: the spawner only tops a species back up to the floor, and everything above it comes
# from reproduction and is bounded by starvation (Docs/ECOSYSTEM.md §6). So `cap` is the honest
# number to size the collider budget against, not `seed`.
#
# `prisms` is body prisms per creature, measured from the prefabs - that IS the collider count
# each creature contributes, and every one of them is a MOVER (it re-buckets in the spatial
# index as the creature swims), which is why this roster and not the cage is the mode's headline
# performance risk.
#
# NOTE the Clawfish is deliberately absent: its prefab carries no HealthPrism at all, so it has
# no body to shoot and cannot be killed by a Sparrow. Adding it would put un-scoreable creatures
# in a hunt. See WILDLIFE_LIBERATION.md "Known limitations".
ROOM_OUTER, ROOM_MIDDLE, ROOM_CORE = 0, 1, 2

#          species        room             seed   cap  level  prisms
ROSTER = [
    # OPEN WATER - outside the outer cage, where the players spawn. Big creatures here on
    # purpose ("the big ones can spawn outside as well"): the hunt starts before you break in.
    ("Brittlestar",  ROOM_OPEN_WATER,       28,   65,   1,    10),
    ("QuadFish",     ROOM_OPEN_WATER,      130,  300,   1,     1),
    # OUTER ROOM - the heavy swarm. QuadFish is the small-fauna species now that the tadpole
    # is out of this mode, mixed with the big ones as before.
    ("QuadFish",     ROOM_OUTER,           320,  750,   1,     1),
    ("Brittlestar",  ROOM_OUTER,            38,   88,   1,    10),
    # MIDDLE ROOM - noticeably bigger creatures, and the first predators.
    ("Brittlestar",  ROOM_MIDDLE,           50,  115,   2,    10),
    ("Shark",        ROOM_MIDDLE,           24,   52,   2,    11),
    # CORE - the biggest and hardest, fewest in number.
    ("Shark",        ROOM_CORE,             14,   28,   5,    11),
    ("WormColony",   ROOM_CORE,              6,   11,   3,    26),
]

# ~610 creatures at seed rising to ~1409 at the caps, and DELIBERATELY THE SAME AT EVERY
# INTENSITY (requested 2026-08: "keep around 600 rising to 1400 at all intensities - the later
# levels can have more complexity"). The seed/cap gap is wide on purpose: the spawner only tops
# each species back up to its floor, so everything between the two is REPRODUCTION - the swarm
# visibly thickens as a match runs and thins where hunters have been working, which is the food
# web doing the shaping rather than a spawner curve.
#
# NO TADPOLES (removed on request, 2026-08). QuadFish inherits the swarm role. This costs
# collider budget - a tadpole is 1 body prism and so is a QuadFish, but the tadpoles were the
# cheapest way to reach a big headcount and their share had to be spread across species that
# are not all 1-prism. See the collider table in WILDLIFE_LIBERATION.md.
#
# The intensity ramp lives entirely in SHELL_PLANS - tighter weaves and boxier cages - not in
# the population. Left as a per-intensity array rather than a scalar so re-introducing a
# population ramp is one edit; do not confuse "all 1.0" with "unused".
POPULATION_SCALE = [1.0, 1.0, 1.0, 1.0]


def roster_for(intensity):
    """The roster at an intensity: (species, room, seed, cap, level, prisms_per_creature)."""
    k = POPULATION_SCALE[max(1, min(intensity, 4)) - 1]
    out = []
    for species, room, seed, cap, level, prisms in ROSTER:
        out.append((species, room, max(1, round(seed * k)), max(1, round(cap * k)), level, prisms))
    return out


def fauna_totals(intensity):
    """(creatures at seed, creatures at cap, body prisms at cap) for an intensity."""
    r = roster_for(intensity)
    return (sum(s for _, _, s, _, _, _ in r),
            sum(c for _, _, _, c, _, _ in r),
            sum(c * p for _, _, _, c, _, p in r))


if __name__ == "__main__":
    print("== the three-layer jail " + "=" * 56)
    for i in range(1, 5):
        n, v, d = cumulative(i)
        th = phase_thresholds(n, v)
        print(f"\nintensity {i}:  {n:>6} prisms   {v:>11.0f} volume   ({d} danger bars in the core)")
        print(f"{'shell':<8}{'form':>6}{'freq':>6}{'radius':>8}{'segs':>7}"
              f"{'bars':>7}{'danger':>8}{'nodes':>7}{'opening':>9}{'prisms':>8}")
        for shell in range(SHELL_COUNT):
            r = shell_rows(i, shell)
            name = ("outer", "middle", "core")[shell]
            print(f"{name:<8}{r['form']:>6}{r['freq']:>6}{r['radius']:>8.0f}{r['segments']:>7}"
                  f"{r['bars']:>7}{r['danger']:>8}{r['nodes']:>7}{r['opening']:>8.0f}u{r['count']:>8}")
        print(f"  PhaseThresholds  count  {th['RestlessEnter']}/{th['RestlessExit']}"
              f"  {th['FrenzyEnter']}/{th['FrenzyExit']}")
        print(f"                   volume {th['RestlessEnterVolume']}/{th['RestlessExitVolume']}"
              f"  {th['FrenzyEnterVolume']}/{th['FrenzyExitVolume']}")

    print("\n== fauna bands (the rooms) " + "=" * 53)
    for room in range(ROOM_COUNT):
        name = ("outer", "middle", "core", "open water")[room]
        lo, hi = room_band(room)
        wall = f"(wall at {SHELL_RADII[room]:.0f})" if room < SHELL_COUNT else "(outside the outer cage)"
        print(f"  {name:<11} {lo:>6.0f} .. {hi:>6.0f}   {wall}")
    print(f"\nplayer spawn ring {SPAWN_RING_RADIUS}  (outer cage {OUTER_R:.0f}, membrane 1200)")

    print("\n== the wildlife (per intensity) " + "=" * 48)
    room_names = ("outer", "middle", "core", "open water")
    for i in range(1, 5):
        seed, cap, prisms = fauna_totals(i)
        cage, _, _ = cumulative(i)
        print(f"\nintensity {i}: {seed} creatures at seed, {cap} at cap, "
              f"{prisms} body prisms at cap")
        print(f"{'species':<14}{'room':>11}{'seed':>7}{'cap':>6}{'lvl':>5}{'prisms/ea':>11}{'prisms':>9}")
        for species, room, s, c, lvl, p in roster_for(i):
            print(f"{species:<14}{room_names[room]:>11}{s:>7}{c:>6}{lvl:>5}{p:>11}{c * p:>9}")
        print(f"  COLLIDER BUDGET  cage {cage} + fauna {prisms} = {cage + prisms} "
              f"(fauna are MOVERS - see WILDLIFE_LIBERATION.md)")
