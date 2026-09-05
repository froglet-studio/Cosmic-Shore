using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One gate of a Switchback course: where the ring is, which way it faces, and how wide
    /// its mouth is. The mouth radius is BOTH the drawn ring and the crossing test's lateral
    /// bound - a switch's ring IS its trigger volume, drawn at its own radius
    /// (Docs/ToySystem/ARCHITECTURE.md, "The switch"), so the two can never drift.
    /// </summary>
    public readonly struct SwitchbackGate
    {
        public readonly Vector3 Position;
        public readonly Vector3 Axis;      // unit; the direction the course flows through the mouth
        public readonly float Radius;

        public SwitchbackGate(Vector3 position, Vector3 axis, float radius)
        {
            Position = position;
            Axis = axis;
            Radius = radius;
        }
    }

    /// <summary>
    /// Tuning for one generated course. Everything here is geometry a Dolphin has to fly, so
    /// the numbers are stated against the vessel and the cell rather than picked by eye - see
    /// <see cref="SwitchbackCourseSettings.ForIntensity"/> and SWITCHBACK.md.
    /// </summary>
    public struct SwitchbackCourseSettings
    {
        public int GateCount;
        public float InnerRadius;        // course stays outside this (the nucleus)
        public float OuterRadius;        // ...and inside this (the membrane, with margin)
        public float MinStep;            // leg length between consecutive gates
        public float MaxStep;
        public float MaxTurnDegrees;     // heading change a pilot must make at a corner
        public float MinSeparation;      // no two gates closer than this
        public float AxisJitterDegrees;  // how far a gate may be twisted off the flow line
        public float MaxPresentDegrees;  // hard cap on how edge-on a gate may ever present
        public float RingRadius;
        public Vector3 FirstGateDirection;   // the spawn formation's POLE - see Generate()
        public float FirstGateDistance;

        /// <summary>
        /// The shipped course shape per intensity. INTENSITY IS THE COURSE, not the arena: the
        /// mode runs one cell, and what climbs is how hard the gates are to fly - the mouths
        /// narrow, the corners sharpen, the legs shorten, and each gate is twisted further off
        /// the line you arrive on. Gate COUNT is deliberately constant (it is the end-game
        /// target, authored in one place), so a match is the same length at every level and the
        /// four are comparable. The same reasoning as Rampage, where the forest is identical at
        /// all four and only the pressure changes.
        ///
        /// <para>Every row is measured, not eyeballed: <c>SwitchbackCourseTests</c> sweeps 400
        /// seeds of each and asserts the caps hold, no course fails to generate, no two mouths
        /// come within a ring diameter of each other, and every corner clears the Dolphin's
        /// turning circle at BOOST (min turn radius 180.7u = 347 u/s over 110 deg/s) - the state
        /// in which a racer is least able to correct.</para>
        /// </summary>
        public static SwitchbackCourseSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            var s = new SwitchbackCourseSettings
            {
                // A leg is never shorter than the ~360u an AI needs for its approach run at
                // cruise, and never shorter than the 313u a boosted Dolphin's turning circle
                // demands at the sharpest corner these caps allow.
                MinStep = new[] { 420f, 400f, 380f, 360f }[i - 1],
                MaxStep = new[] { 680f, 650f, 620f, 580f }[i - 1],
                MaxTurnDegrees = new[] { 45f, 50f, 55f, 60f }[i - 1],
                AxisJitterDegrees = new[] { 30f, 40f, 50f, 60f }[i - 1],
                MaxPresentDegrees = new[] { 50f, 55f, 60f, 65f }[i - 1],
                // 72 -> 42. The shipped fly-through band for a vessel is 24-62 (the Scarab's
                // switch, Astro League's 62u goal mouth, Scramble's 60/54/48/42 hoops); a racer
                // arrives far faster than a ball, so level 1 opens wider than any of them and
                // level 4 lands on Scramble's tightest.
                RingRadius = new[] { 72f, 60f, 50f, 42f }[i - 1],
                // Comfortably more than two mouths across at every level, so no two gates can be
                // threaded by one pass and the wrong one can never be the nearer.
                MinSeparation = 260f,
            };
            return s;
        }
    }

    /// <summary>
    /// Builds a Switchback course: an ORDERED chain of gates scattered through a cell, each
    /// randomly placed and randomly oriented, that a Dolphin can actually fly.
    ///
    /// <para><b>Pure and deterministic.</b> No <c>UnityEngine.Random</c> (global state), no
    /// <c>System.Random</c> (implementation-defined across runtimes - the trap
    /// Docs/WEEKLY_CHALLENGE.md records), no <c>Time</c>, no scene access. The generator owns a
    /// fully specified xorshift32, so the same seed yields the same course on any machine and
    /// the whole thing is unit-testable offline. The server still SENDS the resulting geometry
    /// rather than the seed (SwitchbackController), so peers cannot disagree even if a
    /// transcendental differs in its last bit - determinism here buys reproducibility and
    /// testability, not the network contract.</para>
    ///
    /// <para><b>Two properties hold BY CONSTRUCTION, not by luck</b>, and both are asserted in
    /// SwitchbackCourseTests:</para>
    /// <list type="number">
    /// <item><b>The turn cap.</b> The heading only ever advances when a gate is PLACED, and
    /// every proposal - including the one that steers away from a wall - is clamped to
    /// <see cref="SwitchbackCourseSettings.MaxTurnDegrees"/> of the previous leg. A wall can
    /// therefore never manufacture a hairpin: when there is no legal escape the walk
    /// BACKTRACKS instead of bending the rule. (Letting the heading rotate between failed
    /// attempts is the tempting shortcut and it is wrong - two 55 degree rotations compose into
    /// a 110 degree corner between two placed gates.)</item>
    /// <item><b>The presentation cap.</b> A gate faces the flow BISECTOR of its corner, which
    /// sits half the turn angle off each leg. The jitter that makes it "randomly oriented" is
    /// therefore spent from what is LEFT of the cap after the corner has taken its half:
    /// <c>presentation &lt;= halfTurn + jitter &lt;= MaxPresentDegrees</c> against both the
    /// arriving and the departing leg. Without that budget a sharp corner plus full jitter
    /// yields a gate standing edge-on to the flight line, which is not a hard gate - it is an
    /// impossible one.</item>
    /// </list>
    /// </summary>
    public static class SwitchbackCourse
    {
        /// <summary>Attempts at one gate before the walk gives up and backtracks.</summary>
        const int AttemptsPerGate = 24;

        /// <summary>
        /// Deterministic 32-bit xorshift. Specified arithmetic on unsigned ints, so it is
        /// identical on every runtime - unlike <c>System.Random</c>, whose sequence is a
        /// property of the implementation rather than of the seed.
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
        /// The course, or null when the walk could not satisfy its own constraints inside the
        /// attempt budget. A null is a CONFIGURATION fault (a shell too thin for the step
        /// length, a separation floor larger than the shortest leg), never bad luck - the
        /// caller should widen the geometry rather than re-roll, and the tests sweep 400 seeds
        /// to prove the shipped settings never produce one.
        /// </summary>
        public static List<SwitchbackGate> Generate(int seed, SwitchbackCourseSettings s)
        {
            if (s.GateCount < 2) return null;

            var rng = new Rng(seed);

            // GATE 1 SITS ON THE SPAWN FORMATION'S POLE, and that is a fairness rule rather
            // than a layout preference: pilots spawn on an equatorial ring around the cell, so
            // every one of them is exactly sqrt(spawnRadius^2 + d^2) from a point on the axis
            // of that ring. Put the first gate anywhere else and whoever spawned nearest it
            // starts the race ahead.
            Vector3 first = SafeNormalize(s.FirstGateDirection, Vector3.up) * s.FirstGateDistance;

            var pts = new List<Vector3>(s.GateCount) { first };
            var headings = new List<Vector3>(s.GateCount) { Deflect(ref rng, SafeNormalize(-first, Vector3.forward), 35f) };
            var tries = new List<int>(s.GateCount) { 0 };

            int budget = s.GateCount * AttemptsPerGate * 4;

            while (pts.Count < s.GateCount && budget-- > 0)
            {
                if (tries[tries.Count - 1] >= AttemptsPerGate)
                {
                    if (pts.Count == 1)
                    {
                        // Cannot backtrack past the fixed first gate - re-roll its outbound leg.
                        headings[0] = Deflect(ref rng, SafeNormalize(-first, Vector3.forward), 35f);
                        tries[0] = 0;
                        continue;
                    }

                    pts.RemoveAt(pts.Count - 1);
                    headings.RemoveAt(headings.Count - 1);
                    tries.RemoveAt(tries.Count - 1);
                    tries[tries.Count - 1]++;   // do not immediately re-walk the branch we abandoned
                    continue;
                }

                tries[tries.Count - 1]++;

                Vector3 p = pts[pts.Count - 1];
                Vector3 prevHeading = headings[headings.Count - 1];
                Vector3 h = Deflect(ref rng, prevHeading, s.MaxTurnDegrees);
                float step = rng.Range(s.MinStep, s.MaxStep);
                Vector3 cand = p + h * step;
                float r = cand.magnitude;

                if (r > s.OuterRadius || r < s.InnerRadius)
                {
                    // Steer back toward the middle of the shell - CLAMPED to the same turn cap,
                    // so the wall cannot buy a corner the vessel could not fly.
                    Vector3 mid = SafeNormalize(cand, Vector3.forward) * ((s.InnerRadius + s.OuterRadius) * 0.5f);
                    h = ClampTurn(prevHeading, SafeNormalize(mid - p, prevHeading), s.MaxTurnDegrees);
                    cand = p + h * step;
                    r = cand.magnitude;
                    if (r > s.OuterRadius || r < s.InnerRadius) continue;
                }

                if (TooClose(pts, cand, s.MinSeparation)) continue;

                pts.Add(cand);
                headings.Add(h);
                tries.Add(0);
            }

            if (pts.Count < s.GateCount) return null;

            var gates = new List<SwitchbackGate>(s.GateCount);
            for (int i = 0; i < s.GateCount; i++)
            {
                Vector3 axis;
                float halfTurn;

                if (i == 0)
                {
                    axis = SafeNormalize(pts[1] - pts[0], Vector3.forward);
                    halfTurn = 0f;
                }
                else if (i == s.GateCount - 1)
                {
                    axis = SafeNormalize(pts[i] - pts[i - 1], Vector3.forward);
                    halfTurn = 0f;
                }
                else
                {
                    Vector3 inbound = SafeNormalize(pts[i] - pts[i - 1], Vector3.forward);
                    Vector3 outbound = SafeNormalize(pts[i + 1] - pts[i], inbound);
                    axis = SafeNormalize(inbound + outbound, inbound);
                    halfTurn = Angle(inbound, outbound) * 0.5f;
                }

                float jitter = Mathf.Max(0f, Mathf.Min(s.AxisJitterDegrees, s.MaxPresentDegrees - halfTurn));
                gates.Add(new SwitchbackGate(pts[i], Deflect(ref rng, axis, jitter), s.RingRadius));
            }

            return gates;
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
        /// Rotate <paramref name="v"/> by a random angle up to <paramref name="maxDegrees"/>
        /// about a random perpendicular axis - a uniform draw on the CONE around v.
        ///
        /// <para>The angle is drawn as <c>max * sqrt(u)</c> rather than <c>max * u</c>: a cone's
        /// area grows with the angle, so a linear draw crowds every deflection near zero and the
        /// course comes out nearly straight. This is the same shape as the fauna-band fix in
        /// Docs/ECOSYSTEM.md - a uniform draw in a radial coordinate is not a uniform
        /// dispersal.</para>
        /// </summary>
        static Vector3 Deflect(ref Rng rng, Vector3 v, float maxDegrees)
        {
            if (maxDegrees <= 0f) return v;

            Vector3 u = Perpendicular(v);
            Vector3 w = Vector3.Cross(v, u);
            float phi = rng.Range(0f, 2f * Mathf.PI);
            Vector3 spin = SafeNormalize(u * Mathf.Cos(phi) + w * Mathf.Sin(phi), u);
            float angle = maxDegrees * Mathf.Deg2Rad * Mathf.Sqrt(rng.Unit());
            return SafeNormalize(RotateAbout(v, spin, angle), v);
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
        /// <paramref name="prev"/>, else the direction exactly that far from
        /// <paramref name="prev"/> in want's plane. This is what makes the turn cap structural:
        /// every heading the walk accepts has passed through here or through
        /// <see cref="Deflect"/>, and neither can exceed it.
        /// </summary>
        static Vector3 ClampTurn(Vector3 prev, Vector3 want, float maxDegrees)
        {
            float angle = Angle(prev, want);
            if (angle <= maxDegrees) return want;

            Vector3 axis = Vector3.Cross(prev, want);
            axis = axis.sqrMagnitude < 1e-10f ? Perpendicular(prev) : axis.normalized;
            return SafeNormalize(RotateAbout(prev, axis, maxDegrees * Mathf.Deg2Rad), prev);
        }
    }
}
