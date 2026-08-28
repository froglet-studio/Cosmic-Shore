#if UNITY_EDITOR
using CosmicShore.Utility; // GameDataSO
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    /// <summary>
    /// Bridges the Collect tab's "Start" to a benchmark that begins automatically once Play Mode
    /// is entered. The request is stored in <see cref="SessionState"/> so it survives the domain
    /// reload that entering Play triggers; on <c>EnteredPlayMode</c> we spawn/configure the runner
    /// and start it. Mirrors the play-mode-automation pattern in <c>SceneBootstrapper</c>.
    /// </summary>
    [InitializeOnLoad]
    public static class BenchmarkAutoStart
    {
        const string K_Capture = "CSBench.CaptureOnPlay";
        const string K_ConfigGuid = "CSBench.ConfigGuid";
        const string K_RulesGuid = "CSBench.RulesGuid";
        const string K_GameDataGuid = "CSBench.GameDataGuid";
        const string K_RestoreBootstrap = "CSBench.RestoreBootstrap";
        const string K_PrevLoadBootstrap = "CSBench.PrevLoadBootstrap";

        static BenchmarkAutoStart()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>
        /// Requests a capture run: stashes the config/rules/game-data, enables the profiler + frame
        /// timing, optionally opens the chosen scene (suppressing the Bootstrap redirect), and enters
        /// Play Mode. Capture begins in <see cref="OnPlayModeChanged"/> once play starts.
        /// </summary>
        public static void RequestCaptureOnPlay(
            BenchmarkConfigSO config, BenchmarkHintRulesSO rules, GameDataSO gameData,
            string sceneAssetPath, bool bootFromBootstrap)
        {
            if (config == null)
            {
                Debug.LogError("[Benchmark] No config assigned - cannot start a capture.");
                return;
            }

            SessionState.SetString(K_ConfigGuid, ToGuid(config));
            SessionState.SetString(K_RulesGuid, ToGuid(rules));
            SessionState.SetString(K_GameDataGuid, ToGuid(gameData));
            SessionState.SetBool(K_Capture, true);

            // Populate spike markers + CPU/GPU split.
            SpikeAnalyzer.SetProfilerEnabled(true);
            PlayerSettings.enableFrameTimingStats = true;

            if (!bootFromBootstrap && !string.IsNullOrEmpty(sceneAssetPath))
            {
                // Capture the chosen scene directly - suppress SceneBootstrapper's Bootstrap
                // redirect for this launch and restore it when we leave Play Mode.
                SessionState.SetBool(K_RestoreBootstrap, true);
                SessionState.SetBool(K_PrevLoadBootstrap, CosmicShore.Editor.SceneBootstrapper.LoadBootstrapSceneOnPlay);
                CosmicShore.Editor.SceneBootstrapper.LoadBootstrapSceneOnPlay = false;
                EditorSceneManager.OpenScene(sceneAssetPath);
            }
            else
            {
                SessionState.SetBool(K_RestoreBootstrap, false);
                // bootFromBootstrap: let SceneBootstrapper redirect to Bootstrap as usual.
            }

            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(K_Capture, false)) return;
                SessionState.SetBool(K_Capture, false); // consume the request

                var config = LoadByGuid<BenchmarkConfigSO>(SessionState.GetString(K_ConfigGuid, ""));
                if (config == null)
                {
                    Debug.LogError("[Benchmark] Config reference was lost across the domain reload.");
                    return;
                }
                var rules = LoadByGuid<BenchmarkHintRulesSO>(SessionState.GetString(K_RulesGuid, ""));
                var gameData = LoadByGuid<GameDataSO>(SessionState.GetString(K_GameDataGuid, ""));

                var runner = Object.FindFirstObjectByType<PerformanceBenchmarkRunner>();
                if (runner == null)
                {
                    var go = new GameObject("[PerformanceBenchmarkRunner]");
                    runner = go.AddComponent<PerformanceBenchmarkRunner>();
                }

                runner.Configure(config, null, gameData, rules);
                runner.AutoSave = false; // Collect saves explicitly via the Save button
                runner.StartBenchmark();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (SessionState.GetBool(K_RestoreBootstrap, false))
                {
                    CosmicShore.Editor.SceneBootstrapper.LoadBootstrapSceneOnPlay =
                        SessionState.GetBool(K_PrevLoadBootstrap, true);
                    SessionState.SetBool(K_RestoreBootstrap, false);
                }
            }
        }

        static string ToGuid(Object obj)
        {
            if (obj == null) return "";
            string path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        }

        static T LoadByGuid<T>(string guid) where T : Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
#endif
