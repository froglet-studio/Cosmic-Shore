using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CosmicShore.Content;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Player
{
    /// <summary>
    /// <c>--race-judge SPEC.json --race-out RESULT.json</c>: times Regatta races flown by every seat's SHIPPED AI, in
    /// the game's own code, headless and as fast as the CPU allows. The editor's <c>RegattaHullBenchmark</c> flow,
    /// step for step: boot to the menu, configure GameDataSO from the Regatta card (host seat idle in a hull the AIs
    /// do not fly, AI seats picked with <see cref="GameDataSO.SetRequestedAIVessels"/>, Legendary), launch through the
    /// normal arcade flow, press the public Ready, timestamp every gate each AI threads from its
    /// <c>RoundStats.SwitchesThreaded</c>, and race the next seed through Play Again.
    ///
    /// One process races ONE seat list. <c>"field": false</c> (default) wants exactly one seat: the hull alone, a
    /// clean finish time. <c>"field": true</c> races every seat at once; the race ends when the first pilot finishes
    /// (the mode's own end), so the others report their gates. The Python wrapper
    /// (<c>Tools/Compute/evaluators/regatta_judge.py</c>) runs one process per hull in parallel.
    ///
    /// SPEC: <c>{"genome": {...}|null, "genome_file": path, "seats": [{"hull": "Urchin", "ai": "shipped"}], "field": false,
    /// "seeds": [1,2,3], "limit_s": 400, "difficulty": "Legendary"}</c>. The genome sets intensity (default 1), the course
    /// seed (default the shipped one) and "features" (RegattaCourseFeatures JSON; absent = the arena's own, {} = none).
    /// When the game has <c>RegattaCourseGenome</c> (the expressive genome), the whole genome is handed to its Override.
    /// </summary>
    internal sealed class RaceJudge
    {
        const string CardPath = "Assets/_SO_Assets/Games/ArcadeGameRegatta.asset";
        const string CellConfigPath = "Assets/_SO_Assets/Cell Configs/Regatta Cell/Regatta Cell Config {0}.asset";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        const double TickSeconds = 1.0 / 60.0;

        readonly string _outPath;
        readonly JsonObject _spec;
        readonly List<VesselClassType> _hulls = new();
        readonly List<int> _seeds = new();
        readonly bool _field;
        readonly bool _trace;
        readonly float _limit;
        readonly AIDifficulty _difficulty = AIDifficulty.Legendary;
        readonly JsonArray _races = new();
        readonly List<string> _notes = new();
        ContentRuntime _runtime;
        IEnumerator _stepper;
        double _waitUntil;
        int _frame;
        bool _menuReached;
        readonly System.Diagnostics.Stopwatch _wall = System.Diagnostics.Stopwatch.StartNew();

        public bool Done { get; private set; }
        public int ExitCode { get; private set; }

        public RaceJudge(string specPath, string outPath)
        {
            _outPath = outPath ?? Path.ChangeExtension(specPath, ".result.json");
            _spec = JsonNode.Parse(File.ReadAllText(specPath))?.AsObject() ?? throw new InvalidOperationException("empty spec");
            foreach (var s in _spec["seats"]?.AsArray() ?? new JsonArray())
            {
                string hull = s?["hull"]?.GetValue<string>() ?? throw new InvalidOperationException("a seat without a hull");
                if (!Enum.TryParse(hull, out VesselClassType v)) throw new InvalidOperationException($"not a hull: {hull}");
                string ai = s["ai"]?.GetValue<string>() ?? "shipped";
                if (ai != "shipped") throw new InvalidOperationException($"seat {hull}: only ai \"shipped\" is supported, not '{ai}'");
                _hulls.Add(v);
            }
            if (_hulls.Count == 0) throw new InvalidOperationException("the spec has no seats");
            _field = _spec["field"]?.GetValue<bool>() ?? false;
            if (!_field && _hulls.Count != 1)
                throw new InvalidOperationException("an alone race seats one hull per process; set \"field\": true or run one process per hull");
            foreach (var n in _spec["seeds"]?.AsArray() ?? new JsonArray { 1 }) _seeds.Add(n!.GetValue<int>());
            _trace = _spec["trace"]?.GetValue<bool>() ?? false;
            _limit = _spec["limit_s"]?.GetValue<float>() ?? 400f;
            if (_spec["difficulty"]?.GetValue<string>() is { } d && Enum.TryParse(d, out AIDifficulty diff)) _difficulty = diff;
            if (_spec["genome"] == null && _spec["genome_file"]?.GetValue<string>() is { } gf)
                _spec["genome"] = JsonNode.Parse(File.ReadAllText(gf));
        }

        public void Install(ContentRuntime runtime)
        {
            _runtime = runtime;
            _stepper = Run();
        }

        double Now => _frame * TickSeconds;

        /// <summary>Once per frame, after the game ticked.</summary>
        public void Tick(int frame)
        {
            _frame = frame;
            if (Done || _stepper == null) return;
            if (!_menuReached) UnattendedBoot.Tick();
            if (Now < _waitUntil) return;
            try
            {
                if (!_stepper.MoveNext()) { Finish(0); return; }
                _waitUntil = _stepper.Current is float s ? Now + s : 0;
            }
            catch (Exception e)
            {
                _notes.Add("judge failed: " + e.Message);
                Console.WriteLine("[race-judge] " + e);
                Finish(1);
            }
        }

        IEnumerator Run()
        {
            var gameData = TrainingHost.FindFirstAsset(_runtime, "GameDataSO") as GameDataSO
                ?? throw new InvalidOperationException("GameDataSO not found");
            var card = TrainingHost.LoadAsset(_runtime, CardPath) as SO_ArcadeGame
                ?? throw new InvalidOperationException(CardPath + " not found");

            double deadline = Now + 300;
            while (Now < deadline && !(IsMenu() && gameData.LocalPlayer?.Vessel != null)) yield return 0.25f;
            if (!IsMenu()) throw new InvalidOperationException($"never reached the menu (stuck in '{SceneManager.GetActiveScene()?.name}')");
            _menuReached = true;
            Console.WriteLine($"[race-judge] menu at {Now:0.0} s sim, {_wall.Elapsed.TotalSeconds:0.0} s wall");
            yield return 1.5f;

            var genome = _spec["genome"] as JsonObject;
            int intensity = Math.Clamp(genome?["intensity"]?.GetValue<int>() ?? 1, 1, 4);
            ApplyCourse(genome, intensity);

            var all = new[] { "Squirrel", "Manta", "Rhino", "Urchin", "Dolphin", "Stoat", "Scarab", "Serpent", "Sparrow" }
                .Select(Enum.Parse<VesselClassType>);
            var hostHull = all.First(h => !_hulls.Contains(h));
            gameData.SyncFromArcadeGame(card);
            gameData.IsTraining = false;
            if (gameData.selectedVesselClass != null) gameData.selectedVesselClass.Value = hostHull;
            if (gameData.SelectedIntensity != null) gameData.SelectedIntensity.Value = intensity;
            gameData.ConfigurePlayerCounts(1 + _hulls.Count, 1);
            gameData.RequestedDomainCount = 2;
            gameData.RequestedAIDifficulty = AIDifficultyRules.Resolve(_difficulty);
            gameData.SetRequestedAIVessels(_hulls);
            CosmicShore.Engine.Random.InitState(_seeds[0]);
            gameData.InvokeGameLaunch();

            for (int r = 0; r < _seeds.Count; r++)
            {
                int seed = _seeds[r];
                double raceWall = _wall.Elapsed.TotalSeconds;
                GateRaceController controller = null;
                List<IPlayer> ais = null;
                deadline = Now + 180;
                while (Now < deadline)
                {
                    if (SceneManager.GetActiveScene()?.name == gameData.SceneName)
                    {
                        controller = CosmicShore.Engine.Object.FindAnyObjectByType<GateRaceController>();
                        ais = gameData.Players.Where(p => p != null && p.IsInitializedAsAI && p.Vessel != null).ToList();
                        if (controller != null && ais.Count >= _hulls.Count && controller.AuthoritativeGateCount > 0) break;
                    }
                    yield return 0.25f;
                }
                var race = new JsonObject { ["seed"] = seed, ["mode"] = _field ? "field" : "alone", ["intensity"] = intensity };
                _races.Add(race);
                if (controller == null || ais == null || ais.Count < _hulls.Count)
                {
                    race["outcome"] = "scene-not-ready";
                    race["seats"] = new JsonArray(_hulls.Select(h => (JsonNode)Dnf(h.ToString(), "scene-not-ready")).ToArray());
                    yield break;
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
                    race["outcome"] = "not-started";
                    race["seats"] = new JsonArray(_hulls.Select(h => (JsonNode)Dnf(h.ToString(), "not-started")).ToArray());
                    yield break;
                }

                // The arena's SegmentSpawner re-seeds UnityEngine.Random with the course seed while it lays the course,
                // so a seed set before launch is spent by then. Seeding again as the turn starts makes the race's own
                // draws (AI risk rolls, mistakes, spawns) follow this race's seed.
                CosmicShore.Engine.Random.InitState(unchecked(seed * 7919 + 17));
                int total = controller.AuthoritativeGateCount;
                race["gates_total"] = total;
                race["laps_total"] = total / RegattaCourse.RingsPerLap;
                race["course_seed"] = RegattaCourseSource.TryResolveLiveRails(out var rails, out _) ? rails.CourseSeed : controller.CourseSeed;
                race["host_hull"] = hostHull.ToString();
                float start = Time.time;
                var gateTimes = ais.Select(_ => new List<float>()).ToArray();
                var traces = ais.Select(_ => new List<float>()).ToArray();
                float nextSample = 0f;
                string outcome;
                while (true)
                {
                    bool someoneFinished = false;
                    for (int k = 0; k < ais.Count; k++)
                    {
                        int now = ais[k].RoundStats?.SwitchesThreaded ?? 0;
                        while (gateTimes[k].Count < now) gateTimes[k].Add(Time.time - start);
                        if (now >= total) someoneFinished = true;
                    }
                    if (_trace && Time.time - start >= nextSample)
                    {
                        // One row per seat every 0.25 s: t, x, y, z, speed.
                        nextSample = Time.time - start + 0.25f;
                        for (int k = 0; k < ais.Count; k++)
                        {
                            var v = ais[k].Vessel;
                            if (v?.Transform == null) continue;
                            var pos = v.Transform.position;
                            traces[k].AddRange(new[] { Time.time - start, pos.x, pos.y, pos.z, v.VesselStatus?.Speed ?? 0f });
                        }
                    }
                    if (someoneFinished) { outcome = "finished"; break; }
                    if (!gameData.IsTurnRunning) { outcome = "ended"; break; }
                    if (Time.time - start > _limit) { outcome = "timeout"; break; }
                    yield return null;
                }
                race["outcome"] = outcome;
                race["race_s"] = Math.Round(Time.time - start, 3);
                race["wall_s"] = Math.Round(_wall.Elapsed.TotalSeconds - raceWall, 2);
                var seats = new JsonArray();
                for (int k = 0; k < ais.Count; k++)
                {
                    var p = ais[k];
                    var g = gateTimes[k];
                    string hull = (p.Vessel?.VesselStatus?.VesselType ?? VesselClassType.Any).ToString();
                    bool done = g.Count >= total;
                    var lapS = new JsonArray();
                    float prev = 0;
                    for (int lap = 1; lap * RegattaCourse.RingsPerLap <= g.Count; lap++)
                    {
                        float t = g[lap * RegattaCourse.RingsPerLap - 1];
                        lapS.Add(Math.Round(t - prev, 3));
                        prev = t;
                    }
                    var stats = p.RoundStats;
                    seats.Add(new JsonObject
                    {
                        ["hull"] = hull,
                        ["domain"] = p.Domain.ToString(),
                        ["finish_s"] = done ? Math.Round(g[total - 1], 3) : null,
                        ["gates"] = g.Count,
                        ["laps"] = g.Count / RegattaCourse.RingsPerLap,
                        ["lap_s"] = lapS,
                        ["gate_s"] = new JsonArray(g.Select(t => (JsonNode)Math.Round(t, 3)).ToArray()),
                        ["prism_hits"] = stats == null ? null : stats.FriendlyPrismsDestroyed + stats.HostilePrismsDestroyed,
                        ["dnf_reason"] = done ? null : outcome == "finished" ? "race-ended" : outcome,
                    });
                    if (_trace)
                    {
                        seats[seats.Count - 1]!["trace_columns"] = "t,x,y,z,speed";
                        seats[seats.Count - 1]!["trace"] = new JsonArray(traces[k].Select(t => (JsonNode)Math.Round(t, 2)).ToArray());
                    }
                }
                race["seats"] = seats;
                Console.WriteLine($"[race-judge] seed {seed}: {outcome} {Time.time - start:0.0} s sim in {_wall.Elapsed.TotalSeconds - raceWall:0.0} s wall - " +
                                  string.Join(", ", seats.Select(s => $"{s!["hull"]} {s["gates"]}/{total} {s["finish_s"]}")));
                Flush(running: true);

                if (r + 1 >= _seeds.Count) break;
                deadline = Now + 30;
                while (gameData.IsTurnRunning && Now < deadline) yield return 0.25f;
                yield return 2.5f;
                controller = CosmicShore.Engine.Object.FindAnyObjectByType<GateRaceController>();
                CosmicShore.Engine.Random.InitState(_seeds[r + 1]);
                if (controller != null) controller.RequestReplay();
                deadline = Now + 120;
                while (controller != null && Now < deadline) yield return 0.25f;
            }
        }

        static JsonObject Dnf(string hull, string why) => new()
        {
            ["hull"] = hull, ["finish_s"] = null, ["gates"] = 0, ["laps"] = 0, ["lap_s"] = new JsonArray(),
            ["prism_hits"] = null, ["dnf_reason"] = why,
        };

        /// <summary>Lay the genome's course: the expressive genome's own Override when the game has it, otherwise the
        /// pool entry for the intensity, its seed and its features.</summary>
        void ApplyCourse(JsonObject genome, int intensity)
        {
            CourseVariantOverride.Forced = intensity - 1;
            var genomeType = typeof(RegattaCourseFeatures).Assembly.GetType("CosmicShore.Gameplay.RegattaCourseGenome");
            if (genome != null && genomeType != null)
            {
                var parse = genomeType.GetMethod("FromJson", Any, null, new[] { typeof(string) }, null);
                var slot = (MemberInfo)genomeType.GetField("Override", Any) ?? genomeType.GetProperty("Override", Any);
                if (parse != null && slot != null)
                {
                    object g = parse.Invoke(null, new object[] { genome.ToJsonString() });
                    if (slot is FieldInfo f) f.SetValue(null, g); else ((PropertyInfo)slot).SetValue(null, g);
                    _notes.Add("course: RegattaCourseGenome.Override");
                    return;
                }
            }

            var config = TrainingHost.LoadAsset(_runtime, string.Format(CellConfigPath, intensity)) as CellConfigDataSO;
            var rails = config ? config.EnvironmentPrefab as SpawnableRegattaRails : null;
            if (rails == null) throw new InvalidOperationException($"Regatta Cell Config {intensity} has no SpawnableRegattaRails");
            if (genome?["seed"] is JsonNode seedNode)
                typeof(SpawnableRegattaRails).GetField("courseSeed", Any)!.SetValue(rails, seedNode.GetValue<int>());
            if (genome?["features"] is JsonObject features)
            {
                var parsed = new RegattaCourseFeatures();
                JsonUtility.FromJsonOverwrite(features.ToJsonString(), parsed);
                RegattaCourseFeatures.Override = parsed;
            }
            if (genome?["circuit"] is JsonObject { Count: > 0 })
                _notes.Add("genome.circuit ignored: this build has no RegattaCourseGenome; the circuit is RegattaCourse.ForIntensity");
            if (genome?["placements"] is JsonArray { Count: > 0 })
                _notes.Add("genome.placements ignored: this build has no RegattaCourseGenome");
            _notes.Add($"course: pool entry {intensity - 1}, seed {rails.CourseSeed}, features {(RegattaCourseFeatures.Override == null ? "arena's own" : "genome")}");
        }

        static bool IsMenu() =>
            (SceneManager.GetActiveScene()?.name ?? "").IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        void Finish(int code)
        {
            if (Done) return;
            Done = true;
            ExitCode = code;
            Flush(running: false);
            Console.WriteLine($"[race-judge] {_races.Count} race(s) in {_wall.Elapsed.TotalSeconds:0.0} s wall -> {_outPath}");
        }

        void Flush(bool running)
        {
            var result = new JsonObject
            {
                ["races"] = JsonNode.Parse(_races.ToJsonString()),
                ["wall_s"] = Math.Round(_wall.Elapsed.TotalSeconds, 2),
                ["engine_commit"] = Environment.GetEnvironmentVariable("COSMIC_SHORE_COMMIT") ?? "",
                ["backend"] = "amoebius",
                ["complete"] = !running,
                ["notes"] = new JsonArray(_notes.Select(n => (JsonNode)n).ToArray()),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_outPath))!);
            File.WriteAllText(_outPath, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
