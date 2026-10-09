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
