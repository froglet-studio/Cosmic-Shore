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
        /// Rewrite an existing mesh in-place. Reuses the mesh's vertex/index
        /// buffers; cheaper than allocating a new Mesh each frame - use this
        /// for lerp/morph animations.
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
                    AddFace(verts, norms, tris, ref vi, T, Vx, Vy);
                    AddFace(verts, norms, tris, ref vi, T, Vy, Vz);
                    AddFace(verts, norms, tris, ref vi, T, Vz, Vx);
                }
                else
                {
                    AddFace(verts, norms, tris, ref vi, T, Vy, Vx);
                    AddFace(verts, norms, tris, ref vi, T, Vz, Vy);
                    AddFace(verts, norms, tris, ref vi, T, Vx, Vz);
                }
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            // Normals are authored per-face for flat shading; do not recalculate.
        }

        /// <summary>
        /// Rewrite an existing mesh in-place with per-face scaling. Each of
        /// the 24 triangular faces is scaled around its own centroid by
        /// <paramref name="faceScale"/>:
        ///   0 → every face collapsed to a point at its center (invisible)
        ///   1 → full-size stellated octahedron (identical to <see cref="PopulateMesh"/>)
        ///
        /// Each vertex v_i on a face becomes:
        ///   centroid + faceScale · (v_i − centroid)
        ///
        /// Use this for the engage morph so faces "bloom" outward from their
        /// centers rather than the whole shape growing uniformly.
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
            // Normals stay correct - direction is unchanged by uniform
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
        /// Signed margin of a point in local space relative to the stellated
        /// octahedron. The stellation is the union of two tetrahedra, and a
        /// point is inside the union iff it lies in either.
        ///
        /// In normalized local coords (u, v, w) = (x·invA, y·invB, z·invC),
        /// Tet A's 4 face planes correspond to linear forms ε·(u,v,w) where
        /// ε ∈ { (+,+,+), (+,-,-), (-,+,-), (-,-,+) }, each constrained to ≥ -1.
        /// Tet B's planes are the negations of Tet A's, equivalent to the same
        /// 4 forms constrained to ≤ +1. So the same 4 dot products serve both
        /// margins:
        ///
        ///   Tet A margin:        min(f1,f2,f3,f4) + 1
        ///   Tet B margin:        1 − max(f1,f2,f3,f4)
        ///   union (super-shield) margin: max of the two
        ///
        /// Returns &gt; 0 inside, 0 on the surface, &lt; 0 outside (normalized;
        /// magnitude ∝ distance to the surface).
        ///
        /// Cost: 4 linear forms + min/max - comparable to a box AABB and
        /// cheaper than a full convex collision check.
        ///
        /// Precompute the inverses once per prism and reuse - this is the
        /// fast path for gameplay overlap checks without a MeshCollider.
        /// </summary>
        public static float SignedMarginLocal(Vector3 localPoint, float invA, float invB, float invC)
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

            // Union margin: the better of Tet A's (minF + 1) and Tet B's (1 − maxF).
            return Mathf.Max(minF + 1f, 1f - maxF);
        }

        /// <summary>
        /// Convenience overload taking raw half-extents. Prefer the precomputed
        /// inverse overload inside hot loops.
        /// </summary>
        public static float SignedMarginLocal(Vector3 localPoint, Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            float invA = 1f / (shieldScale * halfExtents.x);
            float invB = 1f / (shieldScale * halfExtents.y);
            float invC = 1f / (shieldScale * halfExtents.z);
            return SignedMarginLocal(localPoint, invA, invB, invC);
        }

        /// <summary>
        /// Branchless containment test for a point in local space relative to
        /// the stellated octahedron. Defined as
        /// <see cref="SignedMarginLocal(Vector3, float, float, float)"/> ≥ 0
        /// so margin and containment share one source of truth.
        /// </summary>
        public static bool ContainsPointLocal(Vector3 localPoint, float invA, float invB, float invC)
        {
            return SignedMarginLocal(localPoint, invA, invB, invC) >= 0f;
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

        // --- Analytic OBB overlap (Separating-Axis Test, normalized frame) ----

        // Tet A: 4 alternating cube corners in the shell's normalized frame.
        static readonly Vector3[] s_tetA =
        {
            new Vector3( 1f,  1f,  1f), new Vector3( 1f, -1f, -1f),
            new Vector3(-1f,  1f, -1f), new Vector3(-1f, -1f,  1f),
        };

        // Tet B: the other 4 cube corners.
        static readonly Vector3[] s_tetB =
        {
            new Vector3(-1f, -1f, -1f), new Vector3(-1f,  1f,  1f),
            new Vector3( 1f, -1f,  1f), new Vector3( 1f,  1f, -1f),
        };

        // The 4 tetrahedron face-normal directions (the 4 linear-form coefficient
        // vectors); identical for both tets (Tet B's planes are Tet A's negated).
        static readonly Vector3[] s_tetFaceNormals =
        {
            new Vector3( 1f,  1f,  1f), new Vector3( 1f, -1f, -1f),
            new Vector3(-1f,  1f, -1f), new Vector3(-1f, -1f,  1f),
        };

        // The 6 tetrahedron edge directions (shared by both tets, and identical to the
        // octahedron edge dirs — the tet edges are the octahedron's silhouette edges).
        static readonly Vector3[] s_tetEdgeDirs =
        {
            new Vector3(0f, 1f,  1f), new Vector3(1f, 0f,  1f), new Vector3(1f, 1f, 0f),
            new Vector3(1f, -1f, 0f), new Vector3(1f, 0f, -1f), new Vector3(0f, 1f, -1f),
        };

        /// <summary>
        /// Separating-Axis Test between the stellated octahedron (as the UNION of two
        /// tetrahedra in the shell's NORMALIZED frame) and an oriented box given IN THAT
        /// SAME frame by its center <paramref name="centerN"/> and half-edge vectors
        /// <paramref name="e1"/>/<paramref name="e2"/>/<paramref name="e3"/> (a general
        /// parallelepiped). The box overlaps the stella iff it overlaps EITHER tetrahedron,
        /// so SAT is run per tet and OR'd. <paramref name="inflate"/> grows each tet's
        /// projection interval (normalized units) for a grazing band. Returns true on overlap.
        ///
        /// Per-tet candidate axes (25): 4 tet face normals, 3 box face normals (e_i × e_j),
        /// and 18 edge crosses (6 tet edge dirs × 3 box edges).
        /// </summary>
        public static bool OverlapsBoxNormalized(Vector3 centerN, Vector3 e1, Vector3 e2, Vector3 e3, float inflate)
        {
            return OverlapsTet(s_tetA, centerN, e1, e2, e3, inflate)
                || OverlapsTet(s_tetB, centerN, e1, e2, e3, inflate);
        }

        // SAT between one tetrahedron (its 4 normalized vertices) and the OBB.
        static bool OverlapsTet(Vector3[] tetVerts, Vector3 centerN, Vector3 e1, Vector3 e2, Vector3 e3, float inflate)
        {
            const float eps = 1e-12f;

            // (a) 4 tet face normals.
            for (int i = 0; i < s_tetFaceNormals.Length; i++)
                if (SeparatedOnAxis(s_tetFaceNormals[i], tetVerts, centerN, e1, e2, e3, inflate, eps))
                    return false;

            // (b) 3 box face normals.
            if (SeparatedOnAxis(Vector3.Cross(e1, e2), tetVerts, centerN, e1, e2, e3, inflate, eps)) return false;
            if (SeparatedOnAxis(Vector3.Cross(e2, e3), tetVerts, centerN, e1, e2, e3, inflate, eps)) return false;
            if (SeparatedOnAxis(Vector3.Cross(e3, e1), tetVerts, centerN, e1, e2, e3, inflate, eps)) return false;

            // (c) 18 edge crosses.
            for (int i = 0; i < s_tetEdgeDirs.Length; i++)
            {
                Vector3 te = s_tetEdgeDirs[i];
                if (SeparatedOnAxis(Vector3.Cross(te, e1), tetVerts, centerN, e1, e2, e3, inflate, eps)) return false;
                if (SeparatedOnAxis(Vector3.Cross(te, e2), tetVerts, centerN, e1, e2, e3, inflate, eps)) return false;
                if (SeparatedOnAxis(Vector3.Cross(te, e3), tetVerts, centerN, e1, e2, e3, inflate, eps)) return false;
            }

            return true; // no separating axis ⇒ this tet overlaps the box
        }

        // True if axis `a` separates the tetrahedron (projected over its 4 verts, grown
        // by `inflate`) from the OBB (center + span e1,e2,e3). Degenerate axes never separate.
        static bool SeparatedOnAxis(Vector3 a, Vector3[] tetVerts, Vector3 centerN,
            Vector3 e1, Vector3 e2, Vector3 e3, float inflate, float eps)
        {
            if (a.sqrMagnitude < eps) return false;

            // Tetrahedron projection interval [tMin, tMax], grown by inflate.
            float tMin = float.PositiveInfinity, tMax = float.NegativeInfinity;
            for (int i = 0; i < tetVerts.Length; i++)
            {
                float p = Vector3.Dot(a, tetVerts[i]);
                if (p < tMin) tMin = p;
                if (p > tMax) tMax = p;
            }
            tMin -= inflate;
            tMax += inflate;

            // Box projection interval [cProj - boxR, cProj + boxR].
            float cProj = Vector3.Dot(a, centerN);
            float boxR = Mathf.Abs(Vector3.Dot(a, e1))
                       + Mathf.Abs(Vector3.Dot(a, e2))
                       + Mathf.Abs(Vector3.Dot(a, e3));

            return (cProj - boxR) > tMax || (cProj + boxR) < tMin;
        }
    }
}
