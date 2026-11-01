using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;
using Role = CosmicShore.Gameplay.LanternRunCourse.ClusterRole;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The LanternRun contract: the shared circuit on the Angler's cut generates at every intensity; each leg carries
    /// the clusters the ladder names (both / both zigzagged / far only / far zigzagged and near every second leg); no
    /// cluster prism sits in a mouth; the near-to-far dash runs THROUGH the ring and lands past it wherever a leg has both
    /// clusters; and the Angler numbers the course is stated against are still the shipped ones. Swept over 400 seeds.
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
        public void Each_leg_carries_the_clusters_the_ladder_names()
        {
            // Legs of eight: both everywhere at I1-I2; far only at I3; far everywhere and near on legs 0, 2, 4, 6 at I4.
            int[] expected = { 0, 16, 16, 8, 12 };
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= 50; seed++)
                {
                    var clusters = LanternRunCourse.Clusters(Circuit(intensity, seed), intensity);
                    Assert.AreEqual(expected[intensity], clusters.Count, $"i{intensity} seed {seed}: cluster count");
                    foreach (var cl in clusters)
                        Assert.GreaterOrEqual(cl.Slots.Length, LanternRunCourse.ClusterPrisms(intensity) * 3 / 4,
                            $"i{intensity} seed {seed}: more than a quarter of a cluster was dropped");
                }
        }

        [Test]
        public void No_cluster_prism_sits_in_a_mouth()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    foreach (var cl in LanternRunCourse.Clusters(c, intensity))
                        foreach (var p in cl.Slots)
                            foreach (var g in c)
                                Assert.GreaterOrEqual((p - g.Position).magnitude, g.Radius + LanternRunCourse.MouthClearance - 0.01f,
                                    $"i{intensity} seed {seed}: a cluster prism in a mouth");
                }
        }

        [Test]
        public void The_near_to_far_dash_runs_through_the_ring_and_lands_past_it()
        {
            // The second dash of a leg goes from the near group to the far one. Its line must cross the ring's plane
            // inside the mouth, and its landing (75% of the way) must already be past the plane: dash, and the ring counts.
            Assert.Less((1f - LanternRunCourse.DashFrac) * (LanternRunCourse.NearBefore + LanternRunCourse.FarPast),
                        LanternRunCourse.FarPast, "the far cluster is too close: the dash lands short of the ring");
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Circuit(intensity, seed);
                    for (int leg = 0; leg < c.Count; leg++)
                    {
                        if (!LanternRunCourse.HasCluster(leg, Role.Near, intensity)) continue;
                        Vector3 near = LanternRunCourse.ClusterCentre(c, leg, Role.Near, intensity);
                        Vector3 far = LanternRunCourse.ClusterCentre(c, leg, Role.Far, intensity);
                        Assert.IsTrue(LanternRunCourse.CrossesPlaneAt(near, far, c[leg], out float miss),
                            $"i{intensity} seed {seed} leg {leg}: the near-to-far line never reaches the ring");
                        Assert.Less(miss, c[leg].Radius, $"i{intensity} seed {seed} leg {leg}: the dash line misses the mouth by {miss - c[leg].Radius:F1} u");
                        Vector3 land = LanternRunCourse.DashLanding(near, far);
                        Assert.Greater(Vector3.Dot(land - c[leg].Position, c[leg].Axis), 0f,
                            $"i{intensity} seed {seed} leg {leg}: the dash lands short of the ring");
                    }
                }
        }

        [Test]
        public void An_on_line_cluster_paints_from_the_ring_line_and_a_zigzagged_one_does_not()
        {
            // From 400 u before a ring on its own line (its axis), aiming through the mouth: I1's far cluster is inside
            // the 3 degree brush; I2's is not - it has to be aimed.
            for (int seed = 1; seed <= Seeds; seed++)
                for (int intensity = 1; intensity <= 2; intensity++)
                {
                    var c = Circuit(intensity, seed);
                    Vector3 arrive = c[0].Axis;
                    Vector3 from = c[0].Position - arrive * 400f;
                    float off = Vector3.Angle(arrive, LanternRunCourse.ClusterCentre(c, 0, Role.Far, intensity) - from);
                    if (intensity == 1) Assert.Less(off, LanternRunCourse.PaintCone, $"seed {seed}: I1 cluster off the brush");
                    else Assert.Greater(off, LanternRunCourse.PaintCone, $"seed {seed}: I2 cluster should need aiming");
                }
        }

        [Test]
        public void The_ladder_tightens_level_by_level()
        {
            for (int i = 2; i <= 4; i++)
            {
                Assert.Less(Settings(i).RingRadius, Settings(i - 1).RingRadius, $"i{i}: mouths must tighten");
                Assert.Less(LanternRunCourse.ClusterPrisms(i), LanternRunCourse.ClusterPrisms(i - 1), $"i{i}: clusters must shrink");
                Assert.Greater(LanternRunCourse.RegrowSeconds(i), LanternRunCourse.RegrowSeconds(i - 1), $"i{i}: regrowth must slow");
            }
        }

        [Test]
        public void A_cluster_fits_inside_the_brush_from_a_throwing_distance()
        {
            // From 300 u (the near cluster's distance) the whole ball lies inside the 3-degree brush around its centre.
            for (int i = 1; i <= 4; i++)
                Assert.Less(LanternRunCourse.ClusterRadius(i), 300f * Mathf.Tan(LanternRunCourse.PaintCone * Mathf.Deg2Rad),
                    $"i{i}: cluster wider than the brush");
        }
    }
}
