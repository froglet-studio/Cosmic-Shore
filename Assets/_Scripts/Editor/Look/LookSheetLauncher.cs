using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The look sheet: the real game, in the real renderer, captured to PNGs without a person at the editor
    /// (Docs/LOOK_SHEET.md, Docs/SKILL_TRAINING_PLAN.md section 3.1). From the command line, the soak launchers' way
    /// (<c>SwitchbackSoakLauncher</c>, and the per-mode smoke test on its branch):
    /// <code>Unity -batchmode -projectPath . -executeMethod CosmicShore.Editor.LookSheetLauncher.Run
    ///   -lookScreens HOME,HANGAR,PORT -lookOut Logs/LookSheet -lookQuit -logFile -</code>
    /// <code>Unity -batchmode -projectPath . -executeMethod CosmicShore.Editor.LookSheetLauncher.Run
    ///   -lookMode switchback -lookTimes 5,15,30 -lookIntensity 1 -lookOut Logs/LookSheet -lookQuit -logFile -</code>
    /// Never add <c>-quit</c> (Unity would exit before Play starts) and never <c>-nographics</c> (nothing would render).
    ///
    /// <para>What it plays: <c>-lookScreens</c> boots from Bootstrap to Menu_Main (offline, the first-run privacy overlay
    /// closed unanswered and the username step skipped, as the soak runners do), opens each named screen through
    /// <see cref="ScreenSwitcher"/>'s own navigation and captures it at the PC size and at the phone scale.
    /// <c>-lookMode switchback</c> or <c>thresher-time</c> starts that mode's shipped soak runner (one race, every seat
    /// the AI, real time) and captures the game camera at the game seconds in <c>-lookTimes</c> after the turn starts.
    /// Other modes follow when the per-mode smoke test (PR #1754) lands: its driver plays any card the same way.</para>
    ///
    /// <para>Other arguments: <c>-lookSize 1920x1080</c>, <c>-lookPhone 1600x900</c> (two times the 800x450 phone scale),
    /// <c>-lookSettle 1.0</c> (seconds a screen is given before its capture), <c>-lookQuit</c> (exit 0 when every capture
    /// was written, 1 otherwise, 3 when the run could not start).</para>
    ///
    /// <para>A READER tool: it writes only PNGs and <c>manifest.jsonl</c> under the output folder (default
    /// <c>Logs/LookSheet</c>), never a scene, prefab or asset, so it carries no FrogletToolChangeLedger. It turns the
    /// offline preference on for the run and puts it back.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class LookSheetLauncher
    {
        const string StateKey = "CosmicShore.LookSheet.Plan";
        const string OfflineKey = "CosmicShore.OfflinePreferred";
        const string BootstrapScene = "Assets/_Scenes/Bootstrap.unity";
        const double BootTimeoutSeconds = 180.0;
        const double StartTimeoutSeconds = 420.0;
        const double GatePokeSeconds = 0.25;

        [Serializable]
        sealed class Plan
        {
            public bool active;
            public bool quit;
            public string outDir = "Logs/LookSheet";
            public string[] screens = Array.Empty<string>();
            public string mode = "";
            public int intensity = 1;
            public float[] times = Array.Empty<float>();
            public int width = 1920;
            public int height = 1080;
            public int phoneWidth = 1600;
            public int phoneHeight = 900;
            public float settle = 1f;
            public string stage = "boot";
            public int index;
            public double stageStarted;
            public int captured;
            public int failed;
            public bool offlineSaved;
            public int offlineWas;
            public bool skippedName;
            public float turnStartedAt = -1f;
            public double lastPoke;
            public string notes = "";
        }

        static Plan _plan;
        static double _exitRequestedAt = -1;

        static LookSheetLauncher()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            _plan = Load();
        }

        // ── Entry points ─────────────────────────────────────
        /// <summary>The batchmode entry (<c>-executeMethod CosmicShore.Editor.LookSheetLauncher.Run</c>).</summary>
        public static void Run()
        {
            var plan = new Plan
            {
                outDir = Arg("-lookOut", Path.Combine("Logs", "LookSheet")),
                screens = Split(Arg("-lookScreens", "")),
                mode = Arg("-lookMode", "").Trim().ToLowerInvariant(),
                intensity = ParseInt(Arg("-lookIntensity", "1"), 1),
                times = ParseTimes(Arg("-lookTimes", "5,15,30")),
                settle = ParseFloat(Arg("-lookSettle", "1"), 1f),
                quit = HasArg("-lookQuit"),
            };
            ParseSize(Arg("-lookSize", "1920x1080"), out plan.width, out plan.height, 1920, 1080);
            ParseSize(Arg("-lookPhone", "1600x900"), out plan.phoneWidth, out plan.phoneHeight, 1600, 900);
            if (plan.screens.Length == 0 && plan.mode.Length == 0) plan.screens = new[] { "HOME" };
            if (!Start(plan, out string error))
            {
                Debug.LogWarning("[LookSheet] cannot start: " + error);
                if (plan.quit) EditorApplication.Exit(3);
            }
        }

        [MenuItem("FrogletTools/Build/Look Sheet/Menu screens", priority = 310)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 3,
            Description = "The look sheet of the menu: boots to Menu_Main and captures Home, Hangar, Port, Profile and Codex at PC size and phone scale into Logs/LookSheet.")]
        static void MenuScreensMenu() => StartFromMenu(new Plan
        {
            screens = new[] { "HOME", "HANGAR", "PORT", "PROFILE", "CODEX" },
        });

        [MenuItem("FrogletTools/Build/Look Sheet/Switchback, the Dolphin's AI", priority = 311)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 3,
            Description = "The look sheet of a race: the Switchback soak (every seat the AI) captured at 5, 15 and 30 game seconds into Logs/LookSheet.")]
        static void SwitchbackMenu() => StartFromMenu(new Plan { mode = "switchback", times = new[] { 5f, 15f, 30f } });

        [MenuItem("FrogletTools/Build/Look Sheet/Thresher Time, the Thresher's AI", priority = 312)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 2,
            Description = "The look sheet of Thresher Time: the shipped soak (every seat the AI) captured at 5, 15 and 30 game seconds into Logs/LookSheet.")]
        static void ThresherMenu() => StartFromMenu(new Plan { mode = "thresher-time", times = new[] { 5f, 15f, 30f } });

        static void StartFromMenu(Plan plan)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Look Sheet", "Leave Play mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Start(plan, out string error))
                EditorUtility.DisplayDialog("Look Sheet", "Cannot start: " + error, "OK");
        }

        static bool Start(Plan plan, out string error)
        {
            error = null;
            if (_plan is { active: true }) { error = "a look-sheet run is already in progress"; return false; }
            if (EditorApplication.isPlaying) { error = "leave Play mode first"; return false; }
            if (plan.mode.Length > 0 && plan.mode != "switchback" && plan.mode != "thresher-time")
            {
                error = $"unknown mode '{plan.mode}' (switchback or thresher-time today; every card once the per-mode smoke test lands)";
                return false;
            }
            if (!File.Exists(BootstrapScene)) { error = BootstrapScene + " not found"; return false; }
            Directory.CreateDirectory(plan.outDir);
            plan.active = true;
            plan.stage = "boot";
            plan.offlineSaved = true;
            plan.offlineWas = PlayerPrefs.GetInt(OfflineKey, 0);
            PlayerPrefs.SetInt(OfflineKey, 1);
            PlayerPrefs.Save();
            _plan = plan;
            Save();
            EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            return true;
        }

        // ── The run ──────────────────────────────────────────
        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (_plan is not { active: true }) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                _plan.stage = "boot";
                _plan.stageStarted = EditorApplication.timeSinceStartup;
                Save();
                if (_plan.mode.Length > 0) LaunchSoak();
            }
            else if (change == PlayModeStateChange.EnteredEditMode && _plan.stage == "exiting")
            {
                FinishInEditMode();
            }
        }

        static void LaunchSoak()
        {
            string output = Path.Combine(_plan.outDir, "soak.jsonl");
            try
            {
                if (_plan.mode == "switchback")
                {
                    SwitchbackSoakRunner.Launch(new SwitchbackSoakRunner.Settings
                    {
                        Races = 1,
                        Intensity = _plan.intensity,
                        TotalPlayers = 3,
                        TimeScale = 1f,
                        OutputFile = output,
                        Tag = "look-sheet",
                    });
                }
                else if (_plan.mode == "thresher-time")
                {
                    ThresherTimeSoakRunner.Launch(new ThresherTimeSoakRunner.Settings
                    {
                        Races = 1,
                        Intensities = new[] { _plan.intensity },
                        TotalPlayers = 3,
                        TimeScale = 1f,
                        OutputFile = output,
                        Tag = "look-sheet",
                    });
                }
            }
            catch (Exception e)
            {
                Note("the soak runner did not start: " + e.Message);
                Finish(false);
            }
        }

        static void Update()
        {
            if (_plan is not { active: true }) return;
            if (_plan.stage == "exiting")
            {
                TickExit();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            switch (_plan.stage)
            {
                case "boot": TickBoot(now); break;
                case "settle": TickSettle(now); break;
                case "screens": TickScreens(now); break;
                case "race": TickRace(now); break;
            }
        }

        static void TickBoot(double now)
        {
            var gameData = FindGameData();
            bool inMenu = SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;
            if (_plan.mode.Length > 0)
            {
                // The soak runner boots the game itself; we only wait for its race.
                Enter("race", now);
                return;
            }
            if (inMenu && gameData != null && gameData.LocalPlayer?.Vessel != null)
            {
                Enter("settle", now);
                return;
            }
            if (now - _plan.lastPoke >= GatePokeSeconds)
            {
                _plan.lastPoke = now;
                PokeBootGates();
            }
            if (now - _plan.stageStarted > BootTimeoutSeconds)
            {
                Note($"the menu never gave a local pilot within {BootTimeoutSeconds:0} s");
                Finish(false);
            }
        }

        /// <summary>
        /// Nobody is at the keyboard for the first-run age gate or the username step, and the boot holds for both. The
        /// soak runners' answer, unchanged (and the per-mode smoke test's): close the overlay unanswered, nothing is
        /// recorded; take the username step's own route to the menu without claiming a name. This is one more copy of
        /// that handling; the smoke test's PR proposes one shared helper, which this should call once it exists.
        /// </summary>
        static void PokeBootGates()
        {
            var privacy = PrivacyConsentOverlay.Current;
            if (privacy != null)
            {
                Note("closed the unanswered privacy overlay");
                UnityEngine.Object.Destroy(privacy.gameObject);
            }
            if (_plan.skippedName) return;
            var auth = UnityEngine.Object.FindAnyObjectByType<AuthenticationSceneController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (auth != null && auth.GetType().GetField("_awaitingPlayerInput", flags)?.GetValue(auth) is true)
            {
                _plan.skippedName = true;
                Note("skipped the username step");
                auth.GetType().GetMethod("NavigateToMainMenu", flags)?.Invoke(auth, null);
            }
        }

        static void TickSettle(double now)
        {
            if (now - _plan.stageStarted < 1.5 + _plan.settle) return;
            _plan.index = 0;
            Enter("screens", now);
        }

        static void TickScreens(double now)
        {
            if (_plan.index >= _plan.screens.Length)
            {
                Finish(_plan.failed == 0 && _plan.captured > 0);
                return;
            }
            string screen = _plan.screens[_plan.index].Trim();
            // First visit: ask the switcher for the screen, then give it settle seconds before the capture.
            if (_plan.stageStarted < 0)
            {
                var switcher = UnityEngine.Object.FindAnyObjectByType<ScreenSwitcher>();
                if (switcher == null)
                {
                    Note("no ScreenSwitcher in the menu");
                    _plan.failed++;
                    NextScreen(now);
                    return;
                }
                if (!ShowScreen(switcher, screen))
                {
                    Note($"unknown screen '{screen}' (HOME, HANGAR, PORT, STORE, CODEX, ARK, PROFILE, ARCADE, TOYBOX, ARENA)");
                    _plan.failed++;
                    NextScreen(now);
                    return;
                }
                _plan.stageStarted = now;
                Save();
                return;
            }
            if (now - _plan.stageStarted < _plan.settle) return;
            string stem = "menu_" + screen.ToLowerInvariant();
            CaptureAll(stem, screen);
            NextScreen(now);
        }

        static void NextScreen(double now)
        {
            _plan.index++;
            _plan.stageStarted = -1;
            Save();
        }

        static bool ShowScreen(ScreenSwitcher switcher, string screen)
        {
            switch (screen.ToUpperInvariant())
            {
                case "HOME": switcher.OnClickHomeNav(); return true;
                case "HANGAR": switcher.OnClickHangarNav(); return true;
                case "PORT": switcher.OnClickPortNav(); return true;
                case "STORE": switcher.OnClickStoreNav(); return true;
                case "CODEX": switcher.OnClickCodexNav(); return true;
                case "ARK": switcher.OnClickArkNav(); return true;
                case "PROFILE": switcher.OnClickProfileNav(); return true;
                case "ARCADE": switcher.OnClickArcadeNav(); return true;
                case "TOYBOX": switcher.OnClickToyboxNav(); return true;
                case "ARENA": switcher.OnClickArenaNav(); return true;
                default: return false;
            }
        }

        static void TickRace(double now)
        {
            var gameData = FindGameData();
            if (gameData == null) return;
            if (_plan.turnStartedAt < 0f)
            {
                if (gameData.IsTurnRunning)
                {
                    _plan.turnStartedAt = Time.time;
                    _plan.index = 0;
                    Note($"turn started at game time {Time.time:0.0}");
                    Save();
                }
                else if (now - _plan.stageStarted > StartTimeoutSeconds)
                {
                    Note($"no turn started within {StartTimeoutSeconds:0} s");
                    Finish(false);
                }
                return;
            }
            if (_plan.index >= _plan.times.Length)
            {
                Finish(_plan.failed == 0 && _plan.captured > 0);
                return;
            }
            float due = _plan.times[_plan.index];
            if (Time.time - _plan.turnStartedAt < due) return;
            if (!gameData.IsTurnRunning)
            {
                Note($"the turn ended before {due:0} s");
                _plan.failed++;
                Finish(false);
                return;
            }
            string stem = $"mode_{_plan.mode}_t{due.ToString("0", CultureInfo.InvariantCulture)}";
            CaptureOne(stem + ".png", _plan.width, _plan.height, $"{_plan.mode} t={due:0} s");
            _plan.index++;
            Save();
        }

        // ── Captures ─────────────────────────────────────────
        static void CaptureAll(string stem, string label)
        {
            CaptureOne(stem + "_pc.png", _plan.width, _plan.height, label + " (PC)");
            CaptureOne(stem + "_phone.png", _plan.phoneWidth, _plan.phoneHeight, label + " (phone scale)");
            if (!Application.isBatchMode)
            {
                var gv = LookSheetCapture.CaptureGameView(Path.Combine(_plan.outDir, stem + "_gameview.png"), 1, label + " (Game view)");
                LookSheetCapture.Append(_plan.outDir, gv);
            }
        }

        static void CaptureOne(string file, int width, int height, string label)
        {
            var record = LookSheetCapture.Capture(Path.Combine(_plan.outDir, file), width, height, label);
            LookSheetCapture.Append(_plan.outDir, record);
            if (record.Ok)
            {
                _plan.captured++;
                Debug.Log($"[LookSheet] wrote {record.path} ({width}x{height}, {record.cameras} cameras, {record.overlayCanvases} overlay canvases)");
            }
            else
            {
                _plan.failed++;
                Debug.LogWarning($"[LookSheet] {file}: {record.error}");
            }
        }

        // ── Finish ───────────────────────────────────────────
        static void Finish(bool ok)
        {
            if (_plan == null) return;
            _plan.notes = (_plan.notes + (ok ? "" : " [FAILED]")).Trim();
            Time.timeScale = 1f;
            WriteSummary(ok);
            if (_plan.offlineSaved)
            {
                PlayerPrefs.SetInt(OfflineKey, _plan.offlineWas);
                PlayerPrefs.Save();
                _plan.offlineSaved = false;
            }
            if (_plan.quit)
            {
                _plan.stage = "exiting";
                _exitRequestedAt = EditorApplication.timeSinceStartup;
                Save();
                EditorApplication.isPlaying = false;
                return;
            }
            _plan.active = false;
            Save();
            Debug.Log($"[LookSheet] done: {_plan.captured} captured, {_plan.failed} failed, in {_plan.outDir}");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        static void TickExit()
        {
            // Leave Play, then quit; if leaving Play hangs (a network shutdown), quit anyway after a minute.
            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - _exitRequestedAt < 60.0) return;
            FinishInEditMode();
        }

        static void FinishInEditMode()
        {
            if (_plan is not { active: true }) return;
            int code = _plan.failed == 0 && _plan.captured > 0 ? 0 : 1;
            _plan.active = false;
            Save();
            Debug.Log($"[LookSheet] exit {code}: {_plan.captured} captured, {_plan.failed} failed, in {_plan.outDir}");
            EditorApplication.Exit(code);
        }

        static void WriteSummary(bool ok)
        {
            try
            {
                var summary = new Summary
                {
                    ok = ok,
                    captured = _plan.captured,
                    failed = _plan.failed,
                    mode = _plan.mode,
                    screens = _plan.screens,
                    times = _plan.times,
                    notes = _plan.notes,
                    unity = Application.unityVersion,
                    batchmode = Application.isBatchMode,
                    stamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                };
                File.WriteAllText(Path.Combine(_plan.outDir, "summary.json"), JsonUtility.ToJson(summary, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LookSheet] summary not written: " + e.Message);
            }
        }

        [Serializable]
        sealed class Summary
        {
            public bool ok;
            public int captured;
            public int failed;
            public string mode = "";
            public string[] screens = Array.Empty<string>();
            public float[] times = Array.Empty<float>();
            public string notes = "";
            public string unity = "";
            public bool batchmode;
            public string stamp = "";
        }

        // ── Helpers ──────────────────────────────────────────
        static void Enter(string stage, double now)
        {
            _plan.stage = stage;
            _plan.stageStarted = stage == "screens" ? -1 : now;
            Save();
        }

        static void Note(string line)
        {
            if (_plan == null) return;
            if (_plan.notes.Contains(line)) return;
            _plan.notes = _plan.notes.Length == 0 ? line : _plan.notes + "; " + line;
        }

        static GameDataSO FindGameData()
        {
            var loaded = Resources.FindObjectsOfTypeAll<GameDataSO>();
            if (loaded is { Length: > 0 }) return loaded[0];
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameDataSO>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        static void Save()
        {
            if (_plan == null) return;
            SessionState.SetString(StateKey, JsonUtility.ToJson(_plan));
        }

        static Plan Load()
        {
            string json = SessionState.GetString(StateKey, "");
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<Plan>(json); }
            catch { return null; }
        }

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static string[] Split(string list)
        {
            if (string.IsNullOrWhiteSpace(list)) return Array.Empty<string>();
            var parts = new List<string>();
            foreach (var p in list.Split(','))
                if (!string.IsNullOrWhiteSpace(p)) parts.Add(p.Trim());
            return parts.ToArray();
        }

        static float[] ParseTimes(string list)
        {
            var times = new List<float>();
            foreach (var p in Split(list))
                if (float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out float t) && t >= 0f) times.Add(t);
            times.Sort();
            return times.ToArray();
        }

        static int ParseInt(string s, int fallback) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        static float ParseFloat(string s, float fallback) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        static void ParseSize(string s, out int width, out int height, int fallbackWidth, int fallbackHeight)
        {
            width = fallbackWidth;
            height = fallbackHeight;
            var parts = (s ?? "").ToLowerInvariant().Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) && w > 0 && h > 0)
            {
                width = w;
                height = h;
            }
        }
    }
}
