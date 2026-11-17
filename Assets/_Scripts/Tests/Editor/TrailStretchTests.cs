using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The fleet's trail: every hull lays by distance, so the mass it lays per second is the same at every frame rate,
    /// and a hull stretches its prisms only past the Manta's speed limits (600 u/s to start, complete at 1100 u/s), so
    /// a hull that never flies that fast lays exactly its authored prism at its authored wavelength (Caleb,
    /// 2026-11-17; <c>Docs/Manta/POLISH.md</c>, round "trail lay by distance, fleet-wide").
    /// </summary>
    public class TrailStretchTests
    {
        const string ControllerPath = "Assets/_Scripts/Controller/Vessel/VesselPrismController.cs";
        const string VesselPrefabs = "Assets/_Prefabs/Spacevessels";
        const float Tol = 1e-3f;

        // The elemental loop's hulls: lay spacing (initialWavelength, x2 for the Sparrow's unboosted spawn rule) and
        // the fastest each flies (u/s) - docs where measured, the prefab's throttle x boost otherwise.
        static readonly (string hull, float wavelength, float top)[] Fleet =
        {
            ("Serpent", 2f, 210f), ("Rhino", 5f, 840f), ("Squirrel", 7f, 300f), ("Stoat", 7f, 300f),
            ("Thresher", 7f, 2299f), ("Scarab", 7f, 100f), ("Sparrow", 14f, 135f), ("Manta", 10f, 1080f),
            ("Urchin", 10f, 260f), ("Dolphin", 12f, 357f),
        };

        [Test]
        public void Defaults_AreTheMantasLimitsAndMultipliers()
        {
            var manta = new TrailSpeedShape();   // its defaults are Caleb's pick (TrailSpeedShapeTests pins the prefab)
            var fleet = new TrailStretch();
            Assert.IsTrue(fleet.Enabled);
            Assert.AreEqual(manta.SoarSpeed, fleet.StartSpeed, Tol, "the stretch starts where the Manta's does");
            Assert.AreEqual(manta.TopSpeed, fleet.TopSpeed, Tol, "and is complete where the Manta's is");
            Assert.AreEqual(manta.TopSpacingMultiplier, fleet.SpacingMultiplier, Tol);
            Assert.AreEqual(manta.TopLength / manta.SoarLength, fleet.LengthMultiplier, Tol);
            Assert.AreEqual(manta.TopHeightMultiplier, fleet.HeightMultiplier, Tol);
            Assert.AreEqual(manta.MaxLengthOfSpacing, fleet.MaxLengthOfSpacing, Tol);
        }

        [Test]
        public void AtOrBelow600_NothingStretches()
        {
            var s = new TrailStretch();
            var prism = new Vector3(0.75f, 0.75f, 5.5f);
            foreach (float v in new[] { 0f, 35f, 135f, 300f, 357f, 599f, 600f })
            {
                Assert.AreEqual(0f, s.Amount(v), Tol, $"{v} u/s");
                Assert.AreEqual(7f, s.LaySpacing(7f, v), Tol, $"{v} u/s keeps the wavelength");
                Assert.AreEqual(prism, s.Apply(prism, v, s.LaySpacing(7f, v)), $"{v} u/s keeps the prism");
            }
        }

        [Test]
        public void AtTopSpeed_FurtherApartLongerThinner()
        {
            var s = new TrailStretch();
            var prism = new Vector3(0.75f, 0.75f, 5.5f);
            foreach (float v in new[] { 1100f, 2299f })
            {
                float spacing = s.LaySpacing(7f, v);
                Assert.AreEqual(21f, spacing, Tol);
                var t = s.Apply(prism, v, spacing);
                Assert.AreEqual(prism.x, t.x, Tol, "width is the hull's own");
                Assert.AreEqual(0.3f, t.y, Tol);
                Assert.AreEqual(11f, t.z, Tol);
            }
        }

        [Test]
        public void Stretch_RisesWithSpeed_AndNeverMakesNeighboursTouch()
        {
            var s = new TrailStretch();
            foreach (var (hull, wl, top) in Fleet)
            foreach (float z in new[] { 0.5f, 5.5f, 8f })
            {
                float lastSpacing = 0f;
                for (float v = 0f; v <= Mathf.Max(top, 1200f); v += 25f)
                {
                    float spacing = s.LaySpacing(wl, v);
                    Assert.GreaterOrEqual(spacing, lastSpacing - Tol, $"{hull} spacing at {v}");
                    lastSpacing = spacing;
                    float stretched = s.Apply(new Vector3(1f, 1f, z), v, spacing).z;
                    Assert.GreaterOrEqual(stretched, z - Tol, $"{hull} never shorter than its own prism");
                    if (stretched > z + Tol)
                        Assert.LessOrEqual(stretched, spacing * s.MaxLengthOfSpacing + Tol, $"{hull} at {v}: a gap stays");
                }
            }
        }

        [Test]
        public void Disabled_NeverStretches()
        {
            var s = new TrailStretch { Enabled = false };
            Assert.AreEqual(7f, s.LaySpacing(7f, 2000f), Tol);
            Assert.AreEqual(Vector3.one, s.Apply(Vector3.one, 2000f, 7f));
        }

        /// <summary>
        /// The balance fix: the same flight lays the same number of prisms at 30, 60 and 120 fps on every loop hull,
        /// up to its top speed, with the controller's per-frame cap applied. The timed lay this replaced laid half as
        /// many on Thresher, Rhino, Manta and Serpent at 30 fps as at 60.
        /// </summary>
        [Test]
        public void EveryHull_LaysTheSameAtEveryFrameRate()
        {
            var s = new TrailStretch();
            foreach (var (hull, wl, top) in Fleet)
            foreach (float v in new[] { 50f, top * 0.5f, Mathf.Min(top, 650f), top })
            {
                int at60 = Lays(s, wl, v, 60f);
                foreach (float fps in new[] { 30f, 120f })
                {
                    int lays = Lays(s, wl, v, fps);
                    Assert.AreEqual(at60, lays, Mathf.Max(3f, at60 * 0.02f), $"{hull} at {v} u/s, {fps} fps vs 60");
                }
            }
        }

        // 20 s of flight at a steady speed and a jittery frame time, laid as DistanceSpawnLoopAsync lays it.
        static int Lays(TrailStretch s, float wl, float speed, float fps)
        {
            var rnd = new System.Random(11);
            float travelled = float.PositiveInfinity, spacing = s.LaySpacing(wl, speed);
            int lays = 0;
            for (float time = 0f; time < 20f;)
            {
                float dt = (1f / fps) * (0.85f + 0.3f * (float)rnd.NextDouble());
                time += dt;
                bool lay = TrailSpeedShape.StepLay(ref travelled, speed * dt, spacing, out _);
                for (int n = 0; lay; n++)
                {
                    lays++;
                    if (n >= TrailSpeedShape.MaxLaysPerFrame - 1) { travelled = Mathf.Repeat(travelled, spacing); break; }
                    lay = TrailSpeedShape.NextLay(ref travelled, spacing, out _);
                }
            }
            return lays;
        }

        /// <summary>The distance lay keeps each hull's spawn rule (the Sparrow family's unboosted x2, the Shrike's and
        /// the Brittle Star's spacing multipliers): the first cut of the distance lay skipped it.</summary>
        [Test]
        public void Controller_LaysByDistanceOnly_AndKeepsTheSpawnRule()
        {
            string src = File.ReadAllText(ControllerPath);
            StringAssert.DoesNotContain("UniTask.Delay(TimeSpan.FromSeconds(finalDelay)", src, "the timed lay is gone");
            StringAssert.Contains("ApplyBoostSpawnDelay(1f)", src, "the lay spacing carries the hull's spawn rule");
        }

        /// <summary>The limits are the same for every hull: no vessel prefab authors a different start or top.</summary>
        [Test]
        public void NoHullMovesTheSpeedLimits()
        {
            foreach (string path in Directory.GetFiles(VesselPrefabs, "*.prefab"))
            {
                string yaml = File.ReadAllText(path);
                foreach (Match m in Regex.Matches(yaml, @"\n  stretch:\n((?:    .*\n)+)"))
                {
                    StringAssert.Contains("    StartSpeed: 600\n", m.Groups[1].Value, path);
                    StringAssert.Contains("    TopSpeed: 1100\n", m.Groups[1].Value, path);
                }
            }
        }
    }
}
