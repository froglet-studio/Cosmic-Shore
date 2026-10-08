# Branch archive: `claude/list-bootstrap-scripts-oeeeP`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-01 by Claude
- **Unmerged commits:** 2
- **Forked from:** `dfe12d248` (2026-03-01, Merge pull request #303 from froglet-studio/claude/fix-camera-follow-vessel-u2)
- **Tip:** `a9db7d045`
- **Files touched (4):**
  - `Assets/_Scripts/Editor/BootstrapSceneBuilder.cs`
  - `Assets/_Scripts/System/AppManager.cs`
  - `Assets/_Scripts/System/Bootstrap/BootstrapPrefabRegistrySO.cs`
  - `Assets/_Scripts/System/Bootstrap/Tests/BootstrapPrefabRegistrySOTests.cs`

### `958f79e68` — feat(bootstrap): add BootstrapPrefabRegistrySO for scene-to-prefab migration

_Claude, 2026-03-01 07:31:20 +0000_

```text
Introduces a prefab registry pattern to minimize the Bootstrap scene file,
making it git-friendly (tiny diffs) and Claude-friendly (C# over YAML).

- BootstrapPrefabRegistrySO: SO asset listing prefabs to instantiate at
  bootstrap time, replacing direct scene GameObjects
- AppManager: instantiates registry prefabs in Awake() before manager
  resolution, so FindAnyObjectByType still discovers them for DI
- BootstrapSceneBuilder: editor tool with 4-step workflow:
  1. List Scene Objects (audit what's in the scene)
  2. Extract All to Prefabs (convert direct GOs to prefabs)
  3. Build Registry SO (populate the registry from extracted prefabs)
  4. Clean Scene (remove extracted objects, now instantiated from registry)
- Unit tests for BootstrapPrefabRegistrySO matching existing test patterns
```

```text
 Assets/_Scripts/Editor/BootstrapSceneBuilder.cs                       | 310 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/System/AppManager.cs                                  |  45 +++++
 Assets/_Scripts/System/Bootstrap/BootstrapPrefabRegistrySO.cs         |  43 +++++
 .../_Scripts/System/Bootstrap/Tests/BootstrapPrefabRegistrySOTests.cs | 120 +++++++++++++
 4 files changed, 518 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 561 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/BootstrapSceneBuilder.cs b/Assets/_Scripts/Editor/BootstrapSceneBuilder.cs
new file mode 100644
index 000000000..8db1c8578
--- /dev/null
+++ b/Assets/_Scripts/Editor/BootstrapSceneBuilder.cs
@@ -0,0 +1,310 @@
+#if UNITY_EDITOR
+
+using System.Collections.Generic;
+using System.IO;
+using System.Linq;
+using CosmicShore.Core;
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using UnityEngine.SceneManagement;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Editor utility for migrating the Bootstrap scene to a prefab-registry architecture.
+    ///
+    /// Workflow:
+    ///   1. Open Bootstrap scene in the editor.
+    ///   2. Run "Tools > Cosmic Shore > Bootstrap Scene > List Scene Objects" to audit.
+    ///   3. Run "Tools > Cosmic Shore > Bootstrap Scene > Extract All to Prefabs" to convert
+    ///      direct scene GameObjects into prefabs saved in _Prefabs/CORE/.
+    ///   4. Run "Tools > Cosmic Shore > Bootstrap Scene > Build Registry SO" to create a
+    ///      BootstrapPrefabRegistrySO asset populated with all extracted prefabs.
+    ///   5. Wire the registry SO into AppManager's _bootstrapPrefabs field.
+    ///   6. Delete the direct scene objects — they are now instantiated from the registry.
+    ///
+    /// After migration, the Bootstrap scene contains only the AppManager prefab instance
+    /// and a ContainerScope. All other objects live as prefabs referenced by the registry SO.
+    /// </summary>
+    public static class BootstrapSceneBuilder
+    {
+        const string PrefabFolder = "Assets/_Prefabs/CORE";
+        const string SOFolder = "Assets/_SO_Assets";
+        const string RegistryAssetName = "BootstrapPrefabRegistry.asset";
+
+        // GameObjects that should stay in the scene (not extracted).
+        // These are either the orchestrator itself or objects that must exist before
+        // AppManager.Awake() runs (e.g., ContainerScope for Reflex DI).
+        static readonly HashSet<string> ExcludeFromExtraction = new()
+        {
+            "AppManager",       // The orchestrator — must remain a scene prefab instance
+            "ContainerScope",   // Reflex DI scope — must exist before AppManager.InstallBindings()
+            "EventSystem",      // Unity UI event system — trivial, can stay or be extracted
+        };
+
+        // ── List Scene Objects ───────────────────────────────────────────
+
+        [MenuItem("Tools/Cosmic Shore/Bootstrap Scene/List Scene Objects")]
+        public static void ListBootstrapSceneObjects()
+        {
+            var scene = EnsureBootstrapScene();
+            if (!scene.HasValue) return;
+
+            var roots = scene.Value.GetRootGameObjects();
+            var directObjects = new List<GameObject>();
+            var prefabInstances = new List<GameObject>();
+
+            foreach (var root in roots)
+            {
+                if (PrefabUtility.IsPartOfPrefabInstance(root))
+                    prefabInstances.Add(root);
+                else
+                    directObjects.Add(root);
+            }
+
+            Debug.Log($"[BootstrapSceneBuilder] === Bootstrap Scene Audit ===");
+            Debug.Log($"  Total root GameObjects: {roots.Length}");
+            Debug.Log($"  Direct scene objects: {directObjects.Count}");
+            Debug.Log($"  Prefab instances: {prefabInstances.Count}");
+
+            Debug.Log($"\n  --- Direct Scene Objects (candidates for extraction) ---");
+            foreach (var go in directObjects)
+            {
+                var components = go.GetComponents<Component>()
+                    .Where(c => c != null && c is not Transform)
+                    .Select(c => c.GetType().Name);
+                var children = go.transform.childCount;
+                var excluded = ExcludeFromExtraction.Contains(go.name) ? " [EXCLUDED]" : "";
+                Debug.Log($"    {go.name}{excluded} — Components: [{string.Join(", ", components)}] Children: {children}");
+            }
+
+            Debug.Log($"\n  --- Prefab Instances (already good) ---");
+            foreach (var go in prefabInstances)
+            {
+                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
+                var prefabPath = prefabAsset != null ? AssetDatabase.GetAssetPath(prefabAsset) : "(unknown)";
+                Debug.Log($"    {go.name} → {prefabPath}");
+            }
+
+            Debug.Log($"\n  --- Recommended Actions ---");
+            var extractable = directObjects.Where(go => !ExcludeFromExtraction.Contains(go.name)).ToList();
+            Debug.Log($"  {extractable.Count} object(s) can be extracted to prefabs.");
+            if (extractable.Count > 0)
+                Debug.Log($"  Run 'Tools > Cosmic Shore > Bootstrap Scene > Extract All to Prefabs' to proceed.");
+        }
+
+        // ── Extract to Prefabs ───────────────────────────────────────────
+
+        [MenuItem("Tools/Cosmic Shore/Bootstrap Scene/Extract All to Prefabs")]
+        public static void ExtractAllToPrefabs()
+        {
+            var scene = EnsureBootstrapScene();
+            if (!scene.HasValue) return;
+
+            EnsureFolder(PrefabFolder);
+
+            var roots = scene.Value.GetRootGameObjects();
+            var extracted = new List<(string name, string path)>();
+
+            foreach (var root in roots)
+            {
+                if (PrefabUtility.IsPartOfPrefabInstance(root)) continue;
+                if (ExcludeFromExtraction.Contains(root.name)) continue;
+
+                var prefabPath = $"{PrefabFolder}/{SanitizeName(root.name)}.prefab";
+
+                // Skip if prefab already exists (don't overwrite).
+                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
+                {
+                    Debug.Log($"[BootstrapSceneBuilder] Prefab already exists, skipping: {prefabPath}");
+                    extracted.Add((root.name, prefabPath));
+                    continue;
+                }
+
+                // Save as new prefab.
+                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
+                    root, prefabPath, InteractionMode.AutomatedAction);
+
+                if (prefab != null)
+                {
+                    Debug.Log($"[BootstrapSceneBuilder] Extracted: {root.name} → {prefabPath}");
+                    extracted.Add((root.name, prefabPath));
+                }
+                else
+                {
+                    Debug.LogWarning($"[BootstrapSceneBuilder] Failed to extract: {root.name}");
+                }
+            }
+
+            AssetDatabase.SaveAssets();
+            AssetDatabase.Refresh();
+            EditorSceneManager.SaveOpenScenes();
+
+            Debug.Log($"[BootstrapSceneBuilder] Extracted {extracted.Count} prefab(s). Scene saved.");
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
