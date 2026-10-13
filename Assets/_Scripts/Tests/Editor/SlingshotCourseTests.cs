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
    /// Slingshot's circuit (<c>Arcade/SLINGSHOT.md</c>): the course constants read back off the
    /// shipped Stoat and the wormhole config, and the shared solver's output over 400 seeds per
    /// intensity - the invariants <c>Tools/Build/slingshot_course_harness</c> runs offline.
    /// </summary>
    public class SlingshotCourseTests
    {
        // The race cell's derived shell - the same cell Headlong, Redline and Grizzly Time race in:
        // nucleus x1.22 -> 480, membrane x0.9 -> 1080.
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        static HeadlongCircuitSettings Settings(int intensity)
        {
            var s = SlingshotCourse.ForIntensity(intensity);
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
        public void Course_constants_match_the_shipped_Stoat_and_the_wormhole_pull()
        {
            const string stoat = "Assets/_Prefabs/Spacevessels/Stoat.prefab";
            Assert.AreEqual(SlingshotCourse.StoatThrottleScaler, AssetFloat(stoat, "DefaultThrottleScaler"), 1e-4f);
            Assert.AreEqual(SlingshotCourse.StoatTurnScaler, AssetFloat(stoat, "YawScaler"), 1e-4f);
            Assert.AreEqual(SlingshotCourse.StoatTurnScaler, AssetFloat(stoat, "PitchScaler"), 1e-4f);
            Assert.AreEqual(0f, AssetFloat(stoat, "RotationThrottleScaler"), 1e-4f, "a flat turn: the circle grows with speed alone");
            Assert.AreEqual(SlingshotCourse.SlingPullCeiling, AssetFloat("Assets/Resources/BlackHoleConfig.asset", "maxVesselPullSpeed"), 1e-4f);
        }

        [Test]
        public void TheSlungCircleIsTheCruisePivotScaledBySpeed()
        {
            Assert.AreEqual(60f / (120f * Mathf.Deg2Rad), SlingshotCourse.CruiseRadius, 0.05f);
            Assert.AreEqual(150f / (120f * Mathf.Deg2Rad), SlingshotCourse.FullSlingRadius, 0.05f);
        }

        [Test]
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void EveryCircuitIsFlyable_ByAStoatThatNeverSlings(int intensity)
        {
            var s = Settings(intensity);
            for (int k = 0; k < Seeds; k++)
            {
                var c = Circuit(intensity, 1000 + k * 7919 + intensity);
                Assert.AreEqual(SlingshotCourse.GatesPerLap, c.Count, $"seed {k}: gate count");
                Assert.Less(Vector3.Angle(c[0].Position, Vector3.up), 0.5f, $"seed {k}: gate 0 sits on the spawn ring's pole");
                for (int g = 0; g < c.Count; g++)
                {
                    float r = c[g].Position.magnitude;
                    Assert.That(r, Is.InRange(Inner - 1f, Outer + 1f), $"seed {k} gate {g}: inside the race shell");
                    float corner = CornerRadius(c, g);
                    Assert.GreaterOrEqual(corner, 2f * SlingshotCourse.CruiseRadius, $"seed {k} gate {g}: a cruising Stoat makes every corner");
                    Assert.GreaterOrEqual(corner, s.CornerFloorRadius * 0.98f, $"seed {k} gate {g}: the safety floor holds");
                }
            }
        }

        [Test]
        public void HigherIntensityAsksForAMoreAimedThrow()
        {
            float MedianTightest(int i)
            {
                var mins = new List<float>();
                for (int k = 0; k < Seeds; k++)
                {
                    var c = Circuit(i, 1000 + k * 7919 + i);
                    mins.Add(Enumerable.Range(0, c.Count).Min(g => CornerRadius(c, g)));
                }
                mins.Sort();
                return mins[mins.Count / 2];
            }
            float l1 = MedianTightest(1), l2 = MedianTightest(2), l4 = MedianTightest(4);
            Assert.Greater(l1, 2f * SlingshotCourse.FullSlingRadius, "level 1: no corner asks the sling to turn you");
            Assert.Less(l2, l1, "level 2 tightens");
            Assert.LessOrEqual(l4, l2, "level 4 is the tightest");
            for (int i = 2; i <= 4; i++)
                Assert.Less(Settings(i).RingRadius, Settings(i - 1).RingRadius, "the mouth closes with intensity");
        }
    }
}
