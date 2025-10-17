using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class PostCompileAttach
{
    const string KeyBundle = "CS_MGM_PendingAttachBundle"; // JSON

    [Serializable]
    class Bundle { public string scenePath; public string goName; public string[] types; }

    static PostCompileAttach()
    {
        AssemblyReloadEvents.afterAssemblyReload += TryAttach;
    }

    public static void QueueComponent(string scenePath, string goName, string fullType)
    {
        var json = EditorPrefs.GetString(KeyBundle, "");
        Bundle b = string.IsNullOrEmpty(json) ? new Bundle() : JsonUtility.FromJson<Bundle>(json);
        if (b.types == null || b.types.Length == 0)
        {
            b.scenePath = scenePath;
            b.goName = goName;
            b.types = new[] { fullType };
        }
        else
        {
            var list = b.types.ToList();
            if (!list.Contains(fullType)) list.Add(fullType);
            b.types = list.ToArray();
        }
        EditorPrefs.SetString(KeyBundle, JsonUtility.ToJson(b));
    }

    static void TryAttach()
    {
        var json = EditorPrefs.GetString(KeyBundle, "");
        if (string.IsNullOrEmpty(json)) return;
        EditorPrefs.DeleteKey(KeyBundle);

        var b = JsonUtility.FromJson<Bundle>(json);
        if (!string.IsNullOrEmpty(b.scenePath))
            EditorSceneManager.OpenScene(b.scenePath, OpenSceneMode.Single);

        var scene = SceneManager.GetActiveScene();
        var go = scene.GetRootGameObjects().FirstOrDefault(r => r.name == b.goName);
        if (!go) { Debug.LogWarning("[PostCompileAttach] Game object not found"); return; }

        foreach (var typeName in b.types)
        {
            var t = TypeResolver.FindType(typeName);
            if (t == null) { Debug.LogWarning("[PostCompileAttach] Type not found: " + typeName); continue; }
            if (!go.GetComponent(t))
            {
                Undo.AddComponent(go, t);
                Debug.Log($"[PostCompileAttach] Attached {typeName} to '{go.name}'.");
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
