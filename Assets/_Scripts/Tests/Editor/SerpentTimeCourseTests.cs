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
    /// The Serpentine course contract (<see cref="SerpentTimeCourse.Build"/>, the switchback ladder): a closed lap of
    /// rings inside the shell, legs long and clear of the nucleus, a hairpin at every ring but the ladder's two ends, no
    /// two rings touching, mouths that tighten level by level, and one answer per seed; plus the vessel numbers the course
    /// is flown with, read back off the shipped assets. Swept over 400 seeds.
    /// </summary>
    public class SerpentTimeCourseTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;
        const int PerLap = SerpentTimeCourse.GatesPerLap;

        static List<RaceGate> Lap(int intensity, int seed) => SerpentTimeCourse.Build(seed, intensity, PerLap, Inner, Outer);

        static float AssetFloat(string path, string key)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            var m = Regex.Match(File.ReadAllText(path), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void Course_constants_match_the_shipped_Serpent()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/Serpent.prefab";
            const string why = " moved - SerpentTimeCourse is cut against it; update the constant and re-run the sweep.";
            Assert.AreEqual(SerpentTimeCourse.ThrottleScaler, AssetFloat(prefab, "DefaultThrottleScaler"), 1e-4f, "DefaultThrottleScaler" + why);
            Assert.AreEqual(SerpentTimeCourse.MinimumSpeed, AssetFloat(prefab, "DefaultMinimumSpeed"), 1e-4f, "DefaultMinimumSpeed" + why);
            Assert.AreEqual(SerpentTimeCourse.RotationThrottleScaler, AssetFloat(prefab, "RotationThrottleScaler"), 1e-4f, "RotationThrottleScaler" + why);
            Assert.AreEqual(SerpentTimeCourse.TurnScaler, Mathf.Min(AssetFloat(prefab, "PitchScaler"), AssetFloat(prefab, "YawScaler")), 1e-4f, "Pitch/YawScaler" + why);
            const string pellets = "Assets/_SO_Assets/VesselActions/Serpent/ConsumeBoostAction.asset";
            Assert.AreEqual(SerpentTimeCourse.PelletMultiplier, AssetFloat(pellets, "boostMultiplier"), 1e-4f, "boostMultiplier" + why);
            Assert.AreEqual(SerpentTimeCourse.PelletBurnSeconds, AssetFloat(pellets, "boostDuration"), 1e-4f, "boostDuration" + why);
            Assert.AreEqual(SerpentTimeCourse.TankPellets, Mathf.RoundToInt(1f / AssetFloat(pellets, "resourceCost")), "pellets in the tank" + why);
        }

        [Test]
        public void Every_lap_is_closed_and_inside_the_shell()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Lap(intensity, seed);
                Assert.AreEqual(PerLap, lap.Count);
                foreach (var g in lap)
                {
                    float r = g.Position.magnitude;
                    Assert.LessOrEqual(r + g.Radius, Outer, $"I{intensity} seed {seed}: a ring past the shell");
                    Assert.GreaterOrEqual(r - g.Radius, Inner, $"I{intensity} seed {seed}: a ring inside the nucleus band");
                    Assert.AreEqual(1f, g.Axis.magnitude, 1e-3f);
                }
            }
        }

        [Test]
        public void Legs_are_long_and_clear_the_nucleus()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var legs = new List<float>();
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var lap = Lap(intensity, seed);
                    for (int i = 0; i < lap.Count; i++)
                    {
                        var a = lap[i].Position;
                        var b = lap[(i + 1) % lap.Count].Position;
                        legs.Add(Vector3.Distance(a, b));
                        Assert.GreaterOrEqual(SerpentTimeCourse.DistanceFromCentre(a, b), Inner * SerpentTimeCourse.NucleusClearance - 0.5f,
                            $"I{intensity} seed {seed}: leg {i + 1} cuts the nucleus");
                    }
                }
                legs.Sort();
                Assert.Greater(legs[legs.Count / 2], 900f, $"I{intensity}: rings far apart (median leg)");
                Assert.Greater(legs[0], 600f, $"I{intensity}: no short leg");
            }
        }

        [Test]
        public void Every_ring_but_the_ladders_ends_is_a_hairpin()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Lap(intensity, seed);
                int hairpins = 0;
                for (int i = 0; i < lap.Count; i++)
                    if (SerpentTimeCourse.TurnDegrees(lap, i) >= 150f) hairpins++;
                Assert.GreaterOrEqual(hairpins, lap.Count - 2, $"I{intensity} seed {seed}: {hairpins} hairpins");
            }
        }

        [Test]
        public void No_two_rings_touch()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Lap(intensity, seed);
                for (int i = 0; i < lap.Count; i++)
                for (int j = i + 1; j < lap.Count; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(lap[i].Position, lap[j].Position),
                        lap[i].Radius + lap[j].Radius + SerpentTimeCourse.RingGap - 0.5f, $"I{intensity} seed {seed}: rings {i + 1} and {j + 1}");
            }
        }

        [Test]
        public void Mouths_shrink_and_hairpins_sharpen_level_by_level()
        {
            for (int i = 2; i <= 4; i++)
            {
                Assert.Less(SerpentTimeCourse.RingRadiusFor(i), SerpentTimeCourse.RingRadiusFor(i - 1), $"I{i}: mouths must tighten");
                Assert.Less(SerpentTimeCourse.LateralStepDegrees(i), SerpentTimeCourse.LateralStepDegrees(i - 1), $"I{i}: hairpins sharpen");
            }
        }

        [Test]
        public void One_seed_one_lap()
        {
            var a = Lap(3, 77);
            var b = Lap(3, 77);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].Position, b[i].Position);
        }
    }
}
