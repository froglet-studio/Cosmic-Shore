#if UNITY_EDITOR

using CosmicShore.Game.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.Tools
{
    public class LogControlWindow : EditorWindow
    {
        const string PrefLogEnabled = "CSDebug_LogEnabled";
        const string PrefWarningsEnabled = "CSDebug_WarningsEnabled";
        const string PrefErrorsEnabled = "CSDebug_ErrorsEnabled";
        const string PrefUnityLoggerEnabled = "CSDebug_UnityLoggerEnabled";

        Vector2 scrollPos;

        [MenuItem("FrogletTools/Log Control", false, 50)]
        static void Open()
        {
            var window = GetWindow<LogControlWindow>("FrogletTools");
            window.minSize = new Vector2(300, 380);
        }

        void OnEnable()
        {
            LoadPrefs();
        }

        void OnFocus()
        {
            // Stay in sync if flags were changed from elsewhere (e.g. menu items).
            Repaint();
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(4);

            // ── Scene Shortcuts ──────────────────────────
            EditorGUILayout.LabelField("Scene Shortcuts", EditorStyles.boldLabel);

            if (GUILayout.Button("Main Menu"))
            {
                EditorSceneManager.OpenScene("Assets/_Scenes/Menu_Main.unity", OpenSceneMode.Single);
                CSDebug.LogFormat("{0} - Opening Main Menu scene.", nameof(LogControlWindow));
            }
            if (GUILayout.Button("Photo Booth"))
            {
                EditorSceneManager.OpenScene("Assets/_Scenes/Tools/PhotoBooth.unity", OpenSceneMode.Single);
                CSDebug.LogFormat("{0} - Opening Photo Booth.", nameof(LogControlWindow));
            }
            if (GUILayout.Button("Recording Studio (WIP)"))
            {
                EditorSceneManager.OpenScene("Assets/_Scenes/Tools/Recording Studio.unity", OpenSceneMode.Single);
                CSDebug.LogFormat("{0} - Opening Recording Studio.", nameof(LogControlWindow));
            }
            if (GUILayout.Button("PlayFab Sandbox"))
            {
                EditorSceneManager.OpenScene("Assets/_Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity", OpenSceneMode.Single);
                CSDebug.LogFormat("{0} - Opening PlayFab Sandbox.", nameof(LogControlWindow));
            }

            EditorGUILayout.Space(8);
            DrawHorizontalLine();
            EditorGUILayout.Space(4);

            // ── Unity Logger master switch ───────────────
            EditorGUILayout.LabelField("Unity Logger", EditorStyles.boldLabel);
            bool unityLogger = Debug.unityLogger.logEnabled;
            bool newUnityLogger = EditorGUILayout.Toggle("Enable Unity Logger", unityLogger);
            if (newUnityLogger != unityLogger)
            {
                Debug.unityLogger.logEnabled = newUnityLogger;
                EditorPrefs.SetBool(PrefUnityLoggerEnabled, newUnityLogger);
            }

            EditorGUILayout.Space(8);

            // ── Presets ──────────────────────────────────
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("All"))
            {
                CSDebug.LogLevel = CSLogLevel.All;
                SavePrefs();
            }
            if (GUILayout.Button("Warnings & Errors"))
            {
                CSDebug.LogLevel = CSLogLevel.WarningsAndErrors;
                SavePrefs();
            }
            if (GUILayout.Button("Off"))
            {
                CSDebug.LogLevel = CSLogLevel.Off;
                SavePrefs();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // ── Per-type toggles ─────────────────────────
            EditorGUILayout.LabelField("Log Types", EditorStyles.boldLabel);

            bool logEnabled = EditorGUILayout.Toggle("Logs", CSDebug.LogEnabled);
            if (logEnabled != CSDebug.LogEnabled)
            {
                CSDebug.LogEnabled = logEnabled;
                SavePrefs();
            }

            bool warningsEnabled = EditorGUILayout.Toggle("Warnings", CSDebug.WarningsEnabled);
            if (warningsEnabled != CSDebug.WarningsEnabled)
            {
                CSDebug.WarningsEnabled = warningsEnabled;
                SavePrefs();
            }

            bool errorsEnabled = EditorGUILayout.Toggle("Errors", CSDebug.ErrorsEnabled);
            if (errorsEnabled != CSDebug.ErrorsEnabled)
            {
                CSDebug.ErrorsEnabled = errorsEnabled;
                SavePrefs();
            }

            EditorGUILayout.Space(8);

            // ── Current state summary ────────────────────
            EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
            string status = $"Logs: {OnOff(CSDebug.LogEnabled)}  |  " +
                            $"Warnings: {OnOff(CSDebug.WarningsEnabled)}  |  " +
                            $"Errors: {OnOff(CSDebug.ErrorsEnabled)}";
            EditorGUILayout.HelpBox(status, MessageType.Info);

            EditorGUILayout.Space(8);
            DrawHorizontalLine();
            EditorGUILayout.Space(4);

            // ── Quest Debug ─────────────────────────────
            EditorGUILayout.LabelField("Quest Debug (Play Mode)", EditorStyles.boldLabel);

            GUI.enabled = Application.isPlaying && GameModeProgressionService.Instance != null;

            if (GUILayout.Button("Complete All Quests"))
                GameModeProgressionService.Instance?.DebugCompleteAllQuests();

            if (GUILayout.Button("Reset All Quests"))
                GameModeProgressionService.Instance?.ResetAllProgress();

            if (Application.isPlaying && GameModeProgressionService.Instance != null)
            {
                var svc = GameModeProgressionService.Instance;
                string info = $"Unlocked: {svc.ProgressionData.UnlockedModes.Count}  |  " +
                              $"Completed: {svc.ProgressionData.CompletedQuests.Count}  |  " +
                              $"Claimed: {svc.GetClaimedQuestCount()}";
                EditorGUILayout.HelpBox(info, MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use quest debug tools.", MessageType.Info);
            }

            GUI.enabled = true;

            EditorGUILayout.EndScrollView();
        }

        static void DrawHorizontalLine()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

        static string OnOff(bool value) => value ? "ON" : "OFF";

        static void SavePrefs()
        {
            EditorPrefs.SetBool(PrefLogEnabled, CSDebug.LogEnabled);
            EditorPrefs.SetBool(PrefWarningsEnabled, CSDebug.WarningsEnabled);
            EditorPrefs.SetBool(PrefErrorsEnabled, CSDebug.ErrorsEnabled);
        }

        internal static void LoadPrefs()
        {
            CSDebug.LogEnabled = EditorPrefs.GetBool(PrefLogEnabled, true);
            CSDebug.WarningsEnabled = EditorPrefs.GetBool(PrefWarningsEnabled, true);
            CSDebug.ErrorsEnabled = EditorPrefs.GetBool(PrefErrorsEnabled, true);

            if (EditorPrefs.HasKey(PrefUnityLoggerEnabled))
                Debug.unityLogger.logEnabled = EditorPrefs.GetBool(PrefUnityLoggerEnabled, true);
        }
    }
}

#endif
