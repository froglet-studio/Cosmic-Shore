#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Redline's AI steering (2026-11-03, the Manta's Time game, Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md section 13):
    /// the AI Mantas fly the Regatta gate steering. On the old steering they finished 4 of 12 offline races over
    /// I1-I4 and none at I3 or I4 (Tools/Build/manta_redline_sim.py); on this one, 12 of 12.
    /// </summary>
    public class RedlineAISteeringTests
    {
        [Test]
        public void Redline_FliesTheRegattaSteering_WhateverTheSceneSays()
        {
            var go = new GameObject("RedlineAISteeringTests");
            try
            {
                var redline = go.AddComponent<RedlineController>();   // MinigameRedline.unity leaves the switch off
                var prop = typeof(GateRaceController).GetProperty("AICommitFromApproachPoint",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(prop, "GateRaceController.AICommitFromApproachPoint is gone");
                Assert.IsTrue((bool)prop.GetValue(redline),
                    "the AI Mantas would line up on the axis point and circle after every missed mouth again");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The AI nose rate by difficulty (Docs/Reports/REDLINE_MANTA_NOSE.md section 4, 2026-11-08): Easy 2,
        /// Medium 3, Hard 6, Legendary 9 per second at I1-I2, each faster than the fleet lag and a harder level never
        /// slower than the one below. Since 2026-11-11 Easy is 2.5 at I3 and the I4 row 4 / 5 / 6 / 9: with Soar lit from
        /// the gun a 2/s nose could not hold the 72 / 44 u mouths (manta_redline_sim.py: 5 of 6 I3 and 2-4 of 6 I4 races
        /// finished; 6 of 6 at 2.5/s and 4/s).</summary>
        [Test]
        public void Redline_AINoseLag_TableByIntensityAndDifficulty()
        {
            var go = new GameObject("RedlineAISteeringTests_NoseLag");
            try
            {
                var field = typeof(RedlineController).GetField("aiNoseFollowByIntensity",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, "RedlineController.aiNoseFollowByIntensity is gone");
                var table = (SwitchbackNoseFollow[])field.GetValue(go.AddComponent<RedlineController>());
                Assert.AreEqual(4, table.Length, "one row per intensity");
                for (int intensity = 1; intensity <= 4; intensity++)
                {
                    var row = table[intensity - 1];
                    var want = intensity == 4 ? new[] { 4f, 5f, 6f, 9f }
                        : intensity == 3 ? new[] { 2.5f, 3f, 6f, 9f }
                        : new[] { 2f, 3f, 6f, 9f };
                    CollectionAssert.AreEqual(want, new[] { row.easy, row.medium, row.hard, row.legendary },
                        $"I{intensity}");
                    for (int level = 1; level <= 4; level++)
                    {
                        float rate = SwitchbackController.NoseFollowFor(table, intensity, level);
                        Assert.Greater(rate, VesselTransformer.RotationFollowRate, $"I{intensity} level {level}: faster than the fleet lag");
                        if (level > 1)
                            Assert.GreaterOrEqual(rate, SwitchbackController.NoseFollowFor(table, intensity, level - 1));
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The AI throttle floor by difficulty (2026-11-11): the skill throttle flew a Soaring Manta at a third
        /// of its speed at I1, so every seat flies at least 0.7, Hard and Legendary at full, and a harder seat never
        /// slower than an easier one. Any other level is the skill throttle (0).</summary>
        [Test]
        public void Redline_AIThrottleFloor_ByDifficulty()
        {
            var go = new GameObject("RedlineAISteeringTests_Throttle");
            try
            {
                var field = typeof(RedlineController).GetField("aiThrottleFloorByDifficulty",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, "RedlineController.aiThrottleFloorByDifficulty is gone");
                var table = (float[])field.GetValue(go.AddComponent<RedlineController>());
                CollectionAssert.AreEqual(new[] { 0.7f, 0.85f, 1f, 1f }, table);
                for (int level = 1; level <= 4; level++)
                {
                    Assert.AreEqual(table[level - 1], RedlineController.ThrottleFloorFor(table, level), 1e-6f);
                    if (level > 1)
                        Assert.GreaterOrEqual(RedlineController.ThrottleFloorFor(table, level),
                                              RedlineController.ThrottleFloorFor(table, level - 1));
                }
                Assert.AreEqual(0f, RedlineController.ThrottleFloorFor(table, 0), 0f, "no difficulty: the skill throttle");
                Assert.AreEqual(0f, RedlineController.ThrottleFloorFor(table, 5), 0f);
                Assert.AreEqual(0f, RedlineController.ThrottleFloorFor(null, 2), 0f);
                Assert.AreEqual(1f, RedlineController.ThrottleFloorFor(new[] { 3f }, 1), 0f, "clamped to a throttle");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The AI race line (2026-11-11): straight at the mouth it is flying, the next one as the leg after, the
        /// last gate its own "after", nothing once finished.</summary>
        [Test]
        public void Redline_AIRaceLine_AimsAtTheMouthItIsFlying()
        {
            Assert.IsTrue(RedlineController.RaceLinePoints(0, 64, out int ring, out int next));
            Assert.AreEqual(0, ring);
            Assert.AreEqual(1, next);
            Assert.IsTrue(RedlineController.RaceLinePoints(63, 64, out ring, out next));
            Assert.AreEqual(63, ring);
            Assert.AreEqual(63, next, "the finish is its own after");
            Assert.IsFalse(RedlineController.RaceLinePoints(64, 64, out _, out _), "finished");
            Assert.IsFalse(RedlineController.RaceLinePoints(-1, 64, out _, out _));

            var go = new GameObject("RedlineAISteeringTests_Line");
            try
            {
                var field = typeof(RedlineController).GetField("aiRaceLine", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, "RedlineController.aiRaceLine is gone");
                Assert.IsTrue((bool)field.GetValue(go.AddComponent<RedlineController>()), "the race line ships on");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif
