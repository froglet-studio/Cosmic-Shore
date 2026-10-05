using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Editing;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Editing through nested prefab instances: every change is written into the instance's
    /// <c>m_Modification</c> block the way the Unity Editor writes it, and the expanded graph — the
    /// view the game loads — must then show exactly that change. Most tests run on a miniature
    /// project (one prefab, one scene) written to a temp folder; the corpus tests run on the real one.
    /// </summary>
    public class PrefabInstanceEditingTests : IDisposable
    {
        const string Header = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
        const string WidgetGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", SceneGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", MatGuid = "cccccccccccccccccccccccccccccccc";

        static string ObjHeader(long go = -1) =>
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            + (go >= 0 ? $"  m_GameObject: {{fileID: {go}}}\n" : "");

        static string GameObject(long id, string name, params long[] comps) =>
            $"--- !u!1 &{id}\nGameObject:\n" + ObjHeader() + "  serializedVersion: 6\n  m_Component:\n"
            + string.Concat(comps.Select(c => $"  - component: {{fileID: {c}}}\n"))
            + $"  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n";

        static string Transform(long id, long go, long father, params long[] children) =>
            $"--- !u!4 &{id}\nTransform:\n" + ObjHeader(go) + "  serializedVersion: 2\n"
            + "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n"
            + (children.Length == 0 ? "  m_Children: []\n" : "  m_Children:\n" + string.Concat(children.Select(c => $"  - {{fileID: {c}}}\n")))
            + $"  m_Father: {{fileID: {father}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n";

        // Widget.prefab: Widget (Transform, BoxCollider, MeshRenderer with one material) / Knob (Transform).
        static readonly string WidgetPrefab = Header
            + GameObject(100, "Widget", 101, 102, 103)
            + Transform(101, 100, 0, 201)
            + "--- !u!65 &102\nBoxCollider:\n" + ObjHeader(100) + "  m_IsTrigger: 0\n  m_Enabled: 1\n  m_Size: {x: 1, y: 1, z: 1}\n  m_Center: {x: 0, y: 0, z: 0}\n"
            + "--- !u!23 &103\nMeshRenderer:\n" + ObjHeader(100) + $"  m_Enabled: 1\n  m_Materials:\n  - {{fileID: 2100000, guid: {MatGuid}, type: 2}}\n"
            + GameObject(200, "Knob", 201)
            + Transform(201, 200, 101);

        static readonly string Scene = Header
            + GameObject(10, "Holder", 11)
            + Transform(11, 10, 0)
            + "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n  - {fileID: 11}\n";

        readonly string _root;
        readonly AssetDatabase _db;

        public PrefabInstanceEditingTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "cs-prefab-edit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets"));
            Directory.CreateDirectory(Path.Combine(_root, "ProjectSettings"));
            Write("Assets/Widget.prefab", WidgetPrefab, WidgetGuid);
            Write("Assets/Scene.unity", Scene, SceneGuid);
            Write("Assets/Wood.mat", "%YAML 1.1\n", MatGuid);
            _db = new AssetDatabase(_root);
        }

        public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch (IOException) { } }

        void Write(string rel, string text, string guid)
        {
            string p = Path.Combine(_root, rel);
            File.WriteAllText(p, text);
            File.WriteAllText(p + ".meta", $"fileFormatVersion: 2\nguid: {guid}\n");
        }

        (PrefabInstanceEditor pie, UnityYamlFile file) Open(string text = null)
        {
            string path = Path.Combine(_root, "Assets/Scene.unity");
            var file = UnityYamlFile.Parse(text ?? Scene);
            return (new PrefabInstanceEditor(new UnityAssetEditor(file, seed: 3), _db, path), file);
        }

        static List<string> Paths(UnityDocument pi)
            => pi.Body["m_Modification"]["m_Modifications"].Items.Select(m => m.Str("propertyPath")).ToList();

        static List<long> Targets(UnityDocument pi)
            => pi.Body["m_Modification"]["m_Modifications"].Items.Select(m => m["target"].Long("fileID")).ToList();

        // ── Placing ───────────────────────────────────────────────────

        [Fact]
        public void PlacingAPrefabWritesTheOverridesTheEditorWrites()
        {
            var (pie, file) = Open();
            var root = pie.Instantiate("Assets/Widget.prefab", name: "Gizmo", position: (YMap)UnityYaml.ParseValue("{x: 1, y: 2, z: 3}"));

            var pi = file.Documents.Single(d => d.ClassId == 1001);
            Assert.Equal(new[] { "m_Name", "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.w", "m_LocalRotation.x",
                                 "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalEulerAnglesHint.x", "m_LocalEulerAnglesHint.y",
                                 "m_LocalEulerAnglesHint.z" }, Paths(pi));
            Assert.Equal(new long[] { 100, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101 }, Targets(pi)); // grouped, ascending target
            Assert.Equal($"{{fileID: 100100000, guid: {WidgetGuid}, type: 3}}", UnityYamlFile.FormatValue(pi.Body["m_SourcePrefab"]));
            // A scene lists a root instance itself among its roots; no stand-in is needed.
            Assert.Contains(file.Documents.Last().Body["m_Roots"].Items, r => r.Long("fileID") == pi.FileId);
            Assert.DoesNotContain(file.Documents, d => d.Stripped);

            Assert.Equal("Gizmo", pie.PathOf(root));
            Assert.Equal("Gizmo/Knob", pie.PathOf(pie.Children(root).Single()));
            Assert.Equal("{x: 1, y: 2, z: 3}", UnityYamlFile.FormatValue(pie.Get(pie.TransformOf(root), "m_LocalPosition")));
            Assert.True(UnityYamlFile.SameContent(file, UnityYamlFile.Parse(file.Write())));
        }

        [Fact]
        public void PlacingUnderAnObjectThenDeletingTheInstanceRestoresTheFile()
        {
            var (pie, file) = Open();
            var holder = pie.FindGameObject("Holder");
            var root = pie.Instantiate("Assets/Widget.prefab", holder);
            Assert.Equal("Holder/Widget", pie.PathOf(root));
            var stand = file.Documents.Single(d => d.Stripped);
            Assert.Equal(4, stand.ClassId);
            Assert.Equal(PrefabGraph.Xor(root.Instance, 101), stand.FileId); // the derived id Unity gives a stand-in
            Assert.Contains(file.Find(11).Body["m_Children"].Items, c => c.Long("fileID") == stand.FileId);

            pie.Delete(root);
            Assert.Equal(Scene, file.Write());
        }

        [Fact]
        public void APrefabCannotBePlacedInsideItselfOrAtAPrefabsRoot()
        {
            Write("Assets/Bolt.prefab", Header + GameObject(6, "Bolt", 5) + Transform(5, 6, 0), "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
            var db = new AssetDatabase(_root);
            string path = Path.Combine(_root, "Assets/Widget.prefab");
            var pie = new PrefabInstanceEditor(new UnityAssetEditor(UnityYamlFile.Parse(WidgetPrefab)), db, path);
            var e = Assert.Throws<ArgumentException>(() => pie.Instantiate("Assets/Widget.prefab", pie.FindGameObject("Widget")));
            Assert.Contains("nest a prefab in itself", e.Message);
            e = Assert.Throws<ArgumentException>(() => pie.Instantiate("Assets/Bolt.prefab"));
            Assert.Contains("one root", e.Message);
            Assert.Equal("Widget/Bolt", pie.PathOf(pie.Instantiate("Assets/Bolt.prefab", pie.FindGameObject("Widget"))));
        }

        // ── Overrides ─────────────────────────────────────────────────

        [Fact]
        public void OverridesAreGroupedByTargetInAscendingOrderWhateverOrderTheyAreMadeIn()
        {
            var (pie, file) = Open();
            var root = pie.Instantiate("Assets/Widget.prefab");
            var pi = file.Find(root.Instance);
            var knob = pie.TransformOf(pie.Children(root).Single());
            var box = pie.Components(root).Single(c => c.ClassId == 65);

            pie.SetOverride(knob, "m_LocalScale", UnityYaml.ParseValue("{x: 2, y: 3, z: 4}"));
            pie.SetOverride(box, "m_IsTrigger", new YScalar("1"));
            pie.SetOverride(knob, "m_LocalScale.y", new YScalar("9")); // an existing leaf is updated in place

            var targets = Targets(pi);
            Assert.Equal(targets.OrderBy(t => t), targets);
            Assert.Equal(new[] { "m_IsTrigger" }, Paths(pi).Where((p, i) => targets[i] == 102));
            Assert.Equal(new[] { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" }, Paths(pi).Where((p, i) => targets[i] == 201));
            Assert.Equal("{x: 2, y: 9, z: 4}", UnityYamlFile.FormatValue(pie.Get(knob, "m_LocalScale")));
            Assert.Equal("1", pie.Get(box, "m_IsTrigger").Scalar);

            Assert.Equal(3, pie.Revert(knob, "m_LocalScale"));
            Assert.Equal("{x: 1, y: 1, z: 1}", UnityYamlFile.FormatValue(pie.Get(knob, "m_LocalScale")));
        }

        [Fact]
        public void AListIsOverriddenAsItsSizeThenItsElements_AndAReferenceInObjectReference()
        {
            var (pie, file) = Open();
            var root = pie.Instantiate("Assets/Widget.prefab");
            var renderer = pie.Components(root).Single(c => c.ClassId == 23);
            pie.SetOverride(renderer, "m_Materials", UnityYaml.ParseValue(
                $"[{{fileID: 2100000, guid: {MatGuid}, type: 2}}, {{fileID: 0}}]"));

            var mods = file.Find(root.Instance).Body["m_Modification"]["m_Modifications"].Items.Where(m => m["target"].Long("fileID") == 103).ToList();
            Assert.Equal(new[] { "m_Materials.Array.size", "m_Materials.Array.data[0]", "m_Materials.Array.data[1]" }, mods.Select(m => m.Str("propertyPath")));
            Assert.Equal("2", mods[0].Str("value"));
            Assert.Equal("", mods[1].Str("value"));                       // a reference has an empty value…
            Assert.Equal(MatGuid, mods[1]["objectReference"].Str("guid")); // …and lives in objectReference
            Assert.Equal(2, pie.Get(renderer, "m_Materials").Items.Count);

            // Editor-style paths address the same override.
            pie.SetOverride(renderer, "m_Materials[1]", YMap.Ref(2100000, MatGuid, 2));
            Assert.Equal(3, file.Find(root.Instance).Body["m_Modification"]["m_Modifications"].Items.Count(m => m["target"].Long("fileID") == 103));
        }

        [Fact]
        public void AReferenceToAnObjectInsideAnInstanceGoesThroughItsStandIn()
        {
            var (pie, file) = Open(Scene + "--- !u!114 &60\nMonoBehaviour:\n" + ObjHeader(10) + "  m_Enabled: 1\n  target: {fileID: 0}\n");
            var root = pie.Instantiate("Assets/Widget.prefab");
            var knob = pie.Children(root).Single();
            new UnityAssetEditor(file).Set(60, "target", YMap.Ref(pie.StandIn(knob)));
            pie.Invalidate();
            var stand = file.Documents.Single(d => d.Stripped);
            Assert.Equal(1, stand.ClassId);
            Assert.Equal(knob.Id, pie.Graph.Resolve(file.Find(60).Body["target"].Long("fileID")));
        }

        [Fact]
        public void AnInstanceCannotOverrideItsStructureOrAFieldOfTheWrongKind()
        {
            var (pie, _) = Open();
            var root = pie.Instantiate("Assets/Widget.prefab");
            var tf = pie.TransformOf(root);
            Assert.Throws<ArgumentException>(() => pie.SetOverride(tf, "m_Father", YMap.Ref(0)));
            Assert.Throws<ArgumentException>(() => pie.SetOverride(tf, "m_GameObject", YMap.Ref(0)));
            Assert.Throws<ArgumentException>(() => pie.SetOverride(tf, "m_LocalPosition", new YScalar("5")));
            Assert.Throws<ArgumentException>(() => pie.SetOverride(tf, "m_NoSuchField", new YScalar("5")));
            pie.SetOverride(tf, "m_NoSuchField", new YScalar("5"), force: true); // Unity keeps such an override, ignored
            Assert.Throws<ArgumentException>(() => pie.SetOverride(pie.FindGameObject("Holder"), "m_Name", new YScalar("x")));
        }

        // ── Removing from and adding to an instance ───────────────────

        [Fact]
        public void RemovingAComponentOrObjectIsRecordedAndDropsTheirOverrides()
        {
            var (pie, file) = Open();
            var root = pie.Instantiate("Assets/Widget.prefab");
            var box = pie.Components(root).Single(c => c.ClassId == 65);
            pie.SetOverride(box, "m_IsTrigger", new YScalar("1"));
            pie.RemoveComponent(box);
            var mod = file.Find(root.Instance).Body["m_Modification"];
            Assert.Equal(new long[] { 102 }, mod["m_RemovedComponents"].Items.Select(r => r.Long("fileID")));
            Assert.DoesNotContain(mod["m_Modifications"].Items, m => m["target"].Long("fileID") == 102);
            Assert.DoesNotContain(pie.Components(pie.InstanceRoot(root.Instance)), c => c.ClassId == 65);
            Assert.Throws<ArgumentException>(() => pie.RemoveComponent(pie.TransformOf(root)));

            pie.Delete(pie.Children(root).Single());
            Assert.Equal(new long[] { 200 }, mod["m_RemovedGameObjects"].Items.Select(r => r.Long("fileID")));
            Assert.Empty(pie.Children(pie.InstanceRoot(root.Instance)));
            Assert.DoesNotContain(pie.GameObjects(), g => g.Path.EndsWith("Knob"));
        }

        [Fact]
        public void AddingAComponentOrChildToAnInstanceObjectAndRemovingThemRestoresTheFile()
        {
            var (pie, file) = Open();
            var root = pie.Instantiate("Assets/Widget.prefab");
            string placed = file.Write();
            var knob = pie.Children(root).Single();

            long collider = pie.Editor.AddComponentDocument(pie.StandIn(knob), 65, "BoxCollider", (YMap)UnityYaml.ParseValue("{m_Enabled: 1}"));
            long child = pie.CreateGameObject("Grip", knob);
            var mod = file.Find(root.Instance).Body["m_Modification"];
            Assert.Single(mod["m_AddedComponents"].Items);
            Assert.Equal(collider, mod["m_AddedComponents"].Items[0]["addedObject"].Long("fileID"));
            Assert.Equal("201", mod["m_AddedGameObjects"].Items[0]["targetCorrespondingSourceObject"].Str("fileID"));
            Assert.Equal("-1", mod["m_AddedGameObjects"].Items[0].Str("insertIndex"));
            Assert.Contains(pie.Components(pie.Object(knob.Id)), c => c.Id == collider);
            Assert.Equal("Widget/Knob/Grip", pie.PathOf(pie.Object(child)));

            pie.Delete(pie.Object(child));
            pie.RemoveComponent(pie.Object(collider));
            Assert.Equal(placed, file.Write()); // the stand-ins they needed went with them
        }

        // ── Property paths ────────────────────────────────────────────

        [Theory]
        [InlineData("m_Materials[0]", "m_Materials.Array.data[0]")]
        [InlineData("m_Materials.0", "m_Materials.Array.data[0]")]
        [InlineData("m_Materials.Array.data[0]", "m_Materials.Array.data[0]")]
        [InlineData("m_Materials.Array.size", "m_Materials.Array.size")]
        [InlineData("m_Shape[1][2].x", "m_Shape.Array.data[1].Array.data[2].x")]
        [InlineData("m_LocalPosition.x", "m_LocalPosition.x")]
        public void FieldPathsBecomeUnityPropertyPaths(string path, string expected)
            => Assert.Equal(expected, PrefabInstanceEditor.PropertyPath(path));

        // ── Apply to prefab ───────────────────────────────────────────

        [Fact]
        public void ApplyingAllWritesOverridesIntoThePrefabAndLeavesThePlacement()
        {
            var (pie, file) = Open();
            var root = pie.Instantiate("Assets/Widget.prefab", name: "Gizmo", position: (YMap)UnityYaml.ParseValue("{x: 1, y: 2, z: 3}"));
            var knob = pie.TransformOf(pie.Children(root).Single());
            pie.SetOverride(knob, "m_LocalScale", UnityYaml.ParseValue("{x: 2, y: 3, z: 4}"));
            pie.SetOverride(pie.Components(root).Single(c => c.ClassId == 65), "m_IsTrigger", new YScalar("1"));

            var r = pie.ApplyAll(root.Instance); // throws if the instance would load differently

            Assert.Equal(4, r.Applied.Count);
            Assert.Equal(11, r.PlacementOverrides);                   // name + transform: where THIS copy is
            Assert.Equal(11, Paths(file.Find(root.Instance)).Count);  // …and nothing else stays on it
            Assert.Equal("{x: 2, y: 3, z: 4}", UnityYamlFile.FormatValue(r.Prefab.Find(201).Body["m_LocalScale"]));
            Assert.Equal("1", r.Prefab.Find(102).Body.Str("m_IsTrigger"));
            // Only the two changed lines of the prefab differ.
            var lines = r.Prefab.Write().Split('\n');
            Assert.Equal(2, WidgetPrefab.Split('\n').Zip(lines).Count(p => p.First != p.Second));
            Assert.Equal("Gizmo", pie.PathOf(pie.InstanceRoot(root.Instance)));
        }

        [Fact]
        public void ApplyingAnAdditionMovesItIntoThePrefabAndReferencesFollowIt()
        {
            var (pie, file) = Open(Scene + "--- !u!114 &60\nMonoBehaviour:\n" + ObjHeader(10) + "  m_Enabled: 1\n  target: {fileID: 0}\n");
            var root = pie.Instantiate("Assets/Widget.prefab");
            var knob = pie.Children(root).Single();
            long collider = pie.Editor.AddComponentDocument(pie.StandIn(knob), 65, "BoxCollider", (YMap)UnityYaml.ParseValue("{m_Enabled: 1, m_Size: {x: 3, y: 3, z: 3}}"));
            long grip = pie.CreateGameObject("Grip", knob);
            new UnityAssetEditor(file).Set(60, "target", YMap.Ref(grip));
            pie.Invalidate();

            var r = pie.ApplyAll(root.Instance);

            Assert.Equal(2, r.Applied.Count(a => a.StartsWith("added", StringComparison.Ordinal)));
            var mod = file.Find(root.Instance).Body["m_Modification"];
            Assert.Empty(mod["m_AddedComponents"].Items);
            Assert.Empty(mod["m_AddedGameObjects"].Items);
            Assert.Null(file.Find(collider));
            Assert.Null(file.Find(grip));
            Assert.Equal(2, r.Prefab.Find(200).Body["m_Component"].Items.Count);   // Knob: Transform + the collider
            Assert.Single(r.Prefab.Find(201).Body["m_Children"].Items);           // Knob's transform: Grip
            var g = pie.FindGameObject("Widget/Knob/Grip");
            Assert.True(g.InInstance);
            Assert.Equal(g.Id, pie.Graph.Resolve(file.Find(60).Body["target"].Long("fileID"))); // through a stand-in
            Assert.Contains(pie.Components(pie.Object(knob.Id)), c => c.ClassId == 65 && c.InInstance);
        }

        [Fact]
        public void ApplyingRemovalsDeletesFromThePrefab()
        {
            var (pie, file) = Open();
            var root = pie.Instantiate("Assets/Widget.prefab");
            pie.RemoveComponent(pie.Components(root).Single(c => c.ClassId == 65));
            pie.Delete(pie.Children(root).Single());

            var r = pie.ApplyAll(root.Instance);

            Assert.Null(r.Prefab.Find(102));
            Assert.Null(r.Prefab.Find(200));
            Assert.Null(r.Prefab.Find(201));
            Assert.Empty(r.Prefab.Find(101).Body["m_Children"].Items);
            Assert.DoesNotContain(r.Prefab.Find(100).Body["m_Component"].Items, c => c["component"].Long("fileID") == 102);
            var mod = file.Find(root.Instance).Body["m_Modification"];
            Assert.Empty(mod["m_RemovedComponents"].Items);
            Assert.Empty(mod["m_RemovedGameObjects"].Items);
        }

        [Fact]
        public void AChangeThatRefersOutsideThePrefabStaysOnTheInstance()
        {
            var (pie, file) = Open(Scene + "--- !u!114 &60\nMonoBehaviour:\n" + ObjHeader(10) + "  m_Enabled: 1\n");
            var root = pie.Instantiate("Assets/Widget.prefab");
            var renderer = pie.Components(root).Single(c => c.ClassId == 23);
            pie.SetOverride(renderer, "m_Materials[0]", YMap.Ref(60)); // a scene object: the prefab cannot name it

            var r = pie.Apply(renderer);

            Assert.Empty(r.Applied);
            Assert.Contains(r.Kept, k => k.Contains("outside the prefab"));
            Assert.Contains("m_Materials.Array.data[0]", Paths(file.Find(root.Instance)));
        }

        [Fact]
        public void AnObjectFromANestedPrefabIsAppliedAsTheOuterPrefabsOverride()
        {
            // Assembly.prefab places Widget under its root; the scene places Assembly.
            string asmPath = Path.Combine(_root, "Assets/Assembly.prefab");
            var asm = new PrefabInstanceEditor(new UnityAssetEditor(UnityYamlFile.Parse(Header + GameObject(300, "Assembly", 301) + Transform(301, 300, 0)), seed: 5), _db, asmPath);
            asm.Instantiate("Assets/Widget.prefab", asm.FindGameObject("Assembly"));
            Write("Assets/Assembly.prefab", asm.Editor.File.Write(), "ffffffffffffffffffffffffffffffff");
            var db = new AssetDatabase(_root);
            var file = UnityYamlFile.Parse(Scene);
            var pie = new PrefabInstanceEditor(new UnityAssetEditor(file, seed: 3), db, Path.Combine(_root, "Assets/Scene.unity"));
            var root = pie.Instantiate("Assets/Assembly.prefab");
            var knob = pie.TransformOf(pie.FindGameObject("Assembly/Widget/Knob"));
            pie.SetOverride(knob, "m_LocalScale.x", new YScalar("7"));

            var r = pie.ApplyAll(root.Instance);

            Assert.EndsWith("Assembly.prefab", r.PrefabPath);
            var nested = r.Prefab.Documents.Single(d => d.ClassId == 1001);
            var o = nested.Body["m_Modification"]["m_Modifications"].Items.Single(m => m.Str("propertyPath") == "m_LocalScale.x");
            Assert.Equal(201, o["target"].Long("fileID")); // Knob's Transform, as Widget.prefab knows it
            Assert.Equal("7", o.Str("value"));
            Assert.Equal(WidgetPrefab, File.ReadAllText(Path.Combine(_root, "Assets/Widget.prefab")));
        }

        // ── The loader ────────────────────────────────────────────────

        [Fact]
        public void AStandInsIdWinsOverAnEqualDerivedIdOfAnotherObject()
        {
            // Two instances whose ids differ in the low bits derive colliding ids: (2^5) == (1^6).
            // The file's stand-in &7 names instance 2's Transform, so instance 1's GameObject — which
            // derives 7 too — must be the one renamed (Dolphin.prefab's jets shipped this way).
            Write("Assets/Jet.prefab", Header + GameObject(6, "Jet", 5) + Transform(5, 6, 0), "dddddddddddddddddddddddddddddddd");
            var db = new AssetDatabase(_root);
            string pi(long id) => $"--- !u!1001 &{id}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n"
                                  + "    m_TransformParent: {fileID: 0}\n    m_Modifications: []\n    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n"
                                  + "    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {fileID: 100100000, guid: dddddddddddddddddddddddddddddddd, type: 3}\n";
            string text = Header + pi(1) + pi(2)
                + "--- !u!4 &7 stripped\nTransform:\n  m_CorrespondingSourceObject: {fileID: 5, guid: dddddddddddddddddddddddddddddddd, type: 3}\n  m_PrefabInstance: {fileID: 2}\n  m_PrefabAsset: {fileID: 0}\n";
            var g = PrefabGraph.Build(db, new AssetFile(Path.Combine(_root, "Assets/S.unity"), null, UnityYaml.ParseDocuments(text)));

            foreach (var o in g.Objects.Values.Where(o => o.ClassId == 1))
                foreach (var c in o.Body["m_Component"].Items)
                    Assert.Equal(o.Id, g.Resolve(g.Get(c["component"].Long("fileID")).Body["m_GameObject"].Long("fileID")));
            Assert.Equal(1, g.Get(g.MapInstance(1, 6)).ClassId);
            Assert.Equal(g.MapInstance(2, 5), g.Resolve(7));
        }

        // ── The corpus ────────────────────────────────────────────────

        /// <summary>
        /// Every override list Unity wrote in grouped, ascending-target order, stripped and re-made
        /// through the API with its targets in a shuffled order, comes back byte for byte.
        /// </summary>
        [Fact]
        public void UnityWrittenOverrideListsReplayByteForByte()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var db = ContentYamlTests.Db;
            var rng = new Random(1);
            int files = 0, identical = 0;
            var lost = new List<string>();
            foreach (var f in Directory.EnumerateFiles(db.AssetsRoot, "*", SearchOption.AllDirectories)
                                       .Where(f => f.EndsWith(".unity", StringComparison.Ordinal) || f.EndsWith(".prefab", StringComparison.Ordinal)))
            {
                string text = File.ReadAllText(f);
                if (!text.Contains("--- !u!1001 ") || text.Contains("m_EditorClassIdentifier:\n")) continue; // generator-written
                var file = UnityYamlFile.Parse(text);
                var pie = new PrefabInstanceEditor(new UnityAssetEditor(file), db, f);
                bool unitySorted = true;
                foreach (var d in file.Documents.Where(d => d.ClassId == 1001).ToList())
                {
                    if (d.Body["m_Modification"]?["m_Modifications"] is not YSeq mods) continue;
                    var items = mods.List.Cast<YMap>().ToList();
                    var t = items.Select(m => m["target"].Long("fileID")).ToList();
                    if (!t.SequenceEqual(t.OrderBy(x => x))) unitySorted = false;
                    if (items.Any(m => m["target"].Str("guid") != d.Body["m_SourcePrefab"].Str("guid"))) unitySorted = false;
                    mods.List.Clear();
                    foreach (var group in items.GroupBy(m => m["target"].Long("fileID")).OrderBy(_ => rng.Next()))
                        foreach (var m in group)
                        {
                            var r = (YMap)m["objectReference"];
                            pie.SetOverrideLeaf(d.FileId, m["target"].Long("fileID"), m.Str("propertyPath"),
                                                r.Long("fileID") != 0 ? r.Clone() : new YScalar(m.Str("value")));
                        }
                }
                files++;
                if (file.Write() == text) identical++;
                else if (unitySorted) lost.Add(f);
            }
            Assert.True(files > 100, $"only {files} files with prefab instances");
            Assert.Empty(lost);
            Assert.True(identical >= files - 12, $"{identical}/{files} identical");
        }

        /// <summary>
        /// Apply-all on a sample of every placed prefab with changes in the project (every fourth,
        /// for time): each must leave its instance loading exactly as before — Apply re-reads the
        /// file against the edited prefab and throws on any difference — and write files that read back.
        /// </summary>
        [Fact]
        public void ApplyingAllToProjectInstancesLeavesThemLoadingTheSame()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var db = new AssetDatabase(ContentYamlTests.ProjectRoot); // own cache: applies replace prefabs in it
            int tried = 0, k = 0;
            foreach (var f in Directory.EnumerateFiles(db.AssetsRoot, "*.prefab", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string text = File.ReadAllText(f);
                if (!text.Contains("--- !u!1001 ") || text.Contains("m_EditorClassIdentifier:\n")) continue;
                foreach (var pi in UnityYaml.ParseDocuments(text).Where(d => d.ClassId == 1001).Select(d => d.FileId))
                {
                    if (k++ % 4 != 0) continue;
                    var file = UnityYamlFile.Parse(text);
                    var pie = new PrefabInstanceEditor(new UnityAssetEditor(file, seed: 1), db, f);
                    string guid = file.Find(pi).Body["m_SourcePrefab"].Str("guid");
                    if (db.PathOf(guid)?.EndsWith(".prefab", StringComparison.Ordinal) != true) continue;
                    var previous = db.Load(guid);
                    try
                    {
                        var r = pie.ApplyAll(pi);
                        tried++;
                        Assert.True(UnityYamlFile.SameContent(r.Prefab, UnityYamlFile.Parse(r.Prefab.Write())));
                        Assert.True(UnityYamlFile.SameContent(file, UnityYamlFile.Parse(file.Write())));
                    }
                    finally { db.Restore(guid, previous); }
                }
            }
            Assert.True(tried > 40, $"only {tried} instances tried");
        }

        [Fact]
        public void EveryComponentOfTheShippedDolphinBelongsToItsOwnGameObject()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var g = PrefabGraph.Build(ContentYamlTests.Db, ContentYamlTests.Db.LoadPath("Assets/_Prefabs/Spacevessels/Dolphin.prefab"));
            foreach (var o in g.Objects.Values.Where(o => !o.Removed && o.ClassId == 1))
                foreach (var c in o.Body["m_Component"]?.Items ?? Array.Empty<YNode>())
                    if (g.Get(c["component"].Long("fileID")) is { } comp)
                        Assert.Equal(o.Id, g.Resolve(comp.Body["m_GameObject"].Long("fileID")));
        }
    }
}
