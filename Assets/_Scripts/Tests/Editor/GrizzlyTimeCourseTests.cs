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
    /// The Grizzly Time circuit contract. The mode's proposition is that a corner is a question -
    /// how much launch is it worth? - and that is only true if the corners are actually cut around
    /// the Grizzly's FULL-LAUNCH circle (350 u/s on a 95 deg/s turn: ~211 u), with the intensity
    /// ladder DEMANDING the corners it claims rather than merely permitting them (HEADLONG.md §1:
    /// a floor is a permission, not a demand). Asserted across a 400-seed sweep of every
    /// intensity, because a generated course is exactly the kind of thing that is fine on the
    /// seed you looked at.
    ///
    /// <para>The solver is Headlong's and shared with Redline and Regatta, so the liveness half
    /// (closed ring, shell, mouth separation, pole placement) is re-asserted against the
    /// Grizzly's settings - a change to the shared solver has to keep every mode's contract - and
    /// the ladder half is the Grizzly's own. The vessel constants are read back off the shipped
    /// prefab, the trigger-bomb config (which owns the self-launch since 2026-10-08) and the blast
    /// prefab, so a retune of any of them names every corner that moved with it.</para>
    /// </summary>
    public class GrizzlyTimeCourseTests
    {
        // The race cell's derived shell - the same cell Headlong and Redline race in (see
        // RedlineCourseTests): Nucleus x1.22 -> 480, membrane x0.9 -> 1080.
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        static HeadlongCircuitSettings Settings(int intensity)
        {
            var s = GrizzlyTimeCourse.ForIntensity(intensity);
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

        static float AssetFloat(string path, string key)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            var m = Regex.Match(File.ReadAllText(path), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void Course_constants_match_the_shipped_Grizzly_and_its_launch()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/Grizzly.prefab";
            const string bombs = "Assets/_SO_Assets/VesselActions/Grizzly/GrizzlyTriggerBombConfig.asset";
            const string blast = "Assets/_Prefabs/Projectile/AOEGrizzlyExplosion.prefab";
            const string why = " moved - GrizzlyTimeCourse states it as a constant and the whole ladder is cut against it. " +
                               "Update the constant, re-run the sweep and re-read GRIZZLYTIME.md §4.";

            Assert.AreEqual(GrizzlyTimeCourse.GrizzlyThrottleScaler, AssetFloat(prefab, "DefaultThrottleScaler"), 1e-4f, "Grizzly DefaultThrottleScaler" + why);
            Assert.AreEqual(GrizzlyTimeCourse.GrizzlyRotationThrottleScaler, AssetFloat(prefab, "RotationThrottleScaler"), 1e-4f, "Grizzly RotationThrottleScaler" + why);
            Assert.AreEqual(GrizzlyTimeCourse.GrizzlyTurnScaler,
                Mathf.Min(AssetFloat(prefab, "PitchScaler"), AssetFloat(prefab, "YawScaler")), 1e-4f, "Grizzly Pitch/YawScaler" + why);
            Assert.AreEqual(GrizzlyTimeCourse.MinBlastScale, AssetFloat(bombs, "minBlastScale"), 1e-4f, "trigger bomb minBlastScale" + why);
            Assert.AreEqual(GrizzlyTimeCourse.MaxBlastScale, AssetFloat(bombs, "maxBlastScale"), 1e-4f, "trigger bomb maxBlastScale" + why);
            Assert.AreEqual(GrizzlyTimeCourse.SelfLaunchMultiplier, AssetFloat(bombs, "selfLaunchMultiplier"), 1e-4f, "trigger bomb selfLaunchMultiplier" + why);
            Assert.AreEqual(GrizzlyTimeCourse.VelocityModifierCeiling, AssetFloat(bombs, "selfLaunchCeiling"), 1e-4f, "trigger bomb selfLaunchCeiling" + why);
            Assert.AreEqual(GrizzlyTimeCourse.ImpulseDuration, AssetFloat(bombs, "selfLaunchSeconds"), 1e-4f, "trigger bomb selfLaunchSeconds" + why);
            Assert.AreEqual(GrizzlyTimeCourse.ExplosionDuration, AssetFloat(blast, "ExplosionDuration"), 1e-4f, "AOEGrizzlyExplosion ExplosionDuration" + why);
        }

        [Test]
        public void Full_launch_radius_matches_the_shipped_Grizzly()
        {
            Assert.AreEqual(350f, GrizzlyTimeCourse.TopSpeed, 0.1f,
                "50 cruise + the 300 u/s bomb-launch ceiling. If either moved, so did every corner.");
            Assert.GreaterOrEqual(GrizzlyTimeCourse.LaunchImpulse(1f) * GrizzlyTimeCourse.ImpulseWeight(0f),
                GrizzlyTimeCourse.VelocityModifierCeiling,
                "a full squeeze's launch must reach the ceiling, or top speed is the bomb's and not the ceiling's");

            // 350 u/s over 95 deg/s: a 211u circle launching flat out; 50 u/s over 95: 30u coasting.
            Assert.AreEqual(211.1f, GrizzlyTimeCourse.FullLaunchRadius, 0.5f, "full-launch radius changed - re-derive the ladder");
            Assert.AreEqual(30.2f, GrizzlyTimeCourse.CruiseRadius, 0.5f, "cruise radius changed - re-derive the safety floors");
        }

        [Test]
        public void Corner_radius_is_monotone_in_launch()
        {
            float previous = 0f;
            for (int step = 0; step <= 20; step++)
            {
                float radius = GrizzlyTimeCourse.CornerRadiusAtLaunch(step / 20f);
                Assert.GreaterOrEqual(radius, previous, $"radius must not shrink with launch (step {step})");
                previous = radius;
            }

            Assert.AreEqual(1f, GrizzlyTimeCourse.FastestLaunchForCorner(GrizzlyTimeCourse.FullLaunchRadius + 1f), 1e-4f);
            Assert.AreEqual(0f, GrizzlyTimeCourse.FastestLaunchForCorner(GrizzlyTimeCourse.CruiseRadius - 1f), 1e-4f);
            Assert.AreEqual(0.5f, GrizzlyTimeCourse.FastestLaunchForCorner(GrizzlyTimeCourse.CornerRadiusAtLaunch(0.5f)), 1e-3f,
                "the inverse must recover the launch the forward curve was evaluated at");
        }

        [Test]
        public void A_full_launch_carries_about_one_full_launch_circle()
        {
            // The distance one launch carries along the heading it was aimed down is what makes
            // aiming a launch down the leg a DECISION: under half a full-launch circle and the
            // launch would be too short to matter; over two and a launch taken before a corner
            // would throw the hull past the next mouth.
            float carry = GrizzlyTimeCourse.CarryAfterLaunch(1f);
            Assert.Greater(carry, GrizzlyTimeCourse.FullLaunchRadius * 0.5f, "a full launch should carry the hull a real distance");
            Assert.Less(carry, GrizzlyTimeCourse.FullLaunchRadius * 2f, "a full launch should not carry the hull two circles wide");
            Assert.Greater(carry, GrizzlyTimeCourse.CarryAfterLaunch(0f), "a full squeeze must carry further than a tap");
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
        public void Every_corner_fits_its_absolute_safety_floor()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                Assert.Greater(s.CornerFloorRadius, 0f, "Grizzly Time states its floor in absolute units");
                Assert.Greater(s.CornerFloorRadius, GrizzlyTimeCourse.CruiseRadius,
                    $"i{intensity}: the floor must never ask for a corner tighter than the Grizzly's cruise pivot");
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

        /// <summary>How many corners of a median lap cost launch - sit inside the full-launch
        /// circle. 0 / 1 / 2 / 3 at levels 1-4.</summary>
        [Test]
        public void Every_intensity_demands_the_corners_it_claims()
        {
            float full = GrizzlyTimeCourse.FullLaunchRadius;
            int[] expected = { 0, 0, 1, 2, 3 };   // index = intensity
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var counts = new List<int>();
                for (int seed = 1; seed <= Seeds; seed++)
                    counts.Add(SortedCorners(Circuit(intensity, seed)).Count(r => r < full));

                var sorted = counts.OrderBy(c => c).ToList();
                Assert.AreEqual(expected[intensity], sorted[sorted.Count / 2],
                    $"i{intensity}: a median lap must have exactly {expected[intensity]} corner(s) that cost launch");
            }
        }

        [Test]
        public void The_hardest_corner_of_each_level_costs_more_speed_than_the_last()
        {
            // Speed, not radius: the ladder is stated in what a corner COSTS, via the Grizzly's
            // own curve (measured 100% / 76% / 64% / 47% of top speed). Each level's hardest
            // corner must give up at least 8 more points than the level below.
            var costs = new float[5];
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var tightest = new List<float>();
                for (int seed = 1; seed <= Seeds; seed++)
                    tightest.Add(SortedCorners(Circuit(intensity, seed))[0]);
                costs[intensity] = GrizzlyTimeCourse.FastestSpeedForCorner(Median(tightest)) / GrizzlyTimeCourse.TopSpeed;
            }

            Assert.AreEqual(1f, costs[1], 1e-3f, "i1's hardest corner must hold full launch");
            for (int intensity = 2; intensity <= 4; intensity++)
                Assert.LessOrEqual(costs[intensity], costs[intensity - 1] - 0.08f,
                    $"i{intensity}'s hardest corner must cost at least 8 more points of top speed than i{intensity - 1}'s");
            Assert.Less(costs[4], 0.5f, "i4 must produce a real hairpin - under half of top speed");
        }

        [Test]
        public void Legs_are_long_enough_to_launch_down()
        {
            // A leg is where a launch is taken: fire, freeze a few lengths ahead, ride it - and
            // the launch then carries ~one and a half full-launch circles down the leg (300 u
            // since the launch was tripled, 2026-10-08). A leg that cannot hold one launch's
            // carry with half a carry left to turn in is one the pilot can never launch down
            // without throwing the hull past the next mouth, and the race stops being a
            // bomb-jump. The shortest leg of a median lap must hold a carry and a half, and a
            // second and a quarter at top speed, at every level. (It was two carries when a
            // launch carried 100 u; at 300 u that asks for 600 u legs, which the 480-1080 shell
            // cannot lay eight of round a lap with a hairpin in it.)
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
                Assert.GreaterOrEqual(Median(shortest), 1.5f * GrizzlyTimeCourse.CarryAfterLaunch(1f),
                    $"i{intensity}: the shortest leg of a median lap must hold a full launch's carry and half again");
                Assert.GreaterOrEqual(Median(shortest), GrizzlyTimeCourse.TopSpeed * 1.25f,
                    $"i{intensity}: the shortest leg of a median lap must be 1.25 s at top speed");
            }
        }
    }
}
