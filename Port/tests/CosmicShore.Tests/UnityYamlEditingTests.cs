using System;
using System.IO;
using System.Linq;
using CosmicShore.Content.Editing;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The authoring path: a Unity YAML file opened with <see cref="UnityYamlFile"/>, edited, and
    /// written back. The property everything rests on is that an untouched file — or an untouched
    /// PART of an edited one — comes back byte for byte, so the port and the Unity Editor can
    /// share the project's files without either reformatting the other's.
    /// </summary>
    public class UnityYamlEditingTests
    {
        static readonly string[] YamlExtensions =
            { ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController", ".mixer", ".physicMaterial", ".lighting", ".playable" };

        const string Header = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";

        [Fact]
        public void EveryProjectYamlFileRoundTripsByteForByte()
        {
            if (ContentYamlTests.ProjectRoot == null) return; // Port checked out without the Unity project
            var assets = Path.Combine(ContentYamlTests.ProjectRoot, "Assets");
            int checkedFiles = 0;
            var lossy = new System.Collections.Generic.List<string>();
            var unreadable = new System.Collections.Generic.List<string>();
            foreach (var f in Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories)
                                       .Where(f => YamlExtensions.Contains(Path.GetExtension(f))))
            {
                string text = File.ReadAllText(f);
                if (!text.StartsWith("%YAML", StringComparison.Ordinal)) continue;
                checkedFiles++;
                var file = UnityYamlFile.Parse(text);
                if (file.Write() != text) lossy.Add(f);
                // A full canonical re-emit must at least read back as the same content.
                if (!UnityYamlFile.SameContent(file, UnityYamlFile.Parse(file.Write(canonical: true)))) unreadable.Add(f);
            }
            Assert.True(checkedFiles > 1000, $"only {checkedFiles} YAML files found");
            Assert.Empty(lossy);
            Assert.Empty(unreadable);
        }

        [Fact]
        public void EditingOneFieldChangesOnlyItsLine()
        {
            string text = Header +
                "--- !u!1 &10\nGameObject:\n  m_ObjectHideFlags: 0\n  m_Component:\n  - component: {fileID: 11}\n  m_Name: Ship\n  m_IsActive: 1\n" +
                "--- !u!4 &11\nTransform:\n  m_GameObject: {fileID: 10}\n  m_LocalPosition: {x: 0, y: 0,\n    z: 0}\n  m_Children: []\n  m_Father: {fileID: 0}\n";
            var file = UnityYamlFile.Parse(text);
            var ed = new UnityAssetEditor(file);
            ed.Set(10, "m_Name", "Mothership");
            string written = file.Write();
            Assert.Equal(text.Replace("m_Name: Ship", "m_Name: Mothership"), written);
        }

        [Fact]
        public void CreatingThenDeletingAnObjectRestoresTheExactFile()
        {
            string text = Header +
                "--- !u!1 &10\nGameObject:\n  m_Component:\n  - component: {fileID: 11}\n  m_Name: Root\n" +
                "--- !u!4 &11\nTransform:\n  m_GameObject: {fileID: 10}\n  m_Children: []\n  m_Father: {fileID: 0}\n" +
                "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n  - {fileID: 11}\n";
            var file = UnityYamlFile.Parse(text);
            var ed = new UnityAssetEditor(file, seed: 7);
            long child = ed.CreateGameObject("Child", parentGameObject: 10);
            long root = ed.CreateGameObject("Second Root");
            string grown = file.Write();
            Assert.Contains("  m_Children:\n  - {fileID: ", grown);  // a filled list is a block list, as Unity writes it
            Assert.Contains("m_Name: Child", grown);
            Assert.True(UnityYamlFile.SameContent(file, UnityYamlFile.Parse(grown)));
            // SceneRoots stays the last document; new objects land in fileID order before it.
            Assert.Equal(UnityAssetEditor.SceneRootsClass, file.Documents[^1].ClassId);

            ed.DeleteGameObject(child);
            ed.DeleteGameObject(root);
            Assert.Equal(text, file.Write());
        }

        [Fact]
        public void DeletingAnObjectTakesItsHierarchyAndClearsReferencesToIt()
        {
            string text = Header +
                "--- !u!1 &10\nGameObject:\n  m_Component:\n  - component: {fileID: 11}\n  m_Name: Root\n" +
                "--- !u!4 &11\nTransform:\n  m_GameObject: {fileID: 10}\n  m_Children:\n  - {fileID: 21}\n  m_Father: {fileID: 0}\n" +
                "--- !u!1 &20\nGameObject:\n  m_Component:\n  - component: {fileID: 21}\n  - component: {fileID: 22}\n  m_Name: Panel\n" +
                "--- !u!4 &21\nTransform:\n  m_GameObject: {fileID: 20}\n  m_Children:\n  - {fileID: 31}\n  m_Father: {fileID: 11}\n" +
                "--- !u!114 &22\nMonoBehaviour:\n  m_GameObject: {fileID: 20}\n  target: {fileID: 31}\n" +
                "--- !u!1 &30\nGameObject:\n  m_Component:\n  - component: {fileID: 31}\n  m_Name: Button\n" +
                "--- !u!4 &31\nTransform:\n  m_GameObject: {fileID: 30}\n  m_Children: []\n  m_Father: {fileID: 21}\n" +
                "--- !u!114 &40\nMonoBehaviour:\n  m_GameObject: {fileID: 10}\n  panel: {fileID: 20}\n  prefab: {fileID: 20, guid: 0123456789abcdef0123456789abcdef, type: 3}\n";
            var file = UnityYamlFile.Parse(text);
            var ed = new UnityAssetEditor(file);
            var r = ed.DeleteGameObject(ed.FindGameObject("Root/Panel"));

            Assert.Equal(new long[] { 20, 21, 22, 30, 31 }, r.Removed.OrderBy(x => x));
            Assert.Equal(new long[] { 10, 11, 40 }, file.Documents.Select(d => d.FileId));
            string written = file.Write();
            Assert.Contains("--- !u!4 &11\nTransform:\n  m_GameObject: {fileID: 10}\n  m_Children: []\n", written);
            Assert.Contains("  panel: {fileID: 0}\n", written);                                         // a dangling local reference is cleared...
            Assert.Contains("  prefab: {fileID: 20, guid: 0123456789abcdef0123456789abcdef, type: 3}", written); // ...a reference into another asset is not
            Assert.Single(r.ClearedReferences);
        }

        [Theory]
        // Unity breaks a flow mapping once the column after a ',' is past 80…
        [InlineData("  m_CorrespondingSourceObject: {fileID: 100100000, guid: 3c58c4dd53a6a2240acd7f7b81a3adb5, type: 3}",
                    "  m_CorrespondingSourceObject: {fileID: 100100000, guid: 3c58c4dd53a6a2240acd7f7b81a3adb5,\n    type: 3}")]
        // …and leaves it alone when that column is still 80.
        [InlineData("  m_sharedMaterial: {fileID: 21, guid: 6ab8eca0e6e2b7c4a8a495d9afae2053, type: 2}",
                    "  m_sharedMaterial: {fileID: 21, guid: 6ab8eca0e6e2b7c4a8a495d9afae2053, type: 2}")]
        // A value ending in ']' is quoted; a KEY ending in ']' is not.
        [InlineData("  propertyPath: 'm_Materials.Array.data[0]'", "  propertyPath: 'm_Materials.Array.data[0]'")]
        [InlineData("  m_MeshMetrics[0]: 1", "  m_MeshMetrics[0]: 1")]
        // ": " forces single quotes; outside ASCII forces double quotes with escapes.
        [InlineData("  m_text: 'Cost: 1'", "  m_text: 'Cost: 1'")]
        [InlineData("  label: \"Connecting\\u2026\"", "  label: \"Connecting\\u2026\"")]
        // An empty value keeps Unity's trailing space.
        [InlineData("  m_Name: ", "  m_Name: ")]
        public void CanonicalOutputMatchesUnity(string bodyLine, string expected)
        {
            string text = Header + "--- !u!114 &1\nMonoBehaviour:\n" + bodyLine + "\n";
            var file = UnityYamlFile.Parse(text);
            Assert.Equal(Header + "--- !u!114 &1\nMonoBehaviour:\n" + expected + "\n", file.Write(canonical: true));
        }

        [Fact]
        public void LongPlainScalarsWrapAtTheFirstSpaceFromColumn80()
        {
            string value = string.Join(" ", Enumerable.Repeat("word", 30));
            var file = UnityYamlFile.Parse(Header + "--- !u!114 &1\nMonoBehaviour:\n  m_Text: x\n");
            new UnityAssetEditor(file).Set(1, "m_Text", value);
            var lines = file.Write().Split('\n').SkipWhile(l => !l.StartsWith("  m_Text:")).TakeWhile(l => l.Length > 0).ToList();
            Assert.True(lines.Count > 1);
            Assert.All(lines.Skip(1), l => Assert.StartsWith("    word", l));
            Assert.All(lines.Take(lines.Count - 1), l => Assert.InRange(l.Length, 80, 84));
            Assert.Equal(value, UnityYaml.ParseDocuments(file.Write())[0].Body.Str("m_Text"));
        }

        [Fact]
        public void NestedBlockSequencesRoundTrip()
        {
            string text = Header + "--- !u!114 &1\nMonoBehaviour:\n  m_PhysicsShape:\n  - - {x: 0, y: 1}\n    - {x: 2, y: 3}\n  - - {x: 4, y: 5}\n";
            var file = UnityYamlFile.Parse(text);
            Assert.Equal(text, file.Write());
            Assert.Equal(text, file.Write(canonical: true));
            var shape = (YSeq)file.Documents[0].Body["m_PhysicsShape"];
            Assert.Equal(2, shape.List.Count);
            Assert.Equal("3", shape.List[0].Items[1].Str("y"));
        }
    }
}
