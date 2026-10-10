#if UNITY_EDITOR
using System.IO;
using CosmicShore.Editor.Studios;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Unity Vessel Studio home reads the hub's own files (<c>Docs/Studios/VesselStudio/</c>): the catalog, the
    /// game's domain colours and the fleet list, and draws each studio's preview. If a parse drifts, the home shows
    /// the wrong studios or colours, so these read the real files. (The artifact's NUMBERS reach the game through
    /// <c>Tools/Build/studio_to_unity.py</c>, held by its own <c>--self-test</c>.)
    /// </summary>
    public class VesselStudioWindowTests
    {
        // ---- the home page reads the hub's own files ----

        [Test]
        public void HomeReadsTheGamesDomainColours()
        {
            var d = VesselStudioWindow.ParseDomains(File.ReadAllText("Docs/Studios/VesselStudio/studio-domains.js"));
            CollectionAssert.IsSupersetOf(d.Keys, new[] { "jade", "ruby", "gold", "blue" });
            Assert.AreEqual(0x13 / 255f, d["jade"].r, 1e-3f, "studio-domains.js is generated from the palette asset; re-run domain_colors.py if this moved");
        }

        [Test]
        public void HomeReadsTheHubsFleetList()
        {
            var fleet = VesselStudioWindow.ParseFleet(File.ReadAllText("Docs/Studios/VesselStudio/index.html"));
            CollectionAssert.Contains(fleet, "Manta");
            CollectionAssert.DoesNotContain(fleet, "Squirrel", "a vessel with a studio is a card, not a 'no studio yet' chip");
            CollectionAssert.IsEmpty(VesselStudioWindow.ParseFleet("<p>no fleet here</p>"));
        }

        [Test]
        public void EveryCatalogStudioHasItsPageAndAName()
        {
            var cat = UnityEngine.JsonUtility.FromJson<VesselStudioWindow.Catalog>(File.ReadAllText("Docs/Studios/VesselStudio/studios.json"));
            Assert.IsNotEmpty(cat.studios);
            Assert.IsTrue(File.Exists("Docs/Studios/VesselStudio/" + cat.hub), "the hub page");
            foreach (var s in cat.studios)
            {
                Assert.IsFalse(string.IsNullOrEmpty(s.name), s.id);
                Assert.IsTrue(File.Exists("Docs/Studios/VesselStudio/" + s.file), $"{s.id}: {s.file}");
            }
        }

        [Test]
        public void EveryPreviewDrawsSomethingOnTheNight()
        {
            StudioPreviewCanvas.Rgba Domain(string k) => StudioPreviewCanvas.Rgba.Hex("#13fff2");
            foreach (var id in new[] { "squirrel", "stoat", "a-studio-with-no-preview-yet" })
            {
                var c = new StudioPreviewCanvas(320, 150, 0.5f);
                StudioPreviews.Draw(id, c, 1.7f, Domain, StudioPreviewCanvas.Rgba.Hex("#ffb347"));
                int lit = 0;
                for (int i = 0; i < c.Pixels.Length; i += 4)
                    if (c.Pixels[i] + c.Pixels[i + 1] + c.Pixels[i + 2] > 120) lit++;
                Assert.Greater(lit, 50, id);   // measured 1 790+ (squirrel), 1 218+ (stoat), 129+ (generic)
            }
        }
    }
}
#endif
