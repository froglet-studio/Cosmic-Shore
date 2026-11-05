using System.Collections.Generic;
using System.IO;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Orrery (<see cref="SlingshotOrrery"/>, <c>Arcade/SLINGSHOT.md</c> "The Orrery") on the shipped Slingshot circuits:
    /// a moon at every corner just outside its ring, a ladder that adds one demand a level, every clearance and budget kept,
    /// and the same layout on every run. <c>Tools/Build/orrery_port.py</c> ports it for the offline model.
    /// </summary>
    public class SlingshotOrreryTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const float NucleusClear = Inner / 1.22f + 30f;   // SlingshotController: inner / innerRadiusNucleusFactor + margin
        static readonly int[] Seeds = { 3, 17, 101, 2026, 90210 };

        static List<RaceGate> Circuit(int intensity, int seed)
        {
            var s = SlingshotCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        static SwitchbackFeatureLayout Lay(List<RaceGate> course, int intensity, List<OrreryMoon> moons) =>
            SlingshotOrrery.Layout(course, intensity, Vector3.zero, NucleusClear, moons);

        [Test]
        public void TheSameCourse_LaysTheSameOrrery()
        {
            var course = Circuit(4, 2026);
            var a = Lay(course, 4, new List<OrreryMoon>());
            var b = Lay(course, 4, new List<OrreryMoon>());
            Assert.AreEqual(a.Prisms.Count, b.Prisms.Count);
            for (int i = 0; i < a.Prisms.Count; i++) Assert.AreEqual(a.Prisms[i].Position, b.Prisms[i].Position);
            Assert.AreEqual(a.Movers.Count, b.Movers.Count);
        }

        [Test]
        public void EveryTurningCorner_HasAMoonJustOutsideItsRing_OnTheInside()
        {
            foreach (int intensity in new[] { 1, 2, 3, 4 })
            foreach (int seed in Seeds)
            {
                var course = Circuit(intensity, seed);
                var moons = new List<OrreryMoon>();
                Lay(course, intensity, moons);
                int turning = 0;
                for (int g = 0; g < course.Count; g++)
                {
                    var a = (course[g].Position - course[(g - 1 + course.Count) % course.Count].Position).normalized;
                    var b = (course[(g + 1) % course.Count].Position - course[g].Position).normalized;
                    if (Vector3.Angle(a, b) >= SlingshotOrrery.MinTurnDegrees) turning++;
                }
                Assert.AreEqual(turning, moons.Count, $"I{intensity} seed {seed}: one moon per turning corner");
                foreach (var m in moons)
                {
                    var gate = course[m.Gate];
                    float gap = (m.Centre - gate.Position).magnitude - gate.Radius - m.Radius;
                    Assert.AreEqual(SlingshotOrrery.MoonRimGap, gap, 1e-3f, "the moon sits just outside its ring's rim");
                    Assert.Greater(Vector3.Dot(m.Centre - gate.Position, m.Outbound - m.Inbound), 0f, "on the turn's inside");
                }
            }
        }

        [Test]
        public void APressAtThePressPoint_LaysThePairOnTheMoon()
        {
            // StoatSlingConfig.asset: the attractor is laid catchAhead along the nose and catchSide to the pressed side.
            var sling = File.ReadAllText("Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset").Replace("\r\n", "\n");
            StringAssert.Contains($"  catchAhead: {SlingshotOrrery.PressAhead}\n", sling);
            StringAssert.Contains($"  catchSide: {SlingshotOrrery.PressSide}\n", sling);
            var moons = new List<OrreryMoon>();
            Lay(Circuit(1, 17), 1, moons);
            foreach (var m in moons)
            {
                var inward = (m.Outbound - m.Inbound).normalized;
                var laid = m.PressPoint + m.Inbound * SlingshotOrrery.PressAhead + inward * SlingshotOrrery.PressSide;
                Assert.Less((laid - m.Centre).magnitude, 1e-3f);
            }
        }

        [Test]
        public void EveryPrism_KeepsOffOtherRingsAndLegs_AndTheNucleus()
        {
            foreach (int intensity in new[] { 1, 2, 3, 4 })
            foreach (int seed in Seeds)
            {
                var course = Circuit(intensity, seed);
                var layout = Lay(course, intensity, new List<OrreryMoon>());
                int n = course.Count;
                for (int k = 0; k < layout.Prisms.Count; k++)
                {
                    var p = layout.Prisms[k];
                    Assert.Greater(p.Position.magnitude, NucleusClear, "out of the nucleus");
                    int touching = 0;
                    for (int g = 0; g < n; g++)
                        if ((p.Position - course[g].Position).magnitude - course[g].Radius < SwitchbackFeatures.MouthClearance) touching++;
                    Assert.LessOrEqual(touching, 1, $"I{intensity} seed {seed} prism {k}: inside two rings' clearance");
                    int nearLegs = 0;
                    for (int g = 0; g < n; g++)
                        if (SwitchbackFeatures.DistanceToSegment(p.Position, course[g].Position, course[(g + 1) % n].Position)
                            < SlingshotOrrery.LineClearance) nearLegs++;
                    Assert.LessOrEqual(nearLegs, 2, $"I{intensity} seed {seed} prism {k}: on a leg that is not its own");
                }
            }
        }

        [Test]
        public void TheLadder_AddsOneDemandALevel_AndGetsDenser()
        {
            int last = -1;
            foreach (int intensity in new[] { 1, 2, 3, 4 })
            {
                var c = Circuit(intensity, 101);
                var layout = Lay(c, intensity, new List<OrreryMoon>());
                Assert.Greater(layout.Prisms.Count, last, $"I{intensity} is denser than the level below");
                Assert.LessOrEqual(layout.Prisms.Count, SlingshotOrrery.TotalCap(intensity), "the 100 fps budget");
                last = layout.Prisms.Count;
                int danger = 0;
                foreach (var p in layout.Prisms) if (p.Kind == PrismKind.Danger) danger++;
                if (intensity <= 2) Assert.AreEqual(0, danger, $"I{intensity} has no danger");
                else Assert.Greater(danger, 0, $"I{intensity} has danger");
                if (intensity < 4) Assert.AreEqual(0, layout.Movers.Count, $"I{intensity} has no shepherds");
            }
            Assert.IsTrue(SlingshotOrrery.HasBeacons(1) && !SlingshotOrrery.HasBeacons(2), "beacons teach I1 only");
            Assert.IsTrue(SlingshotOrrery.HasBelts(2) && !SlingshotOrrery.HasBelts(1));
            Assert.IsTrue(SlingshotOrrery.HasTails(3) && !SlingshotOrrery.HasTails(2));
            Assert.IsTrue(SlingshotOrrery.HasShepherds(4) && !SlingshotOrrery.HasShepherds(3));
        }

        [Test]
        public void Shepherds_CircleTheirMoon_OnTheSharedClock_WithinTheMovingBudget()
        {
            int seen = 0;
            foreach (int seed in Seeds)
            {
                var moons = new List<OrreryMoon>();
                var layout = Lay(Circuit(4, seed), 4, moons);
                int moving = 0;
                foreach (var mover in layout.Movers)
                {
                    Assert.AreEqual(BlastRunMotion.Spin, mover.Motion);
                    Assert.AreEqual(4, mover.Count);
                    moving += mover.Count;
                    var moon = moons[mover.Station];
                    Assert.Less((mover.Pivot - moon.Centre).magnitude, 1e-3f, "it circles its own moon");
                    mover.PoseAt(SlingshotOrrery.ShepherdPeriod * 0.5f, out var spin, out _);
                    Assert.AreEqual(180f, Quaternion.Angle(Quaternion.identity, spin), 0.5f, "half a lap in half a period");
                    seen++;
                }
                Assert.LessOrEqual(moving, SlingshotOrrery.MovingPrismCap(4));
            }
            Assert.Greater(seen, 0, "some I4 course has shepherds");
        }

        [Test]
        public void TheSlingshotScene_LaysTheOrrery()
        {
            var scene = File.ReadAllText("Assets/_Scenes/Multiplayer Scenes/MinigameSlingshot.unity");
            StringAssert.Contains("featuresPrefab: {fileID: 5330000000000103, guid: 29ec5eb8fa7cb3fd1d816c63b5b39687, type: 3}", scene,
                "the SlingshotController lays the Orrery with Blast Run's spawnable");
        }
    }
}
