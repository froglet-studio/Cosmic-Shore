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
    ///
    /// PLATE FILTER. An exploded crystal is a set of disjoint plates, and on the omni crystal each
    /// family of plates stands for one element (20 triangular prisms = Mass, 12 pentagonal prisms =
    /// Charge — Docs/PALETTE.md §2.10). <c>plateCorners</c> keeps only the plates with exactly that
    /// many distinct corners (a k-gonal prism has 2k), so the charge discharge can be drawn on the
    /// omni's pentagons and nowhere else. 0 keeps every triangle — the charge crystal itself.
    /// The filtered bake keeps the SOURCE's model radius, so every size the shader reads stays a
    /// fraction of the crystal's radius rather than of whichever plates were kept.
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

        /// <summary>Every baked mesh's name ends with this, so a renderer already wearing one is
        /// recognised and not baked twice (a pooled crystal re-awakening on the shared mesh).</summary>
        public const string BakedSuffix = "(EdgeArcs)";

        static readonly Dictionary<(Mesh source, int plateCorners), Mesh> s_cache = new();
        static readonly Dictionary<Mesh, Mesh> s_readableTwins = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            s_cache.Clear();
            s_readableTwins.Clear();
        }

        /// <summary>
        /// A CPU-READABLE copy of a mesh a renderer is drawing, for a caller that has to read or
        /// rewrite its vertices (a crystal fusing onto a hull). A readable mesh is returned as is; a
        /// baked twin <see cref="GetOrBake"/> uploaded (and so dropped the CPU copy of) is re-baked
        /// once from its source, identical channel for channel, and cached. False for an unreadable
        /// mesh this baker did not make.
        ///
        /// It exists because the drawn twin is DELIBERATELY unreadable: a reader that only checks
        /// <c>isReadable</c> on what the renderer holds refuses every charge crystal, and a refusal
        /// that falls back to an older effect looks, on screen, exactly like that effect.
        /// </summary>
        public static bool TryGetReadable(Mesh displayed, out Mesh readable)
        {
            readable = null;
            if (displayed == null) return false;
            if (displayed.isReadable) { readable = displayed; return true; }
            if (s_readableTwins.TryGetValue(displayed, out readable) && readable != null) return true;

            foreach (var entry in s_cache)
            {
                if (entry.Value != displayed || entry.Key.source == null || !entry.Key.source.isReadable) continue;
                readable = Bake(entry.Key.source, entry.Key.plateCorners);
                s_readableTwins[displayed] = readable;
                return readable != null;
            }
            return false;
        }

        /// <summary>
        /// Returns the arc-baked twin of <paramref name="source"/>, building it on first request.
        /// Returns null (and logs once) when the source mesh is not CPU-readable — the shader is
        /// fail-safe for that case and renders the crystal body without discharges.
        /// <paramref name="plateCorners"/> &gt; 0 bakes only the plates with that many distinct
        /// corners (see PLATE FILTER above); 0 bakes the whole mesh.
        /// </summary>
        public static Mesh GetOrBake(Mesh source, int plateCorners = 0)
        {
            if (source == null) return null;
            var key = (source, plateCorners);
            if (s_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            if (!source.isReadable)
            {
                Debug.LogError(
                    $"[CrystalEdgeArcMeshBaker] '{source.name}' is not CPU-readable, so its crease " +
                    "edges cannot be baked and the charge crystal will render without discharges. " +
                    "Fix: enable Read/Write on the model importer (isReadable: 1).");
                s_cache[key] = null;
                return null;
            }

            var baked = Bake(source, plateCorners);
            // Shared and static from here on: drop the CPU copy.
            if (baked != null) baked.UploadMeshData(true);
            s_cache[key] = baked;
            return baked;
        }

        /// <summary>
        /// Builds the baked twin, still CPU-readable and uncached (callers outside
        /// <see cref="GetOrBake"/> own it — the edit-mode tests read its channels back). Returns null
        /// when <paramref name="plateCorners"/> matches no plate.
        /// </summary>
        internal static Mesh Bake(Mesh source, int plateCorners)
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

            if (plateCorners > 0)
            {
                triangleCount = KeepPlates(subTriangles, weldIds, weldLookup.Count, plateCorners);
                if (triangleCount == 0)
                {
                    Debug.LogError(
                        $"[CrystalEdgeArcMeshBaker] '{source.name}' has no plate with exactly " +
                        $"{plateCorners} corners, so nothing was baked and its discharge will not " +
                        "draw. The model was re-exported with different plates, or the renderer's " +
                        "CrystalEdgeArcs.plateCorners names the wrong shape.");
                    return null;
                }
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
                name = plateCorners > 0
                    ? $"{source.name} ({plateCorners}-corner plates) {BakedSuffix}"
                    : $"{source.name} {BakedSuffix}",
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
            return mesh;
        }

        /// <summary>
        /// Rewrites <paramref name="subTriangles"/> in place to the triangles of the plates (connected
        /// components over WELDED corners) that have exactly <paramref name="plateCorners"/> distinct
        /// corners. Welded, because the importer splits a plate's corners by face normal — a pentagonal
        /// prism arrives as 30 raw vertices and is 10 corners. Returns the kept triangle count.
        /// </summary>
        static int KeepPlates(int[][] subTriangles, int[] weldIds, int weldCount, int plateCorners)
        {
            var parent = new int[weldCount];
            for (int i = 0; i < weldCount; i++) parent[i] = i;

            int Find(int a)
            {
                while (parent[a] != a) a = parent[a] = parent[parent[a]];
                return a;
            }

            foreach (var tris in subTriangles)
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = Find(weldIds[tris[i]]);
                    parent[Find(weldIds[tris[i + 1]])] = a;
                    parent[Find(weldIds[tris[i + 2]])] = a;
                }

            var cornersPerPlate = new Dictionary<int, int>();
            for (int w = 0; w < weldCount; w++)
            {
                int root = Find(w);
                cornersPerPlate.TryGetValue(root, out int n);
                cornersPerPlate[root] = n + 1;
            }

            int kept = 0;
            for (int s = 0; s < subTriangles.Length; s++)
            {
                var tris = subTriangles[s];
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (cornersPerPlate[Find(weldIds[tris[i]])] != plateCorners) continue;
                    keep.Add(tris[i]);
                    keep.Add(tris[i + 1]);
                    keep.Add(tris[i + 2]);
                }
                subTriangles[s] = keep.ToArray();
                kept += keep.Count / 3;
            }
            return kept;
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
