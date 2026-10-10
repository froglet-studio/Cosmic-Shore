#if UNITY_EDITOR
using System.IO;
using CosmicShore.Editor.Studios;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Unity Vessel Studio window's STUDIO column reads the web studio's own numbers
    /// (<c>Docs/Studios/StoatFlightStudio.html</c>, the <c>SHIPPED</c> block). If the parse drifts, every "studio"
    /// button beside a slider offers a wrong number, so this reads the real file.
    /// </summary>
    public class VesselStudioWindowTests
    {
        [Test]
        public void ParsesTheWebStudiosShippedNumbers()
        {
            string html = File.ReadAllText("Docs/Studios/StoatFlightStudio.html");
            var p = VesselStudioWindow.ParseShipped(html);
            Assert.AreEqual(250f, p["ftAhead"], 1e-4f);
            Assert.AreEqual(6f, p["ftStrength"], 1e-4f);
            Assert.AreEqual(200f, p["ftSepMax"], 1e-4f);
            Assert.AreEqual(0.5f, p["dpKeySqueeze"], 1e-4f);
            Assert.AreEqual(1f, p["aiWarpQ"], 1e-4f, "the first occurrence in SHIPPED, not a play style's override");
        }

        [Test]
        public void ParseIgnoresEverythingOutsideShipped()
        {
            var p = VesselStudioWindow.ParseShipped("const X = { a: 1 };\n  const SHIPPED = {\n    b: 2.5, c: -3,\n  };\n  const Y = { d: 4 };");
            Assert.AreEqual(2, p.Count);
            Assert.AreEqual(2.5f, p["b"], 1e-6f);
            Assert.AreEqual(-3f, p["c"], 1e-6f);
        }
    }
}
#endif
