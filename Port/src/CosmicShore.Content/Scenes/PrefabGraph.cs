using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Content.Scenes
{
    /// <summary>One object of an expanded scene/prefab: a (possibly instance-overridden) document body.</summary>
    public sealed class GraphObject
    {
        public long Id;
        public int ClassId;
        public string TypeName;
        public YMap Body;
        /// <summary>The asset file whose id space this object's asset refs (guid refs) resolve against.</summary>
        public AssetFile Origin;
        public bool Removed;

        public override string ToString() => $"{TypeName} &{Id}";
    }

    /// <summary>
    /// A scene or prefab with every <c>PrefabInstance</c> expanded into concrete objects —
    /// Unity's "what actually loads" view of a file. Objects inside an instance get the id
    /// <c>(instanceId ^ sourceId) &amp; 0x7FFF_FFFF_FFFF_FFFF</c> (the formula Unity uses for
    /// nested-prefab object ids; stripped documents in the owning file are aliases resolved
    /// to it). Instance modifications, removed components/GameObjects and the instance's
    /// transform parent are applied; added GameObjects/components are ordinary documents of
    /// the owning file that point at stripped aliases, so they attach through resolution.
    /// Every local object reference inside a copied source object is re-mapped into the
    /// owning file's id space, so the whole graph shares ONE id space.
    /// </summary>
    public sealed class PrefabGraph
    {
        public const long IdMask = 0x7FFF_FFFF_FFFF_FFFF;

        public readonly AssetFile File;
        public readonly Dictionary<long, GraphObject> Objects = new();
        /// <summary>Stripped-document id → the concrete object id it stands for.</summary>
        public readonly Dictionary<long, long> Aliases = new();
        /// <summary>
        /// For every object a PrefabInstance of THIS file put into the graph: which instance, and
        /// its id in the instance's source prefab (the <c>target</c> fileID an override names).
        /// </summary>
        public readonly Dictionary<long, (long Instance, long Source)> InstanceOrigin = new();

        /// <summary>Scene root transform order (SceneRoots document), when present.</summary>
        public readonly List<long> RootOrder = new();
        public readonly List<string> Warnings = new();

        /// <summary>
        /// (instance, source object) → the id that object carries here. Normally the XOR id; when
        /// that collides with an object already in the graph (an authored fileID that happens to
        /// equal a derived one — Sparrow.prefab's root GameObject equals its HUD instance root's
        /// derived id), a fresh id is assigned. Unity resolves nested objects by
        /// (m_PrefabInstance, m_CorrespondingSourceObject), never by the derived number.
        /// </summary>
        readonly Dictionary<(long, long), long> _instanceIds = new();

        /// <summary>The source prefab graph a PrefabInstance of this file was expanded from.</summary>
        public PrefabGraph InstanceSource(long instanceId) => _instanceSources.TryGetValue(instanceId, out var s) ? s : null;

        /// <summary>The id the object <paramref name="sourceId"/> of instance <paramref name="instanceId"/> carries in this graph.</summary>
        public long MapInstance(long instanceId, long sourceId)
            => _instanceIds.TryGetValue((instanceId, sourceId), out var id) ? id : Xor(instanceId, sourceId);

        internal PrefabGraph(AssetFile file) { File = file; }

        /// <summary>Writes one override (a Unity property path) into a body, the way the loader applies it.</summary>
        internal static void ApplyOverride(YMap body, string path, string value, YMap refNode, ObjRef r)
            => Builder.ApplyModification(body, path, value, refNode, r);

        internal static void ApplyModificationForTests(YMap body, string path, string value, YMap refNode, ObjRef r)
            => Builder.ApplyModification(body, path, value, refNode, r);

        public long Resolve(long id)
        {
            // Aliases can chain across nesting levels.
            for (int guard = 0; guard < 16 && Aliases.TryGetValue(id, out var target); guard++)
            {
                if (target == id) break;
                id = target;
            }
            return id;
        }

        public GraphObject Get(long id)
            => Objects.TryGetValue(Resolve(id), out var o) && !o.Removed ? o : null;

        public static long Xor(long instanceId, long sourceId) => (instanceId ^ sourceId) & IdMask;

        // Per-instance source graph, so an owning stripped doc can resolve through the source's own aliases.
        readonly Dictionary<long, PrefabGraph> _instanceSources = new();
        readonly HashSet<long> _reserved = new();
        // This file's stripped documents: id → (instance, source object it stands for).
        readonly Dictionary<long, (long Instance, long Source)> _standIns = new();

        bool ForeignStandIn(long id, long instanceId, PrefabGraph src, long sourceId)
            => _standIns.TryGetValue(id, out var s) && !(s.Instance == instanceId && src.Resolve(s.Source) == sourceId);

        internal long MapInstanceResolved(long instanceId, long sourceId)
            => _instanceSources.TryGetValue(instanceId, out var src)
                ? MapInstance(instanceId, src.Resolve(sourceId))
                : Xor(instanceId, sourceId);

        /// <summary>The single root GameObject of a prefab graph (the GameObject whose transform has no parent).</summary>
        public GraphObject FindPrefabRoot()
        {
            foreach (var o in Objects.Values)
            {
                if (o.Removed || (o.ClassId != 4 && o.ClassId != 224)) continue;
                var father = ObjRef.From(o.Body["m_Father"]);
                if (father.IsNull) return Get(ObjRef.From(o.Body["m_GameObject"]).FileId);
            }
            return null;
        }

        /// <summary>
        /// Where an added component sits among its instance's additions: its index in the
        /// m_AddedComponents list of the file that added it — this one, or (deeper) a prefab nested in it.
        /// Unity shows a prefab's own additions before those a file placing it adds on top, so sort
        /// by Depth descending, then Index.
        /// </summary>
        public (int Depth, int Index) AddedComponentRank(long id) => AddedRank(this, id, 0);

        static (int Depth, int Index) AddedRank(PrefabGraph g, long id, int depth)
        {
            if (depth < 12 && g.InstanceOrigin.TryGetValue(id, out var origin) && g.InstanceSource(origin.Instance) is { } src)
                return AddedRank(src, src.Resolve(origin.Source), depth + 1);
            if (g._addedIndex == null)
            {
                g._addedIndex = new Dictionary<long, int>();
                foreach (var d in g.File.Documents)
                {
                    if (d.ClassId != 1001 || d.Body["m_Modification"]?["m_AddedComponents"] is not YSeq list) continue;
                    for (int i = 0; i < list.List.Count; i++)
                        g._addedIndex.TryAdd(ObjRef.From(list.List[i]["addedObject"]).FileId, i);
                }
            }
            return (depth, g._addedIndex.TryGetValue(id, out int at) ? at : int.MaxValue);
        }

        Dictionary<long, int> _addedIndex; // addedObject id → its index in its instance's m_AddedComponents

        // ── Construction ───────────────────────────────────────────────────

        /// <summary>
        /// Expands a scene/prefab file. A model file (FBX) has no documents; its graph is the
        /// model prefab Unity's ModelImporter generates (see <see cref="ModelPrefabGraph"/>).
        /// </summary>
        public static PrefabGraph Build(AssetDatabase db, AssetFile file)
            => file != null && AssetDatabase.IsModelPath(file.Path)
                ? ModelPrefabGraph.Build(db, file)
                : new Builder(db).Build(file, 0);

        sealed class Builder
        {
            readonly AssetDatabase _db;
            readonly Dictionary<string, PrefabGraph> _sourceCache = new(StringComparer.Ordinal);

            public Builder(AssetDatabase db) { _db = db; }

            public PrefabGraph Build(AssetFile file, int depth)
            {
                var g = new PrefabGraph(file);
                if (depth > 12) { g.Warnings.Add($"prefab nesting too deep at {file.Path}"); return g; }

                var instances = new List<UnityDocument>();
                var stripped = new List<UnityDocument>();
                foreach (var d in file.Documents)
                {
                    if (d.ClassId == 1001) { instances.Add(d); continue; }
                    if (d.ClassId == 1660057539) // SceneRoots
                    {
                        foreach (var r in d.Body["m_Roots"]?.Items ?? Array.Empty<YNode>())
                            g.RootOrder.Add(ObjRef.From(r).FileId);
                        continue;
                    }
                    if (d.Stripped)
                    {
                        stripped.Add(d);
                        continue;
                    }
                    g.Objects[d.FileId] = new GraphObject
                    {
                        Id = d.FileId, ClassId = d.ClassId, TypeName = d.TypeName, Body = d.Body, Origin = file,
                    };
                }

                // An id the file gives a stand-in belongs to the object that stand-in names. A derived
                // id can equal it for a DIFFERENT object when instance ids are close together
                // (Dolphin.prefab's jets are …001, …002, …: 2^805 = 1^806 in the low bits), and the
                // derived object must then be the one renamed — otherwise every lookup of it would
                // resolve through the stand-in to the other object.
                foreach (var d in stripped)
                    g._standIns[d.FileId] = (ObjRef.From(d.Body["m_PrefabInstance"]).FileId, ObjRef.From(d.Body["m_CorrespondingSourceObject"]).FileId);

                foreach (var pi in instances)
                    ExpandInstance(g, pi, depth);

                // Stripped documents alias the instance object they stand for — resolved through the
                // instance map, so a collision-renamed object is still found.
                foreach (var d in stripped)
                {
                    var src = ObjRef.From(d.Body["m_CorrespondingSourceObject"]);
                    var pi = ObjRef.From(d.Body["m_PrefabInstance"]);
                    if (!pi.IsNull) g.Aliases[d.FileId] = g.MapInstanceResolved(pi.FileId, src.FileId);
                }

                return g;
            }

            PrefabGraph Source(string guid, int depth)
            {
                if (_sourceCache.TryGetValue(guid, out var cached)) return cached;
                var file = _db.Load(guid);
                PrefabGraph g = file == null ? null
                    : AssetDatabase.IsModelPath(file.Path) ? ModelPrefabGraph.Build(_db, file)
                    : Build(file, depth + 1);
                _sourceCache[guid] = g;
                return g;
            }

            void ExpandInstance(PrefabGraph g, UnityDocument pi, int depth)
            {
                var mod = pi.Body["m_Modification"] as YMap ?? new YMap();
                var sourceRef = ObjRef.From(pi.Body["m_SourcePrefab"]);
                var src = sourceRef.IsNull ? null : Source(sourceRef.Guid, depth);
                if (src == null)
                {
                    g.Warnings.Add($"missing source prefab {sourceRef} for instance &{pi.FileId} in {g.File.Path}");
                    return;
                }

                long piId = pi.FileId;
                g._instanceSources[piId] = src;

                // 0. Assign every source object its id here (derived; fresh on collision).
                foreach (var so in src.Objects.Values)
                {
                    if (so.Removed) continue;
                    long nid = Xor(piId, so.Id);
                    if (g.Objects.ContainsKey(nid) || g.ForeignStandIn(nid, piId, src, so.Id))
                    {
                        long fresh = nid;
                        do fresh = (long)(((ulong)fresh * 6364136223846793005UL + 1442695040888963407UL) & (ulong)IdMask);
                        while (fresh == 0 || g.Objects.ContainsKey(fresh) || g._reserved.Contains(fresh) || g._standIns.ContainsKey(fresh));
                        g.Warnings.Add($"derived id {nid} of &{so.Id} in instance &{piId} collides in {g.File.Path}; using {fresh}");
                        nid = fresh;
                    }
                    g._instanceIds[(piId, so.Id)] = nid;
                    g._reserved.Add(nid);
                    g.InstanceOrigin[nid] = (piId, so.Id);
                }

                // 1. Copy every source object into our id space, re-mapping its local refs.
                foreach (var so in src.Objects.Values)
                {
                    if (so.Removed) continue;
                    long nid = g.MapInstance(piId, so.Id);
                    var body = (YMap)so.Body.Clone();
                    RemapLocalRefs(body, src, g, piId);
                    g.Objects[nid] = new GraphObject
                    {
                        Id = nid, ClassId = so.ClassId, TypeName = so.TypeName, Body = body, Origin = so.Origin,
                    };
                }
                // The source's own aliases (its stripped docs for deeper nesting) become ours too.
                foreach (var kv in src.Aliases)
                {
                    long key = Xor(piId, kv.Key);
                    if (!g.Objects.ContainsKey(key)) // never let an alias shadow a real object's id
                        g.Aliases[key] = g.MapInstance(piId, src.Resolve(kv.Key));
                }

                // 2. The instance root's parent.
                var parentRef = ObjRef.From(mod["m_TransformParent"]);
                var srcRoot = src.FindPrefabRoot();
                if (srcRoot != null)
                {
                    var rootGo = g.Objects[g.MapInstance(piId, srcRoot.Id)];
                    var rootTf = FindTransformOf(g, rootGo);
                    if (rootTf != null)
                        rootTf.Body.Set("m_Father", YMap.Ref(parentRef.FileId));
                }

                // 3. Modifications.
                foreach (var m in mod["m_Modifications"]?.Items ?? Array.Empty<YNode>())
                {
                    var target = ObjRef.From(m["target"]);
                    long tid = g.MapInstance(piId, src.Resolve(target.FileId));
                    if (!g.Objects.TryGetValue(tid, out var obj))
                    {
                        // Targets a component the source no longer has (stale override) — Unity ignores it too.
                        continue;
                    }
                    string path = m.Str("propertyPath");
                    if (string.IsNullOrEmpty(path)) continue;
                    var objRef = ObjRef.From(m["objectReference"]);
                    string value = m.Str("value") ?? "";
                    ApplyModification(obj.Body, path, value, m["objectReference"] as YMap, objRef);
                }

                // 4. Removed components / GameObjects.
                foreach (var r in mod["m_RemovedComponents"]?.Items ?? Array.Empty<YNode>())
                {
                    long id = g.MapInstance(piId, src.Resolve(ObjRef.From(r).FileId));
                    if (g.Objects.TryGetValue(id, out var o)) o.Removed = true;
                }
                foreach (var r in mod["m_RemovedGameObjects"]?.Items ?? Array.Empty<YNode>())
                {
                    long id = g.MapInstance(piId, src.Resolve(ObjRef.From(r).FileId));
                    if (g.Objects.TryGetValue(id, out var o)) RemoveGameObjectTree(g, o);
                }
            }

            static GraphObject FindTransformOf(PrefabGraph g, GraphObject go)
            {
                foreach (var c in go.Body["m_Component"]?.Items ?? Array.Empty<YNode>())
                {
                    var r = ObjRef.From(c["component"]);
                    if (g.Objects.TryGetValue(g.Resolve(r.FileId), out var comp) && (comp.ClassId == 4 || comp.ClassId == 224))
                        return comp;
                }
                return null;
            }

            static void RemoveGameObjectTree(PrefabGraph g, GraphObject go)
            {
                go.Removed = true;
                foreach (var c in go.Body["m_Component"]?.Items ?? Array.Empty<YNode>())
                {
                    long cid = g.Resolve(ObjRef.From(c["component"]).FileId);
                    if (!g.Objects.TryGetValue(cid, out var comp)) continue;
                    comp.Removed = true;
                    if (comp.ClassId == 4 || comp.ClassId == 224)
                        foreach (var ch in comp.Body["m_Children"]?.Items ?? Array.Empty<YNode>())
                        {
                            long tid = g.Resolve(ObjRef.From(ch).FileId);
                            if (g.Objects.TryGetValue(tid, out var childTf))
                            {
                                long goId = g.Resolve(ObjRef.From(childTf.Body["m_GameObject"]).FileId);
                                if (g.Objects.TryGetValue(goId, out var childGo)) RemoveGameObjectTree(g, childGo);
                            }
                        }
                }
            }

            // Every local ref {fileID: f} (no guid) inside a source object's body points into the
            // source file's id space; move it into ours.
            static void RemapLocalRefs(YNode node, PrefabGraph src, PrefabGraph g, long piId)
            {
                if (node is YMap map)
                {
                    if (map.Entries.Count <= 3 && map["fileID"] is YScalar fid && map["guid"] == null)
                    {
                        if (YScalar.TryLong(fid.Value, out var f) && f != 0)
                            map.Set("fileID", new YScalar(g.MapInstance(piId, src.Resolve(f)).ToString(CultureInfo.InvariantCulture)));
                        return;
                    }
                    for (int i = 0; i < map.Entries.Count; i++) RemapLocalRefs(map.Entries[i].Value, src, g, piId);
                }
                else if (node is YSeq seq)
                {
                    foreach (var n in seq.List) RemapLocalRefs(n, src, g, piId);
                }
            }

            /// <summary>Applies one <c>propertyPath</c> override (e.g. <c>m_Colors.m_NormalColor.r</c>, <c>m_Materials.Array.data[0]</c>).</summary>
            internal static void ApplyModification(YMap body, string path, string value, YMap objectReferenceNode, ObjRef objectReference)
            {
                var segs = path.Split('.');
                YNode cur = body;
                YNode parent = null;
                string parentKey = null;
                int parentIndex = -1;

                for (int s = 0; s < segs.Length; s++)
                {
                    string seg = segs[s];
                    bool last = s == segs.Length - 1;

                    if (seg == "Array" && s + 1 < segs.Length)
                    {
                        // cur must be a sequence (or become one).
                        var seq = cur as YSeq;
                        if (seq == null)
                        {
                            seq = new YSeq();
                            Replace(parent, parentKey, parentIndex, seq);
                            cur = seq;
                        }
                        string next = segs[s + 1];
                        s++;
                        if (next == "size")
                        {
                            if (YScalar.TryLong(value, out var size) && size >= 0 && size < 100000)
                            {
                                while (seq.List.Count > size) seq.List.RemoveAt(seq.List.Count - 1);
                                while (seq.List.Count < size)
                                    seq.List.Add(seq.List.Count > 0 ? seq.List[^1].Clone() : new YScalar(""));
                            }
                            return;
                        }
                        if (next.StartsWith("data[", StringComparison.Ordinal) && next.EndsWith("]", StringComparison.Ordinal)
                            && int.TryParse(next.AsSpan(5, next.Length - 6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                        {
                            while (seq.List.Count <= idx) seq.List.Add(new YScalar(""));
                            if (s == segs.Length - 1)
                            {
                                seq.List[idx] = LeafValue(seq.List[idx], value, objectReferenceNode, objectReference);
                                return;
                            }
                            parent = seq; parentKey = null; parentIndex = idx;
                            cur = seq.List[idx];
                            if (cur is not YMap) { var m = new YMap(); seq.List[idx] = m; cur = m; }
                            continue;
                        }
                        return; // unknown array accessor
                    }

                    var map = cur as YMap;
                    if (map == null)
                    {
                        map = new YMap();
                        Replace(parent, parentKey, parentIndex, map);
                        cur = map;
                    }
                    if (last)
                    {
                        map.Set(seg, LeafValue(map[seg], value, objectReferenceNode, objectReference));
                        return;
                    }
                    var child = map[seg];
                    if (child == null)
                    {
                        // Look ahead: an Array accessor next means a sequence.
                        child = s + 1 < segs.Length && segs[s + 1] == "Array" ? new YSeq() : new YMap();
                        map.Set(seg, child);
                    }
                    parent = map; parentKey = seg; parentIndex = -1;
                    cur = child;
                }
            }

            static void Replace(YNode parent, string key, int index, YNode value)
            {
                if (parent is YMap pm && key != null) pm.Set(key, value);
                else if (parent is YSeq ps && index >= 0) ps.List[index] = value;
            }

            static YNode LeafValue(YNode existing, string value, YMap objectReferenceNode, ObjRef objectReference)
            {
                bool existingIsRef = existing is YMap em && em.Has("fileID");
                if (existingIsRef || !objectReference.IsNull)
                    return objectReferenceNode != null ? objectReferenceNode.Clone() : YMap.Ref(0);
                return new YScalar(value);
            }
        }
    }

    /// <summary>Test seam over the private modification applier.</summary>
    internal static class PrefabGraphTestHooks
    {
        internal static void Apply(YMap body, string path, string value, YMap refNode, ObjRef r)
            => PrefabGraph.ApplyModificationForTests(body, path, value, refNode, r);
    }
}
