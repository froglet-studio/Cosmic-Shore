using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Content.Editing
{
    /// <summary>
    /// An object of a scene or prefab as the game loads it: either a document the file defines
    /// (<see cref="InInstance"/> false; <see cref="Id"/> is its fileID) or an object inside a nested
    /// prefab instance (<see cref="Instance"/> is the PrefabInstance document, <see cref="Source"/>
    /// the object's fileID in the instance's source prefab — what an override's <c>target</c> names).
    /// </summary>
    public sealed record SceneObject(long Id, int ClassId, string TypeName, long Instance, long Source)
    {
        public bool InInstance => Instance != 0;
    }

    /// <summary>
    /// Editing what a scene or prefab holds through its nested prefab instances — the half of
    /// Unity's file model <see cref="UnityAssetEditor"/> alone cannot reach, because an object
    /// inside an instance has no document of its own in the file. Every change is written the way
    /// the Unity Editor writes it into the instance's <c>m_Modification</c> block:
    ///
    /// <list type="bullet">
    /// <item>a field change is a property override in <c>m_Modifications</c> — one entry per leaf
    /// (<c>m_LocalPosition.x</c>), a list as <c>.Array.size</c> plus <c>.Array.data[i]</c>, a reference
    /// in <c>objectReference</c> with an empty <c>value</c>;</item>
    /// <item>a removed component or GameObject is a source reference in <c>m_RemovedComponents</c> /
    /// <c>m_RemovedGameObjects</c>;</item>
    /// <item>a component or child GameObject added to an instance object is an ordinary document
    /// of this file hung off a "stripped" stand-in for that object, listed in
    /// <c>m_AddedComponents</c> / <c>m_AddedGameObjects</c>;</item>
    /// <item>a placed prefab is a new PrefabInstance with the overrides the Editor writes on
    /// placement (the root's name and its transform).</item>
    /// </list>
    ///
    /// <para>Ordering is Unity's, as measured over the project's Unity-written files: overrides
    /// are grouped by target, the groups in ascending (signed) target fileID, and within a target
    /// in the order they were made; a list's <c>Array.size</c> precedes its elements. Reads go
    /// through <see cref="PrefabGraph"/>, the same expansion the game loader uses, so what an edit
    /// writes is checked against what the file then loads as.</para>
    /// </summary>
    public sealed class PrefabInstanceEditor
    {
        public readonly UnityAssetEditor Editor;
        readonly AssetDatabase _db;
        readonly string _path;
        readonly string _guid;

        PrefabGraph _graph;
        Dictionary<long, List<long>> _components; // GameObject → its components, in m_Component order then added ones
        Dictionary<long, List<long>> _children;   // GameObject → child GameObjects

        const int GO = UnityAssetEditor.GameObjectClass, T = UnityAssetEditor.TransformClass,
                  RT = UnityAssetEditor.RectTransformClass, PI = UnityAssetEditor.PrefabInstanceClass;

        /// <summary>The fileID every prefab asset's root is referenced by (<c>m_SourcePrefab</c>).</summary>
        public const long PrefabAssetFileId = 100100000;

        public PrefabInstanceEditor(UnityAssetEditor editor, AssetDatabase db, string path)
        {
            Editor = editor;
            _db = db;
            _path = Path.GetFullPath(path);
            _guid = db.GuidOf(_path);
            editor.Instances = this;
        }

        UnityYamlFile File => Editor.File;

        /// <summary>The file expanded as the game loads it. Rebuilt after any edit.</summary>
        public PrefabGraph Graph
        {
            get
            {
                if (_graph == null) Build();
                return _graph;
            }
        }

        public void Invalidate() { _graph = null; _components = null; _children = null; }

        void Build()
        {
            _graph = PrefabGraph.Build(_db, new AssetFile(_path, _guid, File.Documents));
            _components = new Dictionary<long, List<long>>();
            var claimed = new HashSet<long>();
            foreach (var o in _graph.Objects.Values)
            {
                if (o.Removed || o.ClassId != GO) continue;
                var list = new List<long>();
                foreach (var c in o.Body["m_Component"]?.Items ?? Array.Empty<YNode>())
                {
                    long cid = _graph.Resolve(Ref(c["component"]));
                    if (_graph.Get(cid) is { } co && _graph.Resolve(Ref(co.Body["m_GameObject"])) == o.Id && claimed.Add(cid))
                        list.Add(cid);
                }
                _components[o.Id] = list;
            }
            // A component added to an object inside an instance is in no m_Component list: it
            // names its GameObject (through a stand-in) and the instance lists it as added.
            foreach (var o in _graph.Objects.Values)
            {
                if (o.Removed || o.ClassId == GO || o.ClassId == PI || claimed.Contains(o.Id) || o.Body["m_GameObject"] == null) continue;
                if (_components.TryGetValue(_graph.Resolve(Ref(o.Body["m_GameObject"])), out var l)) { l.Add(o.Id); claimed.Add(o.Id); }
            }
            _children = new Dictionary<long, List<long>>();
            foreach (var goId in _components.Keys)
            {
                long parent = ParentId(goId);
                if (parent == 0) continue;
                if (!_children.TryGetValue(parent, out var kids)) _children[parent] = kids = new List<long>();
                kids.Add(goId);
            }
        }

        // ── Reading ───────────────────────────────────────────────────

        /// <summary>
        /// The object behind an id: a document of this file, a stand-in (resolved to the instance
        /// object it stands for), or an instance object's id in the expanded graph. Null when it
        /// does not exist or an instance removes it.
        /// </summary>
        public SceneObject Object(long id)
        {
            long gid = Graph.Resolve(id);
            var o = Graph.Get(gid);
            if (o == null) return null;
            return Graph.InstanceOrigin.TryGetValue(gid, out var origin)
                ? new SceneObject(gid, o.ClassId, o.TypeName, origin.Instance, origin.Source)
                : new SceneObject(gid, o.ClassId, o.TypeName, 0, 0);
        }

        /// <summary>The object <paramref name="source"/> of the source prefab, as instance <paramref name="instance"/> holds it.</summary>
        public SceneObject InstanceObject(long instance, long source)
        {
            var src = Graph.InstanceSource(instance);
            return src == null ? null : Object(Graph.MapInstance(instance, src.Resolve(source)));
        }

        /// <summary>The object's fields as loaded: the source prefab's values with this file's overrides applied.</summary>
        public YMap Body(SceneObject o) => Graph.Get(o.Id)?.Body;

        public string Name(SceneObject go) => Body(go)?.Str("m_Name") ?? "";

        /// <summary>A GameObject's components, in the order the Inspector shows them.</summary>
        public List<SceneObject> Components(SceneObject go)
        {
            _ = Graph;
            return _components.TryGetValue(go.Id, out var l) ? l.Select(Object).Where(o => o != null).ToList() : new List<SceneObject>();
        }

        public SceneObject TransformOf(SceneObject go) => Components(go).FirstOrDefault(c => c.ClassId is T or RT);

        /// <summary>The GameObject a component belongs to.</summary>
        public SceneObject GameObjectOf(SceneObject component)
            => component.ClassId == GO ? component : Object(Graph.Resolve(Ref(Body(component)?["m_GameObject"])));

        public SceneObject Parent(SceneObject go)
        {
            _ = Graph;
            long p = ParentId(go.Id);
            return p == 0 ? null : Object(p);
        }

        long ParentId(long goId)
        {
            if (!_components.TryGetValue(goId, out var comps)) return 0;
            foreach (var c in comps)
            {
                var co = _graph.Get(c);
                if (co == null || (co.ClassId != T && co.ClassId != RT)) continue;
                var father = _graph.Get(_graph.Resolve(Ref(co.Body["m_Father"])));
                return father == null ? 0 : _graph.Resolve(Ref(father.Body["m_GameObject"]));
            }
            return 0;
        }

        public List<SceneObject> Children(SceneObject go)
        {
            _ = Graph;
            return _children.TryGetValue(go.Id, out var l) ? l.Select(Object).ToList() : new List<SceneObject>();
        }

        /// <summary>A GameObject's hierarchy path (<c>Canvas/Panel/Button</c>).</summary>
        public string PathOf(SceneObject go)
        {
            var names = new List<string>();
            for (int guard = 0; go != null && guard < 256; guard++)
            {
                names.Add(Name(go));
                go = Parent(go);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>Every GameObject the file loads — its own and every nested prefab instance's — with its path.</summary>
        public List<(SceneObject GameObject, string Path)> GameObjects()
        {
            var list = new List<(SceneObject, string)>();
            foreach (var o in Graph.Objects.Values)
                if (!o.Removed && o.ClassId == GO && Object(o.Id) is { } so)
                    list.Add((so, PathOf(so)));
            return list;
        }

        /// <summary>A GameObject by hierarchy path, or by name when that name is unique. Null when not found or ambiguous.</summary>
        public SceneObject FindGameObject(string pathOrName)
        {
            var all = GameObjects();
            var exact = all.Where(g => g.Path == pathOrName).ToList();
            if (exact.Count == 1) return exact[0].GameObject;
            var named = all.Where(g => Name(g.GameObject) == pathOrName).ToList();
            return named.Count == 1 ? named[0].GameObject : null;
        }

        /// <summary>A field of the object as loaded (paths as <see cref="UnityAssetEditor.Get"/> takes them).</summary>
        public YNode Get(SceneObject o, string path)
            => Body(o) is { } b ? UnityAssetEditor.Lookup(b, path) ?? Walk(b, PropertyPath(path)) : null;

        /// <summary>The prefab instances this file places, in file order.</summary>
        public IEnumerable<UnityDocument> Instances() => File.Documents.Where(d => d.ClassId == PI);

        /// <summary>The root GameObject of a placed instance.</summary>
        public SceneObject InstanceRoot(long instance)
            => Graph.InstanceSource(instance)?.FindPrefabRoot() is { } r ? Object(Graph.MapInstance(instance, r.Id)) : null;

        public string SourcePrefabPath(long instance) => _db.PathOf(SourceGuid(instance));

        // ── Overrides ─────────────────────────────────────────────────

        /// <summary>One <c>m_Modifications</c> entry.</summary>
        public sealed record Override(long Target, string PropertyPath, string Value, YMap ObjectReference);

        public List<Override> Overrides(long instance)
            => (Modification(instance, create: false)?["m_Modifications"]?.Items ?? Array.Empty<YNode>())
               .Select(m => new Override(Ref(m["target"]), m.Str("propertyPath"), m.Str("value") ?? "", m["objectReference"] as YMap))
               .ToList();

        /// <summary>
        /// Overrides a field of an object inside a prefab instance. <paramref name="path"/> may use
        /// either the editor's form (<c>m_Materials[0]</c>) or Unity's property path
        /// (<c>m_Materials.Array.data[0]</c>). A structure is written as one override per leaf, a list
        /// as its size and every element; whatever was overridden under <paramref name="path"/> and is
        /// not part of the new value is dropped. The field must exist on the object unless
        /// <paramref name="force"/> is set (Unity keeps an override whose field is gone, but ignores it).
        /// </summary>
        public void SetOverride(SceneObject obj, string path, YNode value, bool force = false)
        {
            RequireInstanceObject(obj);
            string p = PropertyPath(path);
            string field = p.Split('.')[0];
            if (Structural.Contains(field))
                throw new ArgumentException($"'{field}' links the prefab's objects together; an instance cannot override it"
                                            + (field is "m_Father" or "m_Children" ? " (place or delete objects instead)" : ""));
            var existing = Walk(Body(obj), p);
            if (existing == null && !force)
                throw new ArgumentException($"{obj.TypeName} &{obj.Id} has no field '{p}' (as its prefab defines it); force to write it anyway");
            if (existing != null) CheckKind(existing, value, p);

            var leaves = new List<(string Path, YNode Value)>();
            Flatten(p, value, leaves);
            var mods = ModList(obj.Instance, "m_Modifications");
            var keep = leaves.Select(l => l.Path).ToHashSet(StringComparer.Ordinal);
            mods.List.RemoveAll(m => Ref(m["target"]) == obj.Source && Under(m.Str("propertyPath"), p) && !keep.Contains(m.Str("propertyPath")));
            foreach (var (lp, lv) in leaves) Upsert(mods, obj.Instance, obj.Source, lp, lv);
            Invalidate();
        }

        /// <summary>
        /// Writes one override exactly as given — a leaf property path, its value and object
        /// reference — in Unity's order. The building block of <see cref="SetOverride"/>, exposed for
        /// tools that already speak in property paths.
        /// </summary>
        public void SetOverrideLeaf(long instance, long target, string propertyPath, YNode leaf)
        {
            Upsert(ModList(instance, "m_Modifications"), instance, target, propertyPath, leaf);
            Invalidate();
        }

        /// <summary>
        /// Drops overrides of an object: the one at <paramref name="path"/> and everything under it,
        /// or all of the object's overrides when <paramref name="path"/> is null. Returns how many went.
        /// </summary>
        public int Revert(SceneObject obj, string path = null)
        {
            RequireInstanceObject(obj);
            string p = path == null ? null : PropertyPath(path);
            var mods = Modification(obj.Instance, create: false)?["m_Modifications"] as YSeq;
            if (mods == null) return 0;
            int n = mods.List.RemoveAll(m => Ref(m["target"]) == obj.Source
                                             && (p == null || m.Str("propertyPath") == p || Under(m.Str("propertyPath"), p)));
            if (n > 0) Invalidate();
            return n;
        }

        // ── Remove from an instance ───────────────────────────────────

        /// <summary>
        /// Removes a component. One inside a prefab instance is recorded in
        /// <c>m_RemovedComponents</c> (its overrides and stand-ins go with it); one the file defines
        /// is deleted outright.
        /// </summary>
        public UnityAssetEditor.DeleteResult RemoveComponent(SceneObject comp)
        {
            if (comp.ClassId is T or RT) throw new ArgumentException("a Transform cannot be removed; delete the GameObject instead");
            if (comp.ClassId is GO or PI) throw new ArgumentException($"&{comp.Id} is not a component");
            if (!comp.InInstance) return Editor.RemoveComponent(comp.Id);

            AddSourceRef(ModList(comp.Instance, "m_RemovedComponents"), comp.Instance, comp.Source);
            DropOverrides(comp.Instance, new HashSet<long> { comp.Source });
            return Editor.DeleteDocuments(StandIns(comp.Instance, new HashSet<long> { comp.Source }));
        }

        /// <summary>
        /// Deletes a GameObject with its hierarchy. The root of a prefab instance takes the whole
        /// instance with it; an object inside one is recorded in <c>m_RemovedGameObjects</c>, and
        /// anything this file had added under it goes too.
        /// </summary>
        public UnityAssetEditor.DeleteResult Delete(SceneObject go)
        {
            if (go.ClassId != GO) throw new ArgumentException($"&{go.Id} is not a GameObject");
            if (!go.InInstance) return Editor.DeleteGameObject(go.Id);
            if (InstanceRoot(go.Instance)?.Id == go.Id) return DeleteInstance(go.Instance);

            var sources = new HashSet<long>();
            var doomed = new HashSet<long>();
            foreach (var o in Subtree(go))
            {
                if (o.InInstance && o.Instance == go.Instance) sources.Add(o.Source);
                else if (o.InInstance) Editor.CollectPrefabInstance(o.Instance, doomed); // a prefab placed under it
                else if (o.ClassId is T or RT) Editor.CollectTransform(o.Id, doomed);    // an object added under it
                else doomed.Add(o.Id);                                                  // a component added to it
            }
            var removed = ModList(go.Instance, "m_RemovedGameObjects");
            // A removed child under a removed parent is implied; only the top one is recorded.
            removed.List.RemoveAll(r => Ref(r) != go.Source && sources.Contains(Ref(r)));
            AddSourceRef(removed, go.Instance, go.Source);
            if (Modification(go.Instance, create: false)?["m_RemovedComponents"] is YSeq rc)
                rc.List.RemoveAll(r => sources.Contains(Ref(r)));
            DropOverrides(go.Instance, sources);
            doomed.UnionWith(StandIns(go.Instance, sources));
            return Editor.DeleteDocuments(doomed);
        }

        /// <summary>Deletes a placed prefab instance and everything this file added to it.</summary>
        public UnityAssetEditor.DeleteResult DeleteInstance(long instance)
        {
            var doomed = new HashSet<long>();
            Editor.CollectPrefabInstance(instance, doomed);
            return Editor.DeleteDocuments(doomed);
        }

        IEnumerable<SceneObject> Subtree(SceneObject go)
        {
            yield return go;
            foreach (var c in Components(go)) yield return c;
            foreach (var child in Children(go))
                foreach (var o in Subtree(child)) yield return o;
        }

        void DropOverrides(long instance, HashSet<long> sources)
        {
            if (Modification(instance, create: false)?["m_Modifications"] is YSeq mods)
                mods.List.RemoveAll(m => sources.Contains(Ref(m["target"])));
        }

        HashSet<long> StandIns(long instance, HashSet<long> sources)
            => File.Documents.Where(d => d.Stripped && Ref(d.Body["m_PrefabInstance"]) == instance
                                         && sources.Contains(Ref(d.Body["m_CorrespondingSourceObject"])))
                             .Select(d => d.FileId).ToHashSet();

        // ── Add to an instance ────────────────────────────────────────

        /// <summary>
        /// The fileID this file refers to <paramref name="obj"/> by: its own id for an object the file
        /// defines; for an object inside an instance, its "stripped" stand-in document — the existing
        /// one, or a new one (with the derived id Unity gives it when that id is free).
        /// </summary>
        public long StandIn(SceneObject obj)
            => obj.InInstance ? StandIn(obj.Instance, obj.Source, obj.ClassId, obj.TypeName, Body(obj)) : obj.Id;

        long StandIn(long instance, long source, int classId, string typeName, YMap effective)
        {
            foreach (var d in File.Documents)
                if (d.Stripped && Ref(d.Body["m_PrefabInstance"]) == instance && Ref(d.Body["m_CorrespondingSourceObject"]) == source)
                    return d.FileId;
            long id = PrefabGraph.Xor(instance, source);
            if (id == 0 || File.Find(id) != null) id = Editor.NewFileId();
            var b = new YMap();
            b.Add("m_CorrespondingSourceObject", YMap.Ref(source, SourceGuid(instance), 3));
            b.Add("m_PrefabInstance", YMap.Ref(instance));
            b.Add("m_PrefabAsset", YMap.Ref(0));
            if (classId == 114)
            {
                // A script's stand-in keeps the identifying fields, as Unity writes it.
                b.Add("m_GameObject", YMap.Ref(0));
                b.Add("m_Enabled", effective?["m_Enabled"]?.Clone() ?? new YScalar("1"));
                b.Add("m_EditorHideFlags", new YScalar("0"));
                b.Add("m_Script", effective?["m_Script"]?.Clone() ?? YMap.Ref(0));
                b.Add("m_Name", effective?["m_Name"]?.Clone() ?? new YScalar(""));
                b.Add("m_EditorClassIdentifier", effective?["m_EditorClassIdentifier"]?.Clone() ?? new YScalar(""));
            }
            Editor.Insert(new UnityDocument { ClassId = classId, FileId = id, TypeName = typeName, Body = b, Stripped = true });
            return id;
        }

        /// <summary>
        /// A body read from the loaded view names objects by their ids in the expanded graph. This
        /// rewrites each local reference in <paramref name="body"/> to the id the FILE knows that
        /// object by (a stand-in for one inside an instance, created when missing), and clears any
        /// that resolve to nothing. References into other assets are left alone.
        /// </summary>
        public YMap ToFileRefs(YMap body) => (YMap)Rewrite(body);

        YNode Rewrite(YNode n)
        {
            switch (n)
            {
                case YMap m when IsRef(m) && string.IsNullOrEmpty(m.Str("guid")):
                    long id = m.Long("fileID");
                    if (id == 0) return m;
                    return Object(id) is { } o ? YMap.Ref(StandIn(o)) : YMap.Ref(0);
                case YMap m:
                    for (int i = 0; i < m.Entries.Count; i++) m.SetAt(i, Rewrite(m.Entries[i].Value));
                    return m;
                case YSeq q:
                    for (int i = 0; i < q.List.Count; i++) q.List[i] = Rewrite(q.List[i]);
                    return q;
                default:
                    return n;
            }
        }

        /// <summary>The components of a GameObject inside an instance, as documents (for <see cref="ComponentAdder"/>'s checks).</summary>
        internal List<UnityDocument> ComponentDocuments(long goId)
        {
            var go = Object(goId) ?? throw new ArgumentException($"no object &{goId}");
            return Components(go).Select(c => new UnityDocument { ClassId = c.ClassId, FileId = c.Id, TypeName = c.TypeName, Body = Body(c) }).ToList();
        }

        /// <summary>Lists <paramref name="added"/> (a document hung off stand-in <paramref name="standIn"/>) as added to its instance.</summary>
        internal void RecordAdded(long standIn, long added, string listKey)
        {
            var sd = File.Find(standIn) ?? throw new ArgumentException($"no document &{standIn}");
            long instance = Ref(sd.Body["m_PrefabInstance"]);
            long target = Ref(sd.Body["m_CorrespondingSourceObject"]);
            ModList(instance, listKey).List.Add(AddedEntry(instance, target, added));
            Invalidate();
        }

        YMap AddedEntry(long instance, long target, long added)
        {
            var e = new YMap();
            e.Add("targetCorrespondingSourceObject", YMap.Ref(target, SourceGuid(instance), 3));
            e.Add("insertIndex", new YScalar("-1"));
            e.Add("addedObject", YMap.Ref(added));
            return e;
        }

        /// <summary>
        /// A UI component on an object inside an instance needs a RectTransform there already: Unity
        /// cannot swap a Transform for a RectTransform through an instance.
        /// </summary>
        internal bool EnsureRectTransform(long goId)
        {
            var go = Object(goId) ?? throw new ArgumentException($"no object &{goId}");
            if (TransformOf(go)?.ClassId == RT) return false;
            throw new ArgumentException($"'{PathOf(go)}' has a Transform in its prefab ({SourcePrefabPath(go.Instance)}); "
                                        + "a UI component needs a RectTransform, which only that prefab can provide");
        }

        /// <summary>
        /// A new empty GameObject under <paramref name="parent"/> (null = a scene root). Under an
        /// object inside an instance it hangs off that object's transform stand-in and is listed in
        /// the instance's <c>m_AddedGameObjects</c>.
        /// </summary>
        public long CreateGameObject(string name, SceneObject parent)
        {
            if (parent == null) return Editor.CreateGameObject(name);
            if (!parent.InInstance) return Editor.CreateGameObject(name, parent.Id);
            var tf = TransformOf(parent) ?? throw new ArgumentException($"'{PathOf(parent)}' has no Transform");
            long stand = StandIn(tf);
            long go = Editor.CreateUnderTransform(name, stand, tf.ClassId == RT, Body(parent).Str("m_Layer"), out long tId);
            ModList(parent.Instance, "m_AddedGameObjects").List.Add(AddedEntry(parent.Instance, tf.Source, tId));
            Invalidate();
            return go;
        }

        // ── Place a prefab ────────────────────────────────────────────

        /// <summary>
        /// Places a prefab: a new PrefabInstance under <paramref name="parent"/> (null = a scene
        /// root), with the overrides the Editor writes when a prefab is dropped into a hierarchy —
        /// the root GameObject's name and its transform. <paramref name="position"/> (<c>{x, y, z}</c>)
        /// sets the root's local position (its anchored position for a RectTransform root). Returns
        /// the instance's root GameObject.
        /// </summary>
        public SceneObject Instantiate(string prefabPath, SceneObject parent = null, string name = null, YMap position = null)
        {
            string full = Path.GetFullPath(Path.IsPathRooted(prefabPath) ? prefabPath : Path.Combine(_db.ProjectRoot, prefabPath));
            string guid = _db.GuidOf(full) ?? throw new ArgumentException($"no asset at {prefabPath} (an asset needs its .meta)");
            if (!full.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !AssetDatabase.IsModelPath(full))
                throw new ArgumentException($"{prefabPath} is not a prefab or model");
            if (_guid != null && (guid == _guid || Nests(guid, _guid, new HashSet<string>())))
                throw new ArgumentException($"{Path.GetFileName(full)} is or contains this file; placing it here would nest a prefab in itself");
            bool isPrefabFile = _path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            if (parent == null && isPrefabFile)
                throw new ArgumentException("a prefab has exactly one root; place the instance under a parent");

            var src = PrefabGraph.Build(_db, _db.Load(guid) ?? throw new ArgumentException($"cannot read {prefabPath}"));
            var root = src.FindPrefabRoot() ?? throw new ArgumentException($"{prefabPath} has no root GameObject");
            var rootTf = root.Body["m_Component"]?.Items.Select(c => src.Get(Ref(c["component"])))
                             .FirstOrDefault(c => c != null && (c.ClassId == T || c.ClassId == RT))
                         ?? throw new ArgumentException($"{prefabPath}'s root has no Transform");

            // The instance document.
            long instance = Editor.NewFileId();
            var mod = new YMap();
            mod.Add("serializedVersion", new YScalar("3"));
            mod.Add("m_TransformParent", YMap.Ref(0));
            mod.Add("m_Modifications", new YSeq());
            mod.Add("m_RemovedComponents", new YSeq());
            mod.Add("m_RemovedGameObjects", new YSeq());
            mod.Add("m_AddedGameObjects", new YSeq());
            mod.Add("m_AddedComponents", new YSeq());
            var body = new YMap();
            body.Add("m_ObjectHideFlags", new YScalar("0"));
            body.Add("serializedVersion", new YScalar("2"));
            body.Add("m_Modification", mod);
            body.Add("m_SourcePrefab", YMap.Ref(PrefabAssetFileId, guid, 3));
            Editor.Insert(new UnityDocument { ClassId = PI, FileId = instance, TypeName = "PrefabInstance", Body = body });

            // The overrides placement writes: the root's name, then its transform.
            var mods = (YSeq)mod["m_Modifications"];
            Upsert(mods, instance, root.Id, "m_Name", new YScalar(name ?? Path.GetFileNameWithoutExtension(full)));
            bool rect = rootTf.ClassId == RT;
            foreach (var p in rect ? RectPlacementPaths : TransformPlacementPaths)
            {
                string v = Walk(rootTf.Body, p)?.Scalar ?? (p == "m_LocalRotation.w" ? "1" : "0");
                int dot = p.IndexOf('.');
                string field = p[..dot], axis = p[(dot + 1)..];
                // A UI root is placed by its anchored position; depth stays a local position.
                string placedBy = rect && axis != "z" ? "m_AnchoredPosition" : "m_LocalPosition";
                if (position != null && field == placedBy && position.Str(axis) is { } given) v = given;
                Upsert(mods, instance, rootTf.Id, p, new YScalar(v));
            }

            // Where it hangs.
            if (parent == null)
            {
                // A scene lists a root prefab instance itself among its roots.
                if (File.Documents.FirstOrDefault(d => d.ClassId == UnityAssetEditor.SceneRootsClass)?.Body["m_Roots"] is YSeq roots)
                    roots.List.Add(YMap.Ref(instance));
            }
            else
            {
                var parentTf = TransformOf(parent) ?? throw new ArgumentException($"'{PathOf(parent)}' has no Transform");
                long parentRef = StandIn(parentTf);
                mod.Set("m_TransformParent", YMap.Ref(parentRef));
                long rootStand = StandIn(instance, rootTf.Id, rootTf.ClassId, rootTf.TypeName, rootTf.Body);
                if (parent.InInstance)
                    ModList(parentTf.Instance, "m_AddedGameObjects").List.Add(AddedEntry(parentTf.Instance, parentTf.Source, rootStand));
                else
                {
                    var pd = File.Find(parentRef);
                    if (pd.Body["m_Children"] is not YSeq kids) { kids = new YSeq(); pd.Body.Set("m_Children", kids); }
                    kids.List.Add(YMap.Ref(rootStand));
                }
            }
            Invalidate();
            return InstanceRoot(instance);
        }

        /// <summary>The transform overrides the Editor writes when it places a prefab with a Transform root.</summary>
        public static readonly string[] TransformPlacementPaths =
        {
            "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
            "m_LocalRotation.w", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z",
            "m_LocalEulerAnglesHint.x", "m_LocalEulerAnglesHint.y", "m_LocalEulerAnglesHint.z",
        };

        /// <summary>…and with a RectTransform root.</summary>
        public static readonly string[] RectPlacementPaths =
        {
            "m_Pivot.x", "m_Pivot.y", "m_AnchorMax.x", "m_AnchorMax.y", "m_AnchorMin.x", "m_AnchorMin.y",
            "m_SizeDelta.x", "m_SizeDelta.y",
            "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
            "m_LocalRotation.w", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z",
            "m_AnchoredPosition.x", "m_AnchoredPosition.y",
            "m_LocalEulerAnglesHint.x", "m_LocalEulerAnglesHint.y", "m_LocalEulerAnglesHint.z",
        };

        /// <summary>Does prefab <paramref name="guid"/> (transitively) place prefab <paramref name="target"/>?</summary>
        bool Nests(string guid, string target, HashSet<string> seen)
        {
            if (!seen.Add(guid)) return false;
            var f = _db.Load(guid);
            if (f == null) return false;
            foreach (var d in f.Documents)
            {
                if (d.ClassId != PI) continue;
                string g = d.Body["m_SourcePrefab"]?.Str("guid");
                if (g == target || (g != null && Nests(g, target, seen))) return true;
            }
            return false;
        }

        // ── The m_Modification block ──────────────────────────────────

        static readonly string[] ModificationKeys =
        {
            "serializedVersion", "m_TransformParent", "m_Modifications", "m_RemovedComponents",
            "m_RemovedGameObjects", "m_AddedGameObjects", "m_AddedComponents",
        };

        UnityDocument InstanceDoc(long instance)
            => File.Find(instance) is { ClassId: PI } d ? d : throw new ArgumentException($"&{instance} is not a prefab instance");

        string SourceGuid(long instance) => InstanceDoc(instance).Body["m_SourcePrefab"]?.Str("guid");

        YMap Modification(long instance, bool create)
        {
            var body = InstanceDoc(instance).Body;
            if (body["m_Modification"] is YMap m) return m;
            if (!create) return null;
            m = new YMap();
            m.Add("serializedVersion", new YScalar("3"));
            m.Add("m_TransformParent", YMap.Ref(0));
            body.Set("m_Modification", m);
            return m;
        }

        /// <summary>A list of the modification block, created in its place in Unity's key order when absent.</summary>
        YSeq ModList(long instance, string key)
        {
            var mod = Modification(instance, create: true);
            if (mod[key] is YSeq s) return s;
            s = new YSeq();
            int rank = Array.IndexOf(ModificationKeys, key);
            int at = mod.Entries.Count;
            for (int i = 0; i < mod.Entries.Count; i++)
                if (Array.IndexOf(ModificationKeys, mod.Entries[i].Key) > rank) { at = i; break; }
            mod.Insert(at, key, s);
            return s;
        }

        void AddSourceRef(YSeq list, long instance, long source)
        {
            if (list.List.Any(r => Ref(r) == source)) return;
            list.List.Add(YMap.Ref(source, SourceGuid(instance), 3));
        }

        /// <summary>
        /// Writes one override where Unity keeps it: in place when the target already overrides that
        /// path; else after the target's last override; else before the first target with a larger
        /// (signed) fileID.
        /// </summary>
        void Upsert(YSeq mods, long instance, long target, string path, YNode leaf)
        {
            bool isRef = leaf is YMap lm && IsRef(lm);
            YNode value = isRef ? new YScalar("") : leaf;
            YMap objRef = isRef ? (YMap)leaf.Clone() : YMap.Ref(0);
            int lastSame = -1, firstAfter = -1;
            for (int i = 0; i < mods.List.Count; i++)
            {
                long t = Ref(mods.List[i]["target"]);
                if (t == target)
                {
                    if (mods.List[i] is YMap m && m.Str("propertyPath") == path)
                    {
                        m.Set("value", value);
                        m.Set("objectReference", objRef);
                        return;
                    }
                    lastSame = i;
                }
                else if (t > target && firstAfter < 0) firstAfter = i;
            }
            var mod = new YMap();
            mod.Add("target", YMap.Ref(target, SourceGuid(instance), 3));
            mod.Add("propertyPath", new YScalar(path));
            mod.Add("value", value);
            mod.Add("objectReference", objRef);
            mods.List.Insert(lastSame >= 0 ? lastSame + 1 : firstAfter >= 0 ? firstAfter : mods.List.Count, mod);
        }

        // ── Property paths ────────────────────────────────────────────

        /// <summary>Fields an instance never overrides: they ARE the prefab's structure, which only the prefab can change.</summary>
        static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
        {
            "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
            "m_Component", "m_Children", "m_Father", "m_Script", "m_ObjectHideFlags",
        };

        /// <summary>
        /// Unity's property path for a field path: <c>m_Materials[0]</c> and <c>m_Materials.0</c>
        /// become <c>m_Materials.Array.data[0]</c>; a path already in Unity's form is unchanged.
        /// </summary>
        public static string PropertyPath(string path)
        {
            var parts = new List<string>();
            var segs = path.Split('.');
            for (int i = 0; i < segs.Length; i++)
            {
                string s = segs[i];
                if (s == "Array" && i + 1 < segs.Length && (segs[i + 1] == "size" || segs[i + 1].StartsWith("data[", StringComparison.Ordinal)))
                {
                    parts.Add("Array");
                    parts.Add(segs[++i]);
                    continue;
                }
                if (s.Length > 0 && s.All(char.IsDigit)) { parts.Add("Array"); parts.Add($"data[{s}]"); continue; }
                int b = s.IndexOf('[');
                if (b > 0 && s.EndsWith("]", StringComparison.Ordinal))
                {
                    parts.Add(s[..b]);
                    foreach (var idx in s[(b + 1)..^1].Split("]["))
                    {
                        parts.Add("Array");
                        parts.Add($"data[{idx}]");
                    }
                    continue;
                }
                parts.Add(s);
            }
            return string.Join(".", parts);
        }

        /// <summary>The node at a Unity property path, or null. <c>X.Array.size</c> reads as the list's length.</summary>
        public static YNode Walk(YMap body, string propertyPath)
        {
            YNode cur = body;
            var segs = propertyPath.Split('.');
            for (int i = 0; i < segs.Length && cur != null; i++)
            {
                if (segs[i] == "Array" && i + 1 < segs.Length)
                {
                    string next = segs[++i];
                    if (cur is not YSeq q) return null;
                    if (next == "size") return i == segs.Length - 1 ? new YScalar(q.List.Count.ToString(CultureInfo.InvariantCulture)) : null;
                    if (!next.StartsWith("data[", StringComparison.Ordinal)
                        || !int.TryParse(next.AsSpan(5, next.Length - 6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int k)
                        || k < 0 || k >= q.List.Count) return null;
                    cur = q.List[k];
                    continue;
                }
                cur = cur[segs[i]];
            }
            return cur;
        }

        static void Flatten(string path, YNode v, List<(string, YNode)> leaves)
        {
            switch (v)
            {
                case YMap m when IsRef(m):
                    leaves.Add((path, m));
                    break;
                case YMap m:
                    foreach (var e in m.Entries) Flatten(path + "." + e.Key, e.Value, leaves);
                    break;
                case YSeq q:
                    leaves.Add((path + ".Array.size", new YScalar(q.List.Count.ToString(CultureInfo.InvariantCulture))));
                    for (int i = 0; i < q.List.Count; i++) Flatten($"{path}.Array.data[{i}]", q.List[i], leaves);
                    break;
                default:
                    leaves.Add((path, v));
                    break;
            }
        }

        static void CheckKind(YNode existing, YNode value, string p)
        {
            bool exRef = existing is YMap em && IsRef(em), valRef = value is YMap vm && IsRef(vm);
            if (exRef && !valRef) throw new ArgumentException($"'{p}' is an object reference: give {{fileID: …}} or @object");
            if (valRef && !exRef) throw new ArgumentException($"'{p}' is not an object reference");
            if (existing is YMap && !exRef && value is not YMap)
                throw new ArgumentException($"'{p}' is a structure: set one of its fields ({p}.…) or give the whole {{…}}");
            if (existing is YSeq && value is not YSeq) throw new ArgumentException($"'{p}' is a list: give [ … ] or set {p}[i]");
            if (existing is YScalar && value is not YScalar)
                throw new ArgumentException($"'{p}' is a single value");
        }

        static bool IsRef(YMap m) => m.Flow && m.Has("fileID") && m.Entries.All(e => e.Key is "fileID" or "guid" or "type");

        static bool Under(string path, string prefix) => path != null && path.StartsWith(prefix + ".", StringComparison.Ordinal);

        static void RequireInstanceObject(SceneObject o)
        {
            if (o == null) throw new ArgumentException("no such object");
            if (!o.InInstance) throw new ArgumentException($"&{o.Id} is defined by this file, not a prefab instance: set its fields directly");
        }

        static long Ref(YNode n) => n is YMap m ? m.Long("fileID") : 0;
    }
}
