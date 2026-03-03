using CosmicShore.Game;
using UnityEditor;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Editor tool that converts a triangulated mesh into its topological dual
    /// and optionally bakes a complete membrane prefab (mesh + material).
    ///
    /// For a triangulated sphere (icosphere), the dual replaces every triangle with a
    /// vertex at its centroid, and connects centroids of adjacent triangles to form
    /// polygonal faces:
    ///   - Vertices with valence 5 (original icosahedron verts) → pentagons
    ///   - Vertices with valence 6 (subdivision verts) → hexagons
    ///
    /// Core algorithm lives in DualMeshUtility (runtime-accessible). This window
    /// provides the editor UI for baking mesh assets and prefabs to disk.
    /// </summary>
    public class DualMeshGenerator : EditorWindow
    {
        [SerializeField] Mesh sourceMesh;
        [SerializeField] bool projectToSphere = true;
        [SerializeField] bool flatShading = true;

        [Header("Membrane Prefab")]
        [SerializeField] Material membraneMaterial;
        [SerializeField] float prefabScale = 1000f;

        [MenuItem("Tools/Dual Mesh Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<DualMeshGenerator>("Dual Mesh Generator");
            window.minSize = new Vector2(360, 340);
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
                    "Project dual vertices onto the average radius sphere."),
                projectToSphere);
            flatShading = EditorGUILayout.Toggle(
                new GUIContent("Flat Shading",
                    "Per-face normals for polygon edge visibility. " +
                    "Disable for smooth shading (preserves Fresnel/displacement behavior)."),
                flatShading);

            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(sourceMesh == null);
            if (GUILayout.Button("Save Dual Mesh Asset..."))
                GenerateAndSaveMesh();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(20);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Dual Membrane Prefab", EditorStyles.boldLabel);
            GUILayout.Label(
                "One-click: generates dual mesh + prefab with MeshFilter/MeshRenderer.\n" +
                "Assets saved to Assets/_Graphics/Meshes/ and Assets/_Prefabs/Environment/.",
                EditorStyles.wordWrappedLabel);
            GUILayout.Space(5);

            membraneMaterial = (Material)EditorGUILayout.ObjectField("Material", membraneMaterial, typeof(Material), false);
            prefabScale = EditorGUILayout.FloatField(
                new GUIContent("Scale", "Uniform scale for the prefab transform."),
                prefabScale);

            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(sourceMesh == null);
            if (GUILayout.Button("Generate Dual Membrane"))
                GenerateDualMembrane();
            EditorGUI.EndDisabledGroup();
        }

        void GenerateAndSaveMesh()
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

        void GenerateDualMembrane()
        {
            var dual = DualMeshUtility.ComputeDual(sourceMesh, projectToSphere, flatShading);
            if (dual == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to generate dual mesh. " +
                    "Make sure the source mesh is readable (enable Read/Write in import settings).", "OK");
                return;
            }

            // Ensure output directories exist
            if (!AssetDatabase.IsValidFolder("Assets/_Graphics/Meshes"))
                AssetDatabase.CreateFolder("Assets/_Graphics", "Meshes");

            // Save mesh asset
            string meshName = sourceMesh.name + "_Dual";
            string meshPath = $"Assets/_Graphics/Meshes/{meshName}.asset";
            dual.name = meshName;

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                // Update in place so references are preserved
                existing.Clear();
                EditorUtility.CopySerialized(dual, existing);
                AssetDatabase.SaveAssets();
                dual = existing;
                Debug.Log($"[DualMeshGenerator] Updated existing mesh at {meshPath}");
            }
            else
            {
                AssetDatabase.CreateAsset(dual, meshPath);
                Debug.Log($"[DualMeshGenerator] Created mesh at {meshPath}");
            }

            // Build prefab
            string prefabName = "DualMembraneBase";
            string prefabPath = $"Assets/_Prefabs/Environment/{prefabName}.prefab";

            var go = new GameObject(prefabName);
            go.transform.localScale = Vector3.one * prefabScale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = dual;

            var mr = go.AddComponent<MeshRenderer>();
            if (membraneMaterial != null)
                mr.sharedMaterial = membraneMaterial;

            // Save or update prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;

            Debug.Log($"[DualMeshGenerator] Saved prefab at {prefabPath} — " +
                      $"mesh: {dual.vertexCount} verts, {dual.triangles.Length / 3} tris, " +
                      $"scale: {prefabScale}");
        }
    }
}
