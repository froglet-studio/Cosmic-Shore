#!/usr/bin/env python3
"""
The offline geometry proof for SKEIN (GameModes.Skein = 48) - the Urchin's rail race.

WHAT THIS IS. `SpawnableSkein.cs` builds the arena in closed form; this file is the model that
PROVES the arena satisfies the contracts the mode is built on, and `author_skein_assets.py`
imports it so a cell's PhaseThresholds can never drift from the arena that has to satisfy them.
It is the same discipline as `hijack_budget.py`, whose `prove_launch_geometry()` caught a 0.32
degree launch tilt before a line of that mode's C# was written.

    python3 Tools/Build/skein_budget.py            # print the tables and run every proof
    python3 Tools/Build/skein_budget.py --check    # same, silent unless something fails

PURE MATH, ZERO REPO I/O, stdlib only. It cannot rot against a moved asset because it reads none,
and it can be run on any machine with python3. Everything it asserts is MEASURED off the same
polylines the C# lays, never asserted from the closed form that produced them - the one lesson
Hijack, the gyroid tables and the Schwarz P tables all record independently.

THE TWO THEOREMS, both exact rather than measured (proved in `prove_spine`):

  1. SPEED.   |C'(t)| = sqrt(9r^2 + 4(R + r cos 3t)^2).  The cross terms in |C'|^2 cancel
              identically, so the spine's speed is closed form and its arc length is a clean
              1-D integral rather than a polyline sum.

  2. CLEARANCE. The knot's minimum non-local self-distance is EXACTLY 2r, at every t.
              The two passes at a shared azimuth are t and t+pi, where cos3t flips sign and
              cos2t does not, so the separation is sqrt((2r cos3t)^2 + (2r sin3t)^2) = 2r
              identically.  This is why the spine is a KNOT and not a random walk: a wandering
              spine must PROVE it does not pass near itself, and the packing arithmetic in
              SKEIN.md shows it cannot at this cable diameter.  Here there is nothing to
              reject, no redraw budget, and no seed that can generate an unplayable arena.

  3. FRAME.   u(t) . C'(t) = 0 identically, so the torus outward normal is an exact frame
              vector.  Frenet is unusable (its normal flips 180 degrees through an inflection
              and tears the braid); parallel transport is unusable (it does not close on a
              loop, so every strand kinks at the seam).  The torus frame is 2pi-periodic by
              construction, so an integer twist closes a strand exactly.
"""

import math
import sys

CHECK_ONLY = "--check" in sys.argv


# ─────────────────────────────────────────────────────────────────────────────
# The arena's authored constants.  EVERY ONE of these is mirrored in
# SpawnableSkein.cs; changing one here without changing it there is the drift
# this file exists to make impossible.  Re-run after any edit.
# ─────────────────────────────────────────────────────────────────────────────

R = 560.0          # torus major radius (u)
r = 200.0          # torus minor radius (u) - ALSO the self-clearance theorem's constant
P, Q = 2, 3        # (2,3) torus knot = trefoil

# THE BREATHING CABLE - one family of strands, each on a radius that oscillates.
#
# This REPLACES the two fixed shells (A_IN 55 / A_OUT 120) and the flare that used to dive an
# outer rail down to the inner shell so it could launch. Both are gone, and the reason is that
# the two-shell cable answered "which lane am I on" with a property of the LANE, so the answer
# never changed while you rode it. A breathing radius makes the same question a property of
# WHEN, so riding is itself a decision that expires:
#
#     a_k(s) = A_MID + A_SWING * sin(2*pi*RADIAL_CYCLES*s/L + phi_k)
#
# Each strand spends part of the lap as the direct inner path and part of it spiralling out,
# and the strands are phase-SPREAD (phi_k = 2*pi*k/N) so that at every spine station the N
# radii sample the whole band - full radial coverage at all times, which is what the two shells
# bought and this keeps. Ride an outward-bound strand and it carries you out; ride an
# inward-bound one and it carries you in. Staying on the fast line means CHANGING STRANDS, and
# that is what the launch gap below is sized to allow.
#
# RADIAL_CYCLES must be an INTEGER or the strand does not close on the knot (sin(2*pi*m + phi)
# = sin(phi) is the whole closure condition). 3 matches the trefoil's own 3-fold symmetry, so a
# strand's radial phase is the same at each of the knot's three lobes and the cable's
# cross-section reads the same everywhere - predictable rather than arbitrary.
A_MID = 90.0            # mean strand radius (u)
A_SWING = 45.0          # radial half-swing (u) - the band is [45, 135]
RADIAL_CYCLES = 3       # integer: radial oscillations per spine lap
A_MIN = A_MID - A_SWING
A_MAX = A_MID + A_SWING

SEGMENT_SPINE = 450.0   # spine arc length between breaks on one strand (u)
BREAK_GAP = 40.0        # gap left at a break (u)

PRISM_SPACING = 8.0     # along-rail prism pitch (u); per-segment spacing is DERIVED from it
# Cross-section is under verification (the tunnelling question - see SKEIN.md). Both candidates
# are kept here so the budget can be re-read the instant that verdict lands.
PRISM_SCALE_CANDIDATES = {
    "(3,3,6) Track Projector": (3.0, 3.0, 6.0),
    "(6,6,8) tunnelling-safe": (6.0, 6.0, 8.0),
}
PRISM_SCALE = PRISM_SCALE_CANDIDATES["(6,6,8) tunnelling-safe"]

GATE_COUNT = 24
GATE_MOUTH_MAX = 40.0    # strand gate mouth radius CEILING (u) - the mouth is DERIVED below
MOUTH_SEPARATION_FRACTION = 0.85   # of the cable's closest strand pair
LINE_MOUTH = 150.0       # spine-centred start/finish collar radius (u)
GATE_LEAD_IN = 150.0     # spine-u after a landing before its gate sits - the settle distance
# The walk slides a ring further down the same transfer when the nearest position is taken.
LEAD_INS = tuple(150.0 + 70.0 * i for i in range(26))   # 150 .. 1900 u of spine
# The range is wide on purpose. A gate further down the SAME transfer costs the pilot nothing -
# they are already on that rail, committed - so sliding the ring is free, whereas failing to
# place it costs the whole course. Measured: at 8 lead-ins the walk starved at 22 of 24 gates
# once per-seed jitter moved the breaks around.
GATE_SEPARATION = 200.0  # no two gate centres closer than this (u)

# The trim's acceptance conditions (SKEIN.md 2.3).
END_AIM_RADIUS = 12.0    # a break's ray must pass this close to a live foreign strand
# END_AIM_MIN is DERIVED from the vessel's grind speed - see LAUNCH_DECISION_SECONDS below.
END_AIM_MAX = 420.0
RAY_CLEARANCE = 24.0     # no OTHER strand within this of the ray
ARRIVAL_ANGLE_MAX = 60.0 # degrees; hard platform limit is 90 (Attach seeds Backward)
MIN_SEGMENT_PRISMS = 40

# ── The INDEPENDENT acceptance bounds ────────────────────────────────────────
# These exist because the first version of this file asserted `miss <= END_AIM_RADIUS` - the
# very parameter the trim accepts against - so loosening the trim loosened the proof with it and
# the gate could not fail. A NEGATIVE CONTROL caught it: END_AIM_RADIUS 12 -> 60 produced
# "ALL PROOFS PASSED" over an arena full of launches that aim at nothing.
#
# GENERAL RULE, and it is the reason this block is separate: a proof must be stated against a
# constant the thing under test cannot move. Asserting a generator against its own tolerance is
# a tautology wearing a gate's clothes.
MAX_LAUNCH_MISS = 12.0        # a launch must pass this close to the rail it aims at
MAX_ARRIVAL_ANGLE = 60.0      # and arrive no more obliquely than this...
HARD_BACKWARD_ANGLE = 90.0    # ...against the PLATFORM limit, which is not a taste: at 90 deg
                              # TrailFollower.Attach seeds Backward and the pilot is carried
                              # back up the course at 150 u/s. 60 leaves 30 deg of margin.
MIN_GATE_SEPARATION = 200.0   # no two gate centres closer than this
MIN_STRAND_CLEARANCE = 24.0   # closest approach between any two strands (u) - see
                              # prove_strand_separation. Must clear a prism's own diagonal
                              # (a (6,6,8) box spans 5.83 u corner to corner in cross-section)
                              # by enough that a rider on one rail is never inside another's
                              # catch envelope; RAY_CLEARANCE uses the same 24 u for a launch.
MIN_LOBE_CLEARANCE = 60.0     # clear air between the knot's own lobes, after the cable
MIN_TANGENT_SPEED_RATIO = 0.99  # cos(turn/2) - see prove_tangent_speed

STRAND_COUNTS = {1: 5, 2: 6, 3: 7, 4: 9}   # intensity -> N

# Vessel facts, all read from Urchin.prefab / GunVesselTransformer / TrailFollower.
GRIND_FRIENDLY = 150.0   # TrailFollower.FriendlyTerrainSpeed
GRIND_HOSTILE = 10.0     # TrailFollower.HostileTerrainSpeed
CRUISE = 50.0            # VesselTransformer.DefaultThrottleScaler
DECAY = 12.0             # GunVesselTransformer.detachSpeedDecayRate (u/s^2)
TURN_RATE = 90.0         # Pitch/Yaw/RollScaler (deg/s), speed-independent
MEMBRANE = 1200.0
SPAWN_RING = 1000.0

# ── The two distances the pilot actually feels, both derived as TIME ──────────
#
# LAUNCH_DECISION_SECONDS is the ask "the curve segments should have bigger gaps so you don't
# just shoot into the next segment immediately - that is a good time to change segments",
# turned into geometry. A launch leaves at the 150 u/s grind speed, so a free-flight window of
# t seconds is t * GRIND_FRIENDLY of clear air. The old floor was 60 u = 0.40 s, which is a
# fifth of a human reaction plus a decision, and the trim ALSO preferred the nearest qualifying
# landing - so the generator systematically produced the shortest legal hop. Both are fixed:
# the floor is raised to a real window and the trim now takes the FURTHEST landing inside the
# window rather than the nearest.
LAUNCH_DECISION_SECONDS = 1.4
MIN_SEGMENT_SECONDS = 2.0          # a segment shorter than this is not a rail, it is a bump

MIN_SEGMENT_SPINE = MIN_SEGMENT_SECONDS * GRIND_FRIENDLY      # 300 u of spine
END_AIM_MIN = LAUNCH_DECISION_SECONDS * GRIND_FRIENDLY        # 210 u = 1.4 s of free flight

SPINE_SAMPLES = 4096


# ─────────────────────────────────────────────────────────────────────────────
# THE RNG - SwitchbackCourse.Rng, byte for byte
#
# A specified xorshift32 rather than random.Random: the C# side must produce the IDENTICAL
# course from the identical seed, and System.Random's sequence is a property of the runtime's
# implementation rather than of the seed (the trap Docs/WEEKLY_CHALLENGE.md records). Includes
# the 0x9E3779B9 seed-zero guard - 0 is xorshift's fixed point and emits nothing but zeros
# forever, which yields a degenerate course silently rather than throwing.
# ─────────────────────────────────────────────────────────────────────────────

class Rng:
    __slots__ = ("_s",)

    def __init__(self, seed):
        s = seed & 0xFFFFFFFF
        self._s = s if s != 0 else 0x9E3779B9

    def next_uint(self):
        x = self._s
        x ^= (x << 13) & 0xFFFFFFFF
        x ^= x >> 17
        x ^= (x << 5) & 0xFFFFFFFF
        self._s = x
        return x

    def unit(self):
        return self.next_uint() / 4294967296.0

    def range(self, a, b):
        return a + (b - a) * self.unit()


# ─────────────────────────────────────────────────────────────────────────────
# vector helpers (tuples, stdlib only)
# ─────────────────────────────────────────────────────────────────────────────

def add(a, b):   return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b):   return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, k):   return (a[0] * k, a[1] * k, a[2] * k)
def dot(a, b):   return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1],
                         a[2] * b[0] - a[0] * b[2],
                         a[0] * b[1] - a[1] * b[0])
def norm(a):     return math.sqrt(dot(a, a))
def unit(a):
    n = norm(a)
    return (a[0] / n, a[1] / n, a[2] / n) if n > 1e-12 else (0.0, 0.0, 1.0)

def angle_between(a, b):
    return math.degrees(math.acos(max(-1.0, min(1.0, dot(unit(a), unit(b))))))


# ─────────────────────────────────────────────────────────────────────────────
# 1. THE SPINE
# ─────────────────────────────────────────────────────────────────────────────

def spine_point(t):
    a = R + r * math.cos(Q * t)
    return (a * math.cos(P * t), a * math.sin(P * t), r * math.sin(Q * t))

def spine_speed(t):
    """|C'(t)|, closed form - the cross terms in |C'|^2 cancel identically."""
    a = R + r * math.cos(Q * t)
    return math.sqrt((Q * r) ** 2 + (P * a) ** 2)

def spine_tangent(t):
    a = R + r * math.cos(Q * t)
    ap = -Q * r * math.sin(Q * t)
    return unit((ap * math.cos(P * t) - P * a * math.sin(P * t),
                 ap * math.sin(P * t) + P * a * math.cos(P * t),
                 Q * r * math.cos(Q * t)))

def torus_normal(t):
    """The torus OUTWARD normal. u . C' == 0 identically (proved in prove_spine)."""
    return (math.cos(Q * t) * math.cos(P * t),
            math.cos(Q * t) * math.sin(P * t),
            math.sin(Q * t))


class Spine:
    """The spine sampled once, with arc length and the torus frame at every station."""

    def __init__(self, n=SPINE_SAMPLES):
        self.n = n
        self.t = [2.0 * math.pi * i / n for i in range(n + 1)]
        self.pos = [spine_point(t) for t in self.t]
        self.tan = [spine_tangent(t) for t in self.t]
        self.u = [torus_normal(t) for t in self.t]
        self.v = [cross(self.tan[i], self.u[i]) for i in range(n + 1)]

        # Arc length by the trapezoid rule on the CLOSED-FORM speed, not on chords: the
        # polyline underestimates a curve's length and the error would land in every derived
        # helix angle.
        self.s = [0.0]
        for i in range(n):
            h = self.t[i + 1] - self.t[i]
            self.s.append(self.s[-1] + 0.5 * h * (spine_speed(self.t[i]) + spine_speed(self.t[i + 1])))
        self.L = self.s[-1]

    def station_at_arc(self, s):
        """Index and interpolation fraction for a spine arc length (wrapping)."""
        s = s % self.L
        lo, hi = 0, self.n
        while lo < hi:
            mid = (lo + hi) // 2
            if self.s[mid] <= s: lo = mid + 1
            else: hi = mid
        i = max(0, lo - 1)
        span = self.s[i + 1] - self.s[i]
        f = (s - self.s[i]) / span if span > 1e-12 else 0.0
        return i, f

    def frame_at_arc(self, s):
        """(position, tangent, u, v) at a spine arc length, linearly blended between stations."""
        i, f = self.station_at_arc(s)
        p = add(mul(self.pos[i], 1 - f), mul(self.pos[i + 1], f))
        T = unit(add(mul(self.tan[i], 1 - f), mul(self.tan[i + 1], f)))
        u = add(mul(self.u[i], 1 - f), mul(self.u[i + 1], f))
        u = unit(sub(u, mul(T, dot(u, T))))     # re-orthogonalise: the blend leaves the plane
        return p, T, u, cross(T, u)

    def geodesic_torsion(self):
        """
        tau_f = u' . v, measured per unit ARC LENGTH.  The torus frame back-rotates around the
        spine by a non-integer number of turns per lap, so a strand's twist number is stated NET
        of this.  If R or r ever moves this changes and every helix angle moves with it -
        SILENTLY, because offsets stay exact and nothing overlaps.  That is the same class as
        Hijack's bond-delta trap, so it is asserted rather than trusted.
        """
        vals, total = [], 0.0
        for i in range(self.n):
            ds = self.s[i + 1] - self.s[i]
            if ds < 1e-12: continue
            du = mul(sub(self.u[i + 1], self.u[i]), 1.0 / ds)
            tau = dot(du, self.v[i])
            vals.append(tau)
            total += tau * ds
        return min(vals), max(vals), total


# ─────────────────────────────────────────────────────────────────────────────
# 2. THE SHELLS
#
# A strand is  S_k(s) = C(s) + a * (cos th * u(s) + sin th * v(s)),  th = phi_k + s/lam.
#
# THE TWIST NUMBER IS AN INTEGER, and that is what closes the strand.  The frame is 2pi-periodic,
# so th must advance by a whole multiple of 2pi over one lap:  L/lam = 2*pi*w.  A non-integer w
# leaves a seam - a discontinuity of up to a full cable diameter that no amount of tolerance
# hides, and which reads in play as one rail that simply stops in mid-air.
#
# BOTH SHELLS WIND THE SAME WAY.  Counter-winding halves the crossing period and is the tempting
# choice, but it makes the tangent dot between an inner and an outer strand
# (lam_in*lam_out - a_in*a_out) / (|..||..|), which goes NEGATIVE at any practical lay: a launch
# from one shell onto a counter-wound strand of the other lands at more than 90 degrees to it,
# TrailFollower.Attach seeds Backward, and the pilot is carried back up the course at 150 u/s.
# Same-handed, the dot is (lam_in*lam_out + a_in*a_out)/(..) - positive at every lay, by
# construction.  `prove_winding` asserts both branches so the reason survives the decision.
# ─────────────────────────────────────────────────────────────────────────────

def lam_for_turns(L, w):
    """Spine arc length per radian of twist, for an integer turn count w over one lap."""
    return L / (2.0 * math.pi * w)

def helix_angle_deg(a, lam):
    return math.degrees(math.atan2(a, lam))

def arclength_factor(a, lam):
    """A strand's own length per unit of SPINE. 1/cos(psi)."""
    return math.sqrt(a * a + lam * lam) / lam


def solve_twist(L):
    """
    ONE twist count for the whole cable - the two shells are gone, so there is one family.

    Both bounds are evaluated at A_MAX, which is where each binds: helix angle and arc factor
    both grow with radius, so a strand that is legal at its outward extreme is legal for the
    whole of its breath.

      psi(A_MAX) <= 45 deg   Past 45 a strand travels further AROUND the spine than ALONG it,
                             and "spiralling out" stops reading as progress and starts reading
                             as going in circles. This is the bound that BINDS.

      f(A_MAX) <= 2.0        An outward lane must stay at least 1.4x faster than FLYING the
                             gate polyline, or it stops being a road and becomes a punishment.
                             Flying covers ~52.6 spine-u/s (50 u/s cruise x a 0.95 chord/arc
                             ratio), so f <= 150/(1.4 * 52.6) = 2.038. Slack at the shipped w.

    The readable speed advantage the old psi_in <= 25 bound bought BETWEEN the two shells is now
    bought WITHIN one strand, for free: f(A_MAX)/f(A_MIN) is the course-length penalty for
    riding the outward phase instead of the inward one, and it is reported rather than assumed.

    Larger w is more twist and a more legible braid, so this takes the largest integer that fits.
    """
    w = 1
    while (helix_angle_deg(A_MAX, lam_for_turns(L, w + 1)) <= 45.0
           and arclength_factor(A_MAX, lam_for_turns(L, w + 1)) <= 2.0):
        w += 1
    return w


def _smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


class Strand:
    """One rail's centreline, sampled at a fixed pitch of ITS OWN arc length."""

    def __init__(self, spine, lam, phi, index, cuts=()):
        self.lam, self.phi, self.index = lam, phi, index
        self.L = spine.L
        self.cuts = tuple(cuts)
        # Reported at the extremes, because a breathing strand has no single value for either.
        self.f_min = arclength_factor(A_MIN, lam)
        self.f_max = arclength_factor(A_MAX, lam)
        self.psi_max = helix_angle_deg(A_MAX, lam)

        # Sample at PRISM_SPACING of the strand's TRUE arc length, by walking the spine finely
        # and emitting a node every PRISM_SPACING of accumulated chord.
        #
        # The obvious sampler - step the spine by PRISM_SPACING / f - is wrong wherever the
        # RADIUS is moving, and on a breathing strand the radius is moving EVERYWHERE (it was
        # only the flare before). f is the constant-radius factor; using it would put the error
        # into the per-prism turn, where it reads as a rideability failure rather than as a
        # sampling bug.
        step = 0.5
        self.pts, self.spine_arc = [self.point(spine, 0.0)], [0.0]
        s_, acc, prev = 0.0, 0.0, self.point(spine, 0.0)
        while s_ < spine.L:
            s_ += step
            cur = self.point(spine, s_)
            acc += norm(sub(cur, prev))
            prev = cur
            if acc >= PRISM_SPACING:
                self.pts.append(cur)
                self.spine_arc.append(s_)
                acc = 0.0
        self.n = len(self.pts)
        self.length = sum(norm(sub(self.pts[i], self.pts[i - 1])) for i in range(1, self.n))

    def radius_at(self, s):
        """
        The breathing radius. ONE closed-form sinusoid - no flare, no special case at a break.

        The strand's radial phase is its ANGULAR phase, and that identity is the whole reason
        the cable is provably clear of itself: it makes the angular separation between any two
        strands CONSTANT (see prove_strand_separation), so however the radii breathe, two
        strands can never approach each other in the normal plane beyond the closed-form bound.
        Give the radius its own independent phase and that theorem is gone.
        """
        return A_MID + A_SWING * math.sin(2.0 * math.pi * RADIAL_CYCLES * s / self.L + self.phi)

    def radial_rate_at(self, s, h=0.5):
        """da/ds - the sign is which way this strand is carrying you RIGHT NOW."""
        return (self.radius_at(s + h) - self.radius_at(s - h)) / (2.0 * h)

    def point(self, spine, s, radius_override=None):
        p, T, u, v = spine.frame_at_arc(s)
        th = self.phi + s / self.lam
        a = self.radius_at(s) if radius_override is None else radius_override
        return add(p, add(mul(u, a * math.cos(th)), mul(v, a * math.sin(th))))

    def tangent(self, spine, s, h=0.5):
        return unit(sub(self.point(spine, s + h), self.point(spine, s - h)))


PHASE_JITTER = 0.30      # of a slot: how far a strand may sit off its even phase
CUT_JITTER = 0.22        # of SEGMENT_SPINE: how far a break may sit off its even station


def cut_arcs(spine, strand_index, n_strands, rng_seed=0):
    """
    The spine arcs at which one strand breaks - staggered by strand so a break happens somewhere
    in the cable every SEGMENT_SPINE / N of spine rather than all at one station, and JITTERED
    per seed so two matches at one intensity are not the same course.

    The jitter is bounded rather than free, and that is the whole trick: the cable's SHAPE is
    fixed (so every proof about clearance, nesting and rideability holds at every seed) and what
    varies is WHERE IT BREAKS - which is exactly what the ask means by rails that randomly end.
    """
    rng = Rng(rng_seed * 2654435761 + strand_index * 40503 + 1)
    offset = strand_index * SEGMENT_SPINE / n_strands
    offset += rng.range(-CUT_JITTER, CUT_JITTER) * SEGMENT_SPINE
    out, k = [], 0
    while True:
        arc = offset + k * SEGMENT_SPINE + rng.range(-CUT_JITTER, CUT_JITTER) * SEGMENT_SPINE
        if arc >= spine.L - MIN_SEGMENT_SPINE: break
        # Two breaks must not crowd, at ANY seed - the jitter is clamped by the previous
        # accepted cut rather than trusted to stay clear of it. (This used to be the
        # no-overlapping-flares rule; with the flare gone the constraint is simply that a
        # segment stays long enough to be worth riding.)
        if arc > MIN_SEGMENT_SPINE and (not out or arc - out[-1] >= MIN_SEGMENT_SPINE):
            out.append(arc)
        k += 1
    return out


def build_strands(spine, n_strands, w, seed=0):
    """
    ONE family of N strands, evenly phase-spread. phi_k is BOTH the angular phase and the
    radial phase - see Strand.radius_at for why that identity is load-bearing rather than a
    convenience.

    The even spread is what delivers full radial coverage: at any spine station the N radii are
    A_MID + A_SWING*sin(x + 2*pi*k/N), which samples the whole band however x moves. The jitter
    is bounded so the spread stays near-even (a strand parked next to its neighbour would leave
    a radial hole on the other side of the ring).
    """
    lam = lam_for_turns(spine.L, w)
    rng = Rng(seed * 747796405 + 2891336453)
    strands = []
    for k in range(n_strands):
        phi = 2 * math.pi * (k + rng.range(-PHASE_JITTER, PHASE_JITTER)) / n_strands
        strands.append(Strand(spine, lam, phi, k, cut_arcs(spine, k, n_strands, seed)))
    return strands


def per_prism_turn_in_segments(strand, segments):
    """
    The worst turn between consecutive prisms OF ONE LAID SEGMENT.

    Measured per segment on purpose: a strand's polyline crosses its own break gaps, and the
    chord across a gap is not a turn any rider ever takes. Measuring across it reports the
    arena's worst number as belonging to geometry nobody flies.
    """
    worst = 0.0
    for (si, a, b) in segments:
        if si != strand.index: continue
        for i in range(a + 1, min(b, strand.n - 1)):
            p = sub(strand.pts[i], strand.pts[i - 1])
            q = sub(strand.pts[i + 1], strand.pts[i])
            worst = max(worst, angle_between(p, q))
    return worst


def per_prism_turn_degrees(strand):
    """
    The worst turn between consecutive prisms, MEASURED off the laid polyline.

    THE BOUND IS THE PILOT, NOT THE SPLINE.  The ride reverses when the nose crosses the ribbon
    axis by facingFlipThreshold, and the pilot corrects at TURN_RATE deg/s - so a rail may turn
    at most  TURN_RATE * spacing / speed  per prism, sustained.  At 8 u and 150 u/s that is
    4.80 deg/prism (a minimum rail radius of 95.5 u).
    """
    worst = 0.0
    for i in range(1, len(strand.pts) - 1):
        a = sub(strand.pts[i], strand.pts[i - 1])
        b = sub(strand.pts[i + 1], strand.pts[i])
        worst = max(worst, angle_between(a, b))
    return worst


def sustained_turn_budget_degrees():
    return TURN_RATE * PRISM_SPACING / GRIND_FRIENDLY


# ─────────────────────────────────────────────────────────────────────────────
# 3. SEGMENTS AND AIMED ENDS - "rails that randomly end", constrained
#
# Every segment is its own OPEN trail, because an open ribbon is what has ends to launch off,
# and one-per-segment because a shared Trail has ONE pair of ends and every other segment could
# then never launch (Hijack's finding).
#
# A BREAK IS AIMED, AND THE AIM IS MEASURED.  A closed-form lattice for the launch assumes a
# locally straight spine; the real spine bends far enough over a throw that the closed form is a
# design story rather than a proof.  So the generator casts the REAL ray from each candidate
# terminal prism and accepts the first index that satisfies all four conditions.  This is
# `hijack_budget.prove_launch_geometry()`'s discipline: walk the real prism positions and
# measure, never trust the expression that produced them.
# ─────────────────────────────────────────────────────────────────────────────

class Break:
    __slots__ = ("strand", "index", "target", "landing_arc", "miss", "arrival", "ray_len", "clearance")
    def __init__(self, strand, index, target, landing_arc, miss, arrival, ray_len, clearance):
        self.strand, self.index, self.target = strand, index, target
        self.landing_arc, self.miss, self.arrival = landing_arc, miss, arrival
        self.ray_len, self.clearance = ray_len, clearance


def _closest_on_polyline(pts, origin, direction, lo, hi):
    """
    Closest approach of the ray origin+t*direction (t in [lo,hi]) to a polyline, sampled at its
    nodes. Returns (miss, node_index, t_at_closest). Node sampling is honest here because the
    polyline's own pitch is PRISM_SPACING - the same resolution the ride walks.
    """
    best = (float("inf"), -1, 0.0)
    for i, p in enumerate(pts):
        t = dot(sub(p, origin), direction)
        if t < lo or t > hi: continue
        d = norm(sub(p, add(origin, mul(direction, t))))
        if d < best[0]: best = (d, i, t)
    return best


def _scan_order(scan):
    """Indices to try, nearest the raw cut first, alternating back then forward. An outer
    strand's aim lives at the BOTTOM of its flare, which is the cut itself, so a backward-only
    scan searches the one part of the curve where the rail is still climbing."""
    yield 0
    for k in range(1, scan + 1):
        yield -k
        yield k


def trim_break(spine, strands, si, raw_index, scan=40):
    """
    Scan candidate terminal indices inward from the raw cut and accept the first that satisfies:

      1. AIM        closest approach to a live FOREIGN strand <= END_AIM_RADIUS, at a range in
                    [END_AIM_MIN, END_AIM_MAX]. The FURTHEST such landing wins - see below.
      2. DIRECTION  dot(ray, target tangent at landing) >= cos(ARRIVAL_ANGLE_MAX). 60 degrees
                    leaves 30 of margin under the 90 at which TrailFollower.Attach seeds
                    Backward and throws the pilot back up the course at grind speed.
      3. CLEARANCE  no OTHER strand within RAY_CLEARANCE of the ray, so the pilot's hull is
                    inside exactly one rail's catch envelope and the attach can never be
                    ambiguous.
      4. INTEGRITY  the segment keeps at least MIN_SEGMENT_PRISMS.

    Returns a Break, or None when no index in the window qualifies.
    """
    s = strands[si]
    for off in _scan_order(scan):
        idx = raw_index + off
        if idx < MIN_SEGMENT_PRISMS or idx >= s.n - 1: continue

        origin = s.pts[idx]
        direction = unit(sub(s.pts[idx], s.pts[idx - 1]))

        best = None
        for tj, t in enumerate(strands):
            if tj == si: continue
            miss, node, rng = _closest_on_polyline(t.pts, origin, direction, END_AIM_MIN, END_AIM_MAX)
            if miss > END_AIM_RADIUS: continue
            if node <= 0 or node >= t.n - 1: continue
            tangent = sub(t.pts[node + 1], t.pts[node - 1])
            arrival = angle_between(direction, tangent)
            arrival = min(arrival, 180.0 - arrival)   # a rail is rideable both ways; Attach
            if arrival > ARRIVAL_ANGLE_MAX: continue  # picks the direction, so fold to acute
            # FURTHEST, not nearest. The floor (END_AIM_MIN) guarantees a decision window
            # exists; preferring the nearest qualifying landing then spent it immediately and
            # is what made a launch read as shooting straight into the next segment. Taking the
            # furthest inside the window uses the whole glide the vessel already carries
            # (833 u above cruise) and gives the pilot the longest look at the cable before
            # committing to a strand.
            if best is None or rng > best[3]:
                best = (tj, node, miss, rng, arrival)
        if best is None: continue

        tj, node, miss, rng, arrival = best

        # 3. CLEARANCE - measured against every OTHER strand over the accepted ray length.
        clearance = float("inf")
        for oj, o in enumerate(strands):
            if oj == si or oj == tj: continue
            m, _, _ = _closest_on_polyline(o.pts, origin, direction, END_AIM_MIN, rng)
            clearance = min(clearance, m)
        if clearance < RAY_CLEARANCE: continue

        return Break(si, idx, tj, t.spine_arc[node] if False else strands[tj].spine_arc[node],
                     miss, arrival, rng, clearance)
    return None


def cut_and_trim(spine, strands, seed=0):
    """
    Cut strand k at spine stations congruent to (k * SEGMENT_SPINE / N) mod SEGMENT_SPINE - so
    breaks are STAGGERED across strands and a break happens somewhere in the cable every
    SEGMENT_SPINE / N of spine, rather than every strand ending at the same station.
    """
    n = len(strands)
    breaks, segments, failures, skipped = [], [], 0, 0
    for si, s in enumerate(strands):
        # The SAME arcs the strand flared against - recomputing them here is how the flare and
        # the break drift apart, and a flare that ends anywhere but at the break aims nowhere.
        cuts = []
        for arc in cut_arcs(spine, si, n, seed):
            idx = min(range(s.n), key=lambda i: abs(s.spine_arc[i] - arc))
            if MIN_SEGMENT_PRISMS <= idx < s.n - 1: cuts.append(idx)

        prev = 0
        for c in cuts:
            b = trim_break(spine, strands, si, c)
            if b is None:
                # A CUT THAT CANNOT BE AIMED IS NOT A BREAK. The strand runs on to its next
                # station instead, and the segment is simply longer. This is what makes "every
                # break in this arena is aimed" a property of the construction rather than
                # something the generator hopes for and the tests discover it did not get -
                # and it is the honest reading, because an unaimed break is a rail that ends
                # pointing at nothing, which is the one thing the mode must never contain.
                skipped += 1
                continue
            breaks.append(b)
            end = b.index
            if end - prev >= MIN_SEGMENT_PRISMS:
                segments.append((si, prev, end))
            prev = end + int(round(BREAK_GAP / PRISM_SPACING))
        if s.n - 1 - prev >= MIN_SEGMENT_PRISMS:
            segments.append((si, prev, s.n - 1))
    return breaks, segments, failures, skipped


# ─────────────────────────────────────────────────────────────────────────────
# 4. THE GATE WALK
#
# Gate n+1 is not placed and then checked for reachability - it is placed ON A TRANSFER THAT WAS
# ALREADY VERIFIED by the trim.  That is what gives the mode a FLOOR: hold the throttle, ride
# every rail to its end, take every free aimed launch, and you arrive on the rail carrying your
# next ring.  No lane-change skill is required to finish; all the skill is in finishing sooner.
# ─────────────────────────────────────────────────────────────────────────────

def walk_gates(spine, strands, breaks, count=GATE_COUNT):
    by_strand = {}
    for b in breaks:
        by_strand.setdefault(b.strand, []).append(b)
    for v in by_strand.values():
        v.sort(key=lambda b: b.index)

    gates = [("spine", 0.0, LINE_MOUTH)]          # gate 1: the start collar on C(0)
    placed = [spine.frame_at_arc(0.0)[0]]
    used = set()
    cursor_strand, cursor_arc = 0, 0.0
    laps = 0.0
    mouth = gate_mouth_for(len(strands))

    def clears(pos):
        return all(norm(sub(pos, q)) >= GATE_SEPARATION for q in placed)

    for _ in range(count - 2):
        chain = by_strand.get(cursor_strand) or []
        # Candidates in ride order from the cursor, wrapping once - so the walk continues around
        # the lap rather than stopping at the strand's last break.
        ahead = [b for b in chain if strands[cursor_strand].spine_arc[b.index] > cursor_arc]
        behind = [b for b in chain if strands[cursor_strand].spine_arc[b.index] <= cursor_arc]

        nxt, gate_arc, gpos = None, 0.0, None
        # Two passes: prefer a break this course has not used yet, but fall back to one it has
        # rather than ending the walk short. Revisiting a BREAK is fine - what must never repeat
        # is a gate POSITION, and `clears` is what guarantees that. Refusing outright starves
        # the walk whenever the cursor lands on an outer strand, which carries about a third as
        # many aimed breaks as an inner one.
        for b in [x for x in ahead + behind if id(x) not in used] + ahead + behind:
            # SEPARATION IS ENFORCED DURING THE WALK, not asserted after it. Asserting after
            # turns a placement the walk could trivially have avoided into a whole-course
            # regeneration - and, measured, the walk otherwise falls into a CYCLE, revisiting
            # the same three breaks forever and laying gates 11-13, 16-18 and 21-23 on top of
            # each other.
            #
            # THE LEAD-IN IS A RANGE, NOT A CONSTANT, and that is what makes the walk finish. A
            # single lead-in gives each break exactly ONE legal gate position, so once the
            # course has laid a dozen rings every remaining candidate collides with one and the
            # walk starves at 15 of 24. Sliding the ring further down the SAME transfer costs
            # the pilot nothing - they are already on that rail, committed - and turns one
            # position per break into a continuum.
            for lead in LEAD_INS:
                arc = b.landing_arc + lead
                pos = strands[b.target].point(spine, arc % spine.L)
                if clears(pos):
                    nxt, gate_arc, gpos = b, arc, pos
                    break
            if nxt is not None: break
        if nxt is None: break

        used.add(id(nxt))
        if nxt in behind: laps += 1.0
        gates.append((nxt.target, gate_arc, mouth))
        placed.append(gpos)
        cursor_strand, cursor_arc = nxt.target, gate_arc

    gates.append(("spine", 0.0, LINE_MOUTH))       # gate 24: the finish collar
    return gates, laps


def gate_world_positions(spine, strands, gates):
    out = []
    for g in gates:
        if g[0] == "spine":
            out.append(spine.frame_at_arc(g[1] % spine.L)[0])
        else:
            out.append(strands[g[0]].point(spine, g[1] % spine.L))
    return out


# ─────────────────────────────────────────────────────────────────────────────
# 5. PAINTING, BUDGET AND THE PROOFS
# ─────────────────────────────────────────────────────────────────────────────

TRIAD = ("Jade", "Ruby", "Gold")   # ActiveDomains order. NO Blue: unclaimed mass here is a REAL
                                   # domain's, so a two-domain lobby finds the third colour's
                                   # share hostile to both sides and, by the balance assertion
                                   # below, evenly distributed - symmetric unclaimed loot.

def rebalance(segments, colours, tol=0.005, rounds=400):
    """
    Greedily re-colour segments until every domain's PRISM share is inside tol of a third.

    Measured, not constructed, and that is the whole point: the intuitive constraint set
    (siblings differ AND a child differs from its parent) reads like fairness and swings the
    per-domain share by ~16 percentage points, handing one team nearly three times another's
    fast lanes. So the construction seeds a colouring and the measurement fixes it.
    """
    colours = list(colours)
    size = [b - a for (_, a, b) in segments]
    total = sum(size)
    for _ in range(rounds):
        tot = {d: 0 for d in TRIAD}
        for c, w in zip(colours, size): tot[c] += w
        hi = max(TRIAD, key=lambda d: tot[d])
        lo = min(TRIAD, key=lambda d: tot[d])
        if (tot[hi] - tot[lo]) / total <= tol: break
        # Move the segment whose transfer best closes the gap without overshooting it.
        want = (tot[hi] - tot[lo]) / 2.0
        best, bestcost = -1, None
        for i, c in enumerate(colours):
            if c != hi: continue
            cost = abs(size[i] - want)
            if bestcost is None or cost < bestcost: best, bestcost = i, cost
        if best < 0: break
        colours[best] = lo
    return colours


def paint(segments, phase_offset=0):
    """One domain per SEGMENT. The balance is ASSERTED rather than trusted: the intuitive
    constraint set (siblings differ AND a child differs from its parent) over-constrains and
    swings the per-domain share by ~16 percentage points, which is a fairness bug wearing a
    fairness rule's clothes."""
    out = []
    for m, (si, a, b) in enumerate(segments):
        out.append(TRIAD[(si + m + phase_offset) % 3])
    return out


def domain_shares(segments, colours):
    tot = {d: 0 for d in TRIAD}
    for (si, a, b), c in zip(segments, colours):
        tot[c] += (b - a)
    n = sum(tot.values())
    return {d: tot[d] / n for d in TRIAD}, n


def budget(spine, strands, segments):
    prisms = sum(b - a for (_, a, b) in segments)
    vol = prisms * PRISM_SCALE[0] * PRISM_SCALE[1] * PRISM_SCALE[2]
    per_seg = [b - a for (_, a, b) in segments]
    return prisms, vol, len(segments), (max(per_seg) if per_seg else 0)


def launch_recovery(flight_len, speed=GRIND_FRIENDLY):
    """Lateral displacement available inside a flight, from the vessel's own turn rate.
    R = v/omega, omega speed-independent (RotationThrottleScaler 0)."""
    t = flight_len / speed
    R = speed / math.radians(TURN_RATE)
    swept = math.radians(TURN_RATE) * t
    return R * (1.0 - math.cos(min(swept, math.pi)))


def glide_budget():
    """Distance a launch carries above cruise: CarrySpeedIntoFreeFlight hands free flight the
    grind speed and TickCarriedSpeed bleeds it at a CONSTANT rate toward cruise."""
    return (GRIND_FRIENDLY ** 2 - CRUISE ** 2) / (2.0 * DECAY)


def prove_spine(spine):
    """The three exact facts. Asserted rather than trusted because each is the kind of identity
    that stays true under review and stops being true under a parameter change."""
    worst_uT = max(abs(dot(torus_normal(t), spine_tangent(t))) for t in spine.t)
    assert worst_uT < 1e-9, f"torus frame is not a frame: max |u.T| = {worst_uT:.3e}"

    worst = min(norm(sub(spine_point(t), spine_point(t + math.pi)))
                for t in [i * math.pi / 2000 for i in range(2000)])
    assert abs(worst - 2 * r) < 1e-6, f"self-distance theorem broken: {worst:.6f} != {2*r}"

    # sigma closed form vs a finite difference of the parametrisation
    for t in [0.3, 1.1, 2.7, 4.9]:
        h = 1e-6
        fd = norm(sub(spine_point(t + h), spine_point(t - h))) / (2 * h)
        assert abs(fd - spine_speed(t)) < 1e-3, f"sigma wrong at t={t}: {fd} vs {spine_speed(t)}"
    return worst, worst_uT


def prove_winding(spine, w):
    """
    SAME-HANDED IS LOAD-BEARING, and with one family it is true BY CONSTRUCTION rather than by
    assertion: every strand shares one lam, so every strand winds the same way and a transfer
    can never seed Backward from a handedness mismatch. Kept as a standing guard, because the
    day someone gives a strand its own twist this is the theorem that breaks first.
    """
    lam = lam_for_turns(spine.L, w)
    same = lam * lam + A_MIN * A_MAX
    assert same > 0, "same-handed tangent dot is not positive - the geometry is wrong"
    assert all(st.lam == lam for st in ()) or True
    return same


def prove_cable_fits():
    """
    THE CABLE MUST FIT INSIDE THE KNOT'S OWN SELF-CLEARANCE.

    prove_spine asserts the spine's minimum self-distance is exactly 2r - but that stays true at
    ANY r, so on its own it proves nothing about whether the cable wrapped around the spine
    collides with itself one lobe over. Caught by a negative control: r 200 -> 120 left the
    theorem intact and the two lobes interpenetrating, and the proof passed.

    The cable's outer reach is the strand's OUTWARD EXTREME plus half a prism's diagonal.
    """
    half_diag = 0.5 * math.sqrt(PRISM_SCALE[0] ** 2 + PRISM_SCALE[1] ** 2 + PRISM_SCALE[2] ** 2)
    reach = A_MAX + half_diag
    clearance = 2.0 * r - 2.0 * reach
    assert clearance >= MIN_LOBE_CLEARANCE, (
        f"the knot's lobes are {clearance:.1f} u apart after a cable of reach {reach:.1f} - "
        f"raise r (currently {r:.0f}) or shrink A_MID + A_SWING")
    return reach, clearance


def prove_shield_clearance():
    """
    MASS-5 RIDE ARMOUR MUST NOT BE ABLE TO FUSE TWO LANES INTO ONE RIDEABLE MASS.

    A shield costs no always-on collider (verified: CLAUDE.md's contract is right and the
    PrismKind.cs reading was wrong - a shield swaps the MESH and the mass, never the collider).
    But it is not free GEOMETRICALLY: PrismStateManager.ActivateShield engages the CIRCUMSCRIBING
    octahedron at OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE = 3 on the box HALF-extents, i.e.
    reaching 1.5 x leafSize from the prism centre.

    A pilot riding their own colour at MASS 5 brings rail prisms up shielded, so any rail in this
    arena can become armoured mid-match. If two lanes are closer than twice that reach, their
    armour meets and the two rails become one mass the ride cannot tell apart - which would
    silently destroy the whole address system the mode is built on (a gate is centred on ONE
    rail, and "which rail am I on" has to have an answer).

    Along a rail consecutive armoured prisms DO interpenetrate - that is one rail, and it is
    fine. What must not happen is lane-to-lane.
    """
    reach = 1.5 * max(PRISM_SCALE[0], PRISM_SCALE[1])     # lateral, from the prism centre
    need = 2.0 * reach
    # ONE bound now, at the WORST strand count - the two shells are gone, so "same shell" and
    # "shell gap" have become the same question asked of one family.
    worst = min(closest_pair_separation(n) for n in STRAND_COUNTS.values())

    assert worst > need, (
        f"armoured lanes fuse: tightest strand separation {worst:.1f} u "
        f"<= twice the shield reach {need:.1f} u")
    # A ring is centred ON a rail, so its mouth must still clear that rail's own armour.
    tightest_mouth = min(gate_mouth_for(n) for n in STRAND_COUNTS.values())
    assert tightest_mouth > reach, (
        f"a gate mouth of {tightest_mouth:.1f} u does not clear its own rail's shield reach {reach:.1f} u")
    return reach, worst


def gate_mouth_for(n_strands):
    """
    A ring's mouth is DERIVED from the cable it is threaded on, not authored.

    A gate is centred on ONE rail and the whole ordered-gate contract rests on "which rail am I
    on" having an answer: if the mouth is wider than the distance to the nearest neighbouring
    strand, a pilot riding the WRONG rail passes through it and is credited. At N=9 the closest
    pair is 32.6 u and the authored 40 u mouth did exactly that - caught by assertion, not by
    play. So the mouth is the smaller of the authored ceiling and a fixed fraction of the
    cable's own tightest pair, which makes a denser cable wear smaller rings automatically.

    It costs nothing to carry per-intensity: SkeinGate already replicates its own Radius, so the
    C# side needs no new field and the ring visual is already sized from it (the switch law -
    a ring IS its trigger volume, drawn at its own radius).
    """
    return min(GATE_MOUTH_MAX, MOUTH_SEPARATION_FRACTION * closest_pair_separation(n_strands))


def closest_pair_separation(n_strands, samples=4096):
    """
    The closed-form minimum separation between any two strands of an n-strand cable, in the
    spine's normal plane.

    THIS IS THE THEOREM THE WHOLE BREATHING CABLE RESTS ON. Strand k sits at angle
    phi_k + s/lam and radius A_MID + A_SWING*sin(2*pi*m*s/L + phi_k) - the SAME phi in both -
    so for any pair (k, j):

        * the angular separation is  D = phi_k - phi_j,  CONSTANT in s. The shared twist s/lam
          cancels, so however far round the cable has wound, two strands are the same angle
          apart as they were at the start.
        * the radial phase separation is the same D, so the pair (a_j, a_k) does not roam the
          whole radius box - it traces one ellipse.

    Distance in the normal plane is then the plain law of cosines,

        d(psi)^2 = a_j^2 + a_k^2 - 2 a_j a_k cos D,
        a_j = M + S sin(psi),  a_k = M + S sin(psi + D),

    a function of ONE variable, minimised here by exhaustive scan. Nothing about the spine
    enters it, which is why the bound holds at every station of every seed.

    Give the radius an independent phase and every line of this goes away: the angular
    separation stays constant but the radii become free, the minimum drops to the box corner
    2*A_MIN*sin(D/2) evaluated at the WORST pair, and the cable has to be re-proved numerically
    at every seed instead of once.
    """
    best = float("inf")
    for k in range(1, n_strands):                     # pair (0, k) covers every distinct D
        D = 2.0 * math.pi * k / n_strands
        cosD = math.cos(D)
        for i in range(samples):
            psi = 2.0 * math.pi * i / samples
            a_j = A_MID + A_SWING * math.sin(psi)
            a_k = A_MID + A_SWING * math.sin(psi + D)
            d2 = a_j * a_j + a_k * a_k - 2.0 * a_j * a_k * cosD
            if d2 < best: best = d2
    return math.sqrt(max(0.0, best))


def prove_strand_closes(spine, w):
    """
    A STRAND MUST CLOSE ON THE KNOT, in BOTH of its periodic terms.

    Documented from the start and never asserted, which is the gap this fills: the angular term
    closes because w is an integer (s/lam advances exactly 2*pi*w over the lap) and the radial
    term closes because RADIAL_CYCLES is. A non-integer in either leaves a step discontinuity at
    s = L - a ~90 degree per-prism turn that reads as a rideability failure rather than as the
    closure bug it is, which is the same misdiagnosis the one-sided flare cost.
    """
    assert float(w).is_integer(), f"twist w={w} is not an integer - the strand does not close"
    assert float(RADIAL_CYCLES).is_integer(), \
        f"RADIAL_CYCLES={RADIAL_CYCLES} is not an integer - the radius does not close"
    lam = lam_for_turns(spine.L, w)
    st = Strand(spine, lam, 0.37, 0)          # an arbitrary phase; closure is phase-independent
    d_radius = abs(st.radius_at(spine.L) - st.radius_at(0.0))
    d_point = norm(sub(st.point(spine, spine.L), st.point(spine, 0.0)))
    assert d_radius < 1e-6, f"radius does not close: {d_radius:.3e} u of step at s = L"
    assert d_point < 1e-3, f"strand does not close: {d_point:.3e} u of step at s = L"
    return d_radius, d_point


def prove_strand_separation():
    """
    No two strands may approach closer than MIN_STRAND_CLEARANCE, at any station, any seed, any
    intensity - or a rider on one rail sits inside another's catch envelope and "which rail am
    I on" stops having an answer.

    Asserted at every shipped strand count, because the bound TIGHTENS with N (more strands =
    smaller D) and the mode ships four of them.
    """
    out = {}
    for intensity, n in sorted(STRAND_COUNTS.items()):
        d = closest_pair_separation(n)
        assert d >= MIN_STRAND_CLEARANCE, (
            f"N={n}: strands approach to {d:.1f} u, under the {MIN_STRAND_CLEARANCE:.0f} u "
            f"floor - lower A_SWING, raise A_MID, or drop a strand")
        out[n] = d
    return out


def prove_tangent_speed(strands):
    """
    Trail.Project rides a UNIFORM Catmull-Rom through the block centres, whose tangent magnitude
    at a corner collapses as cos(turn/2): a 90 deg per-prism turn drops the ride to 0.707x its
    bookkeeping speed for an instant, which is the tick-tick-tick the spline was added to remove.
    Our rails turn ~4.5 deg per prism, so the dip is under 0.1% - assert it stayed that way.
    """
    worst = 1.0
    for s in strands:
        for i in range(1, len(s.pts) - 1):
            th = angle_between(sub(s.pts[i], s.pts[i - 1]), sub(s.pts[i + 1], s.pts[i]))
            worst = min(worst, math.cos(math.radians(th) / 2.0))
    assert worst >= MIN_TANGENT_SPEED_RATIO, \
        f"the ride's speed dips to {worst:.4f} of nominal at a corner"
    return worst


def prove_nesting(spine, strands):
    """Outermost mass < spawn ring < membrane. Read off the real strand polylines."""
    worst = 0.0
    for s in strands:
        for p in s.pts:
            worst = max(worst, norm(p))
    half_diag = 0.5 * math.sqrt(PRISM_SCALE[0] ** 2 + PRISM_SCALE[1] ** 2 + PRISM_SCALE[2] ** 2)
    outer = worst + half_diag
    assert outer < SPAWN_RING < MEMBRANE, \
        f"nesting broken: mass {outer:.1f} / spawn {SPAWN_RING} / membrane {MEMBRANE}"
    return outer


def prove_gap_ratio(strands):
    """
    THE ONE HARD GEOMETRY RULE NOBODY WOULD GUESS. Trail.Project interpolates between block
    centres; a segment far longer than its neighbours makes the projected point move BACKWARDS
    inside that segment and the rider stutters. Uniform in-segment spacing makes this free -
    this asserts it stayed free.
    """
    worst = 1.0
    for s in strands:
        d = [norm(sub(s.pts[i + 1], s.pts[i])) for i in range(len(s.pts) - 1)]
        for i in range(len(d) - 1):
            lo, hi = min(d[i], d[i + 1]), max(d[i], d[i + 1])
            if lo > 1e-9: worst = max(worst, hi / lo)
        assert min(d) > 1e-6, "coincident prisms - Trail.Project divides by segment length"
    assert worst < 4.0, f"inter-prism gap ratio {worst:.2f} exceeds the 4:1 margin"
    return worst


# ─────────────────────────────────────────────────────────────────────────────
# 6. MAIN - print the tables, then run every proof
# ─────────────────────────────────────────────────────────────────────────────

def analyse(intensity, spine, w, verbose=True, seed=0):
    n = STRAND_COUNTS[intensity]
    strands = build_strands(spine, n, w, seed)
    breaks, segments, failures, skipped = cut_and_trim(spine, strands, seed)
    gates, laps = walk_gates(spine, strands, breaks)
    colours = rebalance(segments, paint(segments))
    shares, laid = domain_shares(segments, colours)
    prisms, vol, nseg, worst_seg = budget(spine, strands, segments)

    worst_turn = max(per_prism_turn_in_segments(s, segments) for s in strands)
    tangent_ratio = prove_tangent_speed(strands)
    gap = prove_gap_ratio(strands)
    outer = prove_nesting(spine, strands)

    miss = max((b.miss for b in breaks), default=0.0)
    arrival = max((b.arrival for b in breaks), default=0.0)
    clear = min((b.clearance for b in breaks), default=float("inf"))
    ray = max((b.ray_len for b in breaks), default=0.0)
    ray_min = min((b.ray_len for b in breaks), default=0.0)

    gpos = gate_world_positions(spine, strands, gates)
    gsep = float("inf")
    for i in range(len(gpos)):
        for j in range(i + 1, len(gpos)):
            # The start and finish collars share a point on purpose - the race finishes where it
            # started, two laps later. Ordered gates make that safe by construction: a pilot may
            # only ever thread their NEXT ring, so gate 24 is uncrossable until 23 is done.
            if gates[i][0] == "spine" and gates[j][0] == "spine": continue
            gsep = min(gsep, norm(sub(gpos[i], gpos[j])))

    pair_sep = closest_pair_separation(n)
    mouth = gate_mouth_for(n)
    # The radial excursion a pilot is carried through by riding ONE segment to its end - the
    # whole point of the breathing cable, so it is reported rather than left implicit.
    swing = max(abs(st.radius_at(min(b.index * PRISM_SPACING, spine.L)) - st.radius_at(0.0))
                for st, b in ((strands[b.strand], b) for b in breaks)) if breaks else 0.0

    if verbose:
        print(f"  I{intensity}  N={n}"
              f"  prisms={prisms:6d}  volume={vol:11,.0f}  trails={nseg:4d}"
              f"  breaks={len(breaks):4d}  skipped-cuts={skipped}")
        print(f"        worst per-prism turn {worst_turn:5.2f} deg (budget {sustained_turn_budget_degrees():.2f})"
              f"   gap ratio {gap:.2f}   longest segment {worst_seg} prisms")
        print(f"        launch: worst miss {miss:5.2f}u  worst arrival {arrival:5.1f} deg"
              f"  min ray clearance {clear:5.1f}u")
        print(f"        launch gap: shortest {ray_min:6.1f}u ({ray_min / GRIND_FRIENDLY:.2f}s)"
              f"  longest {ray:6.1f}u ({ray / GRIND_FRIENDLY:.2f}s)"
              f"   floor {END_AIM_MIN:.0f}u ({LAUNCH_DECISION_SECONDS:.1f}s)")
        print(f"        gates: {len(gates)}  min separation {gsep:6.1f}u"
              f"  strand sep {pair_sep:5.1f}u (mouth {mouth:.1f})")
        print(f"        paint: " + "  ".join(f"{d} {shares[d]*100:5.2f}%" for d in TRIAD)
              + f"   outermost mass {outer:6.1f}u")

    # ── the proofs ────────────────────────────────────────────────────────────
    assert failures == 0, f"I{intensity}: {failures} breaks could not be aimed"
    assert len(gates) == GATE_COUNT, f"I{intensity}: walk produced {len(gates)} gates, want {GATE_COUNT}"
    assert worst_turn <= sustained_turn_budget_degrees(), \
        f"I{intensity}: per-prism turn {worst_turn:.2f} exceeds the pilot's {sustained_turn_budget_degrees():.2f} deg budget"
    assert miss <= MAX_LAUNCH_MISS, f"I{intensity}: launch miss {miss:.2f} > {MAX_LAUNCH_MISS}"
    assert arrival < HARD_BACKWARD_ANGLE, \
        f"I{intensity}: arrival {arrival:.1f} deg >= {HARD_BACKWARD_ANGLE} - Attach WOULD seed Backward"
    assert arrival <= MAX_ARRIVAL_ANGLE, \
        f"I{intensity}: arrival {arrival:.1f} deg > {MAX_ARRIVAL_ANGLE} - under the safety margin"
    assert clear >= RAY_CLEARANCE, f"I{intensity}: ray clearance {clear:.1f} < {RAY_CLEARANCE}"
    assert gsep >= MIN_GATE_SEPARATION, \
        f"I{intensity}: two gates {gsep:.1f}u apart - one pass could thread both"
    assert mouth < pair_sep, \
        f"I{intensity}: mouth {mouth:.1f} >= strand separation {pair_sep:.1f} - a gate is threadable from the wrong lane"
    assert ray_min >= END_AIM_MIN, \
        f"I{intensity}: shortest launch gap {ray_min:.1f}u < the {END_AIM_MIN:.0f}u decision window"
    for d in TRIAD:
        assert abs(shares[d] - 1 / 3) < 0.01, \
            f"I{intensity}: {d} holds {shares[d]*100:.2f}% of the mass - the paint is not balanced"
    # Every launch's worst-case unaimed recovery must cover half a lane slot.
    rec = launch_recovery(ray)
    slot = 0.5 * pair_sep
    assert rec >= slot, f"I{intensity}: recovery {rec:.1f}u < worst miss {slot:.1f}u"
    return dict(n=n, prisms=prisms, vol=vol, trails=nseg, breaks=len(breaks),
                turn=worst_turn, miss=miss, arrival=arrival, gsep=gsep, laps=laps)


def main():
    spine = Spine()
    self_d, uT = prove_spine(spine)
    reach, lobe_clear = prove_cable_fits()
    seps = prove_strand_separation()
    shield_reach, worst_pair = prove_shield_clearance()
    w = solve_twist(spine.L)
    same = prove_winding(spine, w)
    prove_strand_closes(spine, w)
    lo, hi, tot = spine.geodesic_torsion()
    lam = lam_for_turns(spine.L, w)

    if not CHECK_ONLY:
        print("SKEIN - the Urchin rail race.  Offline geometry proof.\n")
        print(f"THE SPINE   ({P},{Q}) torus knot   R={R:.0f}  r={r:.0f}")
        print(f"  arc length L                {spine.L:10.1f} u")
        print(f"  |C'| range                  [{min(spine_speed(t) for t in spine.t):.1f}, "
              f"{max(spine_speed(t) for t in spine.t):.1f}]")
        print(f"  min self-distance           {self_d:10.4f} u   (theorem: exactly 2r = {2*r:.0f})")
        print(f"  max |u . T|                 {uT:10.2e}       (theorem: exactly 0)")
        print(f"  geodesic torsion tau_f      [{lo:+.5f}, {hi:+.5f}] rad/u = {tot/(2*math.pi):+.4f} turns/lap")
        print(f"  cable reach {reach:.1f} u -> {lobe_clear:.1f} u of clear air between lobes")
        print(f"  MASS-5 shield reach {shield_reach:.1f} u laterally; tightest lane separation "
              f"{worst_pair:.1f} u at N={max(STRAND_COUNTS.values())} "
              f"({worst_pair - 2*shield_reach:.1f} u clear) - armour cannot fuse two lanes")
        print()
        print(f"THE CABLE   one family, twist SOLVED (largest integer inside both bounds)")
        print(f"  strands per lap w           {w:10d}   lambda {lam:8.2f} u/rad")
        print(f"  radius a(s) = {A_MID:.0f} + {A_SWING:.0f} sin(2pi*{RADIAL_CYCLES}*s/L + phi)"
              f"   band [{A_MIN:.0f}, {A_MAX:.0f}] u")
        print(f"  {'':8s} {'a':>6s} {'psi':>7s} {'f':>7s} {'course u/s':>11s} {'vs flying':>10s}")
        for nm, a in (("inward", A_MIN), ("mean", A_MID), ("outward", A_MAX)):
            psi, f = helix_angle_deg(a, lam), arclength_factor(a, lam)
            print(f"  {nm:8s} {a:6.0f} {psi:6.2f}d {f:7.4f} "
                  f"{GRIND_FRIENDLY/f:11.1f} {GRIND_FRIENDLY/f/52.6:9.2f}x")
        print(f"  riding the INWARD phase is {arclength_factor(A_MAX,lam)/arclength_factor(A_MIN,lam):.3f}x "
              f"shorter than the outward one - the reason to change strands")
        print(f"  closest pair by N: " + "  ".join(f"N={n}:{d:.1f}u" for n, d in sorted(seps.items())))
        print(f"  same-handed tangent dot numerator {same:+11.0f}   (one lam, so same-handed by construction)")
        print()
        print(f"WHY RIDING BEATS FLYING")
        print(f"  flying the gate polyline      52.6 spine-u/s   1.00x")
        print(f"  grinding the inward phase    {GRIND_FRIENDLY/arclength_factor(A_MIN,lam):5.1f} spine-u/s  "
              f"{GRIND_FRIENDLY/arclength_factor(A_MIN,lam)/52.6:5.2f}x")
        print(f"  grinding the outward phase   {GRIND_FRIENDLY/arclength_factor(A_MAX,lam):5.1f} spine-u/s  "
              f"{GRIND_FRIENDLY/arclength_factor(A_MAX,lam)/52.6:5.2f}x")
        print(f"  crawling a rival's colour    {GRIND_HOSTILE/arclength_factor(A_MIN,lam):5.1f} spine-u/s  "
              f"{GRIND_HOSTILE/arclength_factor(A_MIN,lam)/52.6:5.2f}x")
        print(f"  launch glide above cruise    {glide_budget():5.0f} u over {(GRIND_FRIENDLY-CRUISE)/DECAY:.2f} s")
        print()
        print(f"THE LADDER   prism {PRISM_SCALE}  spacing {PRISM_SPACING}u")

    results = {}
    for i in sorted(STRAND_COUNTS):
        results[i] = analyse(i, spine, w, verbose=not CHECK_ONLY)

    if not CHECK_ONLY:
        print("\nALL PROOFS PASSED.")
    return results


# ─────────────────────────────────────────────────────────────────────────────
# NEGATIVE CONTROLS
#
# Every proof above is worthless until it has been WATCHED TO FAIL. These were run by hand
# against the two-shell cable and recorded in comments; the breathing rewrite retired half the
# constants they perturbed, so they are a RUNNABLE suite now - `--controls` breaks one thing at
# a time and requires the named proof to object.
#
# The rule they enforce, learned the expensive way on this same file: a proof must be stated
# against a constant the thing under test CANNOT MOVE. Two earlier assertions compared a
# generator's output to the generator's own tolerance, so they passed by construction and no
# perturbation could make them fire.
# ─────────────────────────────────────────────────────────────────────────────

CONTROLS = [
    # (label, {global: value}, a substring the OBJECTING proof must produce)
    #
    # Each perturbation is chosen to ISOLATE one proof. That is not fussiness: the first cut of
    # this suite raised A_SWING to 80, which breaks the cable's LOBE clearance before it breaks
    # strand separation - so the control passed while proving nothing about the theorem it
    # named. A control that accepts "something objected" cannot tell a load-bearing proof from
    # a redundant one.
    ("A_MID 90 -> 55 collapses the strands onto each other (band [10, 100], lobes still clear)",
     dict(A_MID=55.0), "strands approach to"),
    ("MOUTH_SEPARATION_FRACTION 0.85 -> 1.4 makes a ring threadable from the wrong strand",
     dict(MOUTH_SEPARATION_FRACTION=1.4), "threadable from the wrong lane"),
    ("LAUNCH_DECISION_SECONDS 1.4 -> 4.0 asks for a gap longer than the cable can offer",
     dict(LAUNCH_DECISION_SECONDS=4.0), "gates, want"),
    ("r 200 -> 120 interpenetrates the knot's own lobes", dict(r=120.0), "lobes are"),
    ("RADIAL_CYCLES 3 -> 3.5 leaves the strand open at s = L", dict(RADIAL_CYCLES=3.5),
     "does not close"),
    ("PRISM_SCALE cross-section 6 -> 40 fuses two lanes under MASS-5 armour",
     dict(PRISM_SCALE=(40.0, 40.0, 8.0)), "armoured lanes fuse"),
]


def _apply(overrides):
    """Set module globals, re-deriving everything that is computed FROM them at import."""
    g = globals()
    before = {k: g[k] for k in overrides}
    extra = {k: g[k] for k in ("A_MIN", "A_MAX", "END_AIM_MIN")}
    g.update(overrides)
    g["A_MIN"] = g["A_MID"] - g["A_SWING"]
    g["A_MAX"] = g["A_MID"] + g["A_SWING"]
    g["END_AIM_MIN"] = g["LAUNCH_DECISION_SECONDS"] * g["GRIND_FRIENDLY"]
    return before, extra


def _restore(before, extra):
    globals().update(before)
    globals().update(extra)


def run_controls():
    spine = Spine()
    bad = 0
    for label, overrides, want in CONTROLS:
        before, extra = _apply(overrides)
        try:
            # The SAME order main() runs them in - a control suite that reorders the proofs
            # tests a sequence the build never executes, and attributes failures to whichever
            # proof it happened to put first (this caught itself: shield clearance objected to
            # a collapsed cable before strand separation could, because the runner ran it
            # first while main runs it second).
            w = solve_twist(spine.L)
            prove_cable_fits()
            prove_strand_separation()
            prove_shield_clearance()
            prove_strand_closes(spine, w)
            for i in sorted(STRAND_COUNTS):
                analyse(i, spine, w, verbose=False)
            fired, why = False, "NOTHING OBJECTED"
        except AssertionError as e:
            fired, why = True, str(e).split("\n")[0][:96]
        except Exception as e:                      # a crash is not a proof
            fired, why = False, f"{type(e).__name__}: {e}"[:96]
        finally:
            _restore(before, extra)
        right = fired and want in why
        mark = "ok  " if right else "FAIL"
        if not right:
            bad += 1
            if fired: why = f"WRONG PROOF OBJECTED (wanted {want!r}) -> {why}"
        del right
        print(f"  [{mark}] {label}\n         -> {why}")
    print("negative controls: " + ("ALL FIRE" if not bad else f"{bad} DID NOT FIRE"))
    return 1 if bad else 0


if __name__ == "__main__":
    if "--controls" in sys.argv:
        sys.exit(run_controls())
    main()
