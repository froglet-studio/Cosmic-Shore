#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="SkimRaceTrackFingerprint"/> - the Skim Race map fingerprint a tuned AI policy records -
    /// and the rule that decides whether a tuned policy still fits the live map. The golden values are
    /// the ones <c>Tools/Build/skimrace_track_fingerprint.py --self-test</c> asserts: the two
    /// implementations agree only if both pass. Whether the shipped policies match the shipped scene is
    /// that script's <c>--check</c> (it reads the scene file; these tests never open a scene).
    /// </summary>
    public class SkimRaceTrackFingerprintTests
    {
        static readonly List<Vector3> Points = new()
        {
            new Vector3(0f, 0f, 0f), new Vector3(100.4f, -20.6f, 300f), new Vector3(-250.5f, 12.5f, 7f),
        };

        static readonly List<Vector3> Anchors = new() { new Vector3(10f, 20f, 30f), new Vector3(-5.25f, 0.75f, 1000f) };

        static string Golden(IReadOnlyList<Vector3> points = null, bool spline = true, int laps = 3, IReadOnlyList<Vector3> anchors = null) =>
            SkimRaceTrackFingerprint.Compute(points ?? Points, spline, laps, anchors ?? Anchors);

        [Test]
        public void Compute_MatchesTheValuesThePythonScriptAsserts()
        {
            Assert.AreEqual("0cd105f6", Golden());
            Assert.AreEqual("07b13f04", SkimRaceTrackFingerprint.Compute(null, false, 0, null));
            Assert.AreEqual(SkimRaceTrackFingerprint.Compute(new List<Vector3>(), false, 0, new List<Vector3>()),
                SkimRaceTrackFingerprint.Compute(null, false, 0, null), "a missing list is an empty one");
            Assert.AreEqual(Golden(), Golden(Points.ToArray(), anchors: Anchors.ToArray()), "lists and arrays read the same");
        }

        [Test]
        public void Compute_CountsWholeUnits_ASubUnitNudgeIsTheSameMap()
        {
            var nudged = new List<Vector3>(Points) { [1] = new Vector3(100.3f, -20.6f, 300f) };
            var moved = new List<Vector3>(Points) { [1] = new Vector3(101.4f, -20.6f, 300f) };
            Assert.AreEqual(Golden(), Golden(nudged), "0.1 of a unit is not a different map");
            Assert.AreNotEqual(Golden(), Golden(moved), "a whole unit is");
        }

        [Test]
        public void Compute_TheCurveTheLapsAndTheCrystalsEachMakeADifferentMap()
        {
            Assert.AreNotEqual(Golden(), Golden(spline: false));
            Assert.AreNotEqual(Golden(), Golden(laps: 2));
            Assert.AreNotEqual(Golden(), Golden(anchors: Anchors.GetRange(0, 1)));
            Assert.AreNotEqual(Golden(), Golden(anchors: new List<Vector3> { Anchors[1], Anchors[0] }), "crystal ORDER is the race too");
            Assert.IsTrue(Regex.IsMatch(Golden(), "^[0-9a-f]{8}$"));
        }

        [Test]
        public void FitsTrack_EmptyIsAnyMap_UnreadableMapIsTrusted_AnotherMapIsNot()
        {
            var cfg = ScriptableObject.CreateInstance<SkimRaceAIConfigSO>();
            Assert.IsTrue(cfg.FitsTrack("19fadf77"), "no recorded map = for any map (the general policy)");

            cfg.TrackFingerprint = "19fadf77";
            Assert.IsTrue(cfg.FitsTrack("19fadf77"));
            Assert.IsTrue(cfg.FitsTrack("19FADF77"));
            Assert.IsTrue(cfg.FitsTrack(null), "a map that cannot be read gives no reason to distrust the file");
            Assert.IsFalse(cfg.FitsTrack("b8aa5b7c"), "tuned on another map");
        }

        [Test]
        public void ShippedPolicies_TunedFilesRecordTheirMap_TheGeneralOneRecordsNone()
        {
            int tuned = 0;
            foreach (var p in Resources.LoadAll<SkimRaceAIConfigSO>(""))
            {
                if (p.name == SkimRaceAIConfigSO.ResourcePath)
                    Assert.IsEmpty(p.TrackFingerprint, "the general policy flies every map, so it records none");
                else if (Regex.IsMatch(p.name, @"^SkimRaceAIConfig_I\d+$"))
                {
                    tuned++;
                    StringAssert.IsMatch("^[0-9a-f]{8}$", p.TrackFingerprint,
                        $"{p.name} must record the map it was tuned on - Tools/Build/author_skimrace_ai_config.py");
                }
            }
            Assert.Greater(tuned, 0);
        }

        [Test]
        public void Deployment_AnIntensityWithoutItsOwnFile_FliesTheGeneralPolicy()
        {
            // Intensity 3 has no tuned file today and 5 does not exist yet: both get the general policy,
            // which records no map, so no scene is consulted.
            Assert.AreSame(SkimRaceAIConfigSO.LoadDefault(), SkimRaceAIDeployment.PolicyFor(3));
            Assert.AreSame(SkimRaceAIConfigSO.LoadDefault(), SkimRaceAIDeployment.PolicyFor(5));
        }
    }
}
#endif
