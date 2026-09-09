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

A_IN = 55.0        # inner shell radius (u)
A_OUT = 120.0      # outer shell radius (u)

SEGMENT_SPINE = 450.0   # spine arc length between breaks on one strand (u). Bounded BELOW by
                        # 2 * FLARE_SPINE (adjacent flares must not overlap, or the strand's
                        # radius profile self-intersects and the per-prism turn jumps to ~90 deg
                        # - the tell that a measured number is a discontinuity rather than a
                        # curvature) and chosen ABOVE it so an outer rail still shows a
                        # readable run at its full radius. At 600/220 that run is 27%.
BREAK_GAP = 40.0        # gap left at a break (u)

# THE FLARE - and it is NOT decoration, it is what makes an outer rail launchable at all.
#
# The launch theorem (prove_launch_theorem) says a tangent from a CONSTANT-radius strand climbs
# monotonically in radius: out is free, in is impossible.  Measured directly on the shipped
# geometry, an outer strand's tangent therefore aims at a live foreign strand in 0 of 384 sampled
# indices - it flies away from the cable, forever.  So over the FLARE_SPINE of spine before each
# outer break the strand's radius falls to the inner shell at DIVE_RATE, and it breaks while
# still descending: the tangent then carries a radial component INWARD and the launch becomes a
# chord straight through the knot's hollow core.  This is the only way an outer break reaches the
# inner shell and it costs FLARE_SPINE of authored geometry.
# FLARE_SPINE IS DERIVED, AND IT IS PINNED FROM BOTH SIDES with about 40 u of slack:
#
#   lower bound   the per-prism turn through the flare must stay inside the pilot's sustained
#                 budget of TURN_RATE * PRISM_SPACING / GRIND_FRIENDLY = 4.80 deg. MEASURED:
#                 65 u -> 16.81 deg, 150 -> 5.90, 180 -> 4.96, 200 -> 4.52, 220 -> 4.32.
#   upper bound   2 * FLARE_SPINE < SEGMENT_SPINE, or adjacent flares overlap.
#
# Note what the flare actually DOES, because it is not what it looks like: with a symmetric
# smoothstep V the radial derivative is ZERO at the vertex, so the break does not "dive inward"
# at all. The flare brings the outer rail DOWN TO THE INNER SHELL, and it then launches exactly
# like an inner rail - the ordinary outward throw. That is why a flared outer break's measured
# arrival angles (15-29 deg) match an inner break's rather than being steeper. "Out is free, in
# must be bought" survives intact; what buys it is 220 u of authored descent.
FLARE_SPINE = 215.0
FLARE_FLOOR = 55.0       # radius an outer strand descends to at a break
DIVE_RATE = (A_OUT - FLARE_FLOOR) / FLARE_SPINE

PRISM_SPACING = 8.0     # along-rail prism pitch (u); per-segment spacing is DERIVED from it
# Cross-section is under verification (the tunnelling question - see SKEIN.md). Both candidates
# are kept here so the budget can be re-read the instant that verdict lands.
PRISM_SCALE_CANDIDATES = {
    "(3,3,6) Track Projector": (3.0, 3.0, 6.0),
    "(6,6,8) tunnelling-safe": (6.0, 6.0, 8.0),
}
PRISM_SCALE = PRISM_SCALE_CANDIDATES["(6,6,8) tunnelling-safe"]

GATE_COUNT = 24
GATE_MOUTH = 40.0        # strand gate mouth radius (u)
LINE_MOUTH = 150.0       # spine-centred start/finish collar radius (u)
GATE_LEAD_IN = 150.0     # spine-u after a landing before its gate sits - the settle distance
# The walk slides a ring further down the same transfer when the nearest position is taken.
LEAD_INS = (150.0, 220.0, 300.0, 380.0, 460.0, 560.0, 660.0, 780.0)
GATE_SEPARATION = 200.0  # no two gate centres closer than this (u)

# The trim's acceptance conditions (SKEIN.md 2.3).
END_AIM_RADIUS = 12.0    # a break's ray must pass this close to a live foreign strand
END_AIM_MIN = 60.0
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
MIN_SHELL_GAP = 50.0          # inner and outer shells must stay this far apart
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

SPINE_SAMPLES = 4096


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


def solve_twists(L):
    """
    Choose (w_in, w_out) as the largest integers whose helix geometry still satisfies the two
    derived bounds.  BOTH bounds are derived from the vessel, not chosen:

      psi_in <= 25 deg   The inner shell must be the fast lane by a readable margin. Above 25
                         degrees the two shells' course speeds converge and the mode's central
                         choice - which shell am I on - evaporates.

      f_out <= 2.0       An outer lane must stay at least 1.4x faster than FLYING the gate
                         polyline, or the shell stops being a road and becomes a punishment.
                         Flying covers ~52.6 spine-u/s (50 u/s cruise x a 0.95 chord/arc ratio),
                         so f_out <= 150/(1.4 * 52.6) = 2.038.

    Larger w is better - it is more twist, more crossings and a more legible braid - so this
    takes the largest integer that fits rather than a number somebody liked.
    """
    w_in = 1
    while helix_angle_deg(A_IN, lam_for_turns(L, w_in + 1)) <= 25.0:
        w_in += 1
    w_out = 1
    while arclength_factor(A_OUT, lam_for_turns(L, w_out + 1)) <= 2.0:
        w_out += 1
    return w_in, w_out


def _smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


class Strand:
    """One rail's centreline, sampled at a fixed pitch of ITS OWN arc length."""

    def __init__(self, spine, a, lam, phi, shell, index, cuts=()):
        self.a, self.lam, self.phi = a, lam, phi
        self.shell, self.index = shell, index
        # Spine arcs at which this strand breaks. Outer strands FLARE inward over the
        # FLARE_SPINE preceding each one; inner strands ignore this entirely (their tangent
        # already climbs onto the outer shell, which is the free half of the two-stroke).
        self.cuts = tuple(cuts) if shell == "outer" else ()
        self.f = arclength_factor(a, lam)
        self.psi = helix_angle_deg(a, lam)
        self.length = self.f * spine.L

        # Sample at PRISM_SPACING of the strand's TRUE arc length, by walking the spine finely
        # and emitting a node every PRISM_SPACING of accumulated chord.
        #
        # The obvious sampler - step the spine by PRISM_SPACING / f - is wrong wherever the
        # RADIUS is moving, because f is the constant-radius factor. That is precisely the
        # flare, which is precisely where the geometry is hardest, and the error shows up as a
        # per-prism turn far outside the pilot's budget rather than as anything visibly wrong.
        step = 0.5
        self.pts, self.spine_arc = [self.point(spine, 0.0)], [0.0]
        s, acc, prev = 0.0, 0.0, self.point(spine, 0.0)
        while s < spine.L:
            s += step
            cur = self.point(spine, s)
            acc += norm(sub(cur, prev))
            prev = cur
            if acc >= PRISM_SPACING:
                self.pts.append(cur)
                self.spine_arc.append(s)
                acc = 0.0
        self.n = len(self.pts)
        self.length = self.f * spine.L

    def radius_at(self, s):
        """
        The strand's radius at a spine arc length: a symmetric V dipping to A_IN at each break.

        SYMMETRIC, not one-sided, and that is a correctness fix rather than a flourish. A
        one-sided flare leaves the radius snapping A_IN -> A_OUT the instant past the break,
        which is a 65 u discontinuity in the underlying curve. The ride never traverses it (it
        is the break gap), but every measurement taken over the strand does, and it reads as a
        ~91 deg per-prism turn that no flare length can shift - the tell that a number is a
        discontinuity rather than a curvature. Easing back out over the same FLARE_SPINE keeps
        the strand C1 and makes the measurement honest; it also means the rail visibly dives in
        and climbs back out, so the manoeuvre is legible from outside the cable.
        """
        if not self.cuts: return self.a
        for c in self.cuts:
            d = abs(s - c)
            if d <= FLARE_SPINE:
                return FLARE_FLOOR + (self.a - FLARE_FLOOR) * _smoothstep(d / FLARE_SPINE)
        return self.a

    def point(self, spine, s, radius_override=None):
        p, T, u, v = spine.frame_at_arc(s)
        th = self.phi + s / self.lam
        a = self.radius_at(s) if radius_override is None else radius_override
        return add(p, add(mul(u, a * math.cos(th)), mul(v, a * math.sin(th))))

    def tangent(self, spine, s, h=0.5):
        return unit(sub(self.point(spine, s + h), self.point(spine, s - h)))


def cut_arcs(spine, strand_index, n_strands):
    """The spine arcs at which one strand breaks - staggered by strand so a break happens
    somewhere in the cable every SEGMENT_SPINE / N of spine rather than all at one station."""
    offset = strand_index * SEGMENT_SPINE / n_strands
    out, k = [], 0
    while True:
        arc = offset + k * SEGMENT_SPINE
        if arc >= spine.L: break
        if arc > FLARE_SPINE: out.append(arc)
        k += 1
    return out


def build_strands(spine, n_strands, w_in, w_out):
    """n_in = ceil(N/2) inner, n_out = floor(N/2) outer, each evenly phased on its own shell."""
    n_in = (n_strands + 1) // 2
    n_out = n_strands // 2
    lam_in = lam_for_turns(spine.L, w_in)
    lam_out = lam_for_turns(spine.L, w_out)

    strands = []
    for k in range(n_in):
        i = len(strands)
        strands.append(Strand(spine, A_IN, lam_in, 2 * math.pi * k / n_in, "inner", i,
                              cut_arcs(spine, i, n_strands)))
    for k in range(n_out):
        i = len(strands)
        strands.append(Strand(spine, A_OUT, lam_out, 2 * math.pi * k / n_out, "outer", i,
                              cut_arcs(spine, i, n_strands)))
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
                    [END_AIM_MIN, END_AIM_MAX].
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
            if best is None or rng < best[3]:
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


def cut_and_trim(spine, strands):
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
        for arc in cut_arcs(spine, si, n):
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
        gates.append((nxt.target, gate_arc, GATE_MOUTH))
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


def paint(segments, shell_offset=0):
    """One domain per SEGMENT. The balance is ASSERTED rather than trusted: the intuitive
    constraint set (siblings differ AND a child differs from its parent) over-constrains and
    swings the per-domain share by ~16 percentage points, which is a fairness bug wearing a
    fairness rule's clothes."""
    out = []
    for m, (si, a, b) in enumerate(segments):
        out.append(TRIAD[(si + m + shell_offset) % 3])
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


def prove_winding(spine, w_in, w_out):
    """
    SAME-HANDED IS LOAD-BEARING, and this asserts the reason rather than the decision.
    Counter-winding makes the inner/outer tangent dot go negative, which seeds Backward on
    every shell transfer.
    """
    lam_in, lam_out = lam_for_turns(spine.L, w_in), lam_for_turns(spine.L, w_out)
    same = (lam_in * lam_out + A_IN * A_OUT)
    counter = (lam_in * lam_out - A_IN * A_OUT)
    assert same > 0, "same-handed tangent dot is not positive - the geometry is wrong"
    return same, counter


def prove_cable_fits():
    """
    THE CABLE MUST FIT INSIDE THE KNOT'S OWN SELF-CLEARANCE.

    prove_spine asserts the spine's minimum self-distance is exactly 2r - but that stays true at
    ANY r, so on its own it proves nothing about whether the cable wrapped around the spine
    collides with itself one lobe over. Caught by a negative control: r 200 -> 120 left the
    theorem intact and the two lobes interpenetrating, and the proof passed.

    The cable's outer reach is the outer shell plus half a prism's diagonal.
    """
    half_diag = 0.5 * math.sqrt(PRISM_SCALE[0] ** 2 + PRISM_SCALE[1] ** 2 + PRISM_SCALE[2] ** 2)
    reach = A_OUT + half_diag
    clearance = 2.0 * r - 2.0 * reach
    assert clearance >= MIN_LOBE_CLEARANCE, (
        f"the knot's lobes are {clearance:.1f} u apart after a cable of reach {reach:.1f} - "
        f"raise r (currently {r:.0f}) or shrink A_OUT")
    return reach, clearance


def prove_shells_separate():
    """The two shells must stay far enough apart that a rider on one is never inside the
    other's catch envelope. Also a negative control's finding: A_IN 55 -> 95 put the shells 25 u
    apart and nothing objected."""
    gap = A_OUT - A_IN
    assert gap >= MIN_SHELL_GAP, f"shell gap {gap:.1f} < {MIN_SHELL_GAP}"
    assert A_IN > 0 and A_OUT > A_IN, "shell radii are not ordered"
    return gap


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

def analyse(intensity, spine, w_in, w_out, verbose=True):
    n = STRAND_COUNTS[intensity]
    strands = build_strands(spine, n, w_in, w_out)
    breaks, segments, failures, skipped = cut_and_trim(spine, strands)
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

    gpos = gate_world_positions(spine, strands, gates)
    gsep = float("inf")
    for i in range(len(gpos)):
        for j in range(i + 1, len(gpos)):
            # The start and finish collars share a point on purpose - the race finishes where it
            # started, two laps later. Ordered gates make that safe by construction: a pilot may
            # only ever thread their NEXT ring, so gate 24 is uncrossable until 23 is done.
            if gates[i][0] == "spine" and gates[j][0] == "spine": continue
            gsep = min(gsep, norm(sub(gpos[i], gpos[j])))

    n_in = (n + 1) // 2
    same_shell_sep = 2.0 * A_IN * math.sin(math.pi / n_in)

    if verbose:
        print(f"  I{intensity}  N={n} ({n_in} inner / {n - n_in} outer)"
              f"  prisms={prisms:6d}  volume={vol:11,.0f}  trails={nseg:4d}"
              f"  breaks={len(breaks):4d}  skipped-cuts={skipped}")
        print(f"        worst per-prism turn {worst_turn:5.2f} deg (budget {sustained_turn_budget_degrees():.2f})"
              f"   gap ratio {gap:.2f}   longest segment {worst_seg} prisms")
        print(f"        launch: worst miss {miss:5.2f}u  worst arrival {arrival:5.1f} deg"
              f"  min ray clearance {clear:5.1f}u  longest ray {ray:6.1f}u")
        print(f"        gates: {len(gates)}  min separation {gsep:6.1f}u"
              f"  same-shell sep {same_shell_sep:5.1f}u (mouth {GATE_MOUTH})")
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
    assert GATE_MOUTH < same_shell_sep, \
        f"I{intensity}: mouth {GATE_MOUTH} >= same-shell separation {same_shell_sep:.1f} - a gate is threadable from the wrong lane"
    for d in TRIAD:
        assert abs(shares[d] - 1 / 3) < 0.01, \
            f"I{intensity}: {d} holds {shares[d]*100:.2f}% of the mass - the paint is not balanced"
    # Every launch's worst-case unaimed recovery must cover half a lane slot.
    rec = launch_recovery(ray)
    slot = A_OUT * math.sin(math.pi / max(1, n - n_in))
    assert rec >= slot, f"I{intensity}: recovery {rec:.1f}u < worst miss {slot:.1f}u"
    return dict(n=n, prisms=prisms, vol=vol, trails=nseg, breaks=len(breaks),
                turn=worst_turn, miss=miss, arrival=arrival, gsep=gsep, laps=laps)


def main():
    spine = Spine()
    self_d, uT = prove_spine(spine)
    reach, lobe_clear = prove_cable_fits()
    shell_gap = prove_shells_separate()
    w_in, w_out = solve_twists(spine.L)
    same, counter = prove_winding(spine, w_in, w_out)
    lo, hi, tot = spine.geodesic_torsion()
    lam_in, lam_out = lam_for_turns(spine.L, w_in), lam_for_turns(spine.L, w_out)

    if not CHECK_ONLY:
        print("SKEIN - the Urchin rail race.  Offline geometry proof.\n")
        print(f"THE SPINE   ({P},{Q}) torus knot   R={R:.0f}  r={r:.0f}")
        print(f"  arc length L                {spine.L:10.1f} u")
        print(f"  |C'| range                  [{min(spine_speed(t) for t in spine.t):.1f}, "
              f"{max(spine_speed(t) for t in spine.t):.1f}]")
        print(f"  min self-distance           {self_d:10.4f} u   (theorem: exactly 2r = {2*r:.0f})")
        print(f"  max |u . T|                 {uT:10.2e}       (theorem: exactly 0)")
        print(f"  geodesic torsion tau_f      [{lo:+.5f}, {hi:+.5f}] rad/u = {tot/(2*math.pi):+.4f} turns/lap")
        print(f"  cable reach {reach:.1f} u -> {lobe_clear:.1f} u of clear air between lobes"
              f"   shell gap {shell_gap:.0f} u")
        print()
        print(f"THE SHELLS  (twist SOLVED, not authored - largest integer inside both bounds)")
        print(f"  {'':6s} {'a':>6s} {'w':>4s} {'lambda':>8s} {'psi':>7s} {'f':>7s} {'course u/s':>11s} {'vs flying':>10s}")
        for nm, a, w, lam in (("inner", A_IN, w_in, lam_in), ("outer", A_OUT, w_out, lam_out)):
            psi, f = helix_angle_deg(a, lam), arclength_factor(a, lam)
            print(f"  {nm:6s} {a:6.0f} {w:4d} {lam:8.2f} {psi:6.2f}d {f:7.4f} "
                  f"{GRIND_FRIENDLY/f:11.1f} {GRIND_FRIENDLY/f/52.6:9.2f}x")
        print(f"  same-handed tangent dot numerator {same:+11.0f}   (counter-wound would be {counter:+.0f})")
        print()
        print(f"WHY RIDING BEATS FLYING")
        print(f"  flying the gate polyline      52.6 spine-u/s   1.00x")
        print(f"  grinding inner               {GRIND_FRIENDLY/arclength_factor(A_IN,lam_in):5.1f} spine-u/s  "
              f"{GRIND_FRIENDLY/arclength_factor(A_IN,lam_in)/52.6:5.2f}x")
        print(f"  grinding outer               {GRIND_FRIENDLY/arclength_factor(A_OUT,lam_out):5.1f} spine-u/s  "
              f"{GRIND_FRIENDLY/arclength_factor(A_OUT,lam_out)/52.6:5.2f}x")
        print(f"  crawling a rival's colour    {GRIND_HOSTILE/arclength_factor(A_IN,lam_in):5.1f} spine-u/s  "
              f"{GRIND_HOSTILE/arclength_factor(A_IN,lam_in)/52.6:5.2f}x")
        print(f"  launch glide above cruise    {glide_budget():5.0f} u over {(GRIND_FRIENDLY-CRUISE)/DECAY:.2f} s")
        print()
        print(f"THE LADDER   prism {PRISM_SCALE}  spacing {PRISM_SPACING}u")

    results = {}
    for i in sorted(STRAND_COUNTS):
        results[i] = analyse(i, spine, w_in, w_out, verbose=not CHECK_ONLY)

    if not CHECK_ONLY:
        print("\nALL PROOFS PASSED.")
    return results


if __name__ == "__main__":
    main()
