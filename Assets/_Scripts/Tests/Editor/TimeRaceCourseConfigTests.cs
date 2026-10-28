using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Time Race Prototyper's courses (<see cref="TimeRaceCourseConfigSO"/>, <c>Docs/TIME_RACE_PROTOTYPER.md</c>): a
    /// course made from the shipped race IS the shipped race, a preset gives back exactly what was saved, and a race nobody
    /// prototyped flies its shipped course.
    /// </summary>
    public class TimeRaceCourseConfigTests
    {
        static void AssertSame(HeadlongCircuitSettings a, HeadlongCircuitSettings b)
        {
            Assert.AreEqual(a.RingRadius, b.RingRadius);
            Assert.AreEqual(a.BaseRadius, b.BaseRadius);
            Assert.AreEqual(a.LateralPerturbation, b.LateralPerturbation);
            Assert.AreEqual(a.AngularSpread, b.AngularSpread);
            Assert.AreEqual(a.RadialSwing, b.RadialSwing);
            Assert.AreEqual(a.AxisJitterDegrees, b.AxisJitterDegrees);
            Assert.AreEqual(a.MaxPresentDegrees, b.MaxPresentDegrees);
            Assert.AreEqual(a.CornerFloorRadius, b.CornerFloorRadius);
            Assert.AreEqual(a.CornerRadiusFactor, b.CornerRadiusFactor);
            CollectionAssert.AreEqual(a.CornerProfile, b.CornerProfile);
        }

        static HeadlongCircuitSettings Shell(HeadlongCircuitSettings s, int rings)
        {
            s.InnerRadius = RaceCourseSource.DefaultInnerRadiusFallback;
            s.OuterRadius = RaceCourseSource.DefaultOuterRadius;
            s.FirstGateDirection = Vector3.up;
            s.GateCount = rings;
            return s;
        }

        [Test]
        public void ACourseMadeFromTheShippedRace_IsTheShippedRace()
        {
            var course = TimeRaceCourse.Shipped(GameModes.Slingshot, 2, 8);
            Assert.IsTrue(course.HasRungs);
            Assert.IsFalse(course.UseThisCourse, "a new prototype must not change the game until it is switched on");
            for (int i = 1; i <= 4; i++)
            {
                var shipped = SlingshotCourse.ForIntensity(i);
                var prototyped = course.Rung(i).ApplyTo(SlingshotCourse.ForIntensity(i));
                AssertSame(shipped, prototyped);

                var a = HeadlongCircuit.Generate(7 + i, Shell(shipped, 8));
                var b = HeadlongCircuit.Generate(7 + i, Shell(prototyped, 8));
                Assert.AreEqual(a.Count, b.Count);
                for (int g = 0; g < a.Count; g++)
                    Assert.AreEqual(a[g].Position, b[g].Position, $"I{i} ring {g}");
            }
        }

        [Test]
        public void APreset_GivesBackWhatWasSaved()
        {
            var course = TimeRaceCourse.Shipped(GameModes.Warpline, 2, 5);
            JsonUtility.FromJsonOverwrite("{\"ringRadius\":20.0,\"baseRadius\":800.0}", course.Rung(3));
            course.SavePreset("tight I3", "smaller mouths");
            Assert.AreEqual("tight I3", course.ActivePreset);

            course.ResetToShipped(2, 5);
            Assert.AreEqual(SlingshotCourse.ForIntensity(3).RingRadius, course.Rung(3).RingRadius);

            Assert.IsTrue(course.LoadPreset("tight I3"));
            Assert.AreEqual(20f, course.Rung(3).RingRadius);
            Assert.AreEqual(800f, course.Rung(3).BaseRadius);
            Assert.AreEqual(SlingshotCourse.ForIntensity(1).RingRadius, course.Rung(1).RingRadius, "other rungs are untouched");

            JsonUtility.FromJsonOverwrite("{\"ringRadius\":99.0}", course.Rung(3));
            Assert.AreEqual(20f, course.Presets[0].CopyRungs()[2].RingRadius, "a preset is a copy, not a reference to the live rung");

            course.DeletePreset("tight I3");
            Assert.AreEqual(0, course.Presets.Count);
            Assert.IsFalse(course.LoadPreset("tight I3"));
        }

        [Test]
        public void SavingUnderAnExistingName_ReplacesIt()
        {
            var course = TimeRaceCourse.Shipped(GameModes.Slingshot, 2, 8);
            course.SavePreset("a", null);
            course.SavePreset(" a ", "again");
            Assert.AreEqual(1, course.Presets.Count);
            Assert.AreEqual("again", course.Presets[0].Note);
        }

        [Test]
        public void ARaceNobodyPrototyped_FliesItsShippedCourse()
        {
            // No entry for the mode (or no asset at all): the shipped cut, laps and target.
            var cfg = TimeRaceCourseConfigSO.Instance;
            if (cfg != null && cfg.Find(GameModes.Slingshot) is { UseThisCourse: true })
                Assert.Ignore("Slingshot's prototyped course is switched on in this checkout");
            for (int i = 1; i <= 4; i++)
                AssertSame(SlingshotCourse.ForIntensity(i), TimeRaceCourseConfigSO.Circuit(GameModes.Slingshot, i));
            Assert.AreEqual(3, TimeRaceCourseConfigSO.LapsFor(GameModes.Slingshot, 3));
            Assert.AreEqual(16, TimeRaceCourseConfigSO.GateTargetFor(GameModes.Slingshot, 16));
        }

        [Test]
        public void TheCourseSources_CutTheirCircuitThroughTheConfig()
        {
            var cfg = TimeRaceCourseConfigSO.Instance;
            foreach (var mode in TimeRaceCourseConfigSO.Modes)
                if (cfg != null && cfg.Find(mode) is { UseThisCourse: true })
                    Assert.Ignore($"{mode}'s prototyped course is switched on in this checkout");

            var request = new RaceCourseRequest(11, 2, 10, RaceCourseSource.DefaultInnerRadiusFallback,
                                                RaceCourseSource.DefaultOuterRadius, null);
            var built = new WarplineCourseSource().Build(request, out var failure);
            Assert.IsNull(failure);
            var expected = HeadlongCircuit.Generate(11, Shell(SlingshotCourse.ForIntensity(2), 5));
            Assert.AreEqual(expected.Count, built.Count);
            for (int g = 0; g < built.Count; g++)
                Assert.AreEqual(expected[g].Position, built[g].Position);
        }
    }
}
