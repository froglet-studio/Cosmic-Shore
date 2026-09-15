using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    // The gate struct that lived here is now RaceGate, in
    // Arcade/Racing/RaceCourseGeometry.cs - Headlong flies the same object on a closed
    // circuit, and two copies would have drifted at the first tuning pass.

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
        /// <summary>
        /// The Dolphin's own circumscribing radius, in world units: 2.86.
        ///
        /// <para>MEASURED from the shipped prefab rather than guessed - the eight corners of all
        /// eleven hull box colliders on <c>Dolphin.prefab</c>, pushed through their transform
        /// chains to the vessel root (root scale 1), giving a hull of 5.29 x 1.23 x 5.30 and a
        /// worst-corner distance from the origin of 2.860 on <c>TopNose</c>. The circumscribing
        /// radius rather than the half-width because a pilot may be rolled to any angle when they
        /// arrive, so it is the clearance a mouth has to offer in every orientation.</para>
        /// </summary>
        public const float DolphinHullRadius = 2.86f;

        /// <summary>
        /// How much bigger than the ship the TIGHTEST mouth is. 1.5 - "barely bigger than a
        /// Dolphin", which is what intensity 4 is for. The single dial for the whole ladder's
        /// bottom end; raising it relaxes every level but the first.
        /// </summary>
        public const float NarrowestMouthClearance = 1.5f;

        /// <summary>The widest mouth, at intensity 1. Play-tested; do not derive it.</summary>
        public const float WidestRingRadius = 72f;

        /// <summary>
        /// The mouth radius for an intensity, geometric between the two anchored ends. Pure and
        /// public so <c>SwitchbackCourseTests</c> can assert the ladder rather than a table of
        /// numbers that would have to be edited twice.
        /// </summary>
        public static float RingRadiusForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            float tightest = DolphinHullRadius * NarrowestMouthClearance;
            float ratio = Mathf.Pow(tightest / WidestRingRadius, 1f / 3f);
            return WidestRingRadius * Mathf.Pow(ratio, i - 1);
        }

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
                // 72 -> 4.3, GEOMETRIC (each level 2.56x tighter than the last). Both ends are
                // anchored and the middle is interpolated between them:
                //
                //   Level 1 is 72, the play-tested opening. Wider than any shipped fly-through in
                //   the game (the Scarab's 24u switch, Astro League's 62u goal mouth, Scramble's
                //   60/54/48/42 hoops), because a racer arrives far faster than a ball.
                //
                //   Level 4 is DolphinHullRadius x NarrowestMouthClearance - barely bigger than
                //   the ship itself, which is the whole of what intensity means here.
                //
                // Geometric rather than linear because the ends are an order of magnitude apart:
                // linear would spend three levels barely narrowing and then fall off a cliff,
                // where a constant ratio makes every step the same increment of difficulty. Note
                // the cost of anchoring both ends - level 2 is a big drop from level 1 (72 -> 28),
                // which is arithmetic rather than a judgement, and the one number to retune if it
                // reads as a cliff is NarrowestMouthClearance.
                RingRadius = RingRadiusForIntensity(i),
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
        /// The course, or null when the walk could not satisfy its own constraints inside the
        /// attempt budget. A null is a CONFIGURATION fault (a shell too thin for the step
        /// length, a separation floor larger than the shortest leg), never bad luck - the
        /// caller should widen the geometry rather than re-roll, and the tests sweep 400 seeds
        /// to prove the shipped settings never produce one.
        /// </summary>
        public static List<RaceGate> Generate(int seed, SwitchbackCourseSettings s)
        {
            if (s.GateCount < 2) return null;

            var rng = new RaceCourseGeometry.Rng(seed);

            // GATE 1 SITS ON THE SPAWN FORMATION'S POLE, and that is a fairness rule rather
            // than a layout preference: pilots spawn on an equatorial ring around the cell, so
            // every one of them is exactly sqrt(spawnRadius^2 + d^2) from a point on the axis
            // of that ring. Put the first gate anywhere else and whoever spawned nearest it
            // starts the race ahead.
            Vector3 first = RaceCourseGeometry.SafeNormalize(s.FirstGateDirection, Vector3.up) * s.FirstGateDistance;

            var pts = new List<Vector3>(s.GateCount) { first };
            var headings = new List<Vector3>(s.GateCount) { RaceCourseGeometry.Deflect(ref rng, RaceCourseGeometry.SafeNormalize(-first, Vector3.forward), 35f) };
            var tries = new List<int>(s.GateCount) { 0 };

            int budget = s.GateCount * AttemptsPerGate * 4;

            while (pts.Count < s.GateCount && budget-- > 0)
            {
                if (tries[tries.Count - 1] >= AttemptsPerGate)
                {
                    if (pts.Count == 1)
                    {
                        // Cannot backtrack past the fixed first gate - re-roll its outbound leg.
                        headings[0] = RaceCourseGeometry.Deflect(ref rng, RaceCourseGeometry.SafeNormalize(-first, Vector3.forward), 35f);
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
                Vector3 h = RaceCourseGeometry.Deflect(ref rng, prevHeading, s.MaxTurnDegrees);
                float step = rng.Range(s.MinStep, s.MaxStep);
                Vector3 cand = p + h * step;
                float r = cand.magnitude;

                if (r > s.OuterRadius || r < s.InnerRadius)
                {
                    // Steer back toward the middle of the shell - CLAMPED to the same turn cap,
                    // so the wall cannot buy a corner the vessel could not fly.
                    Vector3 mid = RaceCourseGeometry.SafeNormalize(cand, Vector3.forward) * ((s.InnerRadius + s.OuterRadius) * 0.5f);
                    h = RaceCourseGeometry.ClampTurn(prevHeading, RaceCourseGeometry.SafeNormalize(mid - p, prevHeading), s.MaxTurnDegrees);
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

            var gates = new List<RaceGate>(s.GateCount);
            for (int i = 0; i < s.GateCount; i++)
            {
                Vector3 axis;
                float halfTurn;

                if (i == 0)
                {
                    axis = RaceCourseGeometry.SafeNormalize(pts[1] - pts[0], Vector3.forward);
                    halfTurn = 0f;
                }
                else if (i == s.GateCount - 1)
                {
                    axis = RaceCourseGeometry.SafeNormalize(pts[i] - pts[i - 1], Vector3.forward);
                    halfTurn = 0f;
                }
                else
                {
                    Vector3 inbound = RaceCourseGeometry.SafeNormalize(pts[i] - pts[i - 1], Vector3.forward);
                    Vector3 outbound = RaceCourseGeometry.SafeNormalize(pts[i + 1] - pts[i], inbound);
                    axis = RaceCourseGeometry.SafeNormalize(inbound + outbound, inbound);
                    halfTurn = RaceCourseGeometry.Angle(inbound, outbound) * 0.5f;
                }

                float jitter = Mathf.Max(0f, Mathf.Min(s.AxisJitterDegrees, s.MaxPresentDegrees - halfTurn));
                gates.Add(new RaceGate(pts[i], RaceCourseGeometry.Deflect(ref rng, axis, jitter), s.RingRadius));
            }

            return gates;
        }

        // ── geometry helpers ─────────────────────────────────────────────────
        // Everything except TooClose now lives in RaceCourseGeometry, shared with Headlong.

        static bool TooClose(List<Vector3> pts, Vector3 cand, float minSeparation)
        {
            float sq = minSeparation * minSeparation;
            for (int i = 0; i < pts.Count; i++)
                if ((pts[i] - cand).sqrMagnitude < sq) return true;
            return false;
        }

        /// <summary>Unsigned angle in degrees between two unit vectors. Kept as a forwarder
        /// because SwitchbackCourseTests asserts the turn and presentation caps through it.</summary>
        public static float Angle(Vector3 a, Vector3 b) => RaceCourseGeometry.Angle(a, b);
    }
}
