#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Switchback's AI steering (2026-10-26, the Dolphin's training minigame, Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md):
    /// it flies Regatta's gate steering, and with that steering on the approach point always lies inside the commit
    /// radius. Before, a 300 u approach point sat outside the 260 u commit radius on any leg over 650 u, and the AI
    /// Dolphins orbited it for the whole race (0-2 of 20 rings in every recorded race).
    /// </summary>
    public class SwitchbackAISteeringTests
    {
        const float Lead = 300f, Commit = 260f;   // MinigameSwitchback.unity (the GateRaceController defaults)

        static bool CommitsFromApproachPoint(GateRaceController controller)
        {
            var prop = typeof(GateRaceController).GetProperty("AICommitFromApproachPoint",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(prop, "GateRaceController.AICommitFromApproachPoint is gone");
            return (bool)prop.GetValue(controller);
        }

        [Test]
        public void Switchback_FliesTheRegattaSteering_WhateverTheSceneSays()
        {
            var go = new GameObject("SwitchbackAISteeringTests");
            try
            {
                var switchback = go.AddComponent<SwitchbackController>();   // the scene leaves the switch off
                Assert.IsTrue(CommitsFromApproachPoint(switchback), "the AI Dolphins would orbit gate 0 again");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void OtherGateRaces_KeepTheirSceneSwitch()
        {
            // Negative control: Headlong did not opt in, so it reads its own (off) inspector switch.
            var go = new GameObject("SwitchbackAISteeringTests_Headlong");
            try
            {
                Assert.IsFalse(CommitsFromApproachPoint(go.AddComponent<HeadlongController>()));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ApproachLead_OnALongLeg_StaysInsideTheCommitRadius()
        {
            float lead = GateRaceController.ApproachLeadFor(Lead, 900f, Commit, fromApproachPoint: true);
            Assert.Less(lead, GateRaceController.CommitDistanceFor(Commit, lead, 35f, true),
                "a pilot that reaches its approach point must be inside the commit radius, or it orbits the point");
        }

        [Test]
        public void ApproachLead_WithTheSwitchOff_IsAsShipped()
        {
            // Every gate race that has not opted in keeps its lead exactly: the 40% leg cap, then the authored lead.
            Assert.AreEqual(Lead, GateRaceController.ApproachLeadFor(Lead, 900f, Commit, fromApproachPoint: false), 1e-4f);
            Assert.AreEqual(0.4f * 500f, GateRaceController.ApproachLeadFor(Lead, 500f, Commit, fromApproachPoint: false), 1e-4f);
        }

        [Test]
        public void ApproachLead_OnAShortLeg_KeepsTheLegCap()
        {
            Assert.AreEqual(0.4f * 400f, GateRaceController.ApproachLeadFor(Lead, 400f, Commit, fromApproachPoint: true), 1e-4f);
        }
    }
}
#endif
