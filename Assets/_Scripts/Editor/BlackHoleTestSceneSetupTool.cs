using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click setup for the black hole test scene (Docs/BLACK_HOLE.md §7). It:
    ///   1. authors <c>Assets/Resources/BlackHoleTestConfig.asset</c> and points it at the default
    ///      prism prefab (Dolphin), and <c>Assets/Resources/BlackHoleConfig.asset</c> if missing,
    ///   2. creates/opens <c>Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity</c>, and
    ///   3. populates it with the minimum the rig needs: a plain Main Camera, a Directional Light,
    ///      an instance of <c>PrismManagers.prefab</c>, a ThemeManager, and the harness, and
    ///   4. gives it the game's HyperSea sky (Lighting ▸ Environment ▸ Skybox Material) when it has
    ///      none or Unity's built-in default — a skybox someone authored is kept.
    ///
    /// A KEEPER (idempotent — safe to re-run; existing objects are reused), the same shape as
    /// <see cref="PrismGridTestSceneSetupTool"/>. Both the scene and the config are committed, so
    /// in the normal case this tool only repairs; it is a WRITER and records what it writes.
    ///
    /// The scene is deliberately NOT in Build Settings, matching every other Game_TestDesign scene.
    /// </summary>
    public static class BlackHoleTestSceneSetupTool
    {
        const string ToolName = "Setup Black Hole Test Scene";
        const string ResourcesFolder = "Assets/Resources";
        const string TestConfigAssetPath = "Assets/Resources/BlackHoleTestConfig.asset";
        const string PhysicsConfigAssetPath = "Assets/Resources/BlackHoleConfig.asset";
        const string SceneFolder = "Assets/_Scenes/Game_TestDesign";
        const string ScenePath = "Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity";

        const string PrismPrefabPath = "Assets/_Prefabs/Trails/Prisms With Pools/Dolphin Prism.prefab";
        const string PrismManagersPrefabPath = "Assets/_Prefabs/Environment/PrismManagers.prefab";
        const string SkyboxPath = "Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat";

        [MenuItem("FrogletTools/Scene Setup/Setup Black Hole Test Scene")]
        [FrogletTool(FrogletToolCategory.SceneSetup, Importance = 2,
            Description = "Build (or repair) the black hole gravity test scene and its config assets.",
            DocPath = "Docs/BLACK_HOLE.md#7-the-test-scene")]
        static void SetupScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var config = LoadOrCreateTestConfig();
            EnsurePhysicsConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = BuildScene(config);

            EditorUtility.DisplayDialog(ToolName,
                $"Config: {TestConfigAssetPath}\nScene: {ScenePath}\n\n{report}\n\n" +
                "Before pressing Play, disable Bootstrap auto-load:\n" +
                "FrogletTools > Scene Setup > Testing Multiplayer > Do not load Bootstrap Scene on Play\n\n" +
                "In Play mode: Spawn field, wait for 'ready', then Hole at centre or Fly-through. " +
                "F7 shows the DiagnosticsHUD; `blackhole` and `bhtest` are its console commands.",
                "OK");
        }

        static BlackHoleTestConfigSO LoadOrCreateTestConfig()
        {
            EnsureFolder(ResourcesFolder);
            var config = AssetDatabase.LoadAssetAtPath<BlackHoleTestConfigSO>(TestConfigAssetPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<BlackHoleTestConfigSO>();
                AssetDatabase.CreateAsset(config, TestConfigAssetPath);
                FrogletToolChangeLedger.Record(ToolName, TestConfigAssetPath);
            }

            var so = new SerializedObject(config);
            // Only fill an unset reference, so a re-run never stomps a deliberate retarget.
            var p = so.FindProperty("prismPrefab");
            if (p != null && p.objectReferenceValue == null)
            {
                p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Prism>(PrismPrefabPath);
                FrogletToolChangeLedger.Record(ToolName, TestConfigAssetPath);
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            return config;
        }

        static void EnsurePhysicsConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<BlackHoleConfigSO>(PhysicsConfigAssetPath)) return;
            var config = ScriptableObject.CreateInstance<BlackHoleConfigSO>();
            AssetDatabase.CreateAsset(config, PhysicsConfigAssetPath);
            FrogletToolChangeLedger.Record(ToolName, PhysicsConfigAssetPath);
            BlackHoleRegistry.InvalidateConfig();
        }

        static string BuildScene(BlackHoleTestConfigSO config)
        {
            EnsureFolder(SceneFolder);
            bool existed = System.IO.File.Exists(ScenePath);
            var scene = existed
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = EnsureCamera(scene);
            EnsureLight(scene);
            string managersReport = EnsurePrismManagers(scene);
            EnsureThemeManager(scene, config);
            EnsureHarness(scene, config, camera);
            EnsureSkybox();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            FrogletToolChangeLedger.Record(ToolName, ScenePath);

            return existed
                ? $"Updated existing scene. PrismManagers: {managersReport}."
                : $"Created new scene. PrismManagers: {managersReport}.";
        }

        /// <summary>
        /// The HyperSea sky, unless the scene already names one of its own: no skybox, or Unity's
        /// built-in default (which a new scene gets), is replaced; anything authored is kept. The
        /// black hole's lens bends exactly this material (BlackHoleSky).
        /// </summary>
        static void EnsureSkybox()
        {
            var current = RenderSettings.skybox;
            if (current != null && !AssetDatabase.GetAssetPath(current).StartsWith("Resources/unity_builtin"))
                return;
            var hyperSea = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (hyperSea == null)
            {
                CSDebug.LogWarning($"[BlackHoleTestSceneSetup] {SkyboxPath} is missing; the scene keeps its skybox.");
                return;
            }
            RenderSettings.skybox = hyperSea;
        }

        static Camera EnsureCamera(Scene scene)
        {
            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                var go = NewRoot("Main Camera", scene);
                go.tag = "MainCamera";
                camera = Undo.AddComponent<Camera>(go);
            }
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 20000f);
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -600f), Quaternion.identity);
            EditorUtility.SetDirty(camera);

            // The mouse camera (RMB pan, LMB orbit, wheel / MMB zoom). The harness adds one at
            // runtime if it is missing; authoring it keeps it visible and tunable in the scene.
            if (!camera.TryGetComponent<MouseOrbitCamera>(out var orbit))
                orbit = Undo.AddComponent<MouseOrbitCamera>(camera.gameObject);
            var so = new SerializedObject(orbit);
            var configProp = so.FindProperty("config");
            if (configProp != null && configProp.objectReferenceValue == null)
                configProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<MouseOrbitCameraConfigSO>(
                    "Assets/Resources/" + MouseOrbitCamera.ConfigResourcePath + ".asset");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(orbit);
            return camera;
        }

        static void EnsureLight(Scene scene)
        {
            if (Object.FindFirstObjectByType<Light>() != null) return;
            var go = NewRoot("Directional Light", scene);
            var light = Undo.AddComponent<Light>(go);
            light.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>
        /// Exactly one PrismManagers instance (its managers are Singleton&lt;T&gt;): keeps the first,
        /// deletes duplicates, adds one when none exists — the grid tool's self-healing rule.
        /// </summary>
        static string EnsurePrismManagers(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrismManagersPrefabPath);
            var roots = new System.Collections.Generic.List<GameObject>();
            foreach (var mgr in Object.FindObjectsByType<PrismStateManager>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                var root = mgr.transform.root.gameObject;
                if (!roots.Contains(root)) roots.Add(root);
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                if (roots.Contains(root)) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
                if ((prefab != null && source == prefab) || root.name.StartsWith("PrismManagers"))
                    roots.Add(root);
            }

            int removed = 0;
            for (int i = roots.Count - 1; i >= 1; i--)
            {
                Undo.DestroyObjectImmediate(roots[i]);
                removed++;
            }
            if (removed > 0)
                Debug.LogWarning($"[BlackHoleTestSceneSetupTool] Removed {removed} duplicate PrismManagers instance(s).");
            if (roots.Count > 0)
                return removed > 0 ? $"already present (removed {removed} duplicate{(removed > 1 ? "s" : "")})" : "already present";

            if (prefab == null)
            {
                Debug.LogError($"[BlackHoleTestSceneSetupTool] Missing {PrismManagersPrefabPath}.");
                return "MISSING — check " + PrismManagersPrefabPath;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create PrismManagers");
            return "added";
        }

        /// <summary>
        /// The lay path hard-depends on the theme (Prism.ChangeTeam reads the domain's block
        /// materials from ThemeManagerDataContainerSO, populated only by ThemeManager.Awake).
        /// </summary>
        static void EnsureThemeManager(Scene scene, BlackHoleTestConfigSO config)
        {
            if (Object.FindFirstObjectByType<ThemeManager>() != null) return;

            ThemeManagerDataContainerSO container = null;
            if (config != null && config.PrismPrefab != null &&
                config.PrismPrefab.TryGetComponent<PrismTeamManager>(out var teamManager))
            {
                var prefabSo = new SerializedObject(teamManager);
                container = prefabSo.FindProperty("_themeManagerData")?.objectReferenceValue as ThemeManagerDataContainerSO;
            }
            if (container == null)
            {
                var guids = AssetDatabase.FindAssets("t:ThemeManagerDataContainerSO");
                if (guids.Length > 0)
                    container = AssetDatabase.LoadAssetAtPath<ThemeManagerDataContainerSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (container == null)
            {
                Debug.LogError("[BlackHoleTestSceneSetupTool] No ThemeManagerDataContainerSO found — prism team painting will NullReference on the first laid prism.");
                return;
            }

            var go = NewRoot("[ThemeManager]", scene);
            var tm = Undo.AddComponent<ThemeManager>(go);
            var so = new SerializedObject(tm);
            so.FindProperty("_dataContainer").objectReferenceValue = container;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tm);
        }

        static void EnsureHarness(Scene scene, BlackHoleTestConfigSO config, Camera camera)
        {
            var harness = Object.FindFirstObjectByType<BlackHoleTestHarness>();
            if (harness == null)
            {
                var go = NewRoot("[BlackHoleHarness]", scene);
                harness = Undo.AddComponent<BlackHoleTestHarness>(go);
            }
            var so = new SerializedObject(harness);
            SetObject(so, "config", config);
            SetObject(so, "viewCamera", camera);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(harness);
        }

        static GameObject NewRoot(string name, Scene scene)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void SetObject(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }
    }
}
