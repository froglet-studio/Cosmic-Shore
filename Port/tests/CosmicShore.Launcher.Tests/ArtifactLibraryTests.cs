using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Launcher;
using Xunit;

namespace CosmicShore.Launcher.Tests
{
    /// <summary>The VESSEL STUDIO page's artifact library: Docs/Artifacts/artifacts.json, and IMPORT FILE.</summary>
    public class ArtifactLibraryTests
    {
        const string Good = @"{ ""artifacts"": [
            { ""id"": ""vessel-studio"", ""title"": ""Vessel Studio"", ""url"": ""https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa"",
              ""dir"": ""Docs/Studios/VesselStudio"", ""entry"": ""index.html"", ""group"": ""Vessel Studio"", ""version"": ""v1"" },
            { ""id"": ""tool-a"", ""title"": ""Tool A"", ""url"": ""https://claude.ai/artifact/AAA"", ""dir"": ""Docs/Artifacts/tool-a"", ""entry"": ""index.html"", ""group"": ""Tools"" },
            { ""id"": ""report"", ""title"": ""Report"", ""url"": ""https://claude.ai/code/artifact/0123abcd-0123-0123-0123-0123456789ab"", ""dir"": ""Docs/Artifacts/report"", ""entry"": ""index.html"" },
          ] }";

        static string TempWorkspace()
        {
            var dir = Path.Combine(Path.GetTempPath(), "artifact-lib-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void Parse_SplitsTheVesselStudioFromTheOthersByGroup()
        {
            var lib = ArtifactLibrary.Parse(Good);
            Assert.Null(lib.Error);
            Assert.Equal("v1", lib.VesselStudio!.Version);
            var groups = lib.Others.ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToArray());
            Assert.Equal(new[] { "tool-a" }, groups["Tools"]);
            Assert.Equal(new[] { "report" }, groups["Artifacts"]);   // no group = Artifacts
        }

        [Fact]
        public void Parse_SkipsEntriesThatLeaveDocsOrAreNotArtifactLinks()
        {
            var lib = ArtifactLibrary.Parse(@"{ ""artifacts"": [
                { ""id"": ""a"", ""title"": ""A"", ""url"": ""https://claude.ai/artifact/A1"", ""dir"": ""../outside"", ""entry"": ""index.html"" },
                { ""id"": ""b"", ""title"": ""B"", ""url"": ""https://claude.ai/artifact/B1"", ""dir"": ""Docs/Artifacts/b"", ""entry"": ""../../x.html"" },
                { ""id"": ""c"", ""title"": ""C"", ""url"": ""https://evil.example/artifact/C1"", ""dir"": ""Docs/Artifacts/c"", ""entry"": ""index.html"" },
                { ""id"": ""Bad Id"", ""title"": ""D"", ""url"": ""https://claude.ai/artifact/D1"", ""dir"": ""Docs/Artifacts/d"", ""entry"": ""index.html"" },
                { ""id"": ""e"", ""title"": ""E"", ""url"": ""https://claude.ai/artifact/E1"", ""dir"": ""Docs/Artifacts/e/../../../x"", ""entry"": ""index.html"" },
                { ""id"": ""ok"", ""title"": ""Ok"", ""url"": ""https://claude.ai/artifact/OK1"", ""dir"": ""Docs/Artifacts/ok"", ""entry"": ""index.html"" } ] }");
            Assert.Equal(new[] { "ok" }, lib.Entries.Select(e => e.Id));
        }

        [Fact]
        public void Parse_BadJsonIsAnErrorNotACrash()
        {
            var lib = ArtifactLibrary.Parse("{ nope");
            Assert.NotNull(lib.Error);
            Assert.Empty(lib.Entries);
        }

        [Fact]
        public void ImportPage_AddsThenUpdatesInPlace()
        {
            var ws = TempWorkspace();
            try
            {
                var page = Path.Combine(ws, "download.html");
                File.WriteAllText(page, "<!doctype html><title>Ecology Board</title><p>one</p>");
                var id = ArtifactLibrary.ImportPage(ws, page, "https://claude.ai/artifact/Eco1", today: "2026-10-09");
                Assert.Equal("ecology-board", id);
                Assert.Contains("<p>one</p>", File.ReadAllText(Path.Combine(ws, "Docs/Artifacts/ecology-board/index.html")));

                File.WriteAllText(page, "<!doctype html><title>Ecology Board</title><p>two</p>");
                Assert.Equal("ecology-board", ArtifactLibrary.ImportPage(ws, page, "https://claude.ai/artifact/Eco1"));
                Assert.Contains("<p>two</p>", File.ReadAllText(Path.Combine(ws, "Docs/Artifacts/ecology-board/index.html")));

                var lib = ArtifactLibrary.Load(ws);
                var e = Assert.Single(lib.Entries);
                Assert.Equal("Ecology Board", e.Title);
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(ws, ArtifactLibrary.RelativePath)));
                var hash = doc.RootElement.GetProperty("artifacts")[0].GetProperty("files").GetProperty("index.html").GetString();
                Assert.Equal(64, hash!.Length);
            }
            finally { Directory.Delete(ws, true); }
        }

        [Fact]
        public void ImportPage_ATakenIdGetsTheLinkSuffixAndABadLinkIsRefused()
        {
            var ws = TempWorkspace();
            try
            {
                var page = Path.Combine(ws, "p.html");
                File.WriteAllText(page, "<title>Same Name</title>");
                Assert.Equal("same-name", ArtifactLibrary.ImportPage(ws, page, "https://claude.ai/artifact/One1"));
                Assert.Equal("same-name-two2", ArtifactLibrary.ImportPage(ws, page, "https://claude.ai/artifact/Two2"));
                Assert.Throws<ArgumentException>(() => ArtifactLibrary.ImportPage(ws, page, "https://example.com/artifact/x"));
                Assert.Equal(2, ArtifactLibrary.Load(ws).Entries.Count);
            }
            finally { Directory.Delete(ws, true); }
        }

        [Fact]
        public void TheRepositoryLibraryListsTheVesselStudioAndItsPageExists()
        {
            var root = AppContext.BaseDirectory;
            while (root != null && !File.Exists(Path.Combine(root, ArtifactLibrary.RelativePath))) root = Path.GetDirectoryName(root);
            Assert.NotNull(root);
            var lib = ArtifactLibrary.Load(root!);
            Assert.Null(lib.Error);
            var vs = lib.VesselStudio;
            Assert.NotNull(vs);
            Assert.True(File.Exists(ArtifactLibrary.PagePath(root!, vs!)));
            Assert.Equal(StudioCatalog.Load(root!).Web, vs!.Url);   // the studio catalog and the library name the same artifact
        }

        [Fact]
        public void ImportPrompt_UsesTheSkillAndLeavesCommittingToTheGitPage()
        {
            var p = ArtifactLibrary.ImportPrompt("https://claude.ai/artifact/X1 ", "x");
            Assert.StartsWith("/amoebius-artifact https://claude.ai/artifact/X1\n", p);
            Assert.Contains("GIT page", p);
        }
    }
}
