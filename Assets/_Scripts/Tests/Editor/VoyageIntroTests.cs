using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The voyage intro (Shombith, 2026-11-16, Milestone 1): the escort and Crystal Run ship Rev 114's Departure: the Ark
    /// wide and along its hull, your vessel, the wingmates, the title and route, then GO, every line it shows is authored, and its camera maths frames what each shot names.
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

        static void AssertDeparture(VoyageIntroSO intro)
        {
            var shots = intro.Shots;
            Assert.AreEqual(5, shots.Count, "Departure (Rev 114) is five shots and the hand-over.");
            Assert.AreEqual(VoyageIntroSO.Subject.Ark, shots[0].Subject, "Shot 1 is wide on the Ark.");
            Assert.IsTrue(string.IsNullOrEmpty(shots[0].Title) && string.IsNullOrEmpty(shots[0].Line), "Shot 1 has no words.");
            Assert.AreEqual(VoyageIntroSO.Subject.Ark, shots[1].Subject, "Shot 2 pushes along the Ark's hull.");
            StringAssert.Contains("{hull}", shots[1].Title, "The hull caption does not show the hull.");
            Assert.AreEqual(VoyageIntroSO.Subject.You, shots[2].Subject, "Shot 3 is your own vessel.");
            Assert.AreEqual(VoyageIntroSO.Subject.Wingmates, shots[3].Subject, "Shot 4 is the wingmates.");
            Assert.AreEqual(VoyageIntroSO.Subject.Course, shots[4].Subject, "Shot 5 is the title and the route.");
            Assert.IsFalse(string.IsNullOrEmpty(shots[4].Title), "The course shot has no title.");
            StringAssert.Contains("{objective}", shots[4].Line, "The goal line is not the objective's own words.");
            Assert.IsFalse(string.IsNullOrEmpty(intro.GoWord), "No GO over the hand-over.");
            StringAssert.Contains("{pilot}", intro.CardLine, "The name card does not name the pilot.");
            Assert.IsNotNull(intro.Vessels, "The intro reads no vessel list: the name cards would have no vessel.");
            Assert.That(intro.Duration(3), Is.EqualTo(9f).Within(0.01f), "A three-seat Departure runs nine seconds.");
            Assert.That(intro.Duration(1), Is.EqualTo(8f).Within(0.01f), "Alone: no wingmate shots.");
            Assert.IsTrue(intro.SkippableAfterFirstRun, "The first run plays through; later ones may be skipped.");
        }

        [Test]
        public void EscortIntro_IsTheDeparture() => AssertDeparture(Intro());

        [Test]
        public void MaySkip_OnlyAfterTheFirstRun()
        {
            Assert.IsFalse(VoyageIntro.MaySkip(skippableAfterFirstRun: true, seenBefore: false));
            Assert.IsTrue(VoyageIntro.MaySkip(skippableAfterFirstRun: true, seenBefore: true));
            Assert.IsTrue(VoyageIntro.MaySkip(skippableAfterFirstRun: false, seenBefore: false));
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
        public void CrystalRunIntro_LoadsFromResources_AndIsTheDeparture()
        {
            var intro = Resources.Load<VoyageIntroSO>(VoyageIntroSO.CrystalRunResourcePath);
            Assert.IsNotNull(intro, "VoyageIntroSO.LoadCrystalRun() finds nothing: Crystal Run would open with no intro.");
            AssertDeparture(intro);
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
