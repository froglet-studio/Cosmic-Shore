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
DISH_HALF_ANGLE_DEG = 22.0 # cone half-angle from the AXIS; sets the axial profile only,
                           # so it prices nothing - it is here for station_reach()

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
    dict(port=60.0, min_step=300.0, max_step=433.0, max_turn=55.0, jitter=28.0, present=54.0),
    dict(port=50.0, min_step=300.0, max_step=407.0, max_turn=65.0, jitter=34.0, present=58.0),
    dict(port=42.0, min_step=300.0, max_step=380.0, max_turn=75.0, jitter=40.0, present=62.0),
]

# The leg/turn ladder above is MEASURED, not chosen. An earlier cut ran I1 at legs 340-520 with a
# 40-degree cap on the reasoning that the gentlest corners belong at the easiest level, and it
# failed to generate on 21% of seeds: inside a 660-unit-thick shell a long leg with little turn
# available walks into the wall and cannot come back. Long legs and tight corners are the same
# constraint pulling opposite ways, so the ladder shortens the legs as it tightens the doors and
# lets the corners open. Every row is swept below; the caps are still what the walk enforces.

STATION_COUNT = 15          # EndConditionOverridesSO.DefaultBreakwaterStationTarget
CIRCUIT_COUNT = STATION_COUNT - 1   # the closed loop; station 0 is the START GATE

# ── Laps: the course is a START GATE plus a CLOSED CIRCUIT ──────────────────────────────────
# Station 0 sits on the polar axis with its axis ALONG that pole, so every spawn pad is
# equidistant AND face-on (measured spread 0.0000 on both). Stations 1..14 are a closed circuit
# flown in ONE direction and repeated per lap: after the last one a pilot continues into the
# first, forward, rather than reversing back through the rings they came.
#
# The start gate is not decoration, it is what makes a circuit FAIR. Fairness today is "pilots
# spawn on an equatorial ring, gate 1 sits on that ring's pole, so every pad is equidistant".
# Make gate 1 the first gate of a closed loop instead and the approach is AXIAL while a closed
# loop's tangent at an axial point is PERPENDICULAR - measured over 400 seeds x 4 intensities,
# presentation at that gate ranged 12.8-90.0 deg with up to 73.5 deg of spread ACROSS PADS. One
# pilot gets a 14 deg face-on approach and another 90 deg edge-on to the same gate.
#
# That is structural rather than tuning: inside the 420..1080 shell no circle can cross the
# polar axis at radius >= SHELL_INNER with a near-axial tangent, because c + R <= 1080,
# R^2 - c^2 >= 420^2 and c/R >= 0.866 are jointly unsatisfiable.
LAPS = 2

def crossing_target(stations=STATION_COUNT, laps=LAPS):
    """Total ring crossings a race is: the start gate once, then the circuit every lap.
    15 stations over two laps = 1 + 14*2 = 29."""
    if stations <= 1:
        return max(1, stations)
    return 1 + (stations - 1) * max(1, laps)


def ring_for_crossing(crossing, stations=STATION_COUNT):
    """Which ring the n-th crossing is - the fold that keeps ONE replicated int carrying the
    whole race. Crossing 0 is the start gate; every crossing after it walks the circuit
    forward and wraps, so the fold is a plain modulo rather than a zigzag."""
    if stations <= 1:
        return 0
    if crossing <= 0:
        return 0
    return 1 + ((crossing - 1) % (stations - 1))


COMEBACK_RATE = 0.35        # ArcadeGameBreakwater.ComebackRatePerScoreDeficit
#
# HALVED WITH THE TARGET, and that is the trap Dog Fight, Bends and Wildlife Liberation each
# recorded independently: `bonusLevels = deficit x rate`, so the rate is a function of the TARGET
# and re-targeting a mode silently re-tunes its comeback. Two laps took the target 14 -> 27, which
# at the old 0.7 would have handed a quarter-of-target deficit 4.7 element levels - nearly half the
# sustained band, for being a quarter behind. 0.35 holds the shipped 2.4 and lands on Dog Fight's
# curve, the nearest sibling by structure.

SHELL_INNER = 420.0
SHELL_OUTER = 1080.0        # 0.9 x the CapsuleMembrane's authored 1200
# The start gate's distance along the pole is SOLVED, not authored: it is wherever the polar axis
# is exactly one chord from the circuit's entry station. The authored 660 this replaced was read by
# nothing once the circuit landed, and a config that cannot affect anything is worse than absent.
SPAWN_RING_RADIUS = 480.0

# The spawn ring's pad BEARINGS, as the union over every seat count the card allows (2, 3, 4).
# CellSpawnFormation.EquatorialRing puts slot i of n at i * 360/n degrees from +Z on y = 0, so the
# union is {0, 90, 120, 180, 240, 270}. Taking the UNION rather than the live roster is what makes
# the course independent of how many pilots turned up: the geometry is generated once, broadcast
# once, and must not change if a seat is added between generation and spawn.
SPAWN_PAD_BEARINGS_DEG = (0.0, 90.0, 120.0, 180.0, 240.0, 270.0)

# No station's structure may reach a spawn pad. Four hull radii of air beyond the station's own
# bounding sphere - measured, it costs the walk nothing (0 failures in 800 seeds x 4 intensities
# at every clearance from 0 to 60), and without it a pilot spawns INSIDE the weave on about one
# course in two hundred.
SPARROW_HULL_RADIUS = 12.32
SPAWN_PAD_CLEARANCE = 4.0 * SPARROW_HULL_RADIUS

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
    [EYE_RADIUS, port], which splits a line that passes the eye into two runs.

    THE CLIP IS AGAINST THE BAR'S NEAR EDGE, NOT ITS CENTRELINE. A bar is BAR_CROSS wide, so a
    line whose centre stands exactly EYE_RADIUS off centre still puts BAR_CROSS/2 of prism inside
    the hole. Clipping on `d` alone made the k=1 line (d = 18.0, identically EYE_RADIUS) an
    unsplit full chord and left six bar bodies straddling 16.5..19.5 - a hexagon of inradius 16.5
    at every station, against a keystone collar whose inner faces sit at exactly 18. The station
    advertised a mouth it did not have. Clipping the near edge (`d - BAR_CROSS/2`) and taking the
    eye's half-chord THERE puts the nearest corner of the nearest bar at exactly EYE_RADIUS."""
    runs = []
    k = 0
    while (k + 0.5) * RAKE_PITCH < port - RAKE_EDGE_MARGIN:
        d = (k + 0.5) * RAKE_PITCH
        near = max(0.0, d - BAR_CROSS * 0.5)         # the bar's inner face, not its centreline
        half = math.sqrt(port * port - d * d)
        for _sign in (+1, -1):                       # both sides of the rake's centre line
            if near < EYE_RADIUS:
                inner = math.sqrt(EYE_RADIUS * EYE_RADIUS - near * near)
                runs.append(half - inner)
                runs.append(half - inner)            # one run each side of the eye
            else:
                runs.append(2.0 * half)
        k += 1
    return runs * len(RAKE_ANGLES)


def eye_clearance(port):
    """The radius of the largest disc in the port plane that NO plug bar body intrudes on.

    For a line at perpendicular offset d, the bar body's nearest point to centre is at
    (perp = d - BAR_CROSS/2, along = the run's inner end), so this is exactly what plug_runs'
    near-edge clip is written to hold at EYE_RADIUS."""
    worst = float('inf')
    k = 0
    while (k + 0.5) * RAKE_PITCH < port - RAKE_EDGE_MARGIN:
        d = (k + 0.5) * RAKE_PITCH
        near = max(0.0, d - BAR_CROSS * 0.5)
        along = math.sqrt(EYE_RADIUS * EYE_RADIUS - near * near) if near < EYE_RADIUS else 0.0
        worst = min(worst, math.hypot(near, along))
        k += 1
    return worst


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
    # ONE leg leaves every station, the circuit's CLOSING leg included - see
    # BreakwaterCourseSettings.NextStation. A linear walk had station_count - 1 legs; a start gate
    # plus a closed circuit has station_count.
    legs = station_count
    prisms = legs * SHOAL_CLUSTERS_PER_LEG * SHOAL_PRISMS_PER_CLUSTER
    return prisms, prisms * SHOAL_CUBE ** 3


# ── The course walk, reproduced bit for bit ─────────────────────────────────────────────────

def spawn_pads():
    """World positions of every spawn pad the course must keep clear of."""
    return [(SPAWN_RING_RADIUS * math.sin(math.radians(b)), 0.0,
             SPAWN_RING_RADIUS * math.cos(math.radians(b))) for b in SPAWN_PAD_BEARINGS_DEG]


def station_reach(port):
    """Bounding radius of one station's geometry about its own centre.

    The rim of the dish is the farthest point: it sits at radius DISH_RATIO * port in the port
    plane and (DISH_RATIO - 1) * port / tan(a) BEHIND it along the axis. A sphere is deliberately
    coarse - this number only ever REJECTS a candidate, so erring outward costs a little walk
    freedom and never lets prism near a pad."""
    inv_tan = math.cos(math.radians(DISH_HALF_ANGLE_DEG)) / math.sin(math.radians(DISH_HALF_ANGLE_DEG))
    plate_half = math.sqrt((DISH_PLATE[0] * 0.5) ** 2 + (DISH_PLATE[1] * 0.5) ** 2 +
                           (DISH_PLATE[2] * 0.5) ** 2)
    return port * math.hypot(DISH_RATIO, (DISH_RATIO - 1.0) * inv_tan) + plate_half


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


PADS = spawn_pads()


# ── The circuit, constructed rather than searched ────────────────────────────────────────────
#
# A closing WALK cannot be steered home (measured: 55-76% of seeds failed). A loop is therefore
# not searched for, it is CONSTRUCTED, and every constraint becomes an analytic bound.
#
# 1. A ZIGZAG RING hits an exact turn angle in closed form. For
#        P_i = R(cos t_i, sin t_i) +/- z*axis        (N even, so the zigzag closes)
#    consecutive legs alternate s_i +/- 2z*axis, so
#        cos(turn) = (|s|^2 cos(phi) - 4z^2) / (|s|^2 + 4z^2),      phi = 2*pi/N
#    and solving for a target chord and turn gives the mode's own intensity dial directly:
#        |s| = chord * sqrt((1 + cos T) / (1 + cos phi))    <- ALONG track
#        2z  = sqrt(chord^2 - |s|^2)                        <- ACROSS track
#    Verified: every corner lands on T to 1e-6.
#
# 2. Wander is LOW-FREQUENCY (harmonics k = 1, 2 in radius and out-of-plane). A smooth
#    deformation moves neighbouring stations TOGETHER, so it changes the loop's outline a lot
#    while barely moving adjacent spacing. Per-station jitter does the opposite: it had to be cut
#    to ~20% of nominal to fit the chord band, which made every course look like every other.
#
# 3. Amplitude SHRINKS until the caps hold. At amplitude 0 the loop is a regular zigzag ring,
#    which is legal by construction - so the shrink always terminates. That is what replaces
#    rejection sampling, and it is why there is no failure rate to report.

CIRCUIT_DESIGN_TURN = 0.85   # the base ring aims here, leaving the rest of the cap for wander
ENTRY_FACTORS = (0.80, 0.86, 0.92, 0.97)
JOIN_SCORE_QUANTUM = 0.1     # degrees; ties resolve on index so float noise cannot flip a branch


def _ring_geometry(chord, turn_deg, n):
    """|s| along track and 2z across track for a zigzag ring that hits turn_deg exactly."""
    phi = 2.0 * math.pi / n
    c = math.cos(math.radians(turn_deg))
    s_len = chord * math.sqrt((1.0 + c) / (1.0 + math.cos(phi)))
    across = math.sqrt(max(0.0, chord * chord - s_len * s_len))
    return s_len / (2.0 * math.sin(math.pi / n)), 0.5 * across


def _build_circuit(seed, cfg, amp, n=CIRCUIT_COUNT):
    """The closed loop, centred on the cell, before the start gate is solved for."""
    rng = Rng(seed)
    chord = 0.5 * (cfg['min_step'] + cfg['max_step'])
    radius, z = _ring_geometry(chord, cfg['max_turn'] * CIRCUIT_DESIGN_TURN, n)
    axis = _norm((rng.range(-1.0, 1.0), rng.range(-1.0, 1.0), rng.range(-1.0, 1.0)))
    u = _perp(axis)
    v = _cross(axis, u)
    r1, p1 = 0.20 * amp * rng.unit(), rng.range(0.0, 2.0 * math.pi)
    r2, p2 = 0.13 * amp * rng.unit(), rng.range(0.0, 2.0 * math.pi)
    o1, q1 = 0.42 * amp * rng.unit(), rng.range(0.0, 2.0 * math.pi)
    o2, q2 = 0.26 * amp * rng.unit(), rng.range(0.0, 2.0 * math.pi)
    jit = 0.05 * amp
    phi = 2.0 * math.pi / n
    pts = []
    for i in range(n):
        t = phi * i
        th = t + jit * phi * rng.range(-1.0, 1.0)
        r = radius * (1.0 + r1 * math.cos(t + p1) + r2 * math.cos(2.0 * t + p2)
                      + jit * rng.range(-1.0, 1.0))
        h = (z * (1.0 if i % 2 == 0 else -1.0) * (1.0 + jit * rng.range(-1.0, 1.0))
             + radius * (o1 * math.cos(t + q1) + o2 * math.cos(2.0 * t + q2)) * 0.35)
        pts.append(_add(_add(_mul(u, r * math.cos(th)), _mul(v, r * math.sin(th))), _mul(axis, h)))
    return pts


def _circuit_axes(pts):
    """Legs and flow-bisector axes for a CLOSED loop (index arithmetic wraps)."""
    n = len(pts)
    legs = [_norm(_sub(pts[(i + 1) % n], pts[i])) for i in range(n)]
    return legs


def _place_circuit(pts, legs, entry, bearing, chord, factor):
    """Spend the three rotational DOF: two put the entry station where a POLAR start gate is
    exactly one chord away, the third spins the loop so its tangent there already points down
    the entry leg. Returns (start_gate, ordered_points, ordered_legs) or None."""
    n = len(pts)
    p = pts[entry]
    r = _len(p)
    if r < 1e-6:
        return None
    alpha = math.asin(min(0.999, chord * factor / r))
    target = (math.sin(alpha) * math.cos(bearing), math.cos(alpha),
              math.sin(alpha) * math.sin(bearing))
    d = _norm(p)
    ax = _cross(d, target)
    sn = _len(ax)
    if sn > 1e-9:
        q = _mul(ax, 1.0 / sn)
        th = math.degrees(math.atan2(sn, _dot(d, target)))
        pts = [_rotate_about(x, q, th) for x in pts]
        legs = [_rotate_about(x, q, th) for x in legs]
    p = pts[entry]
    disc = chord * chord - (p[0] * p[0] + p[2] * p[2])
    if disc < 0.0:
        return None
    start = (0.0, p[1] - math.sqrt(disc), 0.0)
    if not (SHELL_INNER <= _len(start) <= SHELL_OUTER):
        return None
    nrm = _norm(pts[entry])
    ed = _norm(_sub(pts[entry], start))
    a1 = _sub(legs[entry], _mul(nrm, _dot(legs[entry], nrm)))
    a2 = _sub(ed, _mul(nrm, _dot(ed, nrm)))
    if _len(a1) > 1e-6 and _len(a2) > 1e-6:
        a1 = _norm(a1)
        a2 = _norm(a2)
        spin = math.degrees(math.atan2(_dot(_cross(a1, a2), nrm), _dot(a1, a2)))
        pts = [_rotate_about(x, nrm, spin) for x in pts]
        legs = [_rotate_about(x, nrm, spin) for x in legs]
    order = [pts[(entry + i) % n] for i in range(n)]
    lgs = [legs[(entry + i) % n] for i in range(n)]
    return start, order, lgs


def _circuit_ok(start, pts, legs, cfg):
    """Every cap the circuit must hold, plus the numbers the proofs report."""
    n = len(pts)
    chords = [_len(_sub(pts[(i + 1) % n], pts[i])) for i in range(n)]
    turns = [_angle_between(legs[(i - 1) % n], legs[i]) for i in range(n)]
    radii = [_len(p) for p in pts]
    inner = []
    for i in range(n):
        a = pts[i]
        ab = _sub(pts[(i + 1) % n], a)
        t = max(0.0, min(1.0, -_dot(a, ab) / max(1e-9, _dot(ab, ab))))
        inner.append(_len(_add(a, _mul(ab, t))))
    sep = min([_len(_sub(pts[i], pts[j])) for i in range(n) for j in range(i + 1, n)]
              + [_len(_sub(start, p)) for p in pts])
    entry_vec = _sub(pts[0], start)
    entry_len = _len(entry_vec)
    ed = _norm(entry_vec)
    join_start = _angle_between((0.0, 1.0, 0.0), ed)
    join_circuit = _angle_between(ed, legs[0])
    reach = station_reach(cfg['port'])
    pad_gap = min([_len(_sub(p, q)) - reach for p in pts for q in PADS]
                  + [_len(_sub(start, q)) - reach for q in PADS])
    ok = (min(chords) >= cfg['min_step'] and max(chords) <= cfg['max_step']
          and max(turns) <= cfg['max_turn']
          and min(radii) >= SHELL_INNER and max(radii) <= SHELL_OUTER
          and min(inner) >= SHELL_INNER
          and cfg['min_step'] <= entry_len <= cfg['max_step']
          and sep >= min_separation(cfg['port'], cfg['min_step'])
          and pad_gap >= SPAWN_PAD_CLEARANCE)
    return ok, dict(chords=chords, turns=turns, radii=radii, sep=sep, entry=entry_len,
                    join_start=join_start, join_circuit=join_circuit, pad_gap=pad_gap)


def _angle_between(a, b):
    return math.degrees(math.acos(max(-1.0, min(1.0, _dot(_norm(a), _norm(b))))))


def generate(seed, cfg, station_count=STATION_COUNT):
    """The whole course: a polar START GATE plus a closed circuit.

    Returns [(position, axis, heading), ...] with index 0 the start gate, or None. There is no
    failure mode by construction, so None means a caller passed something degenerate."""
    n = max(2, station_count - 1)
    chord = 0.5 * (cfg['min_step'] + cfg['max_step'])
    rng = Rng(seed ^ 0x5BF03635)
    offset = int(rng.unit() * n) % n
    bearing = rng.range(0.0, 2.0 * math.pi)
    for k in range(30):
        pts = _build_circuit(seed, cfg, 0.9 ** k, n)
        legs = _circuit_axes(pts)
        best = None
        for j in range(n):
            for factor in ENTRY_FACTORS:
                placed = _place_circuit(pts, legs, (offset + j) % n, bearing, chord, factor)
                if placed is None:
                    continue
                ok, m = _circuit_ok(placed[0], placed[1], placed[2], cfg)
                if not ok:
                    continue
                # Quantised so a float-noise tie cannot flip which branch wins.
                score = round(max(m['join_start'], m['join_circuit']) / JOIN_SCORE_QUANTUM)
                if best is None or score < best[0]:
                    best = (score, placed, m)
        if best is not None:
            start, order, lgs = best[1]
            return _finish_course(start, order, lgs, cfg, seed)
    return None


def _finish_course(start, pts, legs, cfg, seed):
    """Attach the axes. The start gate's axis is the POLE ITSELF - that is what makes every pad
    equidistant AND face-on, which equidistance alone never was."""
    rng = Rng(seed ^ 0x1B873593)
    n = len(pts)
    out = [(start, (0.0, 1.0, 0.0), _norm(_sub(pts[0], start)))]
    for i in range(n):
        incoming = legs[(i - 1) % n]
        outgoing = legs[i]
        bis = _norm(_add(incoming, outgoing))
        half_turn = math.degrees(math.acos(max(-1.0, min(1.0, _dot(bis, incoming)))))
        allowed = max(0.0, min(cfg['jitter'], cfg['present'] - half_turn))
        axis = _norm(_deflect(bis, allowed, rng)) if allowed > 0.01 else bis
        out.append((pts[i], axis, outgoing))
    return out


# ── Proofs ──────────────────────────────────────────────────────────────────────────────────

def sweep(seeds=400):
    """Run the real generator and MEASURE what a table cannot tell you.

    There is no failure rate here any more: the circuit is closed BY CONSTRUCTION and the
    amplitude shrink bottoms out on a regular zigzag ring, which is legal. `fails` is kept and
    asserted zero so a future change that reintroduces a failure mode is loud."""
    report = []
    for idx, cfg in enumerate(INTENSITIES, start=1):
        fails = 0
        worst_turn = 0.0
        worst_present = 0.0
        min_sep_seen = 1e9
        min_leg = 1e9
        dubins_violations = 0
        max_stations_in_lod = 0
        worst_pad_gap = 1e9
        worst_join = 0.0
        worst_start_present = 0.0
        pad_dist_spread = 0.0
        pad_present_spread = 0.0
        for s in range(1, seeds + 1):
            course = generate(s * 7919 + idx, cfg)
            if course is None:
                fails += 1
                continue
            pts = [c[0] for c in course]
            axes = [c[1] for c in course]
            start = pts[0]
            ring = pts[1:]          # the closed circuit
            legs = [_norm(_sub(ring[(i + 1) % len(ring)], ring[i])) for i in range(len(ring))]

            for i, p in enumerate(pts):
                r = _len(p)
                assert SHELL_INNER - 1e-6 <= r <= SHELL_OUTER + 1e-6, f"shell {r}"
                for j in range(i + 1, len(pts)):
                    min_sep_seen = min(min_sep_seen, _len(_sub(pts[i], pts[j])))

            # THE CIRCUIT. Every corner wraps, so the last leg is a real leg like any other -
            # that is the whole point of the change and it is what the modular index asserts.
            for i in range(len(ring)):
                leg = _len(_sub(ring[(i + 1) % len(ring)], ring[i]))
                min_leg = min(min_leg, leg)
                turn = _angle_between(legs[(i - 1) % len(ring)], legs[i])
                worst_turn = max(worst_turn, turn)
                if leg <= 2.0 * CEILING_TURN_RADIUS * math.sin(math.radians(turn)):
                    dubins_violations += 1
                pres = math.degrees(math.acos(min(1.0, abs(_dot(_norm(axes[i + 1]), legs[i])))))
                worst_present = max(worst_present, pres)

            # THE JOIN. The one corner the turn cap does NOT describe: the merge from the start
            # gate onto the circuit. Bounded only by Dubins, which MIN_STEP guarantees at any
            # angle, and asserted as such rather than waved at.
            entry = _sub(ring[0], start)
            elen = _len(entry)
            ed = _norm(entry)
            for ang in (_angle_between((0.0, 1.0, 0.0), ed), _angle_between(ed, legs[0])):
                worst_join = max(worst_join, ang)
                if elen <= 2.0 * CEILING_TURN_RADIUS * math.sin(math.radians(ang)):
                    dubins_violations += 1

            # THE START GATE'S FAIRNESS. Equidistant was the old promise; face-on is the new one,
            # and both are measured rather than argued.
            dists = [_len(_sub(start, q)) for q in PADS]
            press = [math.degrees(math.acos(min(1.0, abs(_dot((0.0, 1.0, 0.0),
                     _norm(_sub(start, q))))))) for q in PADS]
            pad_dist_spread = max(pad_dist_spread, max(dists) - min(dists))
            pad_present_spread = max(pad_present_spread, max(press) - min(press))
            worst_start_present = max(worst_start_present, max(press))

            reach = station_reach(cfg['port'])
            for pt in pts:
                for pad in PADS:
                    worst_pad_gap = min(worst_pad_gap, _len(_sub(pad, pt)) - reach)

            # THE COLLIDER MEASUREMENT. Sample along every leg of the CLOSED circuit and count
            # how many stations fall inside the collider-LOD radius at once.
            for i in range(len(ring)):
                for t in range(0, 11):
                    p = _add(ring[i], _mul(_sub(ring[(i + 1) % len(ring)], ring[i]), t / 10.0))
                    n = sum(1 for q in pts if _len(_sub(q, p)) <= LOD_RADIUS)
                    max_stations_in_lod = max(max_stations_in_lod, n)

        report.append(dict(intensity=idx, fails=fails, worst_turn=worst_turn,
                           worst_present=worst_present, min_sep=min_sep_seen, min_leg=min_leg,
                           dubins_violations=dubins_violations,
                           max_stations_in_lod=max_stations_in_lod,
                           worst_pad_gap=worst_pad_gap,
                           worst_join=worst_join,
                           worst_start_present=worst_start_present,
                           pad_dist_spread=pad_dist_spread,
                           pad_present_spread=pad_present_spread))
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

    print("\nGenerating 400 seeds x 4 intensities ...")
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
        print(f"    air at nearest spawn pad : {r['worst_pad_gap']:.1f} u "
              f"(rejection floor {SPAWN_PAD_CLEARANCE:.1f})")
        print(f"    worst JOIN corner        : {r['worst_join']:.1f} deg "
              f"(cap-exempt; Dubins needs {2.0 * CEILING_TURN_RADIUS * math.sin(math.radians(r['worst_join'])):.1f} "
              f"u, shortest leg is {r['min_leg']:.1f})")
        print(f"    start gate presentation  : {r['worst_start_present']:.1f} deg, "
              f"spread across pads {r['pad_present_spread']:.4f}")
        print(f"    start gate pad distances : spread {r['pad_dist_spread']:.4f} u")
        if r['fails']:
            fail.append(f"I{r['intensity']}: {r['fails']} generation failures")
        if r['worst_turn'] > c['max_turn'] + 0.01:
            fail.append(f"I{r['intensity']}: turn cap exceeded ({r['worst_turn']:.2f})")
        if r['worst_present'] > c['present'] + 0.01:
            fail.append(f"I{r['intensity']}: presentation cap exceeded ({r['worst_present']:.2f})")
        if r['dubins_violations']:
            fail.append(f"I{r['intensity']}: {r['dubins_violations']} unflyable corners")
        # THE START GATE IS THE FAIR START, and that is the whole reason it exists. Every
        # pad must be the same distance from it AND see it at the same angle - equidistance
        # alone is what a circuit breaks.
        if r['pad_dist_spread'] > 1e-6:
            fail.append(f"I{r['intensity']}: spawn pads are not equidistant from the start gate "
                        f"(spread {r['pad_dist_spread']:.6f} u)")
        if r['pad_present_spread'] > 1e-6:
            fail.append(f"I{r['intensity']}: spawn pads do not see the start gate at the same "
                        f"angle (spread {r['pad_present_spread']:.6f} deg)")
        if r['worst_start_present'] > c['present'] + 0.01:
            fail.append(f"I{r['intensity']}: the start gate stands edge-on to its run-in "
                        f"({r['worst_start_present']:.2f} deg)")
        if r['worst_pad_gap'] < SPAWN_PAD_CLEARANCE - 0.01:
            fail.append(f"I{r['intensity']}: a station reaches within "
                        f"{r['worst_pad_gap']:.1f} u of a spawn pad")

        # THE EYE IS THE MODE'S CENTRAL PROMISE, so it is asserted rather than described. The
        # bar bodies must leave EYE_RADIUS clear - the radius the keystone collar's inner faces
        # already advertise - and the Sparrow must fit through it with margin.
        clear = eye_clearance(c['port'])
        print(f"    clear eye radius         : {clear:.3f} u "
              f"({clear / SPARROW_HULL_RADIUS:.2f} x hull)")
        if clear < EYE_RADIUS - 1e-4:
            fail.append(f"I{r['intensity']}: plug bars intrude on the eye "
                        f"({clear:.3f} < {EYE_RADIUS})")
        if clear < SPARROW_HULL_RADIUS:
            fail.append(f"I{r['intensity']}: the eye is narrower than the hull")

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
    print(f"  LOD-cullable. The {STATION_COUNT} switch rings carry no collider at all.")

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

    print(f"\nLaps: {LAPS} - a start gate plus a {CIRCUIT_COUNT}-station circuit flown "
          f"{LAPS}x = {crossing_target()} crossings")
    print("\nComeback rate")
    target = crossing_target()
    lv = 0.25 * target * COMEBACK_RATE
    print(f"  a quarter-of-target deficit ({0.25*target:.1f} of {target} crossings) buys "
          f"{lv:.2f} element levels")
    if lv < 1.0:
        fail.append(f"comeback rate {COMEBACK_RATE} buys only {lv:.2f} levels at a quarter deficit")
    if lv > 5.0:
        fail.append(f"comeback rate {COMEBACK_RATE} hands {lv:.1f} levels at a quarter deficit")

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
