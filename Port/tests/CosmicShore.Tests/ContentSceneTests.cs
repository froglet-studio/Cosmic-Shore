using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests
{
    /// <summary>Arc E: prefab expansion + scene instantiation over the REAL project.</summary>
    public class ContentSceneTests
    {
        [Fact]
        public void ModificationPathsApplyToNestedFieldsAndArrays()
        {
            var body = (YMap)UnityYaml.ParseSingle("m_Colors:\n  m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\nm_Materials:\n- {fileID: 1}\n");
            PrefabGraphTestHooks.Apply(body, "m_Colors.m_NormalColor.r", "0.25", null, default);
            PrefabGraphTestHooks.Apply(body, "m_Materials.Array.size", "2", null, default);
            PrefabGraphTestHooks.Apply(body, "m_Materials.Array.data[1]", "", YMap.Ref(7, "abc", 2), new ObjRef(7, "abc", 2));
            Assert.Equal("0.25", body["m_Colors"]["m_NormalColor"].Str("r"));
            Assert.Equal(2, body["m_Materials"].Items.Count);
            Assert.Equal(7, ObjRef.From(body["m_Materials"].Items[1]).FileId);
        }

        [Fact]
        public void MenuMainInstantiatesItsUiTree()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var db = ContentYamlTests.Db;
            var scripts = new ScriptTypeMap(db, new[] { typeof(GameObject).Assembly, typeof(CosmicShore.Utility.GameDataSO).Assembly });
            var loader = new AssetLoader(db, scripts);
            using var loop = new GameLoop("Menu_Main");
            var graph = PrefabGraph.Build(db, db.LoadPath("Assets/_Scenes/Menu_Main.unity"));
            Assert.Empty(graph.Warnings);
            var scene = new SceneInstantiator(loader, new InstantiateOptions
            {
                IncludeScript = t => t.Namespace?.StartsWith("CosmicShore.Engine") == true,
            }).Instantiate(graph);

            Assert.Contains(scene.Roots, r => r.name == "UI_Refactored" && r.GetComponent<Canvas>() != null);
            Assert.True(scene.ById.Values.OfType<Image>().Count() > 1000);
            Assert.True(scene.ById.Values.OfType<TMP_Text>().Count() > 300);
            // The holder never leaks into the scene.
            Assert.DoesNotContain(loop.Scene.GetRootGameObjects(), g => g.name.StartsWith("__content"));
            // Prefab-instance override landed: the AvatarStrip instance was renamed + resized.
            var strip = scene.ById.Values.OfType<GameObject>().First(g => g.name == "AvatarStrip");
            var rt = (RectTransform)strip.transform;
            Assert.Equal(240.00002f, rt.sizeDelta.x, 3);
            Assert.Equal(-74.4f, rt.anchoredPosition.y, 3);
        }
    }
}
