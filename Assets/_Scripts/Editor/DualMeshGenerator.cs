using CosmicShore.Game;
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
    ///
    /// Core algorithm lives in DualMeshUtility (runtime-accessible). This window
    /// provides the editor UI for baking mesh assets to disk.
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
            var dual = DualMeshUtility.ComputeDual(sourceMesh, projectToSphere, flatShading);
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
    }
}
