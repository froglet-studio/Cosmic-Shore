using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Content.Editing;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;
using CosmicShore.Engine.Events;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests
{
    // ── Fixtures: scripts as a project would write them ────────────────────
    public enum AuthoringMode { Off = 0, Slow = 2, Fast = 7 }

    [Serializable]
    public class AuthoringTuning
    {
        public float speed = 2.5f;
        [SerializeField] List<string> tags;
    }

    public class AuthoringBase : MonoBehaviour
    {
        [SerializeField] protected int baseCount = 3;
    }

    public class AuthoringProbe : AuthoringBase
    {
        public float radius = 0.5f;
        [SerializeField] string label = "probe";
        [SerializeField] Vector3 offset = new(1, 2, 3);
        [SerializeField] Color tint = Color.red;
        [SerializeField] Rect area = new(0, 0, 1, 1);
        [SerializeField] LayerMask mask;
        [SerializeField] AuthoringMode mode = AuthoringMode.Slow;
        [SerializeField] AuthoringMode[] modes = { AuthoringMode.Fast, AuthoringMode.Off };
        [SerializeField] bool[] flags = { true, false };
        [SerializeField] List<float> weights = new() { 0.25f };
        [SerializeField] AuthoringTuning tuning;              // null in C#, a default instance in YAML
        [SerializeField] GameObject target;
        [SerializeField] UnityEvent onFired;
        [field: SerializeField] public int Level { get; private set; } = 4;
        public static int s_static;
        public readonly int readOnly = 1;
        [NonSerialized] public int skipped;
        int _private;
        System.Action _delegate;
    }

    public class AuthoringGraphic : MaskableGraphic { [SerializeField] float thickness = 2; }
    public class AuthoringNetworked : NetworkBehaviour { [SerializeField] int ticks = 30; }

    /// <summary>
    /// "Add Component" without the Unity Editor: a project script's serialized defaults read off
    /// its C# type, built-in and package components from templates measured off the project's own
    /// Unity-saved files, and Unity's component rules (RequireComponent, one Graphic, one Rigidbody).
    /// </summary>
    public class ComponentAuthoringTests
    {
        static string Text(YMap body)
        {
            var f = UnityYamlFile.CreateEmpty();
            f.Documents.Add(new UnityDocument { ClassId = 114, FileId = 1, TypeName = "MonoBehaviour", Body = body });
            string s = f.Write();
            return s[(s.IndexOf("MonoBehaviour:\n", StringComparison.Ordinal) + "MonoBehaviour:\n".Length)..];
        }

        [Fact]
        public void ScriptDefaultsFollowUnitysSerializationRules()
        {
            var body = ComponentSerializer.MonoBehaviourBody(typeof(AuthoringProbe), 42, "0123456789abcdef0123456789abcdef", "Assembly-CSharp::CosmicShore.Tests.AuthoringProbe");
            Assert.Equal(
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_GameObject: {fileID: 42}\n" +
                "  m_Enabled: 1\n" +
                "  m_EditorHideFlags: 0\n" +
                "  m_Script: {fileID: 11500000, guid: 0123456789abcdef0123456789abcdef, type: 3}\n" +
                "  m_Name: \n" +
                "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Tests.AuthoringProbe\n" +
                "  baseCount: 3\n" +                               // base class first
                "  radius: 0.5\n" +
                "  label: probe\n" +
                "  offset: {x: 1, y: 2, z: 3}\n" +                 // built-in structs are flow maps
                "  tint: {r: 1, g: 0, b: 0, a: 1}\n" +
                "  area:\n    serializedVersion: 2\n    x: 0\n    y: 0\n    width: 1\n    height: 1\n" +
                "  mask:\n    serializedVersion: 2\n    m_Bits: 0\n" +
                "  mode: 2\n" +                                    // an enum is its number
                "  modes: 0700000000000000\n" +                    // an enum array is one hex scalar
                "  flags: 0100\n" +                                // so is a bool array
                "  weights:\n  - 0.25\n" +                         // a float list is a block list
                "  tuning:\n    speed: 2.5\n    tags: []\n" +      // null serializable class -> default instance
                "  target: {fileID: 0}\n" +
                "  onFired:\n    m_PersistentCalls:\n      m_Calls: []\n" +
                "  <Level>k__BackingField: 4\n",                   // [field: SerializeField] keeps its compiler name
                Text(body));
        }

        [Fact]
        public void ScriptsDerivingFromPackageClassesStartWithTheirLayout()
        {
            var graphic = Text(ComponentSerializer.MonoBehaviourBody(typeof(AuthoringGraphic), 1, "g", ""));
            Assert.Contains("  m_EditorClassIdentifier: \n" +
                            "  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 1\n" +
                            "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n" +
                            "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  thickness: 2\n", graphic);

            var net = Text(ComponentSerializer.MonoBehaviourBody(typeof(AuthoringNetworked), 1, "n", ""));
            Assert.Contains("  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  ticks: 30\n", net);
        }

        static UnityAssetEditor NewScene(out long go)
        {
            var file = UnityYamlFile.Parse("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            var ed = new UnityAssetEditor(file, seed: 3);
            go = ed.CreateGameObject("Thing");
            return ed;
        }

        static ComponentAdder Adder(UnityAssetEditor ed)
        {
            var db = ContentYamlTests.Db;
            var types = new ScriptTypeMap(db, new[] { typeof(GameObject).Assembly });
            return new ComponentAdder(ed, new ScriptCatalog(db, types), Templates);
        }

        static ComponentTemplates s_templates;
        static ComponentTemplates Templates => s_templates ??= new ComponentTemplates(ContentYamlTests.Db.AssetsRoot);

        [Fact]
        public void BuiltInComponentsUseUnitysLayoutAndPinnedDefaults()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var ed = NewScene(out long go);
            var r = Adder(ed).Add(go, "BoxCollider");
            var box = ed.File.Find(Assert.Single(r.Added).id);
            Assert.Equal(65, box.ClassId);
            Assert.Equal("BoxCollider", box.TypeName);
            Assert.Equal(go, box.Body["m_GameObject"].Long("fileID"));
            Assert.Equal("{x: 1, y: 1, z: 1}", UnityYamlFile.FormatValue(box.Body["m_Size"]));
            Assert.Equal("0", box.Body.Str("m_IsTrigger"));
            // The layout is the one this Unity version writes (Unity 6 added the layer overrides).
            Assert.True(box.Body.Has("m_IncludeLayers") && box.Body.Has("m_ProvidesContacts"));
            Assert.Equal(2, ed.Components(go).Count); // Transform + BoxCollider

            Adder(ed).Add(go, "Rigidbody");
            Assert.Throws<ArgumentException>(() => Adder(ed).Add(go, "Rigidbody")); // one per object
            Adder(ed).Add(go, "BoxCollider");                                        // colliders may repeat
            Assert.True(UnityYamlFile.SameContent(ed.File, UnityYamlFile.Parse(ed.File.Write())));
        }

        [Fact]
        public void UiComponentsBringTheirRequirementsAndStayOnePerObject()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var ed = NewScene(out long go);
            var r = Adder(ed).Add(go, "Image");
            // [RequireComponent(RectTransform, CanvasRenderer)] on Graphic: the Transform becomes a
            // RectTransform IN PLACE (same fileID), and a CanvasRenderer is added before the Image.
            Assert.Equal(new[] { "CanvasRenderer", "Image" }, r.Added.Select(a => a.type));
            var comps = ed.Components(go);
            Assert.Equal(new[] { "RectTransform", "CanvasRenderer", "MonoBehaviour" }, comps.Select(c => c.TypeName));
            Assert.False(comps[0].Body.Has("serializedVersion"));
            Assert.Equal("{x: 100, y: 100}", UnityYamlFile.FormatValue(comps[0].Body["m_SizeDelta"]));
            var image = comps[2];
            Assert.Equal("UnityEngine.UI::UnityEngine.UI.Image", image.Body.Str("m_EditorClassIdentifier"));
            Assert.Equal("{fileID: 0}", UnityYamlFile.FormatValue(image.Body["m_Sprite"]));
            Assert.Equal("fe87c0e1cc204ed48ad3b37840f39efc", image.Body["m_Script"].Str("guid"));

            var ex = Assert.Throws<ArgumentException>(() => Adder(ed).Add(go, "TextMeshProUGUI"));
            Assert.Contains("Graphic", ex.Message);
        }

        [Fact]
        public void RemovingAComponentUnlinksItAndClearsReferencesToIt()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var ed = NewScene(out long go);
            long box = Adder(ed).Add(go, "BoxCollider").Added[0].id;
            long other = ed.CreateGameObject("Watcher");
            long rb = Adder(ed).Add(other, "Rigidbody").Added[0].id;
            ed.Set(rb, "m_Watched", $"{{fileID: {box}}}");

            var r = ed.RemoveComponent(box);
            Assert.Equal(new[] { box }, r.Removed);
            Assert.Null(ed.File.Find(box));
            Assert.Single(ed.Components(go));                               // just the Transform
            Assert.Equal("0", ed.Get(rb, "m_Watched.fileID").Scalar);       // cleared, and reported
            Assert.Single(r.ClearedReferences);
            Assert.Throws<ArgumentException>(() => ed.RemoveComponent(ed.Components(go)[0].FileId));
        }
    }
}
