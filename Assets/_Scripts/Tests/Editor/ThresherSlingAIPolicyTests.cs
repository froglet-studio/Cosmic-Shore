using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Thresher's autopilot sling (ThresherSlingAIPolicySO, 2026-10-26): it plants only at a sharp corner with room,
    /// once per corner, and lets go when its course faces the ring or it has swung long enough.
    /// </summary>
    public class ThresherSlingAIPolicyTests
    {
        ThresherSlingAIPolicySO _policy;

        [SetUp] public void SetUp() => _policy = ScriptableObject.CreateInstance<ThresherSlingAIPolicySO>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_policy);

        [Test]
        public void PlantsAtASharpCornerWithRoom() => Assert.IsTrue(_policy.WantsPlant(90f, 300f, 10f));

        [Test]
        public void DoesNotPlantOnAGentleCorner() => Assert.IsFalse(_policy.WantsPlant(40f, 300f, 10f));

        [Test]
        public void DoesNotPlantRightOnTopOfTheRing() => Assert.IsFalse(_policy.WantsPlant(120f, 60f, 10f));

        [Test]
        public void DoesNotPlantAgainStraightAfterALetGo() => Assert.IsFalse(_policy.WantsPlant(120f, 300f, 0.2f));

        [Test]
        public void LetsGoWhenTheCourseFacesTheRing() => Assert.IsTrue(_policy.WantsLetGo(10f, 0.5f));

        [Test]
        public void KeepsSwingingWhileTheRingIsStillOffTheCourse() => Assert.IsFalse(_policy.WantsLetGo(80f, 0.5f));

        [Test]
        public void GivesUpAfterTheLongestSwing() => Assert.IsTrue(_policy.WantsLetGo(170f, 3.5f));

        // The aimed launch (2026-11-02, from Garrett's Thresher Time races).

        void AimedLaunchOn()
        {
            var so = new UnityEditor.SerializedObject(_policy);
            so.FindProperty("aimedLaunch").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void TheAimedLaunchIsOffUnlessAModeTurnsItOn()
        {
            Assert.IsFalse(_policy.AimsLaunch, "the base policy keeps the plain let-go: every mode without a learned variant is unchanged");
            AimedLaunchOn();
            Assert.IsTrue(_policy.AimsLaunch);
        }

        [Test]
        public void AnAimedLetGoWaitsForTheSpinUp() => Assert.IsFalse(_policy.WantsAimedLetGo(0.5f, 1f));

        [Test]
        public void AnAimedLetGoWaitsForTheAimToBeLaidOnTheRing() => Assert.IsFalse(_policy.WantsAimedLetGo(2f, 20f));

        [Test]
        public void AnAimedLetGoGoesOnceSpunUpAndAimed() => Assert.IsTrue(_policy.WantsAimedLetGo(2f, 1f));

        [Test]
        public void AnAimedPlantStillEndsAtTheLongestSwing() => Assert.IsTrue(_policy.WantsAimedLetGo(3.5f, 90f));

        [Test]
        public void AnAimedLauncherGivesTheViewTimeBeforeAPlainLetGo()
        {
            Assert.IsTrue(_policy.WantsUnaimedLetGo(5f, 0.2f), "negative control: the plain sling lets go as the course faces the ring");
            AimedLaunchOn();
            Assert.IsFalse(_policy.WantsUnaimedLetGo(5f, 0.2f), "planted with the ring nearly ahead: wait for the launch view");
            Assert.IsTrue(_policy.WantsUnaimedLetGo(5f, 2f), "no view after the spin-up: the plain let-go");
            Assert.IsTrue(_policy.WantsUnaimedLetGo(120f, 3.5f), "and the longest swing still ends it");
        }

        [Test]
        public void TheThresherTimeVariantLaunchesAimed()
        {
            var variant = Resources.Load<ThresherSlingAIPolicySO>("AIBoostPolicies/ThresherAIBoostPolicy_ThresherTime");
            Assert.IsNotNull(variant, "the learned Thresher Time variant");
            Assert.IsTrue(variant.AimsLaunch,
                "Thresher Time's people launch aimed (85% of Garrett's planted time): its variant must too");
        }

        [Test]
        public void ThePrefabNamesThisPolicy()
        {
            var thresher = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Spacevessels/Thresher.prefab");
            Assert.IsNotNull(thresher, "Thresher.prefab");
            var pilot = thresher.GetComponentInChildren<AIPilot>(true);
            Assert.IsNotNull(pilot, "Thresher.prefab has no AIPilot");
            var so = new UnityEditor.SerializedObject(pilot);
            Assert.IsInstanceOf<ThresherSlingAIPolicySO>(so.FindProperty("boostPolicy").objectReferenceValue,
                "the AI Thresher must sling with ThresherSlingAIPolicySO");
        }
    }
}
