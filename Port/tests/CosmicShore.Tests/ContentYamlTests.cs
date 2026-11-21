using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Tests
{
    /// <summary>Arc E: the Unity YAML reader + guid database over the REAL project.</summary>
    public class ContentYamlTests
    {
        internal static readonly string ProjectRoot = AssetDatabase.FindProjectRoot();
        static AssetDatabase s_db;
        internal static AssetDatabase Db => s_db ??= new AssetDatabase(ProjectRoot);

        [Fact]
        public void SameIndentSequencesAndWrappedFlowMaps()
        {
            var docs = UnityYaml.ParseDocuments(
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1001 &5 stripped\nPrefabInstance:\n  m_Modification:\n    m_Modifications:\n    - target: {fileID: -12, guid: abc,\n        type: 3}\n      propertyPath: m_Name\n      value: Hello world\n    m_RemovedComponents: []\n  m_Text: \"a\\nb\n    c\"\n");
            var d = Assert.Single(docs);
            Assert.Equal(1001, d.ClassId);
            Assert.Equal(5, d.FileId);
            Assert.True(d.Stripped);
            Assert.Equal("PrefabInstance", d.TypeName);
            var mod = d.Body["m_Modification"]["m_Modifications"].Items.Single();
            var target = ObjRef.From(mod["target"]);
            Assert.Equal(-12, target.FileId);
            Assert.Equal("abc", target.Guid);
            Assert.Equal(3, target.Type);
            Assert.Equal("Hello world", mod.Str("value"));
            Assert.Empty(d.Body["m_Modification"]["m_RemovedComponents"].Items);
            Assert.Equal("a\nb c", d.Body.Str("m_Text"));
        }

        [Fact]
        public void MenuMainParsesWithEveryDocument()
        {
            if (ProjectRoot == null) return; // Port checked out without the Unity project
            var file = Db.LoadPath("Assets/_Scenes/Menu_Main.unity");
            Assert.NotNull(file);
            string text = File.ReadAllText(file.Path);
            int headers = text.Split('\n').Count(l => l.StartsWith("--- !u!"));
            Assert.Equal(headers, file.Documents.Count);
            Assert.All(file.Documents, d => Assert.NotNull(d.TypeName));
        }
    }
}
