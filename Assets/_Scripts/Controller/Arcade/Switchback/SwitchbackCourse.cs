using System.Collections.Generic;
using CosmicShore.Data;
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
        public int HairpinEvery;             // every Nth corner is a hairpin (0 = none) - see IsHairpinCorner
        public float HairpinMinDegrees;      // a hairpin turns at least this much...
        public float HairpinMaxDegrees;      // ...and at most this much

        // ── Plan 1 (SwitchbackFunType.Plan1_LevelShapes / Combined) ─────────
        // Every one of these defaults to "off", and off is the shipped behaviour bit for bit: the Baseline ladder
        // never sets them, and SwitchbackCourseTests.BaselineIsUnchanged pins that.
        public int HairpinRun;               // hairpins in a row inside each HairpinEvery block (0 or 1 = one)
        public SwitchbackCornerStyle CornerStyle; // how ordinary corners are drawn (None = the shipped random cone)
        public int NeedleEvery;              // every Nth ordinary corner (counted) is a needle ring (0 = none)
        public float NeedleScale;            // a needle's mouth, as a share of RingRadius (floored at NeedleMinRadius)

        /// <summary>A needle ring never shrinks below five Dolphin hulls (14.3 u): tight, never a precision cliff.</summary>
        public const float NeedleMinRadius = DolphinHullRadius * 5f;

        /// <summary>
        /// The shipped course shape per intensity: ONE NEW DEMAND PER LEVEL (Yash, 2026-10-26,
        /// <c>Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md</c> §3.3). Switchback is where the Dolphin's AI is
        /// trained, so each level teaches one of the Dolphin's verbs and the next adds one:
        /// <list type="number">
        /// <item><b>I1, learn to fly:</b> a flowing arc of wide gates, gentle corners (40 degrees at most), no hairpins.</item>
        /// <item><b>I2, carve:</b> every second corner is a HAIRPIN (90-150 degrees) that only a drift makes - the
        /// gate faces the leg you arrive on, so you thread it straight and carve the turn after it.</item>
        /// <item><b>I3, charge economy:</b> long legs, a hairpin every third corner, and skim walls along legs
        /// (<see cref="SpawnableSwitchbackFeatures"/>): skim to bank charge, discharge down the straight.</item>
        /// <item><b>I4, gauntlet:</b> short legs, a hairpin every second corner (110-150), the tightest mouths, and
        /// danger prisms beside the racing line - the fast line is the risky one.</item>
        /// </list>
        /// Gate COUNT stays constant (the end-game target, authored in one place), so the four levels are
        /// comparable. <c>SwitchbackCourseTests</c> sweeps 400 seeds of each.
        /// </summary>
        /// <summary>
        /// The Dolphin's own circumscribing radius, in world units: 2.86.
        ///
        /// <para>MEASURED from the shipped prefab rather than guessed - the eight corners of all
        /// eleven hull box colliders on <c>Dolphin.prefab</c>, pushed through their transform
        /// chains to the vessel root (root scale 1), giving a hull of 5.29 x 1.23 x 5.30 and a
        /// worst-corner distance from the origin of 2.860 on <c>TopNose</c>.</para>
        /// </summary>
        public const float DolphinHullRadius = 2.86f;

        /// <summary>The widest mouth, at intensity 1. Play-tested; do not derive it.</summary>
        public const float WidestRingRadius = 72f;

        /// <summary>
        /// The mouth radius per intensity: 72, 48, 32, 20. Each level two thirds of the last at most, so there is no
        /// cliff (the old geometric ladder went 72 -> 28 at I2). The tightest is 7 ship radii: room to come through
        /// it in a drift, which a 4.3 u mouth (1.5 ship radii, the old I4) never left - it ended the ladder in
        /// precision instead of mastery.
        /// </summary>
        public static float RingRadiusForIntensity(int intensity) =>
            new[] { WidestRingRadius, 48f, 32f, 20f }[Mathf.Clamp(intensity, 1, 4) - 1];

        public static SwitchbackCourseSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            var s = new SwitchbackCourseSettings
            {
                // Never under the ~360u an AI needs for its approach run. I3's legs are the LONGEST: they are
                // the straights a banked charge is spent down. I4's are the shortest.
                MinStep = new[] { 420f, 400f, 460f, 360f }[i - 1],
                MaxStep = new[] { 620f, 650f, 720f, 520f }[i - 1],
                // The ordinary corners. Hairpins are their own band, below.
                MaxTurnDegrees = new[] { 40f, 45f, 50f, 55f }[i - 1],
                AxisJitterDegrees = new[] { 15f, 25f, 30f, 40f }[i - 1],
                MaxPresentDegrees = new[] { 40f, 50f, 55f, 60f }[i - 1],
                HairpinEvery = new[] { 0, 2, 3, 2 }[i - 1],
                HairpinMinDegrees = new[] { 0f, 90f, 90f, 110f }[i - 1],
                HairpinMaxDegrees = new[] { 0f, 150f, 130f, 150f }[i - 1],
                RingRadius = RingRadiusForIntensity(i),
                // More than two mouths across at every level. Lower where hairpins fold the course back on
                // itself: two 400u legs either side of a 150 degree hairpin put their outer gates 207u apart.
                MinSeparation = new[] { 260f, 200f, 220f, 180f }[i - 1],
            };
            return s;
        }

        /// <summary>
        /// The course shape of a fun type's world. Anything but <see cref="SwitchbackFunTypes.HasLevelShapes"/> is the
        /// shipped ladder (<see cref="ForIntensity(int)"/>) unchanged - Plan 2 and Plan 3 change what is ON the course
        /// and how it is scored, not its shape.
        ///
        /// <para><b>Plan 1, level shapes</b> (SWITCHBACK.md, "Fun types"): each level is a SHAPE you learn.</para>
        /// <list type="number">
        /// <item><b>I1, arcs:</b> ordinary corners all bend the same way, so the course flows in long sweeping arcs.
        /// No hairpins, no needles.</item>
        /// <item><b>I2, S-bends:</b> corners alternate their bend, a hairpin every THIRD corner (90-130), a needle
        /// ring every 4th ordinary corner.</item>
        /// <item><b>I3, long straight into a hairpin:</b> the leg arriving at each hairpin is drawn long (spend a
        /// charge down it, then drift), a hairpin every SECOND corner (100-140), a needle every 3rd ordinary corner.</item>
        /// <item><b>I4, punchy:</b> short legs, corners pushed to the cap, BACK-TO-BACK hairpins (two of every three
        /// corners, 110-150), a needle every 2nd ordinary corner.</item>
        /// </list>
        /// The hairpin share climbs 0, 1/3, 1/2, 2/3; mouths, turn cap and jitter keep the shipped monotonic ladder;
        /// every leg stays over the 360 u AI approach floor.
        /// </summary>
        public static SwitchbackCourseSettings ForIntensity(int intensity, SwitchbackFunType funType)
        {
            if (!SwitchbackFunTypes.HasLevelShapes(funType)) return ForIntensity(intensity);

            int i = Mathf.Clamp(intensity, 1, 4);
            return new SwitchbackCourseSettings
            {
                MinStep = new[] { 420f, 400f, 380f, 360f }[i - 1],
                MaxStep = new[] { 620f, 600f, 700f, 460f }[i - 1],
                MaxTurnDegrees = new[] { 40f, 45f, 50f, 55f }[i - 1],
                AxisJitterDegrees = new[] { 15f, 25f, 30f, 40f }[i - 1],
                MaxPresentDegrees = new[] { 40f, 50f, 55f, 60f }[i - 1],
                HairpinEvery = new[] { 0, 3, 2, 3 }[i - 1],
                HairpinRun = new[] { 0, 1, 1, 2 }[i - 1],
                HairpinMinDegrees = new[] { 0f, 90f, 100f, 110f }[i - 1],
                HairpinMaxDegrees = new[] { 0f, 130f, 140f, 150f }[i - 1],
                RingRadius = RingRadiusForIntensity(i),
                MinSeparation = new[] { 260f, 220f, 200f, 170f }[i - 1],
                CornerStyle = new[]
                {
                    SwitchbackCornerStyle.Arcs, SwitchbackCornerStyle.SBends,
                    SwitchbackCornerStyle.LongIntoHairpin, SwitchbackCornerStyle.Punchy,
                }[i - 1],
                NeedleEvery = new[] { 0, 4, 3, 2 }[i - 1],
                NeedleScale = 0.65f,
            };
        }

        /// <summary>Of every <see cref="HairpinEvery"/> corners, how many are hairpins (0 when there are none).</summary>
        public float HairpinShare => HairpinEvery > 0 ? Mathf.Min(Mathf.Max(1, HairpinRun), HairpinEvery) / (float)HairpinEvery : 0f;
    }

    /// <summary>How a course draws its ordinary (non-hairpin) corners: Plan 1's per-intensity corner pattern.</summary>
    public enum SwitchbackCornerStyle
    {
        None = 0,            // the shipped random cone (Baseline)
        Arcs = 1,            // every corner bends the same way: long flowing arcs
        SBends = 2,          // the bend flips every corner
        LongIntoHairpin = 3, // the leg arriving at a hairpin is long, the others shorter
        Punchy = 4,          // corners pushed toward the turn cap
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
                // The corner is at the gate just placed (index Count - 1). A hairpin is drawn from its own band and
                // never steered back off a wall (that would cap it): a hairpin that leaves the shell is re-drawn.
                bool hairpin = IsHairpinCorner(pts.Count - 1, s);
                Vector3 h = hairpin
                    ? DeflectBetween(ref rng, prevHeading, s.HairpinMinDegrees, s.HairpinMaxDegrees)
                    : s.CornerStyle == SwitchbackCornerStyle.None
                        ? RaceCourseGeometry.Deflect(ref rng, prevHeading, s.MaxTurnDegrees)
                        : StyledCorner(ref rng, headings, s);
                float step = s.CornerStyle == SwitchbackCornerStyle.LongIntoHairpin
                    ? LongIntoHairpinStep(ref rng, pts.Count, s)
                    : rng.Range(s.MinStep, s.MaxStep);
                Vector3 cand = p + h * step;
                float r = cand.magnitude;

                if (hairpin && (r > s.OuterRadius || r < s.InnerRadius)) continue;
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
                else if (IsHairpinCorner(i, s))
                {
                    // A hairpin gate faces the leg you ARRIVE on: thread it straight, then carve the turn after it.
                    // Facing the bisector would stand it near edge-on (75 degrees off a 150 degree hairpin's legs).
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
                float radius = IsNeedleGate(i, s) ? NeedleRadius(s) : s.RingRadius;
                gates.Add(new RaceGate(pts[i], RaceCourseGeometry.Deflect(ref rng, axis, jitter), radius));
            }

            return gates;
        }

        // ── geometry helpers ─────────────────────────────────────────────────
        // Everything except TooClose now lives in RaceCourseGeometry, shared with Headlong.

        /// <summary>
        /// Whether the corner at gate <paramref name="gateIndex"/> is a hairpin: every
        /// <see cref="SwitchbackCourseSettings.HairpinEvery"/>th corner, never the first or last gate (neither is a
        /// corner). Public so the tests and the features layer agree on which corners are hairpins.
        /// </summary>
        /// <para>With <see cref="SwitchbackCourseSettings.HairpinRun"/> above one, the LAST <c>HairpinRun</c> corners
        /// of each block of <c>HairpinEvery</c> are hairpins, back to back. A run of one is the shipped rule exactly
        /// (<c>(i - 1) % n == n - 1</c> is <c>i % n == 0</c>).</para>
        public static bool IsHairpinCorner(int gateIndex, in SwitchbackCourseSettings s)
        {
            if (s.HairpinEvery <= 0 || gateIndex < 1 || gateIndex >= s.GateCount - 1) return false;
            int run = s.HairpinRun > 1 ? Mathf.Min(s.HairpinRun, s.HairpinEvery) : 1;
            return run == 1
                ? gateIndex % s.HairpinEvery == 0
                : (gateIndex - 1) % s.HairpinEvery >= s.HairpinEvery - run;
        }

        /// <summary>
        /// Whether gate <paramref name="gateIndex"/> is a needle ring (Plan 1): every
        /// <see cref="SwitchbackCourseSettings.NeedleEvery"/>th ORDINARY corner, counted from the start - never the
        /// first gate, the last, or a hairpin (a hairpin is hard enough).
        /// </summary>
        public static bool IsNeedleGate(int gateIndex, in SwitchbackCourseSettings s)
        {
            if (s.NeedleEvery <= 0 || gateIndex < 1 || gateIndex >= s.GateCount - 1 || IsHairpinCorner(gateIndex, s))
                return false;
            int ordinal = 0;
            for (int k = 1; k <= gateIndex; k++)
                if (!IsHairpinCorner(k, s)) ordinal++;
            return ordinal % s.NeedleEvery == 0;
        }

        /// <summary>A needle ring's mouth: <c>NeedleScale</c> of the ring, never under <see cref="SwitchbackCourseSettings.NeedleMinRadius"/>
        /// (and never wider than the ring itself).</summary>
        public static float NeedleRadius(in SwitchbackCourseSettings s) =>
            Mathf.Min(s.RingRadius, Mathf.Max(s.RingRadius * (s.NeedleScale > 0f ? s.NeedleScale : 0.65f),
                                              SwitchbackCourseSettings.NeedleMinRadius));

        /// <summary>
        /// An ordinary corner drawn to Plan 1's pattern. The bend is a rotation of the heading about a SPIN axis
        /// perpendicular to it, by an angle inside the turn cap - so the cap holds by construction, as for the
        /// shipped cone. The previous corner's spin (from the two last headings) is what "the same way" and
        /// "the other way" are measured against; a little wobble keeps an arc from closing into a circle.
        /// </summary>
        /// <summary>Share of Arcs / S-bend corners that break the pattern with a random bend.</summary>
        public const float PatternBreakShare = 0.3f;

        static Vector3 StyledCorner(ref RaceCourseGeometry.Rng rng, List<Vector3> headings, in SwitchbackCourseSettings s)
        {
            Vector3 prev = headings[headings.Count - 1];
            float minShare = s.CornerStyle == SwitchbackCornerStyle.Punchy ? 0.7f : 0.45f;
            float angle = rng.Range(minShare, 1f) * s.MaxTurnDegrees * Mathf.Deg2Rad;

            Vector3 spin;
            Vector3 lastSpin = headings.Count >= 2 ? Vector3.Cross(headings[headings.Count - 2], prev) : Vector3.zero;
            // The pattern is a strong TENDENCY, not a rule: one corner in PatternBreakShare breaks it. A rule cannot
            // be backtracked out of - 25 same-way arcs close into a loop that crosses itself in the shell.
            bool patterned = (s.CornerStyle == SwitchbackCornerStyle.Arcs || s.CornerStyle == SwitchbackCornerStyle.SBends)
                             && lastSpin.sqrMagnitude > 1e-6f
                             && rng.Unit() >= PatternBreakShare;
            if (patterned)
            {
                // Project the last spin off the new heading, then wobble it up to 30 degrees about the heading.
                spin = RaceCourseGeometry.SafeNormalize(lastSpin - Vector3.Dot(lastSpin, prev) * prev, RaceCourseGeometry.Perpendicular(prev));
                if (s.CornerStyle == SwitchbackCornerStyle.SBends) spin = -spin;
                spin = RaceCourseGeometry.RotateAbout(spin, prev, rng.Range(-30f, 30f) * Mathf.Deg2Rad);
            }
            else
            {
                Vector3 u = RaceCourseGeometry.Perpendicular(prev);
                Vector3 w = Vector3.Cross(prev, u);
                float phi = rng.Range(0f, 2f * Mathf.PI);
                spin = RaceCourseGeometry.SafeNormalize(u * Mathf.Cos(phi) + w * Mathf.Sin(phi), u);
            }
            return RaceCourseGeometry.SafeNormalize(RaceCourseGeometry.RotateAbout(prev, spin, angle), prev);
        }

        /// <summary>I3's leg lengths: the leg ARRIVING at a hairpin gate (index <paramref name="nextGate"/>) is drawn
        /// from the top 40% of the range - the straight a charge is spent down - and every other leg from the rest.</summary>
        static float LongIntoHairpinStep(ref RaceCourseGeometry.Rng rng, int nextGate, in SwitchbackCourseSettings s)
        {
            float split = Mathf.Lerp(s.MinStep, s.MaxStep, 0.6f);
            return IsHairpinCorner(nextGate, s) ? rng.Range(split, s.MaxStep) : rng.Range(s.MinStep, split);
        }

        /// <summary>Rotate <paramref name="v"/> by an angle between the two bounds about a random perpendicular
        /// axis: a hairpin's heading change.</summary>
        static Vector3 DeflectBetween(ref RaceCourseGeometry.Rng rng, Vector3 v, float minDegrees, float maxDegrees)
        {
            Vector3 u = RaceCourseGeometry.Perpendicular(v);
            Vector3 w = Vector3.Cross(v, u);
            float phi = rng.Range(0f, 2f * Mathf.PI);
            Vector3 spin = RaceCourseGeometry.SafeNormalize(u * Mathf.Cos(phi) + w * Mathf.Sin(phi), u);
            float angle = rng.Range(minDegrees, maxDegrees) * Mathf.Deg2Rad;
            return RaceCourseGeometry.SafeNormalize(RaceCourseGeometry.RotateAbout(v, spin, angle), v);
        }

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
