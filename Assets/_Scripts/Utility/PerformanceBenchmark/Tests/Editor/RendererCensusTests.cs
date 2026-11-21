using System.Collections.Generic;
using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    /// <summary>
    /// The census itself needs a live scene; its ranking does not, and the ranking is the part a
    /// reader acts on — "which material owns the culling population" is answered by the first row.
    /// </summary>
    [TestFixture]
    public class RendererCensusTests
    {
        [Test]
        public void Top_OrdersLargestFirst_AndFormatsNameEqualsCount()
        {
            var groups = new Dictionary<string, int> { ["Snow"] = 4189, ["Spindle"] = 41000, ["Crystal"] = 1080 };
            Assert.AreEqual(new[] { "Spindle=41000", "Snow=4189", "Crystal=1080" }, RendererCensus.Top(groups, 8));
        }

        [Test]
        public void Top_TruncatesToCount()
        {
            var groups = new Dictionary<string, int> { ["a"] = 3, ["b"] = 2, ["c"] = 1 };
            Assert.AreEqual(new[] { "a=3", "b=2" }, RendererCensus.Top(groups, 2));
        }

        [Test]
        public void Top_BreaksTiesByName_SoTwoRunsPrintTheSameTable()
        {
            var groups = new Dictionary<string, int> { ["zeta"] = 5, ["alpha"] = 5, ["mid"] = 5 };
            Assert.AreEqual(new[] { "alpha=5", "mid=5", "zeta=5" }, RendererCensus.Top(groups, 3));
        }

        [Test]
        public void Top_EmptyOrNonPositiveCount_ReturnsEmpty()
        {
            Assert.AreEqual(0, RendererCensus.Top(new Dictionary<string, int>(), 8).Length);
            Assert.AreEqual(0, RendererCensus.Top(new Dictionary<string, int> { ["a"] = 1 }, 0).Length);
            Assert.AreEqual(0, RendererCensus.Top(new Dictionary<string, int> { ["a"] = 1 }, -3).Length);
        }

        [Test]
        public void HideSwitch_MatchesPrefix_CaseInsensitive_AcrossPhaseVariants()
        {
            Assert.IsTrue(RendererHideSwitch.Matches("SpindleMaterial_Phase3", "Spindle"));
            Assert.IsTrue(RendererHideSwitch.Matches("SpindleMaterial_Phase0", "spindle"));
            Assert.IsTrue(RendererHideSwitch.Matches("SpindleMaterial", "SpindleMaterial"));
        }

        [Test]
        public void HideSwitch_NeverMatchesOnEmptyInput_OrAMidNameHit()
        {
            // An empty prefix must not mean "hide everything" — that is a blank screen, not an A/B.
            Assert.IsFalse(RendererHideSwitch.Matches("SpindleMaterial_Phase3", ""));
            Assert.IsFalse(RendererHideSwitch.Matches("SpindleMaterial_Phase3", null));
            Assert.IsFalse(RendererHideSwitch.Matches(null, "Spindle"));
            // Prefix, not substring: a snow shard carrying "Spindle" mid-name stays lit.
            Assert.IsFalse(RendererHideSwitch.Matches("GyroidSpindle", "Spindle"));
            // A bare star is "hide everything" too.
            Assert.IsFalse(RendererHideSwitch.Matches("SpindleMaterial_Phase3", "*"));
        }

        /// <summary>
        /// The lattice species' spindles wear their own materials (§47.7), so the family test in a
        /// grown Lattice cell needs a substring. NEGATIVE CONTROL: the same names against the plain
        /// prefix must NOT match - that is the 5-of-68,000 miss this form exists to fix.
        /// </summary>
        [TestCase("GyroidSpindleMaterial_Phase3")]
        [TestCase("AssemblySpindleMaterial_Phase0")]
        [TestCase("QuasicrystalSpindleMaterial_Phase7")]
        [TestCase("SpindleMaterial_Phase5")]
        [TestCase("quadfishspindlematerial")]
        public void HideSwitch_LeadingStar_MatchesAnywhereInTheName(string materialName)
        {
            Assert.IsTrue(RendererHideSwitch.Matches(materialName, "*Spindle"));
            if (!materialName.StartsWith("Spindle"))
                Assert.IsFalse(RendererHideSwitch.Matches(materialName, "Spindle"), "the plain prefix must still miss it");
        }

        [Test]
        public void HideSwitch_LeadingStar_StillMissesOtherFamilies()
        {
            Assert.IsFalse(RendererHideSwitch.Matches("SnowMaterial", "*Spindle"));
            Assert.IsFalse(RendererHideSwitch.Matches(null, "*Spindle"));
        }

        [Test]
        public void HideSwitch_Describe_ShowsTheShapeOfTheMatch()
        {
            Assert.AreEqual("Spindle*", RendererHideSwitch.Describe("Spindle"));
            Assert.AreEqual("*Spindle*", RendererHideSwitch.Describe("*Spindle"));
        }
    }
}
