using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The per-mode launch smoke test (Docs/LAUNCH/SMOKE_TEST.md, READINESS.md blocker 19): for every mode on the
    /// shipped rosters, enter Play from Bootstrap, launch the mode through the real launch pipeline with the AI on
    /// every seat, run the clock fast, and check that the scene loaded, an AI vessel moved, the game-over event fired
    /// within the budget and nothing logged an error or exception. One fresh Play session per mode, so one mode's
    /// fault cannot leak into the next. <see cref="ModeSmokeDriver"/> plays a mode; this class sequences them, counts
    /// the log and writes the results.
    ///
    /// <para>Headless, the soak launchers' way (<c>SwitchbackSoakLauncher</c>, Froglet Compute's trainer):</para>
    /// <code>Unity -batchmode -projectPath . -executeMethod CosmicShore.Editor.ModeSmokeLauncher.Run
    ///   -smokeOut Logs/ModeSmoke -smokeModes SkimRace,Joust,Switchback -smokeQuit -logFile -</code>
    /// <para><c>-smokeModes</c>: mode names, <c>quick</c> (the config's quick set) or <c>all</c> (the default).
    /// <c>-smokeConfig</c>: another config asset. <c>-smokeQuit</c>: exit when done, 0 = every mode that ran passed,
    /// 1 = a mode failed or never ran, 3 = the run could not start (no config, an unknown mode name).</para>
    ///
    /// <para>A READER tool: it writes only its result files (default <c>Logs/ModeSmoke</c>, git-ignored), never a
    /// scene, prefab or asset, so it carries no FrogletToolChangeLedger. Shipped assets the game itself writes while
    /// it plays (GameDataSO's runtime state) are written exactly as a person's Play session writes them.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class ModeSmokeLauncher
    {
        const string StateKey = "CosmicShore.ModeSmoke.State";
        const string OfflineKey = "CosmicShore.OfflinePreferred";
        const string BootstrapScene = "Assets/_Scenes/Bootstrap.unity";
        const double PlayStartTimeoutSeconds = 120.0;

        [Serializable]
        class RunState
        {
            public bool active;
            public bool quit;
            public string outDir = "";
            public string configPath = "";
            public int current = -1;
            public bool offlineSaved;
            public int offlineWas;
            public ModeSmokeSummary summary = new();
        }

        static RunState _state;
        static ModeSmokeConfigSO _config;
        static ModeSmokeDriver _driver;
        static double _modeStartedAt;
        static double _exitRequestedAt = -1;

        // Written by the log callback on any thread, read on the main thread.
        static readonly object LogLock = new();
        static volatile int _logPhase; // 0 off, 1 boot, 2 match, 3 exit
        static readonly List<(int phase, LogType type, string message)> PendingLogs = new();

        static ModeSmokeLauncher()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            _state = LoadState();
            // A domain reload on entering Play (when the project turns it on) lands here mid-run: keep counting.
            if (_state is { active: true, current: >= 0 })
            {
                _logPhase = 1;
                _modeStartedAt = EditorApplication.timeSinceStartup;
            }
        }

        // ── Entry points ─────────────────────────────────────
        /// <summary>The batchmode entry (<c>-executeMethod CosmicShore.Editor.ModeSmokeLauncher.Run</c>).</summary>
        public static void Run()
        {
            string modes = Arg("-smokeModes", "all");
            string outDir = Arg("-smokeOut", Path.Combine("Logs", "ModeSmoke"));
            string config = Arg("-smokeConfig", ModeSmokeConfigSO.AssetPath);
            bool quit = HasArg("-smokeQuit");
            if (!Start(modes, outDir, config, quit, out string error))
            {
                Debug.LogWarning("[ModeSmoke] cannot start: " + error);
                if (quit) EditorApplication.Exit(3);
            }
        }

        [MenuItem("FrogletTools/Build/Mode Smoke Test/Run Every Mode", priority = 300)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 4,
            Description = "The launch smoke: every rostered mode from Bootstrap with the AI on every seat, to the game-over event, no errors. Results in Logs/ModeSmoke.")]
        static void RunEveryModeMenu() => StartFromMenu("all");

        [MenuItem("FrogletTools/Build/Mode Smoke Test/Run Quick Set", priority = 301)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 3,
            Description = "The launch smoke on the config's quick set of modes (a few minutes), before a full run.")]
        static void RunQuickSetMenu() => StartFromMenu("quick");

        [MenuItem("FrogletTools/Build/Mode Smoke Test/Select Config", priority = 302)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 2,
            Description = "Select the smoke test's config asset: the rosters, the excluded modes and why, the budgets.")]
        static void SelectConfigMenu()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ModeSmokeConfigSO>(ModeSmokeConfigSO.AssetPath);
            if (cfg) { Selection.activeObject = cfg; EditorGUIUtility.PingObject(cfg); }
            else EditorUtility.DisplayDialog("Mode Smoke Test", "No config at " + ModeSmokeConfigSO.AssetPath, "OK");
        }

        static void StartFromMenu(string modes)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Mode Smoke Test", "Leave Play mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Start(modes, Path.Combine("Logs", "ModeSmoke"), ModeSmokeConfigSO.AssetPath, false, out string error))
                EditorUtility.DisplayDialog("Mode Smoke Test", "Cannot start: " + error, "OK");
        }

        // ── Planning ─────────────────────────────────────────
        static bool Start(string modes, string outDir, string configPath, bool quit, out string error)
        {
            error = null;
            if (_state is { active: true }) { error = "a smoke run is already in progress"; return false; }
            var config = AssetDatabase.LoadAssetAtPath<ModeSmokeConfigSO>(configPath);
            if (config == null) { error = "no ModeSmokeConfigSO at " + configPath; return false; }

            var cards = RosterCards(config);
            if (cards.Count == 0) { error = "the config's rosters list no cards"; return false; }
            if (!SelectModes(config, cards, modes, out var selected, out error)) return false;

            var buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path)).ToList();
            var summary = new ModeSmokeSummary
            {
                startedAt = Now(),
                commit = Commit(),
                unityVersion = Application.unityVersion,
                timeScale = config.TimeScale,
                aiDifficulty = config.AIDifficulty.ToString(),
                batchMode = Application.isBatchMode,
                endConditionsMatchBuild = EndConditionsMatchBuild(),
            };
            foreach (var card in selected)
            {
                var r = new ModeSmokeResult
                {
                    mode = card.Mode.ToString(),
                    modeId = (int)card.Mode,
                    scene = card.SceneName,
                    vessel = card.Vessels is { Count: > 0 } && card.Vessels[0] ? card.Vessels[0].Class.ToString() : "",
                    intensity = config.IntensityFor(card),
                    players = config.PlayersFor(card),
                    matchBudgetSeconds = config.MatchBudgetFor(card.Mode),
                    inBuild = buildScenes.Contains(card.SceneName),
                };
                string reason = config.ExclusionReason(card.Mode);
                if (reason != null) { r.status = ModeSmokeResult.Skipped; r.skipReason = reason; }
                else if (!r.inBuild) r.Evaluate();
                summary.modes.Add(r);
            }
            var rostered = new HashSet<string>(cards.Select(c => c.SceneName));
            foreach (var scene in buildScenes)
                if (!rostered.Contains(scene) && scene.StartsWith("Minigame", StringComparison.Ordinal))
                    summary.uncoveredBuildScenes.Add(scene);

            _config = config;
            _state = new RunState
            {
                active = true,
                quit = quit,
                outDir = Path.GetFullPath(outDir),
                configPath = configPath,
                summary = summary,
                offlineSaved = true,
                offlineWas = PlayerPrefs.GetInt(OfflineKey, 0),
            };
            // Offline (a local host on 127.0.0.1), as the soaks run: a Relay hiccup would bounce the host to the solo
            // menu and void the match. The person's own preference is put back when the run ends.
            PlayerPrefs.SetInt(OfflineKey, 1);
            PlayerPrefs.Save();
            ModeSmokeReport.Write(summary, _state.outDir);
            SaveState();
            Debug.Log($"[ModeSmoke] {summary.modes.Count} mode(s) planned ({summary.Count(ModeSmokeResult.Skipped)} skipped) -> {_state.outDir}");
            EditorApplication.delayCall += RunNext;
            return true;
        }

        /// <summary>Every card on the config's rosters, first listing of each mode wins.</summary>
        static List<SO_ArcadeGame> RosterCards(ModeSmokeConfigSO config)
        {
            var cards = new List<SO_ArcadeGame>();
            var seen = new HashSet<GameModes>();
            foreach (var roster in config.Rosters)
            {
                if (roster == null || roster.Games == null) continue;
                foreach (var card in roster.Games)
                    if (card != null && seen.Add(card.Mode)) cards.Add(card);
            }
            return cards;
        }

        static bool SelectModes(ModeSmokeConfigSO config, List<SO_ArcadeGame> cards, string modes,
            out List<SO_ArcadeGame> selected, out string error)
        {
            error = null;
            string spec = (modes ?? "all").Trim();
            if (spec.Length == 0 || spec.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                selected = cards;
                return true;
            }
            IEnumerable<string> names = spec.Equals("quick", StringComparison.OrdinalIgnoreCase)
                ? config.QuickModes.Select(m => m.ToString())
                : spec.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0);
            selected = new List<SO_ArcadeGame>();
            foreach (var name in names)
            {
                var card = cards.FirstOrDefault(c => c.Mode.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));
                if (card == null)
                {
                    error = $"'{name}' is not a mode on the config's rosters";
                    return false;
                }
                if (!selected.Contains(card)) selected.Add(card);
            }
            if (selected.Count == 0) { error = "no modes selected"; return false; }
            return true;
        }

        // ── Sequencing ───────────────────────────────────────
        static void RunNext()
        {
            if (_state is not { active: true }) return;
            int next = _state.summary.modes.FindIndex(m => m.status == ModeSmokeResult.NotRun);
            if (next < 0) { Complete(); return; }

            _state.current = next;
            SaveState();
            var r = _state.summary.modes[next];
            Debug.Log($"[ModeSmoke] {next + 1}/{_state.summary.modes.Count} {r.mode}: I{r.intensity}, {r.players} seats, scene {r.scene}");
            _modeStartedAt = EditorApplication.timeSinceStartup;
            _exitRequestedAt = -1;
            _driver = null;
            // Count from here: an error in Bootstrap's Awake lands before EnteredPlayMode is raised.
            lock (LogLock) PendingLogs.Clear();
            _logPhase = 1;
            EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (_state is not { active: true } || _state.current < 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                var result = _state.summary.modes[_state.current];
                var card = FindCard(result.modeId);
                if (card == null)
                {
                    result.harnessFault = "the card left the rosters after the run was planned";
                    RequestExit();
                    return;
                }
                _modeStartedAt = EditorApplication.timeSinceStartup;
                _driver = new ModeSmokeDriver(Config(), card, result, _modeStartedAt);
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                FinishMode();
            }
        }

        static void Update()
        {
            if (_state is not { active: true } || _state.current < 0) return;
            double now = EditorApplication.timeSinceStartup;

            if (_exitRequestedAt >= 0)
            {
                if (EditorApplication.isPlaying && now - _exitRequestedAt > Config().ExitTimeoutSeconds)
                {
                    // Leaving Play hung (a network shutdown): the remaining modes cannot get a fresh session.
                    var r = _state.summary.modes[_state.current];
                    r.harnessFault = $"leaving Play did not finish within {Config().ExitTimeoutSeconds:0} s";
                    _logPhase = 0;
                    Settle(r);
                    Complete();
                }
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                if (_driver == null && now - _modeStartedAt > PlayStartTimeoutSeconds)
                {
                    // Play never started (compile errors, a modal in an interactive editor).
                    var r = _state.summary.modes[_state.current];
                    r.harnessFault = $"Play mode did not start within {PlayStartTimeoutSeconds:0} s";
                    Settle(r);
                    Complete();
                }
                return;
            }
            if (_driver == null) return;

            bool done;
            try { done = _driver.Tick(now); }
            catch (Exception e)
            {
                _driver.Abort(now, $"{e.GetType().Name}: {e.Message}");
                done = true;
            }
            if (_logPhase != 3) _logPhase = _driver.IsMatchPhase ? 2 : 1;
            if (done) RequestExit();
        }

        static void RequestExit()
        {
            _logPhase = 3;
            _exitRequestedAt = EditorApplication.timeSinceStartup;
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }

        static void FinishMode()
        {
            if (_state is not { active: true } || _state.current < 0) return;
            var result = _state.summary.modes[_state.current];
            _logPhase = 0;
            // Play ended under the run (the game left Play itself, or a person pressed Stop): judge what it saw.
            if (_driver != null && _driver.Current != ModeSmokeDriver.Phase.Done)
                _driver.Abort(EditorApplication.timeSinceStartup, "Play mode ended before the mode finished");
            _driver?.Unsubscribe();
            _driver = null;
            Settle(result);
            _state.current = -1;
            SaveState();
            EditorApplication.delayCall += RunNext;
        }

        /// <summary>Moves the counted log into the result, judges it and writes the files.</summary>
        static void Settle(ModeSmokeResult result)
        {
            List<(int phase, LogType type, string message)> logs;
            lock (LogLock)
            {
                logs = new List<(int, LogType, string)>(PendingLogs);
                PendingLogs.Clear();
            }
            foreach (var (phase, type, message) in logs)
            {
                if (type == LogType.Exception) result.exceptions++;
                else result.errors++;
                if (phase == 1) result.errorsAtBoot++;
                else if (phase == 2) result.errorsInMatch++;
                else result.errorsAtExit++;
                ModeSmokeReport.KeepError(result, phase == 1 ? "boot" : phase == 2 ? "match" : "exit", message);
            }
            if (result.wallSeconds <= 0f) result.wallSeconds = (float)(EditorApplication.timeSinceStartup - _modeStartedAt);
            result.Evaluate();
            ModeSmokeReport.Write(_state.summary, _state.outDir);
            Debug.Log($"[ModeSmoke] {result.mode}: {result.status}" +
                      (string.IsNullOrEmpty(result.failure) ? "" : " - " + result.failure) +
                      $" (seats {result.aiSeats}/{result.seats} AI, {result.movingSeats} moving, {result.gameSeconds:0} game s, " +
                      $"{result.errors} errors, {result.exceptions} exceptions)");
        }

        static void Complete()
        {
            var summary = _state.summary;
            summary.finishedAt = Now();
            ModeSmokeReport.Write(summary, _state.outDir);
            if (_state.offlineSaved)
            {
                PlayerPrefs.SetInt(OfflineKey, _state.offlineWas);
                PlayerPrefs.Save();
            }
            int code = summary.ExitCode;
            bool quit = _state.quit;
            string dir = _state.outDir;
            _state = new RunState();
            _driver = null;
            _logPhase = 0;
            SaveState();
            Debug.Log($"[ModeSmoke] done: {summary.Count(ModeSmokeResult.Passed)} passed, {summary.Count(ModeSmokeResult.Failed)} failed, " +
                      $"{summary.Count(ModeSmokeResult.Skipped)} skipped, {summary.Count(ModeSmokeResult.NotRun)} not run -> {dir}");
            if (!quit) return;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.Exit(code);
        }

        // ── The log ──────────────────────────────────────────
        static void OnLog(string condition, string stackTrace, LogType type)
        {
            int phase = _logPhase;
            if (phase == 0) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            lock (LogLock) PendingLogs.Add((phase, type, condition));
        }

        // ── Helpers ──────────────────────────────────────────
        static ModeSmokeConfigSO Config()
        {
            if (_config == null && _state != null && !string.IsNullOrEmpty(_state.configPath))
                _config = AssetDatabase.LoadAssetAtPath<ModeSmokeConfigSO>(_state.configPath);
            return _config;
        }

        static SO_ArcadeGame FindCard(int modeId)
        {
            var config = Config();
            return config == null ? null : RosterCards(config).FirstOrDefault(c => (int)c.Mode == modeId);
        }

        static bool EndConditionsMatchBuild()
        {
            var cfg = Resources.Load<EndConditionOverridesSO>(EndConditionOverridesSO.ResourcePath);
            return cfg == null || cfg.LiveMatchesBuild;
        }

        static string Commit()
        {
            string sha = Environment.GetEnvironmentVariable("GITHUB_SHA");
            if (!string.IsNullOrEmpty(sha)) return sha;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetParent(Application.dataPath)?.FullName ?? ".",
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) return "";
                string output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                return output.Length is >= 7 and <= 40 ? output : "";
            }
            catch (Exception) { return ""; }
        }

        static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static RunState LoadState()
        {
            string json = SessionState.GetString(StateKey, "");
            if (string.IsNullOrEmpty(json)) return new RunState();
            try { return JsonUtility.FromJson<RunState>(json) ?? new RunState(); }
            catch (ArgumentException) { return new RunState(); }
        }

        static void SaveState() => SessionState.SetString(StateKey, JsonUtility.ToJson(_state ?? new RunState()));
    }
}
