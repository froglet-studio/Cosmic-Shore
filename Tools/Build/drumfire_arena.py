#!/usr/bin/env python3
"""
Measure Drumfire's arena offline: the DRUM's prism count and volume, its always-on collider
budget, and the firing-lane timings the mode is tuned against.

Cell environments are deterministic by contract (a pure function of the serialized seed - see
CellEnvironmentSpawnableBase), so the count is exactly computable without opening Unity. This
file is a line-by-line mirror of `SpawnableDrum.BuildEnvironment`: the same phyllotaxis point
set, the same value-noise gap test (a port of PaintingStrokeToolkit.ValueNoise, which is what
decides how many panes survive), the same loop order.

The one thing it does NOT reproduce is the per-pane `Jit` factor, which rides the shared
System.Random stream. It does not need to: Jit multiplies all three axes by one k ~ U(1-a, 1+a),
so E[k^3] = ((1+a)^4 - (1-a)^4) / (8a) = 1.04 at the default a = 0.2, exactly, and over ~20k
panes the sample mean is the expectation to well under a percent. (No axis of the shipped pane
can reach the prism scale animator's 0.5 floor at k = 0.8, so nothing is clamped - asserted
below, because a clamp would silently break that identity.)

    python3 Tools/Build/drumfire_arena.py            # print the table
    python3 Tools/Build/drumfire_arena.py --check    # verify the numbers the assets/docs claim

See Assets/_Scripts/Controller/Arcade/DRUMFIRE.md for what these numbers mean.
"""
import math
import sys

CHECK_ONLY = "--check" in sys.argv

# ── SpawnableDrum's authored values (keep in step with the prefab + the C# defaults) ──
SEED = 45
OUTER_RADIUS = 320.0
SHELL_COUNT = 5
OUTER_SHELL_POINTS = 14074
GAP_THRESHOLD = 0.25
GAP_NOISE_FREQUENCY = 0.012
PANE = (8.0, 8.0, 0.7)

RIB_COUNT = 3
PANES_PER_RIB = 72
RIB_PANE = (14.0, 5.0, 2.4)

CORE_PANES = 24
CORE_RADIUS = 34.0
CORE_PANE = (9.0, 9.0, 3.0)

DANGER_STUDS = 120
STUD = (7.0, 7.0, 5.0)
STUD_INDEX_OFFSET = 7919

JIT_AMOUNT = 0.2
GOLDEN_ANGLE = 2.39996323

# ── The lane, as authored on the scene's NetworkCrystalManager ───────────────
LANE_RING_RADIUS = 1120.0
LANE_OFFSET = 420.0
LANE_LEAD = 640.0
LANE_LENGTH = 800.0
MEMBRANE_RADIUS = 1200.0
SLOTS_BY_INTENSITY = (5, 6, 7, 8)          # crystalCountByIntensity ExtraCrystals - MORE
                                           # crystals is a TIGHTER rhythm, so it climbs with
                                           # intensity: fewer beats is the forgiving end.
MATCH_SECONDS = 75                          # EndConditionOverridesSO.DefaultDrumfireSeconds

# Dolphin flight envelope (DOLPHIN_ENERGY_ECONOMY.md section 2): max cruise 68 u/s, boost 347,
# minimum speed 0. "The slower side" is the pace the brief asks the lane to be spaced for.
DOLPHIN_CRUISE = 68.0
DOLPHIN_SLOW = 40.0


# ── PaintingStrokeToolkit's noise, ported ────────────────────────────────────
def _hash(x, y, z, seed):
    h = (x * 374761393 + y * 668265263 + z * 2147483647 + seed * 971) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    h ^= h >> 16
    return (h & 0xFFFFFF) / float(0x800000) - 1.0


def _fade(t):
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def value_noise(p, seed):
    x0, y0, z0 = math.floor(p[0]), math.floor(p[1]), math.floor(p[2])
    fx, fy, fz = _fade(p[0] - x0), _fade(p[1] - y0), _fade(p[2] - z0)
    c000 = _hash(x0, y0, z0, seed);         c100 = _hash(x0 + 1, y0, z0, seed)
    c010 = _hash(x0, y0 + 1, z0, seed);     c110 = _hash(x0 + 1, y0 + 1, z0, seed)
    c001 = _hash(x0, y0, z0 + 1, seed);     c101 = _hash(x0 + 1, y0, z0 + 1, seed)
    c011 = _hash(x0, y0 + 1, z0 + 1, seed); c111 = _hash(x0 + 1, y0 + 1, z0 + 1, seed)
    lerp = lambda a, b, t: a + (b - a) * t
    x00 = lerp(c000, c100, fx); x10 = lerp(c010, c110, fx)
    x01 = lerp(c001, c101, fx); x11 = lerp(c011, c111, fx)
    return lerp(lerp(x00, x10, fy), lerp(x01, x11, fy), fz)


def n01(x, y, z, seed_offset):
    return 0.5 * (value_noise((x, y, z), SEED + seed_offset) + 1.0)


def sphere_point(i, n):
    u = (i + 0.5) / n
    y = 1.0 - 2.0 * u
    r = math.sqrt(max(0.0, 1.0 - y * y))
    a = i * GOLDEN_ANGLE
    return (r * math.cos(a), y, r * math.sin(a))


def is_gap(p, shell):
    if GAP_THRESHOLD <= 0.0:
        return False
    f = GAP_NOISE_FREQUENCY
    return n01(p[0] * f, p[1] * f, p[2] * f, shell) < GAP_THRESHOLD


def jit_volume(size):
    """E[volume] of one Jit()ed pane. Jit scales all three axes by ONE k, so it is E[k^3]."""
    a = JIT_AMOUNT
    e_k3 = ((1 + a) ** 4 - (1 - a) ** 4) / (8 * a)
    return size[0] * size[1] * size[2] * e_k3


# ── Mirror of SpawnableDrum.BuildEnvironment ────────────────────────────────
def build():
    rows = []          # (family, kind, count, volume)

    # BuildShells
    shell_total = shell_volume = 0
    per_shell = []
    for s in range(SHELL_COUNT):
        frac = (SHELL_COUNT - s) / float(SHELL_COUNT)
        r = OUTER_RADIUS * frac
        n = max(1, math.floor(OUTER_SHELL_POINTS * frac * frac + 0.5))
        kept = 0
        for i in range(n):
            d = sphere_point(i, n)
            p = (d[0] * r, d[1] * r, d[2] * r)
            if is_gap(p, s):
                continue
            kept += 1
        per_shell.append((round(r, 1), n, kept))
        shell_total += kept
    shell_volume = shell_total * jit_volume(PANE)
    rows.append(("shells (skin)", "Plain", shell_total, shell_volume))

    # BuildRibs - unjittered, authored size
    rib_total = RIB_COUNT * PANES_PER_RIB
    rows.append(("ribs", "Shielded", rib_total,
                 rib_total * RIB_PANE[0] * RIB_PANE[1] * RIB_PANE[2]))

    # BuildCore - unjittered
    rows.append(("core cage", "SuperShielded", CORE_PANES,
                 CORE_PANES * CORE_PANE[0] * CORE_PANE[1] * CORE_PANE[2]))

    # BuildStuds - unjittered, and a stud in a hole is skipped
    studs = 0
    for i in range(DANGER_STUDS):
        d = sphere_point(i + STUD_INDEX_OFFSET, DANGER_STUDS + STUD_INDEX_OFFSET)
        if is_gap((d[0] * OUTER_RADIUS, d[1] * OUTER_RADIUS, d[2] * OUTER_RADIUS), 0):
            continue
        studs += 1
    rows.append(("danger studs", "Danger", studs,
                 studs * STUD[0] * STUD[1] * STUD[2]))

    return rows, per_shell


def lane_table():
    """Firing-lane geometry, from the same closed form CrystalManager.LaneSlotPosition uses."""
    sin_t = min(1.0, LANE_OFFSET / LANE_RING_RADIUS)
    cos_t = math.sqrt(max(0.0, 1.0 - sin_t * sin_t))
    # distance along the lane from the spawn point to the closest-approach point
    t_closest = LANE_RING_RADIUS * cos_t
    # where the lane leaves the membrane
    t_exit = t_closest + math.sqrt(MEMBRANE_RADIUS ** 2 - LANE_OFFSET ** 2)
    return sin_t, cos_t, t_closest, t_exit



# ── The blast, ported from BlastVolume.Contains (ExplosionHelper.cs) ─────────
# The Dolphin's jaw blast is a CAPSULE sweep, not a circular cone: at axial depth s the
# cross-section is a stadium - a disc of radius TanCore*s dragged along the gape axis by
# +/-TanGape*s. Energy buys the gape (the fan) and never the core (the beam width).
# DOLPHIN_ENERGY_ECONOMY.md section 1 is the source for all four numbers.
BLAST_HEIGHT = 2400.0      # AOEConicExplosion.prefab height
BLAST_MIN_LEN = 400.0      # DolphinVesselExplosionByCrystalEffect _minExplosionScale
BLAST_MAX_LEN = 2080.0     # ... _maxExplosionScale
BLAST_CORE = 320.0         # ... _coreExplosionScale


def blast_tangents(energy01):
    """(TanCorePerUnit, TanGapePerUnit) at a given banked-energy fraction."""
    length = BLAST_MIN_LEN + (BLAST_MAX_LEN - BLAST_MIN_LEN) * energy01
    return (BLAST_CORE * 0.5) / BLAST_HEIGHT, (length * 0.5) / BLAST_HEIGHT


def blast_contains(point, apex, axis, gape_axis, tan_core, tan_gape):
    rel = (point[0] - apex[0], point[1] - apex[1], point[2] - apex[2])
    s = rel[0] * axis[0] + rel[1] * axis[1] + rel[2] * axis[2]
    if s <= 0.0 or s > BLAST_HEIGHT:
        return False
    core_radius = tan_core * s
    radial = (rel[0] - axis[0] * s, rel[1] - axis[1] * s, rel[2] - axis[2] * s)
    half = tan_gape * s
    along = radial[0]*gape_axis[0] + radial[1]*gape_axis[1] + radial[2]*gape_axis[2]
    along = max(-half, min(half, along))
    off = (radial[0] - gape_axis[0]*along, radial[1] - gape_axis[1]*along, radial[2] - gape_axis[2]*along)
    return math.sqrt(off[0]**2 + off[1]**2 + off[2]**2) <= core_radius


def _norm(v):
    m = math.sqrt(v[0]**2 + v[1]**2 + v[2]**2)
    return (v[0]/m, v[1]/m, v[2]/m) if m > 1e-9 else (0.0, 0.0, 1.0)


def _cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def drum_points():
    """Every destructible pane of the drum, as (position, kind). Super-shielded is excluded -
    it can never be destroyed, which is the point of it."""
    pts = []
    for s in range(SHELL_COUNT):
        frac = (SHELL_COUNT - s) / float(SHELL_COUNT)
        r = OUTER_RADIUS * frac
        n = max(1, math.floor(OUTER_SHELL_POINTS * frac * frac + 0.5))
        for i in range(n):
            d = sphere_point(i, n)
            p = (d[0]*r, d[1]*r, d[2]*r)
            if is_gap(p, s):
                continue
            pts.append((p, "plain"))
    rib_radius = OUTER_RADIUS + RIB_PANE[2] * 1.5
    for ridx in range(RIB_COUNT):
        lon = math.pi * ridx / RIB_COUNT
        axis = (math.cos(lon), 0.0, math.sin(lon))
        for i in range(PANES_PER_RIB):
            tt = 2.0 * math.pi * i / PANES_PER_RIB
            d = _norm((axis[0]*math.cos(tt), math.sin(tt), axis[2]*math.cos(tt)))
            pts.append(((d[0]*rib_radius, d[1]*rib_radius, d[2]*rib_radius), "shielded"))
    for i in range(DANGER_STUDS):
        d = sphere_point(i + STUD_INDEX_OFFSET, DANGER_STUDS + STUD_INDEX_OFFSET)
        if is_gap((d[0]*OUTER_RADIUS, d[1]*OUTER_RADIUS, d[2]*OUTER_RADIUS), 0):
            continue
        rr = OUTER_RADIUS + STUD[2] * 0.5
        pts.append(((d[0]*rr, d[1]*rr, d[2]*rr), "danger"))
    return pts


def simulate(energy01, slots, players, passes=2.0, verbose=False):
    """Fly every pilot down their lane firing at the drum's centre from each crystal, and
    report how much of the drum is gone. The aim is IDEALISED (dead on the centre, gape axis
    in the plane through the lane and the centre), so this is an UPPER bound on consumption -
    exactly the bound the arena has to survive."""
    pts = drum_points()
    hp = {}
    for idx, (_, kind) in enumerate(pts):
        hp[idx] = 2 if kind == "shielded" else 1     # a shield sheds first, then the prism dies

    tan_core, tan_gape = blast_tangents(energy01)
    sin_t = min(1.0, LANE_OFFSET / LANE_RING_RADIUS)
    cos_t = math.sqrt(max(0.0, 1.0 - sin_t * sin_t))
    spacing = LANE_LENGTH / (slots - 1)

    shots = 0
    for lane in range(players):
        # The lane's own frame (the formation only rotates it; consumption is frame-invariant
        # for a spherically symmetric drum, so one representative direction per lane is enough
        # as long as the lanes do not overlap - they do not, they are symmetric).
        outward = _norm((math.sin(2*math.pi*lane/max(1, players)), 0.3*((-1)**lane), math.cos(2*math.pi*lane/max(1, players))))
        ref = (0.0, 1.0, 0.0) if abs(outward[1]) <= 0.99 else (0.0, 0.0, 1.0)
        perp = _norm(_cross(outward, ref))
        heading = _norm((-outward[0]*cos_t + perp[0]*sin_t,
                         -outward[1]*cos_t + perp[1]*sin_t,
                         -outward[2]*cos_t + perp[2]*sin_t))
        start = (outward[0]*LANE_RING_RADIUS, outward[1]*LANE_RING_RADIUS, outward[2]*LANE_RING_RADIUS)
        for rep in range(int(round(passes))):
            for s in range(slots):
                along = LANE_LEAD + s * spacing
                apex = (start[0]+heading[0]*along, start[1]+heading[1]*along, start[2]+heading[2]*along)
                axis = _norm((-apex[0], -apex[1], -apex[2]))          # nose on the drum's centre
                gape = _norm(_cross(axis, _cross(heading, axis)))     # roll: gape in the lane plane
                shots += 1
                for idx, (p, kind) in enumerate(pts):
                    if hp[idx] <= 0:
                        continue
                    if blast_contains(p, apex, axis, gape, tan_core, tan_gape):
                        hp[idx] -= 1
    destroyed = sum(1 for idx, (_, kind) in enumerate(pts) if hp[idx] <= 0)
    return shots, destroyed, len(pts)


def main():
    rows, per_shell = build()
    sin_t, cos_t, t_closest, t_exit = lane_table()

    total_count = sum(r[2] for r in rows)
    total_volume = sum(r[3] for r in rows)
    always_on = sum(r[2] for r in rows if r[1] in ("Shielded", "SuperShielded"))

    print("THE DRUM (SpawnableDrum, seed %d, outer radius %g)" % (SEED, OUTER_RADIUS))
    print("  shell        radius   points    kept   gap%")
    for i, (r, n, kept) in enumerate(per_shell):
        print(f"  {i:<12} {r:>6}  {n:>7}  {kept:>6}  {100.0*(1-kept/n):>5.1f}")
    print()
    print("  family            kind             count        volume")
    for fam, kind, count, vol in rows:
        print(f"  {fam:<16}  {kind:<14}  {count:>7}  {vol:>12,.0f}")
    print(f"  {'TOTAL':<16}  {'':<14}  {total_count:>7}  {total_volume:>12,.0f}")
    print()
    print(f"  always-on mesh colliders (shielded + super-shielded): {always_on}")
    print(f"  LOD-cullable box colliders (plain + danger):          {total_count - always_on}")
    print()

    print("THE FIRING LANE (per player, identical at every intensity)")
    print(f"  ring radius {LANE_RING_RADIUS:g}, offset from centre {LANE_OFFSET:g}"
          f"  ->  turn onto the lane {math.degrees(math.asin(sin_t)):.1f} deg off the straight-at-centre line")
    print(f"  closest approach to the drum's skin: {LANE_OFFSET - OUTER_RADIUS:.0f}u"
          f"  (reached {t_closest - LANE_LEAD:.0f}u into a {LANE_LENGTH:g}u run)")
    print(f"  run-out past the last crystal before the membrane: {t_exit - LANE_LEAD - LANE_LENGTH:.0f}u")
    print()
    print("  intensity  slots  spacing   beat @40u/s  beat @68u/s   pass @40u/s  passes in %ds" % MATCH_SECONDS)
    for idx, slots in enumerate(SLOTS_BY_INTENSITY, start=1):
        spacing = LANE_LENGTH / (slots - 1)
        run = LANE_LEAD + LANE_LENGTH
        print(f"  {idx:>9}  {slots:>5}  {spacing:>7.0f}   {spacing/DOLPHIN_SLOW:>10.1f}s  "
              f"{spacing/DOLPHIN_CRUISE:>10.1f}s   {run/DOLPHIN_SLOW:>10.0f}s  "
              f"{MATCH_SECONDS/(run/DOLPHIN_SLOW):>12.1f}")
    print()

    # ── Assertions: what the design claims, checked ──────────────────────────
    errors = []

    # Jit must not clamp, or E[k^3] = 1.04 stops holding and the volume above is wrong.
    for axis, v in zip("xyz", PANE):
        if v * (1 - JIT_AMOUNT) < 0.5:
            errors.append(f"pane {axis} = {v} falls under the prism scale animator's 0.5 floor "
                          f"at Jit's low end - the volume model no longer holds")

    # The lane must never enter the drum, or a pilot flying their own crystals clips it.
    if LANE_OFFSET <= OUTER_RADIUS:
        errors.append(f"lane offset {LANE_OFFSET} does not clear the drum ({OUTER_RADIUS})")

    # ...and it must pass CLOSE enough that leaning in to graze the skin for jaw energy is a
    # real option rather than a separate trip. Half the offset is the working band.
    if LANE_OFFSET - OUTER_RADIUS > OUTER_RADIUS * 0.5:
        errors.append("the lane stands so far off the drum that grazing it is a detour, not a lean")

    # Every crystal inside the membrane, with room to turn around after the last one.
    if LANE_LEAD + LANE_LENGTH >= t_exit:
        errors.append("the last crystal is outside the membrane")
    if t_exit - (LANE_LEAD + LANE_LENGTH) < 200:
        errors.append(f"only {t_exit - LANE_LEAD - LANE_LENGTH:.0f}u of run-out past the last "
                      f"crystal - not enough room to turn around inside the arena")

    # The drum must be PASSED mid-run, not before the first crystal or after the last.
    if not (LANE_LEAD < t_closest < LANE_LEAD + LANE_LENGTH):
        errors.append("the drum is not passed between the first and last crystal")

    # A slow pilot must just about finish one pass in the match, which is the brief - and the
    # clock must not run long past it, because the drum is only about one pass of ammunition.
    slow_pass = (LANE_LEAD + LANE_LENGTH) / DOLPHIN_SLOW
    if not (1.2 <= MATCH_SECONDS / slow_pass <= 3.5):
        errors.append(f"a slow pilot gets {MATCH_SECONDS/slow_pass:.1f} passes - the clock should "
                      f"cover one comfortably and not much more")

    # Beats must leave real aiming time at the pace the mode is FOR (the brief says the slower
    # side; cruise is the pilot's own choice and buys them a tighter rhythm).
    for idx, slots in enumerate(SLOTS_BY_INTENSITY, start=1):
        beat = (LANE_LENGTH / (slots - 1)) / DOLPHIN_SLOW
        if beat < 2.5:
            errors.append(f"intensity {idx}: {beat:.1f}s between crystals at {DOLPHIN_SLOW:g}u/s "
                          f"is not 'plenty of time to aim'")

    # The crystals must sit ACROSS the lane's closest approach, not off one end: the shot yield
    # goes as range^2, so an off-centre band makes the far crystals worth several times the near
    # ones and the whole run collapses into "fire from as far back as you can".
    _sin = min(1.0, LANE_OFFSET / LANE_RING_RADIUS)
    _t_closest = LANE_RING_RADIUS * math.sqrt(max(0.0, 1.0 - _sin * _sin))
    _near = LANE_LEAD
    _far = LANE_LEAD + LANE_LENGTH
    _r_near = math.hypot(LANE_OFFSET, _t_closest - _near)
    _r_far = math.hypot(LANE_OFFSET, _far - _t_closest)
    _spread = (max(_r_near, _r_far) ** 2 + OUTER_RADIUS ** 2) / (LANE_OFFSET ** 2 + OUTER_RADIUS ** 2)
    if _spread > 2.0:
        errors.append(f"the lane's end crystals are worth {_spread:.1f}x its middle ones - "
                      f"re-centre the band on the closest approach (t = {_t_closest:.0f})")

    # Collider budget: the always-on mesh colliders are the ones that do not LOD away. The
    # shipped freestyle cells sit at ~225; stay in that class.
    if always_on > 400:
        errors.append(f"{always_on} always-on mesh colliders is outside the shipped band (~225)")

    # The drum has to survive a full match of four pilots carving it, or scoring stops early.
    if total_count < 15000:
        errors.append(f"{total_count} prisms is too little drum to last a {MATCH_SECONDS}s match")

    # ── Does the drum SURVIVE a match? ──────────────────────────────────────
    # The score is volume torn out of a finite ball, so an arena that can be cleared before the
    # clock stops would leave pilots flying a lane with nothing to shoot. Idealised aim (dead on
    # the centre every time) makes this an upper bound on consumption.
    print("MATCH CONSUMPTION (4 pilots, 2 passes each, idealised aim - an UPPER bound)")
    print("  intensity  banked energy   shots  drum destroyed")
    worst = 0.0
    for idx, slots in enumerate(SLOTS_BY_INTENSITY, start=1):
        for energy, label in ((0.0, "empty  "), (0.5, "half   "), (1.0, "full   ")):
            shots, destroyed, total = simulate(energy, slots, players=4)
            frac = destroyed / total
            worst = max(worst, frac)
            print(f"  {idx:>9}  {label}        {shots:>5}  {destroyed:>6} / {total} = {100*frac:>5.1f}%")
        if idx == 1:
            print()

    if worst >= 0.95:
        errors.append(f"a match can strip {100*worst:.0f}% of the drum - pilots would run out of "
                      f"target before the clock stops")

    if errors:
        print()
        print("FAILED:")
        for e in errors:
            print("  x", e)
        return 1

    print("All arena assertions passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
