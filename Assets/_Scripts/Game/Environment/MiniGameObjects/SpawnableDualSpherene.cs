using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Spawning;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Spawns prisms along the edges of the dual of a geodesic polyhedron — a Goldberg
    /// polyhedron with hexagonal and pentagonal faces.
    ///
    /// Starting from a subdivided icosphere (triangular faces), the dual is computed by
    /// placing a vertex at each triangle's centroid (projected onto the sphere) and
    /// connecting centroids of triangles that share an original edge. Each of the 12
    /// original icosahedron vertices has valence 5, producing 12 pentagons; all subdivision
    /// vertices have valence 6, producing hexagons.
    ///
    /// The result is the classic soccer ball / fullerene / Goldberg polyhedron topology:
    ///   - Subdivision 0 → dodecahedron (12 pentagons, 30 dual edges)
    ///   - Subdivision 1 → truncated icosahedron (12 pentagons + 20 hexagons, 90 dual edges)
    ///   - Subdivision 2 → 12 pentagons + 80 hexagons, 360 dual edges
    ///
    /// Perlin-noise undulation displaces vertices radially, preserving the organic membrane
    /// feel while rendering the topology in hex/pent instead of triangles.
    /// </summary>
    public class SpawnableDualSpherene : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 4f);

        [Header("Geodesic Structure")]
        [Tooltip("Subdivision level of the underlying icosphere whose dual is rendered.\n" +
                 "0 = dodecahedron (12 pentagons)\n" +
                 "1 = truncated icosahedron (12 pent + 20 hex)\n" +
                 "2 = 12 pent + 80 hex")]
        [Range(0, 3)]
        [SerializeField] int subdivisions = 2;

        [Tooltip("Radius of the sphere.")]
        [SerializeField] float radius = 70f;

        [Tooltip("Prism blocks per dual edge. More = smoother curves along edges.")]
        [SerializeField] int blocksPerEdge = 6;

        [Header("Undulation")]
        [Tooltip("Amplitude of radial undulation applied to dual vertices.")]
        [SerializeField] float undulationAmplitude = 3f;

        [Tooltip("Frequency of the undulation pattern across the sphere surface.")]
        [SerializeField] float undulationFrequency = 4f;

        [Tooltip("Seed offset for undulation noise pattern.")]
        [SerializeField] float undulationPhase = 0f;

        [Header("Visual")]
        [SerializeField] Domains edgeDomain = Domains.Blue;
        [SerializeField] Domains pentagonDomain = Domains.Gold;

        [Tooltip("Place marker blocks at pentagon centers to highlight the 12 pentagonal defects.")]
        [SerializeField] bool highlightPentagons = true;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            // 1. Generate the underlying icosphere
            var (vertices, triangles) = GenerateIcosphere(subdivisions);

            // 2. Compute face centroids projected onto the unit sphere
            int faceCount = triangles.Count / 3;
            var faceCentroids = new Vector3[faceCount];
            for (int f = 0; f < faceCount; f++)
            {
                Vector3 a = vertices[triangles[f * 3]];
                Vector3 b = vertices[triangles[f * 3 + 1]];
                Vector3 c = vertices[triangles[f * 3 + 2]];
                faceCentroids[f] = ((a + b + c) / 3f).normalized;
            }

            // 3. Build original-edge → adjacent-faces map
            //    Each edge on a closed mesh is shared by exactly 2 triangles
            var edgeToFaces = new Dictionary<(int, int), (int faceA, int faceB)>();
            for (int f = 0; f < faceCount; f++)
            {
                int i0 = triangles[f * 3];
                int i1 = triangles[f * 3 + 1];
                int i2 = triangles[f * 3 + 2];
                RegisterEdgeFace(edgeToFaces, i0, i1, f);
                RegisterEdgeFace(edgeToFaces, i1, i2, f);
                RegisterEdgeFace(edgeToFaces, i2, i0, f);
            }

            // 4. Collect unique dual edges (pairs of face indices whose triangles share an original edge)
            var dualEdges = new HashSet<(int, int)>();
            foreach (var kvp in edgeToFaces)
            {
                int fa = kvp.Value.faceA;
                int fb = kvp.Value.faceB;
                if (fb < 0) continue; // boundary edge (shouldn't happen on closed mesh)
                if (fa > fb) (fa, fb) = (fb, fa);
                dualEdges.Add((fa, fb));
            }

            // 5. Apply Perlin undulation to face centroids
            var dualVertices = new Vector3[faceCount];
            for (int f = 0; f < faceCount; f++)
            {
                Vector3 n = faceCentroids[f];
                float r = radius + SampleUndulation(n) * undulationAmplitude;
                dualVertices[f] = n * r;
            }

            // 6. Place prism blocks along each dual edge
            var edgePoints = new List<SpawnPoint>();
            foreach (var (fa, fb) in dualEdges)
            {
                Vector3 start = dualVertices[fa];
                Vector3 end = dualVertices[fb];

                for (int i = 0; i < blocksPerEdge; i++)
                {
                    float t = (float)i / blocksPerEdge;
                    float tNext = (float)(i + 1) / blocksPerEdge;

                    Vector3 position = Vector3.Slerp(start, end, t);
                    Vector3 nextPos = Vector3.Slerp(start, end, tNext);

                    var rotation = SpawnPoint.LookRotation(position, nextPos, Vector3.up);
                    edgePoints.Add(new SpawnPoint(position, rotation, blockScale));
                }
            }

            var result = new List<SpawnTrailData>
            {
                new SpawnTrailData(edgePoints.ToArray(), false, edgeDomain),
            };

            // 7. Optionally mark the 12 pentagon centers
            if (highlightPentagons)
            {
                // Build vertex → face adjacency
                var vertexFaces = new Dictionary<int, List<int>>();
                for (int f = 0; f < faceCount; f++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        int v = triangles[f * 3 + j];
                        if (!vertexFaces.TryGetValue(v, out var list))
                        {
                            list = new List<int>();
                            vertexFaces[v] = list;
                        }
                        list.Add(f);
                    }
                }

                // Original icosahedron vertices (index < 12) have valence 5 → pentagon dual faces
                var pentagonPoints = new List<SpawnPoint>();
                for (int v = 0; v < 12 && v < vertices.Count; v++)
                {
                    if (!vertexFaces.TryGetValue(v, out var faces) || faces.Count != 5)
                        continue;

                    Vector3 n = vertices[v];
                    float r = radius + SampleUndulation(n) * undulationAmplitude;
                    Vector3 position = n * r;
                    Vector3 lookTarget = position + n;

                    var rotation = SpawnPoint.LookRotation(position, lookTarget, Vector3.up);
                    pentagonPoints.Add(new SpawnPoint(position, rotation, blockScale * 1.8f));
                }

                if (pentagonPoints.Count > 0)
                    result.Add(new SpawnTrailData(pentagonPoints.ToArray(), false, pentagonDomain));
            }

            return result.ToArray();
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(
                subdivisions, radius, blocksPerEdge, blockScale,
                System.HashCode.Combine(undulationAmplitude, undulationFrequency, undulationPhase,
                    edgeDomain, pentagonDomain, highlightPentagons, seed));
        }

        float SampleUndulation(Vector3 normal)
        {
            return DualMeshUtility.SampleUndulation(normal, undulationFrequency, undulationPhase);
        }

        /// <summary>
        /// Register a face against an original edge. First face goes into faceA; second into faceB.
        /// </summary>
        void RegisterEdgeFace(Dictionary<(int, int), (int faceA, int faceB)> map, int a, int b, int face)
        {
            if (a > b) (a, b) = (b, a);
            var key = (a, b);

            if (map.TryGetValue(key, out var existing))
                map[key] = (existing.faceA, face);
            else
                map[key] = (face, -1);
        }

        #region Icosphere Generation

        (List<Vector3> vertices, List<int> triangles) GenerateIcosphere(int subdivisionLevel)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
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

            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            var triangles = new List<int>
            {
                 2, 1, 0,   1, 2, 3,   5, 2, 3,   3, 8, 1,   3, 4, 8,
                 3, 5, 4,   0, 1, 7,   7, 1, 8,   6, 7, 8,   6, 8, 10,
                 8, 4, 10,  4, 11,10,   4, 5,11,  11, 5, 9,   9, 5, 2,
                 9, 2, 0,   6, 0, 7,   9, 0, 6,  11, 9, 6,  10,11, 6,
            };

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

        #endregion
    }
}
