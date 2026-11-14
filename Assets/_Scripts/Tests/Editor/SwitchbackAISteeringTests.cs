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
        public void Headlong_FliesTheRegattaSteering_WhateverTheSceneSays()
        {
            var go = new GameObject("SwitchbackAISteeringTests_Headlong");
            try
            {
                Assert.IsTrue(CommitsFromApproachPoint(go.AddComponent<HeadlongController>()),
                    "the AI Rhinos would loiter beside a missed I3-I4 mouth again (Docs/Reports/RHINO_HEADLONG_I4_STALLS.md)");
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
                    "the AI Meerkats would circle the rings as the AI Stoats did (0 of 22 finished in Yash's races, 2026-10-25/26)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Slingshot_FliesTheRegattaSteering_WhateverTheSceneSays()
        {
            // The Stoat's Time race since 2026-10-27: its scene carries Warpline's 180 u lead against a 160 u commit.
            var go = new GameObject("SwitchbackAISteeringTests_Slingshot");
            try
            {
                Assert.IsTrue(CommitsFromApproachPoint(go.AddComponent<SlingshotController>()),
                    "the AI Stoats would circle Slingshot's rings the way they circled Warpline's");
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
        public void Slingshot_TheAIStoatLetsGoOfItsWarpWellBeforeTheRing()
        {
            // Slingshot's variants start from Warpline's (2026-10-27): the same hull, the same warp, the same turn.
            var tuned = Resources.Load<StoatDipoleConfigSO>("AIBoostPolicies/StoatDipoleConfig_Slingshot");
            Assert.IsNotNull(tuned, "Slingshot's Stoat autopilot variant is missing");
            Assert.GreaterOrEqual(tuned.AutopilotLetGoNear, 200f);
            Assert.IsNotNull(Resources.Load<StoatSlingConfigSO>("AIBoostPolicies/StoatSlingConfig_Slingshot"),
                "Slingshot's Stoat sling variant is missing");
        }

        static T Field<T>(object o, string name)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"{o.GetType().Name}.{name} is gone");
            return (T)f.GetValue(o);
        }

        [Test]
        public void Switchback_BlastAI_Defaults_AreSteps1And2()
        {
            // SWITCHBACK.md "Step 1" / "Step 2" (2026-11-02, switchback_sim_harness): dodge only Danger prisms and nothing in
            // the last 200 u to the ring; strike the key straight, the drift aiming the blast, with no setup leg behind it.
            var go = new GameObject("SwitchbackAISteeringTests_BlastAIDefaults");
            try
            {
                var c = go.AddComponent<SwitchbackController>();
                Assert.IsTrue(Field<bool>(c, "aiDodgeDangerOnly"));
                Assert.AreEqual(200f, Field<float>(c, "aiNoDodgeNearRing"));
                Assert.IsFalse(Field<bool>(c, "aiStrikeSetupLeg"), "the setup leg cost about what the blast saved");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Switchback_AINoseLag_TableByIntensityAndDifficulty()
        {
            // SWITCHBACK.md "Lag by difficulty" (2026-11-02, Yash's bands): each level 10 s apart at every intensity, I4
            // Easy 130-150 s, Medium 120-140, Hard 110-130, Legendary 100-120. A faster level always follows faster.
            var go = new GameObject("SwitchbackAISteeringTests_NoseLag");
            try
            {
                var table = Field<SwitchbackNoseFollow[]>(go.AddComponent<SwitchbackController>(), "aiNoseFollowByIntensity");
                Assert.AreEqual(4, table.Length, "one row per intensity");
                var i4 = table[3];
                CollectionAssert.AreEqual(new[] { 1.8f, 2.2f, 3.5f, 6f }, new[] { i4.easy, i4.medium, i4.hard, i4.legendary });
                for (int intensity = 1; intensity <= 4; intensity++)
                for (int level = 1; level <= 4; level++)
                {
                    float rate = SwitchbackController.NoseFollowFor(table, intensity, level);
                    Assert.Greater(rate, VesselTransformer.RotationFollowRate, $"I{intensity} level {level}: faster than the fleet lag");
                    if (level > 1)
                        Assert.GreaterOrEqual(rate, SwitchbackController.NoseFollowFor(table, intensity, level - 1),
                            $"I{intensity}: level {level} follows at least as fast as the level below");
                }
                Assert.Less(SwitchbackController.NoseFollowFor(table, 0, 1), 0f, "an unknown intensity keeps the fleet lag");
                Assert.Less(SwitchbackController.NoseFollowFor(table, 4, 5), 0f, "an unknown level keeps the fleet lag");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The I4 gauntlet flies its own row (SWITCHBACK.md "Lag by difficulty", the gauntlet bands of 2026-11-03:
        /// about 10 s apart; retuned 2026-11-13 for the plain seals to Easy ~176 s, Medium ~167, Hard ~155, Legendary ~141 offline,
        /// Easy on the fleet lag); I1-I3, and I4 in a mode without the gauntlet
        /// (Scarab Rally), keep the per-intensity table.</summary>
        [Test]
        public void Switchback_AINoseLag_TheGauntletHasItsOwnRow()
        {
            var go = new GameObject("SwitchbackAISteeringTests_GauntletLag");
            try
            {
                var c = go.AddComponent<SwitchbackController>();
                var table = Field<SwitchbackNoseFollow[]>(c, "aiNoseFollowByIntensity");
                var g = Field<SwitchbackNoseFollow>(c, "aiNoseFollowGauntlet");
                CollectionAssert.AreEqual(new[] { 1.5f, 2.2f, 3.5f, 6f }, new[] { g.easy, g.medium, g.hard, g.legendary });
                for (int level = 1; level <= 4; level++)
                {
                    Assert.AreEqual(g.For(level), SwitchbackController.NoseFollowFor(table, g, true, 4, level), "I4 gauntlet");
                    Assert.AreEqual(table[3].For(level), SwitchbackController.NoseFollowFor(table, g, false, 4, level), "I4 without it");
                    for (int intensity = 1; intensity <= 3; intensity++)
                        Assert.AreEqual(table[intensity - 1].For(level), SwitchbackController.NoseFollowFor(table, g, true, intensity, level));
                    Assert.GreaterOrEqual(g.For(level), VesselTransformer.RotationFollowRate, "never slower than the fleet lag");
                    if (level > 1) Assert.GreaterOrEqual(g.For(level), g.For(level - 1), "a harder level follows at least as fast");
                }
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
