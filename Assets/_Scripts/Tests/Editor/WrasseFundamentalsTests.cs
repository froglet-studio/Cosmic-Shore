using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Wrasse's vessel pass (2026-11-13, Crew/KITE_NAVIGATOR.md "Element scaling and level 5"): Charge scales the kite's
    /// ram power, Mass the cluster, Space the blast, and the open Time 5 slot is Tailwind (saved boosts last longer). The
    /// four level-5 gates read their own element fields on KiteNavigatorConfigSO.
    /// </summary>
    public class WrasseFundamentalsTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset";

        static KiteNavigatorConfigSO Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<KiteNavigatorConfigSO>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            return config;
        }

        static object Raw(object host, string name) =>
            host.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);

        [TestCase("chargePetalScale", Element.Charge)]
        [TestCase("massClusterScale", Element.Mass)]
        [TestCase("spaceReachScale", Element.Space)]
        public void EachElementDoublesItsNumber(string field, Element element)
        {
            var f = (ElementalFloat)Raw(Config(), field);
            Assert.IsTrue(f.Enabled, field + " is switched off");
            Assert.AreEqual(element, f.ScaledElement, field);
            Assert.AreEqual(1f, f.EvaluateAtNormalizedLevel(0f), 1e-5f, field + " at rest");
            Assert.AreEqual(2f, f.EvaluateAtNormalizedLevel(1f), 1e-5f, field + " at level 10");
        }

        [TestCase("crystalStealUpgradeElement", Element.Charge)]
        [TestCase("shieldedClustersUpgradeElement", Element.Mass)]
        [TestCase("shieldBreakerUpgradeElement", Element.Space)]
        [TestCase("tailwindUpgradeElement", Element.Time)]
        public void EveryUpgradeIsGatedOnItsElement(string field, Element element) =>
            Assert.AreEqual(element, (Element)Raw(Config(), field));

        [Test]
        public void OffAVesselEveryNumberIsTheLabsOwn()
        {
            var c = Config();
            Assert.AreEqual(c.ChargePetals, c.ChargePetalsFor(null), 1e-5f);
            Assert.AreEqual(c.ChargeCap, c.ChargeCapFor(null), 1e-5f);
            Assert.AreEqual(c.MassPrisms, c.MassPrismsFor(null), 1e-5f);
            Assert.AreEqual(c.MassPrismCap, c.MassPrismCapFor(null), 1e-5f);
            Assert.AreEqual(c.SpaceMaxScale, c.SpaceMaxScaleFor(null), 1e-5f);
            Assert.AreEqual(c.SpaceMaxScaleCap, c.SpaceMaxScaleCapFor(null), 1e-5f);
            Assert.AreEqual(c.TimeBoostSeconds, c.BoostSecondsFor(null), 1e-5f, "no Tailwind off a vessel");
            Assert.IsFalse(c.CrystalStealActive(null));
            Assert.IsFalse(c.TailwindActive(null));
            Assert.AreEqual(1.5f, c.TailwindBoostScale, 1e-5f);
        }
    }
}
