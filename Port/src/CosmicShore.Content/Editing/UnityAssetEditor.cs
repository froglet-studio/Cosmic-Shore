using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Content.Editing
{
    /// <summary>One GameObject in a scene or prefab, as the file records it.</summary>
    public readonly record struct GameObjectInfo(long FileId, string Name, long TransformId, long ParentTransformId, bool FromPrefabInstance);

    /// <summary>
    /// Object-level edits on a <see cref="UnityYamlFile"/> (a .unity scene or a .prefab): read and
    /// write any serialized field by path, create GameObjects, delete them with everything that
    /// hangs off them. Every edit leaves the rest of the file byte-identical, because the file
    /// only re-emits what changed.
    ///
    /// <para>The rules here are Unity's own file invariants, and keeping them is the whole job:
    /// a GameObject lists its components (<c>m_Component</c>) and each component names its
    /// GameObject; a Transform lists its children (<c>m_Children</c>) and each child names its
    /// father (<c>m_Father</c>); a scene lists its root transforms (<c>SceneRoots.m_Roots</c>); a
    /// nested prefab is a <c>PrefabInstance</c> document plus "stripped" stand-ins for the objects
    /// of it that the file refers to. An edit that leaves one side of any pair stale is a file
    /// Unity loads with "missing" references, so every operation fixes both sides.</para>
    /// </summary>
    public sealed class UnityAssetEditor
    {
        public const int GameObjectClass = 1, TransformClass = 4, RectTransformClass = 224,
                         PrefabInstanceClass = 1001, SceneRootsClass = 1660057539;

        public readonly UnityYamlFile File;
        readonly Random _ids;

        /// <summary>
        /// Set by <see cref="PrefabInstanceEditor"/>: answers for objects that live inside a nested
        /// prefab instance (their components, adding to them, creating under them). Without it,
        /// an edit that reaches into an instance is refused.
        /// </summary>
        public PrefabInstanceEditor Instances { get; internal set; }

        /// <summary>Every write goes through here, so a cached view of the file (the instance graph) is dropped.</summary>
        void Changed() => Instances?.Invalidate();

        public UnityAssetEditor(UnityYamlFile file, int? seed = null)
        {
            File = file;
            _ids = seed is int s ? new Random(s) : new Random();
        }

        // ── Field access ──────────────────────────────────────────────

        /// <summary>
        /// Reads a field of document <paramref name="fileId"/> by path: <c>m_Name</c>,
        /// <c>m_LocalPosition.x</c>, <c>m_Component[1].component.fileID</c>.
        /// </summary>
        public YNode Get(long fileId, string path)
        {
            var doc = Require(fileId);
            return Resolve(doc.Body, path, create: false, out _, out _);
        }

        /// <summary>
        /// Writes a field. <paramref name="yamlValue"/> is a YAML value as it would appear after
        /// "key: " — <c>5</c>, <c>Hello world</c>, <c>{x: 0, y: 1, z: 0}</c>, <c>[]</c>. Missing
        /// map keys along the path are created; a sequence index must already exist.
        /// </summary>
        public void Set(long fileId, string path, string yamlValue)
            => Set(fileId, path, UnityYaml.ParseValue(yamlValue));

        public void Set(long fileId, string path, YNode value)
        {
            var doc = RequireOwned(fileId);
            Resolve(doc.Body, path, create: true, out var parent, out var last);
            switch (parent)
            {
                case YMap m: m.Set(last, value); break;
                case YSeq q when int.TryParse(last, out int i) && i >= 0 && i < q.List.Count: q.List[i] = value; break;
                default: throw new ArgumentException($"cannot set '{path}' on &{fileId}");
            }
            Changed();
        }

        /// <summary>Removes a map key (or sequence item). Returns false when it was not there.</summary>
        public bool Remove(long fileId, string path)
        {
            var doc = RequireOwned(fileId);
            if (Resolve(doc.Body, path, create: false, out var parent, out var last) == null) return false;
            bool removed = false;
            if (parent is YMap m) removed = m.Remove(last);
            else if (parent is YSeq q && int.TryParse(last, out int i)) { q.List.RemoveAt(i); removed = true; }
            if (removed) Changed();
            return removed;
        }

        /// <summary>Reads a field of any body by the same paths <see cref="Get"/> takes.</summary>
        public static YNode Lookup(YMap body, string path) => Resolve(body, path, create: false, out _, out _);

        static YNode Resolve(YMap body, string path, bool create, out YNode parent, out string last)
        {
            var parts = SplitPath(path);
            YNode cur = body;
            parent = null; last = null;
            for (int k = 0; k < parts.Count; k++)
            {
                parent = cur; last = parts[k];
                YNode next;
                if (cur is YSeq q)
                {
                    if (!int.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) || i < 0 || i >= q.List.Count)
                        return null;
                    next = q.List[i];
                }
                else if (cur is YMap m)
                {
                    next = m[last];
                    if (next == null && create && k < parts.Count - 1)
                    {
                        next = parts[k + 1].All(char.IsDigit) ? new YSeq() : new YMap();
                        m.Add(last, next);
                    }
                }
                else return null;
                if (next == null) return null;
                cur = next;
            }
            return cur;
        }

        static List<string> SplitPath(string path)
        {
            var parts = new List<string>();
            var sb = new StringBuilder();
            foreach (char c in path)
            {
                if (c == '.' || c == '[' || c == ']')
                {
                    if (sb.Length > 0) { parts.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) parts.Add(sb.ToString());
            return parts;
        }

        // ── Hierarchy ─────────────────────────────────────────────────

        /// <summary>Every GameObject the file defines or stands in for, with its transform and parent.</summary>
        public List<GameObjectInfo> GameObjects()
        {
            var list = new List<GameObjectInfo>();
            foreach (var d in File.Documents)
            {
                if (d.ClassId != GameObjectClass) continue;
                long t = TransformOf(d);
                long parent = t != 0 && File.Find(t) is { } td ? Ref(td.Body["m_Father"]) : 0;
                list.Add(new GameObjectInfo(d.FileId, d.Body.Str("m_Name") ?? "", t, parent, d.Stripped));
            }
            return list;
        }

        /// <summary>
        /// Finds a GameObject by its hierarchy path (<c>Canvas/Panel/Button</c>) or, failing that,
        /// by bare name when the name is unique. 0 = not found or ambiguous.
        /// </summary>
        public long FindGameObject(string pathOrName)
        {
            var all = GameObjects();
            var byTransform = all.Where(g => g.TransformId != 0).ToDictionary(g => g.TransformId);
            string PathOf(GameObjectInfo g)
            {
                var names = new List<string>();
                var cur = g;
                for (int guard = 0; guard < 256; guard++)
                {
                    names.Add(cur.Name);
                    if (cur.ParentTransformId == 0 || !byTransform.TryGetValue(cur.ParentTransformId, out cur)) break;
                }
                names.Reverse();
                return string.Join("/", names);
            }
            var exact = all.Where(g => PathOf(g) == pathOrName).ToList();
            if (exact.Count == 1) return exact[0].FileId;
            var named = all.Where(g => g.Name == pathOrName).ToList();
            return named.Count == 1 ? named[0].FileId : 0;
        }

        long TransformOf(UnityDocument go)
        {
            if (go.Body["m_Component"] is not YSeq comps) return 0;
            foreach (var c in comps.Items)
            {
                long id = Ref(c["component"]);
                if (File.Find(id) is { } cd && (cd.ClassId == TransformClass || cd.ClassId == RectTransformClass)) return id;
            }
            return 0;
        }

        // ── Create ────────────────────────────────────────────────────

        /// <summary>
        /// Adds an empty GameObject with a Transform, under <paramref name="parentGameObject"/>
        /// (0 = a scene root). Returns the new GameObject's fileID. Like the Editor's "Create
        /// Empty Child", a child of a RectTransform gets a RectTransform and takes its parent's
        /// layer. A parent inside a nested prefab is recorded as an added object of that instance.
        /// </summary>
        public long CreateGameObject(string name, long parentGameObject = 0)
        {
            if (parentGameObject == 0) return CreateUnderTransform(name, 0, rect: false, layer: "0", out _);
            var parent = Require(parentGameObject);
            if (parent.Stripped)
                return (Instances ?? throw InsideInstance(parentGameObject)).CreateGameObject(name, Instances.Object(parentGameObject));
            long parentTransform = TransformOf(parent);
            if (parentTransform == 0) throw new ArgumentException($"&{parentGameObject} has no Transform");
            bool rect = File.Find(parentTransform)?.ClassId == RectTransformClass;
            return CreateUnderTransform(name, parentTransform, rect, parent.Body.Str("m_Layer") ?? "0", out _);
        }

        /// <summary>
        /// The new GameObject + Transform documents, parented to <paramref name="parentTransform"/>
        /// (a stand-in for a transform inside a nested prefab is allowed: it has no child list, the
        /// instance records the addition instead).
        /// </summary>
        internal long CreateUnderTransform(string name, long parentTransform, bool rect, string layer, out long transformId)
        {
            long goId = NewFileId(), tId = NewFileId();

            var go = new YMap();
            go.Add("m_ObjectHideFlags", S("0"));
            go.Add("m_CorrespondingSourceObject", YMap.Ref(0));
            go.Add("m_PrefabInstance", YMap.Ref(0));
            go.Add("m_PrefabAsset", YMap.Ref(0));
            go.Add("serializedVersion", S("6"));
            var comps = new YSeq();
            var entry = new YMap(); entry.Add("component", YMap.Ref(tId)); comps.List.Add(entry);
            go.Add("m_Component", comps);
            go.Add("m_Layer", S(string.IsNullOrEmpty(layer) ? "0" : layer));
            go.Add("m_Name", S(name));
            go.Add("m_TagString", S("Untagged"));
            go.Add("m_Icon", YMap.Ref(0));
            go.Add("m_NavMeshLayer", S("0"));
            go.Add("m_StaticEditorFlags", S("0"));
            go.Add("m_IsActive", S("1"));

            var t = new YMap();
            t.Add("m_ObjectHideFlags", S("0"));
            t.Add("m_CorrespondingSourceObject", YMap.Ref(0));
            t.Add("m_PrefabInstance", YMap.Ref(0));
            t.Add("m_PrefabAsset", YMap.Ref(0));
            t.Add("m_GameObject", YMap.Ref(goId));
            t.Add("serializedVersion", S("2"));
            t.Add("m_LocalRotation", Flow(("x", "0"), ("y", "0"), ("z", "0"), ("w", "1")));
            t.Add("m_LocalPosition", Flow(("x", "0"), ("y", "0"), ("z", "0")));
            t.Add("m_LocalScale", Flow(("x", "1"), ("y", "1"), ("z", "1")));
            t.Add("m_ConstrainProportionsScale", S("0"));
            t.Add("m_Children", new YSeq());
            t.Add("m_Father", YMap.Ref(parentTransform));
            t.Add("m_LocalEulerAnglesHint", Flow(("x", "0"), ("y", "0"), ("z", "0")));

            Insert(new UnityDocument { ClassId = GameObjectClass, FileId = goId, TypeName = "GameObject", Body = go });
            var td = new UnityDocument { ClassId = TransformClass, FileId = tId, TypeName = "Transform", Body = t };
            Insert(td);
            if (rect) MakeRectTransform(td);

            if (parentTransform != 0)
            {
                var pd = Require(parentTransform);
                if (!pd.Stripped) Children(pd).List.Add(YMap.Ref(tId));
            }
            else SceneRoots()?.List.Add(YMap.Ref(tId));
            transformId = tId;
            Changed();
            return goId;
        }

        /// <summary>Inserts a document where Unity keeps it: in fileID order, SceneRoots last.</summary>
        public void Insert(UnityDocument doc)
        {
            var docs = File.Documents;
            bool sorted = true;
            for (int i = 1; i < docs.Count; i++)
                if (docs[i].ClassId != SceneRootsClass && docs[i - 1].FileId > docs[i].FileId) { sorted = false; break; }
            int at = docs.Count;
            while (at > 0 && docs[at - 1].ClassId == SceneRootsClass) at--;
            if (sorted)
                while (at > 0 && docs[at - 1].FileId > doc.FileId) at--;
            docs.Insert(at, doc);
            Changed();
        }

        /// <summary>A fresh local fileID no document of this file uses.</summary>
        public long NewFileId()
        {
            // Unity has written random 64-bit local IDs since 2018.3, which is what keeps two
            // people's additions to one file from colliding when their edits merge.
            long id;
            do id = _ids.NextInt64(1_000_000_000L, long.MaxValue);
            while (File.Find(id) != null);
            return id;
        }

        // ── Components ────────────────────────────────────────────────

        /// <summary>The component documents of a GameObject, in its m_Component order.</summary>
        public List<UnityDocument> Components(long goId)
        {
            var go = Require(goId);
            if (go.Stripped) return (Instances ?? throw InsideInstance(goId)).ComponentDocuments(goId);
            var list = new List<UnityDocument>();
            if (go.Body["m_Component"] is YSeq comps)
                foreach (var c in comps.Items)
                    if (File.Find(Ref(c["component"])) is { } cd) list.Add(cd);
            return list;
        }

        /// <summary>
        /// Attaches a component document: a new fileID, <c>m_GameObject</c> pointing at its owner,
        /// and an entry at the end of the owner's <c>m_Component</c> list (where Unity appends).
        /// </summary>
        public long AddComponentDocument(long goId, int classId, string typeName, YMap body)
        {
            var go = Require(goId);
            if (go.ClassId != GameObjectClass) throw new ArgumentException($"&{goId} is not a GameObject");
            if (go.Stripped && Instances == null) throw InsideInstance(goId);
            long id = NewFileId();
            body.Set("m_GameObject", YMap.Ref(goId));
            Insert(new UnityDocument { ClassId = classId, FileId = id, TypeName = typeName, Body = body });
            if (go.Stripped)
            {
                // A GameObject inside a nested prefab: the stand-in has no component list; the
                // instance records the component as one added to it.
                Instances.RecordAdded(goId, id, "m_AddedComponents");
                return id;
            }
            if (go.Body["m_Component"] is not YSeq comps) { comps = new YSeq(); go.Body.Set("m_Component", comps); }
            var entry = new YMap(); entry.Add("component", YMap.Ref(id));
            comps.List.Add(entry);
            Changed();
            return id;
        }

        /// <summary>
        /// Removes one component. A GameObject's Transform cannot be removed (delete the object
        /// instead); references to the component elsewhere in the file are cleared and reported.
        /// </summary>
        public DeleteResult RemoveComponent(long componentId)
        {
            var doc = RequireOwned(componentId);
            if (doc.ClassId is TransformClass or RectTransformClass) throw new ArgumentException("a Transform cannot be removed; delete the GameObject instead");
            if (doc.ClassId is GameObjectClass or PrefabInstanceClass) throw new ArgumentException($"&{componentId} is not a component");
            return DeleteDocuments(new HashSet<long> { componentId });
        }

        /// <summary>
        /// Turns a GameObject's Transform into a RectTransform (what Unity does when a UI component
        /// is added to a plain object). The fileID is kept, so every reference to it stays valid.
        /// </summary>
        public bool EnsureRectTransform(long goId)
        {
            var go = Require(goId);
            if (go.Stripped) return (Instances ?? throw InsideInstance(goId)).EnsureRectTransform(goId);
            long t = TransformOf(go);
            if (t == 0 || File.Find(t) is not { } td) throw new ArgumentException($"&{goId} has no Transform");
            if (td.ClassId == RectTransformClass) return false;
            if (td.Stripped) throw new ArgumentException($"&{goId}'s Transform is inside a nested prefab; make it a RectTransform in that prefab");
            MakeRectTransform(td);
            Changed();
            return true;
        }

        void MakeRectTransform(UnityDocument td)
        {
            td.ClassId = RectTransformClass;
            td.TypeName = "RectTransform";
            td.Body.Remove("serializedVersion"); // a Transform's; a RectTransform writes none
            void Add(string k, string yaml) { if (!td.Body.Has(k)) td.Body.Add(k, UnityYaml.ParseValue(yaml)); }
            Add("m_AnchorMin", "{x: 0.5, y: 0.5}");
            Add("m_AnchorMax", "{x: 0.5, y: 0.5}");
            Add("m_AnchoredPosition", "{x: 0, y: 0}");
            Add("m_SizeDelta", "{x: 100, y: 100}");
            Add("m_Pivot", "{x: 0.5, y: 0.5}");
        }

        // ── Delete ────────────────────────────────────────────────────

        /// <summary>What a delete removed, and the references elsewhere in the file it had to clear.</summary>
        public sealed class DeleteResult
        {
            public readonly List<long> Removed = new();
            public readonly List<string> ClearedReferences = new();
        }

        /// <summary>
        /// Deletes a GameObject, its components, its whole child hierarchy (nested prefab
        /// instances included) and every link to them: the parent's <c>m_Children</c>, the
        /// scene's root list, and any other field in the file that referenced a removed object
        /// (cleared to <c>{fileID: 0}</c> — what Unity shows as "None" — and reported).
        /// </summary>
        public DeleteResult DeleteGameObject(long goId)
        {
            var go = Require(goId);
            if (go.ClassId != GameObjectClass) throw new ArgumentException($"&{goId} is not a GameObject");
            if (go.Stripped) return (Instances ?? throw InsideInstance(goId)).Delete(Instances.Object(goId));
            var doomed = new HashSet<long>();
            long t = TransformOf(go);
            if (t != 0) CollectTransform(t, doomed);
            else CollectGameObject(go, doomed);
            return DeleteDocuments(doomed);
        }

        /// <summary>
        /// Removes a set of documents and every link to them: list entries (a transform's
        /// <c>m_Children</c>, a GameObject's <c>m_Component</c>, the scene's roots, a prefab
        /// instance's added-object records) are dropped; any other field naming one is cleared to
        /// <c>{fileID: 0}</c> and reported.
        /// </summary>
        public DeleteResult DeleteDocuments(HashSet<long> doomed)
        {
            // A stand-in exists only to be referred to: one whose last referrer is going goes too
            // (e.g. a placed prefab's parent stand-in). Stand-ins that were already unreferenced
            // are left alone — this delete did not orphan them.
            var orphaned = new HashSet<long>();
            foreach (var d in File.Documents)
                if (doomed.Contains(d.FileId)) CollectLocalRefs(d.Body, orphaned);
            if (orphaned.Count > 0)
            {
                var stillReferenced = new HashSet<long>();
                foreach (var d in File.Documents)
                    if (!doomed.Contains(d.FileId)) CollectLocalRefs(d.Body, stillReferenced);
                foreach (var d in File.Documents)
                    if (d.Stripped && orphaned.Contains(d.FileId) && !stillReferenced.Contains(d.FileId)) doomed.Add(d.FileId);
            }

            foreach (var d in File.Documents)
            {
                if (doomed.Contains(d.FileId) || d.Stripped) continue;
                switch (d.ClassId)
                {
                    case TransformClass or RectTransformClass when d.Body["m_Children"] is YSeq kids:
                        RemoveRefs(kids, doomed);
                        break;
                    case GameObjectClass when d.Body["m_Component"] is YSeq comps:
                        comps.List.RemoveAll(c => doomed.Contains(Ref(c["component"])));
                        break;
                    case SceneRootsClass when d.Body["m_Roots"] is YSeq roots:
                        RemoveRefs(roots, doomed);
                        break;
                    case PrefabInstanceClass when d.Body["m_Modification"] is YMap mod:
                        foreach (var key in new[] { "m_AddedGameObjects", "m_AddedComponents" })
                            if (mod[key] is YSeq added) added.List.RemoveAll(a => doomed.Contains(Ref(a["addedObject"])));
                        break;
                }
            }

            var result = new DeleteResult();
            File.Documents.RemoveAll(d =>
            {
                if (!doomed.Contains(d.FileId)) return false;
                result.Removed.Add(d.FileId);
                return true;
            });
            foreach (var d in File.Documents)
                ClearRefs(d.Body, doomed, $"&{d.FileId} {d.TypeName}", result.ClearedReferences);
            Changed();
            return result;
        }

        internal void CollectTransform(long transformId, HashSet<long> doomed)
        {
            if (!doomed.Add(transformId) || File.Find(transformId) is not { } td) return;
            if (td.Stripped)
            {
                // A stand-in for an object inside a nested prefab: the whole instance goes.
                long pi = Ref(td.Body["m_PrefabInstance"]);
                if (pi != 0) CollectPrefabInstance(pi, doomed);
                return;
            }
            if (File.Find(Ref(td.Body["m_GameObject"])) is { } go) CollectGameObject(go, doomed);
            if (td.Body["m_Children"] is YSeq kids)
                foreach (var k in kids.Items) CollectTransform(Ref(k), doomed);
        }

        void CollectGameObject(UnityDocument go, HashSet<long> doomed)
        {
            doomed.Add(go.FileId);
            if (go.Body["m_Component"] is YSeq comps)
                foreach (var c in comps.Items)
                {
                    long id = Ref(c["component"]);
                    if (id == 0 || !doomed.Add(id)) continue;
                    if (File.Find(id) is { } cd && (cd.ClassId == TransformClass || cd.ClassId == RectTransformClass))
                    {
                        doomed.Remove(id);
                        CollectTransform(id, doomed);
                    }
                }
        }

        /// <summary>A prefab instance, its stand-ins, and everything added to it or nested under it.</summary>
        internal void CollectPrefabInstance(long piId, HashSet<long> doomed)
        {
            if (!doomed.Add(piId)) return;
            var standIns = File.Documents.Where(d => d.Stripped && Ref(d.Body["m_PrefabInstance"]) == piId).Select(d => d.FileId).ToHashSet();
            foreach (var id in standIns) doomed.Add(id);
            // Prefab instances placed under one of its objects.
            foreach (var d in File.Documents)
                if (d.ClassId == PrefabInstanceClass && standIns.Contains(Ref(d.Body["m_Modification"]?["m_TransformParent"])))
                    CollectPrefabInstance(d.FileId, doomed);
            // Objects added to the instance from outside it hang off its stripped transforms.
            foreach (var d in File.Documents)
            {
                if (d.Stripped || (d.ClassId != TransformClass && d.ClassId != RectTransformClass)) continue;
                if (standIns.Contains(Ref(d.Body["m_Father"]))) CollectTransform(d.FileId, doomed);
            }
            // GameObjects/components added to stripped game objects of the instance.
            foreach (var d in File.Documents)
                if (!d.Stripped && standIns.Contains(Ref(d.Body["m_GameObject"]))) doomed.Add(d.FileId);
        }

        static void RemoveRefs(YSeq seq, HashSet<long> doomed)
            => seq.List.RemoveAll(n => doomed.Contains(Ref(n)) && Guid(n) == null);

        static void ClearRefs(YNode node, HashSet<long> doomed, string where, List<string> report)
        {
            switch (node)
            {
                case YMap m:
                    for (int i = 0; i < m.Entries.Count; i++)
                    {
                        var v = m.Entries[i].Value;
                        if (IsLocalRef(v, doomed))
                        {
                            report.Add($"{where}.{m.Entries[i].Key} -> &{Ref(v)}");
                            m.SetAt(i, YMap.Ref(0));
                        }
                        else ClearRefs(v, doomed, $"{where}.{m.Entries[i].Key}", report);
                    }
                    break;
                case YSeq q:
                    for (int i = 0; i < q.List.Count; i++)
                    {
                        if (IsLocalRef(q.List[i], doomed))
                        {
                            report.Add($"{where}[{i}] -> &{Ref(q.List[i])}");
                            q.List[i] = YMap.Ref(0);
                        }
                        else ClearRefs(q.List[i], doomed, $"{where}[{i}]", report);
                    }
                    break;
            }
        }

        static void CollectLocalRefs(YNode node, HashSet<long> into)
        {
            switch (node)
            {
                case YMap m when m.Flow && m.Has("fileID") && Guid(m) == null:
                    if (m.Long("fileID") is var id and not 0) into.Add(id);
                    break;
                case YMap m:
                    foreach (var e in m.Entries) CollectLocalRefs(e.Value, into);
                    break;
                case YSeq q:
                    foreach (var x in q.List) CollectLocalRefs(x, into);
                    break;
            }
        }

        static bool IsLocalRef(YNode n, HashSet<long> doomed)
            => n is YMap m && m.Flow && m.Has("fileID") && Guid(m) == null && doomed.Contains(m.Long("fileID"));

        // ── Helpers ───────────────────────────────────────────────────

        UnityDocument Require(long fileId)
            => File.Find(fileId) ?? throw new ArgumentException($"no document &{fileId}");

        UnityDocument RequireOwned(long fileId)
        {
            var d = Require(fileId);
            return d.Stripped ? throw InsideInstance(fileId) : d;
        }

        static ArgumentException InsideInstance(long id)
            => new($"&{id} is an object inside a nested prefab instance; edit it through the instance (an override) or in its prefab");

        YSeq SceneRoots()
            => File.Documents.FirstOrDefault(d => d.ClassId == SceneRootsClass)?.Body["m_Roots"] as YSeq;

        static YSeq Children(UnityDocument transform)
        {
            if (transform.Body["m_Children"] is YSeq s) return s;
            var n = new YSeq();
            transform.Body.Set("m_Children", n);
            return n;
        }

        static long Ref(YNode n) => n is YMap m ? m.Long("fileID") : 0;
        static string Guid(YNode n) => n?.Str("guid") is { Length: > 0 } g ? g : null;
        static YScalar S(string s) => new(s);

        static YMap Flow(params (string k, string v)[] kv)
        {
            var m = new YMap { Flow = true };
            foreach (var (k, v) in kv) m.Add(k, S(v));
            return m;
        }
    }
}
