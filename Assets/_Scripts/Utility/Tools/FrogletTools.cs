#if UNITY_EDITOR

using CosmicShore.Game.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.Tools
{
    [InitializeOnLoad]
    public static class FrogletTools
    {
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
            // Persist the individual flags that LogLevel setter just updated.
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

        // ─── Quest Debug ────────────────────────────────

        [MenuItem("FrogletTools/Quest Debug/Open Quest Debug Panel", false, 200)]
        private static void OpenQuestDebugPanel()
        {
            // Opens the FrogletTools window which contains the quest debug UI with index input
            var window = EditorWindow.GetWindow<LogControlWindow>("FrogletTools");
            window.minSize = new Vector2(300, 380);
        }

        [MenuItem("FrogletTools/Quest Debug/Reset All Quests", false, 201)]
        private static void ResetAllQuests()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FrogletTools] Quest Debug only works in Play Mode.");
                return;
            }

            var service = GameModeProgressionService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[FrogletTools] GameModeProgressionService not found.");
                return;
            }

            service.ResetAllProgress();
            Debug.Log("[FrogletTools] All quest progress has been reset.");
        }
    }
}

#endif