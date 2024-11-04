using UnityEngine;
using UnityEditor;
using System.IO;

public class ForceReserializeScriptableObjects
{
    [MenuItem("FrogletTools/Force Re-Serialize All ScriptableObjects")]
    public static void ReserializeAllScriptableObjects()
    {
        // Find all ScriptableObject asset GUIDs in the project
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

            if (obj != null)
            {
                // Log the asset being processed
                Debug.Log("Re-serializing: " + assetPath);

                // Mark the asset as dirty to force it to be saved
                EditorUtility.SetDirty(obj);

                // Force the asset to save, which triggers the re-serialization
                AssetDatabase.SaveAssets();
            }
        }

        // Optionally, force Unity to reimport all assets (if still needed)
        AssetDatabase.Refresh();

        Debug.Log("Re-serialization of all ScriptableObjects complete.");
    }
}
