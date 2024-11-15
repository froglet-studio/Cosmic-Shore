using UnityEditor;
using UnityEngine;

public class FindAssetByGUID : EditorWindow
{
    private string guid;

    [MenuItem("FrogletTools/Find Asset by GUID")]
    private static void ShowWindow()
    {
        GetWindow<FindAssetByGUID>("Find Asset by GUID");
    }

    private void OnGUI()
    {
        GUILayout.Label("Enter GUID to find corresponding asset:", EditorStyles.boldLabel);
        guid = EditorGUILayout.TextField("GUID", guid);

        if (GUILayout.Button("Find Asset"))
        {
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log($"GUID {guid} maps to asset: {path}");
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
                }
                else
                {
                    Debug.LogWarning($"No asset found for GUID {guid}");
                }
            }
        }
    }
}
