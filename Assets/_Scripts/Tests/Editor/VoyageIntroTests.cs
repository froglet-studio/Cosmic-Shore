using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The voyage intro (Shombith, 2026-11-16, Milestone 1): the escort ships one, it introduces the Ark, the squad and
    /// the objective in that order, every line it shows is authored, and its camera maths frames what each shot names.
    /// </summary>
    public class VoyageIntroTests
    {
        const string IntroPath = "Assets/Resources/Voyages/ArkEscortIntro.asset";

        static VoyageIntroSO Intro()
        {
            var intro = AssetDatabase.LoadAssetAtPath<VoyageIntroSO>(IntroPath);
            Assert.IsNotNull(intro, $"No voyage intro at {IntroPath}: the escort would open with no intro.");
            return intro;
        }

        [Test]
        public void EscortIntro_LoadsFromResources()
        {
            Assert.IsNotNull(Resources.Load<VoyageIntroSO>(VoyageIntroSO.EscortResourcePath),
                "VoyageIntroSO.LoadEscort() finds nothing at its Resources path.");
        }

        [Test]
        public void EscortIntro_ShowsTheArk_ThenTheSquad_ThenTheObjective()
        {
            var shots = Intro().Shots;
            Assert.AreEqual(3, shots.Count);
            Assert.AreEqual(VoyageIntroSO.Subject.Ark, shots[0].Subject);
            Assert.AreEqual(VoyageIntroSO.Subject.Squad, shots[1].Subject);
            Assert.AreEqual(VoyageIntroSO.Subject.Course, shots[2].Subject);
            foreach (var s in shots)
            {
                Assert.IsFalse(string.IsNullOrEmpty(s.Title), $"A {s.Subject} shot has no title.");
                Assert.Greater(s.Seconds, 0.4f);
            }
            StringAssert.Contains("{vessel}", shots[1].Title, "The squad shot does not name the vessel.");
            StringAssert.Contains("{worlds}", shots[2].Line, "The objective does not name the worlds to cross.");
        }

        [Test]
        public void EscortIntro_IsShortAndSkippable()
        {
            var intro = Intro();
            Assert.IsNotNull(intro.Vessels, "The intro reads no vessel list: the squad shots would have no names.");
            Assert.IsFalse(string.IsNullOrEmpty(intro.SkipHint), "The skip is not shown.");
            Assert.LessOrEqual(intro.HoldToSkipSeconds, 1f);
            Assert.LessOrEqual(intro.Duration(3), 25f, "A three-seat intro runs longer than 25 s.");
        }

        [Test]
        public void Orbit_AzimuthZero_IsAhead_Ninety_IsStarboard_AndLooksAtTheAim()
        {
            var frame = Quaternion.Euler(0f, 30f, 0f);
            var center = new Vector3(10f, 5f, -3f);
            var ahead = VoyageIntroFraming.Orbit(center, frame, 0f, 0f, 100f, center);
            Assert.That(Vector3.Distance(ahead.position, center + frame * Vector3.forward * 100f), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(ahead.rotation * Vector3.forward, center - ahead.position), Is.LessThan(0.1f));

            var starboard = VoyageIntroFraming.Orbit(center, frame, 90f, 0f, 100f, center);
            Assert.That(Vector3.Distance(starboard.position, center + frame * Vector3.right * 100f), Is.LessThan(0.01f));

            var above = VoyageIntroFraming.Orbit(center, frame, 180f, 30f, 100f, center);
            Assert.Greater(Vector3.Dot(above.position - center, frame * Vector3.up), 0f, "Positive elevation is below the subject.");
        }

        [Test]
        public void Evaluate_MovesFromTheStartToTheEndOfTheShot()
        {
            var shot = new VoyageIntroSO.Shot { FromAzimuth = 0f, ToAzimuth = 90f, Elevation = 0f, FromDistance = 2f, ToDistance = 1f };
            var a = VoyageIntroFraming.Evaluate(shot, Vector3.zero, Quaternion.identity, 10f, 0f);
            var b = VoyageIntroFraming.Evaluate(shot, Vector3.zero, Quaternion.identity, 10f, 1f);
            Assert.That(Vector3.Distance(a.position, new Vector3(0f, 0f, 20f)), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(b.position, new Vector3(10f, 0f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void Chase_SitsBehindAndAbove_LookingAhead()
        {
            var rot = Quaternion.Euler(10f, 45f, 0f);
            var pose = VoyageIntroFraming.Chase(Vector3.zero, rot, 5f, 4f, 1f);
            Assert.Less(Vector3.Dot(pose.position, rot * Vector3.forward), 0f);
            Assert.Greater(Vector3.Dot(pose.position, rot * Vector3.up), 0f);
            Assert.Greater(Vector3.Dot(pose.rotation * Vector3.forward, rot * Vector3.forward), 0.9f);
        }
    }
}
