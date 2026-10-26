#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
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
        public void Waystation_FliesTheRegattaSteering_WhateverTheSceneSays()
        {
            var go = new GameObject("SwitchbackAISteeringTests_Waystation");
            try
            {
                Assert.IsTrue(CommitsFromApproachPoint(go.AddComponent<WaystationController>()),
                    "the AI Butterflies would orbit the first ring again (0 of 24 in Caleb's races, 2026-10-20/21)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Warpline_FliesTheRegattaSteering_WhateverTheSceneSays()
        {
            var go = new GameObject("SwitchbackAISteeringTests_Warpline");
            try
            {
                Assert.IsTrue(CommitsFromApproachPoint(go.AddComponent<WarplineController>()),
                    "the AI Stoats would cross the rings off their mouths again (0 of 30 races finished, 2026-10-25..27)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Warpline_TheAIStoatLetsGoOfItsWarpWellBeforeTheRing()
        {
            // Tools/Build/switchback_dolphin_sim.py --mode Warpline (2026-10-27): warped to 120-170 u/s at a fixed 48 deg/s
            // the Stoat needs ~200 u to line up on a 26-48 u ring; let go at the base 60 u it missed 180-280 rings over
            // Yash's 15 courses, at 250 u none.
            var tuned = Resources.Load<StoatDipoleConfigSO>("AIBoostPolicies/StoatDipoleConfig_Warpline");
            Assert.IsNotNull(tuned, "Warpline's Stoat autopilot variant is missing");
            Assert.GreaterOrEqual(tuned.AutopilotLetGoNear, 200f);
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

        // AIPilot.TurnHardIfBehind: a target dead astern used to write no turn (the stick scales with sin(error)), and an
        // AI Dolphin flew 14,600 u away from Switchback's course after a missed mouth (Yash's I4 race, 2026-10-26).
        [Test]
        public void TargetDeadAstern_TurnsAtFullStick()
        {
            Vector3 cross = Vector3.zero; float angle = 0f;   // exactly behind: no cross product at all
            AIPilot.TurnHardIfBehind(-1f, ref cross, ref angle);
            Assert.AreEqual(1f, Mathf.Clamp(angle * cross.y, -1f, 1f), 1e-4f, "a full yaw when the target is exactly astern");
        }

        [Test]
        public void TargetNearlyAstern_TurnsFullTowardItsSide()
        {
            Vector3 cross = new(0.02f, -0.03f, 0f); float angle = 0.1f;   // ~178 deg off: the old stick was ~0.003
            AIPilot.TurnHardIfBehind(-0.999f, ref cross, ref angle);
            Assert.AreEqual(-1f, Mathf.Clamp(angle * cross.y, -1f, 1f), 1e-4f, "full stick, on the target's own side");
        }

        [Test]
        public void TargetAhead_SteersAsBefore()
        {
            // Negative control: anything in front of the nose keeps the shipped stick exactly.
            Vector3 cross = new(0.05f, 0.1f, 0f); float angle = 1.3f;
            AIPilot.TurnHardIfBehind(0.99f, ref cross, ref angle);
            Assert.AreEqual(new Vector3(0.05f, 0.1f, 0f), cross);
            Assert.AreEqual(1.3f, angle, 1e-6f);
        }

        [Test]
        public void ApproachLead_OnAShortLeg_KeepsTheLegCap()
        {
            Assert.AreEqual(0.4f * 400f, GateRaceController.ApproachLeadFor(Lead, 400f, Commit, fromApproachPoint: true), 1e-4f);
        }
    }
}
#endif
