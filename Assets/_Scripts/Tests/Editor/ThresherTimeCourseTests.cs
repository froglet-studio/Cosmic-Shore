using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Thresher Time circuit contract. The mode's proposition is that a corner is a question -
    /// how much sling is it worth? - and that is only true if the corners are actually cut around
    /// the Thresher's FULL-SLING circle (479 u/s, the ball's ceiling, on a flat 120 deg/s turn:
    /// ~229 u), with the intensity ladder DEMANDING the corners it claims rather than merely
    /// permitting them (HEADLONG.md §1: a floor is a permission, not a demand). Asserted across a
    /// 400-seed sweep of every intensity, because a generated course is exactly the kind of thing
    /// that is fine on the seed you looked at.
    ///
    /// <para>The solver is Headlong's and shared with Redline, Regatta, Grizzly Time, Slingshot and
    /// Warpline, so the liveness half (closed ring, shell, mouth separation, pole placement) is
    /// re-asserted against the Thresher's settings - a change to the shared solver has to keep
    /// every mode's contract - and the ladder half is the Thresher's own. The vessel constants are
    /// read back off the shipped prefab, ThresherConfig.asset (which owns the sling's dials) and
    /// VesselTransformer.cs (the throttle's tracking rate), so a retune of any of them names every
    /// corner that moved with it.</para>
    /// </summary>
    public class ThresherTimeCourseTests
    {
        // The race cell's derived shell - the same cell Headlong, Redline and Grizzly Time race in
        // (see RedlineCourseTests): Nucleus x1.22 -> 480, membrane x0.9 -> 1080.
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        static HeadlongCircuitSettings Settings(int intensity)
        {
            var s = ThresherTimeCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return s;
        }

        static List<RaceGate> Circuit(int intensity, int seed) =>
            HeadlongCircuit.Generate(seed, Settings(intensity));

        static float CornerRadius(List<RaceGate> c, int i)
        {
            int n = c.Count;
            return RaceCourseGeometry.CornerRadius(c[(i - 1 + n) % n].Position, c[i].Position,
                                                   c[(i + 1) % n].Position);
        }

        /// <summary>Corner radii of one lap, tightest first.</summary>
        static float[] SortedCorners(List<RaceGate> c)
        {
            var radii = new float[c.Count];
            for (int i = 0; i < c.Count; i++) radii[i] = CornerRadius(c, i);
            System.Array.Sort(radii);
            return radii;
        }

        static float Median(List<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[sorted.Count / 2];
        }

        // ── The vessel constants the whole ladder is derived from ────────────

        static string ReadText(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            return File.ReadAllText(path);
        }

        static float AssetFloat(string path, string key)
        {
            var m = Regex.Match(ReadText(path), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void Course_constants_match_the_shipped_Thresher_and_its_sling()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/Thresher.prefab";
            const string config = "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset";
            const string transformer = "Assets/_Scripts/Controller/Vessel/VesselTransformer.cs";
            const string why = " moved - ThresherTimeCourse states it as a constant and the whole ladder is cut against it. " +
                               "Update the constant, re-run the sweep and re-read THRESHERTIME.md §3.";

            Assert.AreEqual(ThresherTimeCourse.ThresherThrottleScaler, AssetFloat(prefab, "DefaultThrottleScaler"), 1e-4f, "Thresher DefaultThrottleScaler" + why);
            Assert.AreEqual(ThresherTimeCourse.ThresherMinimumSpeed, AssetFloat(prefab, "DefaultMinimumSpeed"), 1e-4f, "Thresher DefaultMinimumSpeed" + why);
            Assert.AreEqual(ThresherTimeCourse.ThresherRotationThrottleScaler, AssetFloat(prefab, "RotationThrottleScaler"), 1e-4f, "Thresher RotationThrottleScaler" + why);
            Assert.AreEqual(ThresherTimeCourse.ThresherTurnScaler,
                Mathf.Min(AssetFloat(prefab, "PitchScaler"), AssetFloat(prefab, "YawScaler")), 1e-4f, "Thresher Pitch/YawScaler" + why);
            Assert.AreEqual(ThresherTimeCourse.SandboxCruise, AssetFloat(config, "sandboxCruise"), 1e-4f, "ThresherConfig sandboxCruise" + why);
            Assert.AreEqual(ThresherTimeCourse.GameCruise, AssetFloat(config, "gameCruise"), 1e-4f, "ThresherConfig gameCruise" + why);
            Assert.AreEqual(ThresherTimeCourse.MaxBallSandbox, AssetFloat(config, "maxBall"), 1e-4f, "ThresherConfig maxBall" + why);
            Assert.AreEqual(ThresherTimeCourse.LockMax, AssetFloat(config, "lockMax"), 1e-4f, "ThresherConfig lockMax" + why);
            Assert.AreEqual(ThresherTimeCourse.PerfectBoost, AssetFloat(config, "perfectBoost"), 1e-4f, "ThresherConfig perfectBoost" + why);

            var lerp = Regex.Match(ReadText(transformer), @"const float LERP_AMOUNT = ([0-9.]+)f;");
            Assert.IsTrue(lerp.Success, $"LERP_AMOUNT not found in {transformer}");
            Assert.AreEqual(ThresherTimeCourse.ThrottleTrackingRate,
                float.Parse(lerp.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-4f,
                "VesselTransformer.LERP_AMOUNT (the rate a sling bleeds to cruise)" + why);
        }

        [Test]
        public void Sling_ceiling_and_circles_match_the_shipped_Thresher()
        {
            Assert.AreEqual(125f, ThresherTimeCourse.CruiseSpeed, 0.01f, "15 floor + 110 throttle: cruise at full throttle");
            Assert.AreEqual(478.9f, ThresherTimeCourse.TopSpeed, 0.1f,
                "the ball's 2600 sandbox ceiling x 70/380. If it moved, so did every corner.");
            Assert.Greater(ThresherTimeCourse.TopSpeed, ThresherTimeCourse.OrbitSpinCap,
                "the ceiling must sit above the spin-up's own cap, or the wind-in buys nothing");
            Assert.AreEqual(154f, ThresherTimeCourse.OrbitSpinCap, 0.01f, "lockMax 2.2 x gameCruise 70");
            Assert.AreEqual(766.3f, ThresherTimeCourse.PerfectSlingSpeed, 0.5f, "the ceiling with Time L5's Perfect Release on it");

            // 479 u/s over 120 deg/s: a 229 u circle at the ceiling; 125 u/s over 120: 60 u cruising.
            Assert.AreEqual(228.7f, ThresherTimeCourse.FullSlingRadius, 0.5f, "full-sling radius changed - re-derive the ladder");
            Assert.AreEqual(59.7f, ThresherTimeCourse.CruiseRadius, 0.5f, "cruise radius changed - re-derive the safety floors");
        }

        [Test]
        public void Corner_radius_is_monotone_in_sling()
        {
            float previous = 0f;
            for (int step = 0; step <= 20; step++)
            {
                float radius = ThresherTimeCourse.CornerRadiusAtSling(step / 20f);
                Assert.GreaterOrEqual(radius, previous, $"radius must not shrink with sling (step {step})");
                previous = radius;
            }

            Assert.AreEqual(1f, ThresherTimeCourse.FastestSlingForCorner(ThresherTimeCourse.FullSlingRadius + 1f), 1e-4f);
            Assert.AreEqual(0f, ThresherTimeCourse.FastestSlingForCorner(ThresherTimeCourse.CruiseRadius - 1f), 1e-4f);
            Assert.AreEqual(0.5f, ThresherTimeCourse.FastestSlingForCorner(ThresherTimeCourse.CornerRadiusAtSling(0.5f)), 1e-3f,
                "the inverse must recover the sling the forward curve was evaluated at");
        }

        [Test]
        public void A_ceiling_sling_bleeds_over_one_to_two_full_sling_circles()
        {
            // The distance a release carries while it bleeds is what makes WHERE you let go a
            // DECISION: under one full-sling circle and the sling would be spent before it mattered;
            // over two and a release on one leg would still be running through the corner after it.
            float top = ThresherTimeCourse.TopSpeed;
            Assert.AreEqual(top, ThresherTimeCourse.LaunchSpeedAfter(top, 0f), 1e-3f, "a sling leaves at its release speed");
            float leftAfterTwo = (ThresherTimeCourse.LaunchSpeedAfter(top, 2f) - ThresherTimeCourse.CruiseSpeed)
                                 / (top - ThresherTimeCourse.CruiseSpeed);
            Assert.Less(leftAfterTwo, 0.1f, "two seconds after a ceiling sling, under a tenth of its excess should be left");

            float bleed = ThresherTimeCourse.BleedDistance(top, 0.9f);
            Assert.Greater(bleed, ThresherTimeCourse.FullSlingRadius, "a ceiling sling should carry the hull a real distance");
            Assert.Less(bleed, 2f * ThresherTimeCourse.FullSlingRadius, "a ceiling sling should not still be running two circles on");
            Assert.Greater(bleed, ThresherTimeCourse.BleedDistance(ThresherTimeCourse.OrbitSpinCap, 0.9f),
                "a wound-in sling must carry further than one released at the spin-up's own cap");
        }

        // ── The circuit is a circuit ─────────────────────────────────────────

        [Test]
        public void Every_intensity_yields_a_closed_ring_of_gates()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    Assert.IsNotNull(c, $"i{intensity} seed {seed}: generator returned null");
                    Assert.AreEqual(s.GateCount, c.Count, $"i{intensity} seed {seed}: gate count");
                    foreach (var g in c)
                    {
                        Assert.AreEqual(1f, g.Axis.magnitude, 1e-3f, "gate axis must be unit");
                        Assert.AreEqual(s.RingRadius, g.Radius, 1e-3f, "mouth radius");
                    }
                }
            }
        }

        [Test]
        public void Gate_zero_sits_on_the_spawn_pole()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                    Assert.Less(RaceCourseGeometry.Angle(Circuit(intensity, seed)[0].Position, Vector3.up),
                        0.1f, $"i{intensity} seed {seed}: gate 0 is off the spawn pole");
        }

        [Test]
        public void Every_gate_stays_inside_the_cell_shell()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                    foreach (var g in Circuit(intensity, seed))
                    {
                        float r = g.Position.magnitude;
                        Assert.GreaterOrEqual(r, Inner - 0.01f, $"i{intensity} seed {seed}: inside the nucleus");
                        Assert.LessOrEqual(r, Outer + 0.01f, $"i{intensity} seed {seed}: outside the membrane");
                    }
        }

        [Test]
        public void No_two_mouths_are_within_a_ring_diameter()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                float min = Settings(intensity).RingRadius * 2f;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    for (int i = 0; i < c.Count; i++)
                        for (int j = i + 1; j < c.Count; j++)
                            Assert.GreaterOrEqual((c[i].Position - c[j].Position).magnitude, min - 0.01f,
                                $"i{intensity} seed {seed}: gates {i} and {j} overlap");
                }
            }
        }

        [Test]
        public void Every_corner_fits_its_absolute_safety_floor_and_is_flyable_at_cruise()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                Assert.Greater(s.CornerFloorRadius, 0f, "Thresher Time states its floor in absolute units");
                // An AI Thresher never slings (AIPilot writes no trigger), so it races at cruise:
                // every corner must be one its 60 u cruise circle holds with room to spare.
                Assert.Greater(s.CornerFloorRadius, 1.5f * ThresherTimeCourse.CruiseRadius,
                    $"i{intensity}: the floor must sit well above the Thresher's cruise circle - a pilot who never slings has to hold every corner");
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    for (int i = 0; i < c.Count; i++)
                        Assert.GreaterOrEqual(CornerRadius(c, i), s.CornerFloorRadius - 0.01f,
                            $"i{intensity} seed {seed}: corner {i} is sharper than the floor");
                }
            }
        }

        [Test]
        public void Presentation_cap_covers_half_the_hardest_turn()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                float worstHalfTurn = 0f;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    int n = c.Count;
                    for (int i = 0; i < n; i++)
                        worstHalfTurn = Mathf.Max(worstHalfTurn, 0.5f * RaceCourseGeometry.Angle(
                            c[i].Position - c[(i - 1 + n) % n].Position, c[(i + 1) % n].Position - c[i].Position));
                }
                Assert.Greater(s.MaxPresentDegrees, worstHalfTurn,
                    $"i{intensity}: MaxPresentDegrees {s.MaxPresentDegrees} does not cover the measured worst half-turn {worstHalfTurn:F1}");
            }
        }

        [Test]
        public void Course_is_deterministic_per_seed()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var a = Circuit(intensity, 12345);
                var b = Circuit(intensity, 12345);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(0f, (a[i].Position - b[i].Position).magnitude, 1e-5f, "positions");
                    Assert.AreEqual(0f, (a[i].Axis - b[i].Axis).magnitude, 1e-5f, "axes");
                }
            }
        }

        // ── The ladder, which IS the mode ────────────────────────────────────

        /// <summary>How many corners of a median lap cost sling - sit inside the full-sling
        /// circle. 0 / 1 / 2 / 3 at levels 1-4.</summary>
        [Test]
        public void Every_intensity_demands_the_corners_it_claims()
        {
            float full = ThresherTimeCourse.FullSlingRadius;
            int[] expected = { 0, 0, 1, 2, 3 };   // index = intensity
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var counts = new List<int>();
                for (int seed = 1; seed <= Seeds; seed++)
                    counts.Add(SortedCorners(Circuit(intensity, seed)).Count(r => r < full));

                var sorted = counts.OrderBy(c => c).ToList();
                Assert.AreEqual(expected[intensity], sorted[sorted.Count / 2],
                    $"i{intensity}: a median lap must have exactly {expected[intensity]} corner(s) that cost sling");
            }
        }

        [Test]
        public void The_hardest_corner_of_each_level_costs_more_speed_than_the_last()
        {
            // Speed, not radius: the ladder is stated in what a corner COSTS, via the Thresher's
            // own curve (measured 100% / 71% / 59% / 44% of top speed). Each level's hardest
            // corner must give up at least 8 more points than the level below.
            var costs = new float[5];
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var tightest = new List<float>();
                for (int seed = 1; seed <= Seeds; seed++)
                    tightest.Add(SortedCorners(Circuit(intensity, seed))[0]);
                costs[intensity] = ThresherTimeCourse.FastestSpeedForCorner(Median(tightest)) / ThresherTimeCourse.TopSpeed;
            }

            Assert.AreEqual(1f, costs[1], 1e-3f, "i1's hardest corner must hold a full sling");
            for (int intensity = 2; intensity <= 4; intensity++)
                Assert.LessOrEqual(costs[intensity], costs[intensity - 1] - 0.08f,
                    $"i{intensity}'s hardest corner must cost at least 8 more points of top speed than i{intensity - 1}'s");
            Assert.Less(costs[4], 0.5f, "i4 must produce a real hairpin - under half of top speed");
        }

        [Test]
        public void Legs_are_long_enough_to_spend_a_sling_on()
        {
            // A leg is where a sling is spent: release on the line, then the launch bleeds back to
            // cruise. A leg shorter than a ceiling sling's 90% bleed (~404 u) is one where every
            // release is still running when the next corner arrives, so the corner - not the pilot
            // - decides how much sling was worth taking. The shortest leg of a median lap must hold
            // that bleed at every level.
            float need = ThresherTimeCourse.BleedDistance(ThresherTimeCourse.TopSpeed, 0.9f);
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var shortest = new List<float>();
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    float lo = float.MaxValue;
                    for (int i = 0; i < c.Count; i++)
                        lo = Mathf.Min(lo, (c[(i + 1) % c.Count].Position - c[i].Position).magnitude);
                    shortest.Add(lo);
                }
                Assert.GreaterOrEqual(Median(shortest), need,
                    $"i{intensity}: the shortest leg of a median lap must hold a ceiling sling's 90% bleed ({need:F0} u)");
            }
        }
    }
}
