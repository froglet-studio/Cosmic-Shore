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
        /// Medium 3, Hard 6, Legendary 9 per second at every intensity, each faster than the fleet lag and a harder level
        /// never slower than the one below.</summary>
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
                    CollectionAssert.AreEqual(new[] { 2f, 3f, 6f, 9f }, new[] { row.easy, row.medium, row.hard, row.legendary },
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
    }
}
#endif
