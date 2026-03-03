using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CosmicShore
{
    /// <summary>
    /// Editor tool that generates a subdivided icosphere mesh and saves it as a project asset.
    /// The icosphere starts from a regular icosahedron (12 vertices, 20 faces) and each
    /// subdivision level quadruples the face count while projecting new vertices onto the
    /// unit sphere. Normals point inward so the mesh reads correctly from inside the sphere
    /// (the player's perspective for the cell membrane).
    /// </summary>
    public class IcosphereMeshGenerator : EditorWindow
    {
        [SerializeField] int subdivisions = 3;
        [SerializeField] string assetName = "MembraneIcosphere";

        [MenuItem("Tools/Icosphere Mesh Generator")]
        public static void ShowWindow()
        {
            GetWindow<IcosphereMeshGenerator>("Icosphere Generator").minSize = new Vector2(300, 140);
        }

        void OnGUI()
        {
            GUILayout.Label("Icosphere Settings", EditorStyles.boldLabel);
            subdivisions = EditorGUILayout.IntSlider("Subdivisions", subdivisions, 0, 5);

            int faceCount = 20 * (int)Mathf.Pow(4, subdivisions);
            int vertCount = 2 + faceCount / 2 + faceCount; // Euler: V = 2 + E - F, rough estimate
            EditorGUILayout.HelpBox($"~{faceCount} triangles, ~{faceCount * 3 / 2} edges", MessageType.Info);

            assetName = EditorGUILayout.TextField("Asset Name", assetName);

            if (GUILayout.Button("Generate & Save Mesh Asset"))
                GenerateAndSave();
        }

        void GenerateAndSave()
        {
            var mesh = GenerateIcosphere(subdivisions);
            mesh.name = assetName;

            string path = $"Assets/_Models/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                AssetDatabase.SaveAssets();
                Debug.Log($"Updated existing mesh asset at {path}");
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created mesh asset at {path}");
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        static Mesh GenerateIcosphere(int subdivisionLevel)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            float a = 1f;
            float b = 1f / phi;

            var vertices = new List<Vector3>
            {
                new( 0,  b, -a),
                new( b,  a,  0),
                new(-b,  a,  0),
                new( 0,  b,  a),
                new( 0, -b,  a),
                new(-a,  0,  b),
                new( 0, -b, -a),
                new( a,  0, -b),
                new( a,  0,  b),
                new(-a,  0, -b),
                new( b, -a,  0),
                new(-b, -a,  0),
            };

            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            var triangles = new List<int>
            {
                2, 1, 0,   1, 2, 3,   5, 2, 3,   3, 8, 1,   3, 4, 8,
                3, 5, 4,   0, 1, 7,   7, 1, 8,   6, 7, 8,   6, 8,10,
                8, 4,10,   4,11,10,   4, 5,11,  11, 5, 9,   9, 5, 2,
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

            // Flip winding order so normals point inward (player is inside the membrane)
            for (int i = 0; i < triangles.Count; i += 3)
                (triangles[i], triangles[i + 1]) = (triangles[i + 1], triangles[i]);

            var mesh = new Mesh();
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        static int GetMidpoint(int a, int b, List<Vector3> vertices, Dictionary<(int, int), int> cache)
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
