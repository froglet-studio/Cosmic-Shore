#!/usr/bin/env python3
"""
Author the GARLAND cell - the freestyle world cut for the HOME SCREEN camera.

Why a script and not a folder of hand-typed assets: this cell's PhaseThresholds have to ride
what its generator actually emits and what its roster actually grows, and a hand-typed ladder
drifts the first time either moves. This file holds

  1. an exact offline mirror of Assets/_Scripts/.../SpawnableGarland.cs (count, volume,
     per-domain volume, radial extremes),
  2. the invariants that mirror has to prove, with a NEGATIVE CONTROL so a check nobody has
     watched fail is not mistaken for a check that works,
  3. the lifeform roster model, and
  4. the ladder derived from 1 + 3,

and emits every asset. See Docs/ECOSYSTEM.md §18/§48 and the /ecology skill §4.5 / §7.

THE MIRROR IS EXACT, NOT ESTIMATED
----------------------------------
SpawnableGarland draws NOTHING from the base class's shared System.Random (`RangeF`/`Jit`) and
nothing from value noise - every wobble is `Hash01` of the emitting index, which is an integer
hash this file reproduces bit for bit. That is why the numbers below are measurements rather
than estimates, and it is the whole reason the generator was written that way.

The one estimated quantity is the GROWN flora's volume: PhyllotacticFlora sizes its prisms by
ROLE (a stem spans its segment, a leaf spans its reach), so there is no authored field to read.
It lives in CALIBRATION below with its derivation, and it is deliberately the least load-bearing
number here - the flora is ~7% of this cell's volume, so even a 2x error moves the volume ladder
by less than a seventh of its trail band. The flora COUNT, which is what the Frenzy COUNT
backstop reads, is exact (per-element budgets are authored ints).

Usage:
    python3 Tools/Build/author_garland_cell.py            # write + print the table
    python3 Tools/Build/author_garland_cell.py --check    # verify, exit 1 on drift
    python3 Tools/Build/author_garland_cell.py --report   # print the table, write nothing
"""

import collections
import hashlib
import json
import math
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SO = os.path.join(REPO, "Assets", "_SO_Assets")
CELL_DIR = os.path.join(SO, "Cell Configs", "Garland Cell")
PREFAB = os.path.join(REPO, "Assets", "_Prefabs", "Spawnables", "SpawnableGarland.prefab")
SOURCE = os.path.join(REPO, "Assets", "_Scripts", "Controller", "Environment",
                      "MiniGameObjects", "SpawnableGarland.cs")
HARNESS = os.path.join(REPO, "Tools", "Build", "garland_harness")
MEASURED = os.path.join(REPO, "Tools", "Build", "garland_measurements.json")

# ─────────────────────────────────────────────────────────────────────────────
# 1. The mirror - transliterated from SpawnableGarland.cs, family by family
# ─────────────────────────────────────────────────────────────────────────────

GOLDEN = 2.39996323

NUCLEUS_R, MEMBRANE_R, CAM_R = 392.0, 1200.0, 686.0
NUCLEUS_MARGIN = 16.0

# ── Chains ──
# Every long family is one prism per step. CHAIN_FILL is the fraction of the step that prism
# fills; it is what makes the difference between a readable line and a welded tube, and it is
# under 1 by enough to absorb CHAIN_JIT AND the corner a bend puts on the inside of a joint.
CHAIN_FILL = 0.82
CHAIN_JIT = 0.08

# The yield grid. Cell size must exceed twice the largest prism's bounding radius or the 27-cell
# neighbourhood stops covering every pair that could touch (asserted offline). YIELD_GAP is the
# clearance a yielding prism has to leave, not zero: a fit that clears by a hair re-reads as
# clipping the moment anything moves.
YIELD_CELL, YIELD_GAP = 56.0, 0.75

# ── The seed's crust ──
SHORE_R = 436.0
SHORE_BAND_STEP = 14.0
SHORE_BANDS = 2
SHORE_BAND_LIFT = 15.0
SHORE_BAND_SECTION = (22.0, 3.0)
SHORE_PATCH_PLATES = 14
SHORE_PATCH_R = 42.0
SHORE_PATCH_PLATE = (10.0, 3.0, 10.0)

# ── The two knots ──
BOUGH_P, BOUGH_Q, BOUGH_MAJOR, BOUGH_MINOR, BOUGH_STEP = 2, 3, 700.0, 170.0, 22.0
BOUGH_SECTION = (16.0, 7.0)
VINE_P, VINE_Q, VINE_MAJOR, VINE_MINOR, VINE_STEP = 3, 2, 565.0, 85.0, 26.0
VINE_SECTION = (8.0, 4.0)

# ── Blossoms: CONCENTRIC RINGS, not a golden-angle head ──
# A sunflower head packs its florets at a constant areal density, which is exactly what a head of
# non-overlapping petals cannot be: at this cell's petal size the head's own area runs out before
# the count does. Rings state the two clearances separately - the ring pitch against the petal's
# LENGTH, the ring count against its WIDTH - so each is a bound that can be checked.
BLOSSOMS, VINE_BLOSSOMS = 5, 3
BLOSSOM_RINGS = (11, 21, 30, 41, 48)
BLOSSOM_RING_RADII = (30.0, 47.0, 64.0, 81.0, 98.0)
BLOSSOM_PETAL = (8.0, 2.6, 13.0)
BLOSSOM_BOSSES, BLOSSOM_BOSS_RADIUS, BLOSSOM_BOSS_SIZE = 9, 16.0, 6.0
VINE_BLOSSOM_RINGS = (11, 17, 23, 27)
VINE_BLOSSOM_RING_RADII = (18.0, 30.0, 42.0, 54.0)
VINE_PETAL = (6.0, 2.2, 9.0)
VINE_BLOSSOM_BOSSES, VINE_BOSS_RADIUS, VINE_BOSS_SIZE = 7, 9.0, 5.0

# ── Skirts: ROWS along the bough, not one wide fan ──
SKIRTS, SKIRT_ROWS, SKIRT_LEAVES = 17, 6, 3
SKIRT_LEAF = (6.0, 2.0, 15.0)
SKIRT_STANDOFF, SKIRT_ROW_PITCH, SKIRT_FAN = 11.0, 5.0, 0.78

# ── Falls, crown, terraces ──
FALLS, FALL_STEPS, FALL_SKIP = 8, 82, 2
FALL_SECTION = (4.5, 4.5)
FALL_STANDOFF = 10.0
# A chain that starts at its parent's own lay point starts INSIDE it, and no length makes that
# pair clear - so both start displaced. They are displaced DIFFERENTLY, and that asymmetry is a
# measurement rather than a preference. A crown climbs OUT of the bough's band and never returns,
# so lifting it radially puts the wood behind it for the whole run (a sideways lift does not: the
# drift term swings the chain back across the bough within two steps - 7.3 units apart at step 2,
# from a start 31 units clear). A fall does the opposite: it spends three quarters of its length
# inside the band the bough wanders through, so dropping it radially lays it directly under a
# curve that comes back down to meet it (44 pairs, against 7 for the same fall pushed out the
# bough's SIDE, where it leaves the knot's own osculating plane at once).
FALL_ROOT_OFFSET, CROWN_ROOT_OFFSET = 18.0, 22.0
FALL_EASE = 1.4
CROWNS, CROWN_STEPS, CROWN_SKIP = 8, 41, 1
CROWN_SECTION = (7.0, 7.0)
CROWN_TUFT_LEAVES = 28
CROWN_TUFT_LEAF = (5.0, 2.2, 12.0)
CROWN_TUFT_RADIUS, CROWN_TUFT_CONE = 24.0, 1.309
TERRACES = 3
# Terraces ride the MIDPOINT of a blossom gap rather than their own stride: 3 and 5 beat
# against each other on a closed loop, and a terrace 14 samples from a blossom is a deck inside
# a flower.
TERRACE_GAPS = (0, 2, 4)
TERRACE_RINGS = (10, 20, 30, 40)
TERRACE_RING_RADII = (22.0, 39.0, 56.0, 73.0)
TERRACE_PLATE = (9.0, 3.0, 9.0)
TERRACE_LIFT, TERRACE_KEY_LIFT, TERRACE_KEY = 20.0, 8.0, 11.0
TERRACE_MASTS, TERRACE_MAST_SEGMENTS = 3, 15
TERRACE_MAST = (5.5, 5.5)
TERRACE_MAST_RADIUS, TERRACE_MAST_PITCH = 88.0, 8.0

# Where each family attaches to the bough. Distinct offsets, because two families sharing a knot
# sample means two structures sharing a point in space, and no per-family fit can see that.
BLOSSOM_PHASE, SKIRT_PHASE, FALL_PHASE, CROWN_PHASE, VINE_BLOSSOM_PHASE = 0, 7, 28, 11, 16

KNOT_AXIS = (0.36, 0.88, 0.31)

# Every constant above is read back out of the C# on --check, so the two cannot drift silently.
MIRRORED_CONSTS = {
    "NucleusR": NUCLEUS_R, "MembraneR": MEMBRANE_R, "CamR": CAM_R,
    "NucleusMargin": NUCLEUS_MARGIN, "ChainFill": CHAIN_FILL, "ChainJit": CHAIN_JIT,
    "ShoreR": SHORE_R, "ShoreBandStep": SHORE_BAND_STEP, "ShoreBands": SHORE_BANDS,
    "ShoreBandLift": SHORE_BAND_LIFT, "ShorePatchPlates": SHORE_PATCH_PLATES,
    "ShorePatchR": SHORE_PATCH_R,
    "BoughP": BOUGH_P, "BoughQ": BOUGH_Q, "BoughMajor": BOUGH_MAJOR,
    "BoughMinor": BOUGH_MINOR, "BoughStep": BOUGH_STEP,
    "VineP": VINE_P, "VineQ": VINE_Q, "VineMajor": VINE_MAJOR, "VineMinor": VINE_MINOR,
    "VineStep": VINE_STEP,
    "Blossoms": BLOSSOMS, "VineBlossoms": VINE_BLOSSOMS,
    "BlossomBosses": BLOSSOM_BOSSES, "BlossomBossRadius": BLOSSOM_BOSS_RADIUS,
    "BlossomBossSize": BLOSSOM_BOSS_SIZE,
    "VineBlossomBosses": VINE_BLOSSOM_BOSSES, "VineBossRadius": VINE_BOSS_RADIUS,
    "VineBossSize": VINE_BOSS_SIZE,
    "Skirts": SKIRTS, "SkirtRows": SKIRT_ROWS, "SkirtLeaves": SKIRT_LEAVES,
    "SkirtStandoff": SKIRT_STANDOFF, "SkirtRowPitch": SKIRT_ROW_PITCH, "SkirtFan": SKIRT_FAN,
    "Falls": FALLS, "FallSteps": FALL_STEPS, "FallSkip": FALL_SKIP,
    "FallStandoff": FALL_STANDOFF, "FallRootOffset": FALL_ROOT_OFFSET,
    "CrownRootOffset": CROWN_ROOT_OFFSET, "FallEase": FALL_EASE,
    "Crowns": CROWNS, "CrownSteps": CROWN_STEPS, "CrownSkip": CROWN_SKIP,
    "CrownTuftLeaves": CROWN_TUFT_LEAVES, "CrownTuftRadius": CROWN_TUFT_RADIUS,
    "CrownTuftCone": CROWN_TUFT_CONE,
    "Terraces": TERRACES, "TerraceLift": TERRACE_LIFT, "TerraceKeyLift": TERRACE_KEY_LIFT,
    "TerraceKey": TERRACE_KEY, "TerraceMasts": TERRACE_MASTS,
    "TerraceMastSegments": TERRACE_MAST_SEGMENTS, "TerraceMastRadius": TERRACE_MAST_RADIUS,
    "TerraceMastPitch": TERRACE_MAST_PITCH,
    "BlossomPhase": BLOSSOM_PHASE, "SkirtPhase": SKIRT_PHASE, "FallPhase": FALL_PHASE, "CrownPhase": CROWN_PHASE,
    "VineBlossomPhase": VINE_BLOSSOM_PHASE,
}

# The arrays, read back the same way. They are not scalars, and a slipped digit in one of them is
# exactly the kind of transcription error the harness would catch as a count mismatch AFTER a
# compile - this catches it before.
MIRRORED_ARRAYS = {
    "BlossomRings": BLOSSOM_RINGS, "BlossomRingRadii": BLOSSOM_RING_RADII,
    "VineBlossomRings": VINE_BLOSSOM_RINGS, "VineBlossomRingRadii": VINE_BLOSSOM_RING_RADII,
    "TerraceGaps": TERRACE_GAPS, "TerraceRings": TERRACE_RINGS,
    "TerraceRingRadii": TERRACE_RING_RADII,
}
MIRRORED_VECTORS = {
    "BoughSection": BOUGH_SECTION, "VineSection": VINE_SECTION,
    "ShoreBandSection": SHORE_BAND_SECTION, "FallSection": FALL_SECTION,
    "CrownSection": CROWN_SECTION, "TerraceMast": TERRACE_MAST,
    "ShorePatchPlate": SHORE_PATCH_PLATE, "BlossomPetal": BLOSSOM_PETAL,
    "VinePetal": VINE_PETAL, "SkirtLeaf": SKIRT_LEAF, "CrownTuftLeaf": CROWN_TUFT_LEAF,
    "TerracePlate": TERRACE_PLATE, "KnotAxis": KNOT_AXIS,
}


def hash01(n):
    """CellEnvironmentSpawnableBase.Hash01 - unchecked uint arithmetic, reproduced exactly."""
    h = n & 0xFFFFFFFF
    h = ((h ^ 61) ^ (h >> 16)) & 0xFFFFFFFF
    h = (h * 9) & 0xFFFFFFFF
    h ^= h >> 4
    h = (h * 0x27D4EB2D) & 0xFFFFFFFF
    h ^= h >> 15
    return (h & 0xFFFFFF) / float(0x1000000)


def hjit(s, n, amt):
    k = 1.0 + (hash01(n) * 2.0 - 1.0) * amt
    return tuple(max(0.5, c * k) for c in s)


def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, k): return (a[0] * k, a[1] * k, a[2] * k)
def mag(a): return math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def lerp(a, b, t): return a + (b - a) * t


def norm(a):
    m = mag(a)
    return (0.0, 0.0, 0.0) if m < 1e-9 else (a[0] / m, a[1] / m, a[2] / m)


FORWARD, UP, RIGHT = (0.0, 0.0, 1.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)
_W = norm(KNOT_AXIS)
_U = norm(cross(_W, FORWARD))
_V = cross(_W, _U)


def to_world(x, y, z):
    return add(add(mul(_U, x), mul(_V, y)), mul(_W, z))


def knot_point(t, p, q, major, minor):
    r = major + minor * math.cos(q * t)
    return to_world(r * math.cos(p * t), r * math.sin(p * t), minor * math.sin(q * t))


def hub_point(t, p, major):
    return to_world(major * math.cos(p * t), major * math.sin(p * t), 0.0)


def sample_knot(p, q, major, minor, step):
    SUB = 4096
    arc = [0.0] * (SUB + 1)
    prev = knot_point(0.0, p, q, major, minor)
    for i in range(1, SUB + 1):
        cur = knot_point(2.0 * math.pi * i / SUB, p, q, major, minor)
        arc[i] = arc[i - 1] + mag(sub(cur, prev))
        prev = cur
    total = arc[SUB]
    n = max(8, math.floor(total / step + 0.5))
    out, cursor = [], 0
    for k in range(n):
        want = total * k / n
        while cursor < SUB and arc[cursor + 1] < want:
            cursor += 1
        span = arc[cursor + 1] - arc[cursor]
        f = (want - arc[cursor]) / span if span > 1e-6 else 0.0
        t = 2.0 * math.pi * (cursor + f) / SUB
        pos = knot_point(t, p, q, major, minor)
        H = 1e-3
        tangent = norm(sub(knot_point(t + H, p, q, major, minor),
                           knot_point(t - H, p, q, major, minor)))
        outward = norm(sub(pos, hub_point(t, p, major)))
        out.append((pos, tangent, outward))
    return out, total


def look_rotation(forward, up):
    """Unity's Quaternion.LookRotation as the BASIS it is: z along forward, x = up x z, y = z x x.

    The mirror carries orientations because the cell is checked for CLIPPING, and a prism is an
    oriented box - measuring the cloud as points would report a world of axis-aligned prisms and
    call it clear. SpawnPoint.LookRotation's degenerate guard is reproduced too."""
    if dot(forward, forward) < 0.0001:
        return (RIGHT, UP, FORWARD)
    z = norm(forward)
    x = cross(up, z)
    if dot(x, x) < 1e-10:
        raise ValueError("look_rotation: forward is parallel to up")
    x = norm(x)
    return (x, cross(z, x), z)


def chain_up(step, primary, fallback):
    """A chain prism's UP - the roll about its own length - made safe.

    LookRotation is undefined when up is parallel to forward, and a fall's last steps are very
    nearly radial while its authored up IS the radial. Unity does not report that: it invents a
    pose. So the up is projected off the step, and a second, orthogonal candidate takes over when
    the projection collapses. The mirror THROWS on the degenerate pose rather than measuring one
    the engine made up, which is how this was found at all."""
    d = norm(step)
    perp = sub(primary, mul(d, dot(primary, d)))
    if dot(perp, perp) < 0.02:
        perp = sub(fallback, mul(d, dot(fallback, d)))
    return perp


class Build:
    """The lay list, plus the hash grid the yielding families query.

    YIELDING is what makes "nothing clips" a property of the GENERATOR rather than of five tuned
    phase constants. Most families clear each other by an authored clearance that can be stated
    and checked - a ring pitch against a petal's width, a chain's fill against its step. Three
    cannot: the falls cross the whole cell from the bough to the seed, the patches land wherever a
    root came down, and the bands ring the seed through those landfalls. Those three meet whatever
    happens to be there, so a prism of theirs that would land inside something already laid is
    simply NOT LAID. Nothing is removed and nothing is moved; the root grows round the flower."""

    def __init__(self):
        self.lays = []
        self.family = "?"
        self.yielded = collections.Counter()
        # How close every yield DECISION came to its threshold. The model runs in float64 and the
        # engine in float32, so a candidate sitting on the boundary would be laid by one and
        # dropped by the other - a one-prism disagreement with no obvious cause. Asserted.
        self.margins = []
        self._grid = collections.defaultdict(list)

    def _key(self, p):
        return (math.floor(p[0] / YIELD_CELL), math.floor(p[1] / YIELD_CELL),
                math.floor(p[2] / YIELD_CELL))

    def emit(self, pos, rot, scale, dom, kind="Plain"):
        self.lays.append((pos, rot, scale, dom, kind, self.family))
        self._grid[self._key(pos)].append(len(self.lays) - 1)

    def obstructed(self, pos, rot, scale):
        box = (pos, rot, (0.5 * scale[0], 0.5 * scale[1], 0.5 * scale[2]))
        r = 0.5 * mag(scale)
        k0 = self._key(pos)
        closest = 1e18
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for i in self._grid.get((k0[0] + dx, k0[1] + dy, k0[2] + dz), ()):
                        op, orot, os, _d, _k, _f = self.lays[i]
                        dd = sub(op, pos)
                        rr = r + 0.5 * mag(os)
                        if dot(dd, dd) > rr * rr:
                            continue
                        sep = sat_separation(box, (op, orot, (0.5 * os[0], 0.5 * os[1],
                                                             0.5 * os[2])))
                        closest = min(closest, sep)
        if closest < 1e17:
            self.margins.append(abs(closest - YIELD_GAP))
        return closest < YIELD_GAP

    def try_emit(self, pos, rot, scale, dom, kind="Plain"):
        if self.obstructed(pos, rot, scale):
            self.yielded[self.family] += 1
            return False
        self.emit(pos, rot, scale, dom, kind)
        return True


def chain_scale(cross_section, span, n, jit=CHAIN_JIT):
    """A chain prism's size: the authored cross-section, with its LENGTH derived from the gap it
    has to fill rather than authored.

    Every long family here (bough, vine, falls, crown branches) is one prism per step, and the
    length that makes such a chain read as a continuous line is a fraction of the step, not a
    constant - the falls' step is a third of the bough's and the crown's grows as the branch
    climbs. CHAIN_FILL is that fraction, and it is under 1 by enough to absorb the jitter AND the
    corner a bend puts on the inside of the joint: at 1 the chain welds itself shut and every
    consecutive pair interpenetrates, which is exactly what the cell used to do."""
    return hjit((cross_section[0], cross_section[1], CHAIN_FILL * span), n, jit)


def build_environment(blossom_ring_radii=BLOSSOM_RING_RADII):
    """blossom_ring_radii is a parameter ONLY so the negative control can push the outer ring in
    toward the seed and watch the nucleus assertion fire. Every real call takes the authored
    tuple."""
    b = Build()
    bough, bough_len = sample_knot(BOUGH_P, BOUGH_Q, BOUGH_MAJOR, BOUGH_MINOR, BOUGH_STEP)
    vine, vine_len = sample_knot(VINE_P, VINE_Q, VINE_MAJOR, VINE_MINOR, VINE_STEP)

    def chord(knot, i):
        return mag(sub(knot[(i + 1) % len(knot)][0], knot[i][0]))

    b.family = "bough"
    for i, (pos, tan, out) in enumerate(bough):
        b.emit(pos, look_rotation(tan, out),
               chain_scale(BOUGH_SECTION, chord(bough, i), i * 13 + 5),
               "Gold" if i % 9 == 0 else "Jade")

    b.family = "vine"
    for i, (pos, tan, out) in enumerate(vine):
        b.emit(pos, look_rotation(tan, out),
               chain_scale(VINE_SECTION, chord(vine, i), i * 7 + 19),
               "Gold" if i % 11 == 0 else "Jade")

    def lay_blossoms(knot, count, rings, radii, petal, bosses, boss_r, boss_s,
                     salt_base, phase, fam):
        b.family = fam
        outer = radii[-1]
        for bi in range(count):
            pos, tan, out = knot[(bi * len(knot) // count + phase) % len(knot)]
            axis, u_ax = tan, out
            v_ax = cross(axis, u_ax)
            salt = salt_base
            for r, (n, rr) in enumerate(zip(rings, radii)):
                for i in range(n):
                    salt += 1
                    a = 2.0 * math.pi * i / n + r * 0.37
                    outward = add(mul(u_ax, math.cos(a)), mul(v_ax, math.sin(a)))
                    scale = hjit(petal, salt + bi * 131, 0.14)
                    d = norm(add(outward, mul(axis, 0.34 * rr / outer)))
                    b.emit(add(pos, mul(d, rr)), look_rotation(d, axis), scale,
                           "Ruby" if hash01((salt + bi * 131) * 3) < 0.16 else "Gold")
            for c in range(bosses):
                a = 2.0 * math.pi * c / bosses
                outward = add(mul(u_ax, math.cos(a)), mul(v_ax, math.sin(a)))
                b.emit(add(pos, mul(outward, boss_r)), look_rotation(outward, axis),
                       (boss_s, boss_s, boss_s), "Gold", "SuperShielded")

    lay_blossoms(bough, BLOSSOMS, BLOSSOM_RINGS, blossom_ring_radii, BLOSSOM_PETAL,
                 BLOSSOM_BOSSES, BLOSSOM_BOSS_RADIUS, BLOSSOM_BOSS_SIZE, 1000,
                 BLOSSOM_PHASE, "blossoms")
    lay_blossoms(vine, VINE_BLOSSOMS, VINE_BLOSSOM_RINGS, VINE_BLOSSOM_RING_RADII, VINE_PETAL,
                 VINE_BLOSSOM_BOSSES, VINE_BOSS_RADIUS, VINE_BOSS_SIZE, 2000,
                 VINE_BLOSSOM_PHASE, "vine blossoms")

    b.family = "skirts"
    for k in range(SKIRTS):
        pos, tan, out = bough[(k * len(bough) // SKIRTS + SKIRT_PHASE) % len(bough)]
        hang = mul(out, -1.0)
        side = cross(tan, hang)
        for row in range(SKIRT_ROWS):
            along = mul(tan, (row - (SKIRT_ROWS - 1) * 0.5) * SKIRT_ROW_PITCH)
            for i in range(SKIRT_LEAVES):
                salt = 3000 + k * 97 + row * 11 + i
                a = (i - (SKIRT_LEAVES - 1) * 0.5) * SKIRT_FAN
                d = norm(add(mul(hang, math.cos(a)), mul(side, math.sin(a))))
                scale = hjit(SKIRT_LEAF, salt, 0.18)
                b.emit(add(add(pos, along), mul(d, SKIRT_STANDOFF + scale[2] * 0.5)),
                       look_rotation(d, tan), scale, "Jade")


    for c in range(CROWNS):
        idx = (c * len(bough) // CROWNS + CROWN_PHASE) % len(bough)
        pos, tan, _o = bough[idx]
        pos = mul(norm(pos), mag(pos) + CROWN_ROOT_OFFSET)
        r0 = mag(pos)
        n0 = mul(pos, 1.0 / r0)
        drift = norm(sub(tan, mul(n0, dot(tan, n0))))
        r_end = 900.0 + 150.0 * hash01(8000 + c * 29)
        turn = 0.10 + 0.10 * hash01(8100 + c * 31)
        prev, tip_dir = pos, n0
        b.family = "crown"
        for i in range(1, CROWN_STEPS + 1):
            t = i / float(CROWN_STEPS)
            r = lerp(r0, r_end, t)
            ang = turn * 2.0 * math.pi * t
            d = norm(add(mul(n0, math.cos(ang)), mul(drift, math.sin(ang))))
            p = mul(d, r)
            stepv = sub(p, prev)
            salt = 8200 + c * 173 + i
            taper = lerp(1.0, 0.45, t)
            if i > CROWN_SKIP:
                b.emit(add(prev, mul(stepv, 0.5)),
                       look_rotation(stepv, chain_up(stepv, n0, drift)),
                       chain_scale((CROWN_SECTION[0] * taper, CROWN_SECTION[1] * taper),
                                   mag(stepv), salt), "Jade")
            prev, tip_dir = p, d

        b.family = "crown tufts"
        tu = (norm(cross(tip_dir, RIGHT)) if dot(cross(tip_dir, UP), cross(tip_dir, UP)) < 1e-4
              else norm(cross(tip_dir, UP)))
        tv = cross(tip_dir, tu)
        cos_max = math.cos(CROWN_TUFT_CONE)
        for i in range(CROWN_TUFT_LEAVES):
            salt = 8400 + c * 191 + i
            # A CAP, not a ring: the old fan put every leaf at one polar angle, so a tuft was a
            # circle of leaves whose spacing fell as the count rose - 28 of them on one ring
            # cannot clear each other at any size worth drawing.
            cz = 1.0 - (1.0 - cos_max) * (i + 0.5) / CROWN_TUFT_LEAVES
            rho = math.sqrt(max(0.0, 1.0 - cz * cz))
            a = i * GOLDEN
            outward = norm(add(mul(tip_dir, cz),
                               add(mul(tu, rho * math.cos(a)), mul(tv, rho * math.sin(a)))))
            scale = hjit(CROWN_TUFT_LEAF, salt, 0.18)
            b.emit(add(prev, mul(outward, CROWN_TUFT_RADIUS)), look_rotation(outward, tip_dir),
                   scale, "Gold" if hash01(salt * 7) < 0.22 else "Jade")

    b.family = "terraces"
    for k in range(TERRACES):
        idx = (TERRACE_GAPS[k] * len(bough) // BLOSSOMS
               + len(bough) // (2 * BLOSSOMS) + BLOSSOM_PHASE) % len(bough)
        pos, tan, out = bough[idx]
        # A terrace stands on the bough's own surface normal, FLIPPED to the side the seed is
        # not on. The raw normal points at the seed on the knot's inner equator, so a deck built
        # on it grows INTO the nucleus (a 219-unit mast ended up 90 units inside the control
        # radius that way); the cell radial fixes that and breaks something else, because the
        # deck plane then no longer contains the tangent and the bough runs out through the
        # floor. The flip keeps both: up is never toward the seed, and the wood still lies in
        # the deck's own plane where it can be cleared by the lift.
        n = out if dot(out, norm(pos)) >= 0.0 else mul(out, -1.0)
        u = tan
        v = cross(n, u)
        c = add(pos, mul(n, TERRACE_LIFT))
        for r, (count, radius) in enumerate(zip(TERRACE_RINGS, TERRACE_RING_RADII)):
            for i in range(count):
                salt = 9000 + k * 331 + r * 37 + i
                a = 2.0 * math.pi * i / count + r * 0.4
                outward = add(mul(u, math.cos(a)), mul(v, math.sin(a)))
                b.emit(add(c, mul(outward, radius)), look_rotation(outward, n),
                       hjit(TERRACE_PLATE, salt, 0.16),
                       "Gold" if hash01(salt * 3) < 0.3 else "Blue")
        for m in range(TERRACE_MASTS):
            a = m * 2.0 * math.pi / TERRACE_MASTS + 0.6
            outward = add(mul(u, math.cos(a)), mul(v, math.sin(a)))
            base = add(c, mul(outward, TERRACE_MAST_RADIUS))
            for i in range(TERRACE_MAST_SEGMENTS):
                salt = 9500 + k * 419 + m * 53 + i
                h = 9.0 + i * TERRACE_MAST_PITCH
                taper = lerp(1.0, 0.5, i / float(TERRACE_MAST_SEGMENTS - 1))
                b.emit(add(base, mul(n, h)), look_rotation(n, outward),
                       chain_scale((TERRACE_MAST[0] * taper, TERRACE_MAST[1] * taper),
                                   TERRACE_MAST_PITCH, salt), "Blue")
        b.emit(add(c, mul(n, TERRACE_KEY_LIFT)), look_rotation(n, u),
               (TERRACE_KEY, TERRACE_KEY, TERRACE_KEY), "Gold", "SuperShielded")

    # The shore is laid LAST, and it is the part of the cell that yields. Everything above
    # clears by a clearance it states itself; these three meet whatever the rest of the
    # world put in their way, so they give ground instead of being tuned around it.
    for f in range(FALLS):
        pos, tan, out = bough[(f * len(bough) // FALLS + FALL_PHASE) % len(bough)]
        pos = add(pos, mul(norm(cross(tan, out)), FALL_ROOT_OFFSET))
        r0 = mag(pos)
        n0 = mul(pos, 1.0 / r0)
        drift = norm(sub(tan, mul(n0, dot(tan, n0))))
        turn = 0.30 + 0.16 * hash01(4000 + f * 17)
        r_end = SHORE_R + FALL_STANDOFF
        prev = pos
        land = pos
        b.family = "falls"
        for i in range(1, FALL_STEPS + 1):
            t = i / float(FALL_STEPS)
            r = lerp(r0, r_end, t ** FALL_EASE)
            ang = turn * 2.0 * math.pi * t
            d = norm(add(mul(n0, math.cos(ang)), mul(drift, math.sin(ang))))
            p = mul(d, r)
            stepv = sub(p, prev)
            # The first steps are SKIPPED, not shortened: a chain that starts at its parent's own
            # lay point starts inside it, and no length makes that pair clear.
            if i > FALL_SKIP and dot(stepv, stepv) > 1e-4:
                salt = 5000 + f * 211 + i
                b.try_emit(add(prev, mul(stepv, 0.5)),
                           look_rotation(stepv, chain_up(stepv, n0, drift)),
                           chain_scale(FALL_SECTION, mag(stepv), salt),
                           "Gold" if i > FALL_STEPS - 10 else "Jade")
            prev, land = p, p

        b.family = "shore patches"
        salt0 = 6000 + f * 53
        n = norm(land)
        u = norm(cross(n, RIGHT)) if dot(cross(n, UP), cross(n, UP)) < 1e-4 else norm(cross(n, UP))
        v = cross(n, u)
        for i in range(SHORE_PATCH_PLATES):
            a = i * GOLDEN
            rr = SHORE_PATCH_R * math.sqrt((i + 0.5) / SHORE_PATCH_PLATES)
            p = add(mul(n, SHORE_R), mul(add(mul(u, math.cos(a)), mul(v, math.sin(a))), rr))
            b.try_emit(p, look_rotation(u, n), hjit(SHORE_PATCH_PLATE, salt0 + i, 0.16),
                       "Gold" if hash01(salt0 + i * 5) < 0.25 else "Jade")

    b.family = "shore bands"
    for bi in range(SHORE_BANDS):
        tilt = bi * GOLDEN
        axis = norm((math.cos(tilt), 0.42, math.sin(tilt)))
        u = norm(cross(axis, FORWARD))
        v = cross(axis, u)
        band_r = SHORE_R + (bi + 1) * SHORE_BAND_LIFT
        n = max(8, math.floor(2.0 * math.pi * band_r / SHORE_BAND_STEP + 0.5))
        for i in range(n):
            salt = 7000 + bi * 977 + i
            # A coastline is not a hoop: drop a fifth of the plates so the band breaks up.
            if hash01(salt * 11) < 0.20:
                continue
            a = 2.0 * math.pi * i / n
            d = add(mul(u, math.cos(a)), mul(v, math.sin(a)))
            tangent = norm(sub(mul(v, math.cos(a)), mul(u, math.sin(a))))
            b.try_emit(mul(d, band_r), look_rotation(tangent, d),
                       chain_scale(SHORE_BAND_SECTION, 2.0 * math.pi * band_r / n, salt, 0.14),
                       "Blue" if bi == 1 else "Jade")
    return b


# ─────────────────────────────────────────────────────────────────────────────
# 1b. Clipping - a property of the CLOUD, not of any family's own parameters
# ─────────────────────────────────────────────────────────────────────────────
#
# Two prisms occupying the same space is the one defect a per-family fit cannot see: it is a
# relationship between families that were each individually correct, so it has to be measured
# over what the generator actually emitted, in the orientations it emitted them. (The gyroid
# lattice-scale finding, one level up: every constant correct, the RELATIONSHIP wrong.)

def sat_separation(a, b):
    """Signed separation of two oriented boxes over the 15 separating axes: POSITIVE is the gap,
    NEGATIVE is how deep they interpenetrate. Signed rather than boolean because a fit that only
    reports 'no overlaps' cannot tell a cell that clears by a hair from one that clears by a
    prism, and the first re-reads as clipping the moment anything moves."""
    (ac, ax, ahe), (bc, bx, bhe) = a, b
    d = sub(bc, ac)
    best = -1e18
    axes = list(ax) + list(bx)
    for i in range(3):
        for j in range(3, 6):
            axes.append(cross(axes[i], axes[j]))
    for L in axes:
        ll = math.sqrt(dot(L, L))
        if ll < 1e-9:
            continue
        ra = sum(abs(dot(ax[t], L)) * ahe[t] for t in range(3))
        rb = sum(abs(dot(bx[t], L)) * bhe[t] for t in range(3))
        sep = (abs(dot(d, L)) - ra - rb) / ll
        if sep > best:
            best = sep
    return best


def clip_report(b):
    boxes = []
    for pos, rot, s, _d, _k, fam in b.lays:
        he = (0.5 * s[0], 0.5 * s[1], 0.5 * s[2])
        boxes.append(((pos, rot, he), 0.5 * mag(s), fam))
    cell = max(2.0 * r for _bx, r, _f in boxes)
    grid = collections.defaultdict(list)
    for i, (bx, _r, _f) in enumerate(boxes):
        p = bx[0]
        grid[(math.floor(p[0] / cell), math.floor(p[1] / cell), math.floor(p[2] / cell))].append(i)

    pairs = collections.Counter()
    worst = 0.0
    tightest = 1e18
    tested = 0
    for i, (bxi, ri, fi) in enumerate(boxes):
        p = bxi[0]
        k0 = (math.floor(p[0] / cell), math.floor(p[1] / cell), math.floor(p[2] / cell))
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for j in grid.get((k0[0] + dx, k0[1] + dy, k0[2] + dz), ()):
                        if j <= i:
                            continue
                        bxj, rj, fj = boxes[j]
                        dd = sub(bxj[0], p)
                        if dot(dd, dd) > (ri + rj) ** 2:
                            continue
                        tested += 1
                        sep = sat_separation(bxi, bxj)
                        if sep < 0:
                            pairs[tuple(sorted((fi, fj)))] += 1
                            worst = max(worst, -sep)
                        elif sep < tightest:
                            tightest = sep
    return dict(pairs=pairs, total=sum(pairs.values()), worst=worst,
                tightest=0.0 if tightest > 1e17 else tightest, tested=tested)


def measure(b):
    fams = collections.OrderedDict()
    per_domain = collections.Counter()
    kinds = collections.Counter()
    near_min, far_max, worst = 1e9, 0.0, None
    for pos, _rot, s, dom, kind, fam in b.lays:
        vol = s[0] * s[1] * s[2]
        e = fams.setdefault(fam, [0, 0.0])
        e[0] += 1
        e[1] += vol
        per_domain[dom] += vol
        kinds[kind] += 1
        r = mag(pos)
        half = 0.5 * math.sqrt(s[0] ** 2 + s[1] ** 2 + s[2] ** 2)
        if r - half < near_min:
            near_min, worst = r - half, (fam, r, half)
        far_max = max(far_max, r + half)
    return dict(
        families=fams,
        count=sum(v[0] for v in fams.values()),
        volume=sum(v[1] for v in fams.values()),
        per_domain=per_domain, kinds=kinds,
        nearest=near_min, farthest=far_max, worst=worst,
        max_axis=max(max(s) for _p, _r, s, _d, _k, _f in b.lays),
        min_axis=min(min(s) for _p, _r, s, _d, _k, _f in b.lays),
        max_bounding_radius=max(0.5 * mag(s) for _p, _r, s, _d, _k, _f in b.lays),
        clips=clip_report(b), yielded=b.yielded,
        yield_margin=min(b.margins) if b.margins else 1e9,
    )



# ─────────────────────────────────────────────────────────────────────────────
# 2. The roster - what this cell GROWS on top of what it lays
# ─────────────────────────────────────────────────────────────────────────────
#
# Four flora and three fauna, each picked for silhouette at the menu camera's distance rather
# than for detail. Every species uses the CANONICAL per-element assets as its ElementPalette, so
# an element keeps its own identity (leaf shape, budget, grow tempo, heart size) and this cell
# only says HOW MANY and WHERE - the split FloraConfigurationSO's cell-level overrides exist for.

Flora = collections.namedtuple(
    "Flora", "name prefab_guid palette floor cap budgets band_min band_max")
Fauna = collections.namedtuple("Fauna", "name prefab_guid prefab_fid palette floor cap")

FLORA_COMPONENT_FID = 7514956980722975813   # PhyllotacticFlora, shared by the four species

FLORA = [
    # Budgets are MaxTotalSpawnedObjects per element (Charge, Mass, Space, Time), read off the
    # shipped Lifeforms assets. The cap-worst-case below takes the MAX: a roll can hand every
    # plant the heaviest element, and a ladder authored against the mean is a ladder that
    # occasionally freezes the garden.
    Flora("Arbor",   "38c135e3fee4b466a5ec7e946ee3415d",
          ["8142092b395852d6b70857945eddf051", "789fa0009f601288c6868e7d32c734f9",
           "e800b105afc6ed81fbbe4da2fc221c5d", "636374a00e41c98762163631b2e60477"],
          3, 5, (221, 312, 182, 260), 0.36, 0.62),
    Flora("Tendril", "6a74ca1c30e05f9e311977a264ec5a55",
          ["5b64944b81691f465735bd0a969e4d3a", "a3180b4a6df35fe482db094cd9418b6b",
           "c408a1b8caed658f2d12c55d7bb235a7", "000b83422a748f6434eaeeecc2be7400"],
          4, 6, (102, 144, 84, 120), 0.40, 0.70),
    Flora("Lantern", "02d6411760b77441c06bd767aae879cf",
          ["5f677807a75e4c3dd300c74c2c08845c", "f9c6de2b9c6c7bac6b64b1ae759b74c1",
           "6a2756de008a569c5f02ba30dff53835", "f3d94c9041756fb024d75b4adce9dc66"],
          4, 7, (60, 84, 49, 70), 0.36, 0.56),
    Flora("Spire",   "becb04107104ecb6768ae2d6766e681e",
          ["25e619766581c4807de09c87fddf877c", "10859a22ceb9349219eabe30541f5341",
           "f46966ea66e278b0ad293ccecc672c1d", "6771e101fe9fa81ae0161ecbffc4c901"],
          2, 3, (144, 204, 119, 170), 0.45, 0.78),
]

FAUNA = [
    # Brittlestar and QuadFish are herbivores (diet 0), the shark is the predator (diet 1) - the
    # whole food web this cell needs, at sixteen animals. Note the component fileID differs
    # between the two fauna prefab families; it is copied per species from the shipped asset,
    # because a wrong one resolves to no component at all and spawns nothing, silently.
    Fauna("Brittlestar", "c719f00ea7596c24185379994f7dc824", 5351160486092638538,
          ["503de8d514bf4001a067b76f07c246c5", "26691ece54c94157aba5b832451ec2a2",
           "eb3b0459459a4ee0b1212c181ce80a11", "135c28565d034815adaadd2e66233711"], 3, 6),
    Fauna("QuadFish", "19615ed0c903b1041973d70593d4b0a3", 4652232322436628206,
          ["4053ff006892420d8ca5efa51365570c", "5697aa8685514f2ca9b9de9638fce1a1",
           "3107bcc776d54a74b110b883f10fba61", "414bce89d4dc495f87bbccfb02e5b847"], 4, 8),
    Fauna("Shark", "a67ba7ddaecf6624ab37cd9f5f2210a6", 5351160486092638538,
          ["58835b82ea284255855af2649ef185a5", "a690f25bf21e486ba0e500563b90f1ea",
           "eaf56c14345740849f35fc84467059e9", "78ce842bb8554d748af1e96abf430137"], 1, 2),
]

CALIBRATION = {
    # ESTIMATED, and the only estimate in this file. PhyllotacticFlora sizes prisms by ROLE:
    #   stem = (LeafSize.xy x stemScale.xy x depthTaper^d,  segment x stemScale.z)
    #   leaf = (LeafSize.x x leafScale.x x taper, LeafSize.y x leafScale.y x taper, reach x leafScale.z)
    # Evaluated for Arbor's Mass element at 0.6 x maxDepth (branching weights the population
    # toward the deeper nodes) and mixed 38% stem / 62% leaf gives ~52; x E[j^3] = 1.04 for the
    # prefab's 0.2 prism jitter gives ~54. 80 is carried instead - deliberately HIGH, because an
    # overstated forest volume makes Frenzy arrive LATER, which is the safe direction
    # (Docs/ECOSYSTEM.md §27.4), and because these four species' leaves differ.
    #
    # E[k^3] for k ~ U(1-a, 1+a) is ((1+a)^4 - (1-a)^4) / (8a) = 1.04 at a = 0.2. The 8a is
    # load-bearing: ribcage_budget.py used 4a, got 2.08, and every volume threshold derived from
    # it described a cell twice as heavy as the one that exists (see CLAUDE.md, Cleave).
    "flora_volume_per_prism": 80.0,
    # A creature's body is ~4 prisms (Wildlife Liberation measured 4,155 body prisms over a
    # 1,198 cap). SKELETONS persist after a death and are grazed rather than despawned
    # (Docs/ECOSYSTEM.md §26.7), so the standing allowance is several times the live bodies.
    "fauna_prisms_each": 4,
    "fauna_skeleton_allowance": 200,
    "fauna_volume_per_prism": 16.0,
}

# The trail band: how much VESSEL trail the ladder leaves above the mature cell before the phase
# moves. These are the Blob deltas every freestyle cell authors (Docs/ECOSYSTEM.md §18), kept so
# a pilot has to fly the same amount here as in the other seven worlds to move the phase.
TRAIL_BAND = dict(restless_hyst_count=200, restless_hyst_volume=3200,
                  frenzy_count=3600, frenzy_exit_count=600,
                  frenzy_volume=57600, frenzy_exit_volume=9600)

# Where RESTLESS sits: the fauna must start hunting while the garden is still filling in, or the
# food web is dormant for the whole of the cell's growth and the equilibrium never starts
# breathing (the /ecology skill §7: "put Restless somewhere the fauna start hunting a
# partly-grown cell"). A third of the way up the planting budget.
RESTLESS_AT_FLORA_FRACTION = 0.35

# SpawnablePrism.prefab's serialized PrismScaleAnimator window - read, not assumed.
PRISM_MIN_AXIS, PRISM_MAX_AXIS = 0.5, 100.0


def roster_model(env):
    flora_cap_count = sum(f.cap * max(f.budgets) for f in FLORA)
    flora_floor_count = sum(f.floor * max(f.budgets) for f in FLORA)
    flora_cap_vol = flora_cap_count * CALIBRATION["flora_volume_per_prism"]

    fauna_cap_plants = sum(f.cap for f in FAUNA)
    fauna_count = (fauna_cap_plants * CALIBRATION["fauna_prisms_each"]
                   + CALIBRATION["fauna_skeleton_allowance"])
    fauna_vol = fauna_count * CALIBRATION["fauna_volume_per_prism"]

    mature_count = env["count"] + flora_cap_count + fauna_count
    mature_vol = env["volume"] + flora_cap_vol + fauna_vol

    restless_count = env["count"] + int(flora_cap_count * RESTLESS_AT_FLORA_FRACTION)
    restless_vol = env["volume"] + flora_cap_vol * RESTLESS_AT_FLORA_FRACTION

    t = TRAIL_BAND
    ladder = dict(
        RestlessEnter=restless_count,
        RestlessExit=restless_count - t["restless_hyst_count"],
        FrenzyEnter=mature_count + t["frenzy_count"],
        FrenzyExit=mature_count + t["frenzy_count"] - t["frenzy_exit_count"],
        RestlessEnterVolume=round(restless_vol),
        RestlessExitVolume=round(restless_vol) - t["restless_hyst_volume"],
        FrenzyEnterVolume=round(mature_vol) + t["frenzy_volume"],
        FrenzyExitVolume=round(mature_vol) + t["frenzy_volume"] - t["frenzy_exit_volume"],
    )
    return dict(ladder=ladder, flora_cap_count=flora_cap_count,
                flora_floor_count=flora_floor_count, flora_cap_vol=flora_cap_vol,
                fauna_count=fauna_count, fauna_vol=fauna_vol,
                mature_count=mature_count, mature_vol=mature_vol,
                heart_colliders=sum(f.cap for f in FLORA) + fauna_cap_plants)


# ─────────────────────────────────────────────────────────────────────────────
# 3. Invariants
# ─────────────────────────────────────────────────────────────────────────────

def check_invariants(env, roster, problems):
    def need(ok, msg):
        if not ok:
            problems.append(msg)

    # The one that has shipped broken before (Caldera laid 89% of its mass inside the nucleus):
    # asserted on each prism's NEAREST CORNER, not its lay point.
    need(env["nearest"] >= NUCLEUS_R + NUCLEUS_MARGIN,
         f"a prism reaches {env['nearest']:.1f} from the centre - inside NucleusR {NUCLEUS_R} "
         f"+ margin {NUCLEUS_MARGIN} (worst: {env['worst']}). The nucleus interior is the "
         f"territorial claim; laying in it pre-awards node control (Docs/ECOSYSTEM.md §13).")

    need(env["farthest"] <= MEMBRANE_R,
         f"a prism reaches {env['farthest']:.1f}, outside the membrane {MEMBRANE_R} - "
         f"Cell.ContainsPosition rejects it and neither the volume ladder nor the fauna "
         f"density grids can see it.")

    # The ORDERING the composition is built on, asserted rather than the values (the /ecology
    # skill's rule for absolute-distance tolerances).
    need(BOUGH_MAJOR - BOUGH_MINOR >= NUCLEUS_R + BLOSSOM_RING_RADII[-1] + NUCLEUS_MARGIN,
         "the bough's inner radius does not clear a blossom sitting at its closest approach.")
    need(VINE_MAJOR - VINE_MINOR >= NUCLEUS_R + VINE_BLOSSOM_RING_RADII[-1] + NUCLEUS_MARGIN,
         "the vine's inner radius does not clear a vine blossom at its closest approach.")

    # NOTHING CLIPS ANYTHING. Measured over the whole emitted cloud with a 15-axis
    # separating-axis test, because two prisms occupying the same space is the one defect a
    # per-family fit cannot see: it is a relationship between families that were each
    # individually correct. (The gyroid lattice-scale finding, one level up - every constant
    # correct, the RELATIONSHIP wrong, and no static check able to see it.)
    clips = env["clips"]
    if clips["total"]:
        worst = ", ".join(f"{a} x {b}: {n}" for (a, b), n in clips["pairs"].most_common(4))
        need(False, f"{clips['total']} clipping prism pairs (deepest {clips['worst']:.2f}u) - "
                    f"{worst}")
    need(clips["tightest"] >= 0.1,
         f"the tightest non-clipping pair clears by only {clips['tightest']:.3f}u - a fit that "
         f"clears by a hair re-reads as clipping the moment anything moves.")

    # The yield is a LAST RESORT, not a crutch. A family that gives up a big share of itself has
    # stopped being authored and started being carved, and it would thin silently.
    for fam, n in env["yielded"].items():
        laid = env["families"].get(fam, [0])[0]
        need(n <= 0.05 * (laid + n),
             f"the {fam} yielded {n} of {laid + n} prisms - past a twentieth, the family is being "
             f"carved by whatever it runs into rather than authored, and it thins silently.")

    # A yield decision taken ON its threshold is one this model (float64) and the engine (float32)
    # can disagree about, which is a one-prism difference with no obvious cause.
    need(env["yield_margin"] >= 0.02,
         f"a yield decision came within {env['yield_margin']:.4f}u of YIELD_GAP - too close for "
         f"float32 and float64 to be guaranteed to agree about it.")

    # The yield grid's 27-cell neighbourhood only covers every pair that could touch while no
    # prism's bounding radius exceeds half a cell.
    need(env["max_bounding_radius"] <= 0.5 * YIELD_CELL,
         f"a prism's bounding radius is {env['max_bounding_radius']:.1f}, over half the yield "
         f"grid's {YIELD_CELL} cell - the neighbourhood stops covering every pair that can touch.")
    need(VINE_MAJOR + VINE_MINOR <= BOUGH_MAJOR + BOUGH_MINOR,
         "the vine is not inside the bough's envelope - it stops being the inner runner.")
    need(BOUGH_MAJOR - BOUGH_MINOR < CAM_R < BOUGH_MAJOR + BOUGH_MINOR,
         f"the camera orbit {CAM_R} is no longer inside the bough band - the bough stops being "
         f"the subject and becomes a shell the camera looks at from outside.")

    # Armoured-mass budget. This is NOT a collider budget, though it was written as one and the
    # docs repeated it: a shield swaps the MESH and the MASS, never the collider
    # (shieldMeshCollider.enabled = true appears nowhere in the project - four sites, all
    # = false), so a super-shielded prism keeps the same primitive box trigger an ordinary one
    # has. The real cost is to the FOOD WEB: Prism.Consume is a no-op on super-shielded mass and
    # only sheds the shield on shielded mass, and armoured mass also leaves the cell's targeting
    # grids - so every prism counted here is mass the grazers can never remove. In a cell whose
    # whole equilibrium is "the food web holds the population down", that is a real ceiling.
    # Yggdra ships 225; this cell is a menu world and holds itself to a third of that.
    armoured = env["kinds"].get("Shielded", 0) + env["kinds"].get("SuperShielded", 0)
    need(armoured <= 80, f"{armoured} armoured (inedible) prisms - over this cell's 80 budget.")

    # Nothing may state a size outside the shared prism prefab's serialized scale window, because
    # PrismScaleAnimator clamps per axis INSIDE its setter with no log and no return value
    # (Docs/ECOSYSTEM.md §34.9) - an over-range axis is not an error, it is a different arena
    # that looks authored. This generator deliberately does NOT set AdmitsAuthoredPrismScale, so
    # SpawnablePrism.prefab's own window is what binds and every size here has to fit inside it.
    need(PRISM_MIN_AXIS <= env["min_axis"] and env["max_axis"] <= PRISM_MAX_AXIS,
         f"a prism axis lands at {env['min_axis']:.2f}..{env['max_axis']:.2f}, outside "
         f"SpawnablePrism.prefab's [{PRISM_MIN_AXIS}, {PRISM_MAX_AXIS}] scale window - "
         f"PrismScaleAnimator would silently clamp it.")

    # The volume ladder must leave the mature cell room to exist.
    L = roster["ladder"]
    need(L["FrenzyEnterVolume"] > roster["mature_vol"],
         "FrenzyEnterVolume sits at or below the mature cell - planting and growth would freeze "
         "with the garden still bare (Docs/ECOSYSTEM.md §5.1).")
    need(L["FrenzyEnter"] > roster["mature_count"],
         "the Frenzy COUNT backstop sits at or below the mature cell's prism count.")
    need(L["RestlessEnterVolume"] < L["FrenzyEnterVolume"], "Restless is not below Frenzy.")
    need(L["RestlessExitVolume"] < L["RestlessEnterVolume"], "Restless has no hysteresis band.")
    need(L["FrenzyExitVolume"] < L["FrenzyEnterVolume"], "Frenzy has no hysteresis band.")
    need(L["RestlessEnterVolume"] > env["volume"],
         "Restless fires at or below the bare baseline - the cell would boot already hunting.")


def check_source_constants(problems):
    """Every constant the mirror holds is read back out of the C# - a mirror that can drift from
    its source is an estimate wearing a measurement's clothes."""
    if not os.path.exists(SOURCE):
        problems.append(f"missing {os.path.relpath(SOURCE, REPO)}")
        return
    src = open(SOURCE, encoding="utf-8").read()
    for name, want in MIRRORED_CONSTS.items():
        m = re.search(r"\b%s\s*=\s*(-?[\d.]+)f?" % re.escape(name), src)
        if not m:
            problems.append(f"constant {name} not found in SpawnableGarland.cs")
            continue
        got = float(m.group(1))
        if abs(got - float(want)) > 1e-4:
            problems.append(f"{name}: C# says {got}, the model says {want}")
    for name, want in MIRRORED_ARRAYS.items():
        m = re.search(r"\b%s\s*=\s*\{([^}]*)\}" % re.escape(name), src)
        if not m:
            problems.append(f"array {name} not found in SpawnableGarland.cs")
            continue
        got = tuple(float(x) for x in re.findall(r"-?[\d.]+", m.group(1)))
        if len(got) != len(want) or any(abs(a - float(b)) > 1e-4 for a, b in zip(got, want)):
            problems.append(f"{name}: C# says {got}, the model says {tuple(want)}")
    for name, want in MIRRORED_VECTORS.items():
        m = re.search(r"\b%s\s*=\s*new\(([^)]*)\)" % re.escape(name), src)
        if not m:
            problems.append(f"vector {name} not found in SpawnableGarland.cs")
            continue
        got = tuple(float(x) for x in re.findall(r"-?[\d.]+", m.group(1)))
        if len(got) != len(want) or any(abs(a - float(b)) > 1e-4 for a, b in zip(got, want)):
            problems.append(f"{name}: C# says {got}, the model says {tuple(want)}")
    if "AdmitsAuthoredPrismScale" in src:
        problems.append("SpawnableGarland opts into AdmitsAuthoredPrismScale - the prism scale "
                        "window no longer binds, so the max-axis check below is vacuous.")


# ── The harness cross-check: this model vs. what the SHIPPED generator actually emits ──
#
# Tools/Build/garland_harness/run.sh compiles SpawnableGarland.cs against a Unity shim and RUNS
# it. Everything below is asserted against that run, so the two hand-written implementations
# cannot drift apart silently and the ladder cannot ship on a number the engine does not produce.
#
# TOLERANCES. Counts, prism KINDS and per-domain domains are asserted EXACT - they are integers
# and enum members, and a difference there is a transliteration bug, not arithmetic. The float
# quantities carry a tolerance because the engine is float32 and this model is float64: measured,
# the worst positional disagreement is 0.055u on a crown tuft at radius ~1050 (5e-5 relative),
# which is dominated by `Mathf.PI` and `GoldenAngle` being float32 literals multiplied by angles
# up to ~200 radians. 0.25u is that, with room, and is four hundred times smaller than the 17.8u
# nucleus margin it has to protect - so no float difference can turn a passing clearance into a
# failing one or vice versa.
RADIUS_TOLERANCE = 0.25
VOLUME_TOLERANCE = 1e-6

# The shim's transcriptions of PROJECT signatures. A transcription is evidence about the shipped
# code only if something pins it to its source, so each is asserted VERBATIM in the file named.
SHIM_CLAIMS = [
    ("Assets/_Scripts/Controller/Environment/Spawning/CellEnvironmentSpawnableBase.cs", [
        "protected const float GoldenAngle = 2.39996323f;",
        "protected abstract int DefaultSeed { get; }",
        "protected virtual int LayCapacity => 40000;",
        "protected abstract void BuildEnvironment();",
        "protected virtual bool AdmitsAuthoredPrismScale => false;",
        "protected abstract int BuildParameterHash();",
        "protected void Emit(Vector3 pos, Quaternion rot, Vector3 scale, Domains dom, PrismKind kind = PrismKind.Plain)",
        "public abstract class CellEnvironmentSpawnableBase : SpawnableBase",
        # Hash01's BODY, not just its signature: the harness RUNS, and every wobble in the
        # generator is a hash of the emitting index.
        "h = (h ^ 61u) ^ (h >> 16);",
        "h *= 0x27d4eb2du;",
    ]),
    ("Assets/_Scripts/Controller/Environment/Spawning/SpawnPoint.cs", [
        "public static Quaternion LookRotation(Vector3 forward, Vector3 up)",
    ]),
]


def check_shim_fidelity(problems):
    for rel, claims in SHIM_CLAIMS:
        path = os.path.join(REPO, rel)
        if not os.path.exists(path):
            problems.append(f"the harness shim transcribes {rel}, which is missing")
            continue
        src = open(path, encoding="utf-8").read()
        for claim in claims:
            if claim not in src:
                problems.append(f"harness shim claims `{claim.strip()}` from {rel} - not verbatim "
                                f"there any more, so the harness is measuring a different class.")


def check_against_harness(env, problems):
    if not os.path.exists(MEASURED):
        problems.append("no Tools/Build/garland_measurements.json - run "
                        "`bash Tools/Build/garland_harness/run.sh` (needs a dotnet 8 SDK). The "
                        "ladder below is derived from this file's model and is UNVERIFIED "
                        "against the shipped generator until that runs.")
        return
    doc = json.load(open(MEASURED, encoding="utf-8"))

    for rel, want in doc.get("sources", {}).items():
        path = os.path.join(REPO, rel)
        if not os.path.exists(path):
            problems.append(f"the measurement hashes {rel}, which is gone")
        elif hashlib.sha256(open(path, "rb").read()).hexdigest() != want:
            problems.append(f"{rel} has changed since the measurement was taken - re-run "
                            f"Tools/Build/garland_harness/run.sh.")

    def same(label, got, want, tol, rel=False):
        d = abs(got - want)
        if (d / max(1e-9, abs(want)) if rel else d) > tol:
            problems.append(f"the model and the shipped generator disagree on {label}: "
                            f"model {got}, engine {want}.")

    same("prism count", env["count"], doc["count"], 0)
    same("total volume", env["volume"], doc["volume"], VOLUME_TOLERANCE, rel=True)
    same("nearest prism corner", env["nearest"], doc["nearest"], RADIUS_TOLERANCE)
    same("farthest prism corner", env["farthest"], doc["farthest"], RADIUS_TOLERANCE)
    same("smallest prism axis", env["min_axis"], doc["min_axis"], RADIUS_TOLERANCE)
    same("largest prism axis", env["max_axis"], doc["max_axis"], RADIUS_TOLERANCE)
    if dict(env["kinds"]) != doc["kinds"]:
        problems.append(f"prism KINDS differ: model {dict(env['kinds'])}, engine {doc['kinds']}.")
    for dom, vol in doc["per_domain"].items():
        same(f"{dom} volume", env["per_domain"].get(dom, 0.0), vol, VOLUME_TOLERANCE, rel=True)
    if set(env["per_domain"]) != set(doc["per_domain"]):
        problems.append("the model and the engine lay different DOMAINS.")


# ── Every serialized key this script writes must be DECLARED by the class that reads it ──
#
# A key a type no longer declares is not an error in Unity: it deserializes to nothing, the asset
# still loads, and the cell quietly runs on the field's default. That is the same shape as the 40
# SO_ArcadeGame assets still carrying a retired `PreviewClip` (Docs/LAUNCH_BLOCKER_INDEX.md E2) -
# so the templates below are checked against the live C# rather than assumed to have kept up.
SERIALIZED_OWNERS = {
    "Garland Cell Config.asset": ["CellConfigDataSO.cs", "CellPhaseThresholds.cs"],
    "Garland Cell Spawn Profile.asset": ["SpawnProfileSO.cs"],
    "Garland %s Flora Config Data.asset": ["FloraConfigurationSO.cs"],
    "Garland %s Fauna Config Data.asset": ["FaunaConfigurationSO.cs"],
}
UNITY_KEYS = {"m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
              "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script",
              "m_Name", "m_EditorClassIdentifier"}


def _declared_fields(basenames, problems):
    import glob
    known = set()
    for base in basenames:
        hits = glob.glob(os.path.join(REPO, "Assets", "**", base), recursive=True)
        if not hits:
            problems.append(f"cannot find {base} to validate serialized keys against")
            continue
        src = open(hits[0], encoding="utf-8").read()
        known |= set(re.findall(r"public\s+(?:readonly\s+)?[\w<>,\[\]\.\? ]+?\s+(\w+)\s*(?:=|;)", src))
    return known


def check_serialized_keys(files, problems):
    for path, text in files.items():
        if not path.endswith(".asset"):
            continue
        name = os.path.basename(path)
        owners = None
        for pattern, o in SERIALIZED_OWNERS.items():
            if "%s" in pattern:
                head, tail = pattern.split("%s")
                if name.startswith(head) and name.endswith(tail):
                    owners = o
            elif name == pattern:
                owners = o
        if owners is None:
            problems.append(f"{name}: no class recorded to validate its serialized keys against")
            continue
        known = _declared_fields(owners, problems)
        for line in text.splitlines():
            m = re.match(r"^\s{2,4}(\w+):", line)
            if m and m.group(1) not in UNITY_KEYS and m.group(1) not in known:
                problems.append(f"{name} writes `{m.group(1)}`, which {'/'.join(owners)} "
                                f"does not declare - it would deserialize to nothing.")


def self_test():
    """Negative controls. A check nobody has watched fail is a check nobody should trust, and
    this file carries two that would otherwise be easy to mistake for working: the NUCLEUS
    clearance, which has shipped broken before (Caldera laid 89% of its mass inside the seed),
    and the NO-CLIPPING assertion, which passes trivially the moment the measurement stops
    seeing orientations or the SAT loses an axis."""
    global CHAIN_FILL
    failed = []

    wide = list(BLOSSOM_RING_RADII)
    wide[-1] += 90.0
    problems = []
    env = measure(build_environment(blossom_ring_radii=tuple(wide)))
    check_invariants(env, roster_model(env), problems)
    hit = [p for p in problems if "inside NucleusR" in p]
    if hit:
        print("self-test OK - nucleus clearance fires on a deliberately over-wide blossom:")
        print("   ", hit[0].split(" (worst")[0])
    else:
        failed.append("widening the outer blossom ring by 90u did not trip the nucleus clearance")

    # A chain that fills MORE than its own step is exactly the defect this cell shipped with
    # (4,372 clipping pairs): every consecutive pair of every long family welded shut.
    keep, problems = CHAIN_FILL, []
    try:
        CHAIN_FILL = 1.06
        env = measure(build_environment())
        check_invariants(env, roster_model(env), problems)
    finally:
        CHAIN_FILL = keep
    hit = [p for p in problems if "clipping prism pairs" in p]
    if hit:
        print("self-test OK - the clipping assertion fires on a chain fill over 1:")
        print("   ", hit[0][:140])
    else:
        failed.append("CHAIN_FILL 1.06 welds every chain shut and did not trip the clipping check")

    if failed:
        for f in failed:
            print("SELF-TEST FAILED:", f)
        return 1
    return 0


# ─────────────────────────────────────────────────────────────────────────────
# 4. Emitting the assets
# ─────────────────────────────────────────────────────────────────────────────
#
# Guids are FIXED here rather than generated per run: an asset's guid is its identity, so a
# re-run that minted fresh ones would dangle every reference the scene and the configs hold.

GUID = {
    "script":            "d7c41a9e5b3f4e0a8c62f19d4a73b508",   # SpawnableGarland.cs
    "prefab":            "b21f6c8d4e9a47d3ab05e7c2f8143d6b",
    "config":            "3e8a0d5c71b24f96b8e3a4d70c519f2a",
    "profile":           "9c74b2e03fa8425db61ef50a8d3c76e1",
    "Arbor":             "1f5d38a2c604473e9ab7d20e68f4c913",
    "Tendril":           "4a90e6b7d2f34c18ae53b901c7d62e84",
    "Lantern":           "7b23c9f05d8e4a61b04fe8237a95d1c6",
    "Spire":             "c6e14b83a70d495f82d9c1e64b307af5",
    "Brittlestar":       "2d87f41069b34c0ea5931cd7f28e6b40",
    "QuadFish":          "8e05a72cb94f41d6970b3e8a52d1c7f9",
    "Shark":             "5a3bd08e17c6429fb2604ae9d81f3572",
}

# Shared, already-shipped references this cell composes from - the Blob-family membrane, nucleus,
# cytoplasm and modifiers, so Garland is a sibling of the freestyle seven rather than a fork.
REF = dict(
    membrane=(346633111830028674, "6e330f85972faf843b8a128e7166f7b5"),
    nucleus=(7555898194514117247, "b9cf1833fa2493d4b8724ccb6740fb3a"),
    cytoplasm=(639495419069806261, "9cacd903fcf4643459f5f14ac811bb20"),
    modifier=(8058406376250941529, "daa37ae0e7af4b04383c1c4e6e76817d"),
    prism=(4563009547826722997, "ed9defc56162b4b4588e61c20984b6d9"),
    # Shared with Yggdra and Ourobor. A Garland-specific card image is an ART task, and a
    # placeholder that looks authored is worse than one that is visibly borrowed.
    icon=(21300000, "6aa1c06e11b265744a5f9fa8858ac72a"),
)

SCRIPT_GUID = dict(
    cell_config="01f934d50526431a9392a6ceca1dc33d",
    spawn_profile="e8d8aa5d835249798a256e18f2f7d912",
    flora_config="a32a297a7606432885f4d3e1f83bea9a",
    fauna_config="c778cfbe4dfc4c5c8401e40c17802311",
)

def head(script_guid, name):
    """A ScriptableObject asset's preamble. Built by concatenation rather than %-formatting
    because the YAML directives are themselves percent signs."""
    return ("%YAML 1.1\n"
            "%TAG !u! tag:unity3d.com,2011:\n"
            "--- !u!114 &11400000\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            f"  m_Name: {name}\n"
            "  m_EditorClassIdentifier: \n")


def meta(guid, kind="asset"):
    if kind == "script":
        return f"fileFormatVersion: 2\nguid: {guid}\n"
    if kind == "prefab":
        return (f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n"
                "  externalObjects: {}\n  userData: \n  assetBundleName: \n"
                "  assetBundleVariant: \n")
    return (f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n")


def palette_lines(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in guids)


def flora_asset(f):
    name = f"Garland {f.name} Flora Config Data"
    return head(SCRIPT_GUID["flora_config"], name) + f"""  FloraPrefab: {{fileID: {FLORA_COMPONENT_FID}, guid: {f.prefab_guid}, type: 3}}
  NetworkSynced: 0
  SpawnProbability: 1
  InitialSpawnCount: {f.floor}
  OverrideDefaultPlantPeriod: 0
  NewPlantPeriod: 9999999
  PopulationSize: {f.floor}
  MaxLivePopulation: {f.cap}
  GrowthPerOffspring: {int(round(max(f.budgets) * 0.35))}
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 5
  MaturityFraction: 0.5
  OffspringSpread: 90
  PreferredSites: 0
  Element: 0
  Variant:
    Enabled: 0
  SpreadElements: 1
  ElementPalette:
{palette_lines(f.palette)}  PlantRadiusCellFractionMaxOverride: {f.band_max}
  PlantRadiusCellFractionMinOverride: {f.band_min}
  MaxTotalSpawnedObjectsOverride: -1
"""


def fauna_asset(f):
    name = f"Garland {f.name} Fauna Config Data"
    return head(SCRIPT_GUID["fauna_config"], name) + f"""  FaunaPrefab: {{fileID: {f.prefab_fid}, guid: {f.prefab_guid}, type: 3}}
  InitialSpawnCount: {f.floor}
  PopulationSize: {f.floor}
  SpawnProbability: 1
  NetworkSynced: 0
  FeedsPerOffspring: 3
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 20
  MaxLivePopulation: {f.cap}
  ReleaseTier: 0
  BandInnerRadius: 0
  BandOuterRadius: 0
  CenterFocusBias: 0
  Element: 0
  Variant:
    Enabled: 0
  SpreadElements: 1
  ElementPalette:
{palette_lines(f.palette)}"""


def profile_asset():
    floras = "".join(f"  - {{fileID: 11400000, guid: {GUID[f.name]}, type: 2}}\n" for f in FLORA)
    faunas = "".join(f"  - {{fileID: 11400000, guid: {GUID[f.name]}, type: 2}}\n" for f in FAUNA)
    return head(SCRIPT_GUID["spawn_profile"], "Garland Cell Spawn Profile") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 12000
  FloraInitialDelaySeconds: 3
  FloraSpawnIntervalSeconds: 0.5
  FloraPopulationScale: 1
  FloraPlantBudgetScale: 1
  FloraPrismScale: 1
  SupportedFloras:
{floras}  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 20
  InitialFaunaReleaseTier: 2147483647
  FaunaSpawnVolumeThreshold: 1
  FaunaPopulationScale: 1
  BaseFaunaSpawnTime: 30
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 2000
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0
  HerbivoreSpawnPointCount: 3
  HerbivoreSpawnRadius: 560
  PredatorSpawnPointCount: 2
  PredatorSpawnRadius: 760
  SupportedFaunas:
{faunas}"""


def config_asset(ladder):
    mf, mg = REF["membrane"]
    nf, ng = REF["nucleus"]
    cf, cg = REF["cytoplasm"]
    kf, kg = REF["modifier"]
    inf, ing = REF["icon"]
    return head(SCRIPT_GUID["cell_config"], "Garland Cell Config") + f"""  CellName: Garland
  Description: A flowering bough wound twice around the seed of the world
  Icon: {{fileID: {inf}, guid: {ing}, type: 3}}
  Difficulty: 1
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {mf}, guid: {mg}, type: 3}}
  NucleusPrefab: {{fileID: {nf}, guid: {ng}, type: 3}}
  CytoplasmPrefab: {{fileID: {cf}, guid: {cg}, type: 3}}
  CellModifiers:
  - {{fileID: {kf}, guid: {kg}, type: 3}}
  BootDefault: 1
  SpawnProfile: {{fileID: 11400000, guid: {GUID['profile']}, type: 2}}
  EnvironmentPrefab: {{fileID: 5260000000000103, guid: {GUID['prefab']}, type: 3}}
  EnvironmentIntensity: 1
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {ladder['RestlessEnter']}
    RestlessExit: {ladder['RestlessExit']}
    FrenzyEnter: {ladder['FrenzyEnter']}
    FrenzyExit: {ladder['FrenzyExit']}
    RestlessEnterVolume: {ladder['RestlessEnterVolume']}
    RestlessExitVolume: {ladder['RestlessExitVolume']}
    FrenzyEnterVolume: {ladder['FrenzyEnterVolume']}
    FrenzyExitVolume: {ladder['FrenzyExitVolume']}
"""


def prefab_asset():
    pf, pg = REF["prism"]
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5260000000000101
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5260000000000102}}
  - component: {{fileID: 5260000000000103}}
  m_Layer: 0
  m_Name: SpawnableGarland
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5260000000000102
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000101}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5260000000000103
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000101}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID['script']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  seed: 73
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: 1
  prism: {{fileID: {pf}, guid: {pg}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
"""


def planned_files(ladder):
    files = {
        SOURCE + ".meta": meta(GUID["script"], "script"),
        PREFAB: prefab_asset(),
        PREFAB + ".meta": meta(GUID["prefab"], "prefab"),
        os.path.join(CELL_DIR, "Garland Cell Config.asset"): config_asset(ladder),
        os.path.join(CELL_DIR, "Garland Cell Config.asset.meta"): meta(GUID["config"]),
        os.path.join(CELL_DIR, "Garland Cell Spawn Profile.asset"): profile_asset(),
        os.path.join(CELL_DIR, "Garland Cell Spawn Profile.asset.meta"): meta(GUID["profile"]),
    }
    for f in FLORA:
        base = os.path.join(CELL_DIR, f"Garland {f.name} Flora Config Data.asset")
        files[base] = flora_asset(f)
        files[base + ".meta"] = meta(GUID[f.name])
    for f in FAUNA:
        base = os.path.join(CELL_DIR, f"Garland {f.name} Fauna Config Data.asset")
        files[base] = fauna_asset(f)
        files[base + ".meta"] = meta(GUID[f.name])
    return files


# ─────────────────────────────────────────────────────────────────────────────
# 5. Report + entry point
# ─────────────────────────────────────────────────────────────────────────────

def report(env, roster):
    print("\n═══ GARLAND ─ the environment as laid ═══")
    print(f"{'family':<16}{'count':>8}{'share':>8}{'volume':>14}{'vol/prism':>11}")
    for fam, (c, v) in env["families"].items():
        print(f"{fam:<16}{c:>8}{100*c/env['count']:>7.1f}%{v:>14,.0f}{v/c:>11,.0f}")
    print(f"{'TOTAL':<16}{env['count']:>8}{100.0:>7.1f}%{env['volume']:>14,.0f}"
          f"{env['volume']/env['count']:>11,.0f}")
    print(f"\narmoured (inedible) prisms    : {env['kinds'].get('SuperShielded', 0)} "
          f"super-shielded, {env['kinds'].get('Shielded', 0)} shielded  (budget 80)")
    print( "                                (costs NO collider - a shield swaps the mesh and the"
           " mass only)")
    print(f"danger prisms                 : {env['kinds'].get('Danger', 0)}  "
          f"(zero by design - the autopilot flies this world)")
    print(f"prism axis range              : {env['min_axis']:.2f} .. {env['max_axis']:.2f}  "
          f"(prefab window {PRISM_MIN_AXIS} .. {PRISM_MAX_AXIS})")
    print(f"nearest prism corner          : {env['nearest']:.1f}  "
          f"(nucleus {NUCLEUS_R:.0f} + margin {NUCLEUS_MARGIN:.0f} = {NUCLEUS_R+NUCLEUS_MARGIN:.0f})")
    print(f"farthest prism corner         : {env['farthest']:.1f}  (membrane {MEMBRANE_R:.0f})")
    print("per-domain volume             : " + ", ".join(
        f"{d} {v:,.0f}" for d, v in env["per_domain"].most_common()))

    print("\n═══ what it GROWS ═══")
    print(f"{'species':<14}{'floor':>7}{'cap':>6}{'budget/plant':>14}{'prisms at cap':>15}")
    for f in FLORA:
        print(f"{f.name:<14}{f.floor:>7}{f.cap:>6}{max(f.budgets):>14}{f.cap*max(f.budgets):>15}")
    for f in FAUNA:
        print(f"{f.name:<14}{f.floor:>7}{f.cap:>6}{CALIBRATION['fauna_prisms_each']:>14}"
              f"{f.cap*CALIBRATION['fauna_prisms_each']:>15}")
    print(f"\nflora at cap   : {roster['flora_cap_count']:,} prisms "
          f"(~{roster['flora_cap_vol']:,.0f} volume, ESTIMATED - see CALIBRATION)")
    print(f"fauna + bones  : {roster['fauna_count']:,} prisms (~{roster['fauna_vol']:,.0f} volume)")
    print(f"MATURE CELL    : {roster['mature_count']:,} prisms, ~{roster['mature_vol']:,.0f} volume")
    print(f"always-on heart colliders at cap: {roster['heart_colliders']} "
          f"(one per live lifeform - Blob 171, Lattice 1,080)")

    L = roster["ladder"]
    print("\n═══ the ladder ═══")
    print(f"  Restless  volume {L['RestlessEnterVolume']:>12,} / exit {L['RestlessExitVolume']:>12,}"
          f"   count {L['RestlessEnter']:>7,} / {L['RestlessExit']:>7,}")
    print(f"  Frenzy    volume {L['FrenzyEnterVolume']:>12,} / exit {L['FrenzyExitVolume']:>12,}"
          f"   count {L['FrenzyEnter']:>7,} / {L['FrenzyExit']:>7,}")
    print("  (Restless is VOLUME-only - CellPhaseRules.Compute never reads the Restless counts;\n"
          "   the count pair is authored for consistency and for the zero-volume derivation path.)")


def main(argv):
    if "--self-test" in argv:
        return self_test()

    env = measure(build_environment())
    roster = roster_model(env)
    ladder = roster["ladder"]

    problems = []
    check_source_constants(problems)
    check_shim_fidelity(problems)
    check_against_harness(env, problems)
    check_invariants(env, roster, problems)

    files = planned_files(ladder)
    check_serialized_keys(files, problems)

    report(env, roster)

    if problems:
        print("\nFAILED:")
        for p in problems:
            print("  -", p)
        return 1

    if "--report" in argv:
        print("\n--report: wrote nothing.")
        return 0

    if "--check" in argv:
        drift = []
        for path, want in files.items():
            if not os.path.exists(path):
                drift.append(f"missing {os.path.relpath(path, REPO)}")
            elif open(path, encoding="utf-8").read() != want:
                drift.append(f"drifted {os.path.relpath(path, REPO)}")
        if drift:
            print("\nFAILED - the assets on disk are not what this model authors:")
            for d in drift:
                print("  -", d)
            return 1
        print(f"\n--check OK: {len(files)} files match the model.")
        return 0

    os.makedirs(CELL_DIR, exist_ok=True)
    os.makedirs(os.path.dirname(PREFAB), exist_ok=True)
    for path, text in files.items():
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(text)
    print(f"\nwrote {len(files)} files.")
    print("Still to do by hand (both are one-line edits this script deliberately does not make,\n"
          "because they live in files it does not own):\n"
          "  1. Menu_Main.unity - add the config to the freestyle Cell's CellConfigs list.\n"
          "  2. author_flora_populations.py - OWNED_ELSEWHERE['Garland '] = this script.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
