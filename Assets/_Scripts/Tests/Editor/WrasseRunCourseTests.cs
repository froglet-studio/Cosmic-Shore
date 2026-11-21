using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Wrasse Run circuit contract (<c>Assets/_Scripts/Controller/Arcade/WRASSERUN.md</c>): the corners are cut against
    /// the Grouper's boosted circle with floors every crew can fly at cruise, the mouths are a kite's, and the hull
    /// constants are read back off Wrasse.prefab so a retune names what moved. The liveness half (a closed ring, inside
    /// the shell) is re-asserted against the shared Headlong solver over a seed sweep.
    /// </summary>
    public class WrasseRunCourseTests
    {
        const float Inner = 480f;   // the barren race cell's derived shell (RedlineCourseTests, ThresherTimeCourseTests)
        const float Outer = 1080f;
        const int Seeds = 200;

        static HeadlongCircuitSettings Settings(int intensity)
        {
            var s = WrasseRunCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return s;
        }

        static float AssetFloat(string path, string key)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            var m = Regex.Match(File.ReadAllText(path), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void Course_constants_match_the_shipped_Wrasse()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/Wrasse.prefab";
            const string config = "Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset";
            const string why = " moved - WrasseRunCourse states it as a constant and the corners are cut against it. " +
                               "Update the constant, re-run the sweep and re-read WRASSERUN.md.";
            Assert.AreEqual(WrasseRunCourse.WrasseThrottleScaler, AssetFloat(prefab, "ThrottleScaler"), 1e-4f, "Wrasse ThrottleScaler" + why);
            Assert.AreEqual(WrasseRunCourse.WrasseMinimumSpeed, AssetFloat(prefab, "MinimumSpeed"), 1e-4f, "Wrasse MinimumSpeed" + why);
            Assert.AreEqual(WrasseRunCourse.WrasseRotationThrottleScaler, AssetFloat(prefab, "RotationThrottleScaler"), 1e-4f, "Wrasse RotationThrottleScaler" + why);
            Assert.AreEqual(WrasseRunCourse.WrasseTurnScaler,
                Mathf.Min(AssetFloat(prefab, "PitchScaler"), AssetFloat(prefab, "YawScaler")), 1e-4f, "Wrasse Pitch/YawScaler" + why);
            Assert.AreEqual(WrasseRunCourse.WrasseBoostMultiplier, AssetFloat(prefab, "boostMultiplier"), 1e-4f, "Wrasse boostMultiplier" + why);
            Assert.AreEqual(WrasseRunCourse.BoardRadius, AssetFloat(config, "boardRadius"), 1e-4f, "KiteNavigatorConfig boardRadius" + why);
        }

        [Test]
        public void Speeds_and_circles_match_the_shipped_Wrasse()
        {
            Assert.AreEqual(60f, WrasseRunCourse.CruiseSpeed, 1e-4f, "10 floor + 50 throttle");
            Assert.AreEqual(260f, WrasseRunCourse.BoostSpeed, 1e-4f, "10 floor + 50 throttle x boost 5");
            Assert.AreEqual(32.4f, WrasseRunCourse.CruiseRadius, 0.5f);
            Assert.AreEqual(118.2f, WrasseRunCourse.BoostRadius, 0.5f);
        }

        [Test]
        public void Every_corner_floor_is_flyable_at_cruise([Values(1, 2, 3, 4)] int intensity)
        {
            var s = WrasseRunCourse.ForIntensity(intensity);
            Assert.GreaterOrEqual(s.CornerFloorRadius, 1.6f * WrasseRunCourse.CruiseRadius,
                "a crew that never boosts (every AI hull) must hold every corner at full throttle");
            Assert.LessOrEqual(s.CornerFloorRadius, WrasseRunCourse.BoostRadius, "a floor above the boosted circle asks nothing of a boost");
        }

        [Test]
        public void Mouths_are_a_kites_and_shrink_up_the_ladder()
        {
            float previous = float.MaxValue;
            for (int i = 1; i <= 4; i++)
            {
                var s = WrasseRunCourse.ForIntensity(i);
                Assert.Less(s.RingRadius, previous, $"level {i}: the mouth must shrink up the ladder");
                Assert.Greater(s.RingRadius, 3f * 8f, $"level {i}: a mouth must take the kite's 8 u head with room to swing");
                Assert.Less(s.LateralPerturbation + s.RingRadius, WrasseRunCourse.BoardRadius * 1.5f,
                    $"level {i}: a ring lifted this far out of the circuit's plane cannot be reached by rolling the board");
                previous = s.RingRadius;
            }
        }

        [Test]
        public void Every_intensity_yields_a_closed_ring_of_gates_inside_the_shell([Values(1, 2, 3, 4)] int intensity)
        {
            for (int seed = 0; seed < Seeds; seed++)
            {
                List<RaceGate> c = HeadlongCircuit.Generate(seed, Settings(intensity));
                Assert.AreEqual(WrasseRunCourse.GatesPerLap, c.Count, $"seed {seed}: wrong gate count");
                foreach (var g in c)
                {
                    float r = g.Position.magnitude;
                    Assert.GreaterOrEqual(r, Inner - 0.01f, $"seed {seed}: a gate inside the nucleus");
                    Assert.LessOrEqual(r, Outer + 0.01f, $"seed {seed}: a gate outside the membrane");
                }
            }
        }
    }
}
