#!/usr/bin/env python3
"""
Analytic prism budget and geometry proof for the SWITCHYARD - the Hijack arena.

This is the model of `SpawnableSwitchyard.cs`, and it is a MIRROR, not an estimate: the asset
generator (`author_hijack_assets.py`) imports it, so the arena's PhaseThresholds are derived from
the same numbers that build the arena and the two cannot drift apart. That mirroring is only
possible because the C# was written to make it possible - the whole generator is CLOSED FORM.
There is no `System.Random` draw anywhere in `SpawnableSwitchyard.BuildEnvironment`, so every
count is exact arithmetic and every position is reproducible here to the float.

Run it directly to print the table that belongs in HIJACK.md, and to run the geometry proofs:

    python3 Tools/Build/hijack_budget.py

The proofs are the point. The mode's headline verb - "launch off the end of a rail and land in a
burr" - is a claim about TANGENTS, and a claim about tangents is exactly the kind of thing that
looks right in a diagram and is wrong in the build. `prove_launch_geometry()` checks it the only
way that means anything: it walks each rail's last two prisms, takes the real difference vector,
and confirms the ray from the final prism hits the burr centre.

CONFIRM IN EDITOR before trusting the thresholds: FrogletTools > Ecology > Measure Cell
Environment Baselines. If the measurer disagrees with this table, the C# and this file have
drifted - fix both, do not paper over it in the asset.
"""

import math

# ── Geometry constants: must match SpawnableSwitchyard.cs ────────────────────

RING_RADIUS = 900.0          # great-circle radius of all three rings
STATIONS_PER_RING = 8        # 45 degrees apart
RAIL_HALF_GAP_DEG = 12.5     # each rail stops this far short of its two stations
RAIL_PRISMS = 40             # prisms per rail
PRISM_SCALE = (3.0, 3.0, 6.0)

# Spacing is DERIVED, not authored, and that is load-bearing. The rail's prisms span the FULL
# 20-degree arc, endpoint to endpoint, so the terminal prism sits exactly at
# theta_(j+1) - RAIL_HALF_GAP_DEG - which is the only place the arc tangent there passes through
# the station radial at R/cos(gap). An authored 8.0 spacing centres 312u of prisms inside a 314u
# arc, insets the terminal prism by ~1u, and tilts the launch 0.32 degrees off the burr. Caught by
# prove_launch_geometry() before a line of C# was written; do not "tidy" this back to a round
# number without re-running that proof.
RAIL_ARC_DEG = 45.0 - 2.0 * RAIL_HALF_GAP_DEG                       # 20 degrees
RAIL_SPACING = (RING_RADIUS * math.radians(RAIL_ARC_DEG)) / (RAIL_PRISMS - 1)   # ~8.06u
YAW_DEGREES = 22.5           # whole-yard rotation about world Y (spawn-slot alignment)

# Burr shells: radius 10*s, n_s = round(4*pi*s^2) prisms on a Fibonacci sphere.
SHELL_PITCH = 10.0

# (big shells, small shells) per intensity.
INTENSITIES = [(3, 2), (4, 2), (5, 3), (6, 3)]

SEED = 45                    # SpawnableSwitchyard.DefaultSeed (unused - closed form - but authored)

# Derived: where a burr sits. The rail's end tangent at +-RAIL_HALF_GAP_DEG from a station passes
# through the station's radial at exactly R/cos(gap), which is why the launch is aimed by
# construction rather than by tuning. Both numbers are re-derived (not asserted) in the proofs.
BURR_RADIUS_FROM_CORE = RING_RADIUS / math.cos(math.radians(RAIL_HALF_GAP_DEG))
LAUNCH_GAP = RING_RADIUS * math.tan(math.radians(RAIL_HALF_GAP_DEG))

# SpawnableSwitchyard.BurrMatchRadius - how close a rail's named target must be to a
# burr centre to BE that burr.
BURR_MATCH_RADIUS = 50.0

SPAWN_RING_RADIUS = 1120.0
MEMBRANE_RADIUS = 1200.0

GOLDEN_ANGLE = 2.39996323    # CellEnvironmentSpawnableBase.GoldenAngle


def shell_prisms(s: int) -> int:
    """Prisms on shell s. Mirrors Mathf.RoundToInt(4*pi*s*s) - banker's rounding, which is also
    Python's round() for floats, so the two agree without special-casing."""
    return int(round(4.0 * math.pi * s * s))


def burr_prisms(shells: int) -> int:
    return sum(shell_prisms(s) for s in range(1, shells + 1))


def burr_radius(shells: int) -> float:
    return SHELL_PITCH * shells


def unit_volume() -> float:
    x, y, z = PRISM_SCALE
    return x * y * z


# ── The three rings, parametrised so a 120-degree turn about (1,1,1) maps ring k -> k+1 ──
# Ring 0 (XY): +X toward +Y.  Ring 1 (YZ): +Y toward +Z.  Ring 2 (ZX): +Z toward +X.
RING_BASIS = [
    ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0)),
    ((0.0, 1.0, 0.0), (0.0, 0.0, 1.0)),
    ((0.0, 0.0, 1.0), (1.0, 0.0, 0.0)),
]


def ring_point(k: int, theta: float, radius: float = RING_RADIUS):
    a, b = RING_BASIS[k]
    c, s = math.cos(theta), math.sin(theta)
    return tuple(radius * (c * a[i] + s * b[i]) for i in range(3))


def ring_tangent(k: int, theta: float):
    a, b = RING_BASIS[k]
    c, s = math.cos(theta), math.sin(theta)
    return tuple(-s * a[i] + c * b[i] for i in range(3))


def yaw(p):
    """Rotate about world Y by YAW_DEGREES (the last build step)."""
    c, s = math.cos(math.radians(YAW_DEGREES)), math.sin(math.radians(YAW_DEGREES))
    x, y, z = p
    return (c * x + s * z, y, -s * x + c * z)


def sub(a, b):
    return tuple(a[i] - b[i] for i in range(3))


def norm(a):
    return math.sqrt(sum(v * v for v in a))


def unit(a):
    n = norm(a)
    return tuple(v / n for v in a)


def dot(a, b):
    return sum(a[i] * b[i] for i in range(3))


# ── Rails ────────────────────────────────────────────────────────────────────

def rail_thetas(j: int):
    """(start, end) angle of the rail leaving station j, in radians."""
    gap = math.radians(RAIL_HALF_GAP_DEG)
    return math.radians(45.0 * j) + gap, math.radians(45.0 * (j + 1)) - gap


def rail_prism_angles(j: int):
    """The 40 prism angles of the rail leaving station j - endpoint to endpoint, so the terminal
    prism sits exactly on the launch tangent that aims at the next station's burr."""
    t0, t1 = rail_thetas(j)
    return [t0 + (t1 - t0) * (i / (RAIL_PRISMS - 1)) for i in range(RAIL_PRISMS)]


def rail_prism_positions(k: int, j: int):
    """The 40 prism centres of rail (k, j), in build order (low theta first)."""
    return [ring_point(k, t) for t in rail_prism_angles(j)]


def burr_centre(k: int, j: int):
    """The burr the rail leaving station j on ring k launches INTO (the station at j+1)."""
    return ring_point(k, math.radians(45.0 * (j + 1)), BURR_RADIUS_FROM_CORE)


# ── Painting: full triad, exactly equal per domain, no Blue ──────────────────
# Domains enum: Jade = 1, Ruby = 2, Blue = 3, Gold = 4.
DOMAINS = [1, 2, 4]          # Jade, Ruby, Gold
DOMAIN_NAMES = {1: "Jade", 2: "Ruby", 4: "Gold"}

RAIL_THIRDS = (14, 13, 13)   # prisms per third; sums to RAIL_PRISMS


def rail_domain(k: int, j: int, i: int) -> int:
    """Domain of prism i on rail (k, j). Three thirds from the low-theta end, so EVERY rail has
    a fast third for every domain."""
    a, b, _ = RAIL_THIRDS
    third = 0 if i < a else (1 if i < a + b else 2)
    return DOMAINS[(j + k + third) % 3]


def big_burr_domain(k: int, kp: int) -> int:
    """A big burr sits where rings k and k' cross; it wears the THIRD domain, so it is always
    hostile to both rings that launch into it."""
    return DOMAINS[(3 - k - kp) % 3]


def small_burr_domain(k: int, j: int) -> int:
    return DOMAINS[(k + (j - 1) // 2) % 3]


# ── Budget ───────────────────────────────────────────────────────────────────

def budget(big_shells: int, small_shells: int) -> dict:
    big = burr_prisms(big_shells)
    small = burr_prisms(small_shells)
    rails = 3 * STATIONS_PER_RING * RAIL_PRISMS      # 24 rails x 40
    burrs = 6 * big + 12 * small                     # 6 big (axis crossings) + 12 small
    total = rails + burrs

    per_domain_rail = rails // 3
    per_domain_burr = 2 * big + 4 * small
    return {
        "rails": rails,
        "big_each": big,
        "small_each": small,
        "burrs": burrs,
        "total": total,
        "volume": total * unit_volume(),
        "big_radius": burr_radius(big_shells),
        "small_radius": burr_radius(small_shells),
        "per_domain": per_domain_rail + per_domain_burr,
    }


BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000,
                   rev=11200, rxv=8000, fev=57600, fxv=48000)


def phase_thresholds(n: int, v: float) -> dict:
    """Measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md 18)."""
    return dict(
        RestlessEnter=n + BLOB_DELTAS["re"], RestlessExit=n + BLOB_DELTAS["rx"],
        FrenzyEnter=n + BLOB_DELTAS["fe"], FrenzyExit=n + BLOB_DELTAS["fx"],
        RestlessEnterVolume=round(v + BLOB_DELTAS["rev"]),
        RestlessExitVolume=round(v + BLOB_DELTAS["rxv"]),
        FrenzyEnterVolume=round(v + BLOB_DELTAS["fev"]),
        FrenzyExitVolume=round(v + BLOB_DELTAS["fxv"]))


def all_intensities() -> list:
    return [budget(*p) for p in INTENSITIES]


# ── Geometry proofs ──────────────────────────────────────────────────────────

def prove_launch_geometry():
    """THE load-bearing proof: grinding a rail to its end and NOT steering must fly you into a
    burr.

    Measured TWO ways, because the real launch heading sits between them. `Trail.Project`
    evaluates a Catmull-Rom spline through the block centres, so the heading it writes into
    `VesselStatus.Course` at the terminal block is the spline tangent - very close to the ARC
    tangent for points sampled on a circle. The CHORD between the last two prisms is the crudest
    possible reading of the same thing and is off by half the per-prism subtended angle. Reporting
    both means the claim is bounded rather than asserted: the arc tangent must be exact, and even
    the chord must land well inside the smallest burr.
    """
    worst_arc = 0.0
    worst_chord_miss = 0.0
    gaps = []
    for k in range(3):
        for j in range(STATIONS_PER_RING):
            pts = rail_prism_positions(k, j)
            angs = rail_prism_angles(j)
            target = burr_centre(k, j)
            to_target = sub(target, pts[-1])
            gaps.append(norm(to_target))

            arc_t = unit(ring_tangent(k, angs[-1]))
            worst_arc = max(worst_arc, math.degrees(
                math.acos(max(-1.0, min(1.0, dot(arc_t, unit(to_target)))))))

            chord_t = unit(sub(pts[-1], pts[-2]))
            along = dot(to_target, chord_t)
            worst_chord_miss = max(worst_chord_miss,
                                   norm(sub(to_target, tuple(along * t for t in chord_t))))
    return worst_arc, worst_chord_miss, min(gaps), max(gaps)


def prove_rails_do_not_cross():
    """Two rings meet only at their shared axis stations, and every rail stops short of every
    station - so the nearest approach between rails on DIFFERENT rings must clear the prisms."""
    rails = []
    for k in range(3):
        for j in range(STATIONS_PER_RING):
            rails.append((k, rail_prism_positions(k, j)))
    worst = float("inf")
    for a in range(len(rails)):
        for b in range(a + 1, len(rails)):
            if rails[a][0] == rails[b][0]:
                continue                      # same ring: rails are disjoint arcs by construction
            for p in rails[a][1]:
                for q in rails[b][1]:
                    worst = min(worst, norm(sub(p, q)))
    return worst


def prove_burr_clearances():
    """A rail prism must not sit inside a burr, at any intensity."""
    worst = {}
    for idx, (big_s, small_s) in enumerate(INTENSITIES, start=1):
        closest = float("inf")
        for k in range(3):
            for j in range(STATIONS_PER_RING):
                pts = rail_prism_positions(k, j)
                for jj in (j, j + 1):
                    station = jj % STATIONS_PER_RING
                    r = burr_radius(big_s if station % 2 == 0 else small_s)
                    centre = ring_point(k, math.radians(45.0 * jj), BURR_RADIUS_FROM_CORE)
                    for p in pts:
                        closest = min(closest, norm(sub(p, centre)) - r)
        worst[idx] = closest
    return worst


def prove_extent():
    """Everything must sit inside the spawn ring, which must sit inside the membrane."""
    big_s = max(b for b, _ in INTENSITIES)
    return BURR_RADIUS_FROM_CORE + burr_radius(big_s) + PRISM_SCALE[2] * 0.5


def prove_painting_balance():
    """Every domain owns exactly the same mass, and every rail offers every domain a fast third."""
    rail_counts = {d: 0 for d in DOMAINS}
    for k in range(3):
        for j in range(STATIONS_PER_RING):
            seen = set()
            for i in range(RAIL_PRISMS):
                d = rail_domain(k, j, i)
                rail_counts[d] += 1
                seen.add(d)
            assert seen == set(DOMAINS), f"rail ({k},{j}) is missing a domain: {seen}"
    # Big burrs: the six axis stations, each shared by two rings.
    big_counts = {d: 0 for d in DOMAINS}
    for pair in ((0, 1), (1, 2), (0, 2)):
        for _ in range(2):                      # two antipodal stations per crossing
            big_counts[big_burr_domain(*pair)] += 1
    small_counts = {d: 0 for d in DOMAINS}
    for k in range(3):
        for j in range(1, STATIONS_PER_RING, 2):
            small_counts[small_burr_domain(k, j)] += 1
    return rail_counts, big_counts, small_counts


def prove_burr_resolution():
    """SpawnableSwitchyard resolves "which burr does this rail aim at?" by NEAREST CENTRE, with a
    50u match radius. That is safe iff the two numbers bracketing it are far apart, so both are
    measured rather than assumed:
      (1) the closest pair of burr centres - the match radius must be well under half of it, or
          a rail could match the wrong burr;
      (2) the worst rail-target-to-its-own-burr error - the match radius must be well over it.

    A quantize-to-whole-units KEY was written first and rejected: it measured a burr coordinate
    sitting 0.049 of a unit from a .5 rounding boundary, where float32 (the engine) and float64
    (this model) can disagree, the lookup misses, and that rail's launch silently aims at
    nothing. Rounding a float to establish identity is a tolerance with a cliff in the middle.
    """
    centres = []
    for axis in range(3):
        d = [0.0, 0.0, 0.0]; d[axis] = 1.0
        centres.append(yaw(tuple(v * BURR_RADIUS_FROM_CORE for v in d)))
        centres.append(yaw(tuple(-v * BURR_RADIUS_FROM_CORE for v in d)))
    for k in range(3):
        for j in range(1, STATIONS_PER_RING, 2):
            centres.append(yaw(ring_point(k, math.radians(45.0 * j), BURR_RADIUS_FROM_CORE)))

    closest_pair = min(norm(sub(a, b))
                       for i, a in enumerate(centres) for b in centres[i + 1:])

    worst_error = 0.0
    for k in range(3):
        for j in range(STATIONS_PER_RING):
            t = yaw(ring_point(k, math.radians(45.0 * (j + 1)), BURR_RADIUS_FROM_CORE))
            worst_error = max(worst_error, min(norm(sub(t, c)) for c in centres))

    return len(centres), closest_pair, worst_error


def main():
    rows = all_intensities()

    print("HIJACK - the Switchyard arena\n")
    header = (f"{'intensity':<10}{'rails':>8}{'big(each)':>11}{'small(each)':>13}"
              f"{'burrs':>9}{'TOTAL':>9}{'volume':>12}{'per-domain':>12}")
    print(header)
    print("-" * len(header))
    for i, row in enumerate(rows, start=1):
        print(f"{i:<10}{row['rails']:>8,}{row['big_each']:>11,}{row['small_each']:>13,}"
              f"{row['burrs']:>9,}{row['total']:>9,}{row['volume']:>12,.0f}{row['per_domain']:>12,}")

    print("\nPhaseThresholds (baseline + the standard Blob deltas):")
    for i, row in enumerate(rows, start=1):
        th = phase_thresholds(row["total"], row["volume"])
        print(f"  intensity {i}: restless {th['RestlessEnter']}/{th['RestlessExit']}  "
              f"frenzy {th['FrenzyEnter']}/{th['FrenzyExit']}  "
              f"vol restless {th['RestlessEnterVolume']:,}/{th['RestlessExitVolume']:,}  "
              f"frenzy {th['FrenzyEnterVolume']:,}/{th['FrenzyExitVolume']:,}")

    print("\n--- geometry proofs ---")
    ang, chord_miss, gmin, gmax = prove_launch_geometry()
    smallest_burr = burr_radius(min(s for _, s in INTENSITIES))
    print(f"launch aim:      arc tangent {ang:.6f} deg off the burr centre; the crude CHORD "
          f"reading misses by {chord_miss:.2f}u")
    print(f"                 against the smallest burr radius {smallest_burr:.0f}u "
          f"({100.0 * chord_miss / smallest_burr:.1f}% of it)")
    print(f"launch gap:      {gmin:.1f}u .. {gmax:.1f}u   (closed form R*tan(12.5) = {LAUNCH_GAP:.1f})")
    print(f"rail spacing:    {RAIL_SPACING:.4f}u (derived: {RAIL_ARC_DEG:.0f}-deg arc / {RAIL_PRISMS - 1} gaps)")
    print(f"burr radius:     R/cos(12.5) = {BURR_RADIUS_FROM_CORE:.1f}u from the core")

    sep = prove_rails_do_not_cross()
    print(f"rail separation: nearest prisms on different rings {sep:.1f}u apart")

    clear = prove_burr_clearances()
    print("burr clearance:  " + ", ".join(f"I{i} {c:.1f}u" for i, c in clear.items()))

    ext = prove_extent()
    print(f"extent:          outermost mass {ext:.1f}u < spawn ring {SPAWN_RING_RADIUS:.0f} "
          f"< membrane {MEMBRANE_RADIUS:.0f}")

    nburrs, closest_pair, worst_error = prove_burr_resolution()
    print(f"burr resolution: {nburrs} burrs; closest pair {closest_pair:.1f}u apart, worst rail "
          f"target error {worst_error:.1e}u, match radius {BURR_MATCH_RADIUS:.0f}u")

    rc, bc, sc = prove_painting_balance()
    print("painting:        rails " + ", ".join(f"{DOMAIN_NAMES[d]} {rc[d]}" for d in DOMAINS)
          + " | big burrs " + ", ".join(f"{DOMAIN_NAMES[d]} {bc[d]}" for d in DOMAINS)
          + " | small burrs " + ", ".join(f"{DOMAIN_NAMES[d]} {sc[d]}" for d in DOMAINS))

    # 1e-4 deg, not 0: the residual is accumulated float64 trig rounding through the ring
    # basis and the acos, not geometry. A real aiming error shows up in the SECOND digit
    # (the authored-8.0-spacing bug this proof caught measured 0.3232 deg).
    assert ang < 1e-4, f"the launch is not aimed at the burr ({ang} deg)"
    assert chord_miss < 0.25 * smallest_burr, (
        f"even the chord reading of the launch heading misses by {chord_miss:.1f}u against a "
        f"{smallest_burr:.0f}u burr")
    assert len(set(rc.values())) == 1, "rail painting is not equal per domain"
    assert len(set(bc.values())) == 1, "big burrs are not equal per domain"
    assert len(set(sc.values())) == 1, "small burrs are not equal per domain"
    assert min(clear.values()) > PRISM_SCALE[2], "a rail prism sits too close to a burr"
    assert ext < SPAWN_RING_RADIUS, "the arena reaches the spawn ring"
    assert nburrs == 18, f"expected 18 burrs, found {nburrs}"
    assert worst_error < BURR_MATCH_RADIUS, "a rail's target is outside the burr match radius"
    assert BURR_MATCH_RADIUS < 0.5 * closest_pair, (
        "the burr match radius reaches more than halfway to the next burr")
    print("\nall proofs passed.")


if __name__ == "__main__":
    main()
