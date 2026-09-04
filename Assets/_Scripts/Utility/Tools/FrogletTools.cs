#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility
{
    [InitializeOnLoad]
    public static class FrogletTools
    {
        static FrogletTools()
        {
            LogControlWindow.LoadPrefs();
        }

        // ── Legacy menu items (all tools live in the Toolbox panel now) ──────

        [MenuItem("FrogletTools/Legacy/Logging/All Logs", false, 100)]
        static void LegacyLogAll() { CSDebug.LogLevel = CSLogLevel.All; PersistLogPrefs(); }

        [MenuItem("FrogletTools/Legacy/Logging/Warnings & Errors Only", false, 101)]
        static void LegacyLogWarn() { CSDebug.LogLevel = CSLogLevel.WarningsAndErrors; PersistLogPrefs(); }

        [MenuItem("FrogletTools/Legacy/Logging/Off (Silent)", false, 102)]
        static void LegacyLogOff() { CSDebug.LogLevel = CSLogLevel.Off; PersistLogPrefs(); }

        [MenuItem("FrogletTools/Legacy/MainScene", false, 200)]
        static void LegacyMainScene() =>
            EditorSceneManager.OpenScene("Assets/_Scenes/Menu_Main.unity", OpenSceneMode.Single);

        [MenuItem("FrogletTools/Legacy/PhotoBooth", false, 201)]
        static void LegacyPhotoBooth() =>
            EditorSceneManager.OpenScene("Assets/_Scenes/Tools/PhotoBooth.unity", OpenSceneMode.Single);

        [MenuItem("FrogletTools/Legacy/RecordingStudio(WIP)", false, 202)]
        static void LegacyRecordingStudio() =>
            EditorSceneManager.OpenScene("Assets/_Scenes/Tools/Recording Studio.unity", OpenSceneMode.Single);

        [MenuItem("FrogletTools/Legacy/PlayFabSandbox", false, 203)]
        static void LegacyPlayFabSandbox() =>
            EditorSceneManager.OpenScene("Assets/_Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity", OpenSceneMode.Single);

        static void PersistLogPrefs()
        {
            EditorPrefs.SetBool("CSDebug_LogEnabled", CSDebug.LogEnabled);
            EditorPrefs.SetBool("CSDebug_WarningsEnabled", CSDebug.WarningsEnabled);
            EditorPrefs.SetBool("CSDebug_ErrorsEnabled", CSDebug.ErrorsEnabled);
        }

        // ─── Performance tools ───────────────────────────

        [MenuItem("FrogletTools/AOE Benchmark Overlay", false, 300)]
        private static void ToggleAOEBenchmark()
        {
            AOEBenchmarkOverlay.ToggleOverlay();
        }

        [MenuItem("FrogletTools/Run AOE Benchmark", false, 301)]
        private static void RunAOEBenchmark()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[FrogletTools] Enter Play Mode first, then re-run this menu item.");
                return;
            }

            var existing = Object.FindAnyObjectByType<AOEBenchmarkRunner>();
            if (existing != null)
            {
                Debug.Log("[FrogletTools] Benchmark already running.");
                return;
            }

            var go = new GameObject("[AOEBenchmarkRunner]");
            go.AddComponent<AOEBenchmarkRunner>();
            Debug.Log("[FrogletTools] AOE Benchmark started - check Console for results.");
        }
    }
}

#endif
