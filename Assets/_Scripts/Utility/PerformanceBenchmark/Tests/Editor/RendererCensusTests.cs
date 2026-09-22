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
    }
}
