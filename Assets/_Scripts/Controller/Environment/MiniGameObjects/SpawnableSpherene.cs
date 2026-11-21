using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns prisms along the edges of a geodesic polyhedron - a spherene.
    ///
    /// Starting from an icosahedron (20 triangles, 12 vertices), each triangle is
    /// recursively subdivided and projected onto the sphere. Blocks are placed along
    /// every edge, creating a cage-like lattice with icosahedral symmetry.
    ///
    /// The result looks like a Buckminster Fuller dome - or a fullerene / carbon
    /// nanotube end-cap. At subdivision 0 it's a clean icosahedron with 30 edges;
    /// at subdivision 2 it's 320 edges forming a dense geodesic sphere.
    ///
    /// The structure offers clear paths along edges with frequent junctions at vertices,
    /// rewarding both long sweeping runs and quick direction changes.
    /// </summary>
    public class SpawnableSpherene : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 4f);

        [Header("Geodesic Structure")]
        [Tooltip("Subdivision level. 0 = icosahedron (30 edges), 1 = 120 edges, 2 = 480 edges.")]
        [Range(0, 3)]
        [SerializeField] int subdivisions = 2;

        [Tooltip("Radius of the sphere.")]
        [SerializeField] float radius = 70f;

        [Tooltip("Prism blocks per edge. More = smoother curves along edges.")]
        [SerializeField] int blocksPerEdge = 8;

        [Header("Visual")]
        [SerializeField] Domains edgeDomain = Domains.Blue;
        [SerializeField] Domains vertexDomain = Domains.Gold;

        /// <summary>A vertex block is this many times an edge block on every axis.</summary>
        const float VertexBlockScale = 1.5f;

        /// <summary>The smallest axis the prism prefab lays (its PrismScaleAnimator window grows
        /// anything thinner back to this), so a flush edge block is never sized under it.</summary>
        const float MinPrismAxis = 0.5f;

        /// <summary>Edge blocks are sized to this fraction of their measured clearance, so float
        /// noise never turns two faces that touch into a clip.</summary>
        const float FlushMargin = 0.995f;

        /// <summary>One end of an edge, as an edge block near it sees it: the vertex, its block,
        /// and how much angle the edge has to its nearest sibling at that vertex.</summary>
        readonly struct EdgeEnd
        {
            public readonly Vector3 Center, Normal;
            public readonly Quaternion VertexRotation;
            public readonly float TanHalfGap;
            public EdgeEnd(Vector3 center, Vector3 normal, Quaternion vertexRotation, float tanHalfGap)
            {
                Center = center; Normal = normal; VertexRotation = vertexRotation; TanHalfGap = tanHalfGap;
            }
        }

        protected override SpawnTrailData[] GenerateTrailData()
        {
            // Generate geodesic sphere
            var (vertices, triangles) = GenerateIcosphere(subdivisions);

            // Collect unique edges
            var edges = new HashSet<(int, int)>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                AddEdge(edges, triangles[i], triangles[i + 1]);
                AddEdge(edges, triangles[i + 1], triangles[i + 2]);
                AddEdge(edges, triangles[i + 2], triangles[i]);
            }

            // Every vertex's neighbours, and its block's pose (laid below; the edge blocks that
            // meet at a vertex are sized against it).
            var neighbours = new List<int>[vertices.Count];
            for (int i = 0; i < vertices.Count; i++) neighbours[i] = new List<int>(6);
            foreach (var (a, b) in edges) { neighbours[a].Add(b); neighbours[b].Add(a); }
            var vertexRotations = new Quaternion[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 position = vertices[i] * radius;
                vertexRotations[i] = SpawnPoint.LookRotation(position, position + vertices[i], Vector3.up); // look outward
            }

            // Build edge trail points
            var edgePoints = new List<SpawnPoint>();
            foreach (var (a, b) in edges)
            {
                Vector3 start = vertices[a] * radius;
                Vector3 end = vertices[b] * radius;
                var endA = new EdgeEnd(start, vertices[a], vertexRotations[a], TanHalfGap(vertices, neighbours, a, b));
                var endB = new EdgeEnd(end, vertices[b], vertexRotations[b], TanHalfGap(vertices, neighbours, b, a));

                for (int i = 0; i < blocksPerEdge; i++)
                {
                    // Each block sits at the MIDDLE of its segment. Laid at the segment's start, the
                    // first block of every edge landed exactly on a vertex - five or six edge blocks
                    // plus the vertex block stacked in one place at every vertex of the sphere,
                    // 5,047 coincident pairs in the shipped prefab - so the vertex is the vertex
                    // block's alone.
                    float t = (float)i / blocksPerEdge;
                    float tNext = (float)(i + 1) / blocksPerEdge;

                    // Slerp along the sphere surface for great-circle interpolation
                    Vector3 from = Vector3.Slerp(start, end, t);
                    Vector3 nextPos = Vector3.Slerp(start, end, tNext);
                    Vector3 position = Vector3.Slerp(start, end, 0.5f * (t + tNext));

                    var rotation = SpawnPoint.LookRotation(from, nextPos, Vector3.up);
                    edgePoints.Add(new SpawnPoint(position, rotation, FlushEdgeBlockScale(position, rotation, endA, endB)));
                }
            }

            // Build vertex trail points
            var vertexPoints = new SpawnPoint[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
                vertexPoints[i] = new SpawnPoint(vertices[i] * radius, vertexRotations[i], blockScale * VertexBlockScale);

            return new[]
            {
                new SpawnTrailData(edgePoints.ToArray(), false, edgeDomain),
                new SpawnTrailData(vertexPoints, false, vertexDomain),
            };
        }

        /// <summary>
        /// tan of half the angle, in the vertex's tangent plane, between edge (v, other) and the
        /// nearest other edge at v. Each edge near v keeps to its own wedge of that half-angle, so
        /// no two edges' blocks meet at v (most vertices sit at 60 degrees, five-fold ones at 72; the
        /// base mesh's crossed triangles leave some pairs 10-40 degrees apart).
        /// </summary>
        static float TanHalfGap(List<Vector3> vertices, List<int>[] neighbours, int v, int other)
        {
            Vector3 n = vertices[v];
            Vector3 dir = Tangent(vertices[other] - n, n);
            float minCos = -1f;
            foreach (int c in neighbours[v])
                if (c != other) minCos = Mathf.Max(minCos, Vector3.Dot(dir, Tangent(vertices[c] - n, n)));
            float half = 0.5f * Mathf.Acos(Mathf.Clamp(minCos, -1f, 1f));
            return Mathf.Tan(Mathf.Min(half, 1.5f));
        }

        static Vector3 Tangent(Vector3 d, Vector3 n) => (d - Vector3.Dot(d, n) * n).normalized;

        /// <summary>
        /// The largest scale, at most blockScale, at which an edge block is FLUSH with what meets it
        /// at its edge's two vertices (Docs/ECOSYSTEM.md §49.10). Five or six edges converge on every
        /// vertex and its 1.5x block sits there, so at full blockScale the end blocks of every edge
        /// ran into each other and into the vertex block - 7,397 clipping pairs in the shipped
        /// prefab, every one of them at a vertex (blocks along an edge are spaced wider than they are
        /// long). Per end, two exact conditions, in closed form from the pose already computed:
        ///
        ///   - the vertex block: on the block's own long axis, its near face clears the vertex
        ///     block's support along that axis (a separating axis);
        ///   - the sibling edges: projected onto the vertex's tangent plane every block of an edge
        ///     is a rectangle on a line through the vertex, so a block inside the wedge of half-angle
        ///     TanHalfGap around its edge cannot meet a block in a neighbouring edge's wedge.
        ///
        /// The wedge is met by a shorter block (its near face further out) or a thinner one; the
        /// pair that keeps the most volume is taken (length, and a uniform shrink of the cross
        /// section, never below MinPrismAxis). Blocks away from the vertices keep blockScale; the
        /// block's centre and rotation never move.
        /// </summary>
        Vector3 FlushEdgeBlockScale(Vector3 position, Quaternion rotation, in EdgeEnd endA, in EdgeEnd endB)
        {
            Vector3 x = rotation * Vector3.right, y = rotation * Vector3.up, zAxis = rotation * Vector3.forward;
            float hx = 0.5f * blockScale.x, hy = 0.5f * blockScale.y;
            Vector3 vertexHalf = blockScale * (0.5f * VertexBlockScale);

            float lengthCap = blockScale.z;
            float ka0 = 0f, kb0 = 0f, ka1 = 0f, kb1 = 0f;
            for (int e = 0; e < 2; e++)
            {
                var end = e == 0 ? endA : endB;
                Vector3 rel = position - end.Center;
                Vector3 z = Vector3.Dot(rel, zAxis) >= 0f ? zAxis : -zAxis;     // pointing away from this vertex

                // The vertex block's support along the block's long axis.
                var vr = end.VertexRotation;
                float support = Mathf.Abs(Vector3.Dot(vr * Vector3.right, z)) * vertexHalf.x
                              + Mathf.Abs(Vector3.Dot(vr * Vector3.up, z)) * vertexHalf.y
                              + Mathf.Abs(Vector3.Dot(vr * Vector3.forward, z)) * vertexHalf.z;
                lengthCap = Mathf.Min(lengthCap, 2f * (Vector3.Dot(rel, z) - support) * FlushMargin);

                // The wedge, in the tangent plane: s runs out along the edge, t across it.
                Vector3 s = Tangent(z, end.Normal);
                Vector3 t = Vector3.Cross(end.Normal, z).normalized;
                float across = Mathf.Abs(Vector3.Dot(x, t)) * hx + Mathf.Abs(Vector3.Dot(y, t)) * hy;
                float along = Mathf.Abs(Vector3.Dot(x, s)) * hx + Mathf.Abs(Vector3.Dot(y, s)) * hy;
                float tan = end.TanHalfGap, den = across + along * tan;
                // The near face, (rel.s - along*k - length/2 * z.s), sits at least across*k / tan out:
                // k <= ka - kb * length.
                float ka = Vector3.Dot(rel, s) * FlushMargin * tan / den;
                float kb = 0.5f * Vector3.Dot(z, s) * tan / den;
                if (e == 0) { ka0 = ka; kb0 = kb; } else { ka1 = ka; kb1 = kb; }
            }

            float kMin = Mathf.Min(1f, MinPrismAxis / Mathf.Max(1e-4f, Mathf.Min(blockScale.x, blockScale.y)));
            float lengthMin = Mathf.Min(blockScale.z, MinPrismAxis);
            float bestVolume = -1f, bestK = kMin, bestLength = Mathf.Max(lengthMin, Mathf.Min(lengthCap, lengthMin));
            // Volume k^2 * length peaks on the cap, where k reaches 1, or at length = ka / (3 kb).
            for (int c = 0; c < 5; c++)
            {
                float candidate = c switch
                {
                    0 => lengthCap,
                    1 => ka0 / (3f * kb0),
                    2 => (ka0 - 1f) / kb0,
                    3 => ka1 / (3f * kb1),
                    _ => (ka1 - 1f) / kb1,
                };
                float length = Mathf.Min(candidate, lengthCap);
                if (!(length >= lengthMin)) continue;
                float k = Mathf.Min(1f, Mathf.Min(ka0 - kb0 * length, ka1 - kb1 * length));
                if (k < kMin) continue;
                float volume = k * k * length;
                if (volume > bestVolume) { bestVolume = volume; bestK = k; bestLength = length; }
            }
            return new Vector3(blockScale.x * bestK, blockScale.y * bestK, bestLength);
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(subdivisions, radius, blocksPerEdge, blockScale, edgeDomain, vertexDomain, seed);
        }

        void AddEdge(HashSet<(int, int)> edges, int a, int b)
        {
            // Canonical order so each edge is stored once
            if (a > b) (a, b) = (b, a);
            edges.Add((a, b));
        }

        (List<Vector3> vertices, List<int> triangles) GenerateIcosphere(int subdivisionLevel)
        {
            // --- Icosahedron base mesh ---
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f; // golden ratio
            float a = 1f;
            float b = 1f / phi;

            var vertices = new List<Vector3>
            {
                new Vector3( 0,  b, -a),  //  0
                new Vector3( b,  a,  0),  //  1
                new Vector3(-b,  a,  0),  //  2
                new Vector3( 0,  b,  a),  //  3
                new Vector3( 0, -b,  a),  //  4
                new Vector3(-a,  0,  b),  //  5
                new Vector3( 0, -b, -a),  //  6
                new Vector3( a,  0, -b),  //  7
                new Vector3( a,  0,  b),  //  8
                new Vector3(-a,  0, -b),  //  9
                new Vector3( b, -a,  0),  // 10
                new Vector3(-b, -a,  0),  // 11
            };

            // Normalize to unit sphere
            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            var triangles = new List<int>
            {
                 2, 1, 0,   1, 2, 3,   5, 2, 3,   3, 8, 1,   3, 4, 8,
                 3, 5, 4,   0, 1, 7,   7, 1, 8,   6, 7, 8,   6, 8, 10,
                 8, 4, 10,  4, 11,10,   4, 5,11,  11, 5, 9,   9, 5, 2,
                 9, 2, 0,   6, 0, 7,   9, 0, 6,  11, 9, 6,  10,11, 6,
            };

            // --- Subdivide ---
            var midpointCache = new Dictionary<(int, int), int>();

            for (int level = 0; level < subdivisionLevel; level++)
            {
                var newTriangles = new List<int>();
                midpointCache.Clear();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int v0 = triangles[i];
                    int v1 = triangles[i + 1];
                    int v2 = triangles[i + 2];

                    int m01 = GetMidpoint(v0, v1, vertices, midpointCache);
                    int m12 = GetMidpoint(v1, v2, vertices, midpointCache);
                    int m20 = GetMidpoint(v2, v0, vertices, midpointCache);

                    newTriangles.AddRange(new[] { v0, m01, m20 });
                    newTriangles.AddRange(new[] { v1, m12, m01 });
                    newTriangles.AddRange(new[] { v2, m20, m12 });
                    newTriangles.AddRange(new[] { m01, m12, m20 });
                }

                triangles = newTriangles;
            }

            return (vertices, triangles);
        }

        int GetMidpoint(int a, int b, List<Vector3> vertices, Dictionary<(int, int), int> cache)
        {
            var key = a < b ? (a, b) : (b, a);
            if (cache.TryGetValue(key, out int index))
                return index;

            Vector3 midpoint = ((vertices[a] + vertices[b]) * 0.5f).normalized;
            index = vertices.Count;
            vertices.Add(midpoint);
            cache[key] = index;
            return index;
        }
    }
}
