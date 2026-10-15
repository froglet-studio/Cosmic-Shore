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

        /// <summary>The home's cards draw studios.json's card model (/vessel-studio D35): the same buttons in the same order as Amoebius.</summary>
        [Test]
        public void CardButtons_AreTheCatalogsInOrder_EveryOneInItsPlace()
        {
            const string json = "{\"mirror\":\"https://m/\",\"cardActions\":[" +
                "{\"id\":\"open\",\"label\":\"Open studio\",\"on\":\"web,amoebius,unity\"}," +
                "{\"id\":\"engine\",\"label\":\"PLAY IN ENGINE\",\"on\":\"amoebius,unity\",\"needs\":\"engineMode\"}," +
                "{\"id\":\"live\",\"label\":\"OPEN LIVE IN BROWSER\",\"on\":\"amoebius,unity\",\"needs\":\"mirror\"}]," +
                "\"studios\":[{\"id\":\"a\",\"name\":\"A\",\"file\":\"a.html\",\"engineMode\":\"SkimRace\"},{\"id\":\"b\",\"name\":\"B\",\"file\":\"b.html\"}]}";
            var cat = UnityEngine.JsonUtility.FromJson<VesselStudioWindow.Catalog>(json);
            var a = VesselStudioWindow.CardButtons(cat, cat.studios[0]);
            CollectionAssert.AreEqual(new[] { "open", "engine", "live" }, a.ConvertAll(x => x.Id));
            CollectionAssert.AreEqual(new[] { true, true, true }, a.ConvertAll(x => x.Enabled));
            var b = VesselStudioWindow.CardButtons(cat, cat.studios[1]);
            CollectionAssert.AreEqual(new[] { true, false, true }, b.ConvertAll(x => x.Enabled));
            StringAssert.Contains("no game mode", b[1].Tip);
        }

        [Test]
        public void TheShippedCatalogHasTheHubsCards()
        {
            var cat = UnityEngine.JsonUtility.FromJson<VesselStudioWindow.Catalog>(File.ReadAllText("Docs/Studios/VesselStudio/studios.json"));
            if (cat.cardActions == null || cat.cardActions.Length == 0) Assert.Ignore("a branch from before the card fields (D35)");
            foreach (var s in cat.studios)
            {
                Assert.IsFalse(string.IsNullOrEmpty(s.chip), s.id + ": chip");
                Assert.IsNotEmpty(s.spec, s.id + ": spec");
                Assert.IsTrue(File.Exists("Docs/Studios/VesselStudio/" + s.preview), s.id + ": " + s.preview);
            }
            CollectionAssert.AreEqual(VesselStudioWindow.ParseFleet(File.ReadAllText("Docs/Studios/VesselStudio/index.html")), cat.fleet);
            // the hub's "+ New studio" card (D36/D37) is in the catalog, so the home draws it with the hub's text
            if (File.ReadAllText("Docs/Studios/VesselStudio/index.html").Contains("class=\"bay add\""))
            {
                Assert.IsFalse(string.IsNullOrEmpty(cat.newCard?.name), "studios.json newCard");
                StringAssert.IsMatch("^[A-Za-z0-9-]{1,40}$", cat.newCard.fragment);
                StringAssert.Contains(cat.newCard.fragment, File.ReadAllText("Docs/Studios/VesselStudio/studio-new.js"));
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
