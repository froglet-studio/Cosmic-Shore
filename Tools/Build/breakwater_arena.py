#!/usr/bin/env python3
"""
Analytic model and geometry proof for the BREAKWATER arena - the Sparrow-only station race.

This is the MIRROR of `SpawnableBreakwater.cs` + `BreakwaterStationBuilder.cs` +
`BreakwaterCourse.cs`, not an estimate. `author_breakwater_assets.py` IMPORTS it, so the cell's
PhaseThresholds are derived from the same arithmetic that builds the arena and the two cannot
drift. That mirroring is only possible because the station builder is CLOSED FORM - there is no
random draw anywhere in it - and because the course walk uses a SPECIFIED xorshift32 that this
file reproduces bit for bit.

Run it to print the tables that belong in BREAKWATER.md and to run the proofs:

    python3 Tools/Build/breakwater_arena.py

THE PROOFS ARE THE POINT. Three things here are claims that look right in a table and are wrong
in the build, so each is MEASURED over the real walk rather than asserted:

  1. FLYABILITY. Every corner must clear the Sparrow's own turning circle at the transient
     element ceiling, or a course exists that no pilot can hold. Dubins: leg > 2R.sin(turn).
  2. THE COLLIDER BUDGET. It is a claim about how many stations fall inside
     PrismColliderLodManager's 200 m radius at once - which depends on how the walk actually
     folds, not on the authored separation. Measured over every sampled point of every course.
  3. THE PRISM CLAMP. PrismScaleAnimator clamps per axis into [minScale, maxScale] INSIDE the
     setter, with no log and no return value, and the environment lay path writes TargetScale
     without calling AdmitTargetScale - so an out-of-band axis is silently swallowed and every
     offline measurement of it is wrong. Every emitted axis is checked here.

CONFIRM IN EDITOR before trusting the thresholds: FrogletTools > Ecology > Measure Cell
Environment Baselines. If the measurer disagrees with this table, the C# and this file have
drifted - fix both, do not paper over it in the asset.
"""

import math
import sys

# ── The vessel, measured (see BREAKWATER.md "The numbers and where they come from") ──────────

SPARROW_HULL_RADIUS = 12.32     # circumscribing radius of the hull colliders at the vessel root
MIN_SCALE = 0.5                 # SpawnablePrism.prefab PrismScaleAnimator.minScale (per axis)
MAX_SCALE = 100.0               # ...maxScale
LOD_RADIUS = 200.0              # PrismColliderLodManager.lodRadiusMeters
COLLIDER_BAND = 1500            # the per-cell active-collider target this arena must stay under

# Skyburst blast, resting Charge -> Charge 10. The DOOR-CUTTER is AOEExplosion.prefab's
# spherical blast, NOT AOEConicSkyBurst's radial blocks (which lay a forward cone shell, not a
# bore - see BREAKWATER.md "The correction").
BLAST_RADIUS_REST = 50.0
BLAST_RADIUS_CHARGE10 = 85.0

# ── Station geometry: must match BreakwaterStationBuilder.cs ─────────────────────────────────

EYE_RADIUS = 18.0          # the threadable gap at the plug's centre
RAKE_ANGLES = (0.0, 60.0, 120.0)
RAKE_PITCH = 12.0          # perpendicular spacing between parallel bars
RAKE_EDGE_MARGIN = 3.0     # a line closer than this to the rim is skipped
MAX_BAR_LEN = 62.0         # under MAX_SCALE with margin
BAR_CROSS = 3.0            # bar cross-section (x and y)

COLLAR_COUNT = 12
COLLAR_RADIUS = 22.0
COLLAR_CUBE = 8.0

DISH_RATIO = 1.75          # R_dish / R_port
DISH_PITCH = 14.0          # radial spacing of rings, and the plate pitch along a ring
DISH_PLATE = (7.0, 7.0, 1.5)
DISH_JITTER = 0.18         # +/- fraction applied to a plate's scale

SHOAL_CLUSTERS_PER_LEG = 6
SHOAL_PRISMS_PER_CLUSTER = 7
SHOAL_CUBE = 4.0

NOMINAL_PRISM_VOLUME = 16.0

# ── The intensity ladder ────────────────────────────────────────────────────────────────────
# R_port is THE axis. It is bounded on both sides by shipped facts: it must clear the hull with
# real margin at the hard end, and at the hard end it must also sit UNDER the resting-Charge
# blast radius so a pilot with no upgrade still clears a whole plug in one rocket. It moves two
# things with one number - a smaller port means a smaller dish (x1.75), so the hardest course is
# also the poorest ammo bank.
#
# MinStep/MaxStep are the leg length. MinSeparation is DERIVED below and is always < MinStep,
# because BreakwaterCourse.TooClose (like SwitchbackCourse's) tests a candidate against EVERY
# placed station INCLUDING its immediate predecessor - so a separation above the minimum leg
# rejects most of the step range and the walk starves.

INTENSITIES = [
    # R_port, MinStep, MaxStep, MaxTurn, AxisJitter, MaxPresent
    dict(port=72.0, min_step=300.0, max_step=460.0, max_turn=45.0, jitter=22.0, present=50.0),
    dict(port=60.0, min_step=300.0, max_step=450.0, max_turn=50.0, jitter=28.0, present=54.0),
    dict(port=50.0, min_step=290.0, max_step=440.0, max_turn=55.0, jitter=34.0, present=58.0),
    dict(port=42.0, min_step=275.0, max_step=420.0, max_turn=60.0, jitter=40.0, present=62.0),
]

# The leg/turn ladder above is MEASURED, not chosen. An earlier cut ran I1 at legs 340-520 with a
# 40-degree cap on the reasoning that the gentlest corners belong at the easiest level, and it
# failed to generate on 21% of seeds: inside a 660-unit-thick shell a long leg with little turn
# available walks into the wall and cannot come back. Long legs and tight corners are the same
# constraint pulling opposite ways, so the ladder shortens the legs as it tightens the doors and
# lets the corners open. Every row is swept below; the caps are still what the walk enforces.

STATION_COUNT = 14          # EndConditionOverridesSO.DefaultBreakwaterStationTarget
COMEBACK_RATE = 0.7         # ArcadeGameBreakwater.ComebackRatePerScoreDeficit

SHELL_INNER = 420.0
SHELL_OUTER = 1080.0        # 0.9 x the CapsuleMembrane's authored 1200
FIRST_STATION_DISTANCE = 660.0
SPAWN_RING_RADIUS = 480.0

ATTEMPTS_PER_STATION = 32

# A hard seed is RESEEDED, never shortened. The residual failure rate at 32 attempts is ~0.1%
# per seed, and halving the station count (Switchback's back-off) answers a one-in-a-thousand
# roll by shipping that match a different, shorter race. Three reseeds take it to ~1e-9 and keep
# every match the same length; halving stays underneath as the last resort so generation can
# never return empty, which would hang the turn outright.
RESEED_ATTEMPTS = 3


def min_separation(port, min_step):
    """DERIVED, never authored. Two stations must not come close enough to read as one place -
    four port radii is 'the mouths are clearly separate' - but the value can never reach the
    minimum leg, or the predecessor test starves the walk (see the module docstring)."""
    return min(0.9 * min_step, 4.0 * port)


# ── The Sparrow's turning circle ─────────────────────────────────────────────────────────────
# v = XDiff * DefaultThrottleScaler * boostMultiplier * Mult(Time) + MinimumSpeed
# omega = RotationThrottleScaler * v + PitchScaler   (degrees/second)
# R = v / omega
DEFAULT_THROTTLE_SCALER = 25.0
BOOST_MULTIPLIER = 5.0
MINIMUM_SPEED = 10.0
ROTATION_THROTTLE_SCALER = 0.1
PITCH_SCALER = 80.0


def speed(xdiff, time_mult=1.0, boosting=True):
    b = BOOST_MULTIPLIER if boosting else 1.0
    return xdiff * DEFAULT_THROTTLE_SCALER * b * time_mult + MINIMUM_SPEED


def turn_rate(v):
    return ROTATION_THROTTLE_SCALER * v + PITCH_SCALER


def min_turn_radius(v):
    """R = v / omega with omega in radians/second."""
    return v / math.radians(turn_rate(v))


# The mouse scheme pins XDiff at the NEUTRAL 0.5 - a one-thumb pilot cannot throttle - so the
# mouse rows bracket what a desktop player actually flies, and the pad rows what a pad player
# does. The last row is the transient ceiling: full throttle at the top of the overcharge band,
# the state a racer is least able to correct in, and the one the hard flyability proof uses.
FLIGHT_STATES = [
    ("mouse cruise (XDiff 0.5, no boost)", speed(0.5, boosting=False)),
    ("mouse boost (XDiff 0.5)", speed(0.5)),
    ("pad cruise (XDiff 0.75, no boost)", speed(0.75, boosting=False)),
    ("pad boost, Time rest (XDiff 0.75)", speed(0.75)),
    ("pad boost, Time 10 (comeback)", speed(0.75, 1.45)),
    ("transient ceiling (XDiff 1, Time 15)", speed(1.0, 1.8)),
]
CEILING_TURN_RADIUS = min_turn_radius(FLIGHT_STATES[-1][1])


# ── One station, closed form ────────────────────────────────────────────────────────────────

def plug_runs(port):
    """Every bar RUN in the plug, as lengths. Three rakes of parallel lines at 0/60/120 degrees,
    offset half a pitch so no line runs through the eye; each line clipped to the annulus
    [EYE_RADIUS, port], which splits a line that passes the eye into two runs."""
    runs = []
    k = 0
    while (k + 0.5) * RAKE_PITCH < port - RAKE_EDGE_MARGIN:
        d = (k + 0.5) * RAKE_PITCH
        half = math.sqrt(port * port - d * d)
        for _sign in (+1, -1):                       # both sides of the rake's centre line
            if d < EYE_RADIUS:
                inner = math.sqrt(EYE_RADIUS * EYE_RADIUS - d * d)
                runs.append(half - inner)
                runs.append(half - inner)            # one run each side of the eye
            else:
                runs.append(2.0 * half)
        k += 1
    return runs * len(RAKE_ANGLES)


def plug_bars(port):
    """(bar count, total bar length, longest single bar). A run is cut into EQUAL bars no longer
    than MAX_BAR_LEN, so no emitted z ever reaches the prism clamp."""
    runs = plug_runs(port)
    count = 0
    longest = 0.0
    for r in runs:
        n = max(1, math.ceil(r / MAX_BAR_LEN))
        count += n
        longest = max(longest, r / n)
    return count, sum(runs), longest


def dish_rings(port):
    """Radii of the dish's concentric rings, from the port rim out to R_dish."""
    outer = DISH_RATIO * port
    rings = []
    r = port
    while r <= outer + 1e-9:
        rings.append(r)
        r += DISH_PITCH
    return rings


def dish_plates(port):
    return sum(max(1, round(2.0 * math.pi * r / DISH_PITCH)) for r in dish_rings(port))


def _hash01(ring, plate):
    """Bit-exact mirror of BreakwaterStationBuilder.Hash01. The dish's plate jitter is a pure
    function of (ring, plate) with NO station index, which is precisely what lets this file price
    the dish EXACTLY rather than at its nominal volume - the arithmetic here is the arithmetic
    that ships, so the PhaseThresholds below describe the arena rather than approximating it."""
    m32 = 0xFFFFFFFF
    h = ((ring * 0x9E3779B9) & m32) ^ ((plate * 0x85EBCA6B) & m32) ^ 0x7F4A7C15
    h &= m32
    h ^= h >> 15; h = (h * 0x2545F491) & m32
    h ^= h >> 13; h = (h * 0xC2B2AE35) & m32
    h ^= h >> 16
    return (h & 0xFFFFFF) / float(0x1000000)


def dish_volume(port):
    """EXACT dish volume, jitter included. One factor k scales all three axes of a plate, so a
    plate's volume is nominal * k**3; k is mean-zero per axis but k**3 is not, and summing the
    real draws is the only way the number is the shipped one."""
    base = DISH_PLATE[0] * DISH_PLATE[1] * DISH_PLATE[2]
    total = 0.0
    for ring, r in enumerate(dish_rings(port)):
        plates = max(1, round(2.0 * math.pi * r / DISH_PITCH))
        for p in range(plates):
            k = 1.0 + (2.0 * _hash01(ring, p) - 1.0) * DISH_JITTER
            total += base * k ** 3
    return total


def dish_extreme_axes(port):
    """The smallest and largest axis the dish actually emits, for the prism-clamp proof. The
    jitter's authored range is not the same question as the values it really takes."""
    lo, hi = 1e9, -1e9
    for ring, r in enumerate(dish_rings(port)):
        plates = max(1, round(2.0 * math.pi * r / DISH_PITCH))
        for p in range(plates):
            k = 1.0 + (2.0 * _hash01(ring, p) - 1.0) * DISH_JITTER
            lo = min(lo, DISH_PLATE[2] * k)
            hi = max(hi, DISH_PLATE[0] * k)
    return lo, hi


def station_totals(port):
    bars, barlen, longest = plug_bars(port)
    plates = dish_plates(port)
    vol = (BAR_CROSS * BAR_CROSS * barlen
           + COLLAR_COUNT * COLLAR_CUBE ** 3
           + dish_volume(port))
    return dict(
        port=port,
        # LINES per rake, both signs - not runs. A line through the eye is split into TWO
        # runs, so runs/2 undercounts exactly the lines nearest the centre.
        lines_per_rake=2 * sum(1 for k in range(200)
                               if (k + 0.5) * RAKE_PITCH < port - RAKE_EDGE_MARGIN),
        bars=bars, longest_bar=longest,
        rings=len(dish_rings(port)), plates=plates,
        collar=COLLAR_COUNT,
        prisms=bars + COLLAR_COUNT + plates,
        volume=vol,
    )


def shoal_totals(station_count):
    legs = station_count - 1
    prisms = legs * SHOAL_CLUSTERS_PER_LEG * SHOAL_PRISMS_PER_CLUSTER
    return prisms, prisms * SHOAL_CUBE ** 3


# ── The course walk, reproduced bit for bit ─────────────────────────────────────────────────

class Rng:
    """The specified xorshift32 BreakwaterCourse.Rng uses. Reproduced exactly so this model and
    the shipped C# generate the SAME course for a seed - which is what lets the proofs below be
    statements about the arena that actually ships."""

    def __init__(self, seed):
        self.s = seed & 0xFFFFFFFF
        if self.s == 0:
            self.s = 0x9E3779B9

    def next_u32(self):
        x = self.s
        x ^= (x << 13) & 0xFFFFFFFF
        x ^= x >> 17
        x ^= (x << 5) & 0xFFFFFFFF
        self.s = x & 0xFFFFFFFF
        return self.s

    def unit(self):
        return self.next_u32() / 4294967296.0

    def range(self, lo, hi):
        return lo + (hi - lo) * self.unit()


def _norm(v):
    m = math.sqrt(sum(c * c for c in v))
    return (v[0] / m, v[1] / m, v[2] / m) if m > 1e-10 else (0.0, 0.0, 1.0)


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _mul(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _len(a):
    return math.sqrt(_dot(a, a))


def _perp(v):
    ref = (1.0, 0.0, 0.0) if abs(v[0]) < 0.9 else (0.0, 1.0, 0.0)
    return _norm(_cross(v, ref))


def _rotate_about(v, axis, deg):
    """Rodrigues. Same formula Quaternion.AngleAxis applies."""
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return _add(_add(_mul(v, c), _mul(_cross(axis, v), s)),
                _mul(axis, _dot(axis, v) * (1.0 - c)))


def _deflect(v, max_deg, rng):
    """A cone deflection of at most max_deg. The cone angle is drawn as max*sqrt(u), NEVER max*u -
    a linear draw crowds every deflection near zero and the course comes out straight."""
    axis = _rotate_about(_perp(v), v, rng.range(0.0, 360.0))
    return _norm(_rotate_about(v, _norm(axis), max_deg * math.sqrt(rng.unit())))


def _clamp_turn(prev, cand, max_deg):
    """Clamp cand to at most max_deg away from prev, about the axis between them."""
    d = max(-1.0, min(1.0, _dot(prev, cand)))
    ang = math.degrees(math.acos(d))
    if ang <= max_deg:
        return cand
    axis = _cross(prev, cand)
    if _len(axis) < 1e-8:
        axis = _perp(prev)
    return _norm(_rotate_about(prev, _norm(axis), max_deg))


def generate(seed, cfg, station_count=STATION_COUNT):
    """The constructive backtracking walk. Returns a list of (position, axis) or None.

    The heading advances ONLY when a station is PLACED, and the walk BACKTRACKS on failure.
    Letting the heading rotate between failed attempts is the tempting shortcut and is wrong:
    two 55-degree rotations compose into a 110-degree hairpin between two PLACED stations."""
    rng = Rng(seed)
    sep = min_separation(cfg['port'], cfg['min_step'])

    first = _mul((0.0, 1.0, 0.0), FIRST_STATION_DISTANCE)
    pts = [first]
    heading = _norm(_deflect((0.0, 1.0, 0.0), cfg['max_turn'], rng))
    headings = [heading]

    budget = station_count * ATTEMPTS_PER_STATION * 4
    while len(pts) < station_count and budget > 0:
        placed = False
        for _ in range(ATTEMPTS_PER_STATION):
            budget -= 1
            if budget <= 0:
                break
            step = rng.range(cfg['min_step'], cfg['max_step'])
            cand_dir = _clamp_turn(headings[-1],
                                   _deflect(headings[-1], cfg['max_turn'], rng),
                                   cfg['max_turn'])
            cand = _add(pts[-1], _mul(cand_dir, step))
            r = _len(cand)
            if r < SHELL_INNER or r > SHELL_OUTER:
                # Steer at the shell midline, but STILL through the same turn cap - the wall is
                # not an excuse to exceed it.
                mid = _mul(_norm(cand), (SHELL_INNER + SHELL_OUTER) * 0.5)
                cand_dir = _clamp_turn(headings[-1], _norm(_sub(mid, pts[-1])), cfg['max_turn'])
                cand = _add(pts[-1], _mul(cand_dir, step))
                r = _len(cand)
                if r < SHELL_INNER or r > SHELL_OUTER:
                    continue
            if any(_len(_sub(p, cand)) < sep for p in pts):
                continue
            pts.append(cand)
            headings.append(cand_dir)
            placed = True
            break
        if not placed:
            if len(pts) <= 1:
                return None
            pts.pop()
            headings.pop()

    if len(pts) < station_count:
        return None

    # Axis = the corner's FLOW BISECTOR, jittered from what is LEFT of the presentation cap.
    axes = []
    for i in range(len(pts)):
        incoming = headings[i]
        outgoing = headings[i + 1] if i + 1 < len(headings) else headings[i]
        bis = _norm(_add(incoming, outgoing))
        half_turn = math.degrees(math.acos(max(-1.0, min(1.0, _dot(bis, incoming)))))
        allowed = max(0.0, min(cfg['jitter'], cfg['present'] - half_turn))
        axes.append(_norm(_deflect(bis, allowed, rng)) if allowed > 0.01 else bis)
    return list(zip(pts, axes, headings[:len(pts)]))


# ── Proofs ──────────────────────────────────────────────────────────────────────────────────

def sweep(seeds=400):
    """Run the real walk and MEASURE the three things a table cannot tell you."""
    report = []
    for idx, cfg in enumerate(INTENSITIES, start=1):
        fails = 0
        worst_turn = 0.0
        worst_present = 0.0
        min_sep_seen = 1e9
        min_leg = 1e9
        dubins_violations = 0
        max_stations_in_lod = 0
        for s in range(1, seeds + 1):
            course = generate(s * 7919 + idx, cfg)
            if course is None:
                fails += 1
                continue
            pts = [c[0] for c in course]
            axes = [c[1] for c in course]
            heads = [c[2] for c in course]

            for i in range(len(pts)):
                r = _len(pts[i])
                assert SHELL_INNER - 1e-6 <= r <= SHELL_OUTER + 1e-6, f"shell {r}"
                for j in range(i + 1, len(pts)):
                    min_sep_seen = min(min_sep_seen, _len(_sub(pts[i], pts[j])))

            for i in range(1, len(pts)):
                leg = _len(_sub(pts[i], pts[i - 1]))
                min_leg = min(min_leg, leg)
                turn = math.degrees(math.acos(max(-1.0, min(1.0, _dot(heads[i - 1], heads[i])))))
                worst_turn = max(worst_turn, turn)
                # Dubins: a corner is holdable only if the leg exceeds the chord the turning
                # circle needs. Checked at the TRANSIENT CEILING - the state a racer is least
                # able to correct in.
                if leg <= 2.0 * CEILING_TURN_RADIUS * math.sin(math.radians(turn)):
                    dubins_violations += 1

            for i in range(len(pts)):
                incoming = heads[i]
                pres = math.degrees(math.acos(min(1.0, abs(_dot(_norm(axes[i]), incoming)))))
                worst_present = max(worst_present, pres)

            # THE COLLIDER MEASUREMENT. Sample along every leg and count how many stations fall
            # inside the collider-LOD radius at once. This is the number the budget is about,
            # and it is a property of how the walk FOLDS, not of the authored separation.
            for i in range(1, len(pts)):
                for t in range(0, 11):
                    p = _add(pts[i - 1], _mul(_sub(pts[i], pts[i - 1]), t / 10.0))
                    n = sum(1 for q in pts if _len(_sub(q, p)) <= LOD_RADIUS)
                    max_stations_in_lod = max(max_stations_in_lod, n)

        report.append(dict(intensity=idx, fails=fails, worst_turn=worst_turn,
                           worst_present=worst_present, min_sep=min_sep_seen, min_leg=min_leg,
                           dubins_violations=dubins_violations,
                           max_stations_in_lod=max_stations_in_lod))
    return report


def check_scales():
    """Every emitted axis against PrismScaleAnimator's silent per-axis clamp."""
    problems = []

    def chk(name, axes):
        for a in axes:
            if a < MIN_SCALE or a > MAX_SCALE:
                problems.append(f"{name}: axis {a:.3f} outside [{MIN_SCALE}, {MAX_SCALE}]")

    for cfg in INTENSITIES:
        _, _, longest = plug_bars(cfg['port'])
        chk(f"plug bar (port {cfg['port']:.0f})", [BAR_CROSS, BAR_CROSS, longest])
    chk("collar", [COLLAR_CUBE] * 3)
    for cfg in INTENSITIES:
        lo, hi = dish_extreme_axes(cfg['port'])
        chk(f"dish plate REAL extremes (port {cfg['port']:.0f})", [lo, hi])
    chk("shoal", [SHOAL_CUBE] * 3)
    return problems


def arena_totals(intensity_index):
    cfg = INTENSITIES[intensity_index]
    st = station_totals(cfg['port'])
    sp, sv = shoal_totals(STATION_COUNT)
    return dict(
        station=st,
        prisms=st['prisms'] * STATION_COUNT + sp,
        volume=st['volume'] * STATION_COUNT + sv,
        shoal_prisms=sp, shoal_volume=sv,
    )


def phase_thresholds(baseline_volume, baseline_prisms):
    """Volume is the spine; count is the rare frenzy/perf backstop. Deltas over the MEASURED
    baseline, with exits 10% under their enters so a trail-caused frenzy always releases."""
    return dict(
        RestlessEnterVolume=baseline_volume + 120000.0,
        RestlessExitVolume=baseline_volume + 108000.0,
        FrenzyEnterVolume=baseline_volume + 300000.0,
        FrenzyExitVolume=baseline_volume + 270000.0,
        RestlessEnterCount=baseline_prisms + 700,
        RestlessExitCount=baseline_prisms + 500,
        FrenzyEnterCount=baseline_prisms + 3600,
        FrenzyExitCount=baseline_prisms + 3000,
    )


def main():
    fail = []
    print("BREAKWATER ARENA MODEL")
    print("=" * 78)

    print("\nThe Sparrow's turning circle (R = v / omega)")
    print(f"{'state':<40}{'v (u/s)':>10}{'omega':>10}{'R (u)':>10}")
    for name, v in FLIGHT_STATES:
        print(f"{name:<40}{v:>10.1f}{turn_rate(v):>10.2f}{min_turn_radius(v):>10.1f}")
    print(f"\nTransient-ceiling turning radius used for the hard flyability proof: "
          f"{CEILING_TURN_RADIUS:.1f} u")

    print("\nPer-station geometry")
    hdr = f"{'':<22}" + "".join(f"{'I'+str(i+1):>12}" for i in range(4))
    print(hdr)
    rows = [station_totals(c['port']) for c in INTENSITIES]
    for key, label in [('port', 'R_port'), ('lines_per_rake', 'plug lines/rake'),
                       ('bars', 'plug bars'), ('longest_bar', 'longest bar'),
                       ('rings', 'dish rings'), ('plates', 'dish plates'),
                       ('collar', 'collar blocks'), ('prisms', 'prisms/station'),
                       ('volume', 'volume/station')]:
        print(f"{label:<22}" + "".join(f"{r[key]:>12,.0f}" for r in rows))

    print("\nArena totals (14 stations + shoals)")
    tot = [arena_totals(i) for i in range(4)]
    print(f"{'arena prisms':<22}" + "".join(f"{t['prisms']:>12,}" for t in tot))
    print(f"{'arena volume':<22}" + "".join(f"{t['volume']:>12,.0f}" for t in tot))
    print(f"{'x nominal (16/prism)':<22}"
          + "".join(f"{t['volume']/(t['prisms']*NOMINAL_PRISM_VOLUME):>12.1f}" for t in tot))
    print(f"\nShoals: {tot[0]['shoal_prisms']} prisms / {tot[0]['shoal_volume']:,.0f} volume "
          f"(constant at every intensity)")

    print("\nDerived minimum separation (must stay BELOW the minimum leg)")
    for i, c in enumerate(INTENSITIES, start=1):
        sep = min_separation(c['port'], c['min_step'])
        ok = sep < c['min_step']
        print(f"  I{i}: sep {sep:7.1f}  min leg {c['min_step']:7.1f}   {'OK' if ok else 'STARVES THE WALK'}")
        if not ok:
            fail.append(f"I{i}: MinSeparation {sep} >= MinStep {c['min_step']}")

    print("\nPrism scale clamp [%.1f, %.1f]" % (MIN_SCALE, MAX_SCALE))
    problems = check_scales()
    if problems:
        for p in problems:
            print("  FAIL " + p)
        fail.extend(problems)
    else:
        print("  every emitted axis is inside the clamp (including the dish plate's jitter ends)")

    print("\nWalking 400 seeds x 4 intensities ...")
    rep = sweep()
    for r in rep:
        c = INTENSITIES[r['intensity'] - 1]
        print(f"\n  I{r['intensity']}  (port {c['port']:.0f}, legs {c['min_step']:.0f}-{c['max_step']:.0f}, "
              f"turn cap {c['max_turn']:.0f}, present cap {c['present']:.0f})")
        print(f"    generation failures      : {r['fails']} / 400")
        print(f"    worst corner             : {r['worst_turn']:.1f} deg (cap {c['max_turn']:.0f})")
        print(f"    worst presentation       : {r['worst_present']:.1f} deg (cap {c['present']:.0f})")
        print(f"    closest two stations     : {r['min_sep']:.1f} u "
              f"(derived floor {min_separation(c['port'], c['min_step']):.1f})")
        print(f"    shortest leg             : {r['min_leg']:.1f} u")
        print(f"    Dubins violations        : {r['dubins_violations']} "
              f"(leg <= 2R.sin(turn) at R={CEILING_TURN_RADIUS:.1f})")
        print(f"    MAX stations inside LOD  : {r['max_stations_in_lod']} "
              f"(radius {LOD_RADIUS:.0f} u)")
        if r['fails']:
            fail.append(f"I{r['intensity']}: {r['fails']} generation failures")
        if r['worst_turn'] > c['max_turn'] + 0.01:
            fail.append(f"I{r['intensity']}: turn cap exceeded ({r['worst_turn']:.2f})")
        if r['worst_present'] > c['present'] + 0.01:
            fail.append(f"I{r['intensity']}: presentation cap exceeded ({r['worst_present']:.2f})")
        if r['dubins_violations']:
            fail.append(f"I{r['intensity']}: {r['dubins_violations']} unflyable corners")

    print("\nCollider budget (MEASURED worst case, not asserted)")
    print(f"{'':<26}" + "".join(f"{'I'+str(i+1):>12}" for i in range(4)))
    for i, r in enumerate(rep):
        pass
    worst_line = []
    for i in range(4):
        n = rep[i]['max_stations_in_lod']
        # A leg's shoals that can share the radius: at most half the clusters of the two legs.
        shoal_in = SHOAL_CLUSTERS_PER_LEG * SHOAL_PRISMS_PER_CLUSTER
        worst_line.append(n * rows[i]['prisms'] + shoal_in)
    print(f"{'stations in radius':<26}" + "".join(f"{rep[i]['max_stations_in_lod']:>12}" for i in range(4)))
    print(f"{'active prism colliders':<26}" + "".join(f"{w:>12,}" for w in worst_line))
    print(f"{'against band':<26}" + "".join(f"{COLLIDER_BAND:>12,}" for _ in range(4)))
    for i, w in enumerate(worst_line, start=1):
        if w > COLLIDER_BAND:
            fail.append(f"I{i}: {w} active colliders over the {COLLIDER_BAND} band")
    print("\n  Zero ALWAYS-ON mesh colliders are authored: every prism is Plain or Danger, both")
    print("  LOD-cullable. The 14 switch rings carry no collider at all.")

    print("\nPhase thresholds (volume is the spine; count is the backstop)")
    for i in range(4):
        t = phase_thresholds(tot[i]['volume'], tot[i]['prisms'])
        print(f"  I{i+1}: baseline {tot[i]['volume']:>10,.0f}   "
              f"RestlessEnter {t['RestlessEnterVolume']:>10,.0f}   "
              f"FrenzyEnter {t['FrenzyEnterVolume']:>10,.0f}")
        if t['RestlessEnterVolume'] <= tot[i]['volume']:
            fail.append(f"I{i+1}: RestlessEnterVolume <= baseline")

    print("\nBounds that make the intensity axis honest")
    tight = INTENSITIES[-1]['port']
    wide = INTENSITIES[0]['port']
    print(f"  tightest port {tight:.0f} vs hull radius {SPARROW_HULL_RADIUS:.2f} -> "
          f"{tight/SPARROW_HULL_RADIUS:.2f}x clearance")
    print(f"  eye {EYE_RADIUS:.0f} vs hull radius {SPARROW_HULL_RADIUS:.2f} -> "
          f"{EYE_RADIUS/SPARROW_HULL_RADIUS:.2f}x clearance")
    print(f"  tightest port {tight:.0f} vs resting-Charge blast {BLAST_RADIUS_REST:.0f} -> "
          f"one rocket clears the whole plug at I4: {tight <= BLAST_RADIUS_REST}")
    print(f"  widest port {wide:.0f} vs resting-Charge blast {BLAST_RADIUS_REST:.0f} -> "
          f"aim CHOICE exists at I1: {wide > BLAST_RADIUS_REST}")
    if EYE_RADIUS <= SPARROW_HULL_RADIUS:
        fail.append("eye radius does not clear the hull")
    if tight > BLAST_RADIUS_REST:
        fail.append("I4 port is wider than the resting-Charge blast: a door needs an upgrade")

    print("\nComeback rate")
    lv = 0.25 * STATION_COUNT * COMEBACK_RATE
    print(f"  a quarter-of-target deficit ({0.25*STATION_COUNT:.1f} stations) buys "
          f"{lv:.2f} element levels")
    if lv < 1.0:
        fail.append(f"comeback rate {COMEBACK_RATE} buys only {lv:.2f} levels at a quarter deficit")

    print("\n" + "=" * 78)
    if fail:
        print("FAILED:")
        for f in fail:
            print("  - " + f)
        return 1
    print("All proofs pass.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
