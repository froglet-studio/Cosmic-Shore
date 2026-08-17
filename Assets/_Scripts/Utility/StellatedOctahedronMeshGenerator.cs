using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Generates a runtime stellated octahedron (Stella Octangula) mesh from
    /// box half-extents. The stellation is the compound of two regular
    /// tetrahedra interpenetrating; their intersection is the inscribed
    /// shielded octahedron (matching <see cref="OctahedronMeshGenerator"/>),
    /// and their union has spike tips at the 8 cube corners.
    ///
    /// Geometry (stellation of the circumscribing dual):
    ///   Given box half-extents (a, b, c) and shieldScale s (default 3):
    ///     inscribed octahedron vertices = { (±s·a, 0, 0), (0, ±s·b, 0), (0, 0, ±s·c) }
    ///     stellation spike tips         = { (±s·a, ±s·b, ±s·c) }   (cube corners)
    ///     visible surface: 24 outer triangular faces (3 lateral faces per
    ///       tetrahedral spike × 8 spikes; the spike base coincides with the
    ///       inscribed octahedron face and is hidden inside the union)
    ///     volume:    V_super = 108·a·b·c
    ///                V_box   = 8·a·b·c   →  ratio 13.5
    ///                V_oct   = 36·a·b·c  →  super:oct ratio 3
    ///
    /// Containment test: A point is inside the stellation iff it lies in
    /// either constituent tetrahedron. The two tetrahedra's face planes
    /// share the same 4 linear forms (Tet B's planes are the negations of
    /// Tet A's), so containment reduces to 4 dot products plus min/max -
    /// see <see cref="ContainsPointLocal"/>. Comparable cost to a box
    /// AABB check (3 abs + 3 compares) and cheaper than a convex hull.
    /// </summary>
    public static class StellatedOctahedronMeshGenerator
    {
        /// <summary>
        /// Mass ratio between the stellated octahedron super-shield and the
        /// inscribed box, assuming uniform density.
        /// V_stellated / V_box = 108·a·b·c / 8·a·b·c = 13.5
        /// </summary>
        public const float SUPER_SHIELD_TO_BOX_VOLUME_RATIO = 13.5f;

        /// <summary>
        /// Volume ratio of the stellation to its inscribed octahedron shield.
        /// V_stellated / V_oct = 108·a·b·c / 36·a·b·c = 3
        /// </summary>
        public const float SUPER_SHIELD_TO_OCTAHEDRON_VOLUME_RATIO = 3f;

        /// <summary>
        /// Scale factor applied to box half-extents to produce the inscribed
        /// octahedron's semi-axes (and equivalently the cube whose corners are
        /// the spike tips). Same value as <see cref="OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE"/>
        /// so the stellation's inscribed octahedron coincides with the
        /// circumscribing-dual octahedron shield, allowing the super-shield
        /// to be a strict superset of the shield.
        /// </summary>
        public const float CIRCUMSCRIBING_SCALE = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        /// <summary>Total visible triangle count: 8 spikes × 3 lateral faces.</summary>
        public const int FACE_COUNT = 24;

        /// <summary>Vertex count for flat shading: <see cref="FACE_COUNT"/> × 3.</summary>
        public const int VERTEX_COUNT = FACE_COUNT * 3;

        /// <summary>
        /// UV channel carrying each vertex's per-face centroid (object space) for the
        /// GPU shield morph — the SAME channel and the same meaning as
        /// <see cref="OctahedronMeshGenerator.FaceCentroidUVChannel"/>, so one shader
        /// path (PrismShieldMorph) animates both shield tiers and neither tier needs a
        /// per-prism morph mesh. Docs/PRISM_ANIMATION.md §5 B4.
        /// </summary>
        public const int FaceCentroidUVChannel = OctahedronMeshGenerator.FaceCentroidUVChannel;

        /// <summary>
        /// Generate a flat-shaded stellated octahedron mesh for a box of the
        /// given half-extents. 24 outer triangles, 72 vertices (per-face for
        /// flat shading). Returns a new Mesh instance; callers are responsible
        /// for its lifecycle (DestroyImmediate/Destroy).
        /// </summary>
        public static Mesh Generate(Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            var mesh = new Mesh { name = "StellatedOctahedron_SuperShield" };
            PopulateMesh(mesh, halfExtents, shieldScale);
            return mesh;
        }

        // Settled super-shield meshes shared across prisms, keyed by quantized geometry -
        // mirrors OctahedronMeshGenerator.GetSharedShieldMesh. Half-extents come from the
        // authored LOCAL BoxCollider size, so every same-size super-shielded prism (e.g. the
        // 240-prism Astro League edge lining) resolves to ONE mesh: one convex MeshCollider
        // cook, and settled stellations batch on the instanced render path. Entries are
        // Unity-null-checked on fetch so a stale cache rebuilds instead of returning
        // destroyed meshes.
        static readonly System.Collections.Generic.Dictionary<(long x, long y, long z, long s), Mesh>
            s_sharedShieldMeshes = new();

        /// <summary>
        /// A cache-shared full-size stellation for the given geometry. Callers must NOT
        /// destroy the returned mesh - the cache owns it for the session.
        /// </summary>
        public static Mesh GetSharedShieldMesh(Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            var key = (x: (long)Mathf.Round(halfExtents.x * 1024f),
                       y: (long)Mathf.Round(halfExtents.y * 1024f),
                       z: (long)Mathf.Round(halfExtents.z * 1024f),
                       s: (long)Mathf.Round(shieldScale * 1024f));

            if (!s_sharedShieldMeshes.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = Generate(halfExtents, shieldScale);
                mesh.name = $"StellatedOctahedron_SuperShield_Shared_{key.x}x{key.y}x{key.z}";
                s_sharedShieldMeshes[key] = mesh;
            }
            return mesh;
        }

        /// <summary>
        /// Rewrite an existing mesh in-place, complete with the per-face centroids the
        /// GPU morph reads. Called once per quantized geometry (the shared cache) and by
        /// the edit-mode previews — NOT per frame: morph animation is
        /// <c>f(clock, stamp)</c> on this same mesh, never a rebuild.
        /// </summary>
        public static void PopulateMesh(Mesh mesh, Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            if (mesh == null) return;

            float a = halfExtents.x * shieldScale;
            float b = halfExtents.y * shieldScale;
            float c = halfExtents.z * shieldScale;

            // 8 spike tetrahedra (one per octant), each contributing 3 lateral
            // outer faces. Total 24 faces, 72 unique vertices for flat shading.
            var verts = new Vector3[VERTEX_COUNT];
            var norms = new Vector3[VERTEX_COUNT];
            var cents = new List<Vector3>(VERTEX_COUNT);
            var tris  = new int[VERTEX_COUNT];

            int vi = 0;
            for (int oct = 0; oct < 8; oct++)
            {
                int sx = ((oct & 1) == 0) ? 1 : -1;
                int sy = ((oct & 2) == 0) ? 1 : -1;
                int sz = ((oct & 4) == 0) ? 1 : -1;

                Vector3 T  = new Vector3(sx * a, sy * b, sz * c);   // spike tip (cube corner)
                Vector3 Vx = new Vector3(sx * a, 0f, 0f);           // octahedron vertex on x-axis
                Vector3 Vy = new Vector3(0f, sy * b, 0f);           // y-axis
                Vector3 Vz = new Vector3(0f, 0f, sz * c);           // z-axis

                // Winding rule (mirrors OctahedronMeshGenerator's parity logic):
                //   sx·sy·sz = +1 → standard winding T → Vx → Vy etc.
                //   sx·sy·sz = -1 → flipped winding to keep outward normals
                if (sx * sy * sz > 0)
                {
                    AddFace(verts, norms, cents, tris, ref vi, T, Vx, Vy);
                    AddFace(verts, norms, cents, tris, ref vi, T, Vy, Vz);
                    AddFace(verts, norms, cents, tris, ref vi, T, Vz, Vx);
                }
                else
                {
                    AddFace(verts, norms, cents, tris, ref vi, T, Vy, Vx);
                    AddFace(verts, norms, cents, tris, ref vi, T, Vz, Vy);
                    AddFace(verts, norms, cents, tris, ref vi, T, Vx, Vz);
                }
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            // Per-face centroid, one copy per vertex — the GPU morph's only
            // non-derivable input. Written AFTER mesh.vertices so the channel is
            // sized against the new vertex count.
            mesh.SetUVs(FaceCentroidUVChannel, cents);
            mesh.RecalculateBounds();
            // Normals are authored per-face for flat shading; do not recalculate.
        }

        // NOTE (Docs/PRISM_ANIMATION.md §5 B4): PopulateMeshFaceScale /
        // PopulateMeshFaceShatter — the per-frame CPU morph rebuilds — are GONE, for
        // the same reason as their octahedron twins. Both animations are now
        // f(clock, stamp) in PrismShieldMorph_float, evaluated on THIS shared mesh
        // from the FaceCentroidUVChannel data baked above.

        private static void AddFace(Vector3[] verts, Vector3[] norms, List<Vector3> cents,
                                    int[] tris, ref int vi,
                                    Vector3 v0, Vector3 v1, Vector3 v2)
        {
            int i0 = vi, i1 = vi + 1, i2 = vi + 2;
            verts[i0] = v0; verts[i1] = v1; verts[i2] = v2;

            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            norms[i0] = n; norms[i1] = n; norms[i2] = n;

            Vector3 centroid = (v0 + v1 + v2) * (1f / 3f);
            cents.Add(centroid); cents.Add(centroid); cents.Add(centroid);

            tris[i0] = i0; tris[i1] = i1; tris[i2] = i2;
            vi += 3;
        }

        /// <summary>
        /// Branchless containment test for a point in local space relative to
        /// the stellated octahedron. The stellation is the union of two
        /// tetrahedra, and a point is inside the union iff it lies in either.
        ///
        /// In normalized local coords (u, v, w) = (x·invA, y·invB, z·invC),
        /// Tet A's 4 face planes correspond to linear forms ε·(u,v,w) where
        /// ε ∈ { (+,+,+), (+,-,-), (-,+,-), (-,-,+) }, each constrained to ≥ -1.
        /// Tet B's planes are the negations of Tet A's, equivalent to the same
        /// 4 forms constrained to ≤ +1. So the same 4 dot products serve both
        /// containment checks:
        ///
        ///   inside Tet A:        min(f1,f2,f3,f4) ≥ -1
        ///   inside Tet B:        max(f1,f2,f3,f4) ≤ +1
        ///   inside super-shield: either holds
        ///
        /// Cost: 4 linear forms + min/max + 2 compares - comparable to a box
        /// AABB and cheaper than a full convex collision check.
        ///
        /// Precompute the inverses once per prism and reuse - this is the
        /// fast path for gameplay overlap checks without a MeshCollider.
        /// </summary>
        public static bool ContainsPointLocal(Vector3 localPoint, float invA, float invB, float invC)
        {
            float u = localPoint.x * invA;
            float v = localPoint.y * invB;
            float w = localPoint.z * invC;

            // 4 linear forms covering both tetrahedra's face planes.
            float f1 =  u + v + w;
            float f2 =  u - v - w;
            float f3 = -u + v - w;
            float f4 = -u - v + w;

            float minF = Mathf.Min(Mathf.Min(f1, f2), Mathf.Min(f3, f4));
            float maxF = Mathf.Max(Mathf.Max(f1, f2), Mathf.Max(f3, f4));

            // Inside iff inside Tet A (min ≥ -1) OR inside Tet B (max ≤ +1).
            return minF >= -1f || maxF <= 1f;
        }

        /// <summary>
        /// Convenience overload taking raw half-extents. Prefer the precomputed
        /// inverse overload inside hot loops.
        /// </summary>
        public static bool ContainsPointLocal(Vector3 localPoint, Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            float invA = 1f / (shieldScale * halfExtents.x);
            float invB = 1f / (shieldScale * halfExtents.y);
            float invC = 1f / (shieldScale * halfExtents.z);
            return ContainsPointLocal(localPoint, invA, invB, invC);
        }
    }
}
