using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Generates a runtime octahedron mesh from box half-extents, using the
    /// *circumscribing dual* of the box (vertices at ±3·halfExtent on each axis),
    /// so the authored box collider nests inside the resulting shape.
    ///
    /// Geometry (circumscribing dual):
    ///   Given box half-extents (a, b, c):
    ///     vertices = { (±3a, 0, 0), (0, ±3b, 0), (0, 0, ±3c) }
    ///     inequality:   |x/(3a)| + |y/(3b)| + |z/(3c)| ≤ 1
    ///     volume:       V_oct = (4/3) * (3a)(3b)(3c) = 36·a·b·c
    ///     V_box = 8·a·b·c  →  mass ratio (shielded/unshielded) = 36/8 = 4.5
    ///
    /// 8 triangular faces, 6 shared vertices. For flat shading each face is
    /// expanded to its own 3 unique vertices (24 verts, 8 tris) so normals
    /// are not smoothed across edges.
    /// </summary>
    public static class OctahedronMeshGenerator
    {
        /// <summary>
        /// Mass ratio between the circumscribing octahedron shield and the
        /// inscribed box, assuming uniform density.
        /// V_oct_circum / V_box = 36·a·b·c / 8·a·b·c = 4.5
        /// </summary>
        public const float SHIELD_TO_BOX_VOLUME_RATIO = 4.5f;

        /// <summary>
        /// Scale factor applied to box half-extents to produce the octahedron
        /// semi-axes. Factor 3 guarantees box-corner containment:
        /// a/(3a) + b/(3b) + c/(3c) = 1.
        /// </summary>
        public const float CIRCUMSCRIBING_SCALE = 3f;

        /// <summary>
        /// Generate a flat-shaded circumscribing octahedron mesh for a box of
        /// the given half-extents. Returns a new Mesh instance; callers are
        /// responsible for its lifecycle (DestroyImmediate/Destroy).
        /// </summary>
        public static Mesh Generate(Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            var mesh = new Mesh { name = "Octahedron_Shield" };
            PopulateMesh(mesh, halfExtents, shieldScale);
            return mesh;
        }

        /// <summary>
        /// Rewrite an existing mesh in-place. Reuses the mesh's vertex/index
        /// buffers and is cheaper than allocating a new Mesh each frame — use
        /// this for lerp/morph animations.
        /// </summary>
        public static void PopulateMesh(Mesh mesh, Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            if (mesh == null) return;

            float a = halfExtents.x * shieldScale;
            float b = halfExtents.y * shieldScale;
            float c = halfExtents.z * shieldScale;

            // Six shared octahedron vertices (face centers of a box scaled by shieldScale).
            Vector3 pX = new Vector3( a,  0,  0);
            Vector3 nX = new Vector3(-a,  0,  0);
            Vector3 pY = new Vector3( 0,  b,  0);
            Vector3 nY = new Vector3( 0, -b,  0);
            Vector3 pZ = new Vector3( 0,  0,  c);
            Vector3 nZ = new Vector3( 0,  0, -c);

            // 8 triangular faces, one per octant. For octant (sx,sy,sz) the
            // face vertices are (sx·pX, sy·pY, sz·pZ). Winding v_X → v_Y → v_Z
            // yields an outward-pointing normal iff sx·sy·sz == +1; otherwise
            // we swap to v_X → v_Z → v_Y to flip the normal.
            //
            // For flat shading each face owns its own 3 vertices (24 verts total).
            var verts = new Vector3[24];
            var norms = new Vector3[24];
            var tris  = new int[24];

            int vi = 0;
            // sx·sy·sz = +1 octants: standard winding (X → Y → Z)
            AddFace(verts, norms, tris, ref vi, pX, pY, pZ); // (+,+,+)
            AddFace(verts, norms, tris, ref vi, pX, nY, nZ); // (+,-,-)
            AddFace(verts, norms, tris, ref vi, nX, pY, nZ); // (-,+,-)
            AddFace(verts, norms, tris, ref vi, nX, nY, pZ); // (-,-,+)
            // sx·sy·sz = -1 octants: flipped winding (X → Z → Y)
            AddFace(verts, norms, tris, ref vi, pX, pZ, nY); // (+,-,+)
            AddFace(verts, norms, tris, ref vi, pX, nZ, pY); // (+,+,-)
            AddFace(verts, norms, tris, ref vi, nX, pZ, pY); // (-,+,+)
            AddFace(verts, norms, tris, ref vi, nX, nZ, nY); // (-,-,-)

            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            // Normals are authored per-face for flat shading; do not recalculate.
        }

        /// <summary>
        /// Rewrite an existing mesh in-place with per-face scaling. Each of
        /// the 8 triangular faces is scaled around its own centroid by
        /// <paramref name="faceScale"/>:
        ///   0 → every face collapsed to a point at its center (invisible)
        ///   1 → full-size octahedron (identical to <see cref="PopulateMesh"/>)
        ///
        /// Each vertex v_i on a face becomes:
        ///   centroid + faceScale · (v_i − centroid)
        ///
        /// Use this for the engage/disengage morph so faces "bloom" outward
        /// from their centers rather than the whole shape growing uniformly.
        /// </summary>
        public static void PopulateMeshFaceScale(Mesh mesh, Vector3 halfExtents,
            float faceScale, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            // First build the full-size octahedron, then shrink each face
            // around its centroid. We reuse the same AddFace helper so the
            // topology and winding are identical to PopulateMesh.
            PopulateMesh(mesh, halfExtents, shieldScale);

            var verts = mesh.vertices; // copy out

            // Every 3 sequential vertices form one face (24 verts, 8 faces).
            for (int f = 0; f < 8; f++)
            {
                int i0 = f * 3, i1 = i0 + 1, i2 = i0 + 2;
                Vector3 centroid = (verts[i0] + verts[i1] + verts[i2]) * (1f / 3f);
                verts[i0] = centroid + faceScale * (verts[i0] - centroid);
                verts[i1] = centroid + faceScale * (verts[i1] - centroid);
                verts[i2] = centroid + faceScale * (verts[i2] - centroid);
            }

            mesh.vertices = verts; // write back
            mesh.RecalculateBounds();
            // Normals stay correct — direction is unchanged by uniform
            // per-face scaling from centroid; only magnitude changes.
        }

        private static void AddFace(Vector3[] verts, Vector3[] norms, int[] tris, ref int vi,
                                    Vector3 v0, Vector3 v1, Vector3 v2)
        {
            int i0 = vi, i1 = vi + 1, i2 = vi + 2;
            verts[i0] = v0; verts[i1] = v1; verts[i2] = v2;

            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            norms[i0] = n; norms[i1] = n; norms[i2] = n;

            tris[i0] = i0; tris[i1] = i1; tris[i2] = i2;
            vi += 3;
        }

        /// <summary>
        /// Branchless containment test for a point in local space relative to
        /// the circumscribing octahedron. Uses the L1-norm inequality
        ///   |x|·invA + |y|·invB + |z|·invC ≤ 1
        /// where invA/B/C = 1 / (shieldScale · halfExtent).
        ///
        /// Precompute the inverses once per prism and reuse — this is the
        /// fast path for gameplay overlap checks without a MeshCollider.
        /// </summary>
        public static bool ContainsPointLocal(Vector3 localPoint, float invA, float invB, float invC)
        {
            float sum = Mathf.Abs(localPoint.x) * invA
                      + Mathf.Abs(localPoint.y) * invB
                      + Mathf.Abs(localPoint.z) * invC;
            return sum <= 1f;
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
