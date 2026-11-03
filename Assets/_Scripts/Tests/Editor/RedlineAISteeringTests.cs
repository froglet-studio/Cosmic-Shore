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
    }
}
#endif
