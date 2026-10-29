#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
    ///
    /// <para><b>What it records, with the game's own tools</b> (Docs/Compute/README.md section 16): every match already
    /// records a replay (the Theater, Docs/REPLAYS.md); here the replays go to &lt;out&gt;/replays (the last two, and the
    /// match where the best fitness rose in replays/best), never to the person's Cloud Save list. The AI-learning
    /// recorder (HumanFlightRecorder) takes the trainer's seat for a human one, so each flight it saves is moved out of
    /// the folder people's races are sent from into &lt;out&gt;/ai-flights and marked AI-flown: the learning sweep must
    /// never learn the AI from the AI. When a person watches (-trainWatch, Unity with its window), the Third Eye opens
    /// on the match. &lt;out&gt;/recordings.json lists it all. Every game type is reached by name, so a version of the
    /// game without one of them still trains, and says what it skipped.</para>
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
        const string KeyThirdEye = "FrogletCompute.ThirdEye";
        const string KeyReplays = "FrogletCompute.Replays";
        const string KeyBest = "FrogletCompute.Best";
        const string KeyBestRose = "FrogletCompute.BestRose";
        const string KeyLastFlight = "FrogletCompute.LastFlight";
        const string TheaterConfigPath = "Assets/Resources/TheaterConfig.asset";
        const string ReplayConfigPath = "Assets/Resources/ReplayConfig.asset";
        const string ThirdEyeMenu = "FrogletTools/AI/Third Eye";
        const int KeepReplays = 2;
        const int KeepFlights = 20;
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
            // -trainWatch: Unity was opened with its window so a person can watch the match: camera and sound stay on.
            bool watch = HasArg("-trainWatch");
            control.SimulationTimeScale = FloatArg("-trainTimeScale", watch ? 1f : 4f);
            control.MuteAudio = !watch;
            control.DisableCameraRendering = !watch;
            control.AutoStartOnPlay = true;
            if (control.Telemetry != null) control.Telemetry.IsRunning = false;   // a crashed run can leave it on
            SessionState.SetString(KeyReplays, PrepareReplays(outDir));
            SessionState.SetFloat(KeyBest, state.HallOfFameBestFitness);
            SessionState.SetBool(KeyBestRose, false);
            SessionState.SetString(KeyLastFlight, FlightRecorderLastFile() ?? "");
            SessionState.SetString(KeyThirdEye, watch ? "waiting for the match" : "not watched (Unity has no window)");
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
            if (running) OpenThirdEyeOnce();
            SweepRecordings(outDir, state);
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
            {
                // Leaving Play saved the open replay ("app closed") and the recorder's last flight: take them too.
                var control = Load<TrainingControlSO>("TrainingControl");
                SweepRecordings(Arg("-frogletTrain"), control != null ? control.State : null);
                EditorApplication.Exit(SessionState.GetInt(KeyExitCode, 0));
            }
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

        // ── what it records ──

        /// <summary>
        /// Points the game's replay recorder at &lt;out&gt;/replays for this run: the Theater's output folder (a tracked
        /// asset of Compute's own copy, reset by the next run's checkout) and a ReplayConfig of its own (untracked,
        /// removed by the next run's clean) that keeps two and lists none in Cloud Save. Returns what it did.
        /// </summary>
        static string PrepareReplays(string outDir)
        {
            string folder = Path.Combine(outDir, "replays");
            Directory.CreateDirectory(Path.Combine(folder, "best"));
            var theater = AssetDatabase.LoadMainAssetAtPath(TheaterConfigPath);
            var theaterSo = theater != null ? new SerializedObject(theater) : null;
            var output = theaterSo?.FindProperty("outputFolder");
            if (output == null) return "off: this version has no Theater output folder to point at <out>/replays";

            var replay = AssetDatabase.LoadMainAssetAtPath(ReplayConfigPath);
            if (replay == null)
            {
                var type = FindType("CosmicShore.ScriptableObjects.ReplayConfigSO");
                if (type == null) return SwitchReplaysOff(theaterSo, "off: this version has no ReplayConfigSO");
                replay = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(replay, ReplayConfigPath);
            }
            var replaySo = new SerializedObject(replay);
            var cloud = replaySo.FindProperty("cloudIndexSize");
            var keep = replaySo.FindProperty("keepNewest");
            if (cloud == null || keep == null)
                return SwitchReplaysOff(theaterSo, "off: ReplayConfigSO has no cloudIndexSize or keepNewest, so training " +
                                                   "matches would fill the person's replay list");
            cloud.intValue = 0;
            keep.intValue = KeepReplays;
            replaySo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(replay);
            output.stringValue = folder;
            theaterSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theater);
            return "on";
        }

        static string SwitchReplaysOff(SerializedObject theaterSo, string why)
        {
            var record = theaterSo.FindProperty("recordVessels");
            if (record == null) return why + " (and the recorder could not be switched off: replays stay in the copy)";
            record.boolValue = false;
            theaterSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theaterSo.targetObject);
            return why;
        }

        /// <summary>The Third Eye on the match, once, when a person watches (Unity has its window).</summary>
        static void OpenThirdEyeOnce()
        {
            if (!HasArg("-trainWatch") || Application.isBatchMode) return;
            if (SessionState.GetString(KeyThirdEye, "") != "waiting for the match") return;
            SessionState.SetString(KeyThirdEye, "opening");
            EditorApplication.delayCall += () =>
                SessionState.SetString(KeyThirdEye, EditorApplication.ExecuteMenuItem(ThirdEyeMenu)
                    ? "open: the first pilot, Chase; ] or Tab picks the next, C the camera"
                    : "not in this version of the game");
        }

        /// <summary>
        /// Every 2 s: marks a rise of the best fitness, gives each new replay a sidecar (the match it belongs to, found
        /// by time: a replay is saved at the next match's start or 20 s after its end), copies the replay of a match
        /// where the best rose into replays/best, moves the recorder's newest flight out of the human folder, and
        /// rewrites recordings.json.
        /// </summary>
        static void SweepRecordings(string outDir, TrainingSessionStateSO state)
        {
            if (string.IsNullOrEmpty(outDir)) return;
            try
            {
                float best = state != null ? state.HallOfFameBestFitness : float.NegativeInfinity;
                if (IsFinite(best) && (!IsFinite(SessionState.GetFloat(KeyBest, float.NaN)) || best > SessionState.GetFloat(KeyBest, best)))
                {
                    SessionState.SetFloat(KeyBest, best);
                    SessionState.SetBool(KeyBestRose, true);
                }
                string folder = Path.Combine(outDir, "replays");
                if (Directory.Exists(folder))
                    foreach (var side in Directory.GetFiles(folder, "*.compute.json"))   // the game pruned its replay
                        if (!File.Exists(side.Substring(0, side.Length - ".compute.json".Length) + ".cstheater"))
                            File.Delete(side);
                if (Directory.Exists(folder))
                    foreach (var summary in Directory.GetFiles(folder, "*.replay.json"))
                    {
                        string stem = summary.Substring(0, summary.Length - ".replay.json".Length);
                        if (File.Exists(stem + ".compute.json")) continue;
                        bool rose = SessionState.GetBool(KeyBestRose, false);
                        WriteAtomic(stem + ".compute.json", new Json()
                            .Num("episodes_total", state != null ? state.EpisodesCompleted : 0)
                            .Num("best_fitness", best)
                            .Bool("best_rose", rose)
                            .Num("seen", Now())
                            .End());
                        if (rose && File.Exists(stem + ".cstheater"))
                        {
                            string bestDir = Path.Combine(folder, "best");
                            foreach (var old in Directory.GetFiles(bestDir)) File.Delete(old);
                            foreach (var ext in new[] { ".cstheater", ".replay.json", ".compute.json" })
                                File.Copy(stem + ext, Path.Combine(bestDir, Path.GetFileName(stem) + ext), true);
                            SessionState.SetBool(KeyBestRose, false);
                        }
                    }
                MoveNewestFlight(outDir);
                WriteRecordings(outDir);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FrogletCompute] recordings: {e.Message}");
            }
        }

        /// <summary>
        /// The recorder's newest flight, if this process wrote it since the last look, moved to &lt;out&gt;/ai-flights.
        /// Only its LastFile: a folder scan could take a race the person flew in their own Unity at the same time.
        /// </summary>
        static void MoveNewestFlight(string outDir)
        {
            string last = FlightRecorderLastFile();
            if (string.IsNullOrEmpty(last) || last == SessionState.GetString(KeyLastFlight, "")) return;
            SessionState.SetString(KeyLastFlight, last);
            if (!File.Exists(last)) return;
            string dir = Path.Combine(outDir, "ai-flights");
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, "ai" + Path.GetFileName(last));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(last, dest);
            foreach (var old in Directory.GetFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(KeepFlights))
                File.Delete(old);
        }

        static string FlightRecorderLastFile()
        {
            var type = FindType("CosmicShore.Gameplay.HumanFlightRecorder");
            var prop = type?.GetProperty("LastFile", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) as string;
        }

        static void WriteRecordings(string outDir)
        {
            var replays = new List<string>();
            string folder = Path.Combine(outDir, "replays");
            foreach (var (dir, kept) in new[] { (folder, "last"), (Path.Combine(folder, "best"), "best") })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var side in Directory.GetFiles(dir, "*.compute.json"))
                {
                    string stem = side.Substring(0, side.Length - ".compute.json".Length);
                    if (!File.Exists(stem + ".cstheater")) continue;
                    replays.Add(new Json()
                        .Str("file", Path.GetFileName(stem) + ".cstheater")
                        .Str("kept", kept)
                        .Num("bytes", new FileInfo(stem + ".cstheater").Length)
                        .Raw("match", File.ReadAllText(side).Trim())
                        .Raw("summary", File.Exists(stem + ".replay.json") ? File.ReadAllText(stem + ".replay.json").Trim() : "null")
                        .Body());
                }
            }
            var flights = new List<string>();
            string flightDir = Path.Combine(outDir, "ai-flights");
            if (Directory.Exists(flightDir))
                foreach (var f in Directory.GetFiles(flightDir, "*.json").OrderBy(File.GetLastWriteTimeUtc))
                    flights.Add(new Json()
                        .Str("file", Path.GetFileName(f))
                        .Num("bytes", new FileInfo(f).Length)
                        .Str("pilot", "trainer")
                        .Str("note", "flown by the trainer's AI; its 'held' column says 0 (the recorder does not know the " +
                                     "trainer's seat yet), so never learn from it as a person's flight")
                        .Body());
            WriteAtomic(Path.Combine(outDir, "recordings.json"), new Json()
                .Str("schema", "froglet-compute/unity-recordings/1")
                .Str("replays_setup", SessionState.GetString(KeyReplays, ""))
                .Str("replays_folder", folder)
                .Raw("replays", "[" + string.Join(", ", replays) + "]")
                .Str("flights_folder", flightDir)
                .Raw("flights", "[" + string.Join(", ", flights) + "]")
                .Str("third_eye", SessionState.GetString(KeyThirdEye, ""))
                .Num("updated", Now())
                .End());
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
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

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

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

            /// <summary>A value that is already JSON (an object, an array, null), written as it is.</summary>
            public Json Raw(string k, string json)
            {
                Key(k);
                _sb.Append(string.IsNullOrWhiteSpace(json) ? "null" : json);
                return this;
            }

            public string End() => _sb.Append("}\n").ToString();

            /// <summary>The object without the trailing newline, to put inside an array.</summary>
            public string Body() => _sb.Append('}').ToString();
        }
    }
}
#endif
