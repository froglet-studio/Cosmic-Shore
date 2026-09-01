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
    /// One-click setup for the prism-grid explosion test scene. It:
    ///   1. authors <c>Assets/Resources/PrismGridTestConfig.asset</c> and points it at the default
    ///      prism prefab (Dolphin) and the base spherical AOE explosion prefab,
    ///   2. creates/opens <c>Assets/_Scenes/Game_TestDesign/PrismGridExplosionTest.unity</c>, and
    ///   3. populates it with the minimum the rig needs: a plain Main Camera, a Directional Light,
    ///      an instance of <c>PrismManagers.prefab</c> (PrismScaleManager / MaterialStateManager are
    ///      Singleton&lt;T&gt;, which never auto-creates), and the harness GameObject.
    ///
    /// Idempotent — safe to re-run; existing objects are reused rather than duplicated.
    ///
    /// The scene is deliberately NOT added to Build Settings, matching every other
    /// Game_TestDesign scene (Bootstrap must stay at index 0 for SceneBootstrapper). Add it only if
    /// you want it in the Performance Benchmark's automatic multi-scene sweep.
    ///
    /// No DiagnosticsHUD wiring is needed or wanted: it auto-spawns in every scene via
    /// [RuntimeInitializeOnLoadMethod] — F7 overlay, F5 records to Documents/CosmicShore Diagnostics/.
    /// </summary>
    public static class PrismGridTestSceneSetupTool
    {
        const string ResourcesFolder = "Assets/Resources";
        const string ConfigAssetPath = "Assets/Resources/PrismGridTestConfig.asset";
        const string SceneFolder = "Assets/_Scenes/Game_TestDesign";
        const string ScenePath = "Assets/_Scenes/Game_TestDesign/PrismGridExplosionTest.unity";

        const string PrismPrefabPath = "Assets/_Prefabs/Trails/Prisms With Pools/Dolphin Prism.prefab";
        const string ExplosionPrefabPath = "Assets/_Prefabs/Projectile/AOEExplosion.prefab";
        const string PrismManagersPrefabPath = "Assets/_Prefabs/Environment/PrismManagers.prefab";

        [MenuItem("FrogletTools/Scene Setup/Setup Prism Grid Explosion Scene")]
        [FrogletTool(FrogletToolCategory.SceneSetup, Importance = 2,
            Description = "Build the prism-grid explosion benchmark scene.")]
        static void SetupScene()
        {
            // The scene is opened Single, so give the user a chance to keep whatever they had open.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var config = LoadOrCreateConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = BuildScene(config);

            EditorUtility.DisplayDialog("Setup Prism Grid Explosion Scene",
                $"Config: {ConfigAssetPath}\nScene: {ScenePath}\n\n{report}\n\n" +
                "Before pressing Play, disable Bootstrap auto-load:\n" +
                "FrogletTools > Scene Setup > Testing Multiplayer> Do not load Bootstrap Scene on Play\n\n" +
                "In Play mode: F7 shows the DiagnosticsHUD, F5 records a report to " +
                "Documents/CosmicShore Diagnostics/.",
                "OK");
        }

        // ── Config asset ─────────────────────────────────────────────────────

        static PrismGridTestConfigSO LoadOrCreateConfig()
        {
            EnsureFolder(ResourcesFolder);

            var config = AssetDatabase.LoadAssetAtPath<PrismGridTestConfigSO>(ConfigAssetPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<PrismGridTestConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigAssetPath);
            }

            var so = new SerializedObject(config);

            // Only fill unset references, so a re-run never stomps a deliberate retarget.
            SetObjectIfEmpty(so, "prismPrefab",
                AssetDatabase.LoadAssetAtPath<Prism>(PrismPrefabPath));
            SetObjectIfEmpty(so, "explosionPrefab",
                AssetDatabase.LoadAssetAtPath<AOEExplosion>(ExplosionPrefabPath));

            // Benchmark spec (prompter, 2026-08): a ~100k CUBE (47³ = 103,823 — odd-sided
            // so exact face-centre prisms exist) with the blast ending INSCRIBED at the
            // face centres. Enforced on every run so pre-existing config assets pick up
            // the retune; per-session grid variations belong in the harness panel, not
            // the asset.
            var counts = so.FindProperty("defaultCounts");
            if (counts != null) counts.vector3IntValue = new Vector3Int(47, 47, 47);
            var cap = so.FindProperty("maxTotalPrisms");
            if (cap != null && cap.intValue < 150000) cap.intValue = 150000;
            var fit = so.FindProperty("fitBlastToLattice");
            if (fit != null) fit.boolValue = true;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            return config;
        }

        // ── Scene ────────────────────────────────────────────────────────────

        static string BuildScene(PrismGridTestConfigSO config)
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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            return existed
                ? $"Updated existing scene. PrismManagers: {managersReport}."
                : $"Created new scene. PrismManagers: {managersReport}.";
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

            // The lattice can reach thousands of units across; the stock 1000 far plane clips it.
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 20000f);
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -600f), Quaternion.identity);
            EditorUtility.SetDirty(camera);
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
        /// The prism managers are Singleton&lt;T&gt; — they never auto-create, so without this
        /// prefab prisms spawn but never theme/state-manage (and on legacy branches never
        /// animate). A SECOND instance is just as broken: two copies fight for the singleton
        /// slot, and whichever loses destroys itself or shadows the winner unpredictably.
        ///
        /// SELF-HEALING: enumerates every instance — by component with INACTIVE objects
        /// included (a deactivated managers object still races the singleton on enable, and
        /// the old FindFirstObjectByType check silently missed it, which is exactly how
        /// duplicate instances crept into the scene), and by prefab identity (covers an
        /// instance whose scripts are missing/stripped after a branch switch) — keeps the
        /// first and deletes the rest. Re-running the tool now REPAIRS a duplicated scene
        /// instead of making it worse.
        /// </summary>
        static string EnsurePrismManagers(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrismManagersPrefabPath);

            // BRANCH-PORTABLE detection: PrismStateManager lives on the managers prefab and
            // exists on both the legacy-CPU and gpu-clock branches (PrismScaleManager only
            // exists on legacy branches).
            var roots = new System.Collections.Generic.List<GameObject>();
            foreach (var mgr in Object.FindObjectsByType<PrismStateManager>(
                         FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
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
                Debug.LogWarning($"[PrismGridTestSceneSetupTool] Removed {removed} duplicate " +
                                 "PrismManagers instance(s) — the managers are Singleton<T>; " +
                                 "exactly one instance may exist.");

            if (roots.Count > 0)
                return removed > 0
                    ? $"already present (removed {removed} duplicate{(removed > 1 ? "s" : "")})"
                    : "already present";

            if (prefab == null)
            {
                Debug.LogError($"[PrismGridTestSceneSetupTool] Missing {PrismManagersPrefabPath} — " +
                               "prisms will not animate.");
                return "MISSING — check " + PrismManagersPrefabPath;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create PrismManagers");
            return "added";
        }

        /// <summary>
        /// The lay path hard-depends on the theme: Prism.ChangeTeam →
        /// PrismTeamManager.HandleTeamChange fetches the domain's opaque AND
        /// transparent block materials from ThemeManagerDataContainerSO, whose
        /// TeamMaterialSets dictionary is populated ONLY by ThemeManager.Awake
        /// (a Bootstrap-scene object in gameplay). Without one in this
        /// Bootstrap-less scene, the FIRST laid prism NREs and the whole lay dies
        /// — the "Spawn produced no prisms" failure. The container asset is
        /// resolved from the prism prefab's own PrismTeamManager reference so the
        /// scene themes with exactly the asset the prisms will read.
        /// </summary>
        static void EnsureThemeManager(Scene scene, PrismGridTestConfigSO config)
        {
            if (Object.FindFirstObjectByType<ThemeManager>() != null) return;

            ThemeManagerDataContainerSO container = null;
            if (config != null && config.PrismPrefab != null &&
                config.PrismPrefab.TryGetComponent<PrismTeamManager>(out var teamManager))
            {
                var prefabSo = new SerializedObject(teamManager);
                container = prefabSo.FindProperty("_themeManagerData")?.objectReferenceValue
                    as ThemeManagerDataContainerSO;
            }
            if (container == null)
            {
                var guids = AssetDatabase.FindAssets("t:ThemeManagerDataContainerSO");
                if (guids.Length > 0)
                    container = AssetDatabase.LoadAssetAtPath<ThemeManagerDataContainerSO>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (container == null)
            {
                Debug.LogError("[PrismGridTestSceneSetupTool] No ThemeManagerDataContainerSO found — " +
                               "prism team painting will NullReference on the first laid prism.");
                return;
            }

            var go = NewRoot("[ThemeManager]", scene);
            var tm = Undo.AddComponent<ThemeManager>(go);
            var so = new SerializedObject(tm);
            so.FindProperty("_dataContainer").objectReferenceValue = container;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tm);
        }

        static void EnsureHarness(Scene scene, PrismGridTestConfigSO config, Camera camera)
        {
            var harness = Object.FindFirstObjectByType<PrismGridExplosionHarness>();
            if (harness == null)
            {
                var go = NewRoot("[PrismGridHarness]", scene);
                harness = Undo.AddComponent<PrismGridExplosionHarness>(go);
            }

            // A/B envelope recorder rides the same GameObject (Bench button /
            // 'bench' console command / context menu).
            if (harness.GetComponent<PrismExplosionBenchmark>() == null)
                Undo.AddComponent<PrismExplosionBenchmark>(harness.gameObject);

            var so = new SerializedObject(harness);
            SetObject(so, "config", config);
            SetObject(so, "viewCamera", camera);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(harness);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

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

        static void SetObjectIfEmpty(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = value;
        }
    }
}
