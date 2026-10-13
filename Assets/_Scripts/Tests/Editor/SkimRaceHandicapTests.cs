#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="SkimRaceHandicap"/> - the lobby AI difficulty's deliberate mistakes. It only edits
    /// what the driver BELIEVES (the target in its observation), so these hold the edits themselves:
    /// a new crystal is hidden until noticed, a misjudged crystal is displaced AWAY from the ribbon on
    /// its own side (never into the track) and restored once passed or timed out, and Hard carries no
    /// handicap at all. How much time each mistake costs is measured in the offline simulator
    /// (Docs/SKIM_RACE_AI.md section 10), not here.
    /// </summary>
    public class SkimRaceHandicapTests
    {
        const float CaptureRadius = 24f;

        // A straight 1000 u ribbon along +Z, face up. SkimRaceCourse treats it as a loop.
        static SkimRaceCourse Straight()
        {
            var pts = new List<Vector3>();
            var nrm = new List<Vector3>();
            for (int i = 0; i <= 100; i++) { pts.Add(new Vector3(0f, 0f, i * 10f)); nrm.Add(Vector3.up); }
            return new SkimRaceCourse(pts, nrm);
        }

        static SkimRaceObservation Obs(SkimRaceCourse course, Vector3 pos, Vector3 crystal, float time)
        {
            var rot = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var o = new SkimRaceObservation
            {
                Position = pos, Forward = Vector3.forward, Right = Vector3.right, Up = Vector3.up,
                Rotation = rot, CommandedRotation = rot, CommandedForward = Vector3.forward,
                Speed = 200f, RaceTime = time,
                HasTarget = true, TargetPosition = crystal, ToTarget = crystal - pos,
                TargetDistance = (crystal - pos).magnitude, TargetRadius = CaptureRadius,
                HasCourse = true, CourseLength = course.Length,
            };
            int h = -1, th = -1;
            o.CourseProgress = course.Project(pos, ref h, out _, out o.CourseDistance);
            float s = course.Project(crystal, ref th, out _, out _);
            o.TargetAheadOnCourse = course.Ahead(o.CourseProgress, s);
            return o;
        }

        [Test]
        public void Level_DefaultIsNoHandicap()
        {
            Assert.IsTrue(default(SkimRaceHandicapLevel).IsNone);
            Assert.IsTrue(new SkimRaceHandicapLevel(0f, 0f).IsNone);
            Assert.IsFalse(new SkimRaceHandicapLevel(0.2f, 0f).IsNone);
            Assert.IsFalse(new SkimRaceHandicapLevel(0f, 0.1f).IsNone);
        }

        [Test]
        public void NoHandicap_LeavesTheObservationAlone()
        {
            var course = Straight();
            var h = new SkimRaceHandicap(default, 1);
            var o = Obs(course, new Vector3(0, 5, 100), new Vector3(0, 5, 400), 0f);
            var v = h.View(o, course);
            Assert.IsTrue(v.HasTarget);
            Assert.AreEqual(o.TargetPosition, v.TargetPosition);
            Assert.AreEqual(0, h.Mistakes);
        }

        [Test]
        public void Reaction_HidesANewCrystal_UntilNoticed()
        {
            var course = Straight();
            var h = new SkimRaceHandicap(new SkimRaceHandicapLevel(1f, 0f), 7);
            var crystal = new Vector3(0, 5, 400);

            var first = h.View(Obs(course, new Vector3(0, 5, 100), crystal, 0f), course);
            Assert.IsFalse(first.HasTarget, "a crystal that just appeared has not been noticed yet");
            Assert.IsTrue(h.Unnoticed);

            // The longest reaction is ReactionSpreadMax x the level's value.
            float late = 1f * SkimRaceHandicap.ReactionSpreadMax + 0.01f;
            var later = h.View(Obs(course, new Vector3(0, 5, 150), crystal, late), course);
            Assert.IsTrue(later.HasTarget, "after the reaction time the crystal is seen");
            Assert.AreEqual(crystal, later.TargetPosition, "a reaction delay never moves the crystal");
        }

        [Test]
        public void Reaction_StartsAgainForEveryNewCrystal()
        {
            var course = Straight();
            var h = new SkimRaceHandicap(new SkimRaceHandicapLevel(1f, 0f), 7);
            h.View(Obs(course, new Vector3(0, 5, 100), new Vector3(0, 5, 400), 0f), course);
            Assert.IsTrue(h.View(Obs(course, new Vector3(0, 5, 200), new Vector3(0, 5, 400), 2f), course).HasTarget);

            // The crystal was taken and the next one is 300 u further on: a new crystal to notice.
            var next = h.View(Obs(course, new Vector3(0, 5, 400), new Vector3(0, 5, 700), 2.1f), course);
            Assert.IsFalse(next.HasTarget);
        }

        [Test]
        public void Mistake_BelievesTheCrystalFurtherFromTheRibbon_OnItsOwnSide()
        {
            var course = Straight();

            var above = new SkimRaceHandicap(new SkimRaceHandicapLevel(0f, 1f), 3);
            var up = above.View(Obs(course, new Vector3(0, 5, 100), new Vector3(0, 6, 400), 0f), course);
            Assert.AreEqual(1, above.Mistakes);
            Assert.IsTrue(above.Misjudging);
            Assert.Greater(up.TargetPosition.y, 6f + CaptureRadius,
                "a crystal above the ribbon is believed further ABOVE it, beyond the pickup");

            var below = new SkimRaceHandicap(new SkimRaceHandicapLevel(0f, 1f), 3);
            var down = below.View(Obs(course, new Vector3(0, -5, 100), new Vector3(0, -6, 400), 0f), course);
            Assert.Less(down.TargetPosition.y, -6f - CaptureRadius,
                "a crystal under the ribbon is believed further BELOW it - never displaced through the track");

            // Only the belief moves along the ribbon normal; the derived fields follow it.
            Assert.AreEqual(up.TargetPosition - new Vector3(0, 5, 100), up.ToTarget);
            Assert.AreEqual(up.ToTarget.magnitude, up.TargetDistance, 1e-4f);
        }

        [Test]
        public void Mistake_EndsOnceTheCrystalIsBehind_SoThePilotTurnsBack()
        {
            var course = Straight();
            var h = new SkimRaceHandicap(new SkimRaceHandicapLevel(0f, 1f), 3);
            var crystal = new Vector3(0, 6, 400);
            Assert.AreNotEqual(crystal, h.View(Obs(course, new Vector3(0, 5, 100), crystal, 0f), course).TargetPosition);

            // Flown over it: the crystal is now behind on the course.
            var passed = h.View(Obs(course, new Vector3(0, 40, 450), crystal, 2f), course);
            Assert.IsFalse(h.Misjudging);
            Assert.AreEqual(crystal, passed.TargetPosition, "past the crystal, the real one is seen again");
        }

        [Test]
        public void Mistake_NeverOutlastsItsTimeout()
        {
            var course = Straight();
            var h = new SkimRaceHandicap(new SkimRaceHandicapLevel(0f, 1f), 3);
            var crystal = new Vector3(0, 6, 400);
            h.View(Obs(course, new Vector3(0, 5, 100), crystal, 0f), course);

            // Still short of the crystal (e.g. circling), but the misjudgment has run its course.
            var v = h.View(Obs(course, new Vector3(0, 5, 150), crystal, SkimRaceHandicap.MistakeMaxSeconds + 0.1f), course);
            Assert.IsFalse(h.Misjudging);
            Assert.AreEqual(crystal, v.TargetPosition);
        }

        [Test]
        public void Chance_ZeroNeverMisjudges_AndResetForgetsTheRace()
        {
            var course = Straight();
            var never = new SkimRaceHandicap(new SkimRaceHandicapLevel(0f, 0f), 5);
            var always = new SkimRaceHandicap(new SkimRaceHandicapLevel(0f, 1f), 5);
            for (int i = 0; i < 6; i++)
            {
                var o = Obs(course, new Vector3(0, 5, 10), new Vector3(0, 6, 100 + 150 * i), i);
                never.View(o, course);
                always.View(o, course);
            }
            Assert.AreEqual(0, never.Mistakes);
            Assert.AreEqual(6, always.Mistakes);

            always.Reset();
            Assert.AreEqual(0, always.Mistakes);
            Assert.IsFalse(always.Misjudging);
        }

        [Test]
        public void Driver_DecidesOnTheBelief_AnUnnoticedCrystalFliesTheCourse()
        {
            var course = Straight();
            var cfg = ScriptableObject.CreateInstance<SkimRaceAIConfigSO>();
            var o = Obs(course, new Vector3(0, 5, 100), new Vector3(60, 5, 400), 0f);

            // An unnoticed crystal must steer exactly like no crystal at all.
            var blind = new SkimRaceDriver(cfg) { Handicap = new SkimRaceHandicap(new SkimRaceHandicapLevel(100f, 0f), 1) };
            var none = new SkimRaceDriver(cfg);
            blind.Reset();
            none.Reset();
            var noTarget = o;
            noTarget.HasTarget = false;

            var a = blind.Decide(o, course, 0f, 0.02f);
            var b = none.Decide(noTarget, course, 0f, 0.02f);
            Assert.AreEqual(b.Yaw, a.Yaw, 1e-5f);
            Assert.AreEqual(b.Pitch, a.Pitch, 1e-5f);
            Assert.AreEqual(b.Throttle, a.Throttle, 1e-5f);
        }

        [Test]
        public void Difficulty_HardIsTheUnhandicappedPilot_EasyErrsMoreThanMedium()
        {
            var settings = ScriptableObject.CreateInstance<SkimRaceDifficultySO>();
            Assert.IsTrue(settings.For(AIDifficulty.Hard).IsNone, "Hard flies the shipped policy with no mistakes");

            var easy = settings.For(AIDifficulty.Easy);
            var medium = settings.For(AIDifficulty.Medium);
            Assert.IsFalse(easy.IsNone);
            Assert.IsFalse(medium.IsNone);
            Assert.GreaterOrEqual(easy.ReactionSeconds, medium.ReactionSeconds);
            Assert.GreaterOrEqual(easy.MistakeChance, medium.MistakeChance);

            // An unwritten value resolves to the default (Medium) before it is looked up.
            Assert.AreEqual(medium.MistakeChance, settings.For(default).MistakeChance);
        }

        [Test]
        public void Difficulty_ShippedAssetLoadsFromResources()
        {
            var settings = Resources.Load<SkimRaceDifficultySO>(SkimRaceDifficultySO.ResourcePath);
            Assert.IsNotNull(settings, "Resources/SkimRaceDifficulty.asset - run Tools/Build/author_skimrace_ai_config.py");
        }
    }
}
#endif
