using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SceneObjectCounter : EditorWindow
{
    private Dictionary<string, int> objectCounts;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Scene Object Counter")]
    public static void ShowWindow()
    {
        GetWindow<SceneObjectCounter>("Scene Object Counter");
    }

    void OnGUI()
    {
        if (GUILayout.Button("Count Scene Objects"))
        {
            CountSceneObjects();
        }

        if (objectCounts != null)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            foreach (var kvp in objectCounts.OrderByDescending(kvp => kvp.Value))
            {
                GUILayout.Label($"{kvp.Key}: {kvp.Value}");
            }
            GUILayout.EndScrollView();
        }
    }

    void CountSceneObjects()
    {
        objectCounts = new Dictionary<string, int>();

        // Get all objects in the scene
        Object[] allObjects = Resources.FindObjectsOfTypeAll<Object>();

        foreach (var obj in allObjects)
        {
            // Filter out assets (we only want scene objects)
            if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave)
                continue;

            if (EditorUtility.IsPersistent(obj))
                continue;

            // Get the type name
            string typeName = obj.GetType().Name;

            // Count the objects by type
            if (!objectCounts.ContainsKey(typeName))
            {
                objectCounts[typeName] = 0;
            }

            objectCounts[typeName]++;
        }

        Debug.Log("Scene objects counted.");
    }
}
