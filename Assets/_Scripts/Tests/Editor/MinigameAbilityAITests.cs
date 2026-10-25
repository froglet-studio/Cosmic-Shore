#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The AIs race their own minigames with their abilities (Yash, 2026-10-26: "other AIs should be able to use their
    /// ability in order to gain advantage on racing, in their own minigame races"; Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md
    /// §6). The Sparrow in Breakwater first: Regatta's gate steering, and its rocket fired at the station instead of on a
    /// blind timer (HoldBoostAIPolicySO's "clear the way").
    /// </summary>
    public class MinigameAbilityAITests
    {
        static bool CommitsFromApproachPoint(GateRaceController controller) =>
            (bool)typeof(GateRaceController).GetProperty("AICommitFromApproachPoint",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);

        [Test]
        public void Breakwater_FliesTheRegattaSteering()
        {
            var go = new GameObject("MinigameAbilityAITests_Breakwater");
            try { Assert.IsTrue(CommitsFromApproachPoint(go.AddComponent<BreakwaterController>())); }
            finally { Object.DestroyImmediate(go); }
        }

        static HoldBoostAIPolicySO Policy(bool withRocket)
        {
            var policy = ScriptableObject.CreateInstance<HoldBoostAIPolicySO>();
            if (withRocket)
            {
                var rocket = ScriptableObject.CreateInstance<FireGunActionSO>();
                typeof(HoldBoostAIPolicySO).GetField("clearWayRocket", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(policy, rocket);
            }
            return policy;
        }

        static void Destroy(HoldBoostAIPolicySO policy)
        {
            if (policy.ClearWayRocket) Object.DestroyImmediate(policy.ClearWayRocket);
            Object.DestroyImmediate(policy);
        }

        static AIBoostContext Station(float distance, float degreesOffNose) =>
            new(Quaternion.Euler(0f, degreesOffNose, 0f) * Vector3.forward * distance,
                Vector3.forward, Vector3.forward, 60f, 0.05f, false, false, false);

        [Test]
        public void Rocket_FiresAtAStationAheadInReach_WithAmmo()
        {
            var policy = Policy(withRocket: true);
            try
            {
                Assert.IsTrue(policy.RocketShot(Station(180f, 1f), ammo: 1f, ammoCost: 0.25f));
                Assert.IsFalse(policy.RocketShot(Station(400f, 1f), 1f, 0.25f), "out of reach: it would burst short");
                Assert.IsFalse(policy.RocketShot(Station(30f, 1f), 1f, 0.25f), "too close: it bursts in its own face");
                Assert.IsFalse(policy.RocketShot(Station(180f, 20f), 1f, 0.25f), "off the nose: the hole lands beside the eye");
                Assert.IsFalse(policy.RocketShot(Station(180f, 1f), 0.1f, 0.25f), "no rocket in the bay");
            }
            finally { Destroy(policy); }
        }

        [Test]
        public void WithoutARocket_NothingIsFired_AndTheTimerKeepsIt()
        {
            // Negative control: the base Sparrow policy (every mode but Breakwater) has no rocket, so its AIPilot timer
            // keeps firing it exactly as before.
            var policy = Policy(withRocket: false);
            try
            {
                Assert.IsFalse(policy.RocketShot(Station(180f, 1f), 1f, 0.25f));
                Assert.IsNull(policy.ClearWayRocket);
            }
            finally { Destroy(policy); }
        }

        [Test]
        public void TheBreakwaterVariant_CarriesTheSkyBurstRocket_AndTheBaseDoesNot()
        {
            var variant = Resources.Load<HoldBoostAIPolicySO>("AIBoostPolicies/SparrowAIBoostPolicy_Breakwater");
            Assert.IsNotNull(variant, "Breakwater's Sparrow drive variant is missing");
            Assert.IsNotNull(variant.ClearWayRocket, "the variant must fire the rocket at the stations");
            Assert.AreEqual("SkyBurstGunAction", variant.ClearWayRocket.name);
        }
    }
}
#endif
