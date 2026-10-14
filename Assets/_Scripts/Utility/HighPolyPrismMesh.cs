using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The prism's solid, subdivided — the geometry the Urchin's CRADLE drapes
    /// (Docs/PRISM_ANIMATION.md §4.7.2, <c>PrismCradle.hlsl</c>) and the Rhino sword's SLICE cuts
    /// (§4.10, <c>PrismSlice.hlsl</c>: each half of a cut prism is drawn from this mesh, and its
    /// density is what bounds the cut face's rim to one grid cell).
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

        /// <summary>
        /// TEXCOORD2 on the UNWELDED variant only (<see cref="GetUnwelded"/>): the triangle's centroid
        /// in face coordinates (xy, [0, 1]) and a per-triangle random (z, [0, 1)).
        /// </summary>
        public const int TriangleUVChannel = 2;

        /// <summary>Lowest subdivision worth swapping for: below this the mesh is barely denser than the prism.</summary>
        public const int MinSubdivision = 2;

        /// <summary>
        /// Highest subdivision the cache will build. 32 is 6,534 verts / 12,288 tris — still inside
        /// the 16-bit index format, and already far past where the drape stops visibly improving.
        /// </summary>
        public const int MaxSubdivision = 32;

        static readonly Dictionary<int, Mesh> s_cache = new();
        static readonly Dictionary<int, Mesh> s_unweldedCache = new();

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

        /// <summary>
        /// The same solid at the same density with every triangle owning its own three vertices —
        /// the mesh a COLLISION BURST draws (<c>PrismCollisionBurst.hlsl</c>, Docs/PRISM_ANIMATION.md
        /// §4.11). Unwelded is what lets the surface tear along any pattern a burst kind chooses
        /// (shard borders, petal diagonals, ribbon seams) with no triangle stretched across the tear.
        /// At rest it renders exactly as <see cref="Get"/> does: same positions, same hard per-face
        /// normals, same UV0, TEXCOORD1 and tangents. It adds <see cref="TriangleUVChannel"/>, so every
        /// vertex of a triangle makes the same choice of piece. Shared and cached like
        /// <see cref="Get"/>; never mutate it.
        /// </summary>
        public static Mesh GetUnwelded(int subdivision)
        {
            subdivision = Mathf.Clamp(subdivision, MinSubdivision, MaxSubdivision);
            if (s_unweldedCache.TryGetValue(subdivision, out var mesh) && mesh != null)
                return mesh;

            mesh = new Mesh { name = $"Prism_HighPoly_Unwelded_{subdivision}" };
            PopulateUnwelded(mesh, subdivision);
            s_unweldedCache[subdivision] = mesh;
            return mesh;
        }

        /// <summary>True if <paramref name="mesh"/> is one this cache built (identity, not shape).</summary>
        public static bool IsHighPoly(Mesh mesh)
        {
            if (mesh == null) return false;
            foreach (var kv in s_cache)
                if (ReferenceEquals(kv.Value, mesh))
                    return true;
            foreach (var kv in s_unweldedCache)
                if (ReferenceEquals(kv.Value, mesh))
                    return true;
            return false;
        }

        /// <summary>Editor tooling: drop the cache so the next ask rebuilds.</summary>
        public static void InvalidateCache()
        {
            s_cache.Clear();
            s_unweldedCache.Clear();
        }

        /// <summary>
        /// Rewrite <paramref name="mesh"/> as the unwelded subdivided prism (<see cref="GetUnwelded"/>):
        /// the face grid of <see cref="Populate"/>, triangle for triangle in the same order and winding,
        /// with three vertices per triangle.
        /// </summary>
        public static void PopulateUnwelded(Mesh mesh, int subdivision)
        {
            if (mesh == null) return;
            int n = Mathf.Clamp(subdivision, MinSubdivision, MaxSubdivision);

            int triCount = n * n * 2 * 6;
            int vertCount = triCount * 3;
            var verts = new Vector3[vertCount];
            var norms = new Vector3[vertCount];
            var tangents = new Vector4[vertCount];
            var uvs = new List<Vector2>(vertCount);
            var cents = new List<Vector3>(vertCount);
            var tris = new List<Vector3>(vertCount);
            var indices = new int[vertCount];

            var corner = new Vector2[4];
            int vi = 0;
            int tri = 0;
            for (int f = 0; f < 6; f++)
            {
                Vector3 nrm = FaceNormals[f];
                Vector3 u = FaceU[f];
                Vector3 v = FaceV[f];
                Vector3 centre = nrm * HalfExtent;

                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    corner[0] = new Vector2((float)i / n, (float)j / n);
                    corner[1] = new Vector2((float)(i + 1) / n, (float)j / n);
                    corner[2] = new Vector2((float)(i + 1) / n, (float)(j + 1) / n);
                    corner[3] = new Vector2((float)i / n, (float)(j + 1) / n);

                    // (00,10,11) and (00,11,01): the welded mesh's two triangles, same winding.
                    for (int t = 0; t < 2; t++, tri++)
                    {
                        Vector2 a = corner[0];
                        Vector2 b = t == 0 ? corner[1] : corner[2];
                        Vector2 c = t == 0 ? corner[2] : corner[3];
                        Vector2 centroid = (a + b + c) / 3f;
                        float random = TriangleRandom(f, tri);
                        var triangle = new Vector3(centroid.x, centroid.y, random);

                        AddVertex(a);
                        AddVertex(b);
                        AddVertex(c);

                        void AddVertex(Vector2 uv)
                        {
                            verts[vi] = centre + u * ((uv.x - 0.5f) * 2f * HalfExtent)
                                               + v * ((uv.y - 0.5f) * 2f * HalfExtent);
                            norms[vi] = nrm;
                            tangents[vi] = new Vector4(u.x, u.y, u.z, -1f);
                            uvs.Add(uv);
                            cents.Add(centre);
                            tris.Add(triangle);
                            indices[vi] = vi;
                            vi++;
                        }
                    }
                }
            }

            mesh.Clear();
            mesh.indexFormat = vertCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = indices;
            mesh.SetUVs(FaceUVChannel, uvs);
            mesh.SetUVs(FaceCentroidUVChannel, cents);
            mesh.SetUVs(TriangleUVChannel, tris);
            mesh.SetTangents(tangents);
            mesh.RecalculateBounds();
        }

        /// <summary>A stable per-triangle random in [0, 1) — a sine hash of the indices, never a time-seeded RNG.</summary>
        static float TriangleRandom(int face, int triangle)
        {
            float x = Mathf.Sin(triangle * 12.9898f + face * 78.233f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        // The face layout both variants share: u × v == n is Unity's front-facing winding.
        static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3[] FaceU = { Vector3.up,      Vector3.forward, Vector3.forward, Vector3.right,   Vector3.right, Vector3.up };
        static readonly Vector3[] FaceV = { Vector3.forward, Vector3.up,      Vector3.right,   Vector3.forward, Vector3.up,    Vector3.right };

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
            Vector3[] fn = FaceNormals;
            Vector3[] fu = FaceU;
            Vector3[] fv = FaceV;

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
