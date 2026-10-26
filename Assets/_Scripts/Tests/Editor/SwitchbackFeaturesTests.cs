using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Switchback course features (<see cref="SwitchbackFeatures"/>): intensity 3's skim walls and intensity 4's
    /// danger prisms, swept over generated courses. The course is random, so the contract is that a feature sits
    /// beside its own leg and never in a gate's mouth, across another leg's line or inside the nucleus.
    /// </summary>
    public class SwitchbackFeaturesTests
    {
        // The shipped shell, as SwitchbackCourseTests sweeps it.
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Gates = 20;
        const int Seeds = 200;

        // What SwitchbackController passes: the nucleus (inner / 1.22) plus its 30 u margin.
        const float ClearRadius = Inner / 1.22f + 30f;

        static List<RaceGate> Course(int intensity, int seed)
        {
            var s = SwitchbackCourseSettings.ForIntensity(intensity);
            s.GateCount = Gates;
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            s.FirstGateDistance = 620f;
            return SwitchbackCourse.Generate(seed, s);
        }

        static List<SwitchbackFeaturePrism> Layout(List<RaceGate> course, int intensity) =>
            SwitchbackFeatures.Layout(course, intensity, Vector3.zero, ClearRadius);

        static float Offset(int intensity) =>
            intensity == 3 ? SwitchbackFeatures.WallOffset : SwitchbackFeatures.DangerOffset;

        [Test]
        public void NothingIsLaidBelowIntensityThree([Values(1, 2)] int intensity)
        {
            Assert.IsFalse(SwitchbackFeatures.HasFeatures(intensity));
            for (int seed = 1; seed <= Seeds; seed++)
                Assert.IsEmpty(Layout(Course(intensity, seed), intensity), $"intensity {intensity} seed {seed}");
        }

        [Test]
        public void IntensityThreeLaysPlainSkimWalls()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var prisms = Layout(Course(3, seed), 3);
                // Nine walled legs of 460-720 u, a prism every 14 u over the middle half: about 190.
                Assert.Greater(prisms.Count, 100, $"seed {seed}: only {prisms.Count} wall prisms.");
                foreach (var p in prisms)
                {
                    Assert.AreEqual(PrismKind.Plain, p.Kind, $"seed {seed}: a wall prism is not plain.");
                    Assert.AreEqual(SwitchbackFeatures.WallPrismScale, p.Scale);
                }
            }
        }

        [Test]
        public void IntensityFourLaysDangerPrisms()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var prisms = Layout(Course(4, seed), 4);
                // Three per leg on the 18 legs after the first, less the few a crossing leg or gate removes.
                Assert.Greater(prisms.Count, 40, $"seed {seed}: only {prisms.Count} danger prisms.");
                Assert.LessOrEqual(prisms.Count, SwitchbackFeatures.DangerPerLeg * (Gates - 2));
                foreach (var p in prisms)
                    Assert.AreEqual(PrismKind.Danger, p.Kind, $"seed {seed}: a danger prism is not danger.");
            }
        }

        [Test]
        public void EveryFeatureSitsAtItsOffsetBesideALeg([Values(3, 4)] int intensity)
        {
            float offset = Offset(intensity);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var course = Course(intensity, seed);
                foreach (var p in Layout(course, intensity))
                {
                    bool besideOwnLeg = false;
                    for (int k = 1; k + 1 < course.Count && !besideOwnLeg; k++)
                        besideOwnLeg = Mathf.Abs(SwitchbackFeatures.DistanceToSegment(
                            p.Position, course[k].Position, course[k + 1].Position) - offset) < 0.05f;
                    Assert.IsTrue(besideOwnLeg,
                        $"intensity {intensity} seed {seed}: a prism at {p.Position} is not {offset} u beside any leg " +
                        "after the first.");
                }
            }
        }

        [Test]
        public void NoFeatureSitsInAGateMouth([Values(3, 4)] int intensity)
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var course = Course(intensity, seed);
                foreach (var p in Layout(course, intensity))
                    foreach (var gate in course)
                        Assert.GreaterOrEqual((p.Position - gate.Position).magnitude,
                            gate.Radius + SwitchbackFeatures.MouthClearance - 0.01f,
                            $"intensity {intensity} seed {seed}: a prism sits in a gate's mouth.");
            }
        }

        [Test]
        public void NoFeatureStandsOnAnyLegsLine([Values(3, 4)] int intensity)
        {
            float clearance = Offset(intensity) * SwitchbackFeatures.LineClearanceShare;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var course = Course(intensity, seed);
                foreach (var p in Layout(course, intensity))
                    for (int k = 0; k + 1 < course.Count; k++)
                        Assert.GreaterOrEqual(
                            SwitchbackFeatures.DistanceToSegment(p.Position, course[k].Position, course[k + 1].Position),
                            clearance - 0.01f,
                            $"intensity {intensity} seed {seed}: a prism stands on leg {k}'s line.");
            }
        }

        [Test]
        public void NoFeatureSitsInsideTheNucleus([Values(3, 4)] int intensity)
        {
            for (int seed = 1; seed <= Seeds; seed++)
                foreach (var p in Layout(Course(intensity, seed), intensity))
                    Assert.GreaterOrEqual(p.Position.magnitude, ClearRadius - 0.01f,
                        $"intensity {intensity} seed {seed}: a prism sits inside the nucleus.");
        }

        [Test]
        public void TheLayoutIsTheSameOnEveryPeer([Values(3, 4)] int intensity)
        {
            // Every peer lays from the broadcast course, so the same course must give the same prisms.
            for (int seed = 1; seed <= 20; seed++)
            {
                var a = Layout(Course(intensity, seed), intensity);
                var b = Layout(Course(intensity, seed), intensity);
                Assert.AreEqual(a.Count, b.Count);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a[i].Position, b[i].Position);
                    Assert.AreEqual(a[i].Rotation, b[i].Rotation);
                }
            }
        }
    }
}
