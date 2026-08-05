using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Generates a runtime icosphere (geodesic sphere) by recursively subdividing
    /// a regular icosahedron and projecting every vertex onto the sphere of the
    /// requested radius.
    ///
    /// Subdivision count drives the poly budget - each level quadruples the face
    /// count: level 0 = 20 tris, 1 = 80, 2 = 320, 3 = 1280. Level 2 is a good
    /// "medium-poly" default: dense enough to read as round, faceted enough that
    /// rotation is clearly visible (see <see cref="AstroLeagueBall"/>).
    ///
    /// The mesh is FLAT SHADED by default: every triangle owns its own three
    /// vertices with a single per-face normal, so a view-dependent shader (the
    /// prism fresnel rim) lights each facet distinctly and the sphere's spin is
    /// legible as it travels. Pass <c>flatShaded: false</c> for a smooth
    /// (shared-vertex) sphere instead.
    /// </summary>
    public static class IcosphereMeshGenerator
    {
        public const int DefaultSubdivisions = 2;

        /// <summary>
        /// Build a new icosphere mesh. Caller owns its lifecycle (Destroy).
        /// </summary>
        public static Mesh Generate(int subdivisions = DefaultSubdivisions, float radius = 0.5f, bool flatShaded = true)
        {
            var mesh = new Mesh { name = $"Icosphere_{subdivisions}" };
            PopulateMesh(mesh, subdivisions, radius, flatShaded);
            return mesh;
        }

        /// <summary>Rewrite an existing mesh in-place (reuses the Mesh object).</summary>
        public static void PopulateMesh(Mesh mesh, int subdivisions = DefaultSubdivisions, float radius = 0.5f, bool flatShaded = true)
        {
            if (mesh == null) return;

            // 32-bit index buffer so high subdivisions (flat-shaded vert count grows fast) never
            // overflow the default 16-bit limit. Set here (not only in Generate) so callers that
            // rewrite a Mesh in place are equally safe. Level 2 (320 tris / 960 flat verts) is fine
            // either way; this is headroom for subdivisions ≥ 5.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            subdivisions = Mathf.Clamp(subdivisions, 0, 6);

            BuildIcosahedron(out var verts, out var tris);

            var midpointCache = new Dictionary<long, int>();
            for (int s = 0; s < subdivisions; s++)
                Subdivide(verts, tris, midpointCache, radius);

            // Project the base icosahedron's 12 vertices onto the sphere too
            // (Subdivide already projects the midpoints it creates).
            for (int i = 0; i < verts.Count; i++)
                verts[i] = verts[i].normalized * radius;

            mesh.Clear();

            if (flatShaded)
                WriteFlatShaded(mesh, verts, tris);
            else
                WriteSmooth(mesh, verts, tris);

            mesh.RecalculateBounds();
        }

        // ── Base icosahedron (12 verts, 20 faces) ────────────────────────────

        static void BuildIcosahedron(out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f; // golden ratio

            verts = new List<Vector3>(12)
            {
                new(-1f,  t, 0f), new( 1f,  t, 0f), new(-1f, -t, 0f), new( 1f, -t, 0f),
                new(0f, -1f,  t), new(0f,  1f,  t), new(0f, -1f, -t), new(0f,  1f, -t),
                new( t, 0f, -1f), new( t, 0f,  1f), new(-t, 0f, -1f), new(-t, 0f,  1f),
            };

            tris = new List<int>(60)
            {
                // 5 faces around vertex 0
                0, 11, 5,  0, 5, 1,  0, 1, 7,  0, 7, 10,  0, 10, 11,
                // 5 adjacent faces
                1, 5, 9,  5, 11, 4,  11, 10, 2,  10, 7, 6,  7, 1, 8,
                // 5 faces around vertex 3
                3, 9, 4,  3, 4, 2,  3, 2, 6,  3, 6, 8,  3, 8, 9,
                // 5 adjacent faces
                4, 9, 5,  2, 4, 11,  6, 2, 10,  8, 6, 7,  9, 8, 1,
            };
        }

        // ── One subdivision pass: split each tri into 4, sharing midpoints ────

        static void Subdivide(List<Vector3> verts, List<int> tris, Dictionary<long, int> cache, float radius)
        {
            var next = new List<int>(tris.Count * 4);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = MidpointIndex(verts, cache, a, b, radius);
                int bc = MidpointIndex(verts, cache, b, c, radius);
                int ca = MidpointIndex(verts, cache, c, a, radius);

                next.Add(a);  next.Add(ab); next.Add(ca);
                next.Add(b);  next.Add(bc); next.Add(ab);
                next.Add(c);  next.Add(ca); next.Add(bc);
                next.Add(ab); next.Add(bc); next.Add(ca);
            }
            tris.Clear();
            tris.AddRange(next);
        }

        static int MidpointIndex(List<Vector3> verts, Dictionary<long, int> cache, int i0, int i1, float radius)
        {
            // Order-independent key so the two triangles sharing an edge reuse one vertex.
            long key = i0 < i1 ? ((long)i0 << 32) | (uint)i1 : ((long)i1 << 32) | (uint)i0;
            if (cache.TryGetValue(key, out int existing))
                return existing;

            Vector3 mid = ((verts[i0] + verts[i1]) * 0.5f).normalized * radius;
            int index = verts.Count;
            verts.Add(mid);
            cache[key] = index;
            return index;
        }

        // ── Mesh writers ─────────────────────────────────────────────────────

        static void WriteSmooth(Mesh mesh, List<Vector3> verts, List<int> tris)
        {
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            // Smooth normals: for a sphere centered at origin, the normal at a
            // vertex is just its (normalized) position.
            var norms = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
                norms[i] = verts[i].normalized;
            mesh.SetNormals(norms);
        }

        static void WriteFlatShaded(Mesh mesh, List<Vector3> sharedVerts, List<int> tris)
        {
            int faceCount = tris.Count / 3;
            var verts = new Vector3[faceCount * 3];
            var norms = new Vector3[faceCount * 3];
            var indices = new int[faceCount * 3];

            for (int f = 0; f < faceCount; f++)
            {
                int i0 = tris[f * 3], i1 = tris[f * 3 + 1], i2 = tris[f * 3 + 2];
                Vector3 v0 = sharedVerts[i0], v1 = sharedVerts[i1], v2 = sharedVerts[i2];

                int o = f * 3;
                verts[o] = v0; verts[o + 1] = v1; verts[o + 2] = v2;

                Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                norms[o] = n; norms[o + 1] = n; norms[o + 2] = n;

                indices[o] = o; indices[o + 1] = o + 1; indices[o + 2] = o + 2;
            }

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = indices;
        }
    }
}
