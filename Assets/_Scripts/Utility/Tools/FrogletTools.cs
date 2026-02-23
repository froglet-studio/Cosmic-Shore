#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.Tools
{
    [InitializeOnLoad]
    public class FrogletTools : Editor
    {
        const string LogLevelPrefKey = "CSDebug_LogLevel";
        const string MenuAll = "FrogletTools/Logging/All Logs";
        const string MenuWarningsErrors = "FrogletTools/Logging/Warnings & Errors Only";
        const string MenuOff = "FrogletTools/Logging/Off (Silent)";

        static FrogletTools()
        {
            CSDebug.LogLevel = (CSLogLevel)EditorPrefs.GetInt(LogLevelPrefKey, (int)CSLogLevel.All);
            EditorApplication.delayCall += UpdateLogMenuChecks;
        }

        static void SetLogLevel(CSLogLevel level)
        {
            CSDebug.LogLevel = level;
            EditorPrefs.SetInt(LogLevelPrefKey, (int)level);
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

        // ─── Scene shortcuts ───────────────────────────

        [MenuItem("FrogletTools/MainScene", false, -1)]
        private static void OpenMainScene()
        {
            // Open the Main Scene in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/_Scenes/Menu_Main.unity", OpenSceneMode.Single);
            CSDebug.LogFormat("{0} - {1} - Opening Tail Glider Main Menu scene. - Please wait a second for the scene to load.", nameof(FrogletTools), nameof(OpenMainScene));
        }

        [MenuItem("FrogletTools/PhotoBooth", false, -1)]
        private static void OpenPhotoBooth()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/_Scenes/Tools/PhotoBooth.unity", OpenSceneMode.Single);
            CSDebug.LogFormat("{0} - {1} - Opening Tail Glider Photo Booth.", nameof(FrogletTools), nameof(OpenPhotoBooth));
        }

        [MenuItem("FrogletTools/RecordingStudio(WIP)", false, -1)]
        private static void OpenRecordingStudio()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/_Scenes/Tools/Recording Studio.unity", OpenSceneMode.Single);
            CSDebug.LogFormat("{0} - {1} - Opening Tail Glider Recording Studio.", nameof(FrogletTools), nameof(OpenRecordingStudio));
        }

        [MenuItem("FrogletTools/PlayFabSandbox", false, -1)]
        private static void OpenPlayFabSandbox()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene($"Assets/_Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity", OpenSceneMode.Single);
            CSDebug.LogFormat("{0} - {1} - Opening PlayFab Test Sandbox.", nameof(FrogletTools), nameof(OpenPlayFabSandbox));
        }
    }
}

#endif