using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The Brood Rush balance audit in the REAL Unity game (2026-11-21): AI-only matches, a hull asked for every
    /// seat, one fresh Play session from Bootstrap per match, the seat-by-seat books written after every match.
    /// It exists to compare the real game's seats against the Amoebius Brood judge
    /// (<c>Tools/Compute/evaluators/brood_judge.py</c>), which reports the fleet within the bar while a playtest did not.
    ///
    /// <code>Unity -batchmode -projectPath . -executeMethod CosmicShore.Editor.BroodAuditLauncher.Run -logFile -
    ///   -broodOut Logs/BroodAudit -broodRosters "Meerkat,Wrasse,Urchin/Honeyguide,Stoat,Sparrow;..." -broodSeeds 1,2
    ///   [-broodLimit 300] [-broodTimeScale 1] [-broodDifficulty Legendary] [-broodIntensity 2] [-broodPark]
    ///   [-broodStock] -broodQuit</code>
    /// <para>A roster is team A / team B. Without <c>-broodPark</c> the host flies team A's first hull, handed to the
    /// AI as the smoke test does; with it the host is parked outside the cell on a hull nobody flies and every roster
    /// hull is a backfill AI, as the judge does. <c>-broodStock</c> keeps the card's own hull list (an off-card pick is
    /// then re-seated by the game, which is what a person launching from the menu gets).</para>
    /// <para>A READER tool: it writes only its result files, never a scene, prefab or asset.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class BroodAuditLauncher
    {
        const string StateKey = "CosmicShore.BroodAudit.State";
        const string OfflineKey = "CosmicShore.OfflinePreferred";
        const string BootstrapScene = "Assets/_Scenes/Bootstrap.unity";
        const string CardPath = "Assets/_SO_Assets/Games/ArcadeGameBroodRush.asset";
        const double PlayStartTimeout = 900, ExitTimeout = 180;

        [Serializable]
        public class Seat
        {
            public string name, hull, domain, startLevels, pilots, pilotsAtEnd, aiPilot, difficulty, abilityS, abilities;
            public bool isHost, requestedOnSeat;
            public float trailVolume, vc, vd, lost, held, speed, nucleusS, volumeCreated, stolen, destroyed;
            public int superShields, teamBroods, blocks, crystals, abilityStarts;
        }

        [Serializable]
        public class Match
        {
            public int index, seed, intensity;
            public string teamA, teamB, teamC = "", hostHull, difficulty = "Legendary";
            public bool parkHost, stockCard, waitClient;
            public float limitS = 300f, timeScale = 1f;
            public string status = "planned", phaseReached = "", outcome = "", fault = "", notes = "", broods = "",
                winner = "", claim = "", hostDomainAtLaunch = "", allowed = "", hostBooks = "";
            public float raceS, wallS;
            public int serpentWallsMax, serpentBricksMax, serpentActiveMax;
            public List<Seat> seats = new();

            static string[] Hulls(string team) => (team ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            /// <summary>The teams in order (two or three): team A is the host's.</summary>
            public List<string[]> Teams() => new[] { teamA, teamB, teamC }.Select(Hulls).Where(t => t.Length > 0).ToList();
            public int TeamCount => Teams().Count;
            string[] A => Hulls(teamA);
            public IEnumerable<string> AllHulls() => Teams().SelectMany(t => t);
            public string HostHull() => parkHost ? hostHull : A[0];
            /// <summary>The backfill AIs' hulls, in seat order: without parking team A less the host, then the other teams.</summary>
            public IEnumerable<string> AiHulls() => parkHost ? AllHulls() : AllHulls().Skip(1);
            public IEnumerable<int> AiTeams()
            {
                var teams = Teams();
                var all = teams.SelectMany((t, i) => t.Select(_ => i));
                return parkHost ? all : all.Skip(1);
            }
            public AIDifficulty Difficulty() => Enum.TryParse(difficulty, out AIDifficulty d) ? d : AIDifficulty.Legendary;
        }

        [Serializable]
        class RunState
        {
            public bool active, quit;
            public string outDir = "";
            public int current = -1, offlineWas;
            public List<Match> matches = new();
        }

        static RunState _state;
        /// <summary>Where this run writes (the driver adds its per-match wall series here).</summary>
        public static string OutDir => _state?.outDir;
        static BroodAuditDriver _driver;
        static double _startedAt, _exitAt = -1;

        static BroodAuditLauncher()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            _state = Load();
            if (_state is { active: true, current: >= 0 }) _startedAt = EditorApplication.timeSinceStartup;
        }

        public static void Run()
        {
            string rosters = Arg("-broodRosters", "");
            var seeds = Arg("-broodSeeds", "1").Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToList();
            var state = new RunState
            {
                active = true,
                quit = HasArg("-broodQuit"),
                outDir = Path.GetFullPath(Arg("-broodOut", Path.Combine("Logs", "BroodAudit"))),
                offlineWas = PlayerPrefs.GetInt(OfflineKey, 0),
            };
            string hostOff = Arg("-broodHostHull", "Manta");
            foreach (var roster in rosters.Split(';').Where(r => r.Contains('/')))
            {
                var t = roster.Split('/');
                foreach (int seed in seeds)
                    state.matches.Add(new Match
                    {
                        index = state.matches.Count,
                        seed = seed,
                        teamA = t[0].Trim(),
                        teamB = t[1].Trim(),
                        teamC = t.Length > 2 ? t[2].Trim() : "",
                        intensity = int.Parse(Arg("-broodIntensity", "2"), CultureInfo.InvariantCulture),
                        limitS = float.Parse(Arg("-broodLimit", "300"), CultureInfo.InvariantCulture),
                        timeScale = float.Parse(Arg("-broodTimeScale", "1"), CultureInfo.InvariantCulture),
                        difficulty = Arg("-broodDifficulty", "Legendary"),
                        parkHost = HasArg("-broodPark"),
                        stockCard = HasArg("-broodStock"),
                        waitClient = HasArg("-broodWaitClient"),
                        hostHull = hostOff,
                    });
            }
            if (state.matches.Count == 0)
            {
                Debug.LogError("[BroodAudit] no rosters (-broodRosters \"A,B,C/D,E,F;...\")");
                if (state.quit) EditorApplication.Exit(3);
                return;
            }
            Directory.CreateDirectory(state.outDir);
            _state = state;
            PlayerPrefs.SetInt(OfflineKey, 1);
            PlayerPrefs.Save();
            Save();
            Debug.Log($"[BroodAudit] {state.matches.Count} match(es) planned -> {state.outDir}");
            EditorApplication.delayCall += RunNext;
        }

        static void RunNext()
        {
            if (_state is not { active: true }) return;
            int next = _state.matches.FindIndex(m => m.status == "planned");
            if (next < 0) { Complete(); return; }
            _state.current = next;
            _state.matches[next].status = "running";
            Save();
            var m = _state.matches[next];
            Debug.Log($"[BroodAudit] {next + 1}/{_state.matches.Count}: {m.teamA} vs {m.teamB} vs {m.teamC} seed {m.seed}");
            _startedAt = EditorApplication.timeSinceStartup;
            _exitAt = -1;
            _driver = null;
            EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (_state is not { active: true } || _state.current < 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                var card = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(CardPath);
                _startedAt = EditorApplication.timeSinceStartup;
                if (card == null) { _state.matches[_state.current].fault = "no Brood Rush card"; RequestExit(); return; }
                UnityEngine.Random.InitState(_state.matches[_state.current].seed);
                _driver = new BroodAuditDriver(_state.matches[_state.current], card, _startedAt);
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (_driver != null && _driver.Current != BroodAuditDriver.Phase.Done)
                    _driver.Abort(EditorApplication.timeSinceStartup, "Play ended before the match finished");
                _driver?.Unsubscribe();
                _driver = null;
                Settle(_state.matches[_state.current]);
                _state.current = -1;
                Save();
                EditorApplication.delayCall += RunNext;
            }
        }

        static void Update()
        {
            if (_state is not { active: true } || _state.current < 0) return;
            double now = EditorApplication.timeSinceStartup;
            if (_exitAt >= 0)
            {
                if (EditorApplication.isPlaying && now - _exitAt > ExitTimeout)
                {
                    _state.matches[_state.current].fault += " | leaving Play hung";
                    Settle(_state.matches[_state.current]);
                    Complete();
                }
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                if (_driver == null && now - _startedAt > PlayStartTimeout)
                {
                    _state.matches[_state.current].fault = "Play did not start";
                    Settle(_state.matches[_state.current]);
                    Complete();
                }
                return;
            }
            if (_driver == null) return;
            bool done;
            try { done = _driver.Tick(now); }
            catch (Exception e) { _driver.Abort(now, $"{e.GetType().Name}: {e.Message}"); done = true; }
            if (done) RequestExit();
        }

        static void RequestExit()
        {
            _exitAt = EditorApplication.timeSinceStartup;
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }

        static void Settle(Match m)
        {
            m.status = string.IsNullOrEmpty(m.fault) ? "done" : "fault";
            Debug.Log($"[BroodAudit] {m.teamA} vs {m.teamB} vs {m.teamC} seed {m.seed}: {m.status} {m.outcome} {m.raceS:0} s, broods {m.broods}" +
                      (string.IsNullOrEmpty(m.fault) ? "" : " - " + m.fault) + " | " +
                      string.Join(", ", m.seats.Select(s => $"{s.hull}/{s.domain}{(s.isHost ? "*" : "")} VC {s.vc:0} VD {s.vd:0}")));
            Write();
        }

        static void Complete()
        {
            Write();
            PlayerPrefs.SetInt(OfflineKey, _state.offlineWas);
            PlayerPrefs.Save();
            bool quit = _state.quit;
            int faults = _state.matches.Count(m => m.status != "done");
            Debug.Log($"[BroodAudit] finished: {_state.matches.Count - faults} done, {faults} not -> {_state.outDir}");
            _state = new RunState();
            Save();
            if (!quit) return;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.Exit(faults == 0 ? 0 : 1);
        }

        // ── Output ──
        static readonly string[] Columns =
        {
            "match", "seed", "teamA", "teamB", "teamC", "park_host", "stock_card", "difficulty", "outcome", "race_s", "winner",
            "broods", "seat_name", "hull", "requested_on_seat", "domain", "is_host", "vc", "vd", "vc_vd", "lost", "held",
            "super_shields", "team_broods", "trail_volume", "start_levels", "speed", "nucleus_s", "blocks",
            "volume_created", "stolen", "destroyed", "crystals", "ability_s", "seat_difficulty", "pilots",
            "pilots_at_end", "ability_starts", "abilities", "serpent_walls_max", "serpent_bricks_max", "fault", "notes",
        };

        static void Write()
        {
            if (_state == null || string.IsNullOrEmpty(_state.outDir)) return;
            var csv = new StringBuilder(string.Join(",", Columns)).Append('\n');
            var json = new StringBuilder();
            foreach (var m in _state.matches.Where(m => m.status is "done" or "fault"))
            {
                json.Append(JsonUtility.ToJson(m)).Append('\n');
                foreach (var s in m.seats)
                    csv.Append(string.Join(",", new object[]
                    {
                        m.index, m.seed, Q(m.teamA), Q(m.teamB), Q(m.teamC), m.parkHost, m.stockCard, m.difficulty, m.outcome,
                        F(m.raceS), m.winner, Q(m.broods), Q(s.name), s.hull, s.requestedOnSeat, s.domain, s.isHost,
                        F(s.vc), F(s.vd), F(s.vc + s.vd), F(s.lost), F(s.held), s.superShields, s.teamBroods,
                        F(s.trailVolume), Q(s.startLevels), F(s.speed), F(s.nucleusS), s.blocks, F(s.volumeCreated),
                        F(s.stolen), F(s.destroyed), s.crystals, Q(s.abilityS), s.difficulty, Q(s.pilots),
                        Q(s.pilotsAtEnd), s.abilityStarts, Q(s.abilities), m.serpentWallsMax, m.serpentBricksMax, Q(m.fault), Q(m.notes),
                    }.Select(o => o?.ToString() ?? ""))).Append('\n');
            }
            File.WriteAllText(Path.Combine(_state.outDir, "results.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(_state.outDir, "matches.jsonl"), json.ToString());
        }

        static string F(float v) => float.IsNaN(v) ? "" : v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static RunState Load()
        {
            string json = SessionState.GetString(StateKey, "");
            if (string.IsNullOrEmpty(json)) return new RunState();
            try { return JsonUtility.FromJson<RunState>(json) ?? new RunState(); }
            catch (ArgumentException) { return new RunState(); }
        }

        static void Save() => SessionState.SetString(StateKey, JsonUtility.ToJson(_state ?? new RunState()));
    }
}
