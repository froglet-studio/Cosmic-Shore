using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One station of a Breakwater course: where the breakwater hangs, which way it faces, and
    /// how wide its port is.
    ///
    /// <para><b>The port radius is ONE number doing three jobs</b> - it is the drawn switch ring,
    /// the crossing test's lateral bound, and the rim the plug's rakes are clipped to. A switch's
    /// ring IS its trigger volume, drawn at its own radius (Docs/ToySystem/ARCHITECTURE.md, "The
    /// switch"), and here the plug is built to that same rim, so a station can never advertise a
    /// mouth it does not have or weld shut a mouth wider than the one it drew.</para>
    /// </summary>
    public readonly struct BreakwaterStation
    {
        /// <summary>World position, after the cell offset has been applied by the caller.</summary>
        public readonly Vector3 Position;

        /// <summary>Unit; the direction the course flows through the port.</summary>
        public readonly Vector3 Axis;

        public readonly float PortRadius;

        public BreakwaterStation(Vector3 position, Vector3 axis, float portRadius)
        {
            Position = position;
            Axis = axis;
            PortRadius = portRadius;
        }
    }

    /// <summary>
    /// Tuning for one generated course. Every number here is geometry a Sparrow has to fly or
    /// shoot, so none of it is picked by eye: the whole ladder is swept over 400 seeds x 4
    /// intensities by <c>Tools/Build/breakwater_arena.py</c>, which is the AUTHORITY this struct
    /// mirrors - the same file the asset generator imports to derive the cell's PhaseThresholds,
    /// so the arena's difficulty and the arena's mass cannot drift apart.
    /// </summary>
    public struct BreakwaterCourseSettings
    {
        public int StationCount;

        /// <summary>Course stays outside this - the nucleus, or the authored floor when there is none.</summary>
        public float InnerRadius;

        /// <summary>...and inside this - the membrane, with margin.</summary>
        public float OuterRadius;

        /// <summary>Leg length between consecutive stations.</summary>
        public float MinStep;

        public float MaxStep;

        /// <summary>Heading change a pilot must make at a corner.</summary>
        public float MaxTurnDegrees;

        /// <summary>No two stations closer than this. DERIVED - see <see cref="BreakwaterCourse.MinSeparationFor"/>.</summary>
        public float MinSeparation;

        /// <summary>How far a station may be twisted off the flow line.</summary>
        public float AxisJitterDegrees;

        /// <summary>Hard cap on how edge-on a station may ever present to the leg you arrive on.</summary>
        public float MaxPresentDegrees;

        /// <summary>The port rim: the switch ring, and the radius the plug's rakes are clipped to.</summary>
        public float PortRadius;

        /// <summary>The spawn formation's POLE - see <see cref="BreakwaterCourse.Generate"/>.</summary>
        public Vector3 FirstStationDirection;

        public float FirstStationDistance;

        /// <summary>
        /// The Sparrow's own circumscribing radius, in world units: 12.32.
        ///
        /// <para>MEASURED from the shipped prefab rather than guessed, and the circumscribing
        /// radius rather than a half-width because a pilot may be rolled to any angle when they
        /// arrive - it is the clearance a port has to offer in every orientation. It is the floor
        /// under both of this mode's tightest gaps: the eye a pilot threads and the port an
        /// unopened plug still leaves.</para>
        /// </summary>
        public const float SparrowHullRadius = 12.32f;

        /// <summary>
        /// The threadable gap at the plug's centre: 18 units, <b>1.46x the hull radius</b>.
        ///
        /// <para>That number is the whole of the third choice this mode offers. Fire and you spend
        /// a rocket; saw and you stop dead and give up the race for a few seconds; thread and you
        /// spend nothing at all - but you fly a 1.46x clearance with danger bars a hull-width
        /// away, so a fractional roll into one is a full-stop slow and an all-element debuff. It
        /// is deliberately a CONSTANT across the ladder: the port narrows as the intensity
        /// climbs, so at intensity 1 threading is the miser's option beside a wide easy door and
        /// at intensity 4 it is very nearly the only gap left. Nothing about the eye had to change
        /// to say that.</para>
        /// </summary>
        public const float EyeRadius = 18f;

        /// <summary>Shell floor: the course never walks inside this radius. Matches the model's SHELL_INNER.</summary>
        public const float DefaultInnerRadius = 420f;

        /// <summary>Shell ceiling: 0.9x the CapsuleMembrane's authored 1200. Matches the model's SHELL_OUTER.</summary>
        public const float DefaultOuterRadius = 1080f;

        /// <summary>Station 1 sits this far along the pole. Matches the model's FIRST_STATION_DISTANCE.</summary>
        public const float DefaultFirstStationDistance = 660f;

        /// <summary>
        /// The port rim for an intensity: <b>72 / 60 / 50 / 42</b>. A measured ladder rather than
        /// a formula, because both of its ends are pinned by shipped facts and the interesting
        /// property is WHERE IT CROSSES between them.
        ///
        /// <list type="bullet">
        /// <item><b>The hard end is pinned under the blast.</b> The door-cutter is
        /// <c>AOEExplosion.prefab</c>'s spherical blast - 50 units of radius at resting Charge,
        /// 85 at Charge 10. At intensity 4 the port (42) sits UNDER the resting radius, so a pilot
        /// with no upgrade at all still clears a whole plug with one rocket: the hardest course
        /// must never require an element level the comeback system hands to whoever is losing.</item>
        /// <item><b>The easy end is pinned over it.</b> At intensity 1 the port (72) is WIDER than
        /// the resting blast, so one rocket cannot take the whole plug and WHERE you cut becomes a
        /// real choice - cut on the eye and you have widened the thread, cut on the rim and you
        /// have opened a door off the racing line. That choice is the easy level's teaching, and
        /// it exists only because 72 > 50.</item>
        /// </list>
        ///
        /// <para>The port moves two things with one number, which is why it is THE axis: a smaller
        /// port means a smaller dish (x1.75), so the hardest course is also the poorest ammo bank
        /// - fewer prisms in reach means fewer of the 50 that buy the next rocket.</para>
        /// </summary>
        public static float PortRadiusForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new[] { 72f, 60f, 50f, 42f }[i - 1];
        }

        /// <summary>
        /// The shipped course shape per intensity. INTENSITY IS THE COURSE AND THE DOOR, not the
        /// arena's size: station COUNT is constant (it is the end-game target, authored once in
        /// <c>EndConditionOverridesSO</c>) so a match is the same length at every level and the
        /// four are comparable - the same reasoning as Rampage, where the forest is identical at
        /// all four and only the pressure changes.
        ///
        /// <para><b>The leg and turn rows run OPPOSITE ways, and that is measured rather than
        /// chosen.</b> An earlier cut ran intensity 1 at legs 340-520 with a 40 degree cap, on the
        /// obvious reasoning that the gentlest corners belong at the easiest level - and it failed
        /// to generate on <b>21% of seeds</b>. Inside a 660-unit-thick shell a long leg with
        /// little turn available walks into the wall and cannot come back: long legs and tight
        /// corners are the same constraint pulling in opposite directions. So the ladder shortens
        /// the legs as it tightens the doors and lets the corners open, which is also the right
        /// feel - a hard course is a busy one, not a sprawling one.</para>
        ///
        /// <para>Every row is swept by <c>Tools/Build/breakwater_arena.py</c> and asserted by
        /// <c>BreakwaterCourseTests</c>: no seed fails to generate, the turn and presentation caps
        /// hold, no two ports come within the derived separation, and every corner clears the
        /// Sparrow's turning circle at the TRANSIENT CEILING (min turn radius 130.1u = full
        /// throttle boosting at the top of the overcharge band) - the state in which a racer is
        /// least able to correct.</para>
        /// </summary>
        public static BreakwaterCourseSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            float port = PortRadiusForIntensity(i);
            float minStep = new[] { 300f, 300f, 290f, 275f }[i - 1];

            return new BreakwaterCourseSettings
            {
                MinStep = minStep,
                MaxStep = new[] { 460f, 450f, 440f, 420f }[i - 1],
                MaxTurnDegrees = new[] { 45f, 50f, 55f, 60f }[i - 1],
                AxisJitterDegrees = new[] { 22f, 28f, 34f, 40f }[i - 1],
                MaxPresentDegrees = new[] { 50f, 54f, 58f, 62f }[i - 1],
                PortRadius = port,
                MinSeparation = BreakwaterCourse.MinSeparationFor(port, minStep),

                // The shell and the pole are properties of the ARENA rather than of the level, so
                // they are the same at all four and are seeded here rather than left to the
                // caller: the model hardcodes them, and a caller that forgot one would walk a
                // course the measured PhaseThresholds do not describe. A controller with a real
                // cell to read may still overwrite them - Cell.ExpectedNucleusWorldRadius returns
                // 0 when there is no nucleus, so the inner shell needs exactly this fallback.
                InnerRadius = DefaultInnerRadius,
                OuterRadius = DefaultOuterRadius,
                FirstStationDirection = Vector3.up,
                FirstStationDistance = DefaultFirstStationDistance,

                // StationCount is deliberately NOT set. It is the end-game target and lives in
                // EndConditionOverridesSO, read by BOTH the turn monitor (the number that ends the
                // race) and the controller (how many stations to hang) - one authored value in one
                // place, so the course and the number counting it cannot drift.
            };
        }
    }

    /// <summary>
    /// Builds a Breakwater course: an ORDERED chain of stations scattered through a cell, each
    /// randomly placed and randomly oriented, that a Sparrow can actually fly.
    ///
    /// <para><b>Pure and deterministic.</b> No <c>UnityEngine.Random</c> (global state), no
    /// <c>System.Random</c> (implementation-defined across runtimes - the trap
    /// Docs/WEEKLY_CHALLENGE.md records), no <c>Time</c>, no scene access. The generator owns a
    /// fully specified xorshift32, so the same seed yields the same course on any machine and the
    /// whole thing is unit-testable offline. The server still SENDS the resulting geometry rather
    /// than the seed, so peers cannot disagree even if a transcendental differs in its last bit -
    /// determinism here buys reproducibility and testability, not the network contract.</para>
    ///
    /// <para><b>This is a deliberate FORK of <see cref="SwitchbackCourse"/>, not a reuse.</b> The
    /// two walks share a shape but not a stream: Breakwater draws the step BEFORE the heading,
    /// backtracks on a whole-station budget rather than per-station try counters, and seeds its
    /// first heading off the pole instead of off the inbound ray. Extracting a shared walk would
    /// change Switchback's RNG consumption order and silently ship that mode a different course
    /// for every seed it has ever been play-tested on. A common walk is filed for the third race,
    /// where there will be two shipped call sites to justify the shape.</para>
    ///
    /// <para><b>It is a bit-for-bit mirror of <c>Tools/Build/breakwater_arena.py</c>.</b> That
    /// file's proofs - flyability, the collider budget, the prism clamp - are statements about the
    /// arena that actually ships only while the two agree, so the RNG stream (integer arithmetic,
    /// identical on both sides) and the ORDER it is consumed in are part of the contract, not an
    /// implementation detail. The geometry is <c>float</c> here and <c>double</c> there, so a
    /// candidate sitting within float epsilon of the shell wall or the separation floor could in
    /// principle be classified differently; the sweep's tightest observed margin is 0.9 units
    /// against a 168-unit floor, five orders of magnitude clear of it.</para>
    ///
    /// <para><b>Two properties hold BY CONSTRUCTION, not by luck</b>, and both are asserted in
    /// <c>BreakwaterCourseTests</c>:</para>
    /// <list type="number">
    /// <item><b>The turn cap.</b> The heading only ever advances when a station is PLACED, and
    /// every proposal - including the one that steers away from a wall - is clamped to
    /// <see cref="BreakwaterCourseSettings.MaxTurnDegrees"/> of the previous leg. A wall can
    /// therefore never manufacture a hairpin: when there is no legal escape the walk BACKTRACKS
    /// instead of bending the rule. (Letting the heading rotate between failed attempts is the
    /// tempting shortcut and it is wrong - two 60 degree rotations compose into a 120 degree
    /// corner between two placed stations.)</item>
    /// <item><b>The presentation cap.</b> A station faces the flow BISECTOR of its corner, which
    /// sits half the turn angle off each leg. The jitter that makes it "randomly oriented" is
    /// therefore spent from what is LEFT of the cap after the corner has taken its half:
    /// <c>presentation &lt;= halfTurn + jitter &lt;= MaxPresentDegrees</c> against both the
    /// arriving and the departing leg. Without that budget a sharp corner plus full jitter yields
    /// a plug standing edge-on to the flight line - which is not a hard station, it is one whose
    /// eye cannot be threaded and whose dish shields its own plug from the blast.</item>
    /// </list>
    /// </summary>
    public static class BreakwaterCourse
    {
        /// <summary>
        /// Attempts at one station before the walk gives up and backtracks. 32 rather than
        /// Switchback's 24 because this shell is thinner (660 units against a 1200-unit membrane)
        /// and the separation floor is tighter relative to the leg, so more proposals are spent
        /// against the wall.
        /// </summary>
        public const int AttemptsPerStation = 32;

        /// <summary>
        /// How many times a CALLER should re-roll the seed before falling back to a shorter race.
        ///
        /// <para>The residual failure rate at <see cref="AttemptsPerStation"/> is about 0.1% per
        /// seed. Switchback's back-off - halve the gate count - answers a one-in-a-thousand roll by
        /// shipping that one match a different, shorter race, which is a worse outcome than the
        /// wait: three reseeds take the failure rate to about 1e-9 and keep every match the same
        /// length. Halving stays underneath as the last resort, because generation must never
        /// return empty - that would hang the turn outright.</para>
        ///
        /// <para><see cref="Generate"/> deliberately does NOT reseed itself. A generator that
        /// silently retried would make its own failure rate unobservable, and the caller is the
        /// only party that knows which seed it may legitimately move to (the server broadcasts the
        /// geometry, so it is free to pick another; a test asserting the shipped settings is
        /// not).</para>
        /// </summary>
        public const int ReseedAttempts = 3;

        /// <summary>
        /// Deterministic 32-bit xorshift. Specified arithmetic on unsigned ints, so it is
        /// identical on every runtime - unlike <c>System.Random</c>, whose sequence is a property
        /// of the implementation rather than of the seed. The Python model reproduces this
        /// verbatim, including the seed-0 fallback, which is what makes its proofs statements
        /// about the shipped arena.
        /// </summary>
        struct Rng
        {
            uint _s;

            public Rng(int seed)
            {
                // 0 is the xorshift fixed point: it would emit nothing but zeros forever.
                uint s = unchecked((uint)seed);
                _s = s != 0u ? s : 0x9E3779B9u;
            }

            public uint NextUInt()
            {
                uint x = _s;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _s = x;
                return x;
            }

            /// <summary>Uniform in [0,1).</summary>
            public float Unit() => NextUInt() / 4294967296f;

            public float Range(float a, float b) => a + (b - a) * Unit();
        }

        /// <summary>
        /// The minimum distance between any two stations, <c>min(0.9 * minStep, 4 * portRadius)</c>.
        ///
        /// <para><b>DERIVED, never authored</b>, and the derivation is load-bearing in both
        /// directions. Four port radii is "the two mouths are clearly separate places" - close
        /// enough and a pilot cannot tell which ring is theirs, and the ordered-station rule stops
        /// reading as a course. But the value can never reach the minimum leg, because
        /// <see cref="TooClose"/> tests a candidate against EVERY placed station <b>including its
        /// immediate predecessor</b>: a separation above the shortest leg rejects most of the step
        /// range before the walk has even considered the geometry, and the walk starves. An
        /// earlier cut that authored the separation directly failed 21% of seeds this way, and the
        /// symptom was a course that would not generate rather than a course that looked wrong -
        /// which is why it is computed from the two numbers it is actually a function of.</para>
        /// </summary>
        public static float MinSeparationFor(float portRadius, float minStep) =>
            Mathf.Min(0.9f * minStep, 4f * portRadius);

        /// <summary>
        /// The course, or <c>null</c> when the walk could not satisfy its own constraints inside
        /// the attempt budget. About one seed in a thousand at the shipped settings - the caller
        /// re-rolls (<see cref="ReseedAttempts"/>) rather than shortening the race.
        /// </summary>
        public static List<BreakwaterStation> Generate(int seed, BreakwaterCourseSettings s)
        {
            // Unreachable at the shipped settings - the last-resort back-off floors at half of 14
            // - but a one-station "race" has no ordering to score and no leg to lay shoals along.
            if (s.StationCount < 2) return null;

            var rng = new Rng(seed);

            // STATION 1 SITS ON THE SPAWN FORMATION'S POLE, and that is a fairness rule rather
            // than a layout preference: pilots spawn on an EquatorialRing around the cell, so
            // every one of them is exactly sqrt(spawnRadius^2 + d^2) from a point on the axis of
            // that ring. Put the first station anywhere else and whoever spawned nearest it starts
            // the race ahead - and here that is worth more than a head start, because the pilot
            // who arrives first also gets the undamaged plug and the choice of how to open it.
            Vector3 pole = SafeNormalize(s.FirstStationDirection, Vector3.up);
            Vector3 first = pole * s.FirstStationDistance;

            // The opening heading is a deflection of the POLE ITSELF, not of the ray back toward
            // the cell centre: station 1 hangs at 660 inside a 420-1080 shell, so continuing
            // outward along the pole is a legal leg and turning inward is not privileged. The
            // first corner is therefore drawn from the same cone as every other one.
            var pts = new List<Vector3>(s.StationCount) { first };
            var headings = new List<Vector3>(s.StationCount) { SafeNormalize(Deflect(ref rng, pole, s.MaxTurnDegrees), Vector3.forward) };

            float separation = s.MinSeparation;

            // Four proposals per station on average before the walk is declared stuck. The budget
            // is global rather than per-station so that a walk which backtracks deep does not get
            // a fresh allowance each time it re-treads the same dead end.
            int budget = s.StationCount * AttemptsPerStation * 4;

            while (pts.Count < s.StationCount && budget > 0)
            {
                bool placed = false;

                for (int attempt = 0; attempt < AttemptsPerStation; attempt++)
                {
                    budget--;
                    if (budget <= 0) break;

                    Vector3 from = pts[pts.Count - 1];
                    Vector3 prevHeading = headings[headings.Count - 1];

                    // ORDER IS THE CONTRACT: the step is drawn BEFORE the deflection, because the
                    // model draws it in that order. Swapping two draws that are individually
                    // correct still ships a different course for every seed.
                    float step = rng.Range(s.MinStep, s.MaxStep);
                    Vector3 dir = ClampTurn(prevHeading, Deflect(ref rng, prevHeading, s.MaxTurnDegrees), s.MaxTurnDegrees);
                    Vector3 cand = from + dir * step;
                    float r = cand.magnitude;

                    if (r < s.InnerRadius || r > s.OuterRadius)
                    {
                        // Steer back toward the middle of the shell - CLAMPED to the same turn cap,
                        // so the wall cannot buy a corner the vessel could not fly. If the midline
                        // is not reachable inside the cap either, this proposal is simply spent.
                        Vector3 mid = SafeNormalize(cand, Vector3.forward) * ((s.InnerRadius + s.OuterRadius) * 0.5f);
                        dir = ClampTurn(prevHeading, SafeNormalize(mid - from, prevHeading), s.MaxTurnDegrees);
                        cand = from + dir * step;
                        r = cand.magnitude;
                        if (r < s.InnerRadius || r > s.OuterRadius) continue;
                    }

                    if (TooClose(pts, cand, separation)) continue;

                    pts.Add(cand);
                    headings.Add(dir);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    // Nothing legal from here: abandon this station and let its predecessor be
                    // re-walked. The heading is popped WITH the point, which is what keeps the
                    // turn cap a statement about placed stations rather than about attempts.
                    if (pts.Count <= 1) return null;
                    pts.RemoveAt(pts.Count - 1);
                    headings.RemoveAt(headings.Count - 1);
                }
            }

            if (pts.Count < s.StationCount) return null;

            var stations = new List<BreakwaterStation>(s.StationCount);
            for (int i = 0; i < pts.Count; i++)
            {
                // headings[i] is the direction the course ARRIVES on at station i (for station 1,
                // the opening deflection off the pole); headings[i + 1] is the one it leaves on.
                // The last station has no departure, so it faces the leg you flew to reach it.
                Vector3 incoming = headings[i];
                Vector3 outgoing = i + 1 < headings.Count ? headings[i + 1] : headings[i];

                Vector3 bisector = SafeNormalize(incoming + outgoing, incoming);
                float halfTurn = Angle(bisector, incoming);

                // What is LEFT of the presentation cap after the corner has taken its half. The
                // 0.01 floor is not a numerical guard, it is part of the stream: below it no draw
                // is made at all, so a station whose corner has eaten the whole budget consumes no
                // randomness and the seeds after it stay aligned with the model.
                float allowed = Mathf.Max(0f, Mathf.Min(s.AxisJitterDegrees, s.MaxPresentDegrees - halfTurn));
                Vector3 axis = allowed > 0.01f
                    ? SafeNormalize(Deflect(ref rng, bisector, allowed), Vector3.forward)
                    : bisector;

                stations.Add(new BreakwaterStation(pts[i], axis, s.PortRadius));
            }

            return stations;
        }

        // ── geometry helpers (pure) ──────────────────────────────────────────

        static bool TooClose(List<Vector3> pts, Vector3 cand, float minSeparation)
        {
            float sq = minSeparation * minSeparation;
            for (int i = 0; i < pts.Count; i++)
                if ((pts[i] - cand).sqrMagnitude < sq) return true;
            return false;
        }

        static Vector3 SafeNormalize(Vector3 v, Vector3 fallback) =>
            v.sqrMagnitude > 1e-10f ? v.normalized : fallback;

        /// <summary>Unsigned angle in degrees between two unit vectors.</summary>
        public static float Angle(Vector3 a, Vector3 b) =>
            Mathf.Acos(Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>Any unit vector perpendicular to <paramref name="v"/>, chosen deterministically.</summary>
        static Vector3 Perpendicular(Vector3 v)
        {
            Vector3 a = Mathf.Abs(v.x) < 0.9f ? Vector3.right : Vector3.up;
            return SafeNormalize(Vector3.Cross(v, a), Vector3.up);
        }

        /// <summary>
        /// Rotate <paramref name="v"/> by a random angle up to <paramref name="maxDegrees"/> about
        /// a random perpendicular axis - a uniform draw on the CONE around v.
        ///
        /// <para>The angle is drawn as <c>max * sqrt(u)</c> rather than <c>max * u</c>: a cone's
        /// area grows with the angle, so a linear draw crowds every deflection near zero and the
        /// course comes out nearly straight. This is the same shape as the fauna-band fix in
        /// Docs/ECOSYSTEM.md - a uniform draw in a radial coordinate is not a uniform
        /// dispersal.</para>
        ///
        /// <para><b>There is deliberately no early-out for a zero or negative angle.</b> It would
        /// make the number of draws a function of the ARGUMENT, so one degenerate corner would
        /// shift every subsequent draw and the model would walk a different course from the same
        /// seed. The only caller that can pass a small angle guards it at the call site, where the
        /// model guards it, at the same 0.01 threshold.</para>
        /// </summary>
        static Vector3 Deflect(ref Rng rng, Vector3 v, float maxDegrees)
        {
            // Two draws, in this order, always: the azimuth around the cone, then its opening.
            Vector3 spin = SafeNormalize(
                RotateAbout(Perpendicular(v), v, rng.Range(0f, 360f) * Mathf.Deg2Rad),
                Perpendicular(v));
            float angle = maxDegrees * Mathf.Sqrt(rng.Unit());
            return SafeNormalize(RotateAbout(v, spin, angle * Mathf.Deg2Rad), Vector3.forward);
        }

        /// <summary>Rodrigues rotation of <paramref name="v"/> about the unit <paramref name="axis"/>.</summary>
        static Vector3 RotateAbout(Vector3 v, Vector3 axis, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }

        /// <summary>
        /// <paramref name="want"/> when it is already within <paramref name="maxDegrees"/> of
        /// <paramref name="prev"/>, else the direction exactly that far from <paramref name="prev"/>
        /// in want's plane. This is what makes the turn cap structural: every heading the walk
        /// accepts has passed through here, including the one that steers away from the shell wall.
        /// </summary>
        static Vector3 ClampTurn(Vector3 prev, Vector3 want, float maxDegrees)
        {
            float angle = Angle(prev, want);
            if (angle <= maxDegrees) return want;

            Vector3 axis = Vector3.Cross(prev, want);
            axis = axis.sqrMagnitude < 1e-10f ? Perpendicular(prev) : axis.normalized;
            return SafeNormalize(RotateAbout(prev, axis, maxDegrees * Mathf.Deg2Rad), Vector3.forward);
        }
    }
}
