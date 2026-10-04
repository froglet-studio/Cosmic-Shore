using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Editor.AI
{
    /// <summary>
    /// Runs the Skim Race AI benchmark: enters Play mode from the Bootstrap scene and hands a
    /// <see cref="SkimRaceBenchmarkRunner"/> the settings below. The runner launches Skim Race
    /// through the normal arcade flow (host + one AI backfill seat), plays N races back to back
    /// via Play Again, and appends one JSON record per race to
    /// <c>BenchmarkResults/SkimRaceAI/</c> (git-ignored). Summarise a results file with
    /// <c>python3 Tools/Build/skimrace_benchmark_report.py &lt;file&gt;</c>.
    ///
    /// A READER of the game: it writes no asset. See Docs/SKIM_RACE_AI.md.
    /// </summary>
    public class SkimRaceBenchmarkWindow : EditorWindow
    {
        const string PendingKey = "CosmicShore.SkimRaceBenchmark.Pending";
        const string BootstrapScene = "Assets/_Scenes/Bootstrap.unity";

        int _races = 10;
        int _intensity = 4;
        int _players = 2;
        float _limit = 0f;   // 0 = the intensity's default (SkimRaceRaceRecorder.DefaultLimitSeconds)
        float _timeout = 150f;

        [MenuItem("FrogletTools/AI/Skim Race AI Benchmark")]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 3,
            Description = "Play N Skim Races through the normal flow with the Skim Race AI and record completion times.")]
        static void Open() => GetWindow<SkimRaceBenchmarkWindow>("Skim Race AI Benchmark");

        void OnGUI()
        {
            _races = EditorGUILayout.IntSlider("Races", _races, 1, 50);
            _intensity = EditorGUILayout.IntSlider("Intensity", _intensity, 1, 4);
            _players = EditorGUILayout.IntSlider("Total players (host + AI)", _players, 2, 4);
            _limit = EditorGUILayout.FloatField("Benchmark limit (s, 0 = default)", _limit);
            if (_limit <= 0f)
                EditorGUILayout.LabelField(" ", $"default for I{_intensity}: {SkimRaceRaceRecorder.DefaultLimitSeconds(_intensity):F0} s");
            _timeout = EditorGUILayout.FloatField("Per-race timeout (s)", _timeout);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Run benchmark (enters Play mode)"))
                    StartFromEditor(_races, _intensity, _players, _limit, _timeout);
            }

            var r = SkimRaceBenchmarkRunner.Active;
            if (r != null)
                EditorGUILayout.HelpBox($"Running: {r.Successes}/{r.Completed} successful so far\n{r.ResultsFile}",
                    MessageType.Info);
        }

        /// <summary>Entry point also used from the CLI (<c>unity command eval</c>).</summary>
        public static void StartFromEditor(int races, int intensity, int players, float limit, float timeout,
            bool promptToSave = true, int qualityLevel = -1)
        {
            var settings = new SkimRaceBenchmarkRunner.Settings
            {
                Races = races,
                Intensity = intensity,
                TotalPlayers = players,
                LimitSeconds = limit,
                TimeoutSeconds = timeout,
                Commit = GitHead(),
                QualityLevel = qualityLevel,
            };
            SessionState.SetString(PendingKey, JsonUtility.ToJson(settings));
            if (!EditorApplication.isPlaying)
            {
                if (promptToSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(BootstrapScene);
                EditorApplication.isPlaying = true;
            }
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            string json = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(json)) return;
            SessionState.EraseString(PendingKey);
            var settings = JsonUtility.FromJson<SkimRaceBenchmarkRunner.Settings>(json);
            SkimRaceBenchmarkRunner.Launch(settings);
        }

        static string GitHead()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = System.IO.Directory.GetParent(Application.dataPath).FullName,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                string s = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(2000);
                return s;
            }
            catch { return ""; }
        }
    }
}
