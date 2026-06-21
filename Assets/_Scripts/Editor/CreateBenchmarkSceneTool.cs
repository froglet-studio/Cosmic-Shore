using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Tools &gt; Cosmic Shore &gt; Create Benchmark Scene — materializes the stress-test scene by CLONING
    /// the working WildlifeBlitz single-player scene (per the user's "check other scene references"
    /// instruction). Cloning preserves the hard-to-replicate wiring — in-scene NetworkObjects, the
    /// Reflex ContainerScope, the Cell + RandomLifeSpawner, the crystal manager, the GameCanvas, and
    /// the camera — that hand-building from scratch would get subtly wrong.
    ///
    /// The clone + Build-Settings registration are automated; the few semantic edits (Squirrel vessel,
    /// endless controller, high-density spawn profile, benchmark HUD) are printed as a short checklist
    /// because they touch per-scene references this tool can't safely rewrite blind.
    /// </summary>
    public static class CreateBenchmarkSceneTool
    {
        const string ReferenceSceneName = "MinigameWildlifeBlitz";
        const string TargetScenePath = "Assets/_Scenes/Singleplayer Scenes/BenchmarkStressTest.unity";

        [MenuItem("Tools/Cosmic Shore/Create Benchmark Scene")]
        static void CreateBenchmarkScene()
        {
            string refPath = FindScenePath(ReferenceSceneName);
            if (string.IsNullOrEmpty(refPath))
            {
                EditorUtility.DisplayDialog("Create Benchmark Scene",
                    $"Could not find the reference scene '{ReferenceSceneName}.unity'.\n" +
                    "Edit ReferenceSceneName in CreateBenchmarkSceneTool.cs if it moved.", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) != null)
            {
                if (!EditorUtility.DisplayDialog("Create Benchmark Scene",
                        "BenchmarkStressTest.unity already exists.\nOverwrite it?", "Overwrite", "Cancel"))
                    return;
                AssetDatabase.DeleteAsset(TargetScenePath);
            }

            if (!AssetDatabase.CopyAsset(refPath, TargetScenePath))
            {
                EditorUtility.DisplayDialog("Create Benchmark Scene",
                    $"Failed to copy '{refPath}' → '{TargetScenePath}'.", "OK");
                return;
            }
            AssetDatabase.Refresh();
            AddSceneToBuildSettings(TargetScenePath);

            Debug.Log(Checklist());
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath));
            EditorUtility.DisplayDialog("Create Benchmark Scene",
                $"Created {Path.GetFileName(TargetScenePath)} from {ReferenceSceneName} and added it to " +
                "Build Settings.\n\nOpen the Console for the finish-wiring checklist.", "OK");
        }

        static string FindScenePath(string sceneName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{sceneName} t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                    return path;
            }
            return null;
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Benchmark Scene] Added '{scenePath}' to Build Settings.");
        }

        static string Checklist() =>
            "[Benchmark Scene] Created BenchmarkStressTest.unity. Finish wiring in the editor:\n" +
            "  1. ENDLESS: on the 'Game' object, replace the WildlifeBlitz controller with " +
            "SandboxBenchmarkController (HasEndGame=false), OR keep it and delete the TurnMonitor so the " +
            "turn never ends. Keep its CountdownTimer + GameData references.\n" +
            "  2. SQUIRREL: on the MiniGamePlayerSpawnerAdapter, set every _initializeDatas entry " +
            "vesselClass = Squirrel (these are the AI). The human vessel comes from the launcher " +
            "(selectedVesselClass = Squirrel). Set the AI count you want (e.g. 3).\n" +
            "  3. DENSITY: duplicate the cell's SpawnProfileSO to 'BenchmarkSpawnProfile', RAISE flora " +
            "InitialSpawnCount and LOWER Flora/FaunaSpawnIntervalSeconds so load ramps gradually over " +
            "frames (do NOT add decay/TTL — conserved-mass law). Assign it on the cloned cell's " +
            "CellConfigData. Tune for the load you want to stress.\n" +
            "  4. HUD (your UI): add your in-scene benchmark HUD canvas — live FPS + quick graphics " +
            "toggles bound to DisplayGraphicsSettings.Instance, a Run button driving " +
            "PerformanceBenchmarkRunner (Configure → StartBenchmark), and an Exit button that raises " +
            "the OnClickToMainMenuButton event SceneLoader listens to.\n" +
            "  5. LAUNCH: in Menu_Main, put BenchmarkSceneLauncher on the Settings panel and hook your " +
            "'Run Benchmark' button to LaunchBenchmark().";
    }
}
