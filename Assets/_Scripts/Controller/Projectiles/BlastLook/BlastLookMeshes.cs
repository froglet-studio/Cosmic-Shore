using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The geometry the blast look draws (BLAST_LOOK.md): a low-poly ICOSPHERE for the hot core,
    /// the shock shell and the bomb's body, and the SHARD shapes the casing and the embers are
    /// cut from. Every mesh is built once, in code, and shared.
    ///
    /// <para><b>Facets come from the shader, not the vertices.</b> <c>CosmicShore/BlastFacet</c>
    /// and <c>CosmicShore/BlastShell</c> take each fragment's normal from the screen-space
    /// derivatives of its world position, so every triangle is one flat facet whatever normals
    /// the mesh carries. The meshes therefore share vertices and carry RADIAL normals: the bomb
    /// material's <c>_Spread</c> push rides them, exactly as the old fresnel shader's did, so a
    /// bomb keeps its shipped size (GRIZZLY_TRIGGER_BOMBS.md, "Note on size").</para>
    ///
    /// <para>Object space matches Unity's built-in sphere: radius 0.5.</para>
    /// </summary>
    public static class BlastLookMeshes
    {
        static Mesh s_core;
        static Mesh s_shell;

        /// <summary>The core and the bomb body: an icosahedron subdivided once (80 facets). Few
        /// enough facets that each one reads at the chase camera's distance.</summary>
        public static Mesh Core => s_core ? s_core : (s_core = BuildIcosphere(1, "BlastCore"));

        /// <summary>The shock shell: subdivided twice (320 facets), so the rim of a 100 u sphere
        /// is a band of facets and not a polygon.</summary>
        public static Mesh Shell => s_shell ? s_shell : (s_shell = BuildIcosphere(2, "BlastShell"));

        /// <summary>The icosahedron's twelve vertices on the unit sphere.</summary>
        internal static Vector3[] IcosahedronVertices()
        {
            const float phi = 1.618034f;
            var v = new[]
            {
                new Vector3(-1f, phi, 0f), new Vector3(1f, phi, 0f), new Vector3(-1f, -phi, 0f), new Vector3(1f, -phi, 0f),
                new Vector3(0f, -1f, phi), new Vector3(0f, 1f, phi), new Vector3(0f, -1f, -phi), new Vector3(0f, 1f, -phi),
                new Vector3(phi, 0f, -1f), new Vector3(phi, 0f, 1f), new Vector3(-phi, 0f, -1f), new Vector3(-phi, 0f, 1f),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        /// <summary>The icosahedron's twenty faces, wound so their normals point outward.</summary>
        internal static readonly int[] IcosahedronTriangles =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        /// <summary>An icosphere of radius 0.5, <paramref name="subdivisions"/> times split, every
        /// vertex on the sphere with its radial normal. Facet count is 20 x 4^subdivisions.</summary>
        public static Mesh BuildIcosphere(int subdivisions, string name)
        {
            var verts = new List<Vector3>(IcosahedronVertices());
            var tris = new List<int>(IcosahedronTriangles);
            for (int s = 0; s < subdivisions; s++)
            {
                var midCache = new Dictionary<long, int>();
                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;

                int Mid(int p, int q)
                {
                    long key = p < q ? ((long)p << 32) | (uint)q : ((long)q << 32) | (uint)p;
                    if (midCache.TryGetValue(key, out int idx)) return idx;
                    idx = verts.Count;
                    verts.Add(((verts[p] + verts[q]) * 0.5f).normalized);
                    midCache[key] = idx;
                    return idx;
                }
            }

            var normals = new Vector3[verts.Count];
            var positions = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                normals[i] = verts[i];
                positions[i] = verts[i] * 0.5f;
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── Shards ───────────────────────────────────────────────────────────

        /// <summary>Vertices of one shard: a triangular BIPYRAMID (two tips on its long axis, a
        /// triangle of three round its waist), the chunkiest closed solid with six facets. Unit
        /// length along +Z; the waist is pushed off the middle so the two ends differ.</summary>
        internal static readonly Vector3[] ShardVertices =
        {
            new(0f, 0f, 0.62f),          // front tip (the long end)
            new(0f, 0f, -0.38f),         // back tip
            new(0.42f, 0f, 0.05f),
            new(-0.24f, 0.36f, 0.05f),
            new(-0.21f, -0.33f, 0.05f),
        };

        /// <summary>The bipyramid's six faces, outward-wound: three round the front tip, three round
        /// the back.</summary>
        internal static readonly int[] ShardTriangles =
        {
            0, 2, 3, 0, 3, 4, 0, 4, 2,
            1, 3, 2, 1, 4, 3, 1, 2, 4,
        };

        public const int ShardVertexCount = 5;
        public const int ShardIndexCount = 18;
    }
}
