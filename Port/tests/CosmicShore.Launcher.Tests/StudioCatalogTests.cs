using System.IO;
using System.Linq;
using CosmicShore.Launcher;
using Xunit;

namespace CosmicShore.Launcher.Tests
{
    /// <summary>The STUDIOS page's catalog: Docs/Studios/VesselStudio/studios.json.</summary>
    public class StudioCatalogTests
    {
        const string Good = @"{
          ""web"": ""https://claude.ai/artifact/x"",
          ""studios"": [
            { ""id"": ""squirrel"", ""name"": ""Squirrel"", ""file"": ""squirrel.html"", ""kind"": ""built"", ""summary"": ""racer"", ""docs"": ""Docs/a.md"" },
            { ""id"": ""stoat"", ""name"": ""Stoat"", ""file"": ""stoat.html"" },
          ]
        }";

        [Fact]
        public void Parse_ReadsEveryStudioAndTheWebLink()
        {
            var c = StudioCatalog.Parse(Good);
            Assert.Null(c.Error);
            Assert.Equal("https://claude.ai/artifact/x", c.Web);
            Assert.Equal("index.html", c.Hub);
            Assert.Equal(new[] { "squirrel", "stoat" }, c.Studios.Select(s => s.Id));
            Assert.Equal("Docs/a.md", c.Studios[0].Docs);
            Assert.Null(c.Studios[1].Docs);
            Assert.Equal("", c.Studios[1].Summary);
        }

        [Fact]
        public void Parse_ReadsTheLiveMirrorAndBuildsEachPagesLink()
        {
            var c = StudioCatalog.Parse(@"{ ""mirror"": ""https://example.github.io/vessel-studio"", ""studios"": [
                { ""id"": ""stoat"", ""name"": ""Stoat"", ""file"": ""stoat.html"" } ] }");
            Assert.Equal("https://example.github.io/vessel-studio", c.Mirror);
            Assert.Equal("https://example.github.io/vessel-studio/", c.MirrorUrl(c.Hub));          // the hub is the mirror's root
            Assert.Equal("https://example.github.io/vessel-studio/stoat.html", c.MirrorUrl("stoat.html"));
            Assert.Equal("https://example.github.io/vessel-studio/squirrel.html",
                StudioCatalog.Parse(@"{ ""mirror"": ""https://example.github.io/vessel-studio/"" }").MirrorUrl("squirrel.html"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(@"""""")]
        [InlineData(@"""file:///C:/studio/index.html""")]
        [InlineData(@"""javascript:alert(1)""")]
        [InlineData(@"""not a url""")]
        [InlineData("42")]
        public void Parse_NoUsableMirrorMeansNoLiveLink(string? mirror)
        {
            var c = StudioCatalog.Parse(mirror == null ? "{}" : @"{ ""mirror"": " + mirror + " }");
            Assert.Null(c.Error);
            Assert.Null(c.Mirror);
            Assert.Null(c.MirrorUrl("stoat.html"));
        }

        [Fact]
        public void Parse_SkipsIncompleteEntriesAndPagesOutsideTheFolder()
        {
            var c = StudioCatalog.Parse(@"{ ""studios"": [
                { ""id"": ""a"", ""name"": ""A"" },
                { ""id"": ""b"", ""name"": ""B"", ""file"": ""../../secret.html"" },
                { ""id"": ""c"", ""name"": ""C"", ""file"": ""/etc/passwd"" },
                { ""id"": ""d"", ""name"": ""D"", ""file"": ""d.html"" } ] }");
            Assert.Equal(new[] { "d" }, c.Studios.Select(s => s.Id));
        }

        [Fact]
        public void Parse_BadJsonIsAnErrorNotACrash()
        {
            var c = StudioCatalog.Parse("{ not json");
            Assert.NotNull(c.Error);
            Assert.Empty(c.Studios);
        }

        [Fact]
        public void Load_NoCatalogOnTheBranchSaysSo()
        {
            var dir = Path.Combine(Path.GetTempPath(), "studio-catalog-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var c = StudioCatalog.Load(dir);
                Assert.Contains("studios.json", c.Error);
                Assert.Empty(c.Studios);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void TheShippedCatalogParsesAndEveryPageExists()
        {
            // the repository's own catalog, found from the test's working directory upward
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, StudioCatalog.RelativeDir))) d = d.Parent;
            if (d == null) return;   // a checkout without the studio folder has nothing to check
            var c = StudioCatalog.Load(d.FullName);
            Assert.Null(c.Error);
            Assert.NotEmpty(c.Studios);
            foreach (var s in c.Studios) Assert.True(File.Exists(StudioCatalog.PagePath(d.FullName, s.File)), s.File);
            Assert.True(File.Exists(StudioCatalog.PagePath(d.FullName, c.Hub)));
            if (c.Mirror != null) Assert.StartsWith("https://", c.Mirror);   // the live mirror is served over https (pages fetch build.json)
        }

        [Fact]
        public void Defaults_MatchTheRepositoryCatalog()
        {
            // the page's always-on web links (no workspace yet) must be the catalog's own
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, StudioCatalog.RelativeDir))) d = d.Parent;
            if (d == null) return;
            var c = StudioCatalog.Load(d.FullName);
            Assert.Equal(c.Web, StudioCatalog.DefaultWeb);
            Assert.Equal(c.Mirror, StudioCatalog.DefaultMirror);
        }

        [Fact]
        public void Targets_AreTheHubThenEachVessel_AndPickFallsBackToTheHub()
        {
            var c = StudioCatalog.Parse(Good);
            var t = c.Targets();
            Assert.Equal(new[] { "hub", "squirrel", "stoat" }, t.Select(x => x.Key));   // one picker: ALL STUDIOS (the whole studio), then the vessels
            Assert.True(t[0].IsHub);
            Assert.Equal(c.Hub, t[0].File);
            Assert.Equal(StudioCatalog.PlanDoc, t[0].Docs);                               // the hub's DOCS is the plan
            Assert.Equal("Docs/a.md", t[1].Docs);
            Assert.Equal("stoat", c.Pick("stoat").Key);
            Assert.True(c.Pick("gone").IsHub);                                            // a vessel removed from the catalog
            Assert.True(c.Pick(null).IsHub);                                              // opens on the whole Vessel Studio
            Assert.True(new StudioCatalog().Pick(null).IsHub);                            // no catalog: the hub alone, never empty
            Assert.Contains(StudioCatalog.PlanDoc, StudioCatalog.AgentPrompt(t[0]));
            Assert.Contains("squirrel.html", StudioCatalog.AgentPrompt(t[1]));
        }

        [Fact]
        public void WebLinksWork_WithNoCatalog()
        {
            var none = new StudioCatalog { Error = "no checkout" };
            Assert.Equal(StudioCatalog.DefaultWeb, none.WebLink);
            Assert.Equal(StudioCatalog.DefaultMirror, none.LiveUrl(none.Hub));
            Assert.Equal(StudioCatalog.DefaultMirror + "stoat.html", none.LiveUrl("stoat.html"));
            var own = StudioCatalog.Parse(@"{ ""web"": ""https://claude.ai/artifact/x"", ""mirror"": ""https://e.github.io/vs"" }");
            Assert.Equal("https://claude.ai/artifact/x", own.WebLink);   // the catalog wins over the defaults
            Assert.Equal("https://e.github.io/vs/stoat.html", own.LiveUrl("stoat.html"));
        }

        [Fact]
        public void PickRoot_ReadsTheUnityCheckoutUntilTheWorkspaceHasTheStudio()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "pickroot-" + System.Guid.NewGuid().ToString("N"));
            string ws = Path.Combine(tmp, "ws"), clone = Path.Combine(tmp, "clone");
            void Catalog(string dir) { Directory.CreateDirectory(Path.Combine(dir, StudioCatalog.RelativeDir)); File.WriteAllText(Path.Combine(dir, StudioCatalog.RelativeDir, StudioCatalog.FileName), "{}"); }
            try
            {
                Directory.CreateDirectory(ws);
                Assert.Null(StudioCatalog.PickRoot(ws, false, null));                   // nothing at all: no root, the web links still work
                Catalog(clone);
                Assert.Equal(clone, StudioCatalog.PickRoot(ws, false, clone));          // no workspace yet: the Unity checkout (the user's case)
                Assert.Equal(clone, StudioCatalog.PickRoot(ws, true, clone));           // a workspace on a branch without the studio
                Catalog(ws);
                Assert.Equal(ws, StudioCatalog.PickRoot(ws, true, clone));              // the workspace once it carries the studio
                Assert.Equal(ws, StudioCatalog.PickRoot(ws, true, null));
                // started from Unity (--clone): Unity's checkout and branch win over Amoebius's workspace (D33)
                Assert.Equal(clone, StudioCatalog.PickRoot(ws, true, clone, preferClone: true));
                Assert.Equal(ws, StudioCatalog.PickRoot(ws, true, null, preferClone: true));
            }
            finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
        }

        [Fact]
        public void Parse_ReadsTheEngineModeAndPlayInEngineAddsArcade()
        {
            var c = StudioCatalog.Parse(@"{ ""studios"": [
                { ""id"": ""stoat"", ""name"": ""Stoat"", ""file"": ""stoat.html"", ""engineMode"": ""Slingshot"", ""engineNote"": ""the game's own Stoat"" },
                { ""id"": ""squirrel"", ""name"": ""Squirrel"", ""file"": ""squirrel.html"" } ] }");
            Assert.Equal("Slingshot", c.Studios[0].EngineMode);
            Assert.Equal("the game's own Stoat", c.Studios[0].EngineNote);
            Assert.Equal(new[] { "--arcade", "Slingshot" }, StudioCatalog.EngineArgs(c.Studios[0]));
            Assert.Null(c.Studios[1].EngineMode);
            Assert.Empty(StudioCatalog.EngineArgs(c.Studios[1]));
        }

        [Fact]
        public void AppWindowArgs_OpenTheServedStudioAsAnApp_NeverAFile()
        {
            string url = "http://127.0.0.1:5123/abc/stoat.html#amoebius", profile = Path.Combine(Path.GetTempPath(), "studio-window");
            var args = StudioCatalog.AppWindowArgs(url, profile);
            Assert.Equal("--app=" + url, args.Single(a => a.StartsWith("--app=")));
            Assert.Contains("--user-data-dir=" + profile, args);
            Assert.DoesNotContain(args, a => a.Contains("file://"));
        }

        [Fact]
        public void Find_TakesAKeyOrAPageFile_ElseTheHub()
        {
            var c = StudioCatalog.Parse(Good);
            Assert.Equal("stoat", c.Find("STOAT").Key);
            Assert.Equal("stoat", c.Find("stoat.html").Key);
            Assert.True(c.Find("index.html").IsHub);
            Assert.True(c.Find("hub").IsHub);
            Assert.True(c.Find("nope").IsHub);
            Assert.True(c.Find(null).IsHub);
        }

        [Fact]
        public void PageUri_IsAPlainFileUri_ForTheOtherArtifactsOnly()
        {
            var u = StudioCatalog.PageUri(Path.Combine(Path.GetTempPath(), "Docs", "Artifacts", "x", "index.html"));
            Assert.StartsWith("file:///", u);
            Assert.DoesNotContain("#", u);   // no host flag: the Vessel Studio is served (D33), never opened as a file
        }

        [Fact]
        public void TheShippedCatalogsEngineModesAreGameModes()
        {
            // PLAY IN ENGINE passes --arcade MODE, which the player parses as a GameModes member: a typo would open no card
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, StudioCatalog.RelativeDir))) d = d.Parent;
            if (d == null) return;
            var enumFile = Path.Combine(d.FullName, "Assets", "_Scripts", "Data", "Enums", "GameModes.cs");
            if (!File.Exists(enumFile)) return;
            string src = File.ReadAllText(enumFile);
            foreach (var s in StudioCatalog.Load(d.FullName).Studios)
                if (s.EngineMode != null)
                    Assert.True(System.Text.RegularExpressions.Regex.IsMatch(src, @"\b" + s.EngineMode + @"\s*="), s.Id + ": " + s.EngineMode + " is not a GameModes member");
        }

        const string Cards = @"{
          ""mirror"": ""https://example.org/vs/"",
          ""lede"": ""Pick a vessel."",
          ""cardActions"": [
            { ""id"": ""open"", ""label"": ""Open studio \u2192"", ""on"": ""web,amoebius,unity"" },
            { ""id"": ""engine"", ""label"": ""PLAY IN ENGINE"", ""on"": ""amoebius,unity"", ""needs"": ""engineMode"" },
            { ""id"": ""tune"", ""label"": ""TUNE IN UNITY"", ""on"": ""unity"", ""needs"": ""tuner"" },
            { ""id"": ""live"", ""label"": ""OPEN LIVE IN BROWSER"", ""on"": ""amoebius,unity"", ""needs"": ""mirror"" }
          ],
          ""studios"": [
            { ""id"": ""a"", ""name"": ""Alpha"", ""file"": ""a.html"", ""kind"": ""built"", ""chip"": ""built \u00b7 round 1"", ""summary"": ""s"",
              ""spec"": [ { ""k"": ""Cruise"", ""v"": ""60 u/s"" }, { ""k"": ""bad"" } ], ""preview"": ""previews/a.png"", ""accent"": ""jade"", ""engineMode"": ""SkimRace"" },
            { ""id"": ""b"", ""name"": ""Beta"", ""file"": ""b.html"", ""kind"": ""design"", ""tuner"": true }
          ],
          ""fleet"": [ ""Manta"", ""Rhino"" ],
          ""fleetNote"": ""No studio yet.""
        }";

        [Fact]
        public void Parse_ReadsTheHubsCard()
        {
            var c = StudioCatalog.Parse(Cards);
            Assert.Equal("Pick a vessel.", c.Lede);
            Assert.Equal(new[] { "open", "engine", "tune", "live" }, c.CardActions.Select(a => a.Id));
            Assert.Equal("Open studio →", c.CardActions[0].Label);
            Assert.Equal(new[] { "Manta", "Rhino" }, c.Fleet);
            Assert.Equal("No studio yet.", c.FleetNote);
            var a = c.Studios[0];
            Assert.Equal("built · round 1", a.Chip);
            Assert.Equal(new StudioCatalog.SpecRow("Cruise", "60 u/s"), Assert.Single(a.Spec));   // a row without its value is skipped
            Assert.Equal("previews/a.png", a.Preview);
            Assert.Equal("jade", a.Accent);
            Assert.False(a.Tuner);
            Assert.Equal("design", c.Studios[1].Chip);   // no chip: the kind
            Assert.True(c.Studios[1].Tuner);
        }

        [Fact]
        public void Parse_WithoutCards_GivesTheD34Buttons()
        {
            var c = StudioCatalog.Parse(Good);
            Assert.Equal(new[] { "open", "engine", "tune", "live" }, c.CardActions.Select(a => a.Id));
            Assert.Empty(c.Fleet);
        }

        [Fact]
        public void Applies_KeepsEveryButton_AndSaysWhyNot()
        {
            var c = StudioCatalog.Parse(Cards);
            var (a, b) = (c.Studios[0], c.Studios[1]);
            var act = c.CardActions.ToDictionary(x => x.Id);
            Assert.True(c.Applies(act["open"], a, "amoebius").ok);
            Assert.True(c.Applies(act["engine"], a, "amoebius").ok);
            var noMode = c.Applies(act["engine"], b, "amoebius");
            Assert.False(noMode.ok); Assert.Contains("no game mode", noMode.why);
            var tune = c.Applies(act["tune"], b, "amoebius");
            Assert.False(tune.ok); Assert.Contains("Unity", tune.why);   // Unity only: in its place, disabled, saying where
            Assert.True(c.Applies(act["tune"], b, "unity").ok);
            Assert.False(c.Applies(act["tune"], a, "unity").ok);         // no tuner for this vessel
            Assert.True(c.Applies(act["live"], a, "amoebius").ok);
            Assert.False(StudioCatalog.Parse(Good).Applies(act["live"], a, "amoebius").ok);   // no mirror
            Assert.False(c.Applies(act["engine"], a, "web").ok);
        }

        [Fact]
        public void ParseDomains_ReadsTheGeneratedColours()
        {
            var d = StudioCatalog.ParseDomains("[{ key: 'jade', name: 'Jade', color: '#13fff2' }, { key: 'ruby', color: '#ff2e63' }]");
            Assert.Equal("#13fff2", d["jade"]);
            Assert.Equal("#ff2e63", d["ruby"]);
        }

        [Fact]
        public void PreviewPath_StaysInTheStudioFolder()
        {
            var root = Path.Combine(Path.GetTempPath(), "vs-prev-" + System.Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(root, StudioCatalog.RelativeDir, "previews");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "a.png"), new byte[] { 1 });
            try
            {
                var s = StudioCatalog.Parse(Cards).Studios[0];
                Assert.Equal(Path.Combine(dir, "a.png"), StudioCatalog.PreviewPath(root, s));
                Assert.Null(StudioCatalog.PreviewPath(root, s with { Preview = "../../x.png" }));
                Assert.Null(StudioCatalog.PreviewPath(root, s with { Preview = "previews/none.png" }));
            }
            finally { Directory.Delete(root, true); }
        }

        /// <summary>
        /// The shipped catalog's cards ARE the hub's (D34): the same studios in the same order, with the hub card's name, chip,
        /// summary, spec rows and open label, every preview baked, and the fleet. The page draws only the catalog, so this
        /// holds it to the hub (parity_gate.py checks the same from Python).
        /// </summary>
        [Fact]
        public void TheShippedCatalogsCards_AreTheHubs()
        {
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, StudioCatalog.RelativeDir))) d = d.Parent;
            if (d == null) return;
            var dir = Path.Combine(d.FullName, StudioCatalog.RelativeDir);
            var json = File.ReadAllText(Path.Combine(dir, StudioCatalog.FileName));
            if (!json.Contains("\"cardActions\"")) return;   // a branch from before the card fields (/vessel-studio D34)
            var cat = StudioCatalog.Load(d.FullName);
            string html = File.ReadAllText(Path.Combine(dir, "index.html"));
            static string Text(string s) => System.Text.RegularExpressions.Regex.Replace(System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "")), @"\s+", " ").Trim();
            var bays = System.Text.RegularExpressions.Regex.Matches(html, @"<a class=""bay"" href=""([^""]+)""[^>]*>(.*?)</a>", System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.Equal(bays.Select(m => m.Groups[1].Value), cat.Studios.Select(s => s.File));
            foreach (var (m, s) in bays.Zip(cat.Studios))
            {
                string bay = m.Groups[2].Value;
                Assert.Equal(Text(System.Text.RegularExpressions.Regex.Match(bay, @"<div class=""name""><b>(.*?)</b>").Groups[1].Value), s.Name);
                Assert.Equal(Text(System.Text.RegularExpressions.Regex.Match(bay, @"<span class=""chip""[^>]*>(.*?)</span>").Groups[1].Value), s.Chip);
                Assert.Equal(Text(System.Text.RegularExpressions.Regex.Match(bay, @"<p>(.*?)</p>", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value), s.Summary);
                var spec = System.Text.RegularExpressions.Regex.Matches(System.Text.RegularExpressions.Regex.Match(bay, @"<div class=""spec"">(.*?)</div>").Groups[1].Value, @"<span>(.*?)</span><b>(.*?)</b>")
                    .Select(r => new StudioCatalog.SpecRow(Text(r.Groups[1].Value), Text(r.Groups[2].Value)));
                Assert.Equal(spec, s.Spec);
                Assert.Equal(Text(System.Text.RegularExpressions.Regex.Match(bay, @"<span class=""open"">(.*?)</span>").Groups[1].Value), cat.CardActions[0].Label);
                Assert.NotNull(StudioCatalog.PreviewPath(d.FullName, s));
            }
            var fleet = System.Text.RegularExpressions.Regex.Match(html, @"<div class=""fleet""[^>]*>(.*?)</div>", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
            Assert.Equal(System.Text.RegularExpressions.Regex.Matches(fleet, @"<span>([^<]+)</span>").Select(x => x.Groups[1].Value.Trim()), cat.Fleet);
        }

        [Fact]
        public void AgentPrompt_PointsAtThePlanAndTheStudio()
        {
            var s = StudioCatalog.Parse(Good).Studios[0];
            var p = StudioCatalog.AgentPrompt(s);
            Assert.Contains("VESSEL_STUDIO_PLAN.md", p);
            Assert.Contains("squirrel.html", p);
            Assert.Contains("Docs/a.md", p);
        }
    }
}
