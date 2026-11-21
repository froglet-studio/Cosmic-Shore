using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Swordfish's own Time card, QUICKDRAW (2026-11-12, R_VesselActions/SWORDFISH.md § "Fundamentals pass"): Time is
    /// the sword's rate - it divides the energize hold, the energize cooldown and the swipe recovery - and its level-5,
    /// FLURRY, takes the swipe recovery away. The Rhino keeps Ramp Spool; the Swordfish's ramp is unscaled.
    /// </summary>
    public class SwordfishQuickdrawTests
    {
        const string Dir = "Assets/_SO_Assets/VesselActions/Rhino/";

        static ElementalFloat Field(object host, string name) =>
            (ElementalFloat)host.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);

        [Test]
        public void TimeRaisesTheSwordsRateTwoAndAHalfTimes()
        {
            var sword = AssetDatabase.LoadAssetAtPath<ShieldSkimmerScaleConfigSO>(Dir + "ShieldSkimmerScaleConfig.asset");
            var swipe = AssetDatabase.LoadAssetAtPath<SwordfishShieldSwipeConfigSO>(Dir + "SwordfishShieldSwipeConfig.asset");
            Assert.IsNotNull(sword); Assert.IsNotNull(swipe);
            foreach (var f in new[] { Field(sword, "timeSwordRateMultiplier"), Field(swipe, "timeSwipeRateMultiplier") })
            {
                Assert.AreEqual(Element.Time, f.ScaledElement);
                Assert.AreEqual(1f, f.EvaluateAtNormalizedLevel(0f), 1e-5f);
                Assert.AreEqual(2.5f, f.EvaluateAtNormalizedLevel(1f), 1e-5f);
                Assert.AreEqual(0.5f, f.EvaluateAtNormalizedLevel(-0.5f), 1e-5f, "the deficit floor");
            }
        }

        [Test]
        public void OffAVesselTheSwordKeepsItsAuthoredTimings()
        {
            var sword = AssetDatabase.LoadAssetAtPath<ShieldSkimmerScaleConfigSO>(Dir + "ShieldSkimmerScaleConfig.asset");
            var swipe = AssetDatabase.LoadAssetAtPath<SwordfishShieldSwipeConfigSO>(Dir + "SwordfishShieldSwipeConfig.asset");
            Assert.AreEqual(sword.EnergizeCooldownSeconds, sword.EnergizeCooldownSecondsFor(null), 1e-5f);
            Assert.AreEqual(sword.EnergizeHoldSecondsFor(false), sword.EnergizeHoldSecondsFor(false, null), 1e-5f);
            Assert.AreEqual(swipe.SwipeCooldownSeconds, swipe.SwipeCooldownSecondsFor(null), 1e-5f);
            Assert.IsFalse(swipe.FlurryActive(null));
        }

        [Test]
        public void TheSwordfishRampIsNotTimeScaled()
        {
            var ramp = AssetDatabase.LoadAssetAtPath<RampBoostActionSO>(Dir + "SwordfishRampBoostAction.asset");
            Assert.IsNotNull(ramp, "SwordfishRampBoostAction.asset is missing");
            Assert.IsFalse(Field(ramp, "timeAccelerationMultiplier").Enabled);
            Assert.AreEqual(1f, ramp.TimeAccelerationMultiplier(null), 1e-5f);
        }
    }
}
