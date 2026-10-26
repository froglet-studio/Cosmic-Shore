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
