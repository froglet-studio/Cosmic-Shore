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

        // AIBoostContext.PathDirection (2026-10-25): on a race line "straight" is judged on the path, the runway on the ring.
        [Test]
        public void Context_OnAPath_JudgesTheAngleOnThePath_AndTheRunwayOnTheRing()
        {
            var toRing = new Vector3(300f, 0f, 600f);   // the ring round the bend, ~27 deg off the course
            var c = new AIBoostContext(toRing, Vector3.forward, Vector3.forward, 400f, 0f, false, false, false,
                toPath: new Vector3(5f, 0f, 200f));
            Assert.Less(c.AngleToTarget, 2f, "the hull is flying straight along its line");
            Assert.AreEqual(toRing.magnitude, c.Distance, 1e-3f, "the runway is still the distance to the ring");
            Assert.AreEqual(toRing.magnitude / 400f, c.RunwaySeconds, 1e-4f);
        }

        [Test]
        public void Context_WithoutAPath_JudgesTheObjective()
        {
            // Negative control: every mode that is not on a race line (no toPath) reads exactly as before.
            var toRing = new Vector3(300f, 0f, 600f);
            var c = new AIBoostContext(toRing, Vector3.forward, Vector3.forward, 400f, 0f, false, false, false);
            Assert.AreEqual(Vector3.Angle(Vector3.forward, toRing), c.AngleToTarget, 1e-3f);
            Assert.AreEqual(toRing, c.PathDirection);
        }

        [Test]
        public void RampPolicy_OnTheLine_SeesAStraight_WhereTheRingAloneDidNot()
        {
            var policy = ScriptableObject.CreateInstance<RampBoostAIPolicySO>();
            try
            {
                var toRing = new Vector3(300f, 0f, 600f);
                var byRing = new AIBoostContext(toRing, Vector3.forward, Vector3.forward, 400f, 0.05f, false, false, false);
                var byPath = new AIBoostContext(toRing, Vector3.forward, Vector3.forward, 400f, 0.05f, false, false, false,
                    toPath: new Vector3(5f, 0f, 200f));
                Assert.IsFalse(policy.IsStraight(byRing), "judged by the ring round the bend: never straight (the bug)");
                Assert.IsTrue(policy.IsStraight(byPath), "judged by the line it is flying: straight");
            }
            finally { Object.DestroyImmediate(policy); }
        }

        [Test]
        public void ForMode_NullPolicy_StaysNull()
        {
            Assert.IsNull(AIBoostPolicySO.ForMode(null, GameModes.Regatta));
            Assert.IsNull(AIBoostPolicySO.ForMode(null, GameModes.Switchback, 4));
        }

        // Per-intensity variants (Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md §3.6): Switchback's I4 drive is learned from
        // its I4 races, and an intensity with none of its own flies the mode's variant.
        [Test]
        public void ForMode_AnIntensityWithItsOwnVariant_FliesIt_ElseTheModes()
        {
            var policy = ScriptableObject.CreateInstance<ChargeBoostAIPolicySO>();
            policy.name = "DolphinAIBoostPolicy";
            try
            {
                var i4 = AIBoostPolicySO.ForMode(policy, GameModes.Switchback, 4);
                Assert.AreEqual("DolphinAIBoostPolicy_Switchback_I4", i4.name);
                Assert.AreEqual("DolphinAIBoostPolicy_Switchback", AIBoostPolicySO.ForMode(policy, GameModes.Switchback, 3).name,
                    "no I3 races learned yet: the mode's variant");
                Assert.AreEqual("DolphinAIBoostPolicy_Switchback", AIBoostPolicySO.ForMode(policy, GameModes.Switchback, 0).name,
                    "no intensity: the mode's variant");
                Assert.AreSame(policy, AIBoostPolicySO.ForMode(policy, GameModes.Headlong, 4),
                    "negative control: a mode without variants flies the hull's own policy at any intensity");
            }
            finally { Object.DestroyImmediate(policy); }
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

        [Test]
        public void MantaRegattaTuning_LoadsWithThePivotBandAboveTheBoostBand()
        {
            // Resources/AIBoostPolicies/MantaSoarAITuning_Regatta (2026-10-17, from manta_drive_sim.py; a person's
            // Regatta flights replace it through learn_drives_from_flights.py --write).
            var tuning = MantaSoarAITuningSO.LoadForMode(GameModes.Regatta);
            Assert.IsNotNull(tuning, "the Regatta Manta flies its own bands");
            Assert.Greater(tuning.YastriStickBand, tuning.BoostStickBand, "the pivot starts past the boost band");
            Assert.IsNull(MantaSoarAITuningSO.LoadForMode(GameModes.Rampage), "every other mode keeps the executor's bands");
        }
    }
}
