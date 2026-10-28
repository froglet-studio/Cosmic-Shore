#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.AITraining.Editor
{
    /// <summary>
    /// Froglet Compute's batch entry for the AI trainer: runs one trainer scenario in a Unity started with no window,
    /// for as long as Compute gives it, then writes what it found and quits.
    ///
    /// This file is NOT part of the game. Tools/Compute/unity_run.py copies it into Compute's own copy of the project
    /// (~/.froglet-compute/unity/, never the person's checkout) as Assets/Editor/FrogletCompute/, and starts:
    ///
    ///   Unity -batchmode -projectPath &lt;copy&gt; -executeMethod CosmicShore.Utility.AITraining.Editor.FrogletComputeTraining.Run
    ///         -frogletTrain &lt;out dir&gt; -trainMode HexRace -trainVessel Squirrel -trainIntensity 4
    ///         -trainEpisodes 200 -trainDeadline &lt;unix seconds&gt; [-trainTimeScale 4]
    ///
    /// It does what the AI Training window's Learn button does (StartLearn: the control asset, AutoStartOnPlay, Play
    /// from Bootstrap), with the overnight batch flags on, and nothing else: the trainer's own launcher, runner,
    /// fitness and session asset do the work. -trainEpisodes is the unit's TOTAL; the session asset carries the count
    /// across runs (the wrapper saves and restores it), so the runner's own target stops it.
    ///
    /// Writes into the out dir: status.json (every 2 s), result.json and genome.json (at the end). Quits when the
    /// runner stops on its target, at the deadline, or when the wrapper drops a file named "stop" there.
    /// Exit codes: 0 finished or stopped cleanly, 2 bad arguments, 3 the match never started.
    /// </summary>
    [InitializeOnLoad]
    static class FrogletComputeTraining
    {
        const string Root = "Assets/_SO_Assets/AI Training";
        const string BootstrapScene = "Assets/_Scenes/Bootstrap.unity";
        const string KeyStarted = "FrogletCompute.Started";
        const string KeyFinishing = "FrogletCompute.Finishing";
        const string KeyExitCode = "FrogletCompute.ExitCode";
        const string KeyBaseEpisodes = "FrogletCompute.BaseEpisodes";
        const string KeyStartTime = "FrogletCompute.StartTime";
        const double StartGraceSeconds = 900;

        static double _nextTick;
        static bool _sawRunning;

        static FrogletComputeTraining()
        {
            if (string.IsNullOrEmpty(Arg("-frogletTrain"))) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        // ── the entry point ──
        public static void Run()
        {
            string outDir = Arg("-frogletTrain");
            string token = Arg("-trainMode");
            if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(token))
            {
                Fail(outDir, 2, "needs -frogletTrain <dir> and -trainMode <token>");
                return;
            }
            Directory.CreateDirectory(outDir);

            TrainingModeCatalog.Row row = default;
            bool found = false;
            for (int i = 0; i < TrainingModeCatalog.Live.Count; i++)
                if (TrainingModeCatalog.Live[i].Token == token) { row = TrainingModeCatalog.Live[i]; found = true; break; }
            if (!found)
            {
                Fail(outDir, 2, $"the trainer has no scenario for {token}");
                return;
            }

            var scenario = AssetDatabase.LoadAssetAtPath<TrainingScenarioSO>(row.ScenarioPath);
            var control = Load<TrainingControlSO>("TrainingControl");
            var state = Load<TrainingSessionStateSO>("SessionState");
            if (scenario == null || control == null || state == null)
            {
                Fail(outDir, 2, $"missing trainer assets under {Root} (scenario, TrainingControl or SessionState)");
                return;
            }

            string vesselArg = Arg("-trainVessel");
            if (!string.IsNullOrEmpty(vesselArg))
            {
                if (!Enum.TryParse(vesselArg, out VesselClassType vessel))
                {
                    Fail(outDir, 2, $"{vesselArg} is not a vessel class");
                    return;
                }
                if (row.VesselLocked && vessel != row.Vessel)
                {
                    Fail(outDir, 2, $"{row.DisplayName} is locked to {row.Vessel}");
                    return;
                }
                scenario.Vessel = vessel;
            }
            int intensity = Mathf.Clamp(IntArg("-trainIntensity", scenario.Intensity), 1, 4);
            scenario.Intensity = intensity;
            int episodes = Mathf.Max(1, IntArg("-trainEpisodes", 50));

            control.Schedule = null;
            control.Scenario = scenario;
            control.State = state;
            control.Archive = control.Archive != null ? control.Archive : Load<TrainingArchiveSO>("Archive");
            control.Telemetry = control.Telemetry != null ? control.Telemetry : Load<TrainingTelemetrySO>("Telemetry");
            control.HumanPlaysThisLaunch = false;
            control.TargetEpisodes = episodes;
            if (control.WatchdogSeconds <= 0f) control.WatchdogSeconds = 180f;
            control.SimulationTimeScale = FloatArg("-trainTimeScale", 4f);
            control.MuteAudio = true;
            control.DisableCameraRendering = true;
            control.AutoStartOnPlay = true;
            if (control.Telemetry != null) control.Telemetry.IsRunning = false;   // a crashed run can leave it on
            EditorUtility.SetDirty(scenario);
            EditorUtility.SetDirty(control);
            AssetDatabase.SaveAssets();

            int baseEpisodes = state.ScenarioKey == scenario.Key ? state.EpisodesCompleted : 0;
            SessionState.SetInt(KeyBaseEpisodes, baseEpisodes);
            SessionState.SetString(KeyStartTime, Now().ToString(CultureInfo.InvariantCulture));
            SessionState.SetBool(KeyStarted, true);
            WriteStatus(outDir, "starting", state, scenario, episodes);
            Debug.Log($"[FrogletCompute] {scenario.Key}: training to {episodes} episodes (from {baseEpisodes}).");

            EditorSceneManager.OpenScene(BootstrapScene);
            EditorApplication.isPlaying = true;
        }

        // ── the watch ──
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextTick) return;
            _nextTick = EditorApplication.timeSinceStartup + 2.0;
            string outDir = Arg("-frogletTrain");

            if (SessionState.GetBool(KeyFinishing, false))
            {
                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.Exit(SessionState.GetInt(KeyExitCode, 0));
                return;
            }
            if (!SessionState.GetBool(KeyStarted, false)) return;

            var control = Load<TrainingControlSO>("TrainingControl");
            var state = control != null ? control.State : null;
            var scenario = control != null ? control.Scenario : null;
            int target = control != null ? control.TargetEpisodes : 0;
            // The telemetry asset, not the runner: a match replay destroys the runner and builds a new one, and only
            // StopSession (the target, a halt, leaving Play) clears IsRunning.
            var telemetry = control != null ? control.Telemetry : null;
            bool running = EditorApplication.isPlaying && telemetry != null && telemetry.IsRunning;
            if (running) _sawRunning = true;
            WriteStatus(outDir, running ? "training" : (EditorApplication.isPlaying ? "loading the match" : "starting"),
                state, scenario, target);

            double started = double.TryParse(SessionState.GetString(KeyStartTime, ""), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var s) ? s : Now();
            double deadline = DoubleArg("-trainDeadline", 0);
            if (File.Exists(Path.Combine(outDir, "stop")))
                Finish(outDir, 0, "stopped by Froglet Compute (the person came back or the session ended)");
            else if (deadline > 0 && Now() >= deadline)
                Finish(outDir, 0, "the session's time ran out");
            else if (_sawRunning && !running)
                Finish(outDir, 0, state != null && target > 0 && state.EpisodesCompleted >= target
                    ? "finished: the target episodes are done"
                    : "the trainer stopped on its own (see unity.log)");
            else if (!_sawRunning && Now() - started > StartGraceSeconds)
                Finish(outDir, 3, "the match did not start within 15 minutes (see unity.log: boot, sign-in or the menu)");
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(KeyFinishing, false))
                EditorApplication.Exit(SessionState.GetInt(KeyExitCode, 0));
        }

        static void Finish(string outDir, int code, string reason)
        {
            if (SessionState.GetBool(KeyFinishing, false)) return;
            SessionState.SetBool(KeyFinishing, true);
            SessionState.SetInt(KeyExitCode, code);
            var runner = EditorApplication.isPlaying ? UnityEngine.Object.FindAnyObjectByType<TrainingSessionRunner>() : null;
            if (runner != null && runner.IsRunning) runner.StopSession();   // drops the open match, saves the session
            var control = Load<TrainingControlSO>("TrainingControl");
            if (control != null)
            {
                control.AutoStartOnPlay = false;
                EditorUtility.SetDirty(control);
            }
            AssetDatabase.SaveAssets();
            WriteResult(outDir, code, reason, control);
            Debug.Log($"[FrogletCompute] {reason}");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else EditorApplication.Exit(code);
        }

        static void Fail(string outDir, int code, string reason)
        {
            Debug.LogError($"[FrogletCompute] {reason}");
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
                WriteResult(outDir, code, reason, null);
            }
            EditorApplication.Exit(code);
        }

        // ── what it writes ──
        static void WriteStatus(string outDir, string phase, TrainingSessionStateSO state, TrainingScenarioSO scenario, int target)
        {
            if (string.IsNullOrEmpty(outDir)) return;
            int total = state != null ? state.EpisodesCompleted : 0;
            float best = state != null ? state.HallOfFameBestFitness : float.NegativeInfinity;
            var j = new Json()
                .Str("phase", phase)
                .Str("scenario", scenario != null ? scenario.Key : null)
                .Num("episodes_total", total)
                .Num("unit_episodes", target)
                .Num("episodes", Mathf.Max(0, total - SessionState.GetInt(KeyBaseEpisodes, 0)))
                .Num("generation", state != null && state.Population != null ? state.Population.Generation : 0)
                .Num("best_fitness", best)
                .Num("loss", IsFinite(best) ? -best : float.NaN)
                .Num("updated", Now());
            WriteAtomic(Path.Combine(outDir, "status.json"), j.End());
        }

        static void WriteResult(string outDir, int code, string reason, TrainingControlSO control)
        {
            var state = control != null ? control.State : null;
            var scenario = control != null ? control.Scenario : null;
            var j = new Json()
                .Str("schema", "froglet-compute/unity-train/1")
                .Bool("ok", code == 0)
                .Str("reason", reason)
                .Str("scenario", scenario != null ? scenario.Key : null)
                .Str("mode", scenario != null ? scenario.GameMode.ToString() : Arg("-trainMode"))
                .Str("vessel", scenario != null ? scenario.Vessel.ToString() : Arg("-trainVessel"))
                .Num("intensity", scenario != null ? scenario.Intensity : 0)
                .Num("episodes_total", state != null ? state.EpisodesCompleted : 0)
                .Num("episodes", state != null ? Mathf.Max(0, state.EpisodesCompleted - SessionState.GetInt(KeyBaseEpisodes, 0)) : 0)
                .Num("generation", state != null && state.Population != null ? state.Population.Generation : 0)
                .Num("best_fitness", state != null ? state.HallOfFameBestFitness : float.NaN)
                .Nums("history", History(state))
                .Str("unity", Application.unityVersion)
                .Num("updated", Now());
            WriteAtomic(Path.Combine(outDir, "result.json"), j.End());
            if (state != null && state.HallOfFameBest != null)
                WriteAtomic(Path.Combine(outDir, "genome.json"), GenomeJson.Export(state.HallOfFameBest));
        }

        static float[] History(TrainingSessionStateSO state)
        {
            var ring = state != null ? state.FitnessHistory : null;
            if (ring == null || ring.Values == null || ring.Count <= 0) return Array.Empty<float>();
            var outv = new float[ring.Count];
            int start = (ring.Head - ring.Count + ring.Capacity) % ring.Capacity;
            for (int i = 0; i < ring.Count; i++) outv[i] = ring.Values[(start + i) % ring.Capacity];
            return outv;
        }

        // ── small helpers ──
        static T Load<T>(string name) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{Root}/{name}.asset");
            if (asset != null) return asset;
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids is { Length: > 0 } ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static int IntArg(string name, int fallback) =>
            int.TryParse(Arg(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        static float FloatArg(string name, float fallback) =>
            float.TryParse(Arg(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        static double DoubleArg(string name, double fallback) =>
            double.TryParse(Arg(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>A flat JSON object: a non-finite number is written as null, which JsonUtility would not do.</summary>
        sealed class Json
        {
            readonly StringBuilder _sb = new StringBuilder("{");
            bool _first = true;

            Json Key(string k)
            {
                if (!_first) _sb.Append(", ");
                _first = false;
                _sb.Append('"').Append(k).Append("\": ");
                return this;
            }

            public Json Str(string k, string v)
            {
                Key(k);
                if (v == null) { _sb.Append("null"); return this; }
                _sb.Append('"');
                foreach (char c in v)
                {
                    if (c == '"' || c == '\\') _sb.Append('\\').Append(c);
                    else if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else _sb.Append(c);
                }
                _sb.Append('"');
                return this;
            }

            public Json Num(string k, double v)
            {
                Key(k);
                _sb.Append(double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("R", CultureInfo.InvariantCulture));
                return this;
            }

            public Json Bool(string k, bool v)
            {
                Key(k);
                _sb.Append(v ? "true" : "false");
                return this;
            }

            public Json Nums(string k, float[] vs)
            {
                Key(k);
                _sb.Append('[');
                for (int i = 0; i < vs.Length; i++)
                {
                    if (i > 0) _sb.Append(", ");
                    _sb.Append(IsFinite(vs[i]) ? vs[i].ToString("R", CultureInfo.InvariantCulture) : "null");
                }
                _sb.Append(']');
                return this;
            }

            public string End() => _sb.Append("}\n").ToString();
        }
    }
}
#endif
