using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Jacks' element scaling moved from the lab's formulas (JacksMath) onto authored ElementalFloat endpoints on
    /// JacksConfigSO (2026-11-13, JACKS.md section 2), so it can be tuned in the inspector and read by
    /// element_ability_table.py. These tests hold the endpoints to the lab's curves across the whole -5..15 band.
    /// </summary>
    public class JacksScalingTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/Jacks/JacksConfig.asset";

        static JacksConfigSO Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<JacksConfigSO>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            return config;
        }

        static ElementalFloat Raw(JacksConfigSO c, string name) =>
            (ElementalFloat)typeof(JacksConfigSO).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);

        [TestCase("dangerShare", Element.Charge)]
        [TestCase("herdBirthRate", Element.Mass)]
        [TestCase("ringScale", Element.Space)]
        [TestCase("herdSpeed", Element.Time)]
        public void EveryElementScalesOneNumber(string field, Element element)
        {
            var f = Raw(Config(), field);
            Assert.IsTrue(f.Enabled, field + " is switched off");
            Assert.AreEqual(element, f.ScaledElement, field);
        }

        [Test]
        public void TheEndpointsAreTheLabsCurves()
        {
            var c = Config();
            for (float level = -5f; level <= 15f; level += 0.5f)
            {
                float t = level / 10f;
                Assert.AreEqual(JacksMath.DangerShare(level), UnityEngine.Mathf.Clamp01(Raw(c, "dangerShare").EvaluateAtNormalizedLevel(t)), 1e-4f, "Charge " + level);
                Assert.AreEqual(JacksMath.FeedsPerOffspringScale(level), 1f / Raw(c, "herdBirthRate").EvaluateAtNormalizedLevel(t), 1e-4f, "Mass " + level);
                Assert.AreEqual(JacksMath.Ring(1f, level), Raw(c, "ringScale").EvaluateAtNormalizedLevel(t), 1e-4f, "Space " + level);
                Assert.AreEqual(JacksMath.SpeedMultiplier(level), Raw(c, "herdSpeed").EvaluateAtNormalizedLevel(t), 1e-4f, "Time " + level);
            }
        }

        [Test]
        public void OffAVesselEveryNumberIsTheRestingOne()
        {
            var c = Config();
            Assert.AreEqual(JacksMath.DangerShare(0f), c.DangerShareFor(null), 1e-4f);
            Assert.AreEqual(JacksMath.FeedsPerOffspringScale(0f), c.FeedsPerOffspringScaleFor(null), 1e-4f);
            Assert.AreEqual(JacksMath.Ring(c.RingFloor, 0f), c.RingFor(null), 1e-3f);
            Assert.AreEqual(JacksMath.SpeedMultiplier(0f), c.HerdSpeedFor(null), 1e-4f);
        }
    }
}
