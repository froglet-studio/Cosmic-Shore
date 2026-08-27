#!/usr/bin/env python3
"""Analytic budget for SpawnableWildlifeCage. Keep in sync with the C# generator.

The arena is a THREE-LAYER JAIL: three concentric cages at a fixed 1050 / 600 / 200, with a
very wide empty room between each pair. The wildlife is NOT locked one tier per room - every
species roams the whole arena on ONE shared band (see ROAM_INNER/ROAM_OUTER). Unlike
Ribcage, intensity never changes the SHELL COUNT (each shell is a room a hunter breaks into) -
it changes how tightly each cage is woven and, from intensity 3, its SHAPE: the cages become
BOXES (square rail grids with corner posts, "the boxing ring") - one at intensity 3, all three
at intensity 4. The WILDLIFE ROSTER is identical at every intensity, so this table carries the
entire difficulty curve.

Counts are exact loop arithmetic - the icosahedral subdivision, the shared-edge dedupe and the
per-segment prism walk are all simulated the same way the C# does, not estimated. Volume uses
E[k^3] ~ 1.04 for Jit(s, 0.2) (one uniform factor on all three axes).

Emits the per-intensity baselines that Tools/Build/author_wildlife_liberation_assets.py turns
into the four CellConfigDataSO PhaseThresholds blocks, and the one roam band its
FaunaConfigurationSO assets are authored from - so the band and the membrane cannot drift.
"""
import math

# ── Arena (mirrors SpawnableWildlifeCage) ────────────────────────────────────
SHELL_RADII = [1050.0, 600.0, 200.0]     # outer / middle / core
SHELL_COUNT = 3
OUTER_R = SHELL_RADII[0]
ROOM_WALL_CLEARANCE = 60.0

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


# ── Room geometry + the fauna roam band ─────────────────────────────────────
def room_inner(shell):
    """Inner radius of the room belonging to `shell` - the shell below it, plus clearance."""
    return SHELL_RADII[shell + 1] + ROOM_WALL_CLEARANCE if shell + 1 < SHELL_COUNT else 0.0


def room_outer(shell):
    """Outer radius of that room - just inside `shell`'s own wall."""
    return SHELL_RADII[shell] - ROOM_WALL_CLEARANCE


# The OPEN WATER outside the outer cage, between it and the 1200u membrane. The player ring sits
# at 1150, inside it, which is why the outermost room is stocked at all: there is something to
# shoot from the moment you spawn and breaking into a cage is a choice rather than the only way
# to score. OPEN_WATER_OUTER is what ROAM_OUTER is measured from.
OPEN_WATER_INNER = 1090.0
OPEN_WATER_OUTER = 1180.0
ROOM_OPEN_WATER = 3


def room_band(room):
    """(inner, outer) for any room index, including the open water. ARCHITECTURE, not a pen -
    the AI hunters' patrol steps through these to sweep the arena radially."""
    if room == ROOM_OPEN_WATER:
        return OPEN_WATER_INNER, OPEN_WATER_OUTER
    return room_inner(room), room_outer(room)


# THE fauna band, and there is exactly one: every species roams the WHOLE arena, core to
# membrane. Mirrors SpawnableWildlifeCage.RoamInner/RoamOuter.
#
# It replaces a three-tier pen (each species banded to one room), which read as three stacked
# aquariums around a boss room - the fight converged wherever a player broke in and the apex
# creatures were only ever findable at one radius. Mixing every tier through one volume is what
# makes the mode a hunt: what you meet next is a roll, not a radius.
#
# THE COST, stated because the pens were what paid for it: a room-banded creature could not
# reach its own cage, so the bars sat outside the food web. One arena-wide band puts all three
# cages inside it and this cell has no nucleus, so herbivores eat opposing-domain mass and the
# triad-painted bars are now grazeable - the cage erodes as a match runs. Shielding the bars is
# NOT the answer (a shield reaches 1.5x leafSize, which at a 34u bar step fuses the lattice and
# costs the one-hit break-in); raise ROAM_INNER off 0 or cut POPULATION_SCALE instead.
ROAM_INNER = 0.0
ROAM_OUTER = OPEN_WATER_OUTER


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
# One entry per SPECIES - the spawner runs one loop per FaunaConfigurationSO, so each of these
# becomes its own asset. Every one of them carries the SAME band (ROAM_INNER..ROAM_OUTER).
#
# It used to be one entry per SPECIES x LEVEL. Lifeform LEVELS are retired (a lifeform is its
# species and its ELEMENT, nothing else - Docs/ECOSYSTEM.md 40), which removed the last thing
# separating two entries of one species: the room dimension had already gone, so a "level-5
# shark" row and a "level-2 shark" row became two configs that differed in nothing at all.
# They are merged by SUMMING their populations, which is arithmetic and not a retune - the
# authored roster is still 610 creatures at seed and 1409 at cap, and after POPULATION_SCALE
# still 519 / 1198 / 4155 body prisms, each identical to the six-row table row for row in
# total. `prisms` was already equal across a species' rows, so nothing is lost in the merge.
#
# What the mode gives up with it: the deliberate "a level-5 shark among level-2 ones" size mix
# inside one species. Variety within a species is now the ELEMENT (SpreadElements over the
# four-element palette), not a starting tier.
#
# Populations are the seed FLOOR and the hard CAP: the spawner only tops a species back up to
# the floor, and everything above it comes from reproduction and is bounded by starvation
# (Docs/ECOSYSTEM.md §6). So `cap` is the honest number to size the collider budget against,
# not `seed`.
#
# `prisms` is body prisms per creature, measured from the prefabs - that IS the collider count
# each creature contributes, and every one of them is a MOVER (it re-buckets in the spatial
# index as the creature swims), which is why this roster and not the cage is the mode's headline
# performance risk.
#
# NOTE the Clawfish is deliberately absent: its prefab carries no HealthPrism at all, so it has
# no body to shoot and cannot be killed by a Sparrow. Adding it would put un-scoreable creatures
# in a hunt. See WILDLIFE_LIBERATION.md "Known limitations".

#          species        seed   cap  prisms
ROSTER = [
    # The swarm - what you meet constantly, anywhere in the arena.
    ("QuadFish",          450, 1050,    1),
    # 66+50: the two level-1/level-2 rows of the six-row table, summed.
    ("Brittlestar",       116,  268,   10),
    # 24+14: the mid row plus the apex row. These used to be locked in the 200u core - the
    # "big ones concentrated in the centre" the roam-band pass exists to end. Whole arena now.
    ("Shark",              38,   80,   11),
    ("WormColony",          6,   11,   26),
]

# Before POPULATION_SCALE the roster is 610 creatures at seed rising to 1409 at cap - the
# already-play-tested population, preserved exactly through BOTH merges: the room merge (the
# two QuadFish rows and the two level-1 Brittlestar rows were per-room splits of one
# population) and the level merge above. Both are arithmetic, not a retune.
#
# POPULATION_SCALE then takes 15% off the whole roster (requested 2026-08): 519 at seed, 1198 at
# cap, 4155 body prisms at cap against 4896. It is deliberately the dial rather than 12 edited
# numbers, so the cut is one line to revisit and the authored roster above still reads as the
# play-tested one. Do not confuse a uniform scale with "unused".
#
# The seed/cap gap is wide on purpose: the spawner only tops each species back up to its floor,
# so everything between the two is REPRODUCTION - the swarm visibly thickens as a match runs and
# thins where hunters have been working, which is the food web doing the shaping rather than a
# spawner curve.
#
# The intensity ramp lives entirely in SHELL_PLANS - tighter weaves and boxier cages - not in
# the population, which is why every row here is the same scalar.
POPULATION_SCALE = [0.85, 0.85, 0.85, 0.85]


def _round_half_up(x):
    """Explicit half-up rounding. Python's round() is BANKER'S rounding, which lands 10*0.85 on
    8 for one species and 9 for the next and nobody can explain why (the ecology skill's own
    warning). An authoring-facing scalar must round predictably."""
    return math.floor(x + 0.5)


def roster_for(intensity):
    """The roster at an intensity: (species, seed, cap, prisms_per_creature)."""
    k = POPULATION_SCALE[max(1, min(intensity, 4)) - 1]
    return [(species, max(1, _round_half_up(seed * k)), max(1, _round_half_up(cap * k)),
             prisms)
            for species, seed, cap, prisms in ROSTER]


def fauna_totals(intensity):
    """(creatures at seed, creatures at cap, body prisms at cap) for an intensity."""
    r = roster_for(intensity)
    return (sum(s for _, s, _, _ in r),
            sum(c for _, _, c, _ in r),
            sum(c * p for _, _, c, p in r))


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

    print("\n== rooms (cage architecture; the AI patrol sweeps these) " + "=" * 20)
    for room in range(ROOM_COUNT):
        name = ("outer", "middle", "core", "open water")[room]
        lo, hi = room_band(room)
        wall = f"(wall at {SHELL_RADII[room]:.0f})" if room < SHELL_COUNT else "(outside the outer cage)"
        print(f"  {name:<11} {lo:>6.0f} .. {hi:>6.0f}   {wall}")
    print(f"\n  fauna ROAM BAND {ROAM_INNER:.0f} .. {ROAM_OUTER:.0f}  "
          f"- ONE band, every species, the whole arena")
    print(f"  player spawn ring {SPAWN_RING_RADIUS}  (outer cage {OUTER_R:.0f}, membrane 1200)")

    print("\n== the wildlife (per intensity) " + "=" * 48)
    for i in range(1, 5):
        seed, cap, prisms = fauna_totals(i)
        cage, _, _ = cumulative(i)
        print(f"\nintensity {i}: {seed} creatures at seed, {cap} at cap, "
              f"{prisms} body prisms at cap  (scale {POPULATION_SCALE[i - 1]})")
        print(f"{'species':<14}{'seed':>7}{'cap':>6}{'prisms/ea':>11}{'prisms':>9}"
              f"{'band':>16}")
        for species, s_, c, p in roster_for(i):
            band = f"{ROAM_INNER:.0f}..{ROAM_OUTER:.0f}"
            print(f"{species:<14}{s_:>7}{c:>6}{p:>11}{c * p:>9}{band:>16}")
        print(f"  COLLIDER BUDGET  cage {cage} + fauna {prisms} = {cage + prisms} "
              f"(fauna are MOVERS - see WILDLIFE_LIBERATION.md)")
