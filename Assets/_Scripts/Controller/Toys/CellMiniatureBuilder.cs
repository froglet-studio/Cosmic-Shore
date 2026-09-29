using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds a <b>scale model</b> of an authored cell environment: one combined mesh sampled
    /// from the generator's real output, so a Cell Selector mini-cell shows the world it will
    /// actually create - its silhouette, its structure, and its true domain composition -
    /// rather than a decorative stand-in.
    ///
    /// How it stays cheap:
    /// <list type="bullet">
    /// <item>It reads the generator's point data (<see cref="SpawnableBase.GetTrailData"/> plus
    /// <see cref="CellEnvironmentSpawnableBase.CachedLays"/> for per-prism domain) and spawns
    /// <b>no prisms</b>. Generation is pure math; the ~97%-of-cost part of a real build is the
    /// per-prism Instantiate, which never happens here.</item>
    /// <item>It strides the lays down to a point budget and emits one small box per sample into
    /// ONE mesh with a submesh per domain - a few draw calls per model, not thousands of
    /// GameObjects.</item>
    /// <item>The caller is expected to <see cref="CellEnvironmentSpawnableBase.ReleaseGeneratedData"/>
    /// afterwards and cache the resulting <see cref="Mesh"/> instead: seven 34k-entry lay lists
    /// retained so the menu can show seven thumbnails is the wrong trade on mobile, and
    /// re-generating on load is a small fraction of the lay cost.</item>
    /// </list>
    ///
    /// Nested generators (a <see cref="SpawnableBase"/> with children) preview their own
    /// placement points rather than their descendants' prisms - the freestyle environments are
    /// all leaf nodes, so this is a graceful degradation, not a case to design around.
    /// </summary>
    public static class CellMiniatureBuilder
    {
        /// <summary>Domain submesh order. Blue is the neutral/no-team sentinel and is a real
        /// structural colour in these environments, so it gets a slot like the rest.</summary>
        static readonly Domains[] DomainSlots = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };

        /// <summary>A built model: one mesh, plus the domain each submesh should be painted with.</summary>
        public readonly struct Miniature
        {
            public readonly Mesh Mesh;
            public readonly Domains[] SubmeshDomains;

            public Miniature(Mesh mesh, Domains[] submeshDomains)
            {
                Mesh = mesh;
                SubmeshDomains = submeshDomains;
            }

            public bool IsValid => Mesh && SubmeshDomains is { Length: > 0 };
        }

        /// <summary>
        /// Generate (or reuse) <paramref name="prefab"/>'s point data and build a model of it
        /// that fits inside <paramref name="radius"/>. Returns an invalid <see cref="Miniature"/>
        /// when the generator produces nothing.
        /// </summary>
        /// <param name="pointBudget">Samples taken across the whole structure. The silhouette is
        /// what reads at thumbnail size, so a stride sample of ~1k carries it; higher just costs
        /// vertices (24 per sample).</param>
        public static Miniature Build(SpawnableBase prefab, float radius, int pointBudget,
            float signatureCoverage = 1f)
        {
            if (!prefab || radius <= 0f) return default;

            var samples = CollectSamples(prefab, Mathf.Max(16, pointBudget));
            if (samples.Count == 0) return default;

            return Assemble(samples, radius, signatureCoverage, prefab.name);
        }

        /// <summary>
        /// Build a model from lays a caller ALREADY has - the currently-loaded environment's
        /// <see cref="CellEnvironmentSpawnableBase.CachedLays"/>, or a microscene's planned prisms.
        ///
        /// This entry point <b>cannot reach <see cref="SpawnableBase.GetTrailData"/></b>, and that
        /// is the point: one generation is a full ~34k-lay pass, so anything that must never pay it
        /// (a toy-root emblem built on the frame the toybox spawns) calls this instead of
        /// <see cref="Build"/>. The restriction is enforced by the API, not by a comment.
        /// </summary>
        public static Miniature BuildFromLays(IReadOnlyList<PrismLay> lays, float radius,
            int pointBudget, float signatureCoverage, string label)
        {
            if (lays == null || lays.Count == 0 || radius <= 0f) return default;

            int budget = Mathf.Max(16, pointBudget);
            int stride = Mathf.Max(1, lays.Count / budget);
            var samples = new List<Sample>(Mathf.Min(lays.Count, budget) + 1);
            for (int i = 0; i < lays.Count; i += stride)
            {
                var lay = lays[i];
                samples.Add(new Sample(lay.Point.Position, lay.Point.Rotation, lay.Point.Scale, lay.Domain));
            }

            return Assemble(samples, radius, signatureCoverage, label);
        }

        /// <summary>The shared tail: signature filter → fit to radius → one mesh, submesh per domain.</summary>
        static Miniature Assemble(List<Sample> samples, float radius, float signatureCoverage, string label)
        {
            samples = KeepSignatureStructures(samples, signatureCoverage);
            if (samples.Count == 0) return default;

            // Fit the structure into the mini-cell: centre on its own bounds, scale the longest
            // axis to just inside the membrane rings.
            var bounds = new Bounds(samples[0].Position, Vector3.zero);
            for (int i = 1; i < samples.Count; i++) bounds.Encapsulate(samples[i].Position);
            float extent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            float fit = extent > 0.0001f ? radius * 0.82f / extent : 1f;

            // Faithful shard size (the prism's real scale, shrunk by the same factor) with a floor:
            // at a ~1/100 reduction a truthfully-scaled prism is sub-pixel, and a model you cannot
            // see is not a scale model. The floor keeps the SHAPE legible, which is what reads at
            // thumbnail size.
            float shardFloor = radius * 0.011f;

            return BuildMesh(samples, bounds.center, fit, shardFloor, label);
        }

        // ── Sampling ─────────────────────────────────────────────────────────

        readonly struct Sample
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public readonly Domains Domain;

            public Sample(Vector3 position, Quaternion rotation, Vector3 scale, Domains domain)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
                Domain = domain;
            }
        }

        static List<Sample> CollectSamples(SpawnableBase prefab, int pointBudget)
        {
            var samples = new List<Sample>(pointBudget);
            CollectInto(prefab, pointBudget, samples, depth: 0);
            return samples;
        }

        /// <summary>How deep a composite spawnable tree is followed. Safety rail, not a tuning.</summary>
        const int MaxNestingDepth = 4;

        static void CollectInto(SpawnableBase prefab, int pointBudget, List<Sample> samples, int depth)
        {
            if (!prefab) return;

            // GetTrailData drives the generation; the lay list is the richer read of the same
            // pass (per-PRISM domain, which SpawnTrailData flattens to one domain per trail).
            var trails = prefab.GetTrailData();

            if (prefab is CellEnvironmentSpawnableBase env && env.CachedLays is { Count: > 0 } lays)
            {
                int stride = Mathf.Max(1, lays.Count / pointBudget);
                for (int i = 0; i < lays.Count; i += stride)
                {
                    var lay = lays[i];
                    samples.Add(new Sample(lay.Point.Position, lay.Point.Rotation, lay.Point.Scale, lay.Domain));
                }
                return;
            }

            if (trails == null) return;

            // A COMPOSITE spawnable's own points are PLACEMENTS, not geometry - the real build
            // (SpawnableBase.SpawnChildren) spawns each child at each point, so a model that reads
            // only the parent's points shows the anchors and none of the structure. Scurry's
            // intensity-2 arena is the worked example: ConcentricLayersGenerator emits three
            // origin-anchors and every visible prism belongs to the sphere-shell CHILD, so the
            // "environment" modelled as three shards stacked at one point, i.e. nothing.
            if (prefab.NestedChildren is { Count: > 0 } nested && depth < MaxNestingDepth)
            {
                CollectComposite(prefab, trails, nested, pointBudget, samples, depth);
                return;
            }

            int total = 0;
            foreach (var trail in trails)
                if (trail?.Points != null) total += trail.Points.Length;
            if (total == 0) return;

            int fallbackStride = Mathf.Max(1, total / pointBudget);
            int index = 0;
            foreach (var trail in trails)
            {
                if (trail?.Points == null) continue;
                for (int i = 0; i < trail.Points.Length; i++, index++)
                {
                    if (index % fallbackStride != 0) continue;
                    var point = trail.Points[i];
                    samples.Add(new Sample(point.Position, point.Rotation, point.Scale, trail.Domain));
                }
            }
        }

        /// <summary>
        /// Model a composite: sample each child ONCE, then stamp those samples at every parent
        /// placement, transformed the way <c>SpawnableBase.SpawnChildren</c> transforms the child
        /// containers - including its scale normalization (scales are divided by the largest
        /// component when any exceeds 1, so leaf-style absolute scales do not balloon a child).
        /// </summary>
        static void CollectComposite(SpawnableBase prefab, SpawnTrailData[] trails,
                                     IReadOnlyList<SpawnableBase> nested, int pointBudget,
                                     List<Sample> samples, int depth)
        {
            var placements = new List<SpawnPoint>();
            float maxScaleComponent = 0f;
            foreach (var trail in trails)
            {
                if (trail?.Points == null) continue;
                foreach (var point in trail.Points)
                {
                    placements.Add(point);
                    maxScaleComponent = Mathf.Max(maxScaleComponent,
                        Mathf.Max(Mathf.Abs(point.Scale.x),
                            Mathf.Max(Mathf.Abs(point.Scale.y), Mathf.Abs(point.Scale.z))));
                }
            }
            if (placements.Count == 0) return;

            float scaleNormalizer = maxScaleComponent > 1f ? maxScaleComponent : 1f;
            int childBudget = Mathf.Max(16, pointBudget / Mathf.Max(1, placements.Count * nested.Count));

            foreach (var child in nested)
            {
                if (!child) continue;

                var childSamples = new List<Sample>(childBudget);
                CollectInto(child, childBudget, childSamples, depth + 1);
                if (childSamples.Count == 0) continue;

                foreach (var placement in placements)
                {
                    var scale = placement.Scale / scaleNormalizer;
                    foreach (var cs in childSamples)
                    {
                        samples.Add(new Sample(
                            placement.Position + placement.Rotation * Vector3.Scale(scale, cs.Position),
                            placement.Rotation * cs.Rotation,
                            Vector3.Scale(scale, cs.Scale),
                            cs.Domain));
                    }
                }
            }
        }

        // ── Signature structures ─────────────────────────────────────────────

        /// <summary>Voxels per axis used to find where an environment's mass actually lives.</summary>
        const int SignatureGridResolution = 12;

        /// <summary>
        /// Keep the environment's <b>signature structures</b> and drop its diffuse scatter.
        ///
        /// A whole cell rendered at thumbnail size is a uniform dust cloud - every world reads the
        /// same, which is exactly the failure a text label then has to paper over. So the samples
        /// are binned into a coarse voxel grid and only the densest voxels are kept, in order,
        /// until they account for <paramref name="coverage"/> of the mass: what survives is the few
        /// motifs the generator actually builds (the orrery's rings, Yggdra's trunk), at their true
        /// relative positions, with the haze between them gone. Still an honest scale model -
        /// nothing is moved, invented, or re-coloured - just the recognisable part of one.
        ///
        /// <paramref name="coverage"/> ≥ 1 keeps everything (the previous whole-world behaviour).
        /// </summary>
        static List<Sample> KeepSignatureStructures(List<Sample> samples, float coverage)
        {
            if (coverage >= 1f || samples.Count < 64) return samples;
            coverage = Mathf.Clamp(coverage, 0.05f, 1f);

            var bounds = new Bounds(samples[0].Position, Vector3.zero);
            for (int i = 1; i < samples.Count; i++) bounds.Encapsulate(samples[i].Position);
            Vector3 size = bounds.size;
            if (size.x <= 1e-4f && size.y <= 1e-4f && size.z <= 1e-4f) return samples;

            Vector3 cell = new(
                Mathf.Max(size.x, 1e-4f) / SignatureGridResolution,
                Mathf.Max(size.y, 1e-4f) / SignatureGridResolution,
                Mathf.Max(size.z, 1e-4f) / SignatureGridResolution);

            var counts = new Dictionary<long, int>();
            var keys = new long[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                long key = VoxelKey(samples[i].Position, bounds.min, cell);
                keys[i] = key;
                counts.TryGetValue(key, out int c);
                counts[key] = c + 1;
            }

            // Densest voxels first, until they hold `coverage` of the samples. Ties break on the
            // key so the same environment always yields the same icon (generation is deterministic
            // and the model is cached against the config - a wobbling icon would be a bug).
            var ranked = new List<KeyValuePair<long, int>>(counts);
            ranked.Sort((a, b) => a.Value != b.Value
                ? b.Value.CompareTo(a.Value)
                : a.Key.CompareTo(b.Key));

            int target = Mathf.CeilToInt(samples.Count * coverage);
            var kept = new HashSet<long>();
            int running = 0;
            foreach (var voxel in ranked)
            {
                kept.Add(voxel.Key);
                running += voxel.Value;
                if (running >= target) break;
            }

            var result = new List<Sample>(running);
            for (int i = 0; i < samples.Count; i++)
                if (kept.Contains(keys[i])) result.Add(samples[i]);
            return result.Count > 0 ? result : samples;
        }

        static long VoxelKey(Vector3 position, Vector3 min, Vector3 cell)
        {
            long x = (long)Mathf.Floor((position.x - min.x) / cell.x);
            long y = (long)Mathf.Floor((position.y - min.y) / cell.y);
            long z = (long)Mathf.Floor((position.z - min.z) / cell.z);
            // Grid is 12^3 with a possible edge overflow - 1024 per axis is room to spare.
            return ((x & 0x3FF) << 20) | ((y & 0x3FF) << 10) | (z & 0x3FF);
        }

        // ── Mesh assembly ────────────────────────────────────────────────────

        static Miniature BuildMesh(List<Sample> samples, Vector3 center, float fit, float shardFloor, string label)
        {
            EnsureBoxTemplate();
            int templateVerts = s_boxVertices.Length;

            var verts = new List<Vector3>(samples.Count * templateVerts);
            var normals = new List<Vector3>(samples.Count * templateVerts);
            var indicesByDomain = new List<int>[DomainSlots.Length];
            for (int d = 0; d < DomainSlots.Length; d++) indicesByDomain[d] = new List<int>();

            foreach (var sample in samples)
            {
                int slot = DomainSlot(sample.Domain);
                int baseIndex = verts.Count;

                Vector3 origin = (sample.Position - center) * fit;
                var shard = new Vector3(
                    Mathf.Max(shardFloor, Mathf.Abs(sample.Scale.x) * fit),
                    Mathf.Max(shardFloor, Mathf.Abs(sample.Scale.y) * fit),
                    Mathf.Max(shardFloor, Mathf.Abs(sample.Scale.z) * fit));

                for (int v = 0; v < templateVerts; v++)
                {
                    verts.Add(origin + sample.Rotation * Vector3.Scale(s_boxVertices[v], shard));
                    normals.Add(sample.Rotation * s_boxNormals[v]);
                }

                var target = indicesByDomain[slot];
                for (int t = 0; t < s_boxTriangles.Length; t++)
                    target.Add(baseIndex + s_boxTriangles[t]);
            }

            // Only domains that actually appear become submeshes - an empty submesh is a wasted
            // material slot and a wasted draw call.
            var usedDomains = new List<Domains>();
            var usedIndices = new List<List<int>>();
            for (int d = 0; d < DomainSlots.Length; d++)
            {
                if (indicesByDomain[d].Count == 0) continue;
                usedDomains.Add(DomainSlots[d]);
                usedIndices.Add(indicesByDomain[d]);
            }
            if (usedDomains.Count == 0) return default;

            var mesh = new Mesh { name = $"CellMiniature_{label}" };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = usedIndices.Count;
            for (int i = 0; i < usedIndices.Count; i++)
                mesh.SetTriangles(usedIndices[i], i, calculateBounds: false);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: false);

            return new Miniature(mesh, usedDomains.ToArray());
        }

        static int DomainSlot(Domains domain)
        {
            for (int i = 0; i < DomainSlots.Length; i++)
                if (DomainSlots[i] == domain) return i;
            return DomainSlots.Length - 1; // Blue - the neutral sentinel
        }

        // ── Box template ─────────────────────────────────────────────────────
        //
        // Lifted from Unity's own primitive cube rather than hand-authored: 24 vertices with
        // per-face normals and known-good winding. Hand-rolling a cube is exactly the kind of
        // thing that ships inside-out.

        static Vector3[] s_boxVertices;
        static Vector3[] s_boxNormals;
        static int[] s_boxTriangles;

        static void EnsureBoxTemplate()
        {
            if (s_boxVertices != null) return;

            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            // Unit cube: vertices at ±0.5, so Vector3.Scale(vertex, shard) yields a box whose
            // FULL extent is `shard` - the same meaning as a prism's localScale.
            s_boxVertices = mesh.vertices;
            s_boxNormals = mesh.normals;
            s_boxTriangles = mesh.triangles;
            temp.SetActive(false); // Destroy is deferred to end-of-frame; don't flash a cube at the origin
            Object.Destroy(temp);
        }
    }
}
