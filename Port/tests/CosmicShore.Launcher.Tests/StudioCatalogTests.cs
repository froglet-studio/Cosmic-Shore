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
        public void AppWindowArgs_OpenThePageAsAnAppThatKnowsItIsInPrisma()
        {
            string page = Path.Combine(Path.GetTempPath(), "VesselStudio", "stoat.html"), profile = Path.Combine(Path.GetTempPath(), "studio-window");
            var args = StudioCatalog.AppWindowArgs(page, profile);
            var app = args.Single(a => a.StartsWith("--app="));
            Assert.StartsWith("--app=file:///", app);
            Assert.EndsWith("stoat.html#prisma", app);
            Assert.Contains("--user-data-dir=" + profile, args);
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
