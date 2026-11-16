using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Orca's level-5 upgrades (the fundamentals pass, Garrett 2026-11-12, Crew/ORCA_CREW.md "Level 5"): Charge 5 Rearm,
    /// Mass 5 Bulwark, Space 5 Chain Burst, Time 5 Afterburner, each gated on its own element, and the map names all four.
    /// </summary>
    public class OrcaUpgradeTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/OrcaCrew/OrcaCrewConfig.asset";
        const string MapPath = "Assets/Resources/ElementalAbilityMaps/Orca.asset";

        static OrcaCrewConfigSO Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<OrcaCrewConfigSO>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            return config;
        }

        static object Raw(object host, string name) =>
            host.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);

        [TestCase("rearmUpgradeElement", Element.Charge)]
        [TestCase("bulwarkUpgradeElement", Element.Mass)]
        [TestCase("chainUpgradeElement", Element.Space)]
        [TestCase("afterburnerUpgradeElement", Element.Time)]
        public void EachUpgradeIsGatedOnItsOwnElement(string field, Element element) =>
            Assert.AreEqual(element, (Element)Raw(Config(), field), field);

        [Test]
        public void OffAVesselNoUpgradeIsActive()
        {
            var config = Config();
            Assert.IsFalse(config.RearmActive(null));
            Assert.IsFalse(config.BulwarkActive(null));
            Assert.IsFalse(config.ChainBurstActive(null));
            Assert.IsFalse(config.AfterburnerActive(null));
            Assert.Greater(config.RearmSeconds, 0f);
            Assert.Greater(config.BulwarkGap, 0f);
        }

        [Test]
        public void TheMapNamesEveryUpgrade()
        {
            var map = AssetDatabase.LoadAssetAtPath<ElementalAbilityMapSO>(MapPath);
            Assert.IsNotNull(map, MapPath + " is missing");
            string yaml = System.IO.File.ReadAllText(MapPath);
            foreach (var label in new[] { "Rearm", "Bulwark", "Chain Burst", "Afterburner" })
                StringAssert.Contains("UpgradeLabel: '" + label + "'", yaml);
        }
    }
}
