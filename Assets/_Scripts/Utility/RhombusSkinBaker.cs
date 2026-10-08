using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Turns the RHOMBIC plates of a static exploded crystal mesh into a skinned plate rig, so a
    /// <see cref="FlipWaveRig"/> can turn them. The omni crystal's body is one mesh of 122 plates; its 30
    /// rhombi are the Time element's shape family (Docs/PALETTE.md §2.10) and carry the Time crystal's flip
    /// wave. Everything else on the body stays put.
    ///
    /// The twin is the source mesh cloned (every channel, submesh and the vertex order kept), plus skin
    /// data: bone 0 is the still body - the renderer's own transform, bindpose identity, so the body draws
    /// exactly where the MeshRenderer did - and bone <c>i</c> (1..N) carries rhombus <c>i</c>, bound at its
    /// centroid. A plate is a connected component of the welded mesh; it is a RHOMBUS when it has exactly
    /// 8 distinct corners (a rhombic slab: 2 faces x 4) whose distances from its centroid come in two
    /// clearly different values (a rectangle's are all equal).
    ///
    /// Built once per source mesh and SHARED (all omni crystals pay for one bake and keep one mesh), and
    /// kept CPU-readable because <see cref="FlipWaveRig"/> measures its plates from the vertices. A reader
    /// that reasons about WHICH mesh a crystal draws (a fusion bake keyed by the FBX asset, a morph built
    /// from the cage) resolves the twin back to its source through <see cref="SourceOf"/>, the same way
    /// <see cref="CrystalEdgeArcMeshBaker.TryGetSource"/> works for the charge crystal.
    /// </summary>
    public static class RhombusSkinBaker
    {
        /// <summary>Positions are snapped to this grid when welding: the importer splits a plate's
        /// corners by normal, and welding is what reunites them into one plate.</summary>
        const float WeldGrid = 1e-4f;

        /// <summary>A rhombic slab's corners sit at two distances from its centroid (the omni's: 1 : 0.60
        /// once the slab's thickness is included); a rectangle's all sit at one. Anything past this ratio
        /// is a rhombus.</summary>
        const float RhombusDistanceRatio = 1.1f;

        /// <summary>Every twin's name ends with this, so a renderer already wearing one is recognised.</summary>
        public const string BakedSuffix = "(RhombusSkin)";

        /// <summary>A baked twin and what a renderer needs to wear it.</summary>
        public sealed class Skin
        {
            public Mesh Source;
            public Mesh Mesh;
            /// <summary>Centroid of rhombus <c>i</c> in mesh space - where bone <c>i + 1</c> sits.</summary>
            public Vector3[] PlateCentroids;
            /// <summary>The largest half long-diagonal of any rhombus: how far a turning plate can reach
            /// past the rest mesh's bounds.</summary>
            public float MaxReach;
        }

        static readonly Dictionary<Mesh, Skin> s_cache = new();

        // Enter Play Mode keeps statics across sessions (domain reload is off), so a re-exported model must
        // be baked afresh rather than served from the last session's cache.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => s_cache.Clear();

        /// <summary>The mesh a drawn twin was baked from; any other mesh is its own source.</summary>
        public static Mesh SourceOf(Mesh displayed)
        {
            if (displayed == null) return null;
            foreach (var skin in s_cache.Values)
                if (skin != null && skin.Mesh == displayed) return skin.Source;
            return displayed;
        }

        /// <summary>
        /// The skinned twin of <paramref name="source"/>, baked on first request. False, with the reason,
        /// when the source is not CPU-readable or has no rhombic plate.
        /// </summary>
        public static bool TryGetOrBake(Mesh source, out Skin skin, out string problem)
        {
            skin = null;
            if (source == null)
            {
                problem = "no mesh to skin";
                return false;
            }
            if (s_cache.TryGetValue(source, out skin) && skin != null && skin.Mesh != null)
            {
                problem = null;
                return true;
            }
            if (!source.isReadable)
            {
                problem = $"mesh '{source.name}' is not Read/Write enabled, so its rhombi cannot be found";
                return false;
            }

            skin = Bake(source, out problem);
            if (skin == null) return false;
            s_cache[source] = skin;
            return true;
        }

        /// <summary>Builds a twin, uncached (the edit-mode tests own what they bake).</summary>
        internal static Skin Bake(Mesh source, out string problem)
        {
            var vertices = source.vertices;

            // Weld positions, then join every triangle's corners: a plate is a connected component.
            var weldIds = new int[vertices.Length];
            var weldLookup = new Dictionary<Vector3Int, int>(vertices.Length);
            var weldPositions = new List<Vector3>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x / WeldGrid), Mathf.RoundToInt(v.y / WeldGrid), Mathf.RoundToInt(v.z / WeldGrid));
                if (!weldLookup.TryGetValue(key, out var id))
                {
                    id = weldLookup.Count;
                    weldLookup.Add(key, id);
                    weldPositions.Add(v);
                }
                weldIds[i] = id;
            }

            var parent = new int[weldPositions.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a)
            {
                while (parent[a] != a) a = parent[a] = parent[parent[a]];
                return a;
            }
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var tris = source.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = Find(weldIds[tris[i]]);
                    parent[Find(weldIds[tris[i + 1]])] = a;
                    parent[Find(weldIds[tris[i + 2]])] = a;
                }
            }

            // Each plate's distinct corners, in order of first appearance so bone order is stable.
            var cornersByPlate = new Dictionary<int, List<Vector3>>();
            var plateOrder = new List<int>();
            for (int w = 0; w < weldPositions.Count; w++)
            {
                int root = Find(w);
                if (!cornersByPlate.TryGetValue(root, out var corners))
                {
                    cornersByPlate[root] = corners = new List<Vector3>(8);
                    plateOrder.Add(root);
                }
                corners.Add(weldPositions[w]);
            }

            var boneOfPlate = new Dictionary<int, int>();
            var centroids = new List<Vector3>();
            float maxReach = 0f;
            foreach (int root in plateOrder)
            {
                var corners = cornersByPlate[root];
                if (corners.Count != 8) continue;
                var centroid = Vector3.zero;
                foreach (var c in corners) centroid += c;
                centroid /= corners.Count;
                float near = float.MaxValue, far = 0f;
                foreach (var c in corners)
                {
                    float d = (c - centroid).magnitude;
                    near = Mathf.Min(near, d);
                    far = Mathf.Max(far, d);
                }
                if (near <= 0f || far / near < RhombusDistanceRatio) continue;

                boneOfPlate[root] = centroids.Count + 1;
                centroids.Add(centroid);
                maxReach = Mathf.Max(maxReach, far);
            }

            if (centroids.Count == 0)
            {
                problem = $"mesh '{source.name}' has no rhombic plate (8 corners at two distances from their centroid)";
                return null;
            }

            var weights = new BoneWeight[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                boneOfPlate.TryGetValue(Find(weldIds[v]), out int bone);   // 0 = the still body
                weights[v] = new BoneWeight { boneIndex0 = bone, weight0 = 1f };
            }

            var bindposes = new Matrix4x4[centroids.Count + 1];
            bindposes[0] = Matrix4x4.identity;
            for (int i = 0; i < centroids.Count; i++) bindposes[i + 1] = Matrix4x4.Translate(-centroids[i]);

            var twin = Object.Instantiate(source);
            twin.name = $"{source.name} {BakedSuffix}";
            twin.bindposes = bindposes;
            twin.boneWeights = weights;

            problem = null;
            return new Skin { Source = source, Mesh = twin, PlateCentroids = centroids.ToArray(), MaxReach = maxReach };
        }

        /// <summary>
        /// Dresses <paramref name="renderer"/> in the skinned twin of the mesh it draws: one child bone per
        /// rhombus at its centroid, the renderer's own transform as the still root bone, and bounds widened
        /// by a plate's reach so a turning plate is never culled. Idempotent - a renderer already wearing a
        /// twin (a re-awakened or cloned crystal) is left alone. The renderer's transform is then the space
        /// the plates and the wave are measured in.
        /// </summary>
        public static bool TryDress(SkinnedMeshRenderer renderer, out string problem)
        {
            var mesh = renderer ? renderer.sharedMesh : null;
            if (!mesh)
            {
                problem = "no skinned mesh renderer, or it draws no mesh";
                return false;
            }
            if (mesh.name.EndsWith(BakedSuffix) && renderer.bones.Length == mesh.bindposes.Length)
            {
                problem = null;
                return true;
            }
            if (!TryGetOrBake(mesh, out var skin, out problem)) return false;

            var root = renderer.transform;
            var bones = new Transform[skin.PlateCentroids.Length + 1];
            bones[0] = root;
            for (int i = 0; i < skin.PlateCentroids.Length; i++)
            {
                var plate = new GameObject($"RhombusPlate {i}").transform;
                plate.gameObject.layer = root.gameObject.layer;
                plate.SetParent(root, false);
                plate.localPosition = skin.PlateCentroids[i];
                bones[i + 1] = plate;
            }

            var bounds = skin.Mesh.bounds;
            bounds.Expand(2f * skin.MaxReach);
            renderer.sharedMesh = skin.Mesh;
            renderer.bones = bones;
            renderer.rootBone = root;
            renderer.localBounds = bounds;
            return true;
        }
    }
}
