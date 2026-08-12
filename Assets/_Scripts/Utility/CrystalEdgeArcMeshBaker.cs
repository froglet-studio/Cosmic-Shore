using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Bakes the per-triangle edge data the <c>Shader Graphs/ChargeCrystal</c> shader needs to
    /// draw a plasma discharge that runs along the CREASE EDGES BETWEEN FACES — and only those.
    ///
    /// A fragment shader has no neighbourhood, so "where is the nearest edge, and which of my
    /// triangle's three edges is a real face boundary rather than a triangulation diagonal?"
    /// cannot be answered at draw time. It is answered once here, per source mesh, and carried
    /// in three UV channels:
    ///
    ///   uv1.xyz  barycentric basis: (1,0,0) / (0,1,0) / (0,0,1) per triangle corner. Requires
    ///            per-triangle vertices, so the mesh is fully unwelded.
    ///   uv2.xyz  |x| = the triangle's height from corner i to the opposite edge, expressed in
    ///            MODEL-RADIUS FRACTIONS so shader sizes are scale-independent. Multiplying by
    ///            the interpolated barycentric gives the exact distance to that edge.
    ///            SIGN &lt; 0 marks the edge as a triangulation diagonal (both adjacent triangles
    ///            coplanar) — the shader draws no bolt there.
    ///   uv3.xyz  frac() = a stable hash of the edge, identical on both triangles that share it,
    ///            so one discharge reads as one bolt rather than two unrelated halves.
    ///            &gt;= 1 flags that this triangle walks the edge against its canonical direction;
    ///            the shader mirrors its travel parameter so a bolt's head is in the same place
    ///            on both sides of the crease.
    ///
    /// Results are cached by source mesh and SHARED — a scene full of crystals bakes once and
    /// keeps one mesh, so instancing/batching is unaffected and there is no per-instance cost.
    /// </summary>
    public static class CrystalEdgeArcMeshBaker
    {
        /// <summary>Positions are snapped to this grid when matching shared edges. The importer
        /// splits vertices by normal, so a crease's two faces hold distinct vertices at the same
        /// place; welding by position is what reunites them.</summary>
        const float WeldGrid = 1e-4f;

        /// <summary>Backstop for the same-imported-face test below: two adjacent triangles that
        /// meet at less than this angle are treated as one flat face, so their shared edge is a
        /// triangulation diagonal rather than a crease.
        ///
        /// The margin is measured, not guessed. On the charge crystal 120 of the 300 prism side
        /// quads are NON-PLANAR — their two fan triangles differ by 5.21 degrees — while the
        /// shallowest genuine face-to-face dihedral in the whole model is 57.5 degrees (900 edges,
        /// range 57.5-108.2). A 1-degree test therefore drew bolts down 120 triangulation
        /// diagonals; anything between roughly 6 and 57 separates the two populations cleanly.</summary>
        const float CoplanarAngleDegrees = 20.0f;

        static readonly Dictionary<Mesh, Mesh> s_cache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => s_cache.Clear();

        /// <summary>
        /// Returns the arc-baked twin of <paramref name="source"/>, building it on first request.
        /// Returns null (and logs once) when the source mesh is not CPU-readable — the shader is
        /// fail-safe for that case and renders the crystal body without discharges.
        /// </summary>
        public static Mesh GetOrBake(Mesh source)
        {
            if (source == null) return null;
            if (s_cache.TryGetValue(source, out var cached) && cached != null) return cached;

            if (!source.isReadable)
            {
                Debug.LogError(
                    $"[CrystalEdgeArcMeshBaker] '{source.name}' is not CPU-readable, so its crease " +
                    "edges cannot be baked and the charge crystal will render without discharges. " +
                    "Fix: enable Read/Write on the model importer (isReadable: 1).");
                s_cache[source] = null;
                return null;
            }

            var baked = Bake(source);
            s_cache[source] = baked;
            return baked;
        }

        static Mesh Bake(Mesh source)
        {
            var srcVerts = source.vertices;
            var srcNormals = source.normals;
            var srcUv0 = new List<Vector2>();
            source.GetUVs(0, srcUv0);
            bool hasUv0 = srcUv0.Count == srcVerts.Length;

            // ── Pass 1: weld positions so the two faces of a crease agree on "same edge" ──
            var weldIds = new int[srcVerts.Length];
            var weldLookup = new Dictionary<Vector3Int, int>(srcVerts.Length);
            for (int i = 0; i < srcVerts.Length; i++)
            {
                var key = Quantize(srcVerts[i]);
                if (!weldLookup.TryGetValue(key, out var id))
                {
                    id = weldLookup.Count;
                    weldLookup.Add(key, id);
                }
                weldIds[i] = id;
            }

            // ── Pass 2: gather every triangle (across all submeshes) and its flat normal ──
            int subMeshCount = source.subMeshCount;
            var subTriangles = new int[subMeshCount][];
            int triangleCount = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                subTriangles[s] = source.GetTriangles(s);
                triangleCount += subTriangles[s].Length / 3;
            }

            var faceNormals = new Vector3[triangleCount];
            var faceCorners = new int[triangleCount * 3];
            int t = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                var tris = subTriangles[s];
                for (int i = 0; i < tris.Length; i += 3, t++)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    faceCorners[t * 3] = a;
                    faceCorners[t * 3 + 1] = b;
                    faceCorners[t * 3 + 2] = c;
                    faceNormals[t] = Vector3.Cross(srcVerts[b] - srcVerts[a], srcVerts[c] - srcVerts[a]).normalized;
                }
            }

            // ── Pass 3: two adjacencies ──
            // WELDED endpoints answer "which triangles meet here" across a crease, where the
            // importer has split the vertices by normal.
            // RAW endpoints answer "were these two triangles cut out of the SAME imported face"
            // — inside one face the fan triangles reference the very same vertex indices, and
            // across a face boundary they cannot, precisely because the normals differ. That is
            // the structural diagonal test; the angle threshold above is only its backstop.
            var edgeFaces = new Dictionary<long, (int first, int second, int count)>(triangleCount * 3);
            var rawEdgeCounts = new Dictionary<long, int>(triangleCount * 3);
            for (t = 0; t < triangleCount; t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    int rawJ = faceCorners[t * 3 + (e + 1) % 3];
                    int rawK = faceCorners[t * 3 + (e + 2) % 3];

                    long key = EdgeKey(weldIds[rawJ], weldIds[rawK]);
                    if (edgeFaces.TryGetValue(key, out var rec))
                        edgeFaces[key] = (rec.first, rec.count == 1 ? t : rec.second, rec.count + 1);
                    else
                        edgeFaces[key] = (t, -1, 1);

                    long rawKey = EdgeKey(rawJ, rawK);
                    rawEdgeCounts.TryGetValue(rawKey, out int rawCount);
                    rawEdgeCounts[rawKey] = rawCount + 1;
                }
            }

            // ── Pass 4: emit unwelded vertices carrying the baked channels ──
            float modelRadius = Mathf.Max(1e-6f, Max3(source.bounds.extents));
            float coplanarDot = Mathf.Cos(CoplanarAngleDegrees * Mathf.Deg2Rad);

            int vertexCount = triangleCount * 3;
            var verts = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv0 = hasUv0 ? new Vector2[vertexCount] : null;
            var bary = new Vector3[vertexCount];
            var edgeH = new Vector3[vertexCount];
            var edgeSeed = new Vector3[vertexCount];

            t = 0;
            var newSubTriangles = new int[subMeshCount][];
            int writeVertex = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                var tris = subTriangles[s];
                var outTris = new int[tris.Length];
                for (int i = 0; i < tris.Length; i += 3, t++)
                {
                    int a = faceCorners[t * 3], b = faceCorners[t * 3 + 1], c = faceCorners[t * 3 + 2];
                    Vector3 pa = srcVerts[a], pb = srcVerts[b], pc = srcVerts[c];

                    // Twice the triangle area — the numerator of every height below.
                    float doubleArea = Vector3.Cross(pb - pa, pc - pa).magnitude;

                    var h = Vector3.zero;
                    var seed = Vector3.zero;
                    for (int e = 0; e < 3; e++)
                    {
                        int j = (e + 1) % 3, k = (e + 2) % 3;
                        int vj = faceCorners[t * 3 + j], vk = faceCorners[t * 3 + k];
                        Vector3 pj = srcVerts[vj], pk = srcVerts[vk];

                        float length = (pk - pj).magnitude;
                        float height = length > 1e-9f ? doubleArea / length : 0f;
                        height /= modelRadius;

                        int wj = weldIds[vj], wk = weldIds[vk];
                        long key = EdgeKey(wj, wk);
                        var rec = edgeFaces[key];

                        // A crease is anything that is not two triangles of one flat face: an
                        // open boundary, a non-manifold fan, or two faces that meet at an angle.
                        bool sameImportedFace = rawEdgeCounts[EdgeKey(vj, vk)] > 1;
                        bool crease = !sameImportedFace &&
                                      (rec.count != 2 ||
                                       Vector3.Dot(faceNormals[rec.first], faceNormals[rec.second]) < coplanarDot);

                        h[e] = crease ? height : -height;

                        // Canonical direction = low welded id -> high welded id, so both faces
                        // of a crease agree on which end of the edge t = 0 sits at.
                        bool flipped = wj > wk;
                        seed[e] = HashEdge(key) + (flipped ? 1f : 0f);
                    }

                    for (int corner = 0; corner < 3; corner++)
                    {
                        int src = faceCorners[t * 3 + corner];
                        verts[writeVertex] = srcVerts[src];
                        normals[writeVertex] = srcNormals != null && srcNormals.Length == srcVerts.Length
                            ? srcNormals[src]
                            : faceNormals[t];
                        if (hasUv0) uv0[writeVertex] = srcUv0[src];
                        bary[writeVertex] = corner == 0 ? new Vector3(1, 0, 0)
                                          : corner == 1 ? new Vector3(0, 1, 0)
                                                        : new Vector3(0, 0, 1);
                        edgeH[writeVertex] = h;
                        edgeSeed[writeVertex] = seed;
                        outTris[i + corner] = writeVertex;
                        writeVertex++;
                    }
                }
                newSubTriangles[s] = outTris;
            }

            // A mesh with no creases means the discharge would never draw. The likeliest cause
            // is a re-export whose normals are SMOOTH rather than per-face: the importer then
            // welds neighbouring faces onto shared vertices, the same-imported-face test sees
            // every edge as internal, and the crystal silently loses its plasma.
            int creaseSlots = 0;
            for (int v = 0; v < vertexCount; v += 3)
                for (int e = 0; e < 3; e++)
                    if (edgeH[v][e] > 0f) creaseSlots++;
            if (creaseSlots == 0)
                Debug.LogError(
                    $"[CrystalEdgeArcMeshBaker] '{source.name}' baked ZERO crease edges, so the " +
                    "charge crystal will render with no discharge. Check that the model imports " +
                    "with hard (per-face) normals — smooth normals weld the faces together and " +
                    "every edge reads as a triangulation diagonal.");

            var mesh = new Mesh
            {
                name = source.name + " (EdgeArcs)",
                // Runtime-only: never let a generated mesh get serialized into a scene.
                hideFlags = HideFlags.DontSave,
                indexFormat = vertexCount > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
                vertices = verts,
                normals = normals,
                subMeshCount = subMeshCount
            };
            if (hasUv0) mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, bary);
            mesh.SetUVs(2, edgeH);
            mesh.SetUVs(3, edgeSeed);
            for (int s = 0; s < subMeshCount; s++) mesh.SetTriangles(newSubTriangles[s], s, false);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        static float Max3(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));

        static Vector3Int Quantize(Vector3 p) => new(
            Mathf.RoundToInt(p.x / WeldGrid),
            Mathf.RoundToInt(p.y / WeldGrid),
            Mathf.RoundToInt(p.z / WeldGrid));

        static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>Deterministic 0..1 hash of an edge key. Deterministic matters: the two
        /// triangles sharing a crease must land on the same value or the bolt splits.</summary>
        static float HashEdge(long key)
        {
            ulong x = (ulong)key + 0x9E3779B97F4A7C15UL;
            x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27; x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            // Strictly below 1 so the shader's ">= 1 means flipped" flag stays unambiguous.
            return (x >> 40) * (1f / 16777216f) * 0.999999f;
        }
    }
}
