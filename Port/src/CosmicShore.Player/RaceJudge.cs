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
    ///
    /// <c>"mode": "SkimRace"</c> races the Squirrel's Skim Race the same way: no gates, so a seat's progress is its
    /// crystals and it finishes at the mode's crystal count; its track is forced to the race seed like any other game's.
    /// </summary>
    internal sealed class RaceJudge
    {
        const string CardPathFormat = "Assets/_SO_Assets/Games/ArcadeGame{0}.asset";
        /// <summary>Regatta's course pool: entries 0-3 are the reference courses Ref1-Ref4 (author_regatta_assets.py), which
        /// a race at intensity 1-4 lays when its genome is not handed to RegattaCourseGenome.Override.</summary>
        const string CoursePoolPath = "Assets/_SO_Assets/Cell Configs/Regatta Cell/Regatta Course Pool.asset";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        const double TickSeconds = 1.0 / 60.0;
        /// <summary>The sim time every judge launches its game at, whatever frame the boot reached the menu on, so a run
        /// repeats exactly (the menu came at 0.2-1.3 s in every measured boot).</summary>
        const double LaunchAtSeconds = 5.0;

        readonly string _outPath;
        readonly JsonObject _spec;
        readonly List<VesselClassType> _hulls = new();
        /// <summary>Each seat's team (<c>"domain": "Jade"</c>), placed through <see cref="GameDataSO.SetRequestedAIDomains"/>;
        /// Blue = unset, which the game balances as usual.</summary>
        readonly List<Domains> _domains = new();
        readonly List<int> _seeds = new();
        readonly bool _field;
        readonly bool _trace;
        readonly string _mode;
        readonly Collider[] _near = new Collider[256];
        bool IsRegatta => _mode == "Regatta";
        /// <summary>Skim Race: no gates; a seat's progress is its crystals (RoundStats.CrystalsCollected) and it finishes at
        /// the mode's crystal count (GameDataSO.CrystalTargetCount, EndConditionOverrides). "gates" and "gate_s" then
        /// count crystals; "crystals" and "crystals_total" say so.</summary>
        bool IsSkim => _mode == "SkimRace";
        /// <summary>Broadside: a brawl, not a race. Every seat fights in one match (always a field); a seat's progress is
        /// its <c>RoundStats.CombatPoints</c> and the match ends at the mode's own point target or at <c>limit_s</c>. A
        /// timed match (no domain reaches the target) is what a balance judge wants: <c>"points_per_pilot"</c> raises the
        /// target for the run. The idle host is parked far outside the arena so nobody farms a pilot that never moves.</summary>
        bool IsBroadside => _mode == "Broadside";
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
                string dom = s["domain"]?.GetValue<string>();
                _domains.Add(dom == null ? Domains.Blue
                    : Enum.TryParse(dom, out Domains dv) ? dv : throw new InvalidOperationException($"seat {hull}: not a domain: {dom}"));
            }
            if (_hulls.Count == 0) throw new InvalidOperationException("the spec has no seats");
            _field = _spec["field"]?.GetValue<bool>() ?? false;
            if ((_spec["mode"]?.GetValue<string>() ?? "Regatta") == "Broadside") _field = true;
            if (!_field && _hulls.Count != 1)
                throw new InvalidOperationException("an alone race seats one hull per process; set \"field\": true or run one process per hull");
            foreach (var n in _spec["seeds"]?.AsArray() ?? new JsonArray { 1 }) _seeds.Add(n!.GetValue<int>());
            _mode = _spec["mode"]?.GetValue<string>() ?? "Regatta";
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

        /// <summary>The idle host's hull: an original hull no seat flies when there is one, otherwise any seated hull.
        /// Seats are read from the AI players, never by hull, so the host sharing a seat's hull changes no result.</summary>
        internal static VesselClassType HostHull(IReadOnlyList<VesselClassType> seated)
        {
            var originals = new[] { "Squirrel", "Manta", "Rhino", "Urchin", "Dolphin", "Stoat", "Scarab", "Serpent", "Sparrow" }
                .Select(Enum.Parse<VesselClassType>);
            foreach (var h in originals)
                if (!seated.Contains(h)) return h;
            return seated.Count > 0 ? seated[0] : throw new InvalidOperationException("the spec has no seats");
        }

        /// <summary>
        /// Seat any requested hull by name, on the card or not: a hull the card does not list is added to the loaded card
        /// in memory for this run only (nothing is written), from its class asset. Without it the game swaps an off-card
        /// hull for one the card lists (ClampVesselToGame), and the judge would time that hull under the requested name.
        /// Regatta keeps hulls that finish too slowly to be an ally AI's draw off its card (author_regatta_assets.py
        /// JUDGE_ONLY_HULLS); this is how step 5 and their AI training still race them.
        /// </summary>
        void SeatOffCardHulls(SO_ArcadeGame card)
        {
            card.Vessels ??= new List<SO_Vessel>();
            foreach (var hull in _hulls)
            {
                // "Random" asks for the game's own draw (the card's roster, or a pinned opponent hull): nothing to seat.
                if (hull == VesselClassType.Random || card.Vessels.Any(v => v != null && v.Class == hull)) continue;
                string path = $"Assets/_SO_Assets/Classes/SO_Class_{hull}.asset";
                var vessel = TrainingHost.LoadAsset(_runtime, path) as SO_Vessel
                    ?? throw new InvalidOperationException($"{hull} is not on the card and {path} did not load");
                card.Vessels.Add(vessel);
                _notes.Add($"seated off the card: {hull}");
            }
        }

        IEnumerator Run()
        {
            var gameData = TrainingHost.FindFirstAsset(_runtime, "GameDataSO") as GameDataSO
                ?? throw new InvalidOperationException("GameDataSO not found");
            string cardPath = _spec["card"]?.GetValue<string>() ?? string.Format(CardPathFormat, _mode);
            var card = TrainingHost.LoadAsset(_runtime, cardPath) as SO_ArcadeGame
                ?? throw new InvalidOperationException(cardPath + " not found");
            ApplyOverrides();

            double deadline = Now + 300;
            while (Now < deadline && !(IsMenu() && gameData.LocalPlayer?.Vessel != null)) yield return 0.25f;
            if (!IsMenu()) throw new InvalidOperationException($"never reached the menu (stuck in '{SceneManager.GetActiveScene()?.name}')");
            _menuReached = true;
            Console.WriteLine($"[race-judge] menu at {Now:0.0} s sim, {_wall.Elapsed.TotalSeconds:0.0} s wall");
            // Launch on a fixed frame. The boot reaches the menu on a frame that depends on the wall clock (frame 14-49
            // measured under four concurrent judges), and a launch 1.5 s after it then started the race at one of
            // several absolute times, each a different race: the same build and seeds gave Dolphin 47.3, 59.2, 59.7 or
            // 79.9 s on one course, decided only by that frame. Every launch on the same frame gave the same times.
            double launchAt = Math.Max(LaunchAtSeconds, Now + 1.5);
            if (launchAt > LaunchAtSeconds)
                _notes.Add($"menu reached late ({Now:0.0} s sim): launched at {launchAt:0.00} s, not the fixed {LaunchAtSeconds} s, so this run may not repeat");
            while (Now < launchAt) yield return null;

            var genome = _spec["genome"] as JsonObject;
            int intensity = Math.Clamp(_spec["intensity"]?.GetValue<int>() ?? genome?["intensity"]?.GetValue<int>() ?? 1, 1, 4);
            if (IsRegatta) ApplyCourse(genome, intensity);
            else if (IsBroadside) ApplyBroadsideTarget();
            else ApplyTrainingOverlay(genome);

            SeatOffCardHulls(card);
            var hostHull = HostHull(_hulls);
            // Broadside's card lists few hulls: the idle host's hull goes on it too, or the spawn clamps the host INTO a
            // card hull and the seat that asked for that hull is re-drawn as another one (arena seating, one pilot a hull).
            if (IsBroadside && !card.Vessels.Any(v => v != null && v.Class == hostHull)
                && TrainingHost.LoadAsset(_runtime, $"Assets/_SO_Assets/Classes/SO_Class_{hostHull}.asset") is SO_Vessel hostClass)
                card.Vessels.Add(hostClass);
            gameData.SyncFromArcadeGame(card);
            ApplyStart(gameData);
            ApplyCrystalGenes();
            gameData.IsTraining = false;
            if (gameData.selectedVesselClass != null) gameData.selectedVesselClass.Value = hostHull;
            // The host's player wrote its hull from the menu's selection before this run set one, and the spawn reads that
            // variable, not the selection (VoyageLaunch does the same). Left alone, the host keeps the profile's default
            // hull (the Squirrel), and under arena seating a seat that asks for that hull is re-drawn as another one.
            if (gameData.LocalPlayer is CosmicShore.Gameplay.Player host && host.NetDefaultVesselType.Value != hostHull)
                host.NetDefaultVesselType.Value = hostHull;
            if (gameData.SelectedIntensity != null) gameData.SelectedIntensity.Value = intensity;
            gameData.ConfigurePlayerCounts(1 + _hulls.Count, 1);
            gameData.RequestedDomainCount = Math.Clamp(_spec["domains"]?.GetValue<int>() ?? 2, 2, 3);
            gameData.RequestedAIDifficulty = AIDifficultyRules.Resolve(_difficulty);
            gameData.SetRequestedAIVessels(_hulls);
            gameData.SetRequestedAIDomains(_domains);
            CosmicShore.Engine.Random.InitState(_seeds[0]);
            ForceCourse(0);
            gameData.InvokeGameLaunch();

            for (int r = 0; r < _seeds.Count; r++)
            {
                int seed = _seeds[r];
                double raceWall = _wall.Elapsed.TotalSeconds;
                MiniGameControllerBase controller = null;
                List<IPlayer> ais = null;
                deadline = Now + 180;
                while (Now < deadline)
                {
                    if (SceneManager.GetActiveScene()?.name == gameData.SceneName)
                    {
                        controller = FindController();
                        ais = gameData.Players.Where(p => p != null && p.IsInitializedAsAI && p.Vessel != null).ToList();
                        if (controller != null && ais.Count >= _hulls.Count && CourseReady(controller)) break;
                    }
                    yield return 0.25f;
                }
                var race = new JsonObject { ["seed"] = seed, ["mode"] = _field ? "field" : "alone", ["game"] = _mode, ["intensity"] = intensity };
                _races.Add(race);
                if (controller == null || ais == null || ais.Count < _hulls.Count)
                {
                    race["outcome"] = "scene-not-ready";
                    race["seats"] = new JsonArray(_hulls.Select(h => (JsonNode)Dnf(h.ToString(), "scene-not-ready")).ToArray());
                    yield break;
                }
                yield return 1f;
                ApplyControllerOverrides(controller);
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
                int total = controller is GateRaceController gates ? gates.AuthoritativeGateCount : 0;
                if (IsBroadside)
                {
                    double targetBy = Now + 5;
                    while (gameData.CombatPointTargetCount <= 0 && Now < targetBy) yield return null;
                    total = gameData.CombatPointTargetCount;
                    race["points_total"] = total;
                }
                if (IsSkim)
                {
                    // The turn monitor publishes the crystal count as the turn starts.
                    double targetBy = Now + 5;
                    while (gameData.CrystalTargetCount <= 0 && Now < targetBy) yield return null;
                    total = gameData.CrystalTargetCount;
                    race["crystals_total"] = total;
                }
                if (total <= 0)
                {
                    race["outcome"] = "no-target";
                    race["seats"] = new JsonArray(_hulls.Select(h => (JsonNode)Dnf(h.ToString(), "no-target")).ToArray());
                    yield break;
                }
                race["gates_total"] = total;
                if (!IsRegatta && TrainingCourseOverlay.Override != null)
                    race["training_prisms"] = TrainingCourseOverlay.LastReports.Sum(r => r.Laid);
                if (IsRegatta) race["laps_total"] = total / RegattaCourse.RingsPerLap;
                race["course_seed"] = IsRegatta && RegattaCourseSource.TryResolveLiveRails(out var rails, out _) ? rails.CourseSeed
                    : controller is GateRaceController gc ? gc.CourseSeed : controller is SkimRaceController sc ? sc.TrackSeed : 0;
                race["host_hull"] = hostHull.ToString();
                // Each racer's element levels as the turn starts (what "start" and the card actually gave it).
                var startLevels = ais.Select(p => p.Vessel?.VesselStatus?.ResourceSystem is { } rs
                    ? $"M{rs.GetLevel(Element.Mass)} C{rs.GetLevel(Element.Charge)} S{rs.GetLevel(Element.Space)} T{rs.GetLevel(Element.Time)}"
                    : "?").ToArray();
                float start = Time.time;
                var gateTimes = ais.Select(_ => new List<float>()).ToArray();
                var traces = ais.Select(_ => new List<float>()).ToArray();
                var nears = ais.Select(_ => new List<string>()).ToArray();
                float nextSample = 0f;
                string outcome;
                var noseFollow = _spec["ai_nose_follow"] as JsonObject;
                var hostVessel = IsBroadside ? gameData.LocalPlayer?.Vessel : null;
                var pointsAt = ais.Select(_ => new List<float>()).ToArray();
                float nextPointSample = 0f;
                while (true)
                {
                    bool someoneFinished = false;
                    if (IsBroadside)
                    {
                        // Park the idle host far outside the Boneyard every frame: a pilot that never moves is free points.
                        if (hostVessel?.Transform != null) hostVessel.Transform.position = HostParking;
                        if (Time.time - start >= nextPointSample)
                        {
                            nextPointSample = Time.time - start + 5f;
                            for (int k = 0; k < ais.Count; k++) pointsAt[k].Add(ais[k].RoundStats?.CombatPoints ?? 0);
                        }
                    }
                    // "ai_nose_follow": {hull: rate/s} - a search's AI nose follow rate for hulls whose mode has no knob
                    // (Switchback and Redline set their own as the AI is armed; this wins over both, every frame).
                    if (noseFollow != null)
                        foreach (var p in ais)
                        {
                            var st = p.Vessel?.VesselStatus;
                            if (st?.VesselTransformer is { } vt && noseFollow[st.VesselType.ToString()] is JsonNode n)
                                vt.AINoseFollowPerSecond = n.GetValue<float>();
                        }
                    for (int k = 0; k < ais.Count; k++)
                    {
                        if (IsBroadside) continue;
                        int now = IsSkim ? ais[k].RoundStats?.CrystalsCollected ?? 0 : ais[k].RoundStats?.SwitchesThreaded ?? 0;
                        while (gateTimes[k].Count < now) gateTimes[k].Add(Time.time - start);
                        if (now >= total) someoneFinished = true;
                    }
                    if (_trace && Time.time - start >= nextSample)
                    {
                        // One row per seat every 0.25 s: t, x, y, z, speed, then the AI's objective phase, ring, objective point, steering target, nose,
                        // and flags (1 emergency steer, 2 orbit break, 4 drifting, 8 boosting, 16 slowed, 32 translation restricted).
                        nextSample = Time.time - start + 0.25f;
                        for (int k = 0; k < ais.Count; k++)
                        {
                            var v = ais[k].Vessel;
                            if (v?.Transform == null) continue;
                            var pos = v.Transform.position;
                            traces[k].AddRange(new[] { Time.time - start, pos.x, pos.y, pos.z, v.VesselStatus?.Speed ?? 0f });
                            // What the mode's steering hook says the pilot is after, and where it steers.
                            AIPilot pilot = null;
                            if (v.VesselStatus is Component c && c) c.TryGetComponent(out pilot);
                            var o = pilot ? pilot.Objective : default;
                            var tgt = pilot ? pilot.TargetPosition : Vector3.zero;
                            var fwd = v.Transform.forward;
                            var st = v.VesselStatus;
                            int flags = (pilot && pilot.EmergencyActive ? 1 : 0) | (pilot && pilot.IsBreakingOrbit ? 2 : 0)
                                      | (st != null && st.IsDrifting ? 4 : 0) | (st != null && st.IsBoosting ? 8 : 0)
                                      | (st != null && st.IsSlowed ? 16 : 0) | (st != null && st.IsTranslationRestricted ? 32 : 0);
                            traces[k].AddRange(new[] { (float)o.Phase, o.Ring, o.Objective.x, o.Objective.y, o.Objective.z, tgt.x, tgt.y, tgt.z,
                                                       fwd.x, fwd.y, fwd.z, flags });
                            // Where the pilot COMMANDS the nose (the nose lead judges from here) and how fast the nose follows.
                            var vt = st?.VesselTransformer;
                            var cmd = vt ? vt.CommandedRotation * Vector3.forward : Vector3.zero;
                            traces[k].AddRange(new[] { cmd.x, cmd.y, cmd.z, vt ? vt.NoseFollowRate : 0f });
                            // Slow (under 50 u/s): what is within 30 u, by the root object's name, for a stall's cause.
                            if ((v.VesselStatus?.Speed ?? 0f) < 50f)
                            {
                                int hits = Physics.OverlapSphereNonAlloc(pos, 30f, _near);
                                var names = new SortedDictionary<string, int>();
                                for (int h = 0; h < hits; h++)
                                {
                                    var col = _near[h];
                                    if (!col) continue;
                                    var tr = col.transform;
                                    string n = tr.parent ? tr.parent.name + "/" + tr.name : tr.name;
                                    n = System.Text.RegularExpressions.Regex.Replace(n, @"\(Clone\)|\s*\(\d+\)|_\d+$", "");
                                    names[n] = names.TryGetValue(n, out int c0) ? c0 + 1 : 1;
                                }
                                nears[k].Add($"{Time.time - start:0.0}: " + string.Join(", ", names.Select(kv => $"{kv.Key} x{kv.Value}")));
                            }
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
                    for (int lap = 1; IsRegatta && lap * RegattaCourse.RingsPerLap <= g.Count; lap++)
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
                        ["laps"] = IsRegatta ? g.Count / RegattaCourse.RingsPerLap : null,
                        ["lap_s"] = lapS,
                        ["gate_s"] = new JsonArray(g.Select(t => (JsonNode)Math.Round(t, 3)).ToArray()),
                        ["prism_hits"] = stats == null ? null : stats.FriendlyPrismsDestroyed + stats.HostilePrismsDestroyed,
                        ["crystals"] = stats?.CrystalsCollected,
                        ["start_levels"] = startLevels[k],
                        ["dnf_reason"] = done ? null : outcome == "finished" ? "race-ended" : outcome,
                    });
                    if (IsBroadside)
                    {
                        var seat = seats[seats.Count - 1]!.AsObject();
                        foreach (var key in new[] { "finish_s", "gates", "laps", "lap_s", "gate_s", "dnf_reason" }) seat.Remove(key);
                        seat["points"] = stats?.CombatPoints ?? 0;
                        seat["bullet_hits"] = stats?.BulletHitsLanded ?? 0;
                        seat["strike_hits"] = stats?.StrikeHitsLanded ?? 0;
                        seat["debuff_hits"] = stats?.DebuffHitsLanded ?? 0;
                        seat["missile_hits"] = stats?.MissileHitsLanded ?? 0;
                        seat["points_every_5s"] = new JsonArray(pointsAt[k].Select(t => (JsonNode)t).ToArray());
                    }
                    if (_trace)
                    {
                        seats[seats.Count - 1]!["trace_columns"] = "t,x,y,z,speed,phase,ring,ox,oy,oz,tx,ty,tz,fx,fy,fz,flags,cx,cy,cz,follow";
                        seats[seats.Count - 1]!["trace"] = new JsonArray(traces[k].Select(t => (JsonNode)Math.Round(t, 2)).ToArray());
                        seats[seats.Count - 1]!["near_when_slow"] = new JsonArray(nears[k].Select(t => (JsonNode)t).ToArray());
                    }
                }
                race["seats"] = seats;
                Console.WriteLine($"[race-judge] seed {seed}: {outcome} {Time.time - start:0.0} s sim in {_wall.Elapsed.TotalSeconds - raceWall:0.0} s wall - " +
                                  (IsBroadside
                                      ? string.Join(", ", seats.Select(s => $"{s!["hull"]}/{s["domain"]} {s["points"]}"))
                                      : string.Join(", ", seats.Select(s => $"{s!["hull"]} {s["gates"]}/{total} {s["finish_s"]}"))));
                Flush(running: true);

                if (r + 1 >= _seeds.Count) break;
                deadline = Now + 30;
                while (gameData.IsTurnRunning && Now < deadline) yield return 0.25f;
                yield return 2.5f;
                controller = FindController();
                CosmicShore.Engine.Random.InitState(_seeds[r + 1]);
                ForceCourse(r + 1);
                if (controller != null) controller.RequestReplay();
                deadline = Now + 120;
                while (controller != null && Now < deadline) yield return 0.25f;
            }
        }

        /// <summary>
        /// <c>"overrides": [{"asset": "Assets/.../X.asset", "fields": {"name": 1.5, "nested.name": 2}}]</c>: set fields of a
        /// loaded asset IN MEMORY before the launch (a ScriptableObject is one instance per guid, the one the game reads
        /// through Resources.Load or a reference). How a search tries an AI's numbers without writing a file, so many
        /// judges can run side by side on one checkout. Nothing is saved.
        /// </summary>
        void ApplyOverrides()
        {
            foreach (var o in _spec["overrides"]?.AsArray() ?? new JsonArray())
            {
                string path = o?["asset"]?.GetValue<string>() ?? throw new InvalidOperationException("an override without an asset");
                // A prefab's component by its fileID ({"asset": "...prefab", "component": 975128113266807093}); else an SO.
                object asset = (o["component"] is JsonNode c
                                   ? TrainingHost.LoadPrefabComponent(_runtime, path, c.GetValue<long>())
                                   : TrainingHost.LoadAsset(_runtime, path))
                               ?? throw new InvalidOperationException(path + " not found");
                SetFields(asset, o["fields"]?.AsObject(), path);
                _notes.Add("override: " + path);
            }
        }

        /// <summary>
        /// <c>"start"</c>: the element levels each hull starts at, published the way the card's
        /// <c>SO_ArcadeGame.StartingElements</c> is (<c>GameDataSO.PublishStartingElements</c>, applied by every vessel's
        /// <c>ApplyStartingElements</c>), with no card or vessel asset edited.
        /// <list type="bullet">
        /// <item><c>"card"</c> (default): the card's own table, as a player gets it.</item>
        /// <item><c>"rest"</c>: no table, every hull at level 0 in every element.</item>
        /// <item><c>"L5"</c>: every hull's Time at least level 5 (normalized 0.5), so every Time level-5 upgrade is on: the
        /// card's own row at each intensity with Time raised to max(card, 0.5), never lowered (Garrett 2026-11-16: every
        /// hull starts with its level-5 upgrades; Regatta's card starts the Scarab, Serpent, Sparrow and Stoat at level 10,
        /// and a flat 0.5 had downgraded them). Its other elements stay the card's.</item>
        /// <item><c>{"Manta": {"Time": 5}, ...}</c>: those hulls at those LEVELS (5 = level 5), every intensity; others at rest.</item>
        /// </list>
        /// </summary>
        void ApplyStart(GameDataSO gameData)
        {
            var start = _spec["start"];
            if (start == null || start is JsonValue v0 && v0.GetValue<string>() == "card") return;
            var rows = new List<VesselStartingElements>();
            var hulls = Enum.GetValues<VesselClassType>().Where(h => (int)h > 0);
            if (start is JsonValue v)
            {
                string mode = v.GetValue<string>();
                if (mode == "L5") AddLevel5(gameData, hulls, rows);
                else if (mode != "rest") throw new InvalidOperationException($"start '{mode}': card, rest, L5 or a table");
            }
            else
                foreach (var (hull, levels) in start.AsObject())
                {
                    float L(string e) => (levels?[e]?.GetValue<float>() ?? 0f) / 10f;
                    rows.Add(new VesselStartingElements(Enum.Parse<VesselClassType>(hull), 0,
                        new ResourceCollection(L("Mass"), L("Charge"), L("Space"), L("Time"))));
                }
            gameData.PublishStartingElements(rows);
            _notes.Add("start: " + start.ToJsonString());
        }

        /// <summary>The "L5" start: per hull and intensity, the card's published row with Time raised to at least level 5.</summary>
        static void AddLevel5(GameDataSO gameData, IEnumerable<VesselClassType> hulls, List<VesselStartingElements> rows)
        {
            foreach (var h in hulls)
                for (int i = 1; i <= 4; i++)
                {
                    var lv = VesselStartingElements.TryResolve(gameData.StartingElements, h, i, out var card) ? card : default;
                    lv.Time = Math.Max(lv.Time, 0.5f);
                    rows.Add(new VesselStartingElements(h, i, lv));
                }
        }

        /// <summary><c>"crystal_genes": {"Scarab": 150}</c>: each hull's learned crystal detour budget (world units) for this
        /// run, as <c>RaceAICrystalGenesSO</c> would hold it, without an asset: the judge installs an in-memory one as the
        /// project's genes. Absent = whatever the project ships (no asset = 0, the old rule).</summary>
        void ApplyCrystalGenes()
        {
            if (_spec["crystal_genes"] is not JsonObject genes) return;
            var so = ScriptableObject.CreateInstance<RaceAICrystalGenesSO>();
            foreach (var (hull, slack) in genes)
                so.Rows.Add(new RaceAICrystalGenesSO.Row { Hull = Enum.Parse<VesselClassType>(hull), DetourSlack = slack!.GetValue<float>() });
            const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
            typeof(RaceAICrystalGenesSO).GetField("_cached", S)!.SetValue(null, so);
            typeof(RaceAICrystalGenesSO).GetField("_looked", S)!.SetValue(null, true);
            _notes.Add("crystal_genes: " + genes.ToJsonString());
        }

        static readonly Vector3 HostParking = new(20000f, 20000f, 20000f);

        /// <summary><c>"points_per_pilot": 100000</c>: Broadside's point target for this run (EndConditionOverridesSO, in
        /// memory, both the editor and the build field), so a match runs to <c>limit_s</c> and every seat's points are a
        /// rate over the same time. Absent = the shipped target.</summary>
        void ApplyBroadsideTarget()
        {
            if (_spec["points_per_pilot"]?.GetValue<int>() is not int perPilot) return;
            var so = TrainingHost.LoadAsset(_runtime, "Assets/Resources/EndConditionOverrides.asset")
                ?? throw new InvalidOperationException("EndConditionOverrides.asset not found");
            foreach (var name in new[] { "broadsidePointsPerPilot", "broadsidePointsPerPilotBuild" })
                if (so.GetType().GetField(name, Any) is { } f) f.SetValue(so, perPilot);
            _notes.Add($"points_per_pilot: {perPilot}");
        }

        /// <summary>The scene's game controller: a gate race's, Skim Race's or Broadside's (the one MiniGameControllerBase a scene has).</summary>
        MiniGameControllerBase FindController() => IsSkim
            ? CosmicShore.Engine.Object.FindAnyObjectByType<SkimRaceController>()
            : IsBroadside
                ? CosmicShore.Engine.Object.FindAnyObjectByType<BroadsideController>()
                : CosmicShore.Engine.Object.FindAnyObjectByType<GateRaceController>();

        /// <summary>The course is laid: a gate race knows its gates; Skim Race has drawn its track seed.</summary>
        static bool CourseReady(MiniGameControllerBase controller) => controller switch
        {
            GateRaceController g => g.AuthoritativeGateCount > 0,
            SkimRaceController s => s.TrackSeed != 0,
            BroadsideController => true,
            _ => false,
        };

        /// <summary>Every game but Regatta draws a fresh course per match (GateRaceController.PickCourseSeed), so the judge
        /// forces it: race r flies course <c>course_seeds[r]</c>, or its race seed when the spec names none. The same
        /// seeds then mean the same courses in every process, which a parameter search needs. (Regatta's course comes
        /// from the genome; Switchback's launch-panel fixed seed is 45_500 + intensity.)</summary>
        void ForceCourse(int r)
        {
            if (IsRegatta) return;
            var list = _spec["course_seeds"]?.AsArray();
            CourseSeedOverride.Forced = list != null && r < list.Count ? list[r]!.GetValue<int>() : _seeds[r];
        }

        /// <summary><c>"controller_overrides": {"aiNoseLead.0.Gain": 1.5, "aiApproachLead": 40}</c>: set fields of the race's
        /// controller (the scene's GateRaceController) before every Ready press, for numbers a mode keeps on its scene
        /// rather than in an asset. A number in the path indexes an array; structs are written back.</summary>
        void ApplyControllerOverrides(MiniGameControllerBase controller)
        {
            if (_spec["controller_overrides"] is not JsonObject fields) return;
            SetFields(controller, fields, controller.GetType().Name);
            string note = "controller override: " + controller.GetType().Name + " " + string.Join(", ", fields.Select(kv => kv.Key));
            if (!_notes.Contains(note)) _notes.Add(note);
        }

        static void SetFields(object root, JsonObject fields, string what)
        {
            foreach (var (name, value) in fields ?? new JsonObject())
                SetPath(root, name.Split('.'), 0, value, what + ": " + name);
        }

        static object SetPath(object target, string[] parts, int i, JsonNode value, string what)
        {
            bool last = i == parts.Length - 1;
            if (target is Array arr)
            {
                int idx = int.Parse(parts[i]);
                var elemType = arr.GetType().GetElementType()!;
                arr.SetValue(last ? ConvertJson(value, elemType, what) : SetPath(arr.GetValue(idx)!, parts, i + 1, value, what), idx);
                return arr;
            }
            if (target is System.Collections.IList list && int.TryParse(parts[i], out int li))
            {
                var elemType = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(object);
                list[li] = last ? ConvertJson(value, elemType, what) : SetPath(list[li]!, parts, i + 1, value, what);
                return list;
            }
            var f = FieldOf(target, parts[i]);
            if (last) f.SetValue(target, ConvertJson(value, f.FieldType, what));
            else
            {
                var child = f.GetValue(target) ?? throw new InvalidOperationException(what + " is null");
                f.SetValue(target, SetPath(child, parts, i + 1, value, what));   // write back: the child may be a boxed struct
            }
            return target;
        }

        static object ConvertJson(JsonNode value, Type type, string what) =>
            type.IsArray && value is JsonArray items ? BuildArray(items, type.GetElementType()!, what)
            : type == typeof(float) ? value!.GetValue<float>()
            : type == typeof(int) ? value!.GetValue<int>()
            : type == typeof(bool) ? value!.GetValue<bool>()
            : type.IsEnum ? Enum.Parse(type, value!.ToString())
            : throw new InvalidOperationException($"{what} is a {type.Name}, not a number, flag or enum");

        /// <summary>A whole array from JSON: numbers for a number array, or objects whose fields fill each struct
        /// (<c>"aiNoseLead": [{"Hull": "Scarab", "Gain": 1, "MaxExtraDegrees": 40}]</c>).</summary>
        static Array BuildArray(JsonArray items, Type elem, string what)
        {
            var arr = Array.CreateInstance(elem, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is JsonObject fields)
                {
                    object box = Activator.CreateInstance(elem)!;
                    foreach (var (name, value) in fields) SetPath(box, new[] { name }, 0, value!, $"{what}[{i}]");
                    arr.SetValue(box, i);
                }
                else arr.SetValue(ConvertJson(items[i]!, elem, $"{what}[{i}]"), i);
            }
            return arr;
        }

        static FieldInfo FieldOf(object target, string name)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
                if (t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is { } f) return f;
            throw new InvalidOperationException($"{target.GetType().Name} has no field '{name}'");
        }

        static JsonObject Dnf(string hull, string why) => new()
        {
            ["hull"] = hull, ["finish_s"] = null, ["gates"] = 0, ["laps"] = 0, ["lap_s"] = new JsonArray(),
            ["prism_hits"] = null, ["dnf_reason"] = why,
        };

        /// <summary>Any game but Regatta: the genome's placements laid round that game's own gates
        /// (<see cref="TrainingCourseOverlay"/>); its course still comes from the mode and the seed. The prism is the
        /// one Regatta's rails lay (its I1 cell config's environment).</summary>
        void ApplyTrainingOverlay(JsonObject genome)
        {
            var g = genome != null ? RegattaCourseGenome.FromJson(genome.ToJsonString()) : null;
            TrainingCourseOverlay.Override = g is { Placements.Count: > 0 } ? g : null;
            if (TrainingCourseOverlay.Override == null)
            {
                if (genome != null) _notes.Add($"course: {_mode}'s own (the genome has no placements)");
                return;
            }
            var rails = ReferenceRails(1);
            TrainingCourseOverlay.PrismPrefab = rails
                ? typeof(CellEnvironmentSpawnableBase).GetField("prism", Any)?.GetValue(rails) as Prism
                : null;
            if (!TrainingCourseOverlay.PrismPrefab)
                throw new InvalidOperationException("Regatta's Ref1 rails carry no prism for the training overlay");
            _notes.Add($"course: {_mode}'s own, with {g.Placements.Count} genome placements");
        }

        /// <summary>The rails of Regatta's reference course Ref<paramref name="intensity"/> (course pool entry intensity - 1).</summary>
        SpawnableRegattaRails ReferenceRails(int intensity)
        {
            var pool = TrainingHost.LoadAsset(_runtime, CoursePoolPath) as RegattaCoursePoolSO
                ?? throw new InvalidOperationException(CoursePoolPath + " not found");
            int i = intensity - 1;
            return i >= 0 && i < pool.Count ? pool.Entries[i]?.Rails : null;
        }

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

            var rails = ReferenceRails(intensity)
                ?? throw new InvalidOperationException($"Regatta's Ref{intensity} course has no SpawnableRegattaRails");
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
