using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Runtime-accessible dual mesh computation.
    ///
    /// Converts a triangulated mesh into its topological dual — replacing every triangle
    /// with a vertex at its centroid and connecting centroids of adjacent triangles.
    /// For an icosphere source this produces a Goldberg polyhedron: 12 pentagons + hexagons.
    ///
    /// Extracted from DualMeshGenerator (editor tool) so the same algorithm can run
    /// at runtime for procedural membrane generation.
    /// </summary>
    public static class DualMeshUtility
    {
        /// <summary>
        /// Output from dual mesh computation — raw topology before Unity mesh construction.
        /// </summary>
        public struct DualResult
        {
            public Vector3[] DualVertices;
            public List<int[]> DualFaces;
            public float AverageRadius;
            public bool NormalsPointInward;
        }

        /// <summary>
        /// Compute the topological dual of a triangulated mesh.
        /// Returns null if the source mesh is invalid.
        /// </summary>
        public static DualResult? ComputeDualTopology(Mesh source, bool projectToSphere)
        {
            Vector3[] srcVerts = source.vertices;
            Vector3[] srcNormals = source.normals;
            int[] srcTris = source.triangles;
            if (srcTris.Length < 3) return null;

            int srcFaceCount = srcTris.Length / 3;
            bool normalsPointInward = DetectInwardNormals(srcVerts, srcNormals);

            // Step 1: Weld split vertices (FBX meshes duplicate verts per-face for flat normals)
            var (weldMap, weldedPositions) = WeldVertices(srcVerts);

            var faces = new List<(int a, int b, int c)>(srcFaceCount);
            for (int f = 0; f < srcFaceCount; f++)
            {
                int a = weldMap[srcTris[f * 3]];
                int b = weldMap[srcTris[f * 3 + 1]];
                int c = weldMap[srcTris[f * 3 + 2]];
                if (a != b && b != c && a != c)
                    faces.Add((a, b, c));
            }

            int faceCount = faces.Count;
            if (faceCount == 0) return null;

            // Step 2: Dual vertices = face centroids
            float avgRadius = 0f;
            for (int i = 0; i < weldedPositions.Length; i++)
                avgRadius += weldedPositions[i].magnitude;
            avgRadius /= weldedPositions.Length;

            var dualVerts = new Vector3[faceCount];
            for (int f = 0; f < faceCount; f++)
            {
                var (a, b, c) = faces[f];
                Vector3 centroid = (weldedPositions[a] + weldedPositions[b] + weldedPositions[c]) / 3f;
                dualVerts[f] = projectToSphere
                    ? centroid.normalized * avgRadius
                    : centroid;
            }

            // Step 3: Edge → face adjacency
            var edgeFaces = new Dictionary<long, (int faceA, int faceB)>();
            for (int f = 0; f < faceCount; f++)
            {
                var (a, b, c) = faces[f];
                RegisterEdgeFace(edgeFaces, a, b, f);
                RegisterEdgeFace(edgeFaces, b, c, f);
                RegisterEdgeFace(edgeFaces, c, a, f);
            }

            // Step 4: Vertex → ordered face ring → dual faces
            var vertexFaces = new Dictionary<int, List<int>>();
            for (int f = 0; f < faceCount; f++)
            {
                var (a, b, c) = faces[f];
                AddToList(vertexFaces, a, f);
                AddToList(vertexFaces, b, f);
                AddToList(vertexFaces, c, f);
            }

            var dualFaces = new List<int[]>();
            foreach (var kvp in vertexFaces)
            {
                int vert = kvp.Key;
                var ring = kvp.Value;
                if (ring.Count < 3) continue;

                var ordered = OrderFacesAroundVertex(vert, ring, weldedPositions,
                    dualVerts, faces, edgeFaces);
                if (ordered != null)
                    dualFaces.Add(ordered);
            }

            return new DualResult
            {
                DualVertices = dualVerts,
                DualFaces = dualFaces,
                AverageRadius = avgRadius,
                NormalsPointInward = normalsPointInward,
            };
        }

        /// <summary>
        /// Build a Unity Mesh from dual topology data.
        /// Fan-triangulates each polygon from its centroid.
        /// </summary>
        public static Mesh BuildMesh(string name, DualResult dual, bool flatShading)
        {
            return BuildMesh(name, dual.DualVertices, dual.DualFaces,
                flatShading, dual.AverageRadius, dual.NormalsPointInward);
        }

        /// <summary>
        /// Full pipeline: source mesh → dual Unity Mesh.
        /// </summary>
        public static Mesh ComputeDual(Mesh source, bool projectToSphere, bool flatShading)
        {
            var result = ComputeDualTopology(source, projectToSphere);
            if (!result.HasValue) return null;
            return BuildMesh(source.name + "_Dual", result.Value, flatShading);
        }

        /// <summary>
        /// Apply radial displacement to dual vertices.
        /// Modifies positions in-place — call before BuildMesh.
        /// </summary>
        public static void ApplyUndulation(Vector3[] dualVertices, float baseRadius,
            float amplitude, float frequency, float phase)
        {
            for (int i = 0; i < dualVertices.Length; i++)
            {
                Vector3 n = dualVertices[i].normalized;
                float undulation = SampleUndulation(n, frequency, phase) * amplitude;
                dualVertices[i] = n * (baseRadius + undulation);
            }
        }

        /// <summary>
        /// Two-octave Perlin noise sampled from a unit-sphere direction.
        /// Returns a value in roughly [-1, 1].
        /// </summary>
        public static float SampleUndulation(Vector3 normal, float frequency, float phase)
        {
            float noise = Mathf.PerlinNoise(
                (normal.x + 1f) * frequency + phase,
                (normal.y + 1f) * frequency + phase * 0.7f
            ) * 2f - 1f;

            noise += 0.5f * (Mathf.PerlinNoise(
                (normal.z + 1f) * frequency * 2f + phase * 1.3f,
                (normal.x + 1f) * frequency * 2f + phase * 0.4f
            ) * 2f - 1f);

            return noise / 1.5f;
        }

        #region Vertex Welding

        static (int[] weldMap, Vector3[] weldedPositions) WeldVertices(Vector3[] positions)
        {
            const float cellSize = 0.0001f;
            float invCell = 1f / cellSize;

            var posToIndex = new Dictionary<(int, int, int), int>();
            var welded = new List<Vector3>();
            int[] map = new int[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                var key = (
                    Mathf.RoundToInt(positions[i].x * invCell),
                    Mathf.RoundToInt(positions[i].y * invCell),
                    Mathf.RoundToInt(positions[i].z * invCell)
                );

                if (posToIndex.TryGetValue(key, out int existing))
                {
                    map[i] = existing;
                }
                else
                {
                    int idx = welded.Count;
                    welded.Add(positions[i]);
                    posToIndex[key] = idx;
                    map[i] = idx;
                }
            }

            return (map, welded.ToArray());
        }

        #endregion

        #region Adjacency

        static bool DetectInwardNormals(Vector3[] verts, Vector3[] normals)
        {
            if (normals == null || normals.Length == 0) return false;

            float dotSum = 0f;
            int count = Mathf.Min(verts.Length, normals.Length);
            for (int i = 0; i < count; i++)
                dotSum += Vector3.Dot(normals[i], verts[i].normalized);

            return dotSum / count < 0f;
        }

        static void RegisterEdgeFace(Dictionary<long, (int, int)> map, int a, int b, int face)
        {
            long key = EdgeKey(a, b);
            if (map.TryGetValue(key, out var existing))
                map[key] = (existing.Item1, face);
            else
                map[key] = (face, -1);
        }

        static long EdgeKey(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            return (long)a << 32 | (uint)b;
        }

        static void AddToList(Dictionary<int, List<int>> dict, int key, int value)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<int>();
                dict[key] = list;
            }
            list.Add(value);
        }

        #endregion

        #region Face Ordering

        static int[] OrderFacesAroundVertex(int vertIdx, List<int> adjFaces,
            Vector3[] weldedPositions, Vector3[] dualVerts,
            List<(int a, int b, int c)> faces,
            Dictionary<long, (int faceA, int faceB)> edgeFaces)
        {
            var walked = WalkFaceRing(vertIdx, adjFaces, faces, edgeFaces);
            if (walked != null && walked.Length == adjFaces.Count)
                return walked;

            return OrderFacesAngularly(vertIdx, adjFaces, weldedPositions, dualVerts);
        }

        static int[] WalkFaceRing(int vertIdx, List<int> adjFaces,
            List<(int a, int b, int c)> faces,
            Dictionary<long, (int faceA, int faceB)> edgeFaces)
        {
            if (adjFaces.Count == 0) return null;

            var ordered = new List<int>(adjFaces.Count);
            var visited = new HashSet<int>();

            int current = adjFaces[0];
            ordered.Add(current);
            visited.Add(current);

            for (int step = 0; step < adjFaces.Count; step++)
            {
                int nextFace = FindNextFace(vertIdx, current, visited, faces, edgeFaces);
                if (nextFace < 0) break;

                ordered.Add(nextFace);
                visited.Add(nextFace);
                current = nextFace;
            }

            return ordered.Count == adjFaces.Count ? ordered.ToArray() : null;
        }

        static int FindNextFace(int vertIdx, int currentFace, HashSet<int> visited,
            List<(int a, int b, int c)> faces,
            Dictionary<long, (int faceA, int faceB)> edgeFaces)
        {
            var (a, b, c) = faces[currentFace];

            int other1, other2;
            if (vertIdx == a) { other1 = b; other2 = c; }
            else if (vertIdx == b) { other1 = c; other2 = a; }
            else { other1 = a; other2 = b; }

            int next = GetOtherFaceOnEdge(vertIdx, other2, currentFace, edgeFaces);
            if (next >= 0 && !visited.Contains(next))
                return next;

            next = GetOtherFaceOnEdge(vertIdx, other1, currentFace, edgeFaces);
            if (next >= 0 && !visited.Contains(next))
                return next;

            return -1;
        }

        static int GetOtherFaceOnEdge(int a, int b, int currentFace,
            Dictionary<long, (int faceA, int faceB)> edgeFaces)
        {
            long key = EdgeKey(a, b);
            if (!edgeFaces.TryGetValue(key, out var pair))
                return -1;

            if (pair.Item1 == currentFace) return pair.Item2;
            if (pair.Item2 == currentFace) return pair.Item1;
            return -1;
        }

        static int[] OrderFacesAngularly(int vertIdx, List<int> adjFaces,
            Vector3[] weldedPositions, Vector3[] dualVerts)
        {
            Vector3 vertPos = weldedPositions[vertIdx];
            Vector3 normal = vertPos.normalized;
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.up;

            Vector3 refDir = Vector3.zero;
            foreach (int f in adjFaces)
            {
                refDir = Vector3.ProjectOnPlane(dualVerts[f] - vertPos, normal);
                if (refDir.sqrMagnitude > 0.0001f)
                {
                    refDir.Normalize();
                    break;
                }
            }

            if (refDir.sqrMagnitude < 0.0001f) return null;

            Vector3 bitangent = Vector3.Cross(normal, refDir).normalized;

            var sorted = new List<(int face, float angle)>(adjFaces.Count);
            foreach (int f in adjFaces)
            {
                Vector3 projected = Vector3.ProjectOnPlane(dualVerts[f] - vertPos, normal);
                float angle = Mathf.Atan2(
                    Vector3.Dot(projected, bitangent),
                    Vector3.Dot(projected, refDir));
                sorted.Add((f, angle));
            }

            sorted.Sort((x, y) => x.angle.CompareTo(y.angle));

            var result = new int[sorted.Count];
            for (int i = 0; i < sorted.Count; i++)
                result[i] = sorted[i].face;
            return result;
        }

        #endregion

        #region Mesh Building

        static Mesh BuildMesh(string name, Vector3[] dualVerts, List<int[]> dualFaces,
            bool flatShading, float avgRadius, bool normalsPointInward)
        {
            int estVerts = 0, estTris = 0;
            foreach (var face in dualFaces)
            {
                estVerts += face.Length + 1;
                estTris += face.Length;
            }

            var outVerts = new List<Vector3>(estVerts);
            var outNormals = new List<Vector3>(estVerts);
            var outUVs = new List<Vector2>(estVerts);
            var outTris = new List<int>(estTris * 3);

            foreach (var face in dualFaces)
            {
                int n = face.Length;
                if (n < 3) continue;

                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < n; i++)
                    centroid += dualVerts[face[i]];
                centroid /= n;

                Vector3 faceNormal = normalsPointInward
                    ? -centroid.normalized
                    : centroid.normalized;

                int baseIdx = outVerts.Count;

                outVerts.Add(centroid);
                outNormals.Add(faceNormal);
                outUVs.Add(SphericalUV(centroid));

                for (int i = 0; i < n; i++)
                {
                    Vector3 pos = dualVerts[face[i]];
                    Vector3 vertNormal = flatShading
                        ? faceNormal
                        : (normalsPointInward ? -pos.normalized : pos.normalized);

                    outVerts.Add(pos);
                    outNormals.Add(vertNormal);
                    outUVs.Add(SphericalUV(pos));
                }

                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;

                    if (normalsPointInward)
                    {
                        outTris.Add(baseIdx);
                        outTris.Add(baseIdx + 1 + next);
                        outTris.Add(baseIdx + 1 + i);
                    }
                    else
                    {
                        outTris.Add(baseIdx);
                        outTris.Add(baseIdx + 1 + i);
                        outTris.Add(baseIdx + 1 + next);
                    }
                }
            }

            var mesh = new Mesh();
            mesh.name = name;

            if (outVerts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(outVerts);
            mesh.SetNormals(outNormals);
            mesh.SetUVs(0, outUVs);
            mesh.SetTriangles(outTris, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        static Vector2 SphericalUV(Vector3 position)
        {
            Vector3 d = position.normalized;
            float u = 0.5f + Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI);
            float v = 0.5f + Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI;
            return new Vector2(u, v);
        }

        #endregion
    }
}
