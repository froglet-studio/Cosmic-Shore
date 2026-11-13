using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Honeyguide's vessel pass (2026-11-13, Crew/HONEYGUIDE.md "Element scaling and level 5"): Charge scales Flit's
    /// strike radius, Mass the hive's reach, Space Peck's burst, Time the Slip's speed, and the three open level-5 slots are
    /// filled: Relentless (Charge), Hive Chain (Mass), Slipstream (Time). Lab numbers stay the base.
    /// </summary>
    public class HoneyguideFundamentalsTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/Honeyguide/HoneyguideConfig.asset";

        static HoneyguideConfigSO Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<HoneyguideConfigSO>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            return config;
        }

        static object Raw(object host, string name) =>
            host.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);

        [TestCase("flitStrikeScale", Element.Charge, 2f)]
        [TestCase("hiveReachScale", Element.Mass, 2f)]
        [TestCase("peckBurstScale", Element.Space, 2f)]
        [TestCase("slipSpeedScale", Element.Time, 1.5f)]
        public void EveryElementScalesOneNumber(string field, Element element, float atFull)
        {
            var f = (ElementalFloat)Raw(Config(), field);
            Assert.IsTrue(f.Enabled, field + " is switched off");
            Assert.AreEqual(element, f.ScaledElement, field);
            Assert.AreEqual(1f, f.EvaluateAtNormalizedLevel(0f), 1e-5f, field + " at rest");
            Assert.AreEqual(atFull, f.EvaluateAtNormalizedLevel(1f), 1e-5f, field + " at level 10");
        }

        [TestCase("relentlessUpgradeElement", Element.Charge)]
        [TestCase("hiveChainUpgradeElement", Element.Mass)]
        [TestCase("slipstreamUpgradeElement", Element.Time)]
        public void EveryUpgradeIsGatedOnItsElement(string field, Element element) =>
            Assert.AreEqual(element, (Element)Raw(Config(), field));

        [Test]
        public void OffAVesselEveryNumberIsTheLabsOwn()
        {
            var c = Config();
            Assert.AreEqual(c.FlitStrikeRadius, c.FlitStrikeRadiusFor(null), 1e-5f);
            Assert.AreEqual(c.HiveReach, c.HiveReachFor(null), 1e-5f);
            Assert.AreEqual(c.PeckBurst, c.PeckBurstFor(null));
            Assert.AreEqual(c.ZipSpeed, c.ZipSpeedFor(null), 1e-5f);
            Assert.IsFalse(c.RelentlessActive(null));
            Assert.IsFalse(c.HiveChainActive(null));
            Assert.IsFalse(c.SlipstreamActive(null));
            Assert.AreEqual(2f, c.SlipstreamExitScale, 1e-5f);
        }
    }
}
