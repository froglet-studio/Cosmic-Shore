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
            /// <summary>Per-hull starting Time overrides, "Manta=-0.5,Rhino=0.3" (normalized: 1 = level 10), applied over
            /// the card's table in memory for every intensity. Empty: the card's own levels.</summary>
            public string TimeLevels = "";
            /// <summary>GRID mode: the AI seats' hulls in seat order, "Manta,Dolphin,Rhino,Urchin,Stoat" (a full Regatta: the
            /// idle host and these, two domains). Empty: the one-hull time trial.</summary>
            public string Grid = "";
            /// <summary>Override of RegattaCourse.NeutralShareFor for this run, or -1 for the course's own.</summary>
            public float Neutral = -1f;
        }

        [Serializable]
        class Record
        {
            public string session, commit, hull, hostHull, difficulty, outcome, timeLevels;
            public float neutral = -1f;
            public int intensity, race, gatesTotal, gatesThreaded, courseSeed;
            public float finishSeconds;
            public List<float> gateSeconds = new();
            /// <summary>Flat rows of <c>traceColumns</c> floats, one every <see cref="TraceInterval"/> s.</summary>
            public List<float> trace = new();
            public string traceColumns = "t,x,y,z,speed,phase,ring,range";
            /// <summary>The course's rings in scene order, 7 floats each: x, y, z, axis x, y, z, mouth radius.</summary>
            public List<float> rings = new();
            /// <summary>Every 2 s, the hull's speed-ability state where the benchmark knows how to read it (the Stoat's sling).</summary>
            public List<string> diag = new();
        }

        const float TraceInterval = 0.25f;

        [Serializable]
        class GridPilot
        {
            public string hull, domain;
            public bool ally;
            public int gates;
            public List<float> gateSeconds = new();
        }

        [Serializable]
        class GridRecord
        {
            public string session, commit, hostHull, hostDomain, difficulty, outcome, timeLevels, grid;
            public int intensity, race, gatesTotal;
            public float seconds;
            public List<GridPilot> pilots = new();
        }

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
            if (job != null)
            {
                job.TimeLevels = Arg("-regattaTime") ?? "";
                job.Grid = Arg("-regattaGrid") ?? "";
                job.Neutral = float.TryParse(Arg("-regattaNeutral"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float neutral) ? neutral : -1f;
                // A run tag names the file, so a driver launching one editor per hull can collect one run's files.
                string tag = Arg("-regattaTag");
                if (!string.IsNullOrEmpty(tag))
                    job.File = Path.Combine(Path.GetDirectoryName(job.File), $"{tag}_{Path.GetFileName(job.File)}");
                Save(job);
            }
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
                // A batch editor holds nobody's unsaved edits, so it may open the scene itself (the restore after Play
                // mode left another scene active between hulls, 2026-11-03).
                if (Application.isBatchMode && Load() is { Batch: true })
                    EditorSceneManager.OpenScene(BootstrapScene);
                else
                {
                    Status = $"waiting: open {BootstrapScene}, then Resume";
                    return;
                }
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
            if (!string.IsNullOrEmpty(job.Grid))
            {
                var grid = RunGrid(job);
                while (grid.MoveNext()) yield return grid.Current;
                yield break;
            }
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
            ApplyTimeLevels(gameData, job.TimeLevels);
            gameData.ConfigurePlayerCounts(2, 1);
            gameData.RequestedDomainCount = 2;
            gameData.RequestedAIDifficulty = AIDifficultyRules.Resolve(job.Difficulty);
            gameData.SetRequestedAIVessels(new[] { hull });
            RegattaCourse.RailNeutralShareOverride = job.Neutral;
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
                    timeLevels = job.TimeLevels, neutral = job.Neutral,
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
                foreach (var ring in UnityEngine.Object.FindObjectsByType<RaceGateRing>(FindObjectsSortMode.InstanceID))
                {
                    Vector3 c = ring.transform.position, a = ring.Axis;
                    rec.rings.AddRange(new[] { c.x, c.y, c.z, a.x, a.y, a.z, ring.Radius });
                }
                int last = 0;
                float nextSample = 0f, nextDiag = 0f;
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
                    if (Time.time - start >= nextDiag)
                    {
                        nextDiag = Time.time - start + 2f;
                        string d = Diagnose(ai);
                        if (d != null) rec.diag.Add($"{Time.time - start:F1} {d}");
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

        /// <summary>
        /// A full Regatta: the idle host (a hull none of the AIs fly) and one AI per <see cref="Job.Grid"/> hull over two
        /// domains, so the host's team has its allies and the other team its opponents. Races until the first pilot
        /// finishes (the mode's own end) and records every AI's gates and its domain: the spread a person sees in a real race.
        /// </summary>
        static IEnumerator RunGrid(Job job)
        {
            var hulls = job.Grid.Split(',').Select(h => (VesselClassType)Enum.Parse(typeof(VesselClassType), h.Trim())).ToList();
            var hostHull = AllHulls.Select(h => (VesselClassType)Enum.Parse(typeof(VesselClassType), h)).First(h => !hulls.Contains(h));
            var gameData = FindGameData();
            var card = FindRegattaCard();
            Status = "grid: booting";
            double deadline = Now + 120;
            while (Now < deadline && !(IsMenu() && gameData.LocalPlayer?.Vessel != null)) yield return 0.25f;
            yield return 1.5f;

            gameData.SyncFromArcadeGame(card);
            gameData.IsTraining = false;
            if (gameData.selectedVesselClass != null) gameData.selectedVesselClass.Value = hostHull;
            if (gameData.SelectedIntensity != null) gameData.SelectedIntensity.Value = job.Intensity;
            ApplyTimeLevels(gameData, job.TimeLevels);
            gameData.ConfigurePlayerCounts(1 + hulls.Count, 1);
            gameData.RequestedDomainCount = 2;
            gameData.RequestedAIDifficulty = AIDifficultyRules.Resolve(job.Difficulty);
            gameData.SetRequestedAIVessels(hulls);
            RegattaCourse.RailNeutralShareOverride = job.Neutral;
            gameData.InvokeGameLaunch();

            for (int race = 0; race < job.RacesPerHull; race++)
            {
                Status = $"grid: race {race + 1}/{job.RacesPerHull}, loading";
                GateRaceController controller = null;
                List<IPlayer> ais = null;
                deadline = Now + 120;
                while (Now < deadline)
                {
                    if (SceneManager.GetActiveScene().name == gameData.SceneName)
                    {
                        controller = UnityEngine.Object.FindAnyObjectByType<GateRaceController>();
                        ais = gameData.Players.Where(p => p != null && p.IsInitializedAsAI && p.Vessel != null).ToList();
                        if (controller != null && ais.Count >= hulls.Count && controller.AuthoritativeGateCount > 0) break;
                    }
                    yield return 0.25f;
                }
                var host = gameData.LocalPlayer;
                var rec = new GridRecord
                {
                    session = job.Session, commit = job.Commit, hostHull = hostHull.ToString(),
                    hostDomain = host != null ? host.Domain.ToString() : "", difficulty = job.Difficulty.ToString(),
                    intensity = job.Intensity, race = race, timeLevels = job.TimeLevels, grid = job.Grid,
                };
                if (controller == null || ais == null || ais.Count == 0)
                {
                    rec.outcome = "scene-not-ready";
                    AppendLine(job, JsonUtility.ToJson(rec));
                    break;
                }
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
                    AppendLine(job, JsonUtility.ToJson(rec));
                    break;
                }

                Status = $"grid: race {race + 1}/{job.RacesPerHull}, racing";
                float start = Time.time;
                rec.gatesTotal = controller.AuthoritativeGateCount;
                var pilots = ais.Select(p => new GridPilot
                {
                    hull = (p.Vessel.VesselStatus?.VesselType ?? VesselClassType.Any).ToString(),
                    domain = p.Domain.ToString(), ally = host != null && p.Domain == host.Domain,
                }).ToList();
                var last = new int[ais.Count];
                while (true)
                {
                    bool someoneFinished = false;
                    for (int k = 0; k < ais.Count; k++)
                    {
                        int now = ais[k].RoundStats?.SwitchesThreaded ?? 0;
                        while (last[k] < now) { last[k]++; pilots[k].gateSeconds.Add(Time.time - start); }
                        if (now >= rec.gatesTotal) someoneFinished = true;
                    }
                    if (someoneFinished) { rec.outcome = "finished"; break; }
                    if (!gameData.IsTurnRunning) { rec.outcome = "ended"; break; }
                    if (Time.time - start > job.TimeoutSeconds) { rec.outcome = "timeout"; break; }
                    yield return null;
                }
                rec.seconds = Time.time - start;
                for (int k = 0; k < ais.Count; k++) pilots[k].gates = last[k];
                rec.pilots = pilots;
                AppendLine(job, JsonUtility.ToJson(rec));
                Debug.Log($"[RegattaHullBenchmark] grid race {race}: {rec.outcome} {rec.seconds:F1} s - " +
                          string.Join(", ", pilots.Select(p => $"{p.hull}{(p.ally ? "*" : "")} {p.gates}")));

                if (race + 1 >= job.RacesPerHull) break;
                deadline = Now + 30;
                while (gameData.IsTurnRunning && Now < deadline) yield return 0.25f;
                yield return 2.5f;
                controller = UnityEngine.Object.FindAnyObjectByType<GateRaceController>();
                if (controller != null) controller.RequestReplay();
                deadline = Now + 120;
                while (controller != null && Now < deadline) yield return 0.25f;
            }

            job.HullIndex = job.Hulls.Count;
            Save(job);
            Status = "grid: done";
            EditorApplication.isPlaying = false;
        }

        /// <summary>A read-only look inside the hull's speed ability, by reflection (editor diagnostics; nothing is
        /// written). The Stoat: is a pair laid, caught, alive, held, and is each trigger side held.</summary>
        static System.Type _slingType;

        static string Diagnose(IPlayer ai)
        {
            var go = (ai.Vessel?.VesselStatus as Component)?.gameObject;
            if (!go) return null;
            _slingType ??= AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CosmicShore.Gameplay.StoatSlingExecutor")).FirstOrDefault(x => x != null);
            if (_slingType == null) return null;
            var sling = go.GetComponentInChildren(_slingType, true);
            if (!sling) return null;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var t = sling.GetType();
            object Get(string name) => t.GetField(name, F)?.GetValue(sling);
            var holds = Get("_holds") as Array;
            string Hold(int k)
            {
                if (holds == null || k >= holds.Length) return "?";
                var h = holds.GetValue(k);
                return h.GetType().GetField("Holding", F)?.GetValue(h) is true ? "H" : "-";
            }
            var pair = Get("_pair");
            string pairState = "none";
            if (pair != null)
            {
                var pt = pair.GetType();
                bool alive = pt.GetProperty("IsAlive", F)?.GetValue(pair) is true;
                bool held = pt.GetProperty("Held", F)?.GetValue(pair) is true;
                pairState = $"{(alive ? "alive" : "dead")}/{(held ? "held" : "free")}";
            }
            float laidAt = Get("_laidAt") is float la ? la : 0f;
            float lastSling = Get("_aiLastSlingTime") is float ls ? ls : 0f;
            return $"laid={Get("_laid")} caught={Get("_caught")} graze={Get("_grazeOnly")} hold={Hold(0)}{Hold(1)} " +
                   $"pair={pairState} laidAgo={Time.time - laidAt:F1} slingAgo={Time.time - lastSling:F1} " +
                   $"auto={ai.Vessel.VesselStatus.AutoPilotEnabled}";
        }

        static void AppendLine(Job job, string json)
        {
            _lastProgress = EditorApplication.timeSinceStartup;
            File.AppendAllText(job.File, json + "\n", Encoding.UTF8);
        }

        static double Now => EditorApplication.timeSinceStartup;

        /// <summary>
        /// Overrides the card's starting Time per hull for this run only (the published table in GameDataSO, never the
        /// card asset): each named hull's rows are replaced by one every-intensity row with its Mass, Charge and Space
        /// kept and Time set. The vessel reads the table on spawn (VesselController.ApplyStartingElements).
        /// </summary>
        static void ApplyTimeLevels(GameDataSO gameData, string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return;
            var table = new List<VesselStartingElements>(gameData.StartingElements);
            foreach (var pair in spec.Split(','))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2 || !Enum.TryParse(kv[0].Trim(), out VesselClassType hull) ||
                    !float.TryParse(kv[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float time))
                {
                    Debug.LogError($"[RegattaHullBenchmark] bad -regattaTime entry '{pair}'");
                    continue;
                }
                VesselStartingElements.TryResolve(table, hull, gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1,
                    out ResourceCollection levels);
                table.RemoveAll(row => row.Class == hull);
                levels.Time = time;
                table.Add(new VesselStartingElements(hull, 0, levels));
            }
            gameData.PublishStartingElements(table);
        }

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
