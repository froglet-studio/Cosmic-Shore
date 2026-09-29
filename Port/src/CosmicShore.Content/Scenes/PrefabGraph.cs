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
        /// <summary>Scene root transform order (SceneRoots document), when present.</summary>
        public readonly List<long> RootOrder = new();
        public readonly List<string> Warnings = new();

        PrefabGraph(AssetFile file) { File = file; }

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

        // ── Construction ───────────────────────────────────────────────────

        public static PrefabGraph Build(AssetDatabase db, AssetFile file)
            => new Builder(db).Build(file, 0);

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
                        var src = ObjRef.From(d.Body["m_CorrespondingSourceObject"]);
                        var pi = ObjRef.From(d.Body["m_PrefabInstance"]);
                        if (!pi.IsNull) g.Aliases[d.FileId] = Xor(pi.FileId, src.FileId);
                        continue;
                    }
                    g.Objects[d.FileId] = new GraphObject
                    {
                        Id = d.FileId, ClassId = d.ClassId, TypeName = d.TypeName, Body = d.Body, Origin = file,
                    };
                }

                foreach (var pi in instances)
                    ExpandInstance(g, pi, depth);

                return g;
            }

            PrefabGraph Source(string guid, int depth)
            {
                if (_sourceCache.TryGetValue(guid, out var cached)) return cached;
                var file = _db.Load(guid);
                PrefabGraph g = file == null ? null : Build(file, depth + 1);
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

                // 1. Copy every source object into our id space, re-mapping its local refs.
                foreach (var so in src.Objects.Values)
                {
                    if (so.Removed) continue;
                    long nid = Xor(piId, so.Id);
                    var body = (YMap)so.Body.Clone();
                    RemapLocalRefs(body, src, piId);
                    g.Objects[nid] = new GraphObject
                    {
                        Id = nid, ClassId = so.ClassId, TypeName = so.TypeName, Body = body, Origin = so.Origin,
                    };
                }
                // The source's own aliases (its stripped docs for deeper nesting) become ours too.
                foreach (var kv in src.Aliases)
                    g.Aliases[Xor(piId, kv.Key)] = Xor(piId, src.Resolve(kv.Key));

                // 2. The instance root's parent.
                var parentRef = ObjRef.From(mod["m_TransformParent"]);
                var srcRoot = src.FindPrefabRoot();
                if (srcRoot != null)
                {
                    var rootGo = g.Objects[Xor(piId, srcRoot.Id)];
                    var rootTf = FindTransformOf(g, rootGo);
                    if (rootTf != null)
                        rootTf.Body.Set("m_Father", YMap.Ref(parentRef.FileId));
                }

                // 3. Modifications.
                foreach (var m in mod["m_Modifications"]?.Items ?? Array.Empty<YNode>())
                {
                    var target = ObjRef.From(m["target"]);
                    long tid = Xor(piId, src.Resolve(target.FileId));
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
                    long id = Xor(piId, src.Resolve(ObjRef.From(r).FileId));
                    if (g.Objects.TryGetValue(id, out var o)) o.Removed = true;
                }
                foreach (var r in mod["m_RemovedGameObjects"]?.Items ?? Array.Empty<YNode>())
                {
                    long id = Xor(piId, src.Resolve(ObjRef.From(r).FileId));
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
            static void RemapLocalRefs(YNode node, PrefabGraph src, long piId)
            {
                if (node is YMap map)
                {
                    if (map.Entries.Count <= 3 && map["fileID"] is YScalar fid && map["guid"] == null)
                    {
                        if (YScalar.TryLong(fid.Value, out var f) && f != 0)
                            map.Set("fileID", new YScalar(Xor(piId, src.Resolve(f)).ToString(CultureInfo.InvariantCulture)));
                        return;
                    }
                    for (int i = 0; i < map.Entries.Count; i++) RemapLocalRefs(map.Entries[i].Value, src, piId);
                }
                else if (node is YSeq seq)
                {
                    foreach (var n in seq.List) RemapLocalRefs(n, src, piId);
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
