using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The prism's solid, subdivided — the geometry the Urchin's CRADLE drapes
    /// (Docs/PRISM_ANIMATION.md §4.7.2, <c>PrismCradle.hlsl</c>).
    ///
    /// <para><b>Why this exists.</b> A prism is 24 triangles (or 12 on the built-in cube), and a
    /// deformation is only as smooth as the surface it moves. The cradle's first two rounds moved
    /// those triangles directly and read on screen as facets hinging, which no amount of tuning
    /// fixes: there is simply not enough surface for a bend. This mesh is the SAME SOLID with
    /// ~3,000 triangles instead of 24, so the drape reads as fabric. At rest it is
    /// indistinguishable from the prism it replaces — identical extents, identical per-face flat
    /// normals, identical face-local UV0 — which is the whole requirement, because it is swapped
    /// IN while a prism is near a riding hull and swapped back OUT when it leaves.</para>
    ///
    /// <para><b>Why ONE mesh serves every prism.</b> Prisms are unit cubes of half-extent
    /// <see cref="HalfExtent"/> sized entirely by their own non-uniform <c>localScale</c> — both
    /// families the project ships (the authored <c>Prism.asset</c> and the built-in Cube) are
    /// exactly that. So a single shared, cached mesh per subdivision is correct for all of them,
    /// which is what keeps the swapped prisms batching: <c>Prism.SetRenderMeshOverride</c> hands
    /// the companion entity a SHARED mesh, never a per-prism one (CLAUDE.md ▸ the exotic-visual
    /// handoff — "fetch it from the quantized-geometry caches ... so same-size prisms batch as ONE
    /// mesh instead of a per-prism draw-call storm").</para>
    ///
    /// <para><b>The cost of that simplicity, stated.</b> Subdivision is in PARAMETER space, so a
    /// prism with a wild aspect ratio (a 60×1×1 lattice strut) gets 60× coarser tessellation along
    /// its long axis than across it. The prismscapes an Urchin actually rides — trail slabs, rail
    /// bars, ribcage struts — are modest, and the deformation is correct on any mesh anyway
    /// (<c>PrismCradle.hlsl</c> reads only world position and normal), so an under-tessellated
    /// prism is coarse, never wrong. Recorded rather than solved with a per-aspect cache.</para>
    ///
    /// <para><b>Channels.</b> UV0 is the face-local frame, the same convention
    /// <see cref="OctahedronMeshGenerator"/> authors and the only channel the exploding-prism
    /// erosion reads. TEXCOORD1 carries the FACE centroid, the shield morph's one non-derivable
    /// input — constant per face here rather than per sub-quad, which is the honest answer
    /// because a shielded prism is never draped (its render override is the octahedron, and the
    /// residency pass declines any prism that already holds one). Tangents are the face's +u
    /// axis so the mesh is well-formed for anything that reads one.</para>
    /// </summary>
    public static class HighPolyPrismMesh
    {
        /// <summary>Half-extent of both prism mesh families (authored Prism.asset and built-in Cube).</summary>
        public const float HalfExtent = 0.5f;

        /// <summary>UV0 — the face-local frame. Mirrors <see cref="OctahedronMeshGenerator.ErosionUVChannel"/>.</summary>
        public const int FaceUVChannel = 0;

        /// <summary>TEXCOORD1 — the per-face centroid. Mirrors <see cref="OctahedronMeshGenerator.FaceCentroidUVChannel"/>.</summary>
        public const int FaceCentroidUVChannel = 1;

        /// <summary>Lowest subdivision worth swapping for: below this the mesh is barely denser than the prism.</summary>
        public const int MinSubdivision = 2;

        /// <summary>
        /// Highest subdivision the cache will build. 32 is 6,534 verts / 12,288 tris — still inside
        /// the 16-bit index format, and already far past where the drape stops visibly improving.
        /// </summary>
        public const int MaxSubdivision = 32;

        static readonly Dictionary<int, Mesh> s_cache = new();

        /// <summary>
        /// The shared subdivided prism for <paramref name="subdivision"/> quads per face axis,
        /// built on first ask and cached forever. Never mutate the returned mesh: every prism the
        /// cradle has swapped is drawing it, and that sharing is what keeps them in one batch.
        /// </summary>
        public static Mesh Get(int subdivision)
        {
            subdivision = Mathf.Clamp(subdivision, MinSubdivision, MaxSubdivision);
            if (s_cache.TryGetValue(subdivision, out var mesh) && mesh != null)
                return mesh;

            mesh = new Mesh { name = $"Prism_HighPoly_{subdivision}" };
            Populate(mesh, subdivision);
            s_cache[subdivision] = mesh;
            return mesh;
        }

        /// <summary>True if <paramref name="mesh"/> is one this cache built (identity, not shape).</summary>
        public static bool IsHighPoly(Mesh mesh)
        {
            if (mesh == null) return false;
            foreach (var kv in s_cache)
                if (ReferenceEquals(kv.Value, mesh))
                    return true;
            return false;
        }

        /// <summary>Editor tooling: drop the cache so the next ask rebuilds.</summary>
        public static void InvalidateCache() => s_cache.Clear();

        /// <summary>
        /// Rewrite <paramref name="mesh"/> as the subdivided unit prism. Called once per
        /// subdivision (the cache) — never per frame: the drape is a pure function of a global
        /// uniform evaluated on THIS mesh, never a rebuild (Docs/PRISM_ANIMATION.md §4).
        /// </summary>
        public static void Populate(Mesh mesh, int subdivision)
        {
            if (mesh == null) return;
            int n = Mathf.Clamp(subdivision, MinSubdivision, MaxSubdivision);

            // Six faces, each with its own copy of the grid so normals stay HARD at the edges —
            // the prism reads as a solid with flat faces and must go on doing so at rest.
            // u × v == n is Unity's front-facing winding for the (00,10,11)/(00,11,01) pair below.
            Vector3[] fn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] fu = { Vector3.up,    Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
            Vector3[] fv = { Vector3.forward, Vector3.up,    Vector3.right,   Vector3.forward, Vector3.up,  Vector3.right };

            int perFace = (n + 1) * (n + 1);
            int vertCount = perFace * 6;
            int triCount = n * n * 2 * 6;

            var verts = new Vector3[vertCount];
            var norms = new Vector3[vertCount];
            var tangents = new Vector4[vertCount];
            var uvs = new List<Vector2>(vertCount);
            var cents = new List<Vector3>(vertCount);
            var tris = new int[triCount * 3];

            int vi = 0;
            int ti = 0;
            for (int f = 0; f < 6; f++)
            {
                Vector3 nrm = fn[f];
                Vector3 u = fu[f];
                Vector3 v = fv[f];
                Vector3 centre = nrm * HalfExtent;
                int baseIndex = vi;

                for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float su = (float)i / n;       // 0..1 across the face
                    float sv = (float)j / n;
                    verts[vi] = centre + u * ((su - 0.5f) * 2f * HalfExtent)
                                       + v * ((sv - 0.5f) * 2f * HalfExtent);
                    norms[vi] = nrm;
                    tangents[vi] = new Vector4(u.x, u.y, u.z, -1f);
                    uvs.Add(new Vector2(su, sv));
                    cents.Add(centre);
                    vi++;
                }

                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int v00 = baseIndex + j * (n + 1) + i;
                    int v10 = v00 + 1;
                    int v01 = v00 + (n + 1);
                    int v11 = v01 + 1;

                    tris[ti++] = v00; tris[ti++] = v10; tris[ti++] = v11;
                    tris[ti++] = v00; tris[ti++] = v11; tris[ti++] = v01;
                }
            }

            mesh.Clear();
            mesh.indexFormat = vertCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            // Written AFTER mesh.vertices so each channel is sized against the new vertex count.
            mesh.SetUVs(FaceCentroidUVChannel, cents);
            mesh.SetUVs(FaceUVChannel, uvs);
            mesh.SetTangents(tangents);
            mesh.RecalculateBounds();
            // Normals are authored per-face for flat shading; do not recalculate.
        }
    }
}
