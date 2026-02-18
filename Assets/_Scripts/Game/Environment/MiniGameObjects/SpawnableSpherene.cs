using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Spawns prisms along the edges of a geodesic polyhedron — a spherene.
    ///
    /// Starting from an icosahedron (20 triangles, 12 vertices), each triangle is
    /// recursively subdivided and projected onto the sphere. Blocks are placed along
    /// every edge, creating a cage-like lattice with icosahedral symmetry.
    ///
    /// The result looks like a Buckminster Fuller dome — or a fullerene / carbon
    /// nanotube end-cap. At subdivision 0 it's a clean icosahedron with 30 edges;
    /// at subdivision 2 it's 320 edges forming a dense geodesic sphere.
    ///
    /// The structure offers clear paths along edges with frequent junctions at vertices,
    /// rewarding both long sweeping runs and quick direction changes.
    /// </summary>
    public class SpawnableSpherene : SpawnableAbstractBase
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

        static int ObjectsSpawned = 0;

        public override GameObject Spawn()
        {
            var container = new GameObject($"Spherene{ObjectsSpawned++}");
            int blockIndex = 0;

            var trail = new Trail();
            trails.Add(trail);

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

            // Place blocks along each edge
            foreach (var (a, b) in edges)
            {
                Vector3 start = vertices[a] * radius;
                Vector3 end = vertices[b] * radius;

                for (int i = 0; i < blocksPerEdge; i++)
                {
                    float t = (float)i / blocksPerEdge;
                    float tNext = (float)(i + 1) / blocksPerEdge;

                    // Slerp along the sphere surface for great-circle interpolation
                    Vector3 position = Vector3.Slerp(start, end, t);
                    Vector3 nextPos = Vector3.Slerp(start, end, tNext);

                    CreateBlock(position, nextPos,
                        $"{container.name}::EDGE::{blockIndex}",
                        trail, blockScale, prism, container, edgeDomain);

                    blockIndex++;
                }
            }

            // Place larger blocks at vertices as junction markers
            var vertexTrail = new Trail();
            trails.Add(vertexTrail);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 position = vertices[i] * radius;
                Vector3 lookTarget = position + vertices[i]; // look outward

                CreateBlock(position, lookTarget,
                    $"{container.name}::VERTEX::{blockIndex}",
                    vertexTrail, blockScale * 1.5f, prism, container, vertexDomain);

                blockIndex++;
            }

            return container;
        }

        public override GameObject Spawn(int intensityLevel)
        {
            return Spawn();
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
