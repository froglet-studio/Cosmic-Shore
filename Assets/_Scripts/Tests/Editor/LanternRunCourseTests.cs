using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Lantern Run contract: the shared circuit on the Angler's cut generates at every intensity, every lantern
    /// hangs where the ladder says (past its ring, on the arrival line or off it, every 1 / 1 / 2 / 3 rings), a lantern
    /// never sits in a mouth, and the Angler numbers the course is stated against are still the shipped ones. Swept over
    /// 400 seeds.
    /// </summary>
    public class LanternRunCourseTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        static HeadlongCircuitSettings Settings(int intensity)
        {
            var s = LanternRunCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return s;
        }

        static List<RaceGate> Circuit(int intensity, int seed) => HeadlongCircuit.Generate(seed, Settings(intensity));

        static float AssetFloat(string path, string key)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            var m = Regex.Match(File.ReadAllText(path), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void Course_constants_match_the_shipped_Angler()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/Angler.prefab";
            const string config = "Assets/_SO_Assets/VesselActions/Angler/AnglerConfig.asset";
            const string why = " moved - LanternRunCourse is stated against it; update the constant and re-read LANTERNRUN.md.";
            Assert.AreEqual(LanternRunCourse.CruiseSpeed, AssetFloat(prefab, "DefaultThrottleScaler"), 1e-4f, "cruise" + why);
            Assert.AreEqual(LanternRunCourse.TurnScaler, Mathf.Min(AssetFloat(prefab, "PitchScaler"), AssetFloat(prefab, "YawScaler")), 1e-4f, "turn" + why);
            Assert.AreEqual(LanternRunCourse.SlingSpeed, AssetFloat(config, "slingSpeed"), 1e-4f, "slingSpeed" + why);
            Assert.AreEqual(LanternRunCourse.DashFrac, AssetFloat(config, "dashFrac"), 1e-4f, "dashFrac" + why);
            Assert.AreEqual(LanternRunCourse.DashTime, AssetFloat(config, "dashTime"), 1e-4f, "dashTime" + why);
            Assert.AreEqual(LanternRunCourse.PaintCone, AssetFloat(config, "paintCone"), 1e-4f, "paintCone" + why);
            Assert.AreEqual(LanternRunCourse.PaintRange, AssetFloat(config, "paintRange"), 1e-4f, "paintRange" + why);
        }

        [Test]
        public void Every_intensity_yields_a_closed_ring_inside_the_shell_flyable_at_cruise()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                Assert.Greater(s.CornerFloorRadius, 1.5f * LanternRunCourse.CruiseRadius, $"i{intensity}: floor under the cruise circle");
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    Assert.IsNotNull(c, $"i{intensity} seed {seed}: null");
                    Assert.AreEqual(s.GateCount, c.Count, $"i{intensity} seed {seed}: gate count");
                    foreach (var g in c)
                    {
                        Assert.GreaterOrEqual(g.Position.magnitude, Inner - 0.01f, "inside the nucleus");
                        Assert.LessOrEqual(g.Position.magnitude, Outer + 0.01f, "outside the membrane");
                    }
                }
            }
        }

        [Test]
        public void Lanterns_hang_on_the_rings_the_ladder_names()
        {
            int[] every = { 0, 1, 1, 2, 3 };
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                Assert.AreEqual(every[intensity], LanternRunCourse.LanternEvery(intensity), $"i{intensity}: lantern spacing");
                for (int seed = 1; seed <= 50; seed++)
                {
                    var c = Circuit(intensity, seed);
                    int expectedLanterns = 0;
                    for (int i = 0; i < c.Count; i++) if (LanternRunCourse.HasLantern(i, intensity)) expectedLanterns++;
                    var prisms = LanternRunCourse.Lanterns(c, intensity);
                    Assert.LessOrEqual(prisms.Count, expectedLanterns * LanternRunCourse.LanternPrisms, "too many prisms");
                    Assert.GreaterOrEqual(prisms.Count, expectedLanterns * LanternRunCourse.LanternPrisms * 3 / 4,
                        $"i{intensity} seed {seed}: more than a quarter of the lantern prisms were dropped");
                }
            }
        }

        [Test]
        public void No_lantern_prism_sits_in_a_mouth()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    foreach (var p in LanternRunCourse.Lanterns(c, intensity))
                        foreach (var g in c)
                            Assert.GreaterOrEqual((p.Position - g.Position).magnitude, g.Radius + LanternRunCourse.MouthClearance - 0.01f,
                                $"i{intensity} seed {seed}: a lantern prism in a mouth");
                }
        }

        [Test]
        public void An_on_line_lantern_is_painted_through_its_ring_and_an_off_line_one_is_not()
        {
            // I1: from 400 u before the ring on its arrival line, the lantern's centre is inside the 3 degree brush.
            // I2: from the same point, the straight line misses it - the pilot flies the line THROUGH the mouth TO it.
            for (int seed = 1; seed <= Seeds; seed++)
                for (int intensity = 1; intensity <= 2; intensity++)
                {
                    var c = Circuit(intensity, seed);
                    int n = c.Count;
                    Vector3 arrive = (c[0].Position - c[n - 1].Position).normalized;
                    Vector3 from = c[0].Position - arrive * 400f;
                    float off = Vector3.Angle(arrive, LanternRunCourse.LanternCentre(c, 0, intensity) - from);
                    if (intensity == 1) Assert.Less(off, LanternRunCourse.PaintCone, $"seed {seed}: I1 lantern off the brush");
                    else Assert.Greater(off, LanternRunCourse.PaintCone, $"seed {seed}: I2 lantern should need a line");
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
