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
    /// The BrittleStarTime circuit contract: Headlong's shared solver on <see cref="BrittleStarTimeCourse"/>'s cut. The liveness
    /// half (closed ring, shell, pole, mouth separation, presentation) is re-asserted because the
    /// solver is shared, the ladder half is this hull's, and the vessel numbers the cut is stated
    /// against are read back off the shipped assets. Swept over 400 seeds.
    /// </summary>
    public class BrittleStarTimeCourseTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        static HeadlongCircuitSettings Settings(int intensity)
        {
            var s = BrittleStarTimeCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return s;
        }

        static List<RaceGate> Circuit(int intensity, int seed) => HeadlongCircuit.Generate(seed, Settings(intensity));

        static float CornerRadius(List<RaceGate> c, int i)
        {
            int n = c.Count;
            return RaceCourseGeometry.CornerRadius(c[(i - 1 + n) % n].Position, c[i].Position, c[(i + 1) % n].Position);
        }

        static float AssetFloat(string path, string key)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            var m = Regex.Match(File.ReadAllText(path), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void Course_constants_match_the_shipped_BrittleStar()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/BrittleStar.prefab";
            const string why = " moved - BrittleStarTimeCourse is cut against it; update the constant and re-run the sweep.";
            Assert.AreEqual(BrittleStarTimeCourse.ThrottleScaler, AssetFloat(prefab, "DefaultThrottleScaler"), 1e-4f, "DefaultThrottleScaler" + why);
            Assert.AreEqual(BrittleStarTimeCourse.MinimumSpeed, AssetFloat(prefab, "DefaultMinimumSpeed"), 1e-4f, "DefaultMinimumSpeed" + why);
            Assert.AreEqual(BrittleStarTimeCourse.RotationThrottleScaler, AssetFloat(prefab, "RotationThrottleScaler"), 1e-4f, "RotationThrottleScaler" + why);
            Assert.AreEqual(BrittleStarTimeCourse.TurnScaler, Mathf.Min(AssetFloat(prefab, "PitchScaler"), AssetFloat(prefab, "YawScaler")), 1e-4f, "Pitch/YawScaler" + why);
            const string config = "Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset";
            var m = Regex.Match(File.ReadAllText(config), @"speedModeMultiplier:\s*\n\s+Enabled: \d\s*\n\s+Value: ([0-9.]+)");
            Assert.IsTrue(m.Success, "speedModeMultiplier.Value not found");
            Assert.AreEqual(BrittleStarTimeCourse.SpeedModeMultiplier, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-4f, "speedModeMultiplier" + why);
        }

        [Test]
        public void Every_intensity_yields_a_closed_ring_of_gates_inside_the_shell()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    Assert.IsNotNull(c, $"i{intensity} seed {seed}: generator returned null");
                    Assert.AreEqual(s.GateCount, c.Count, $"i{intensity} seed {seed}: gate count");
                    Assert.Less(RaceCourseGeometry.Angle(c[0].Position, Vector3.up), 0.1f, "gate 0 is off the spawn pole");
                    for (int i = 0; i < c.Count; i++)
                    {
                        float r = c[i].Position.magnitude;
                        Assert.GreaterOrEqual(r, Inner - 0.01f, "inside the nucleus");
                        Assert.LessOrEqual(r, Outer + 0.01f, "outside the membrane");
                        for (int j = i + 1; j < c.Count; j++)
                            Assert.GreaterOrEqual((c[i].Position - c[j].Position).magnitude, 2f * s.RingRadius - 0.01f,
                                $"i{intensity} seed {seed}: gates {i} and {j} overlap");
                    }
                }
            }
        }

        [Test]
        public void Every_corner_fits_its_floor_and_is_flyable_at_cruise()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                Assert.Greater(s.CornerFloorRadius, 1.5f * BrittleStarTimeCourse.CruiseRadius,
                    $"i{intensity}: the floor must sit well above the cruise circle, so a pilot who never boosts holds every corner");
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
                float worst = 0f;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    int n = c.Count;
                    for (int i = 0; i < n; i++)
                        worst = Mathf.Max(worst, 0.5f * RaceCourseGeometry.Angle(
                            c[i].Position - c[(i - 1 + n) % n].Position, c[(i + 1) % n].Position - c[i].Position));
                }
                Assert.Greater(Settings(intensity).MaxPresentDegrees, worst, $"i{intensity}: worst half-turn {worst:F1}");
            }
        }

        [Test]
        public void Every_intensity_demands_the_corners_it_claims()
        {
            float reference = BrittleStarTimeCourse.SpeedModeRadius;
            int[] expected = { 0, 0, 1, 2, 3 };   // index = intensity
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var counts = new List<int>();
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    counts.Add(Enumerable.Range(0, c.Count).Count(i => CornerRadius(c, i) < reference));
                }
                counts.Sort();
                Assert.AreEqual(expected[intensity], counts[counts.Count / 2],
                    $"i{intensity}: a median lap must have {expected[intensity]} corner(s) tighter than {reference:F0} u");
            }
        }

        [Test]
        public void Mouths_shrink_level_by_level()
        {
            for (int i = 2; i <= 4; i++)
                Assert.Less(Settings(i).RingRadius, Settings(i - 1).RingRadius, $"i{i}: mouths must tighten");
        }
    }
}
