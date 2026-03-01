#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor utility for migrating the Bootstrap scene to a prefab-registry architecture.
    ///
    /// Workflow:
    ///   1. Open Bootstrap scene in the editor.
    ///   2. Run "Tools > Cosmic Shore > Bootstrap Scene > List Scene Objects" to audit.
    ///   3. Run "Tools > Cosmic Shore > Bootstrap Scene > Extract All to Prefabs" to convert
    ///      direct scene GameObjects into prefabs saved in _Prefabs/CORE/.
    ///   4. Run "Tools > Cosmic Shore > Bootstrap Scene > Build Registry SO" to create a
    ///      BootstrapPrefabRegistrySO asset populated with all extracted prefabs.
    ///   5. Wire the registry SO into AppManager's _bootstrapPrefabs field.
    ///   6. Delete the direct scene objects — they are now instantiated from the registry.
    ///
    /// After migration, the Bootstrap scene contains only the AppManager prefab instance
    /// and a ContainerScope. All other objects live as prefabs referenced by the registry SO.
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        const string PrefabFolder = "Assets/_Prefabs/CORE";
        const string SOFolder = "Assets/_SO_Assets";
        const string RegistryAssetName = "BootstrapPrefabRegistry.asset";

        // GameObjects that should stay in the scene (not extracted).
        // These are either the orchestrator itself or objects that must exist before
        // AppManager.Awake() runs (e.g., ContainerScope for Reflex DI).
        static readonly HashSet<string> ExcludeFromExtraction = new()
        {
            "AppManager",       // The orchestrator — must remain a scene prefab instance
            "ContainerScope",   // Reflex DI scope — must exist before AppManager.InstallBindings()
            "EventSystem",      // Unity UI event system — trivial, can stay or be extracted
        };

        // ── List Scene Objects ───────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Bootstrap Scene/List Scene Objects")]
        public static void ListBootstrapSceneObjects()
        {
            var scene = EnsureBootstrapScene();
            if (!scene.HasValue) return;

            var roots = scene.Value.GetRootGameObjects();
            var directObjects = new List<GameObject>();
            var prefabInstances = new List<GameObject>();

            foreach (var root in roots)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                    prefabInstances.Add(root);
                else
                    directObjects.Add(root);
            }

            Debug.Log($"[BootstrapSceneBuilder] === Bootstrap Scene Audit ===");
            Debug.Log($"  Total root GameObjects: {roots.Length}");
            Debug.Log($"  Direct scene objects: {directObjects.Count}");
            Debug.Log($"  Prefab instances: {prefabInstances.Count}");

            Debug.Log($"\n  --- Direct Scene Objects (candidates for extraction) ---");
            foreach (var go in directObjects)
            {
                var components = go.GetComponents<Component>()
                    .Where(c => c != null && c is not Transform)
                    .Select(c => c.GetType().Name);
                var children = go.transform.childCount;
                var excluded = ExcludeFromExtraction.Contains(go.name) ? " [EXCLUDED]" : "";
                Debug.Log($"    {go.name}{excluded} — Components: [{string.Join(", ", components)}] Children: {children}");
            }

            Debug.Log($"\n  --- Prefab Instances (already good) ---");
            foreach (var go in prefabInstances)
            {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                var prefabPath = prefabAsset != null ? AssetDatabase.GetAssetPath(prefabAsset) : "(unknown)";
                Debug.Log($"    {go.name} → {prefabPath}");
            }

            Debug.Log($"\n  --- Recommended Actions ---");
            var extractable = directObjects.Where(go => !ExcludeFromExtraction.Contains(go.name)).ToList();
            Debug.Log($"  {extractable.Count} object(s) can be extracted to prefabs.");
            if (extractable.Count > 0)
                Debug.Log($"  Run 'Tools > Cosmic Shore > Bootstrap Scene > Extract All to Prefabs' to proceed.");
        }

        // ── Extract to Prefabs ───────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Bootstrap Scene/Extract All to Prefabs")]
        public static void ExtractAllToPrefabs()
        {
            var scene = EnsureBootstrapScene();
            if (!scene.HasValue) return;

            EnsureFolder(PrefabFolder);

            var roots = scene.Value.GetRootGameObjects();
            var extracted = new List<(string name, string path)>();

            foreach (var root in roots)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root)) continue;
                if (ExcludeFromExtraction.Contains(root.name)) continue;

                var prefabPath = $"{PrefabFolder}/{SanitizeName(root.name)}.prefab";

                // Skip if prefab already exists (don't overwrite).
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    Debug.Log($"[BootstrapSceneBuilder] Prefab already exists, skipping: {prefabPath}");
                    extracted.Add((root.name, prefabPath));
                    continue;
                }

                // Save as new prefab.
                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    root, prefabPath, InteractionMode.AutomatedAction);

                if (prefab != null)
                {
                    Debug.Log($"[BootstrapSceneBuilder] Extracted: {root.name} → {prefabPath}");
                    extracted.Add((root.name, prefabPath));
                }
                else
                {
                    Debug.LogWarning($"[BootstrapSceneBuilder] Failed to extract: {root.name}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[BootstrapSceneBuilder] Extracted {extracted.Count} prefab(s). Scene saved.");
            Debug.Log($"[BootstrapSceneBuilder] Next: Run 'Build Registry SO' to create the BootstrapPrefabRegistrySO asset.");
        }

        // ── Build Registry SO ────────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Bootstrap Scene/Build Registry SO")]
        public static void BuildRegistrySO()
        {
            var scene = EnsureBootstrapScene();
            if (!scene.HasValue) return;

            // Find all extractable objects that now have prefab counterparts.
            var roots = scene.Value.GetRootGameObjects();
            var entries = new List<BootstrapPrefabEntry>();

            foreach (var root in roots)
            {
                if (ExcludeFromExtraction.Contains(root.name)) continue;

                // Check both prefab instances and direct objects with matching prefabs.
                GameObject prefabAsset = null;

                if (PrefabUtility.IsPartOfPrefabInstance(root))
                {
                    prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(root) as GameObject;
                    if (prefabAsset == null)
                    {
                        var prefabRoot = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
                        prefabAsset = prefabRoot as GameObject;
                    }
                }
                else
                {
                    // Look for a matching prefab in the CORE folder.
                    var prefabPath = $"{PrefabFolder}/{SanitizeName(root.name)}.prefab";
                    prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                }

                if (prefabAsset == null)
                {
                    Debug.LogWarning($"[BootstrapSceneBuilder] No prefab found for '{root.name}' — skipping registry entry. Extract it first.");
                    continue;
                }

                entries.Add(new BootstrapPrefabEntry
                {
                    Prefab = prefabAsset,
                    Persistent = true, // All bootstrap objects are DontDestroyOnLoad
                    Position = root.transform.position,
                    Rotation = root.transform.eulerAngles,
                });
            }

            // Create or update the registry asset.
            var registryPath = $"{SOFolder}/{RegistryAssetName}";
            var registry = AssetDatabase.LoadAssetAtPath<BootstrapPrefabRegistrySO>(registryPath);

            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<BootstrapPrefabRegistrySO>();
                EnsureFolder(SOFolder);
                AssetDatabase.CreateAsset(registry, registryPath);
                Debug.Log($"[BootstrapSceneBuilder] Created new registry: {registryPath}");
            }

            // Write entries via SerializedObject for proper undo/dirty tracking.
            var so = new SerializedObject(registry);
            var entriesProperty = so.FindProperty("_entries");
            entriesProperty.arraySize = entries.Count;

            for (int i = 0; i < entries.Count; i++)
            {
                var element = entriesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Prefab").objectReferenceValue = entries[i].Prefab;
                element.FindPropertyRelative("Persistent").boolValue = entries[i].Persistent;
                element.FindPropertyRelative("Position").vector3Value = entries[i].Position;
                element.FindPropertyRelative("Rotation").vector3Value = entries[i].Rotation;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BootstrapSceneBuilder] Registry built with {entries.Count} entries at: {registryPath}");
            Debug.Log($"[BootstrapSceneBuilder] Next steps:");
            Debug.Log($"  1. Wire '{RegistryAssetName}' into AppManager's _bootstrapPrefabs field.");
            Debug.Log($"  2. Delete direct scene objects that are now in the registry.");
            Debug.Log($"  3. Test play mode to verify all managers are instantiated correctly.");

            EditorGUIUtility.PingObject(registry);
        }

        // ── Clean Scene ──────────────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Bootstrap Scene/Clean Scene (Remove Extracted Objects)")]
        public static void CleanScene()
        {
            var scene = EnsureBootstrapScene();
            if (!scene.HasValue) return;

            var roots = scene.Value.GetRootGameObjects();
            var removed = new List<string>();

            foreach (var root in roots)
            {
                if (ExcludeFromExtraction.Contains(root.name)) continue;

                // Only remove objects that are now prefab instances (were extracted).
                if (!PrefabUtility.IsPartOfPrefabInstance(root)) continue;

                removed.Add(root.name);
                Undo.DestroyObjectImmediate(root);
            }

            if (removed.Count > 0)
            {
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"[BootstrapSceneBuilder] Removed {removed.Count} extracted prefab instances from scene: {string.Join(", ", removed)}");
                Debug.Log($"[BootstrapSceneBuilder] These objects will now be instantiated from the BootstrapPrefabRegistrySO at runtime.");
            }
            else
            {
                Debug.Log($"[BootstrapSceneBuilder] No extracted objects to clean. Scene is already minimal.");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        static Scene? EnsureBootstrapScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex == 0 || scene.name.Contains("Bootstrap"))
                return scene;

            Debug.LogError("[BootstrapSceneBuilder] Open the Bootstrap scene first (build index 0).");
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string SanitizeName(string name)
        {
            // Replace characters that are invalid in file paths.
            return name
                .Replace(" - ", "_")
                .Replace(" ", "_")
                .Replace("/", "_");
        }
    }
}

#endif
