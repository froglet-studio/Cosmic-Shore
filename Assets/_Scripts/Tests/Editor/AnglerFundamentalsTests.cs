using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Angler's fundamentals pass (2026-11-12, R_VesselActions/ANGLER.md §1n): every element scales a number - Charge the
    /// bite rate, Mass the lure's size, Space the paint brush's width, Time the tamed fauna's speed - and the two open level-5
    /// slots are filled: Charge 5 Swallow (bites steal) and Time 5 Riptide (a dash pressed mid-dash is kept).
    /// </summary>
    public class AnglerFundamentalsTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/Angler/AnglerConfig.asset";

        static AnglerConfigSO Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<AnglerConfigSO>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            return config;
        }

        static ElementalFloat Field(object host, string name) =>
            (ElementalFloat)host.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);

        static object Raw(object host, string name) =>
            host.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);

        [TestCase("biteRate", Element.Charge, 2.5f)]
        [TestCase("lureScale", Element.Mass, 2f)]
        [TestCase("brushScale", Element.Space, 2f)]
        [TestCase("faunaSpeed", Element.Time, 2f)]
        public void EveryElementScalesOneNumber(string field, Element element, float atFull)
        {
            var f = Field(Config(), field);
            Assert.IsTrue(f.Enabled, field + " is switched off");
            Assert.AreEqual(element, f.ScaledElement, field);
            Assert.AreEqual(1f, f.EvaluateAtNormalizedLevel(0f), 1e-5f, field + " at rest");
            Assert.AreEqual(atFull, f.EvaluateAtNormalizedLevel(1f), 1e-5f, field + " at level 10");
            Assert.AreEqual(1f, f.EvaluateAtNormalizedLevel(-0.5f), 1e-5f, field + " never shrinks below its authored number");
        }

        [Test]
        public void TheUpgradesAreGatedOnTheirElements()
        {
            var config = Config();
            Assert.AreEqual(Element.Charge, (Element)Raw(config, "swallowUpgradeElement"));
            Assert.AreEqual(Element.Time, (Element)Raw(config, "riptideUpgradeElement"));
            Assert.IsFalse(config.SwallowActive(null));
            Assert.IsFalse(config.RiptideActive(null));
        }

        [Test]
        public void OffAVesselEveryNumberIsTheLabsOwn()
        {
            var config = Config();
            Assert.AreEqual(config.LockBiteCooldown, config.BiteCooldownFor(null), 1e-5f);
            Assert.AreEqual(1f, config.LureScaleFor(null), 1e-5f);
            Assert.AreEqual(1f, config.BrushScaleFor(null), 1e-5f);
        }

        [Test]
        public void TheChargeCardCarriesTheSwallowPetal()
        {
            const string variant = "Assets/_Prefabs/UI Elements/VesselHUD/AnglerHUDVariant.prefab";
            string yaml = System.IO.File.ReadAllText(variant);
            StringAssert.Contains("lockPetalIcon: {fileID: ", yaml);
            StringAssert.DoesNotContain("lockPetalIcon: {fileID: 0}", yaml, "the Swallow petal is unbound");
        }

        [Test]
        public void AtFullChargeAHunterBitesEveryFourTenthsOfASecond()
        {
            var config = Config();
            float rate = Field(config, "biteRate").EvaluateAtNormalizedLevel(1f);
            Assert.AreEqual(0.4f, config.LockBiteCooldown / rate, 1e-4f);
        }
    }
}
