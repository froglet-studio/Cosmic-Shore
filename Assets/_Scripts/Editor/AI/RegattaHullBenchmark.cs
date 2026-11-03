using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor.AI
{
    /// <summary>
    /// Times every hull's shipped AI around Regatta, one hull at a time, in the real game.
    /// For each hull it enters Play mode from Bootstrap, launches Regatta through the normal arcade
    /// flow with the host seat idle (in a different hull) and ONE AI seat picked to the hull under test
    /// (<see cref="GameDataSO.RequestedAIVessels"/>, the launch panel's own pick), presses the public
    /// Ready button, timestamps every gate the AI threads from its <c>RoundStats.SwitchesThreaded</c>,
    /// and plays N races via Play Again before moving to the next hull. Racing alone, the AI flies all
    /// three laps (the race ends when the first pilot finishes), so each record is a true finish time.
    ///
    /// Writes one JSON line per race to <c>BenchmarkResults/RegattaHulls/</c> (git-ignored).
    /// A READER of the game: it writes no asset.
    /// </summary>
    public class RegattaHullBenchmark : EditorWindow
    {
        const string PendingKey = "CosmicShore.RegattaHullBenchmark.Job";
        const string BootstrapScene = "Assets/_Scenes/Bootstrap.unity";

        [Serializable]
        public class Job
        {
            public List<string> Hulls = new();
            public int HullIndex;
            public int RacesPerHull = 3;
            public int Intensity = 1;
            public float TimeoutSeconds = 300f;
            public AIDifficulty Difficulty = AIDifficulty.Legendary;
            public string File = "";
            public string Commit = "";
            public string Session = "";
            public bool Batch;
        }

        [Serializable]
        class Record
        {
            public string session, commit, hull, hostHull, difficulty, outcome;
            public int intensity, race, gatesTotal, gatesThreaded, courseSeed;
            public float finishSeconds;
            public List<float> gateSeconds = new();
            /// <summary>Flat rows of <c>traceColumns</c> floats, one every <see cref="TraceInterval"/> s.</summary>
            public List<float> trace = new();
            public string traceColumns = "t,x,y,z,speed,phase,ring,range";
        }

        const float TraceInterval = 0.25f;

        static readonly string[] AllHulls =
            { "Squirrel", "Manta", "Rhino", "Urchin", "Dolphin", "Stoat", "Scarab", "Serpent", "Sparrow" };

        int _races = 3;
        int _intensity = 1;
        float _timeout = 300f;
        AIDifficulty _difficulty = AIDifficulty.Legendary;

        public static string Status { get; private set; } = "idle";

        [MenuItem("FrogletTools/AI/Regatta Hull Benchmark", false, 102)]
        [FrogletTool(FrogletToolCategory.Ai, Importance = 3,
            Description = "Race every hull's AI alone around Regatta through the normal flow and record its finish times.")]
        static void Open() => GetWindow<RegattaHullBenchmark>("Regatta Hull Benchmark");

        void OnGUI()
        {
            _races = EditorGUILayout.IntSlider("Races per hull", _races, 1, 20);
            _intensity = EditorGUILayout.IntSlider("Intensity", _intensity, 1, 4);
            _timeout = EditorGUILayout.FloatField("Per-race timeout (s)", _timeout);
            _difficulty = (AIDifficulty)EditorGUILayout.EnumPopup("AI difficulty", _difficulty);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if (GUILayout.Button("Run all nine hulls (enters Play mode)"))
                    Begin(string.Join(",", AllHulls), _races, _intensity, _timeout, _difficulty);
            EditorGUILayout.HelpBox(Status, MessageType.Info);
        }

        /// <summary>Entry point, also for the CLI (<c>unity command eval</c>). <paramref name="hulls"/> is comma-separated.</summary>
        public static string Begin(string hulls, int racesPerHull, int intensity, float timeoutSeconds,
                                   AIDifficulty difficulty = AIDifficulty.Legendary)
        {
            string session = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var job = new Job
            {
                Hulls = hulls.Split(',').Select(h => h.Trim()).Where(h => h.Length > 0).ToList(),
                RacesPerHull = Mathf.Max(1, racesPerHull),
                Intensity = Mathf.Clamp(intensity, 1, 4),
                TimeoutSeconds = timeoutSeconds,
                Difficulty = difficulty,
                Session = session,
                Commit = GitHead(),
                File = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BenchmarkResults",
                    "RegattaHulls", $"regatta_I{intensity}_{session}.jsonl"),
            };
            foreach (var h in job.Hulls)
                if (!Enum.TryParse(h, out VesselClassType _)) return $"not a hull: {h}";
            Directory.CreateDirectory(Path.GetDirectoryName(job.File));
            Save(job);
            Status = "starting";
            // Deferred so a CLI caller gets its answer before Play mode blocks the main thread.
            EditorApplication.delayCall += EnterPlay;
            return job.File;
        }

        /// <summary>
        /// Headless entry: <c>Unity -batchmode -projectPath &lt;worktree&gt; -executeMethod
        /// CosmicShore.Editor.AI.RegattaHullBenchmark.RunBatch -regattaHulls Manta,Rhino -regattaRaces 3
        /// -regattaIntensity 1 [-regattaTimeout 300] [-logFile &lt;path&gt;]</c>. A batchmode editor ticks with no window
        /// and no focus, which an unattended run needs. Opens Bootstrap (a batch editor has no person's unsaved edits to
        /// lose), runs the job, and exits: 0 done, 3 no progress for <see cref="BatchStallSeconds"/>.
        /// </summary>
        public static void RunBatch()
        {
            string hulls = Arg("-regattaHulls") ?? string.Join(",", AllHulls);
            int races = int.TryParse(Arg("-regattaRaces"), out int r) ? r : 3;
            int intensity = int.TryParse(Arg("-regattaIntensity"), out int i) ? i : 1;
            float timeout = float.TryParse(Arg("-regattaTimeout"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float t) ? t : 300f;
            // A batch editor runs frames as fast as it can; the race is timed in game seconds either way, and a capped
            // frame rate keeps a long unattended run from holding the machine at full load.
            Application.targetFrameRate = 60;
            EditorSceneManager.OpenScene(BootstrapScene);
            string file = Begin(hulls, races, intensity, timeout);
            var job = Load();
            if (job == null)
            {
                Debug.LogError($"[RegattaHullBenchmark] batch: {file}");
                EditorApplication.Exit(2);
                return;
            }
            job.Batch = true;
            Save(job);
            _lastProgress = EditorApplication.timeSinceStartup;
            Debug.Log($"[RegattaHullBenchmark] batch: {hulls}, {races} race(s) each at I{intensity} -> {file}");
        }

        const double BatchStallSeconds = 900;
        static double _lastProgress;

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int k = 0; k + 1 < args.Length; k++)
                if (args[k] == name) return args[k + 1];
            return null;
        }

        /// <summary>Enters Play mode for the next hull of a pending job (for a CLI driver when the editor is unfocused).</summary>
        public static void Resume()
        {
            if (Load() != null) EnterPlay();
        }

        /// <summary>Stops after the current race's play session ends.</summary>
        public static void Cancel()
        {
            SessionState.EraseString(PendingKey);
            _stepper = null;
            Status = "cancelled";
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        static void Save(Job job) => SessionState.SetString(PendingKey, JsonUtility.ToJson(job));

        static Job Load()
        {
            string json = SessionState.GetString(PendingKey, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Job>(json);
        }

        /// <summary>
        /// Plays the Bootstrap scene that is already open. Nothing else: opening a scene, a save prompt, or a play-mode
        /// start scene (which makes Unity ask to save a modified open scene) would block an unattended run, and could
        /// discard a person's unsaved edits. With another scene open it waits; open Bootstrap and call <see cref="Resume"/>.
        /// </summary>
        static void EnterPlay()
        {
            if (EditorApplication.isPlaying) return;
            if (SceneManager.GetActiveScene().path != BootstrapScene)
            {
                Status = $"waiting: open {BootstrapScene}, then Resume";
                return;
            }
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            var job = Load();
            if (job == null) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                _lastProgress = EditorApplication.timeSinceStartup;
                _stepper = RunHull(job);
                _waitUntil = 0;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                _stepper = null;
                if (job.HullIndex >= job.Hulls.Count)
                {
                    SessionState.EraseString(PendingKey);
                    Status = $"done -> {job.File}";
                    Debug.Log($"[RegattaHullBenchmark] {Status}");
                    if (job.Batch) EditorApplication.Exit(0);
                    return;
                }
                EditorApplication.delayCall += EnterPlay;
            }
        }

        // ── a tiny coroutine stepper on the editor loop (survives every scene load) ──

        static IEnumerator _stepper;
        static double _waitUntil;

        static void Tick()
        {
            if (Application.isBatchMode && _lastProgress > 0 &&
                EditorApplication.timeSinceStartup - _lastProgress > BatchStallSeconds && Load() is { Batch: true })
            {
                Debug.LogError($"[RegattaHullBenchmark] batch: no progress for {BatchStallSeconds} s; exiting.");
                EditorApplication.Exit(3);
                return;
            }
            if (_stepper == null || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < _waitUntil) return;
            try
            {
                if (!_stepper.MoveNext()) { _stepper = null; return; }
                _waitUntil = _stepper.Current is float s ? EditorApplication.timeSinceStartup + s : 0;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _stepper = null;
                EditorApplication.isPlaying = false;
            }
        }

        static IEnumerator RunHull(Job job)
        {
            var hull = (VesselClassType)Enum.Parse(typeof(VesselClassType), job.Hulls[job.HullIndex]);
            var hostHull = hull == VesselClassType.Sparrow ? VesselClassType.Manta : VesselClassType.Sparrow;
            var gameData = FindGameData();
            var card = FindRegattaCard();
            if (gameData == null || card == null)
            {
                Debug.LogError("[RegattaHullBenchmark] GameDataSO or the Regatta card not found.");
                Cancel();
                yield break;
            }

            Status = $"{hull}: booting";
            double deadline = Now + 120;
            while (Now < deadline && !(IsMenu() && gameData.LocalPlayer?.Vessel != null)) yield return 0.25f;
            yield return 1.5f;

            gameData.SyncFromArcadeGame(card);
            gameData.IsTraining = false;
            if (gameData.selectedVesselClass != null) gameData.selectedVesselClass.Value = hostHull;
            if (gameData.SelectedIntensity != null) gameData.SelectedIntensity.Value = job.Intensity;
            gameData.ConfigurePlayerCounts(2, 1);
            gameData.RequestedDomainCount = 2;
            gameData.RequestedAIDifficulty = AIDifficultyRules.Resolve(job.Difficulty);
            gameData.SetRequestedAIVessels(new[] { hull });
            gameData.InvokeGameLaunch();

            for (int race = 0; race < job.RacesPerHull; race++)
            {
                Status = $"{hull}: race {race + 1}/{job.RacesPerHull}, loading";
                GateRaceController controller = null;
                IPlayer ai = null;
                deadline = Now + 90;
                while (Now < deadline)
                {
                    if (SceneManager.GetActiveScene().name == gameData.SceneName)
                    {
                        controller = UnityEngine.Object.FindAnyObjectByType<GateRaceController>();
                        ai = gameData.Players.FirstOrDefault(p => p != null && p.IsInitializedAsAI && p.Vessel != null);
                        if (controller != null && ai != null && controller.AuthoritativeGateCount > 0) break;
                    }
                    yield return 0.25f;
                }
                var rec = new Record
                {
                    session = job.Session, commit = job.Commit, hull = hull.ToString(), hostHull = hostHull.ToString(),
                    difficulty = job.Difficulty.ToString(), intensity = job.Intensity, race = race,
                };
                if (controller == null || ai == null)
                {
                    rec.outcome = "scene-not-ready";
                    Append(job, rec);
                    break;
                }
                var flown = ai.Vessel.VesselStatus?.VesselType ?? VesselClassType.Any;
                if (flown != hull) rec.hull = $"{hull} (seated {flown})";
                yield return 1f;

                deadline = Now + 120;
                while (!gameData.IsTurnRunning && Now < deadline)
                {
                    controller.OnReadyClicked();
                    double repress = Now + 15;
                    while (!gameData.IsTurnRunning && Now < repress && Now < deadline) yield return 0.25f;
                }
                if (!gameData.IsTurnRunning)
                {
                    rec.outcome = "not-started";
                    Append(job, rec);
                    break;
                }

                Status = $"{hull}: race {race + 1}/{job.RacesPerHull}, racing";
                float start = Time.time;
                int total = controller.AuthoritativeGateCount;
                rec.gatesTotal = total;
                rec.courseSeed = controller.CourseSeed;
                int last = 0;
                float nextSample = 0f;
                var status = ai.Vessel.VesselStatus as Component;
                AIPilot pilot = null;
                if (status) status.TryGetComponent(out pilot);
                while (true)
                {
                    int now = ai.RoundStats?.SwitchesThreaded ?? 0;
                    while (last < now) { last++; rec.gateSeconds.Add(Time.time - start); }
                    if (Time.time - start >= nextSample && ai.Vessel?.Transform != null)
                    {
                        // One flight-trace row: t, x, y, z, speed, objective phase, ring, range to the objective.
                        nextSample = Time.time - start + TraceInterval;
                        Vector3 p = ai.Vessel.Transform.position;
                        var obj = pilot ? pilot.Objective : default;
                        rec.trace.AddRange(new[]
                        {
                            Time.time - start, p.x, p.y, p.z, ai.Vessel.VesselStatus.Speed,
                            obj.Has ? (float)obj.Phase : -1f, obj.Has ? obj.Ring : -1f,
                            obj.Has ? Vector3.Distance(p, obj.Objective) : -1f,
                        });
                    }
                    if (now >= total) { rec.outcome = "finished"; break; }
                    if (!gameData.IsTurnRunning) { rec.outcome = "ended-early"; break; }
                    if (Time.time - start > job.TimeoutSeconds) { rec.outcome = "timeout"; break; }
                    yield return null;
                }
                rec.gatesThreaded = last;
                rec.finishSeconds = rec.outcome == "finished" ? rec.gateSeconds[rec.gateSeconds.Count - 1] : Time.time - start;
                Append(job, rec);
                Debug.Log($"[RegattaHullBenchmark] {rec.hull} I{rec.intensity} race {race}: {rec.outcome} " +
                          $"{rec.finishSeconds:F1} s, {rec.gatesThreaded}/{rec.gatesTotal} gates");

                if (race + 1 >= job.RacesPerHull) break;
                if (rec.outcome == "timeout")
                {
                    // The timeout ends the hull: the next race would only time out the same way.
                    break;
                }

                // Let the referee close the turn (a timed-out turn is still running), then Play Again.
                deadline = Now + 30;
                while (gameData.IsTurnRunning && Now < deadline) yield return 0.25f;
                yield return 2.5f;
                controller = UnityEngine.Object.FindAnyObjectByType<GateRaceController>();
                if (controller != null) controller.RequestReplay();
                deadline = Now + 120;
                while (controller != null && Now < deadline) yield return 0.25f;
            }

            job.HullIndex++;
            Save(job);
            Status = $"{hull}: done";
            EditorApplication.isPlaying = false;
        }

        static double Now => EditorApplication.timeSinceStartup;

        static void Append(Job job, Record rec)
        {
            _lastProgress = EditorApplication.timeSinceStartup;
            File.AppendAllText(job.File, JsonUtility.ToJson(rec) + "\n", Encoding.UTF8);
        }

        static bool IsMenu() => SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        static GameDataSO FindGameData()
        {
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            if (all.Length > 0) return all[0];
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameDataSO>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        static SO_ArcadeGame FindRegattaCard()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SO_ArcadeGame"))
            {
                var g = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(AssetDatabase.GUIDToAssetPath(guid));
                if (g != null && g.Mode == GameModes.Regatta) return g;
            }
            return null;
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
                    WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
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
