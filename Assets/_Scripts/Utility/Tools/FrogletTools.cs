#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.Tools
{
    [InitializeOnLoad]
    public static class FrogletTools
    {
        // ── Logging menu items (top-level, still useful as quick toggles) ────
        const string MenuAll = "FrogletTools/Logging/All Logs";
        const string MenuWarningsErrors = "FrogletTools/Logging/Warnings & Errors Only";
        const string MenuOff = "FrogletTools/Logging/Off (Silent)";

        static FrogletTools()
        {
            LogControlWindow.LoadPrefs();
            EditorApplication.delayCall += UpdateLogMenuChecks;
        }

        static void SetLogLevel(CSLogLevel level)
        {
            CSDebug.LogLevel = level;
            EditorPrefs.SetBool("CSDebug_LogEnabled", CSDebug.LogEnabled);
            EditorPrefs.SetBool("CSDebug_WarningsEnabled", CSDebug.WarningsEnabled);
            EditorPrefs.SetBool("CSDebug_ErrorsEnabled", CSDebug.ErrorsEnabled);
            UpdateLogMenuChecks();
        }

        static void UpdateLogMenuChecks()
        {
            Menu.SetChecked(MenuAll, CSDebug.LogLevel == CSLogLevel.All);
            Menu.SetChecked(MenuWarningsErrors, CSDebug.LogLevel == CSLogLevel.WarningsAndErrors);
            Menu.SetChecked(MenuOff, CSDebug.LogLevel == CSLogLevel.Off);
        }

        [MenuItem(MenuAll, false, 100)]
        static void SetLogLevelAll() => SetLogLevel(CSLogLevel.All);

        [MenuItem(MenuWarningsErrors, false, 101)]
        static void SetLogLevelWarningsErrors() => SetLogLevel(CSLogLevel.WarningsAndErrors);

        [MenuItem(MenuOff, false, 102)]
        static void SetLogLevelOff() => SetLogLevel(CSLogLevel.Off);

        // ── Legacy scene shortcuts (kept for muscle-memory) ──────────────────

        [MenuItem("FrogletTools/Legacy/MainScene", false, 500)]
        private static void OpenMainScene()
        {
            EditorSceneManager.OpenScene("Assets/_Scenes/Menu_Main.unity", OpenSceneMode.Single);
        }

        [MenuItem("FrogletTools/Legacy/PhotoBooth", false, 501)]
        private static void OpenPhotoBooth()
        {
            EditorSceneManager.OpenScene("Assets/_Scenes/Tools/PhotoBooth.unity", OpenSceneMode.Single);
        }

        [MenuItem("FrogletTools/Legacy/RecordingStudio(WIP)", false, 502)]
        private static void OpenRecordingStudio()
        {
            EditorSceneManager.OpenScene("Assets/_Scenes/Tools/Recording Studio.unity", OpenSceneMode.Single);
        }

        [MenuItem("FrogletTools/Legacy/PlayFabSandbox", false, 503)]
        private static void OpenPlayFabSandbox()
        {
            EditorSceneManager.OpenScene("Assets/_Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity", OpenSceneMode.Single);
        }
    }
}

#endif
