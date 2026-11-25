using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Shrike's vessel pass (2026-11-13, SHRIKE.md "Level-5 upgrades"): the three open level-5 slots are filled -
    /// Salvo (Charge: nest flora launch twice as often while you paint), Armed Nests (Mass: a left-behind nest carries
    /// two missiles) and Whipcrack (Time: the sling reaches its anchor in 0.6 s). Off a vessel every number is the base.
    /// </summary>
    public class ShrikeFundamentalsTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/Shrike/ShrikeConfig.asset";

        static ShrikeConfigSO Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShrikeConfigSO>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            return config;
        }

        static object Raw(object host, string name) =>
            host.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);

        [TestCase("salvoUpgradeElement", Element.Charge)]
        [TestCase("armedNestsUpgradeElement", Element.Mass)]
        [TestCase("whipcrackUpgradeElement", Element.Time)]
        public void EveryUpgradeIsGatedOnItsElement(string field, Element element) =>
            Assert.AreEqual(element, (Element)Raw(Config(), field));

        [Test]
        public void EveryUpgradeIsStrongerThanTheBase()
        {
            var c = Config();
            Assert.Greater(c.SalvoLaunchScale, 1f);
            Assert.Greater(c.ArmedNestMissiles, c.DetachNestMissiles);
            Assert.Less(c.WhipcrackSeconds, c.SlingSeconds);
        }

        [Test]
        public void OffAVesselEveryNumberIsTheBase()
        {
            var c = Config();
            Assert.AreEqual(c.MissileEvery, c.MissileEveryFor(null), 1e-5f);
            Assert.AreEqual(c.DetachNestMissiles, c.NestMissilesFor(null));
            Assert.AreEqual(c.SlingSeconds, c.SlingSecondsFor(null), 1e-5f);
            Assert.IsFalse(c.SalvoActive(null));
            Assert.IsFalse(c.ArmedNestsActive(null));
            Assert.IsFalse(c.WhipcrackActive(null));
        }
    }
}
