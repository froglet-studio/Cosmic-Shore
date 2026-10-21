#!/usr/bin/env python3
"""
Analytic prism budget for the Boneyard - the Dog Fight arena.

This is the model of `SpawnableBoneyard.cs`, and it is a MIRROR, not an estimate: the
generator (`author_dogfight_assets.py`) imports it, so the arena's PhaseThresholds are
derived from the same numbers that build the arena and the two cannot drift apart. That
mirroring is only possible because the C# was written to make it possible:

  * every per-unit prism count is a `const` (never randomised) - randomness in that file
    moves things around, it never changes how many there are;
  * the only count-affecting rejections are DETERMINISTIC (the reactor's blow-out wedge,
    the overpass's collapsed span), and both are reproduced exactly below;
  * `spawnClearRadius` is authored 0 on every Boneyard prefab, so `Emit`'s clearance
    rejection - the one genuinely awkward term - never fires. Players spawn on a shell
    well outside the wreck field and fly in, so there is nothing to clear.

`Hash01` is ported verbatim from `CellEnvironmentSpawnableBase` because the overpass gap
width is drawn from it. If either implementation changes, both must.

The VOLUME is the laid volume, not an estimate either: since the flush pass (no prism may
interpenetrate another) a prism's size depends on its neighbours, so "THE LAID SIZES" below
walks the structures, the value noise and the seeded draws the C# uses and sizes every prism as
it does - agreeing with the clip audit's measured volume to float rounding. A change to any
size, any draw, or the build order in SpawnableBoneyard.cs must land there too.

Run it directly to print the table that belongs in DOGFIGHT.md:

    python3 Tools/Build/boneyard_budget.py

CONFIRM IN EDITOR before trusting the thresholds: FrogletTools > Ecology >
Measure Cell Environment Baselines. If the measurer disagrees with this table, the C# and
this file have drifted - fix both, do not paper over it in the asset.
"""

import math

# ── Geometry constants: must match SpawnableBoneyard.cs ──────────────────────

ARENA_RADIUS = 520.0
CRUST_DEPTH = -200.0

# ── Scatter ─────────────────────────────────────────────────────────────────
# These do NOT affect the prism count - they only move structures around - so they play no part
# in the count below (the overpass decks' sizes read the field anchors, see "THE LAID SIZES"). They live here anyway because the generator authors them onto the prefab
# variants, and a tuning value that exists in two files drifts. Same reasoning as the geometry
# constants above: one place, imported by whoever needs it.
DEBRIS_FIELDS = 7
FIELD_RADIUS_FRACTION = 0.2
CORE_CLEAR_RADIUS = 120.0
DRIFT_FRACTION = 0.4
DRIFT_HEIGHT = 300.0

CRUST_PLATES = 3000       # before density
RUBBLE_CHUNKS = 2400      # before density
ASH_MOTES = 1700          # before density

HULK_RIBS = 10
HULK_RIB_SEGMENTS = 22
HULK_STATIONS = 34
HULK_PLATE_ARC = 11

SPIRE_SEGMENTS = 26
SPIRE_RING = 8
SPIRE_BEACONS = 1         # one shielded tip per spire

FRAME_EDGES = 12
FRAME_EDGE_PRISMS = 20
FRAME_BRACES = 6
FRAME_BRACE_PRISMS = 16

OVERPASS_DECK = 64
OVERPASS_RAILS = 2

REACTOR_RING = 24
REACTOR_SHELL_RIBS = 10
REACTOR_SHELL_PRISMS = 26

# The reactor shell's blow-out: prisms whose angle falls in this window are skipped.
REACTOR_GAP_LO = 0.35
REACTOR_GAP_HI = 1.92

# The four shipped intensities. Structure counts are the intensity dial; the arena RADIUS
# deliberately never changes (see the C# summary for why).
#   (density, hulks, spires, frames, overpasses)
#
# SPREAD, not shape. All four fly the same Boneyard - the playtest read was that the ARENA is
# right and the LEVELS were too close together (intensity 1 -> 4 spanned only 1.9x, so picking a
# level barely changed the match). The ladder below spans 3.1x on the same recipe: intensity 1 is
# a genuinely open field where a fleeing Sparrow has few places to break line of sight, and
# intensity 4 is a dense wreck maze. Intensity 2 is left EXACTLY where it was, so the level the
# arena was tuned at is untouched and the others move around it.
#
# The top end stays well inside what this mode can afford: ~34k prisms is the same order as the
# freestyle cell environments (34-41k), not the ~69k of Scurry's Atlantis, and Dog Fight adds
# four Sparrows' projectile + AOE traffic on top of whatever the arena costs.
INTENSITIES = [
    (0.55,  4,  6,  3,  2),
    (0.90,  8, 12,  5,  4),
    (1.30, 13, 19,  8,  7),
    (1.75, 19, 27, 11, 10),
]

SEED = 41  # SpawnableBoneyard.DefaultSeed

# ── Per-prism volume, family by family ───────────────────────────────────────
# (x, y, z, jitter-amount): each family's AUTHORED size, before the flush pass trims it to its
# neighbours. The volume this file prices is no longer nominal x E[k^3] - see "THE LAID SIZES"
# below, which sizes every prism as the C# does - so these are the reference the trim is measured
# against (unit_volume), not the price. `Jit(s, amt)` in CellEnvironmentSpawnableBase draws ONE
# factor k = 1 + U(-amt, amt) and scales all three axes by it, so an untrimmed prism's volume
# scales by k^3 and
#   E[k^3] = 1 + 3E[u] + 3E[u^2] + E[u^3] = 1 + amt^2
# for u ~ U(-amt, amt). amt = 0 means the family emits its authored scale verbatim.
#
# (The corresponding constant in wildlife_cage_budget.py integrates k^3 over the jitter range
# but divides by half the width, so it lands at exactly 2x the true expectation. Not corrected
# there - Wildlife Liberation's thresholds were tuned against that model and moving it would
# silently retune a shipped mode - but this file uses the correct factor.)
FAMILY_SCALE = {
    "crust":           (9.0, 8.4, 1.4, 0.35),
    "hulk_rib":        (3.4, 5.6, 2.2, 0.20),
    "hulk_plate":      (8.6, 8.0, 1.5, 0.25),
    "spire_body":      (3.6, 5.4, 2.0, 0.25),
    "spire_beacon":    (4.0, 4.0, 4.0, 0.0),
    "girder":          (2.6, 2.6, 4.4, 0.0),
    "overpass_deck":  (11.0, 7.0, 1.8, 0.0),
    "overpass_rail":   (1.6, 3.2, 3.6, 0.0),
    "rubble":          (3.6, 3.0, 5.2, 0.45),
    "reactor_ring":    (5.0, 8.0, 4.0, 0.0),
    "reactor_shell":   (4.4, 5.8, 1.8, 0.20),
    "ash":             (1.1, 1.1, 2.2, 0.0),
}


def unit_volume(family: str) -> float:
    x, y, z, amt = FAMILY_SCALE[family]
    return x * y * z * (1.0 + amt * amt)


def hash01(n: int) -> float:
    """Verbatim port of CellEnvironmentSpawnableBase.Hash01 (unchecked uint arithmetic)."""
    m = 0xFFFFFFFF
    h = n & m
    h = (h ^ 61) ^ (h >> 16)
    h = (h * 9) & m
    h ^= h >> 4
    h = (h * 0x27D4EB2D) & m
    h ^= h >> 15
    return (h & 0xFFFFFF) / float(0x1000000)


def scaled(n: int, density: float) -> int:
    """Mirror of Mathf.Max(1, Mathf.RoundToInt(n * density)).

    Unity's RoundToInt is banker's rounding (round half to even), which is also Python's
    round() for floats - so the two agree without special-casing.
    """
    return max(1, int(round(n * density)))


def crust(density: float) -> int:
    return scaled(CRUST_PLATES, density)


def rubble(density: float) -> int:
    return scaled(RUBBLE_CHUNKS, density)


def ash(density: float) -> int:
    return scaled(ASH_MOTES, density)


def hulks(count: int) -> tuple:
    """(total, danger) - danger rides the two END ribs only, at a 0.22 hash gate."""
    ribs = count * HULK_RIBS * HULK_RIB_SEGMENTS
    plating = count * HULK_STATIONS * HULK_PLATE_ARC

    danger = 0
    for k in range(count):
        for rib in (0, HULK_RIBS - 1):
            for s in range(HULK_RIB_SEGMENTS):
                if hash01(k * 97 + rib * 31 + s) < 0.22:
                    danger += 1
    return ribs + plating, danger


def spires(count: int) -> tuple:
    """(total, shielded)."""
    body = count * SPIRE_SEGMENTS * SPIRE_RING
    return body + count * SPIRE_BEACONS, count * SPIRE_BEACONS


def frames(count: int) -> int:
    return count * (FRAME_EDGES * FRAME_EDGE_PRISMS + FRAME_BRACES * FRAME_BRACE_PRISMS)


def overpasses(count: int) -> int:
    """The collapsed span is a per-overpass hash draw, so this walks the real loop."""
    total = 0
    for k in range(count):
        gap_half = 0.10 + 0.09 * hash01(k * 73 + 8)
        for i in range(OVERPASS_DECK):
            t = i / float(OVERPASS_DECK - 1)
            if abs(t - 0.5) < gap_half:
                continue
            total += 1 + OVERPASS_RAILS
    return total


def reactor() -> tuple:
    """(total, super_shielded, danger)."""
    total = REACTOR_RING
    danger = 0
    for rib in range(REACTOR_SHELL_RIBS):
        for s in range(REACTOR_SHELL_PRISMS):
            ang = 2.0 * math.pi * s / REACTOR_SHELL_PRISMS
            if REACTOR_GAP_LO < ang < REACTOR_GAP_HI:
                continue
            total += 1
            hot = rib in (REACTOR_SHELL_RIBS // 2, REACTOR_SHELL_RIBS // 2 - 1)
            if hot and hash01(rib * 51 + s * 7) < 0.3:
                danger += 1
    return total, REACTOR_RING, danger


# ── THE LAID SIZES (the flush pass) ──────────────────────────────────────────
# NO PRISM MAY INTERPENETRATE ANOTHER (Docs/ECOSYSTEM.md §49.10), held in SpawnableBoneyard.cs at
# authoring time: every structure sizes its prisms from the geometry it already has (the wedge
# of a ring, the step to the next station, the joint a girder meets, a crust plate's phyllotaxis
# neighbours). A size that depends on a neighbour, a hull's dent or the crust's buckling is not a
# nominal-times-E[k^3] quantity any more, so the volume below is the LAID volume: this section
# walks the same structures the C# walks, reproduces its value noise (PaintingStrokeToolkit) and
# its draws (System.Random(41), .NET's seeded generator - which Unity's Mono shares - consumed in
# the C#'s order: the crust's plates, each hulk's ribs then plates, each spire's rings, the
# rubble, the reactor shell), and sizes every prism exactly as the C# does. It agrees with the
# measured volume (Tools/Build/audit_prism_clipping.py --json) to float rounding.
#
# Positions are only computed where a size depends on them (the crust, the overpass decks).

import struct as _struct

_F32 = _struct.Struct("<f")


def _f32(x):
    return _F32.unpack(_F32.pack(x))[0]


class NetRandom:
    """.NET's seeded System.Random (Knuth's subtractive generator; Net5CompatSeedImpl in .NET 8 and
    the implementation Unity's Mono ships), bit for bit."""
    MBIG = 2147483647
    MSEED = 161803398

    def __init__(self, seed):
        sa = [0] * 56
        mj = self.MSEED - (self.MBIG if seed == -2 ** 31 else abs(seed))
        sa[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            sa[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = sa[ii]
        for _ in range(4):
            for i in range(1, 56):
                sa[i] -= sa[1 + (i + 30) % 55]
                if sa[i] < 0:
                    sa[i] += self.MBIG
        self._sa, self._i, self._ip = sa, 0, 21

    def next_double(self):
        self._i = 1 if self._i + 1 >= 56 else self._i + 1
        self._ip = 1 if self._ip + 1 >= 56 else self._ip + 1
        r = self._sa[self._i] - self._sa[self._ip]
        if r == self.MBIG:
            r -= 1
        if r < 0:
            r += self.MBIG
        self._sa[self._i] = r
        return r * (1.0 / self.MBIG)

    def jit(self, amt):
        """The factor Jit(s, amt) draws: 1f + RangeF(-amt, amt)."""
        return _f32(1.0 + _f32(self.next_double() * (2 * amt) - amt))


def _hash_noise(x, y, z, seed):
    """PaintingStrokeToolkit.Hash: integer hash -> [-1, 1)."""
    m = 0xFFFFFFFF
    h = (x * 374761393 + y * 668265263 + z * 2147483647 + seed * 971) & m
    h = ((h ^ (h >> 13)) * 1274126177) & m
    h ^= h >> 16
    return (h & 0xFFFFFF) / float(0x800000) - 1.0


def _fade(t):
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def _lerp(a, b, t):
    return a + (b - a) * t


def value_noise(x, y, z, seed):
    """PaintingStrokeToolkit.ValueNoise: trilinear value noise with a smootherstep fade."""
    x0, y0, z0 = math.floor(x), math.floor(y), math.floor(z)
    fx, fy, fz = _fade(x - x0), _fade(y - y0), _fade(z - z0)
    c = {}
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                c[dx, dy, dz] = _hash_noise(x0 + dx, y0 + dy, z0 + dz, seed)
    x00 = _lerp(c[0, 0, 0], c[1, 0, 0], fx)
    x10 = _lerp(c[0, 1, 0], c[1, 1, 0], fx)
    x01 = _lerp(c[0, 0, 1], c[1, 0, 1], fx)
    x11 = _lerp(c[0, 1, 1], c[1, 1, 1], fx)
    return _lerp(_lerp(x00, x10, fy), _lerp(x01, x11, fy), fz)


def n01(x, y, z, offset):
    """CellEnvironmentSpawnableBase.N01 at the Boneyard's seed."""
    return 0.5 * (value_noise(x, y, z, SEED + offset) + 1.0)


GOLDEN_ANGLE = 2.39996323
FLUSH_MARGIN = 0.995


def _v_add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _v_sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _v_mul(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def _v_dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _v_cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _v_len(a):
    return math.sqrt(_v_dot(a, a))


def _v_norm(a):
    m = _v_len(a)
    return (a[0] / m, a[1] / m, a[2] / m)


def _look_axes(forward, up):
    """(x, y, z) of Quaternion.LookRotation(forward, up) - z along forward, x = up x z."""
    z = _v_norm(forward)
    x = _v_cross(up, z)
    if _v_dot(x, x) < 1e-12:
        x = (1.0, 0.0, 0.0)
    x = _v_norm(x)
    return x, _v_cross(z, x), z


def crust_y(r):
    t = min(1.0, max(0.0, r / ARENA_RADIUS))
    return CRUST_DEPTH + t * t * 130.0


def crust_surface(x, z):
    coarse = (n01(x * 0.0032, 0.0, z * 0.0032, 3) - 0.5) * 220.0
    fine = (n01(x * 0.011, 17.0, z * 0.011, 8) - 0.5) * 46.0
    return crust_y(math.hypot(x, z)) + coarse + fine


def scatter_planar(index, salt):
    fields = max(1, DEBRIS_FIELDS)
    field = (index * 3 + salt) % fields
    u = (field + 0.5) / fields
    r_in = min(CORE_CLEAR_RADIUS, ARENA_RADIUS * 0.5)
    r_out = ARENA_RADIUS * 0.92
    anchor_r = math.sqrt(r_in * r_in + u * (r_out * r_out - r_in * r_in))
    anchor_a = field * GOLDEN_ANGLE + hash01(field * 191 + SEED) * 1.4
    field_r = ARENA_RADIUS * FIELD_RADIUS_FRACTION
    off_r = field_r * math.sqrt(hash01(index * 53 + salt * 7 + 1))
    off_a = hash01(index * 67 + salt * 11 + 2) * math.pi * 2.0
    x = anchor_r * math.cos(anchor_a) + off_r * math.cos(off_a)
    z = anchor_r * math.sin(anchor_a) + off_r * math.sin(off_a)
    d = math.hypot(x, z)
    if d < 0.001:
        return CORE_CLEAR_RADIUS, 0.0
    c = min(max(d, CORE_CLEAR_RADIUS), ARENA_RADIUS * 0.97)
    return x * c / d, z * c / d


def ring_width(radius, half_thick, half_step):
    """SpawnableBoneyard.RingWidth: a ring prism keeps to its own wedge."""
    return 2.0 * max(0.0, radius - half_thick) * math.tan(half_step) * FLUSH_MARGIN


def _floor(v):
    return max(0.5, v)


def _box_volume(x, y, z):
    return _f32(x) * _f32(y) * _f32(z)


# The crust's neighbours (SpawnableBoneyard.CrustNeighbours) and a plate's authored size.
CRUST_NEIGHBOURS = (1, 2, 3, 5, 8, 13, 21, 34, 55, 76, 89, 110, 144, 199, 233, 288, 377, 466, 610)
CRUST_PLATE = (9.0, 8.4, 1.4)
CRUST_JITTER = 0.35


def laid_crust(density, rng):
    n = crust(density)
    pos, axes, size = [], [], []
    for i in range(n):
        t = _f32((i + 0.5) / n)
        r = ARENA_RADIUS * math.sqrt(t)
        # The C# angle is a float: at i ~ 5000 its rounding moves a plate by ~0.5u, so it is
        # rounded the same way here.
        a = _f32(i * _f32(GOLDEN_ANGLE))
        x, z = r * math.cos(a), r * math.sin(a)
        wx = (n01(x * 0.0026, 5.0, z * 0.0026, 21) - 0.5) * 190.0
        wz = (n01(x * 0.0026, 91.0, z * 0.0026, 22) - 0.5) * 190.0
        x, z = x + wx, z + wz
        y = crust_surface(x, z)
        e = 9.0
        dx = crust_surface(x + e, z) - y
        dz = crust_surface(x, z + e) - y
        normal = _v_norm((-dx, e, -dz))
        along = (-math.sin(a), 0.0, math.cos(a))
        pos.append((x, y, z))
        axes.append(_look_axes(normal, along))
        k = rng.jit(CRUST_JITTER)
        size.append(tuple(_floor(c * k) for c in CRUST_PLATE))

    def reach(j, u, foot_only=False):
        X, Y, Z = axes[j]
        sx, sy, sz = size[j]
        f = 0.5 * (sx * abs(_v_dot(X, u)) + sy * abs(_v_dot(Y, u)))
        return f if foot_only else f + 0.5 * sz * abs(_v_dot(Z, u))

    vol = 0.0
    for i in range(n):
        si = size[i]
        diag_i = _v_len(si)
        scale = 1.0
        for step in CRUST_NEIGHBOURS:
            for j in (i - step, i + step):
                if j < 0 or j >= n:
                    continue
                d = _v_sub(pos[j], pos[i])
                dist = _v_len(d)
                gap = dist * FLUSH_MARGIN
                if gap >= 0.5 * (diag_i + _v_len(size[j])):
                    continue
                u = _v_mul(d, 1.0 / dist)
                foot = reach(i, u, foot_only=True)
                thick = 0.5 * si[2] * abs(_v_dot(axes[i][2], u))
                rj = reach(j, u)
                if foot + thick + rj <= gap:
                    continue
                room = max(0.5 * gap, gap - rj)
                if foot > 1e-5:
                    scale = min(scale, min(1.0, max(0.0, (room - thick) / foot)))
        vol += _box_volume(_floor(si[0] * scale), _floor(si[1] * scale), si[2])
    return vol


def _hulk_taper(t):
    return 0.72 + 0.28 * math.sin(t * math.pi)


RIB_HALF_ALONG_MAX = 3.4 * 1.2 * 0.5
RIB_HALF_AROUND_MAX = 5.6 * 1.2 * 0.5
RIB_HALF_THICK_MAX = 2.2 * 1.2 * 0.5
PLATE_HALF_THICK_MAX = 1.5 * 1.25 * 0.5
HULK_DENT_MIN = 0.94


def hulk_rib_radius(t, length, radius):
    station_step = length / (HULK_STATIONS - 1)
    reach = (RIB_HALF_ALONG_MAX + 0.5 * station_step) / length
    rr_plate = radius * min(_hulk_taper(min(1.0, max(0.0, t - reach))),
                            _hulk_taper(min(1.0, max(0.0, t + reach))))
    inner = (rr_plate * HULK_DENT_MIN - PLATE_HALF_THICK_MAX) * FLUSH_MARGIN
    return math.sqrt(max(0.0, inner * inner - RIB_HALF_AROUND_MAX ** 2)) - RIB_HALF_THICK_MAX


def laid_hulks(count, rng):
    vol = 0.0
    for k in range(count):
        length = 190.0 + 150.0 * hash01(k * 17 + 5)
        radius = 30.0 + 18.0 * hash01(k * 23 + 11)
        for rib in range(HULK_RIBS):
            t = 0.5 if HULK_RIBS == 1 else rib / (HULK_RIBS - 1)
            rr = hulk_rib_radius(t, length, radius)
            for _ in range(HULK_RIB_SEGMENTS):
                kk = rng.jit(0.2)
                sx, sy, sz = _floor(3.4 * kk), _floor(5.6 * kk), _floor(2.2 * kk)
                sy = min(sy, ring_width(rr, 0.5 * sz, math.pi / HULK_RIB_SEGMENTS))
                vol += _box_volume(sx, sy, sz)
        plate_k = [rng.jit(0.25) for _ in range(HULK_STATIONS * HULK_PLATE_ARC)]
        room = length / (HULK_STATIONS - 1) * FLUSH_MARGIN

        def axial(q):
            return _floor(8.0 * plate_k[q])

        for st in range(HULK_STATIONS):
            t = st / (HULK_STATIONS - 1)
            rr = radius * _hulk_taper(t)
            for s_ in range(HULK_PLATE_ARC):
                dent = 1.0 + 0.06 * (n01(st * 0.4, s_ * 0.7, k * 3.1, 11) - 0.5) * 2.0
                q = st * HULK_PLATE_ARC + s_
                kq = plate_k[q]
                sx, sy, sz = _floor(8.6 * kq), axial(q), _floor(1.5 * kq)
                if st > 0:
                    sy = min(sy, 2.0 * max(0.5 * room, room - 0.5 * axial(q - HULK_PLATE_ARC)))
                if st < HULK_STATIONS - 1:
                    sy = min(sy, 2.0 * max(0.5 * room, room - 0.5 * axial(q + HULK_PLATE_ARC)))
                sx = min(sx, ring_width(rr * dent, 0.5 * sz, 0.5 * math.pi / HULK_PLATE_ARC))
                vol += _box_volume(sx, sy, sz)
    return vol


def laid_spires(count, rng):
    vol = 0.0
    for k in range(count):
        height = 140.0 + 210.0 * hash01(k * 29 + 9)
        base_r = 11.0 + 9.0 * hash01(k * 37 + 4)
        ring_step = height / (SPIRE_SEGMENTS - 1) * FLUSH_MARGIN
        for seg in range(SPIRE_SEGMENTS):
            t = seg / (SPIRE_SEGMENTS - 1)
            rr = base_r * (1.0 - 0.62 * t)
            for _ in range(SPIRE_RING):
                kk = rng.jit(0.25)
                sx, sy, sz = _floor(3.6 * kk), _floor(5.4 * kk), _floor(2.0 * kk)
                sx = min(sx, ring_width(rr, 0.5 * sz, math.pi / SPIRE_RING))
                sy = min(sy, ring_step)
                vol += _box_volume(sx, sy, sz)
        vol += 4.0 ** 3   # the shielded beacon
    return vol


GIRDER_SECTION = 2.6
GIRDER_LENGTH = 4.4
GIRDER_REACH = GIRDER_SECTION * 0.5 * 1.41421356
GIRDER_CORNER_CLEAR = GIRDER_REACH * 1.01
GIRDER_END_MIN = 0.6


def _girder_volume(length, count, clear_from, clear_to, mid_scale):
    """SpawnableBoneyard.EmitGirder's sizes (its positions price nothing)."""
    step = length / max(1, count - 1)
    run = min(GIRDER_LENGTH, step * FLUSH_MARGIN)
    next_from = min(run, 2.0 * (step - clear_from - GIRDER_END_MIN)) if clear_from > 0 else run
    next_to = min(run, 2.0 * (step - clear_to - GIRDER_END_MIN)) if clear_to > 0 else run
    vol = 0.0
    for i in range(count):
        ln = run
        if i == 1:
            ln = min(ln, next_from)
        if i == count - 2:
            ln = min(ln, next_to)
        if i == 0 and clear_from > 0:
            ln = min(GIRDER_LENGTH, (step - 0.5 * next_from - clear_from) * FLUSH_MARGIN)
        elif i == count - 1 and clear_to > 0:
            ln = min(GIRDER_LENGTH, (step - 0.5 * next_to - clear_to) * FLUSH_MARGIN)
        kk = mid_scale if i in (count // 2 - 1, count // 2) else 1.0
        vol += _box_volume(GIRDER_SECTION * kk, GIRDER_SECTION * kk, ln * kk)
    return vol


def _brace_run(half, b):
    u = (b + 0.5) / FRAME_BRACES
    start = (-half[0], _lerp(-half[1], half[1], u), -half[2])
    end = (half[0], _lerp(half[1], -half[1], u), half[2] if b % 2 == 0 else -half[2])
    return start, _v_sub(end, start)


def brace_cross_scale(half, parity):
    scale = 1.0
    for i in range(parity, FRAME_BRACES, 2):
        _, di = _brace_run(half, i)
        for j in range(i + 2, FRAME_BRACES, 2):
            _, dj = _brace_run(half, j)
            a = 0.5 * min(_v_len(di), _v_len(dj)) / (FRAME_BRACE_PRISMS - 1)
            cos = min(1.0, max(-1.0, _v_dot(_v_norm(di), _v_norm(dj))))
            sin_half = math.sqrt(0.5 * (1.0 - cos))
            scale = min(scale, FLUSH_MARGIN * a * sin_half /
                        (GIRDER_REACH + 0.5 * GIRDER_LENGTH * sin_half))
    return scale


def laid_frames(count):
    vol = 0.0
    for k in range(count):
        half = (52.0 + 40.0 * hash01(k * 13 + 1), 38.0 + 46.0 * hash01(k * 17 + 2),
                52.0 + 40.0 * hash01(k * 19 + 3))
        for e in range(FRAME_EDGES):
            axis = e // 4
            clear = 0.0 if axis == 0 else GIRDER_CORNER_CLEAR
            vol += _girder_volume(half[axis] * 2.0, FRAME_EDGE_PRISMS, clear, clear, 1.0)
        scales = (brace_cross_scale(half, 0), brace_cross_scale(half, 1))
        for b in range(FRAME_BRACES):
            _, delta = _brace_run(half, b)
            d = _v_norm(delta)
            sin_to_edge = math.sqrt(max(1e-4, 1.0 - d[1] * d[1]))
            clear = 2.0 * GIRDER_REACH / sin_to_edge * 1.01
            vol += _girder_volume(_v_len(delta), FRAME_BRACE_PRISMS, clear, clear, scales[b % 2])
    return vol


def laid_overpasses(count):
    vol = 0.0
    for k in range(count):
        span = 250.0 + 190.0 * hash01(k * 83 + 2)
        mx, mz = scatter_planar(k, 3)
        heading = hash01(k * 59 + 4) * math.pi * 2.0
        along = (math.cos(heading), 0.0, math.sin(heading))
        across = _v_norm(_v_cross((0.0, 1.0, 0.0), along))
        rise = 90.0 + 90.0 * hash01(k * 67 + 6)
        gap_half = 0.10 + 0.09 * hash01(k * 73 + 8)

        def laid(i):
            return abs(i / (OVERPASS_DECK - 1) - 0.5) >= gap_half

        def point(i):
            c = i / (OVERPASS_DECK - 1) - 0.5
            x, z = mx + along[0] * c * span, mz + along[2] * c * span
            return x, crust_surface(x, z) + rise * (1.0 - 4.0 * c * c) + 30.0, z

        for i in range(OVERPASS_DECK):
            if not laid(i):
                continue
            c = i / (OVERPASS_DECK - 1) - 0.5
            p = point(i)
            dy = -8.0 * c * rise / span
            tangent = _v_norm(_v_add(along, (0.0, dy, 0.0)))
            normal = _v_norm(_v_cross(tangent, across))
            rail_up = _look_axes(tangent, (0.0, 1.0, 0.0))[1]
            deck = [11.0, 7.0, 1.8]
            rail = [1.6, 3.2, 3.6]
            for j in (i - 1, i + 1):
                if j < 0 or j >= OVERPASS_DECK or not laid(j):
                    continue
                d = _v_sub(point(j), p)
                room = 0.5 * _v_len(d) * FLUSH_MARGIN
                u = _v_mul(d, 1.0 / _v_len(d))
                along_u = max(1e-3, abs(_v_dot(tangent, u)))
                deck[1] = min(deck[1], 2.0 * (room - 0.5 * deck[2] * abs(_v_dot(normal, u))) / along_u)
                rail[2] = min(rail[2], 2.0 * (room - 0.5 * rail[1] * abs(_v_dot(rail_up, u))) / along_u)
            vol += _box_volume(*deck) + OVERPASS_RAILS * _box_volume(*rail)
    return vol


def laid_rubble(density, rng):
    vol = 0.0
    for _ in range(rubble(density)):
        kk = rng.jit(0.45)
        vol += _box_volume(_floor(3.6 * kk), _floor(3.0 * kk), _floor(5.2 * kk))
    return vol


def laid_reactor(rng):
    vol = REACTOR_RING * 5.0 * 8.0 * 4.0
    for rib in range(REACTOR_SHELL_RIBS):
        phi = math.pi * (rib + 0.5) / REACTOR_SHELL_RIBS
        ring_r = 96.0 * math.sin(phi)
        for s_ in range(REACTOR_SHELL_PRISMS):
            ang = 2.0 * math.pi * s_ / REACTOR_SHELL_PRISMS
            if REACTOR_GAP_LO < ang < REACTOR_GAP_HI:
                continue
            kk = rng.jit(0.2)
            sx, sy, sz = _floor(4.4 * kk), _floor(5.8 * kk), _floor(1.8 * kk)
            reach = 0.5 * sx * abs(math.cos(phi)) + 0.5 * sz * math.sin(phi)
            sy = min(sy, ring_width(ring_r, reach, math.pi / REACTOR_SHELL_PRISMS))
            vol += _box_volume(sx, sy, sz)
    return vol


def laid_volume(density, hulk_count, spire_count, frame_count, overpass_count):
    """The arena's LAID volume, family by family, in the C#'s build (and draw) order."""
    rng = NetRandom(SEED)
    fam = {}
    fam["crust"] = laid_crust(density, rng)
    fam["hulks"] = laid_hulks(hulk_count, rng)
    fam["spires"] = laid_spires(spire_count, rng)
    fam["frames"] = laid_frames(frame_count)
    fam["overpasses"] = laid_overpasses(overpass_count)
    fam["rubble"] = laid_rubble(density, rng)
    fam["reactor"] = laid_reactor(rng)
    fam["ash"] = ash(density) * 1.1 * 1.1 * 2.2
    return fam


# PhaseThresholds = measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md §18).
# Same deltas every authored cell uses, so the fauna ladder sits the same distance above THIS
# arena's own baseline as it does above every other one.
BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000,
                   rev=11200, rxv=8000, fev=57600, fxv=48000)


def phase_thresholds(n: int, v: float) -> dict:
    return dict(
        RestlessEnter=n + BLOB_DELTAS["re"], RestlessExit=n + BLOB_DELTAS["rx"],
        FrenzyEnter=n + BLOB_DELTAS["fe"], FrenzyExit=n + BLOB_DELTAS["fx"],
        RestlessEnterVolume=round(v + BLOB_DELTAS["rev"]),
        RestlessExitVolume=round(v + BLOB_DELTAS["rxv"]),
        FrenzyEnterVolume=round(v + BLOB_DELTAS["fev"]),
        FrenzyExitVolume=round(v + BLOB_DELTAS["fxv"]))


def budget(density, hulk_count, spire_count, frame_count, overpass_count) -> dict:
    hulk_total, hulk_danger = hulks(hulk_count)
    spire_total, spire_shielded = spires(spire_count)
    reactor_total, reactor_super, reactor_danger = reactor()

    n_crust = crust(density)
    n_rubble = rubble(density)
    n_ash = ash(density)
    n_ribs = hulk_count * HULK_RIBS * HULK_RIB_SEGMENTS
    n_plate = hulk_count * HULK_STATIONS * HULK_PLATE_ARC
    n_spire_body = spire_count * SPIRE_SEGMENTS * SPIRE_RING
    n_girder = frames(frame_count)
    n_overpass = overpasses(overpass_count)
    # The overpass loop emits one deck prism and OVERPASS_RAILS rails per surviving station.
    n_deck = n_overpass // (1 + OVERPASS_RAILS)
    n_rail = n_overpass - n_deck
    n_shell = reactor_total - REACTOR_RING

    # The LAID volume (see "THE LAID SIZES"): every prism at the size the flush pass gives it.
    family_volume = laid_volume(density, hulk_count, spire_count, frame_count, overpass_count)
    volume = sum(family_volume.values())

    families = {
        "crust": n_crust,
        "hulks": hulk_total,
        "spires": spire_total,
        "frames": n_girder,
        "overpasses": n_overpass,
        "rubble": n_rubble,
        "reactor": reactor_total,
        "ash": n_ash,
    }
    total = sum(families.values())
    return {
        "families": families,
        "total": total,
        "volume": volume,
        "family_volume": family_volume,
        "danger": hulk_danger + reactor_danger,
        "shielded": spire_shielded,
        "super_shielded": reactor_super,
    }


def all_intensities() -> list:
    return [budget(*params) for params in INTENSITIES]


def main():
    rows = all_intensities()
    order = ["crust", "hulks", "spires", "frames", "overpasses", "rubble", "reactor", "ash"]

    header = (f"{'intensity':<10}" + "".join(f"{k:>12}" for k in order)
              + f"{'TOTAL':>10}{'volume':>12}{'danger':>9}{'shield':>8}{'super':>7}")
    print(header)
    print("-" * len(header))
    for i, (row, params) in enumerate(zip(rows, INTENSITIES), start=1):
        line = f"{i:<10}" + "".join(f"{row['families'][k]:>12,}" for k in order)
        line += (f"{row['total']:>10,}{row['volume']:>12,.0f}{row['danger']:>9,}"
                 f"{row['shielded']:>8,}{row['super_shielded']:>7,}")
        print(line)

    print()
    print("PhaseThresholds (baseline + the standard Blob deltas):")
    for i, row in enumerate(rows, start=1):
        th = phase_thresholds(row["total"], row["volume"])
        print(f"  intensity {i}: restless {th['RestlessEnter']}/{th['RestlessExit']}  "
              f"frenzy {th['FrenzyEnter']}/{th['FrenzyExit']}  "
              f"vol restless {th['RestlessEnterVolume']:,}/{th['RestlessExitVolume']:,}  "
              f"frenzy {th['FrenzyEnterVolume']:,}/{th['FrenzyExitVolume']:,}")

    print()
    print("params (density, hulks, spires, frames, overpasses):")
    for i, p in enumerate(INTENSITIES, start=1):
        print(f"  intensity {i}: {p}")

    print()
    # NOT a collider line: a shield swaps the MESH and the mass, never the collider, so these
    # prisms stay LOD-cullable like any other. What the ration bounds is inedible,
    # near-permanent mass (see Tools/Build/check_shield_collider_claims.py).
    print("armoured prisms (shielded + super-shielded), per intensity - ZERO extra colliders:")
    for i, row in enumerate(rows, start=1):
        armored = row["shielded"] + row["super_shielded"]
        print(f"  intensity {i}: {armored} of {row['total']:,}  ({100.0 * armored / row['total']:.2f}%)")


if __name__ == "__main__":
    main()
