using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Editor tool that converts a triangulated mesh into its topological dual.
    ///
    /// For a triangulated sphere (icosphere), the dual replaces every triangle with a
    /// vertex at its centroid, and connects centroids of adjacent triangles to form
    /// polygonal faces:
    ///   - Vertices with valence 5 (original icosahedron verts) → pentagons
    ///   - Vertices with valence 6 (subdivision verts) → hexagons
    ///
    /// The result is a Goldberg polyhedron — the soccer ball / fullerene / hex-pent
    /// tiling that Euler's formula demands for a closed sphere.
    ///
    /// The generated mesh uses flat normals per face so polygon boundaries are visible
    /// to edge-detection shaders. Vertex positions are projected onto the source mesh's
    /// average radius so the spherical shape is preserved.
    /// </summary>
    public class DualMeshGenerator : EditorWindow
    {
        [SerializeField] Mesh sourceMesh;
        [SerializeField] bool projectToSphere = true;
        [SerializeField] bool flatShading = true;

        [MenuItem("Tools/Dual Mesh Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<DualMeshGenerator>("Dual Mesh Generator");
            window.minSize = new Vector2(320, 200);
        }

        void OnGUI()
        {
            GUILayout.Label("Dual Mesh Generator", EditorStyles.boldLabel);
            GUILayout.Label(
                "Converts a triangulated mesh to its dual.\n" +
                "Triangles → hexagons + pentagons.",
                EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);

            sourceMesh = (Mesh)EditorGUILayout.ObjectField("Source Mesh", sourceMesh, typeof(Mesh), false);
            projectToSphere = EditorGUILayout.Toggle(
                new GUIContent("Project to Sphere",
                    "Project dual vertices onto the average radius sphere. " +
                    "Keeps the spherical shape clean."),
                projectToSphere);
            flatShading = EditorGUILayout.Toggle(
                new GUIContent("Flat Shading",
                    "Use per-face normals so polygon edges are visible. " +
                    "Disable for smooth shading."),
                flatShading);

            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(sourceMesh == null);
            if (GUILayout.Button("Generate Dual Mesh"))
                GenerateAndSave();
            EditorGUI.EndDisabledGroup();
        }

        void GenerateAndSave()
        {
            var dual = ComputeDual(sourceMesh, projectToSphere, flatShading);
            if (dual == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to generate dual mesh. " +
                    "Make sure the source mesh is readable (enable Read/Write in import settings).", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Dual Mesh",
                sourceMesh.name + "_Dual",
                "asset",
                "Choose where to save the dual mesh.");

            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.CreateAsset(dual, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = dual;

            Debug.Log($"[DualMeshGenerator] Saved {path} — " +
                      $"{dual.vertexCount} verts, {dual.triangles.Length / 3} tris");
        }

        /// <summary>
        /// Core algorithm: triangulated mesh → dual mesh with polygonal faces.
        /// </summary>
        public static Mesh ComputeDual(Mesh source, bool projectToSphere, bool flatShading)
        {
            Vector3[] srcVerts = source.vertices;
            Vector3[] srcNormals = source.normals;
            int[] srcTris = source.triangles;
            if (srcTris.Length < 3) return null;

            int srcFaceCount = srcTris.Length / 3;

            // Detect whether source normals point inward or outward.
            // Membrane meshes are viewed from inside → normals point inward.
            bool normalsPointInward = DetectInwardNormals(srcVerts, srcNormals);

            // ── Step 1: Weld split vertices ────────────────────────────────────
            // FBX/modeled meshes duplicate vertices per-face for flat normals.
            // We merge by position to recover the true topological connectivity.
            var (weldMap, weldedPositions) = WeldVertices(srcVerts);

            // Remap triangle indices to welded space
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

            // ── Step 2: Dual vertices = face centroids ─────────────────────────
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

            // ── Step 3: Edge → face adjacency ──────────────────────────────────
            var edgeFaces = new Dictionary<long, (int faceA, int faceB)>();
            for (int f = 0; f < faceCount; f++)
            {
                var (a, b, c) = faces[f];
                RegisterEdgeFace(edgeFaces, a, b, f);
                RegisterEdgeFace(edgeFaces, b, c, f);
                RegisterEdgeFace(edgeFaces, c, a, f);
            }

            // ── Step 4: Vertex → ordered face ring ─────────────────────────────
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

            // ── Step 5: Build Unity mesh ───────────────────────────────────────
            return BuildMesh(source.name + "_Dual", dualVerts, dualFaces,
                flatShading, avgRadius, normalsPointInward);
        }

        /// <summary>
        /// Check whether the source mesh normals point inward (toward center) or outward.
        /// Returns true if the majority of normals point inward.
        /// </summary>
        static bool DetectInwardNormals(Vector3[] verts, Vector3[] normals)
        {
            if (normals == null || normals.Length == 0) return false;

            float dotSum = 0f;
            int count = Mathf.Min(verts.Length, normals.Length);
            for (int i = 0; i < count; i++)
                dotSum += Vector3.Dot(normals[i], verts[i].normalized);

            // If average dot product is negative, normals point inward
            return dotSum / count < 0f;
        }

        #region Vertex Welding

        /// <summary>
        /// Merge vertices that share the same position (within epsilon).
        /// Uses a quantized grid key (3-int tuple) for robust spatial hashing.
        /// </summary>
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

        /// <summary>
        /// Order faces around a vertex by walking the edge adjacency.
        /// This produces a clean ring even when angular sorting would be ambiguous
        /// (e.g., near poles where faces subtend very small angles).
        /// Falls back to angular sorting if the walk fails.
        /// </summary>
        static int[] OrderFacesAroundVertex(int vertIdx, List<int> adjFaces,
            Vector3[] weldedPositions, Vector3[] dualVerts,
            List<(int a, int b, int c)> faces,
            Dictionary<long, (int faceA, int faceB)> edgeFaces)
        {
            // Try topological walk first (more robust than angular sort)
            var walked = WalkFaceRing(vertIdx, adjFaces, faces, edgeFaces);
            if (walked != null && walked.Length == adjFaces.Count)
                return walked;

            // Fallback: angular sort
            return OrderFacesAngularly(vertIdx, adjFaces, weldedPositions, dualVerts);
        }

        /// <summary>
        /// Walk around a vertex following shared edges to produce an ordered face ring.
        /// For a manifold mesh, each pair of consecutive triangles around a vertex
        /// shares exactly one edge.
        /// </summary>
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
                // Get the "exit edge" of the current triangle at this vertex.
                // For triangle (a, b, c) with our vertex at position p:
                // The two edges at vertex p connect to the other two vertices.
                // We follow the edge that leads to an unvisited adjacent triangle.
                int nextFace = FindNextFace(vertIdx, current, visited, faces, edgeFaces);
                if (nextFace < 0) break;

                ordered.Add(nextFace);
                visited.Add(nextFace);
                current = nextFace;
            }

            return ordered.Count == adjFaces.Count ? ordered.ToArray() : null;
        }

        /// <summary>
        /// From the current triangle, find the next unvisited triangle around the vertex
        /// by checking both edges incident to the vertex.
        /// </summary>
        static int FindNextFace(int vertIdx, int currentFace, HashSet<int> visited,
            List<(int a, int b, int c)> faces,
            Dictionary<long, (int faceA, int faceB)> edgeFaces)
        {
            var (a, b, c) = faces[currentFace];

            // Get the two other vertices of this triangle
            int other1, other2;
            if (vertIdx == a) { other1 = b; other2 = c; }
            else if (vertIdx == b) { other1 = c; other2 = a; }
            else { other1 = a; other2 = b; }

            // Try edge (vertex, other2) first — this follows the triangle winding
            int next = GetOtherFaceOnEdge(vertIdx, other2, currentFace, edgeFaces);
            if (next >= 0 && !visited.Contains(next))
                return next;

            // Try edge (vertex, other1)
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

        /// <summary>
        /// Fallback: sort adjacent face centroids angularly around the vertex normal.
        /// </summary>
        static int[] OrderFacesAngularly(int vertIdx, List<int> adjFaces,
            Vector3[] weldedPositions, Vector3[] dualVerts)
        {
            Vector3 vertPos = weldedPositions[vertIdx];
            Vector3 normal = vertPos.normalized;
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.up;

            // Build a tangent frame
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

            sorted.Sort((a, b) => a.angle.CompareTo(b.angle));
            return sorted.Select(s => s.face).ToArray();
        }

        #endregion

        #region Mesh Building

        /// <summary>
        /// Build the final Unity mesh from dual vertices and polygonal faces.
        /// Fan-triangulates each polygon from its centroid.
        /// Matches the winding convention of the source mesh.
        /// </summary>
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

                // Face centroid
                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < n; i++)
                    centroid += dualVerts[face[i]];
                centroid /= n;

                // Normal direction: match source convention
                Vector3 faceNormal = normalsPointInward
                    ? -centroid.normalized
                    : centroid.normalized;

                int baseIdx = outVerts.Count;

                // Center vertex
                outVerts.Add(centroid);
                outNormals.Add(faceNormal);
                outUVs.Add(SphericalUV(centroid));

                // Ring vertices
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

                // Fan triangulation from center.
                // Winding must match the normal direction so front faces are visible.
                // For inward normals: CW when viewed from outside → front faces face inward.
                // For outward normals: CCW when viewed from outside → front faces face outward.
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;

                    if (normalsPointInward)
                    {
                        // CW from outside: center → ring[i+1] → ring[i]
                        outTris.Add(baseIdx);
                        outTris.Add(baseIdx + 1 + next);
                        outTris.Add(baseIdx + 1 + i);
                    }
                    else
                    {
                        // CCW from outside: center → ring[i] → ring[i+1]
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

        /// <summary>
        /// Spherical UV mapping: longitude/latitude → (u, v).
        /// </summary>
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
