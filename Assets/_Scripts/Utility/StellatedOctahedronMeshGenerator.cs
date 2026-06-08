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
    ///     visible surface: the 8 faces of the two constituent tetrahedra. Each
    ///       big tetra face is coplanar with the 3 spike lateral faces tiling
    ///       its corners, so 8 opaque triangles render identically to the
    ///       24-spike-face stella octangula — the protruding dual tetra occludes
    ///       each face's center via the depth buffer — at ⅓ the triangle count.
    ///     volume:    V_super = 108·a·b·c
    ///                V_box   = 8·a·b·c   →  ratio 13.5
    ///                V_oct   = 36·a·b·c  →  super:oct ratio 3
    ///
    /// Containment test: A point is inside the stellation iff it lies in
    /// either constituent tetrahedron. The two tetrahedra's face planes
    /// share the same 4 linear forms (Tet B's planes are the negations of
    /// Tet A's), so containment reduces to 4 dot products plus min/max —
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

        /// <summary>
        /// Total rendered triangle count: the two constituent tetrahedra × 4
        /// faces each. Rendered opaque, these 8 triangles are pixel-identical to
        /// the geometric 24 spike faces (the dual tetra occludes each face center
        /// via the depth buffer) — see the class summary and <see cref="ContainsPointLocal"/>.
        /// </summary>
        public const int FACE_COUNT = 8;

        /// <summary>Vertex count for flat shading: <see cref="FACE_COUNT"/> × 3.</summary>
        public const int VERTEX_COUNT = FACE_COUNT * 3;

        /// <summary>
        /// Generate a flat-shaded stellated octahedron mesh for a box of the
        /// given half-extents. 8 outer triangles (the two tetrahedra), 24
        /// vertices (per-face for flat shading). Returns a new Mesh instance;
        /// callers are responsible for its lifecycle (DestroyImmediate/Destroy).
        /// </summary>
        public static Mesh Generate(Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            var mesh = new Mesh { name = "StellatedOctahedron_SuperShield" };
            PopulateMesh(mesh, halfExtents, shieldScale);
            return mesh;
        }

        /// <summary>
        /// Rewrite an existing mesh in-place. Reuses the mesh's vertex/index
        /// buffers; cheaper than allocating a new Mesh each frame — use this
        /// for lerp/morph animations.
        /// </summary>
        public static void PopulateMesh(Mesh mesh, Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            if (mesh == null) return;

            float a = halfExtents.x * shieldScale;
            float b = halfExtents.y * shieldScale;
            float c = halfExtents.z * shieldScale;

            // The stella octangula is exactly the union of two regular tetrahedra
            // inscribed in the cube of spike tips (±a, ±b, ±c): Tet A on the four
            // even-parity corners, Tet B (its dual) on the four odd-parity
            // corners. Each big tetra face is coplanar with the 3 spike lateral
            // faces tiling its corners, so the 8 tetra faces — rendered opaque —
            // reproduce the full 24-spike-face silhouette: wherever the dual tetra
            // protrudes through a face, the dual's nearer front faces occlude that
            // face's center via the depth buffer. 8 triangles for the same image
            // as 24 (3×); the matching collision model is ContainsPointLocal.
            var verts = new Vector3[VERTEX_COUNT];
            var norms = new Vector3[VERTEX_COUNT];
            var tris  = new int[VERTEX_COUNT];

            int vi = 0;

            // Tet A — even-parity corners (sx·sy·sz = +1).
            AddTetrahedron(verts, norms, tris, ref vi,
                new Vector3( a,  b,  c),
                new Vector3( a, -b, -c),
                new Vector3(-a,  b, -c),
                new Vector3(-a, -b,  c));

            // Tet B — odd-parity corners (sx·sy·sz = -1), the dual of Tet A.
            AddTetrahedron(verts, norms, tris, ref vi,
                new Vector3(-a, -b, -c),
                new Vector3(-a,  b,  c),
                new Vector3( a, -b,  c),
                new Vector3( a,  b, -c));

            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            // Normals are authored per-face for flat shading; do not recalculate.
        }

        /// <summary>
        /// Rewrite an existing mesh in-place with per-face scaling. Each of the
        /// 8 triangular faces (the two tetrahedra) is scaled around its own
        /// centroid by <paramref name="faceScale"/>:
        ///   0 → every face collapsed to a point at its center (invisible)
        ///   1 → full-size stellated octahedron (identical to <see cref="PopulateMesh"/>)
        ///
        /// Each vertex v_i on a face becomes:
        ///   centroid + faceScale · (v_i − centroid)
        ///
        /// Use this for the engage morph so the two tetrahedra "bloom" outward
        /// from their face centers rather than the whole shape growing uniformly.
        /// </summary>
        public static void PopulateMeshFaceScale(Mesh mesh, Vector3 halfExtents,
            float faceScale, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            // First build the full-size stellation, then shrink each face
            // around its centroid. Topology and winding match PopulateMesh.
            PopulateMesh(mesh, halfExtents, shieldScale);

            var verts = mesh.vertices;

            // Every 3 sequential vertices form one face.
            for (int f = 0; f < FACE_COUNT; f++)
            {
                int i0 = f * 3, i1 = i0 + 1, i2 = i0 + 2;
                Vector3 centroid = (verts[i0] + verts[i1] + verts[i2]) * (1f / 3f);
                verts[i0] = centroid + faceScale * (verts[i0] - centroid);
                verts[i1] = centroid + faceScale * (verts[i1] - centroid);
                verts[i2] = centroid + faceScale * (verts[i2] - centroid);
            }

            mesh.vertices = verts;
            mesh.RecalculateBounds();
            // Normals stay correct — direction is unchanged by uniform
            // per-face scaling from centroid.
        }

        /// <summary>
        /// Shatter variant: each face simultaneously shrinks toward its centroid
        /// AND translates outward along its face normal. Produces a "shield
        /// shards flying apart" effect when used during disengage.
        ///
        ///   faceScale:  1 → full-size face, 0 → collapsed to centroid point
        ///   faceOffset: 0 → face at original position, &gt;0 → displaced outward
        ///
        /// Each vertex v_i becomes:
        ///   centroid + faceScale · (v_i − centroid) + faceOffset · faceNormal
        /// </summary>
        public static void PopulateMeshFaceShatter(Mesh mesh, Vector3 halfExtents,
            float faceScale, float faceOffset, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            PopulateMesh(mesh, halfExtents, shieldScale);

            var verts = mesh.vertices;
            var norms = mesh.normals;

            for (int f = 0; f < FACE_COUNT; f++)
            {
                int i0 = f * 3, i1 = i0 + 1, i2 = i0 + 2;
                Vector3 centroid = (verts[i0] + verts[i1] + verts[i2]) * (1f / 3f);
                // Face normal is identical for all 3 verts (flat shaded).
                Vector3 normal = norms[i0];
                Vector3 offset = faceOffset * normal;

                verts[i0] = centroid + faceScale * (verts[i0] - centroid) + offset;
                verts[i1] = centroid + faceScale * (verts[i1] - centroid) + offset;
                verts[i2] = centroid + faceScale * (verts[i2] - centroid) + offset;
            }

            mesh.vertices = verts;
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Emit a tetrahedron's 4 outward-facing triangles (one opposite each
        /// supplied vertex). Both constituent tetrahedra are centered at the
        /// origin, so <see cref="AddOutwardFace"/> can orient every face by its
        /// centroid direction — corner vertices may be passed in any order.
        /// </summary>
        private static void AddTetrahedron(Vector3[] verts, Vector3[] norms, int[] tris, ref int vi,
                                           Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            AddOutwardFace(verts, norms, tris, ref vi, p1, p2, p3); // opposite p0
            AddOutwardFace(verts, norms, tris, ref vi, p0, p3, p2); // opposite p1
            AddOutwardFace(verts, norms, tris, ref vi, p0, p1, p3); // opposite p2
            AddOutwardFace(verts, norms, tris, ref vi, p0, p2, p1); // opposite p3
        }

        /// <summary>
        /// Add a face wound so its normal points outward. The origin is each
        /// tetra's centroid, so the face centroid points outward; if the winding
        /// disagrees, swap the last two vertices before delegating to <see cref="AddFace"/>.
        /// </summary>
        private static void AddOutwardFace(Vector3[] verts, Vector3[] norms, int[] tris, ref int vi,
                                           Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            if (Vector3.Dot(n, v0 + v1 + v2) < 0f)
                (v1, v2) = (v2, v1);

            AddFace(verts, norms, tris, ref vi, v0, v1, v2);
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
        /// Cost: 4 linear forms + min/max + 2 compares — comparable to a box
        /// AABB and cheaper than a full convex collision check.
        ///
        /// Precompute the inverses once per prism and reuse — this is the
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
