using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public class FindAssetByGUID : EditorWindow
{
    private string guid = "";
    private long fileID = 0;
    private int searchMode = 0;
    private string[] searchModes = { "Find Asset by GUID", "Find GameObject by File ID", "Find Sub-Asset (GUID + File ID)" };
    private Vector2 scrollPosition;

    [MenuItem("FrogletTools/Find Asset by GUID")]
    private static void ShowWindow()
    {
        GetWindow<FindAssetByGUID>("Asset & Object Finder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Asset & Object Finder", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Search mode selection
        searchMode = GUILayout.SelectionGrid(searchMode, searchModes, 1);
        GUILayout.Space(10);

        switch (searchMode)
        {
            case 0: // GUID only
                DrawGUIDSearch();
                break;
            case 1: // File ID only
                DrawFileIDSearch();
                break;
            case 2: // GUID + File ID
                DrawCombinedSearch();
                break;
        }

        GUILayout.Space(10);

        // Helper buttons
        if (GUILayout.Button("Get GUID of Selected Asset"))
        {
            GetSelectedAssetGUID();
        }

        if (GUILayout.Button("Get File ID of Selected GameObject"))
        {
            GetSelectedGameObjectFileID();
        }

        // Info section
        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "� GUID: Identifies asset files uniquely across projects\n" +
            "� File ID: Identifies objects within a scene or asset file\n" +
            "� Sub-assets: Components, materials in prefabs, etc.",
            MessageType.Info);
    }

    private void DrawGUIDSearch()
    {
        GUILayout.Label("Find Asset by GUID:", EditorStyles.boldLabel);
        guid = EditorGUILayout.TextField("GUID", guid);

        if (GUILayout.Button("Find Asset"))
        {
            SearchAssetByGUID(); // Renamed method
        }
    }

    private void DrawFileIDSearch()
    {
        GUILayout.Label("Find GameObject in Scene by File ID:", EditorStyles.boldLabel);
        fileID = EditorGUILayout.LongField("File ID", fileID);

        if (GUILayout.Button("Find in Current Scene"))
        {
            FindGameObjectByFileID(fileID, false);
        }

        if (GUILayout.Button("Find in All Scenes"))
        {
            FindGameObjectByFileID(fileID, true);
        }
    }

    private void DrawCombinedSearch()
    {
        GUILayout.Label("Find Sub-Asset (GUID + File ID):", EditorStyles.boldLabel);
        guid = EditorGUILayout.TextField("GUID", guid);
        fileID = EditorGUILayout.LongField("File ID", fileID);

        if (GUILayout.Button("Find Sub-Asset"))
        {
            FindSubAsset();
        }
    }

    private void SearchAssetByGUID() // Renamed from FindAssetByGUID()
    {
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning("Please enter a GUID");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(path))
        {
            Debug.Log($"GUID {guid} maps to asset: {path}");
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
        else
        {
            Debug.LogWarning($"No asset found for GUID {guid}");
        }
    }

    private void FindGameObjectByFileID(long targetFileID, bool searchAllScenes)
    {
        if (targetFileID == 0)
        {
            Debug.LogWarning("Please enter a valid File ID");
            return;
        }

        if (searchAllScenes)
        {
            FindInAllScenes(targetFileID);
        }
        else
        {
            FindInCurrentScene(targetFileID);
        }
    }

    private void FindInCurrentScene(long targetFileID)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            // Skip prefab assets and objects not in current scene
            if (obj.scene.name == null || !obj.scene.IsValid()) continue;

            if (GetFileID(obj) == targetFileID)
            {
                SelectAndLogGameObject(obj, targetFileID);
                return;
            }
        }

        Debug.LogWarning($"GameObject with File ID {targetFileID} not found in current scene.");
    }

    private void FindInAllScenes(long targetFileID)
    {
        var currentScene = EditorSceneManager.GetActiveScene();

        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            if (!EditorBuildSettings.scenes[i].enabled) continue;

            string scenePath = EditorBuildSettings.scenes[i].path;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (FindGameObjectInScene(targetFileID))
            {
                Debug.Log($"Found in scene: {scene.name} ({scenePath})");
                return;
            }
        }

        // Restore original scene
        if (currentScene.IsValid() && !string.IsNullOrEmpty(currentScene.path))
        {
            EditorSceneManager.OpenScene(currentScene.path, OpenSceneMode.Single);
        }

        Debug.LogWarning($"GameObject with File ID {targetFileID} not found in any scene.");
    }

    private bool FindGameObjectInScene(long targetFileID)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.scene.name == null || !obj.scene.IsValid()) continue;

            if (GetFileID(obj) == targetFileID)
            {
                SelectAndLogGameObject(obj, targetFileID);
                return true;
            }
        }
        return false;
    }

    private void FindSubAsset()
    {
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning("Please enter a GUID");
            return;
        }

        if (fileID == 0)
        {
            Debug.LogWarning("Please enter a valid File ID");
            return;
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning($"No asset found for GUID {guid}");
            return;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        foreach (Object asset in assets)
        {
            if (asset == null) continue;

            long assetFileID = GetFileID(asset);
            if (assetFileID == fileID)
            {
                Debug.Log($"Found sub-asset: {asset.name} ({asset.GetType().Name}) in {assetPath}");
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
                return;
            }
        }

        Debug.LogWarning($"No sub-asset found with File ID {fileID} in asset {assetPath}");
    }

    private void SelectAndLogGameObject(GameObject obj, long targetFileID)
    {
        Selection.activeGameObject = obj;
        EditorGUIUtility.PingObject(obj);

        string hierarchyPath = GetHierarchyPath(obj);
        Debug.Log($"Found GameObject: {hierarchyPath} (File ID: {targetFileID})");

        // Also expand hierarchy to show the object
        EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
    }

    private void GetSelectedAssetGUID()
    {
        if (Selection.activeObject != null)
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(path))
            {
                string assetGUID = AssetDatabase.AssetPathToGUID(path);
                guid = assetGUID;
                Debug.Log($"Selected asset GUID: {assetGUID} ({path})");
            }
            else
            {
                Debug.LogWarning("Selected object is not an asset file");
            }
        }
        else
        {
            Debug.LogWarning("No object selected");
        }
    }

    private void GetSelectedGameObjectFileID()
    {
        if (Selection.activeGameObject != null)
        {
            long selectedFileID = GetFileID(Selection.activeGameObject);
            fileID = selectedFileID;
            Debug.Log($"Selected GameObject File ID: {selectedFileID} ({Selection.activeGameObject.name})");
        }
        else
        {
            Debug.LogWarning("No GameObject selected");
        }
    }

    private long GetFileID(Object obj)
    {
        return Unsupported.GetLocalIdentifierInFile(obj.GetInstanceID());
    }
     
    private string GetHierarchyPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}