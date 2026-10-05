using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Content.Editing
{
    /// <summary>
    /// Apply to prefab: an instance's changes become the prefab's own, and leave the instance.
    /// Unity's "Apply" (to the outermost prefab — the one this file placed):
    ///
    /// <list type="bullet">
    /// <item>a property override is written into the prefab's object — or, when that object comes
    /// from a prefab nested inside it, into the prefab's override of that nested instance;</item>
    /// <item>an added component or GameObject (with its whole hierarchy, nested prefabs included)
    /// moves into the prefab, and references to it in this file are re-pointed at the prefab's
    /// copy through a stand-in;</item>
    /// <item>a removed component or GameObject is deleted from the prefab.</item>
    /// </list>
    ///
    /// <para>The root's name and placement (its position, rotation, and a UI root's anchors and size)
    /// are not changes to the prefab — they say where THIS copy is — so they stay with the instance,
    /// as in Unity. A change that refers to something outside the prefab (another object of this
    /// scene) cannot be applied and stays, with the reason.</para>
    ///
    /// <para>The instance must load exactly as it did before: after an apply the file is re-read
    /// against the edited prefab and every object of the instance compared, field by field. If
    /// anything differs the apply throws and nothing should be saved.</para>
    /// </summary>
    public sealed partial class PrefabInstanceEditor
    {
        /// <summary>What an apply did. <see cref="Prefab"/> is the edited prefab, to be saved with this file.</summary>
        public sealed class ApplyResult
        {
            public string PrefabPath;
            public UnityYamlFile Prefab;
            public readonly List<string> Applied = new();
            /// <summary>Changes left on the instance, each with why.</summary>
            public readonly List<string> Kept = new();
            /// <summary>The root name/placement overrides that stay with the instance by design.</summary>
            public int PlacementOverrides;
        }

        sealed class ApplyRefused : Exception { public ApplyRefused(string m) : base(m) { } }

        sealed class Selection
        {
            public Func<long, string, bool> Override = (_, _) => false;
            /// <summary>Added objects (by this file's id) to move; null = all.</summary>
            public HashSet<long> Added = new();
            public bool Removals;
            public string ExplicitPath; // a single field asked for by name: report it even when it can't go
        }

        /// <summary>Applies every change of a placed prefab: overrides, additions and removals.</summary>
        public ApplyResult ApplyAll(long instance)
            => Apply(instance, new Selection { Override = (_, _) => true, Added = null, Removals = true });

        /// <summary>
        /// Applies one thing: an instance object's overrides (all of them, or one field's), or an
        /// object this file added to an instance (an added component, an added GameObject with its
        /// hierarchy, or a prefab placed under an instance object).
        /// </summary>
        public ApplyResult Apply(SceneObject obj, string path = null)
        {
            if (obj == null) throw new ArgumentException("no such object");
            if (obj.InInstance)
            {
                if (path == null && AdditionOf(obj) is (long outer, long added))
                    return Apply(outer, new Selection { Added = new HashSet<long> { added } });
                string p = path == null ? null : PropertyPath(path);
                return Apply(obj.Instance, new Selection
                {
                    Override = (t, mp) => t == obj.Source && (p == null || mp == p || Under(mp, p)),
                    ExplicitPath = p,
                });
            }
            if (path != null) throw new ArgumentException($"&{obj.Id} is defined by this file, not a prefab instance: it has no overrides");
            var (instance, addedId) = AdditionOf(obj)
                ?? throw new ArgumentException($"'{Describe(obj)}' is not part of a prefab instance, so there is nothing to apply");
            return Apply(instance, new Selection { Added = new HashSet<long> { addedId } });
        }

        /// <summary>When <paramref name="obj"/> is (inside) something this file added to an instance: that instance and the added object's id.</summary>
        (long Instance, long Added)? AdditionOf(SceneObject obj)
        {
            // A prefab placed under an instance object is an addition of the OUTER instance.
            if (obj.InInstance)
            {
                if (InstanceRoot(obj.Instance)?.Id != obj.Id) return null;
                long parent = Ref(InstanceDoc(obj.Instance).Body["m_Modification"]?["m_TransformParent"]);
                if (File.Find(parent) is not { Stripped: true } pd) return null;
                long outer = Ref(pd.Body["m_PrefabInstance"]);
                var rootStand = File.Documents.FirstOrDefault(d => d.Stripped && Ref(d.Body["m_PrefabInstance"]) == obj.Instance
                                                                   && Listed(outer, "m_AddedGameObjects", d.FileId));
                return rootStand == null ? null : (outer, rootStand.FileId);
            }
            var doc = File.Find(obj.Id);
            if (doc == null) return null;
            if (doc.ClassId != GO && doc.ClassId != T && doc.ClassId != RT)
            {
                // A component added to an instance object hangs off that object's stand-in.
                if (File.Find(Ref(doc.Body["m_GameObject"])) is { Stripped: true } gs)
                {
                    long inst = Ref(gs.Body["m_PrefabInstance"]);
                    if (Listed(inst, "m_AddedComponents", doc.FileId)) return (inst, doc.FileId);
                }
                // A component of an added GameObject goes with that GameObject.
                return Object(Ref(doc.Body["m_GameObject"])) is { } owner && owner.Id != obj.Id ? AdditionOf(owner) : null;
            }
            // A GameObject: walk up to the top-most object added to an instance.
            long t = doc.ClassId == GO ? TransformDocOf(doc) : doc.FileId;
            for (int guard = 0; t != 0 && guard < 256; guard++)
            {
                var td = File.Find(t);
                if (td == null) return null;
                long father = Ref(td.Body["m_Father"]);
                if (File.Find(father) is { Stripped: true } fs)
                {
                    long inst = Ref(fs.Body["m_PrefabInstance"]);
                    return Listed(inst, "m_AddedGameObjects", t) ? (inst, t) : null;
                }
                t = father;
            }
            return null;
        }

        long TransformDocOf(UnityDocument go)
        {
            foreach (var c in go.Body["m_Component"]?.Items ?? Array.Empty<YNode>())
                if (File.Find(Ref(c["component"])) is { ClassId: T or RT } td) return td.FileId;
            return 0;
        }

        bool Listed(long instance, string key, long added)
            => File.Find(instance) is { ClassId: PI } && (Modification(instance, create: false)?[key]?.Items ?? Array.Empty<YNode>())
                   .Any(e => Ref(e["addedObject"]) == added);

        string Describe(SceneObject o)
            => o.ClassId == GO ? PathOf(o) : GameObjectOf(o) is { } go ? $"{PathOf(go)} {o.TypeName}" : $"&{o.Id} {o.TypeName}";

        // ── The engine ────────────────────────────────────────────────

        ApplyResult Apply(long instance, Selection sel)
        {
            InstanceDoc(instance);
            string guid = SourceGuid(instance);
            string prefabPath = _db.PathOf(guid) ?? throw new ArgumentException($"instance &{instance}'s prefab is missing");
            if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{System.IO.Path.GetFileName(prefabPath)} is a model, not a prefab; its import settings, not a file, define it");
            var src = Graph.InstanceSource(instance) ?? throw new ArgumentException($"cannot read {prefabPath}");
            var root = InstanceRoot(instance) ?? throw new ArgumentException($"{prefabPath} has no root");
            var before = Snapshot(root);

            var res = new ApplyResult { PrefabPath = prefabPath };
            var pFile = UnityYamlFile.Load(prefabPath);
            res.Prefab = pFile;
            var p = new PrefabInstanceEditor(new UnityAssetEditor(pFile), _db, prefabPath);

            var srcRoot = src.FindPrefabRoot();
            long rootGo = srcRoot.Id;
            long rootTf = srcRoot.Body["m_Component"]?.Items.Select(c => src.Resolve(Ref(c["component"])))
                              .FirstOrDefault(id => src.Get(id) is { ClassId: T or RT }) ?? 0;
            bool IsPlacement(long target, string path)
            {
                long t = src.Resolve(target);
                return (t == rootGo && path == "m_Name")
                       || (t == rootTf && (Array.IndexOf(RectPlacementPaths, path) >= 0 || Array.IndexOf(TransformPlacementPaths, path) >= 0 || path == "m_RootOrder"));
            }

            var moved = new Dictionary<long, long>(); // this file's id → the prefab's id
            var mod = Modification(instance, create: false);

            // 1. Additions first, so overrides that refer to them can follow them into the prefab.
            foreach (var key in new[] { "m_AddedGameObjects", "m_AddedComponents" })
                foreach (var e in (mod?[key]?.Items ?? Array.Empty<YNode>()).ToList())
                {
                    long added = Ref(e["addedObject"]);
                    if (sel.Added != null && !sel.Added.Contains(added)) continue;
                    string what = DescribeDoc(added);
                    try
                    {
                        MoveAddition(p, instance, key, Ref(e["targetCorrespondingSourceObject"]), added, moved);
                        res.Applied.Add($"added {what}");
                    }
                    catch (ApplyRefused r) { res.Kept.Add($"added {what}: {r.Message}"); }
                }

            // 2. Removals.
            if (sel.Removals)
                foreach (var key in new[] { "m_RemovedComponents", "m_RemovedGameObjects" })
                {
                    if (mod?[key] is not YSeq list) continue;
                    foreach (var r in list.List.ToList())
                    {
                        long s = Ref(r);
                        var po = p.Object(s);
                        string what = $"{(key == "m_RemovedComponents" ? "component" : "object")} &{s}";
                        if (po == null) { res.Kept.Add($"removed {what}: no longer in the prefab"); continue; }
                        what = $"{(key == "m_RemovedComponents" ? "component" : "object")} {p.Describe(po)}";
                        if (po.ClassId == GO && p.Parent(po) == null) { res.Kept.Add($"removed {what}: a prefab's root cannot be removed"); continue; }
                        if (key == "m_RemovedComponents") p.RemoveComponent(po); else p.Delete(po);
                        list.List.Remove(r);
                        res.Applied.Add($"removed {what}");
                    }
                }

            // 3. Property overrides.
            if (mod?["m_Modifications"] is YSeq mods)
                foreach (var m in mods.List.Cast<YMap>().ToList())
                {
                    long target = Ref(m["target"]);
                    string path = m.Str("propertyPath") ?? "";
                    if (!sel.Override(target, path)) continue;
                    if (IsPlacement(target, path))
                    {
                        res.PlacementOverrides++;
                        if (sel.ExplicitPath != null) res.Kept.Add($".{path}: the root's name and placement belong to this copy, not the prefab");
                        continue;
                    }
                    var po = p.Object(target);
                    if (po == null) { res.Kept.Add($"&{target}.{path}: no longer in the prefab"); continue; }
                    string what = $"{p.Describe(po)} .{path}";
                    try
                    {
                        WriteIntoPrefab(p, po, path, m.Str("value") ?? "", MapRef(m["objectReference"] as YMap ?? YMap.Ref(0), instance, p, moved));
                        mods.List.Remove(m);
                        res.Applied.Add(what);
                    }
                    catch (ApplyRefused r) { res.Kept.Add($"{what}: {r.Message}"); }
                }

            // The instance must load exactly as before, now that the prefab carries its changes.
            var previous = _db.Replace(guid, pFile.Documents);
            Invalidate();
            var after = Snapshot(InstanceRoot(instance));
            if (!before.SequenceEqual(after))
            {
                _db.Restore(guid, previous);
                Invalidate();
                throw new InvalidOperationException("applying would change how this instance loads; nothing should be saved\n"
                                                    + FirstDifference(before, after));
            }
            return res;
        }

        /// <summary>Where two loads first disagree: the object, and a window around the first differing character.</summary>
        static string FirstDifference(List<string> before, List<string> after)
        {
            var a = before.Except(after).FirstOrDefault();
            var b = after.Except(before).FirstOrDefault();
            if (a == null || b == null) return $"  {before.Count} object(s) before, {after.Count} after; {(a ?? b)?.Split(' ')[0]} {(a == null ? "appeared" : "is gone")}";
            string Flat(string x) => x.Replace("\n", " ");
            a = Flat(a); b = Flat(b);
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
            int from = Math.Max(a.IndexOf(' ') + 1, i - 80);
            return $"  {a.Split(' ')[0]}\n  was: …{a[from..Math.Min(a.Length, i + 80)]}\n  now: …{b[from..Math.Min(b.Length, i + 80)]}";
        }

        string DescribeDoc(long id)
        {
            var d = File.Find(id);
            if (d == null) return $"&{id}";
            if (d.Stripped && Ref(d.Body["m_PrefabInstance"]) is var inner && File.Find(inner) is { ClassId: PI })
                return $"prefab {System.IO.Path.GetFileName(SourcePrefabPath(inner) ?? "?")} '{(InstanceRoot(inner) is { } r ? PathOf(r) : "?")}'";
            return Object(id) is { } o ? Describe(o.ClassId is T or RT ? GameObjectOf(o) : o) : $"&{id} {d.TypeName}";
        }

        /// <summary>One override's value written into the prefab's object.</summary>
        static void WriteIntoPrefab(PrefabInstanceEditor p, SceneObject po, string path, string value, YMap refNode)
        {
            if (refNode == null) throw new ApplyRefused("refers to an object outside the prefab");
            bool isRef = refNode.Long("fileID") != 0;
            if (po.InInstance)
            {
                // From a prefab nested in this one: it becomes the prefab's override of that nested instance.
                p.SetOverrideLeaf(po.Instance, po.Source, path, isRef ? refNode : new YScalar(value));
                return;
            }
            var body = p.File.Find(po.Id)?.Body ?? throw new ApplyRefused("not a document of the prefab");
            int arr = path.IndexOf(".Array.", StringComparison.Ordinal);
            if (arr > 0 && Walk(body, path[..arr]) is YScalar packed && packed.Value.Length > 0)
                throw new ApplyRefused("a packed number list; apply it in Unity");
            if (Walk(body, arr < 0 ? path : path[..arr]) == null)
                throw new ApplyRefused("the prefab's object no longer has this field");
            PrefabGraph.ApplyOverride(body, path, value, refNode, ObjRef.From(refNode));
            p.Invalidate();
        }

        /// <summary>
        /// A reference written in this file, as the prefab must write it: a reference into another
        /// asset or to nothing is unchanged; one to an object of this instance becomes that object's
        /// id in the prefab; one to something moved into the prefab follows it. Anything else is
        /// outside the prefab: null.
        /// </summary>
        YMap MapRef(YMap r, long instance, PrefabInstanceEditor p, Dictionary<long, long> moved)
        {
            if (!string.IsNullOrEmpty(r.Str("guid")) || r.Long("fileID") == 0) return (YMap)r.Clone();
            long id = r.Long("fileID");
            if (moved.TryGetValue(id, out var pid)) return YMap.Ref(pid);
            if (File.Find(id) is { Stripped: true } d && Ref(d.Body["m_PrefabInstance"]) == instance
                && p.Object(Ref(d.Body["m_CorrespondingSourceObject"])) is { } po)
                return YMap.Ref(p.StandIn(po));
            return null;
        }

        /// <summary>
        /// Moves something this file added to an instance — a component, or a GameObject with its
        /// whole hierarchy (nested prefab instances included) — into the prefab, under the prefab's
        /// copy of the object it was added to.
        /// </summary>
        void MoveAddition(PrefabInstanceEditor p, long instance, string key, long target, long added, Dictionary<long, long> moved)
        {
            var pTarget = p.Object(target) ?? throw new ApplyRefused("the object it was added to is no longer in the prefab");
            var set = new HashSet<long>();
            if (key == "m_AddedComponents") set.Add(added);
            else Editor.CollectTransform(added, set);
            var docs = File.Documents.Where(d => set.Contains(d.FileId)).ToList();

            // Ids in the prefab: instances first, so their stand-ins can take the id Unity derives.
            var map = new Dictionary<long, long>();
            var used = new HashSet<long>();
            long Fresh() { long id; do id = p.Editor.NewFileId(); while (!used.Add(id)); return id; }
            foreach (var d in docs.Where(d => d.ClassId == PI)) map[d.FileId] = Fresh();
            foreach (var d in docs.Where(d => d.Stripped))
            {
                long x = map.TryGetValue(Ref(d.Body["m_PrefabInstance"]), out var np) ? PrefabGraph.Xor(np, Ref(d.Body["m_CorrespondingSourceObject"])) : 0;
                map[d.FileId] = x != 0 && p.File.Find(x) == null && used.Add(x) ? x : Fresh();
            }
            foreach (var d in docs.Where(d => !map.ContainsKey(d.FileId))) map[d.FileId] = Fresh();

            // Copy every document, re-pointing its references into the prefab's id space.
            var copies = new List<UnityDocument>();
            foreach (var d in docs)
            {
                var body = (YMap)Remap(d.Body.Clone(), id =>
                {
                    if (map.TryGetValue(id, out var m)) return m;
                    var mr = MapRef(YMap.Ref(id), instance, p, moved);
                    return mr?.Long("fileID") ?? throw new ApplyRefused($"refers to {DescribeDoc(id)}, which is outside the prefab");
                });
                copies.Add(new UnityDocument { ClassId = d.ClassId, FileId = map[d.FileId], TypeName = d.TypeName, Stripped = d.Stripped, Body = body });
            }
            foreach (var c in copies) p.Editor.Insert(c);

            // Link it in where it was added.
            long newAdded = map[added];
            if (key == "m_AddedComponents")
            {
                long go = Ref(copies.First(c => c.FileId == newAdded).Body["m_GameObject"]);
                var gd = p.File.Find(go);
                if (gd.Stripped) p.RecordAdded(go, newAdded, "m_AddedComponents");
                else
                {
                    var entry = new YMap(); entry.Add("component", YMap.Ref(newAdded));
                    ((YSeq)gd.Body["m_Component"]).List.Add(entry);
                }
            }
            else
            {
                long parent = p.StandIn(p.TransformOf(p.GameObjectOf(pTarget)) ?? pTarget);
                var pd = p.File.Find(parent);
                if (pd.Stripped) p.RecordAdded(parent, newAdded, "m_AddedGameObjects");
                else
                {
                    if (pd.Body["m_Children"] is not YSeq kids) { kids = new YSeq(); pd.Body.Set("m_Children", kids); }
                    kids.List.Add(YMap.Ref(newAdded));
                }
            }
            p.Invalidate();
            foreach (var kv in map) moved[kv.Key] = kv.Value;

            // This file: references to what moved now reach the prefab's copy through this
            // instance's stand-ins; then the originals go (their added-object entry with them).
            // The instance's own record of the addition goes first: it is the one reference that
            // must not follow the object. Its overrides that point at it DO follow (through a stand-in).
            if (Modification(instance, create: false)?[key] is YSeq entries)
                entries.List.RemoveAll(e => Ref(e["addedObject"]) == added);
            var replace = new Dictionary<long, long>();
            foreach (var d in File.Documents.Where(d => !set.Contains(d.FileId)).ToList())
            {
                var refs = new HashSet<long>();
                CollectLocal(d.Body, refs);
                foreach (var id in refs.Where(set.Contains))
                    if (!replace.ContainsKey(id))
                    {
                        var orig = File.Find(id);
                        long pSpace = p.Object(map[id])?.Id ?? map[id];
                        replace[id] = StandIn(instance, pSpace, orig.ClassId, orig.TypeName, orig.Body);
                    }
            }
            foreach (var d in File.Documents.Where(d => !set.Contains(d.FileId)))
                Remap(d.Body, id => replace.TryGetValue(id, out var r) ? r : id);
            Editor.DeleteDocuments(set);
        }

        /// <summary>Rewrites every local reference (one with no guid) in a node through <paramref name="map"/>.</summary>
        static YNode Remap(YNode n, Func<long, long> map)
        {
            switch (n)
            {
                case YMap m when IsRef(m) && string.IsNullOrEmpty(m.Str("guid")):
                    long id = m.Long("fileID");
                    if (id == 0) return m;
                    long to = map(id);
                    return to == id ? m : YMap.Ref(to);
                case YMap m:
                    for (int i = 0; i < m.Entries.Count; i++)
                    {
                        var v = m.Entries[i].Value;
                        var nv = Remap(v, map);
                        if (!ReferenceEquals(v, nv)) m.SetAt(i, nv);
                    }
                    return m;
                case YSeq q:
                    for (int i = 0; i < q.List.Count; i++) q.List[i] = Remap(q.List[i], map);
                    return q;
                default:
                    return n;
            }
        }

        static void CollectLocal(YNode n, HashSet<long> into)
        {
            switch (n)
            {
                case YMap m when IsRef(m) && string.IsNullOrEmpty(m.Str("guid")):
                    if (m.Long("fileID") is var id and not 0) into.Add(id);
                    break;
                case YMap m:
                    foreach (var e in m.Entries) CollectLocal(e.Value, into);
                    break;
                case YSeq q:
                    foreach (var x in q.List) CollectLocal(x, into);
                    break;
            }
        }

        // ── "Loads the same" ──────────────────────────────────────────

        static readonly HashSet<string> NotCompared = new(StringComparer.Ordinal)
        {
            "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
            "m_Component", "m_Children", "m_Father",
        };

        /// <summary>
        /// Every object under <paramref name="rootPath"/> as loaded — its place, its type and its
        /// fields, with references written as what they point at — sorted, so two loads of the same
        /// content compare equal whatever ids the file uses.
        /// </summary>
        List<string> Snapshot(SceneObject root)
        {
            // The instance's own hierarchy (by parent links: a file may place the same prefab
            // several times under one name, and those copies DO change when their prefab does).
            var mine = new HashSet<long>(Subtree(root).Where(o => o.ClassId == GO).Select(o => o.Id));
            var all = GameObjects();
            var desc = new Dictionary<long, string>();
            foreach (var (go, path) in all)
            {
                desc[go.Id] = path;
                var counts = new Dictionary<string, int>();
                foreach (var c in Components(go))
                {
                    string t = TypeKey(c);
                    counts[t] = counts.GetValueOrDefault(t) + 1;
                    desc[c.Id] = $"{path}|{t}#{counts[t]}";
                }
            }
            var lines = new List<string>();
            foreach (var (go, path) in all)
            {
                if (!mine.Contains(go.Id)) continue;
                foreach (var o in Components(go).Prepend(go))
                {
                    var body = (YMap)Body(o).Clone();
                    foreach (var k in NotCompared) body.Remove(k);
                    body = (YMap)Describe(body, desc);
                    body = SortKeys(body); // a field is found by name; the order a file lists them in is not content
                    lines.Add(desc[o.Id] + " " + UnityYamlFile.FormatValue(body));
                }
            }
            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        static YMap SortKeys(YMap m)
        {
            var sorted = new YMap { Flow = m.Flow };
            foreach (var e in m.Entries.OrderBy(e => e.Key, StringComparer.Ordinal))
                sorted.Add(e.Key, e.Value is YMap c ? SortKeys(c) : e.Value is YSeq q ? SortSeq(q) : e.Value);
            return sorted;
        }

        static YSeq SortSeq(YSeq q)
        {
            var r = new YSeq { Flow = q.Flow };
            foreach (var x in q.List) r.List.Add(x is YMap m ? SortKeys(m) : x is YSeq s ? SortSeq(s) : x);
            return r;
        }

        string TypeKey(SceneObject c) => c.ClassId == 114 ? "script:" + Body(c)?["m_Script"]?.Str("guid") : c.TypeName;

        YNode Describe(YNode n, Dictionary<long, string> desc)
        {
            switch (n)
            {
                case YMap m when IsRef(m) && string.IsNullOrEmpty(m.Str("guid")):
                    // A reference to nothing and a reference to something missing load the same: null.
                    long id = m.Long("fileID");
                    return new YScalar("@" + (id != 0 && desc.TryGetValue(Graph.Resolve(id), out var d) ? d : "none"));
                case YMap m:
                    for (int i = 0; i < m.Entries.Count; i++) m.SetAt(i, Describe(m.Entries[i].Value, desc));
                    return m;
                case YSeq q:
                    for (int i = 0; i < q.List.Count; i++) q.List[i] = Describe(q.List[i], desc);
                    return q;
                default:
                    return n;
            }
        }
    }
}
