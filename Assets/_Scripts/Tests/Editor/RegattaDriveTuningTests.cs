using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-mode drive tuning (Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §9): a mode's learned numbers are a
    /// VARIANT the hull's own policy falls back from, so learning Regatta never retunes a hull in any other mode.
    /// </summary>
    public class RegattaDriveTuningTests
    {
        [Test]
        public void ForMode_WithoutAVariant_ReturnsThePolicyItself()
        {
            var policy = ScriptableObject.CreateInstance<RampBoostAIPolicySO>();
            policy.name = "NoSuchPolicyForTests";
            try
            {
                Assert.AreSame(policy, AIBoostPolicySO.ForMode(policy, GameModes.Regatta),
                    "no Resources/AIBoostPolicies/<name>_Regatta: the hull flies its own policy");
            }
            finally
            {
                Object.DestroyImmediate(policy);
            }
        }

        [Test]
        public void ForMode_NullPolicy_StaysNull()
        {
            Assert.IsNull(AIBoostPolicySO.ForMode(null, GameModes.Regatta));
        }

        [Test]
        public void AIModeTuning_WithoutAVariant_ReturnsTheAssetItself()
        {
            // The Stoat's executors read their autopilot numbers through this; with no StoatSlingConfig_Regatta (or any
            // other <name>_<mode> copy) the AI flies the hull's own config.
            var asset = ScriptableObject.CreateInstance<StoatSlingConfigSO>();
            asset.name = "NoSuchStoatConfigForTests";
            try
            {
                Assert.AreSame(asset, AIModeTuning.For(asset, GameModes.Regatta));
                Assert.IsNull(AIModeTuning.For<StoatSlingConfigSO>(null, GameModes.Regatta));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MantaTriggers_FollowTheBands()
        {
            // Inside the boost band both triggers are flat; past the yaw band the turn's side rises above the other.
            MantaAnalogTurnBoostExecutor.AutopilotTriggers(0.1f, 0.1f, 0.5f, 0.8f, out float lt, out float rt);
            Assert.AreEqual(1f, lt, 1e-5f);
            Assert.AreEqual(1f, rt, 1e-5f);
            MantaAnalogTurnBoostExecutor.AutopilotTriggers(0.9f, 0.9f, 0.5f, 0.8f, out lt, out rt);
            Assert.Less(lt, rt, "a hard right yaw pivots on the right trigger");
            Assert.AreEqual(0.2f, lt, 1e-4f, "hold eases off linearly past the boost band: (1 - 0.9) / (1 - 0.5)");
        }
    }
}
