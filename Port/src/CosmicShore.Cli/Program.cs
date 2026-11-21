using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine.Tasks;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
// Engine's UGS placeholder also declares an IPlayer — alias the game's player interface.
using IPlayer = CosmicShore.Gameplay.IPlayer;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Cli
{
    /// <summary>
    /// Progress-build harness. Default (milestone M1): proves the first-party engine — game
    /// loop, component lifecycle, transforms, SOAP, DI, async scheduler, networking
    /// primitives, and the ported Data layer — working together, deterministically, with no
    /// Unity. With <c>--mode hexrace</c> (milestone port-m2) it instead runs a full headless
    /// AI-vs-AI game-mode round through the verbatim ported systems.
    ///
    ///   dotnet run --project src/CosmicShore.Cli [-- --frames N] [--quiet]
    ///   dotnet run --project src/CosmicShore.Cli -- --mode hexrace [--players N] [--seed S]
    /// </summary>
    static class Program
    {
        static bool _quiet;
        static readonly List<string> Failures = new();

        static int Main(string[] args)
        {
            int frames = 240;
            string mode = null;
            int players = 4;
            int seed = 42;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--frames" && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsed))
                    frames = parsed;
                if (args[i] == "--mode" && i + 1 < args.Length)
                    mode = args[i + 1].ToLowerInvariant();
                if (args[i] == "--players" && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedPlayers))
                    players = parsedPlayers;
                if (args[i] == "--seed" && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedSeed))
                    seed = parsedSeed;
                if (args[i] == "--quiet") _quiet = true;
            }

            int exitCode;
            if (mode != null)
            {
                // A game mode runs INSTEAD of the engine smoke sections.
                exitCode = mode switch
                {
                    "hexrace" => RunHexRaceMode(players, seed),
                    "astroleague" => RunAstroLeagueMode(players, seed),
                    "joust" => RunJoustMode(players, seed),
                    "crystalcapture" => RunCrystalCaptureMode(players, seed),
                    "tournament" => RunTournamentMode(players, seed),
                    _ => UnknownMode(mode),
                };
            }
            else
            {
                Console.WriteLine("┌──────────────────────────────────────────────────────────┐");
                Console.WriteLine("│  COSMIC SHORE — standalone port  ·  progress build M1    │");
                Console.WriteLine("│  first-party engine smoke ·  v0.1.0-m1 ·  no Unity       │");
                Console.WriteLine("└──────────────────────────────────────────────────────────┘");

                RunStateMachineDemo();
                RunSimulationDemo(frames);
                RunRoundStatsDemo();
                RunPortedLogicDemo();
                RunElementalDemo();
                RunSpawnPipelineDemo();

                Console.WriteLine();
                if (Failures.Count == 0)
                {
                    Console.WriteLine("RESULT: PASS — all engine smoke checks green.");
                    exitCode = 0;
                }
                else
                {
                    Console.WriteLine($"RESULT: FAIL — {Failures.Count} check(s) failed:");
                    foreach (var failure in Failures) Console.WriteLine($"  ✗ {failure}");
                    exitCode = 1;
                }
            }

            // Double-clicked .exe on Windows: hold the window open so the transcript is
            // readable. Scripted/terminal runs with redirected input are unaffected.
            if (OperatingSystem.IsWindows() && !Console.IsInputRedirected && Array.IndexOf(args, "--no-wait") < 0)
            {
                Console.WriteLine();
                Console.Write("Press Enter to exit...");
                Console.ReadLine();
            }

            return exitCode;
        }

        // ── [hexrace] Milestone port-m2 — full headless AI-vs-AI round ──────

        static int UnknownMode(string mode)
        {
            Console.WriteLine($"RESULT: FAIL — unknown --mode '{mode}' (supported: hexrace, astroleague, joust, crystalcapture, tournament).");
            return 1;
        }

        // ── [joust] Overtake jousting — headless AI-vs-AI match through the real
        //    MiniGameControllerBase → MultiplayerDomainGamesController →
        //    MultiplayerJoustController chain (player-seek AI, trigger-pass
        //    vessel-vs-skimmer contacts, golf scoring: winner = elapsed time). ──

        static int RunJoustMode(int players, int seed)
        {
            var options = new JoustRoundOptions { PlayerCount = players, Seed = seed };

            Console.WriteLine("┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("│  COSMIC SHORE — standalone port  ·  JOUST                │");
            Console.WriteLine("│  headless overtake jousting (AI vs AI) ·  no Unity       │");
            Console.WriteLine("└──────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine($"[joust] players={Mathf.Clamp(options.PlayerCount, 2, 12)}, seed={options.Seed}, " +
                              $"target={options.JoustTarget} jousts (domain-aggregated, golf rules)");
            Console.WriteLine();

            var result = JoustRound.Run(options, line => Console.WriteLine("  " + line));

            Console.WriteLine();
            foreach (var error in result.EngineErrors)
                Console.WriteLine($"  ✗ engine error during match: {error}");

            if (!result.Finished)
            {
                Console.WriteLine($"RESULT: FAIL — match did not finish within {result.FramesSimulated} frames.");
                return 1;
            }
            if (result.EngineErrors.Count > 0)
            {
                Console.WriteLine($"RESULT: FAIL — match finished but {result.EngineErrors.Count} engine error(s) were logged.");
                return 1;
            }

            Console.WriteLine($"RESULT: PASS — winner '{result.WinnerName}' ({result.WinnerDomain}) in " +
                              $"{result.FinishTime:F2}s · {result.FramesSimulated} frames · {result.TotalJousts} jousts scored.");
            return 0;
        }

        // ── [crystalcapture] Crystal Capture — headless AI-vs-AI round through the
        //    real MiniGameControllerBase → MultiplayerDomainGamesController →
        //    MultiplayerCrystalCaptureController chain (crystal-seek AI, trigger-pass
        //    claims, points scoring: score = crystals captured). ─────────────────

        static int RunCrystalCaptureMode(int players, int seed)
        {
            var options = new CrystalCaptureRoundOptions { PlayerCount = players, Seed = seed };

            Console.WriteLine("┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("│  COSMIC SHORE — standalone port  ·  CRYSTAL CAPTURE       │");
            Console.WriteLine("│  headless crystal race (AI vs AI) ·  no Unity            │");
            Console.WriteLine("└──────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine($"[crystalcapture] players={Mathf.Clamp(options.PlayerCount, 1, 4)}, seed={options.Seed}, " +
                              $"target={options.CrystalTarget} crystals (domain-aggregated, points)");
            Console.WriteLine();

            var result = CrystalCaptureRound.Run(options, line => Console.WriteLine("  " + line));

            Console.WriteLine();
            foreach (var error in result.EngineErrors)
                Console.WriteLine($"  ✗ engine error during round: {error}");

            if (!result.Finished)
            {
                Console.WriteLine($"RESULT: FAIL — round did not finish within {result.FramesSimulated} frames.");
                return 1;
            }
            if (result.EngineErrors.Count > 0)
            {
                Console.WriteLine($"RESULT: FAIL — round finished but {result.EngineErrors.Count} engine error(s) were logged.");
                return 1;
            }

            Console.WriteLine($"RESULT: PASS — winner '{result.WinnerName}' ({result.WinnerDomain}) with " +
                              $"{result.WinnerDomainCrystals} domain crystals · {result.FramesSimulated} frames · {result.TotalClaims} claims.");
            return 0;
        }

        // ── [tournament] Maelstrom — the session-level meta chaining the domain
        //    minigames through the real TournamentController: lobby → random draw
        //    (mode + intensity) → headless leg → network-free {2,1,0} standings fold
        //    → hub → … → race-to-6 (or cap) → summary. Every leg is simulated by the
        //    headless HexRace round until the Joust / Crystal Capture controllers
        //    port (the draw/fold/summary path is the real Tournament system). ─────

        static int RunTournamentMode(int players, int seed)
        {
            var options = new TournamentRoundOptions { PlayerCount = players, Seed = seed };

            Console.WriteLine("┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("│  COSMIC SHORE — standalone port  ·  MAELSTROM             │");
            Console.WriteLine("│  headless tournament meta (AI vs AI legs) ·  no Unity     │");
            Console.WriteLine("└──────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine($"[tournament] players={Mathf.Clamp(options.PlayerCount, 1, 12)}, seed={options.Seed}, " +
                              $"leg target={options.LegCrystalTarget} crystals, intensity ceiling={options.IntensityCeiling}");
            Console.WriteLine();

            var result = TournamentRound.Run(options, line => Console.WriteLine("  " + line));

            Console.WriteLine();
            foreach (var error in result.EngineErrors)
                Console.WriteLine($"  ✗ engine error during session: {error}");

            if (!result.Finished)
            {
                Console.WriteLine($"RESULT: FAIL — the shuffle did not reach the summary (games played: {result.GamesPlayed}).");
                return 1;
            }
            if (result.EngineErrors.Count > 0)
            {
                Console.WriteLine($"RESULT: FAIL — shuffle finished but {result.EngineErrors.Count} engine error(s) were logged.");
                return 1;
            }

            Console.WriteLine($"RESULT: PASS — {result.WinnerDomain} domain takes the Maelstrom with " +
                              $"{result.WinnerPoints} placement crystals after {result.GamesPlayed} game(s).");
            return 0;
        }

        // ── [astroleague] Hypersea soccer — headless AI-vs-AI match through the
        //    real MiniGameControllerBase → MultiplayerDomainGamesController →
        //    AstroLeagueController chain (server-simulated ball, goal-plane detection,
        //    mercy rule / full time / golden goal). ─────────────────────────────

        static int RunAstroLeagueMode(int players, int seed)
        {
            var options = new AstroLeagueRoundOptions { PlayerCount = players, Seed = seed };

            Console.WriteLine("┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("│  COSMIC SHORE — standalone port  ·  ASTRO LEAGUE          │");
            Console.WriteLine("│  headless hypersea soccer (AI vs AI) ·  no Unity          │");
            Console.WriteLine("└──────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine($"[astroleague] players={Mathf.Clamp(options.PlayerCount, 2, 6)}, seed={options.Seed}, " +
                              $"goal limit={options.GoalLimit} (mercy), clock={options.MatchDurationSeconds:0}s (golden goal on tie)");
            Console.WriteLine();

            var result = AstroLeagueRound.Run(options, line => Console.WriteLine("  " + line));

            Console.WriteLine();
            foreach (var error in result.EngineErrors)
                Console.WriteLine($"  ✗ engine error during match: {error}");

            if (!result.Finished)
            {
                Console.WriteLine($"RESULT: FAIL — match did not finish within {result.FramesSimulated} frames.");
                return 1;
            }
            if (result.EngineErrors.Count > 0)
            {
                Console.WriteLine($"RESULT: FAIL — match finished but {result.EngineErrors.Count} engine error(s) were logged.");
                return 1;
            }

            Console.WriteLine($"RESULT: PASS — {result.WinnerDomain} wins Jade {result.JadeGoals}–{result.RubyGoals} Ruby" +
                              $"{(result.WentToOvertime ? " (golden goal)" : "")} · top scorer '{result.WinnerName}' · " +
                              $"{result.FramesSimulated} frames · {result.TotalStrikes} strikes.");
            return 0;
        }

        static int RunHexRaceMode(int players, int seed)
        {
            var options = new HexRaceRoundOptions { PlayerCount = players, Seed = seed };

            Console.WriteLine("┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("│  COSMIC SHORE — standalone port  ·  progress build M2    │");
            Console.WriteLine("│  headless game-mode round (AI vs AI) ·  no Unity         │");
            Console.WriteLine("└──────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine($"[hexrace] players={Mathf.Clamp(options.PlayerCount, 1, 12)}, seed={options.Seed}, " +
                              $"target={options.CrystalTarget} crystals (domain-aggregated, golf rules)");
            Console.WriteLine();

            var result = HexRaceRound.Run(options, line => Console.WriteLine("  " + line));

            Console.WriteLine();
            foreach (var error in result.EngineErrors)
                Console.WriteLine($"  ✗ engine error during round: {error}");

            if (!result.Finished)
            {
                Console.WriteLine($"RESULT: FAIL — race did not finish within {result.FramesSimulated} frames.");
                return 1;
            }
            if (result.EngineErrors.Count > 0)
            {
                Console.WriteLine($"RESULT: FAIL — round finished but {result.EngineErrors.Count} engine error(s) were logged.");
                return 1;
            }

            Console.WriteLine($"RESULT: PASS — winner '{result.WinnerName}' ({result.WinnerDomain}) in " +
                              $"{result.FinishTime:F2}s · {result.FramesSimulated} frames · {result.TotalClaims} crystals claimed.");
            return 0;
        }

        static void Print(string message)
        {
            if (!_quiet) Console.WriteLine(message);
        }

        static void Check(bool condition, string description)
        {
            if (condition) Print($"  ✓ {description}");
            else
            {
                Failures.Add(description);
                Console.WriteLine($"  ✗ {description}");
            }
        }

        // ── [1] Application state walk via SOAP ─────────────────────

        static void RunStateMachineDemo()
        {
            Console.WriteLine();
            Console.WriteLine("[1] Application state walk (SOAP variable + event channel)");

            var stateVariable = new ScriptableVariable<ApplicationState> { name = "ApplicationStateData" };
            var stateChanged = new ScriptableEvent<ApplicationState> { name = "OnApplicationStateChanged" };

            var observed = new List<ApplicationState>();
            stateVariable.OnValueChanged += s => stateChanged.Raise(s);
            stateChanged.OnRaised += s => observed.Add(s);

            var walk = new[]
            {
                ApplicationState.Bootstrapping, ApplicationState.Authenticating,
                ApplicationState.MainMenu, ApplicationState.LoadingGame,
                ApplicationState.InGame, ApplicationState.GameOver, ApplicationState.MainMenu,
            };
            foreach (var state in walk)
            {
                stateVariable.Value = state;
                Print($"  state → {state}");
            }

            Check(observed.Count == walk.Length, $"all {walk.Length} transitions observed through the event channel");
            Check(stateVariable.PreviousValue == ApplicationState.GameOver, "PreviousValue tracks GameOver → MainMenu");
        }

        // ── [2] Deterministic simulation: loop + lifecycle + tasks + DI ──

        interface ITelemetry { void Record(string entry); List<string> Entries { get; } }

        sealed class Telemetry : ITelemetry
        {
            public List<string> Entries { get; } = new();
            public void Record(string entry) { Entries.Add(entry); }
        }

        sealed class ProbeVessel : MonoBehaviour
        {
            [Inject] public ITelemetry Telemetry;

            public float Speed = 10f;
            public int FixedSteps;

            void Start() => Telemetry.Record($"frame {Time.frameCount}: {name} started");

            void Update() => transform.position += transform.forward * (Speed * Time.deltaTime);

            void FixedUpdate() => FixedSteps++;

            public async Task RunPilotScript()
            {
                await GameTask.Delay(0.5f);
                Speed = 20f;
                Telemetry.Record($"frame {Time.frameCount}: boost engaged (t={Time.time:F2}s)");

                await GameTask.WaitUntil(() => transform.position.z >= 20f);
                Telemetry.Record($"frame {Time.frameCount}: crystal waypoint reached (z={transform.position.z:F1})");
            }
        }

        static void RunSimulationDemo(int frames)
        {
            Console.WriteLine();
            Console.WriteLine($"[2] Headless simulation — {frames} frames @ 60 Hz (loop/lifecycle/tasks/DI)");

            using var loop = new GameLoop("CliSmoke");
            Time.fixedDeltaTime = 1f / 50f;

            var container = new Container();
            container.RegisterValue<ITelemetry>(new Telemetry());

            var go = new GameObject("ProbeVessel");
            var vessel = go.AddComponent<ProbeVessel>();
            container.InjectGameObject(go);

            var pilotScript = vessel.RunPilotScript();
            loop.Run(frames, 1f / 60f);

            var telemetry = container.Resolve<ITelemetry>();
            foreach (var entry in telemetry.Entries) Print($"  {entry}");
            Print($"  final position: {go.transform.position}, fixed steps: {vessel.FixedSteps}");

            // Expected: 0.5s at 10 u/s ≈ 5u, remainder at 20 u/s. Boost lands on the frame
            // after 0.5s of game time; tolerance covers the one-frame boundary.
            float seconds = frames / 60f;
            float expectedZ = 5f + (seconds - 0.5f) * 20f;
            float actualZ = go.transform.position.z;
            Check(Mathf.Abs(actualZ - expectedZ) < 0.7f, $"deterministic distance: z={actualZ:F2} (expected ≈{expectedZ:F2})");
            Check(pilotScript.IsCompletedSuccessfully, "pilot script (Delay → boost → WaitUntil) completed");
            Check(vessel.FixedSteps == (int)(seconds * 50f), $"fixed steps: {vessel.FixedSteps} (expected {(int)(seconds * 50f)} @ 50 Hz)");
            Check(telemetry.Entries.Count == 3, "telemetry captured start, boost, and waypoint events");

            Object.Destroy(go);
            loop.Tick(1f / 60f);
            Check(go == null, "vessel destroyed cleanly at end of frame (fake-null contract)");
        }

        // ── [3] Ported Data layer: RoundStats in server mode ────────

        static void RunRoundStatsDemo()
        {
            Console.WriteLine();
            Console.WriteLine("[3] Ported RoundStats (NetworkBehaviour) — server-mode stat events");

            var stats = new RoundStats();
            stats.Spawn(isServer: true, isClient: true);
            stats.Name = "HostPilot";
            stats.Domain = Domains.Jade;

            int anyStatEvents = 0;
            int crystalEvents = 0;
            stats.OnAnyStatChanged += _ => anyStatEvents++;
            stats.OnCrystalsCollectedChanged += s => crystalEvents++;

            for (int i = 1; i <= 3; i++)
            {
                stats.CrystalsCollected = i;
                stats.Score += 25f;
                Print($"  crystal {i} collected → score {stats.Score}");
            }

            Check(stats.CrystalsCollected == 3 && stats.Score == 75f, "stat values propagate through NetworkVariables");
            Check(crystalEvents == 3, $"crystal events fired per change ({crystalEvents}/3)");
            Check(anyStatEvents >= 6, $"aggregate OnAnyStatChanged fired ({anyStatEvents} events)");
            Check(stats.Domain == Domains.Jade && (string)stats.Name == "HostPilot", "identity (name + domain) survives replication path");

            ((IRoundStats)stats).Cleanup();
            Check(stats.Score == 0f && stats.CrystalsCollected == 0, "Cleanup resets stats between rounds");
        }

        // ── [4] Ported game logic: SOAP custom types + cell phase rules ──

        static void RunPortedLogicDemo()
        {
            Console.WriteLine();
            Console.WriteLine("[4] Ported game logic — SOAP custom types + cell phase hysteresis");

            // CrystalStats SOAP channel (ported ScriptableEventCrystalStats + struct).
            var crystalChannel = new CosmicShore.ScriptableObjects.ScriptableEventCrystalStats { name = "Event_CrystalStats" };
            float totalValue = 0f;
            crystalChannel.OnRaised += s => totalValue += s.Value;
            crystalChannel.Raise(new Gameplay.CrystalStats { PlayerName = "HostPilot", Element = Element.Charge, Value = 1.5f });
            crystalChannel.Raise(new Gameplay.CrystalStats { PlayerName = "HostPilot", Element = Element.Mass, Value = 2.5f });
            Check(totalValue == 4f, $"CrystalStats events delivered through ported channel (total {totalValue})");

            // Party roster via ported ScriptableListPartyPlayerData (equality by PlayerId).
            var roster = new CosmicShore.ScriptableObjects.ScriptableListPartyPlayerData { name = "List_OnlinePlayers" };
            int joins = 0, leaves = 0;
            roster.OnItemAdded += _ => joins++;
            roster.OnItemRemoved += _ => leaves++;
            roster.Add(new CosmicShore.ScriptableObjects.PartyPlayerData("p1", "HostPilot", avatarId: 3));
            roster.Add(new CosmicShore.ScriptableObjects.PartyPlayerData("p2", "WingMate", avatarId: 7));
            roster.Remove(new CosmicShore.ScriptableObjects.PartyPlayerData("p2", "renamed-but-same-id", avatarId: 0));
            Print($"  party roster: {joins} joins, {leaves} leaves, {roster.Count} online");
            Check(joins == 2 && leaves == 1 && roster.Count == 1, "party roster list events + PlayerId equality");

            // Cell phase walk (ported CellPhaseRules + default thresholds): climb with the
            // prism VOLUME (the spine — count is only the Frenzy perf backstop since the
            // bleeding-edge volume rework), hold inside the hysteresis band, multi-step
            // descent in one call. Default derives volume thresholds = count × 16.
            var thresholds = Utility.CellPhaseThresholds.Default;
            var phase = CellPhase.Calm;
            var walk = new (int count, CellPhase expected)[]
            {
                (0, CellPhase.Calm),
                (9000, CellPhase.Restless),   // ≥ RestlessEnter (8000)
                (16000, CellPhase.Frenzy),    // ≥ FrenzyEnter (15000)
                (14500, CellPhase.Frenzy),    // hysteresis band: holds above FrenzyExit (14000)
                (700, CellPhase.Calm),        // collapse: multi-step descent resolves at once
            };
            bool phasesOk = true;
            foreach (var (count, expected) in walk)
            {
                float volume = count * Utility.CellPhaseThresholds.NominalPrismVolume;
                phase = Utility.CellPhaseRules.Compute(volume, count, phase, in thresholds);
                Print($"  prisms={count,5} → {phase}");
                phasesOk &= phase == expected;
            }
            Check(phasesOk, "cell phase transitions follow hysteresis thresholds");
        }

        // ── [5] Ported ResourceSystem: crystals, buffs, decay ────────

        static void RunElementalDemo()
        {
            Console.WriteLine();
            Console.WriteLine("[5] Ported ResourceSystem — crystal pickups, temporary debuff decay");

            using var loop = new GameLoop("ElementalDemo");
            var vessel = new GameObject("DemoVessel");
            vessel.SetActive(false); // configure before Awake (runtime-AddComponent pattern)
            var system = vessel.AddComponent<Gameplay.ResourceSystem>();
            system.Resources = new List<Gameplay.Resource> { new() { Name = "boost", resourceGainRate = 0.1f } };
            system.InitializeElementLevels(new ResourceCollection(0f, 0f, 0f, 0f));
            vessel.SetActive(true);

            var levelLog = new List<string>();
            system.OnElementLevelChange += (element, level) => levelLog.Add($"{element}→{level}");
            loop.Tick(1f / 60f);

            for (int i = 0; i < 3; i++) system.IncrementLevel(Element.Charge); // 3 charge crystals
            Print($"  3 charge crystals collected → Charge level {system.GetLevel(Element.Charge)}");

            system.ApplyElementalEffect(Element.Space, -0.4f, duration: 1f);   // danger-prism debuff
            loop.Tick(1f / 60f);
            int debuffed = system.GetLevel(Element.Space);
            Print($"  danger-prism Space debuff applied → Space level {debuffed}");

            loop.Run(70, 1f / 60f); // decay past 1s
            Print($"  debuff decayed after 1s → Space level {system.GetLevel(Element.Space)}");
            Print($"  level events: {string.Join(", ", levelLog)}");

            Check(system.GetLevel(Element.Charge) == 3, "crystal pickups raise the element level permanently");
            Check(debuffed <= -3, "temporary debuff lowers the effective level");
            Check(system.GetLevel(Element.Space) == 0, "debuff decays back without touching base progress");
        }

        // ── [6] Concrete-arc exit state: verbatim spawning pipeline (C1–C6) ──

        /// <summary>Recording HUD controller for the prefab fixture's serialized slot.</summary>
        sealed class CliVesselHud : MonoBehaviour, IVesselHUDController
        {
            public int InitializeCalls, HideCalls;
            public void Initialize(IVesselStatus vesselStatus) => InitializeCalls++;
            public void SubscribeToEvents() { }
            public void UnsubscribeFromEvents() { }
            public void ShowHUD() { }
            public void HideHUD() => HideCalls++;
            public void SetBlockPrefab(GameObject prefab) { }
        }

        /// <summary>Concrete VesselAnimation (base is abstract) — no-op puppetry.</summary>
        sealed class CliVesselAnimation : Gameplay.VesselAnimation
        {
            protected override void AssignTransforms() { }
            protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
        }

        /// <summary>Harness-side stand-in for inspector wiring of serialized fields.</summary>
        static void SetPrivateField(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) continue;
                f.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }

        /// <summary>
        /// Builds the programmatic vessel "prefab": the real VesselController +
        /// VesselStatus + the ten RequireComponent siblings + child near/far skimmers,
        /// serialized refs wired. The root stays INACTIVE — prefab semantics; the
        /// E16 clone remap makes Instantiate prefab-faithful, and the harness
        /// activates clones into the scene.
        /// </summary>
        static GameObject BuildVesselPrefab(string name, VesselClassType vesselType, GameDataSO gameData)
        {
            var go = new GameObject(name);
            go.SetActive(false);

            go.AddComponent<VesselPrismController>();

            var resources = go.AddComponent<Gameplay.ResourceSystem>();
            resources.Resources = new List<Gameplay.Resource> { new() { Name = "Energy" } };

            go.AddComponent<VesselTransformer>();

            var pilot = go.AddComponent<AIPilot>();
            SetPrivateField(pilot, "cellData", ScriptableObject.CreateInstance<CellRuntimeDataSO>());
            SetPrivateField(pilot, "OnCellItemsUpdated", ScriptableObject.CreateInstance<ScriptableEventNoParam>());
            SetPrivateField(pilot, "abilities", new List<AIAbility>());

            go.AddComponent<SilhouetteController>();

            var cameraCustomizer = go.AddComponent<VesselCameraCustomizer>();
            SetPrivateField(cameraCustomizer, "OnInitializePlayerCamera",
                ScriptableObject.CreateInstance<ScriptableEventTransform>());

            go.AddComponent<CliVesselAnimation>();

            var actionHandler = go.AddComponent<R_VesselActionHandler>();
            SetPrivateField(actionHandler, "_onButtonPressed", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_onButtonReleased", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());

            var geometry = new GameObject("geometry");
            geometry.transform.SetParent(go.transform);
            var customization = go.AddComponent<VesselCustomization>();
            SetPrivateField(customization, "_shipGeometries", new List<GameObject> { geometry });

            go.AddComponent<R_ShipElementStatsHandler>();

            var hud = go.AddComponent<CliVesselHud>();
            var controller = go.AddComponent<VesselController>();
            SetPrivateField(controller, "gameData", gameData);
            var status = go.AddComponent<VesselStatus>();

            Skimmer NewChildSkimmer(string skimmerName)
            {
                var child = new GameObject(skimmerName);
                child.transform.SetParent(go.transform);
                var skimmer = child.AddComponent<Skimmer>();
                SetPrivateField(skimmer, "onSkimmerShipImpact", ScriptableObject.CreateInstance<ScriptableEventString>());
                return skimmer;
            }

            var orientationHandle = new GameObject("orientationHandle");
            orientationHandle.transform.SetParent(go.transform);

            SetPrivateField(status, "_shipInstance", controller);
            SetPrivateField(status, "vesselHUDController", hud);
            SetPrivateField(status, "orientationHandle", orientationHandle);
            SetPrivateField(status, "_name", name);
            SetPrivateField(status, "vesselType", vesselType);
            SetPrivateField(status, "_nearFieldSkimmer", NewChildSkimmer("nearFieldSkimmer"));
            SetPrivateField(status, "_farFieldSkimmer", NewChildSkimmer("farFieldSkimmer"));

            return go;
        }

        static void RunSpawnPipelineDemo()
        {
            Console.WriteLine();
            Console.WriteLine("[6] Concrete-arc exit state — verbatim spawning pipeline (C1–C6)");

            using var loop = new GameLoop("SpawnPipeline");
            NetworkManager.Singleton = null;
            AuthenticationService.Reset();

            // Shared data: theme (Jade set populated so ShipHelper's paint is observable)
            // + GameDataSO with the SOAP events the pipeline raises.
            var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
            theme.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet>();
            foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold })
                theme.TeamMaterialSets[domain] = ScriptableObject.CreateInstance<SO_MaterialSet>();
            var jadeSet = theme.TeamMaterialSets[Domains.Jade];
            jadeSet.ShipMaterial = new Material((Shader)null) { name = "jade-ship" };
            jadeSet.BlockSilhouettePrefab = new GameObject("jade-silhouette");

            var gameData = ScriptableObject.CreateInstance<GameDataSO>();
            gameData.ThemeManagerData = theme;
            gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            gameData.OnPlayerNetworkSpawnedUlong = ScriptableObject.CreateInstance<ScriptableEventUlong>();
            gameData.OnVesselNetworkSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
            gameData.selectedVesselClass.Value = VesselClassType.Sparrow;

            var spawnPositions = new[] { new Vector3(40f, 0f, 0f), new Vector3(-40f, 0f, 0f), new Vector3(0f, 0f, 40f) };
            var spawnTransforms = new Transform[spawnPositions.Length];
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                var point = new GameObject($"spawnPoint{i}");
                point.transform.position = spawnPositions[i];
                spawnTransforms[i] = point.transform;
            }
            gameData.SetSpawnPositions(spawnTransforms);

            // Prefab fixtures: the inactive vessel template registered in a
            // VesselPrefabContainer, and a player template carrying the real Player.
            var vesselTemplate = BuildVesselPrefab("SparrowPrefab", VesselClassType.Sparrow, gameData);
            var templateStatus = vesselTemplate.GetComponent<VesselStatus>();
            var templateController = vesselTemplate.GetComponent<VesselController>();
            var prefabContainer = ScriptableObject.CreateInstance<VesselPrefabContainer>();
            SetPrivateField(prefabContainer, "_shipPrefabs", new[] { vesselTemplate.transform });

            var playerPrefabGo = new GameObject("PlayerPrefab");
            var playerPrefab = playerPrefabGo.AddComponent<Player>();
            SetPrivateField(playerPrefab, "gameData", gameData);

            Print($"  prefab fixture: '{vesselTemplate.name}' — VesselController + VesselStatus + 10 components + 2 child skimmers (root inactive: prefab semantics)");

            // DI + spawners — the same Reflex-shaped wiring the scenes use.
            var container = new Container();
            container.RegisterValue(gameData);
            var playerDataService = new GameObject("PlayerDataService").AddComponent<PlayerDataService>();
            container.RegisterValue(playerDataService);

            var spawnerGo = new GameObject("Spawners");
            var vesselSpawner = spawnerGo.AddComponent<VesselSpawner>();
            SetPrivateField(vesselSpawner, "vesselPrefabContainer", prefabContainer);
            var playerSpawner = spawnerGo.AddComponent<PlayerSpawner>();
            SetPrivateField(playerSpawner, "_playerPrefab", playerPrefab);
            SetPrivateField(playerSpawner, "vesselSpawner", vesselSpawner);
            container.InjectGameObject(spawnerGo);

            // ── Human spawn through the verbatim pipeline ────────────────
            var player = playerSpawner.SpawnPlayerAndShip(new IPlayer.InitializeData
            {
                vesselClass = VesselClassType.Sparrow,
                PlayerName = "CliPilot",
                AvatarId = 1,
                AllowSpawning = true,
                IsAI = false,
            });
            Check(player != null, "PlayerSpawner.SpawnPlayerAndShip returned a live IPlayer");

            var vessel = (VesselController)player.Vessel;
            var vesselGo = vessel.gameObject;
            var cloneStatus = vesselGo.GetComponent<VesselStatus>();
            Print($"  SpawnPlayerAndShip(\"CliPilot\") → player '{player.Name}' + vessel '{vesselGo.name}'");

            // The survey's corruption sentinel — E16 clone remap proof.
            Check(ReferenceEquals(cloneStatus.Vessel, vessel),
                "corruption sentinel: clone VesselStatus.Vessel is the clone's OWN VesselController");
            Check(!ReferenceEquals(cloneStatus.Vessel, templateController) &&
                  ReferenceEquals(templateStatus.Vessel, templateController),
                "no template aliasing: template VesselStatus still points at the template controller");
            Check(ReferenceEquals(cloneStatus.Player, player) && ReferenceEquals(player.Vessel, vessel),
                "pair wired both ways: InitializeForSinglePlayerMode + vessel.Initialize(player)");
            Check(ReferenceEquals(cloneStatus.ShipMaterial, jadeSet.ShipMaterial),
                "vessel.Initialize ran the full chain: Jade theme painted via ShipHelper");
            Check(player.Domain == Domains.Jade && (string)player.RoundStats.Name == "CliPilot" && player.InputStatus.Paused,
                "single-player defaults: Jade domain, RoundStats named, input starts paused");

            // Scene placement: clones mirror the template's inactive root — the
            // harness activates them (Awake/OnEnable now run with remapped fields).
            vesselGo.SetActive(true);

            // ── GameDataSO.AddPlayer ─────────────────────────────────────
            gameData.AddPlayer(player);
            var assignedPose = vesselGo.transform.position;
            Check(gameData.Players.Contains(player) && gameData.RoundStatsList.Contains(player.RoundStats),
                "AddPlayer registered the player + its RoundStats");
            Check(spawnPositions.Any(p => (p - assignedPose).sqrMagnitude < 1e-6f),
                $"AddPlayer assigned a spawn pose: vessel at {assignedPose}");
            Check(gameData.LocalPlayer == null,
                "LocalPlayer stays unset for the unspawned single-player path (IsLocalUser == spawned owner)");

            // ── StartPlayer → ticked frames → ResetForPlay ───────────────
            player.StartPlayer();
            bool spawnerEnabled = (bool)typeof(VesselPrismController)
                .GetField("spawnerEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(cloneStatus.VesselPrismController)!;
            Check(player.IsActive && !cloneStatus.IsStationary && !player.InputStatus.Paused && spawnerEnabled,
                "StartPlayer: vessel un-stationed, input unpaused, prism spawner enabled");
            cloneStatus.VesselPrismController.StopSpawn(); // end the async spawn loop before the section exits

            var before = vesselGo.transform.position;
            loop.Run(60, 1f / 60f);
            float movedDistance = (vesselGo.transform.position - before).magnitude;
            Print($"  60 frames @ 60 Hz → vessel moved {movedDistance:F1}u (now at {vesselGo.transform.position})");
            Check(movedDistance > 1f, "VesselTransformer drives the clone while un-stationed");

            player.ResetForPlay();
            var frozen = vesselGo.transform.position;
            loop.Run(10, 1f / 60f);
            Check(cloneStatus.IsStationary && !player.IsActive && vesselGo.transform.position == frozen,
                "ResetForPlay restored the documented reset state (stationary, inactive, holds position)");

            // ── AI variant ───────────────────────────────────────────────
            var bot = playerSpawner.SpawnPlayerAndShip(new IPlayer.InitializeData
            {
                vesselClass = VesselClassType.Sparrow,
                PlayerName = "CliBot",
                AllowSpawning = true,
                IsAI = true,
            });
            gameData.AddPlayer(bot);
            bot.StartPlayer();
            var botStatus = ((VesselController)bot.Vessel).gameObject.GetComponent<VesselStatus>();
            Check(bot.IsInitializedAsAI && botStatus.AIPilot.AutoPilotEnabled && bot.InputStatus.Paused,
                "AI variant: StartPlayer toggled the autopilot ON and kept input paused");
            botStatus.VesselPrismController.StopSpawn();
            bot.ResetForPlay();
            Check(!botStatus.AIPilot.AutoPilotEnabled && !bot.IsActive,
                "AI variant: ResetForPlay toggled the autopilot OFF");

            // ── Networked variant (host-mode Player.Spawn) ───────────────
            AuthenticationService.Instance.PlayerName = "CliHost#1234";
            var spawnEvents = new List<ulong>();
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised += id => spawnEvents.Add(id);

            var netGo = new GameObject("NetPlayer");
            var netPlayer = netGo.AddComponent<Player>();
            SetPrivateField(netPlayer, "gameData", gameData);
            netPlayer.Spawn(); // host-mode: server + client + owner

            Print($"  Player.Spawn() (host-mode) → OnPlayerNetworkSpawnedUlong({string.Join(",", spawnEvents)}), Name='{netPlayer.Name}'");
            Check(spawnEvents.Count == 1 && spawnEvents[0] == 0,
                "networked variant: spawn event raised exactly once for host clientId 0");
            Check(netPlayer.Name == "CliHost",
                "display name resolved through the fallback chain (auth shim, '#1234' suffix stripped)");
            Check(netPlayer.IsLocalUser && gameData.Players.Contains(netPlayer),
                "spawned owner is the local user and registered in GameDataSO.Players");

            netPlayer.ChangeVessel(player.Vessel); // pair with the live vessel so AddPlayer can pose it
            gameData.AddPlayer(netPlayer);
            Check(ReferenceEquals(gameData.LocalPlayer, netPlayer),
                "AddPlayer set LocalPlayer (and a spawn pose) for the spawned local user");

            netPlayer.DestroyPlayer(); // NetworkObject.Despawn(true)
            loop.Tick(1f / 60f);
            Check(!netPlayer.IsSpawned && !gameData.Players.Contains(netPlayer) && netGo.IsDestroyed,
                "DestroyPlayer despawned via NetworkObject.Despawn(true), deregistered, destroyed the GameObject");

            // Static singletons back to neutral for any later sections.
            typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
            AuthenticationService.Reset();
            NetworkManager.Singleton = null;
        }
    }
}
