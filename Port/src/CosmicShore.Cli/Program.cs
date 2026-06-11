using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine.Tasks;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Cli
{
    /// <summary>
    /// Progress-build harness (milestone M1): proves the first-party engine — game loop,
    /// component lifecycle, transforms, SOAP, DI, async scheduler, networking primitives,
    /// and the ported Data layer — working together, deterministically, with no Unity.
    ///
    ///   dotnet run --project src/CosmicShore.Cli [-- --frames N] [--quiet]
    /// </summary>
    static class Program
    {
        static bool _quiet;
        static readonly List<string> Failures = new();

        static int Main(string[] args)
        {
            int frames = 240;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--frames" && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsed))
                    frames = parsed;
                if (args[i] == "--quiet") _quiet = true;
            }

            Console.WriteLine("┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("│  COSMIC SHORE — standalone port  ·  progress build M1    │");
            Console.WriteLine("│  first-party engine smoke ·  v0.1.0-m1 ·  no Unity       │");
            Console.WriteLine("└──────────────────────────────────────────────────────────┘");

            RunStateMachineDemo();
            RunSimulationDemo(frames);
            RunRoundStatsDemo();
            RunPortedLogicDemo();
            RunElementalDemo();

            Console.WriteLine();
            int exitCode;
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
            // prism count, hold inside the hysteresis band, multi-step descent in one call.
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
                phase = Utility.CellPhaseRules.Compute(count, phase, in thresholds);
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
    }
}
