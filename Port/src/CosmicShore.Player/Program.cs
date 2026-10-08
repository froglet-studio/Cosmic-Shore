using System;
using System.Linq;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Player
{
    /// <summary>
    /// CosmicShore — the port's player. Boots build scene 0 (Bootstrap) and lets the real
    /// game take it from there.
    ///
    ///   CosmicShore [--scene NAME] [--size WxH] [--screenshot out.png] [--frames N]
    ///               [--shot FRAME:out.png]... [--do FRAME:ACTION]...
    ///   CosmicShore --headless [--realtime] [--frames N] [--scene NAME] [--quiet] [--do FRAME:ACTION]...
    ///   CosmicShore [--headless] --replay FILE --parity-out DIR     (parity harness, see ParityRun)
    ///   CosmicShore --random-golden DIR --seeds S1,S2,...
    ///   CosmicShore --train [train|replay|eval] [--episodes N] [--repeats K] [--scenario NAME] [--train-out DIR]
    ///               [--seed S] [--frames CAP] [--workers N] [--evals K] [--generations G] [--recycle-mb MB]
    ///
    /// --verbose opens every CSDebug log channel (a development build's bring-up traces).
    /// --do scripts input (see <see cref="InputScript"/>); --shot captures extra frames.
    ///
    /// --headless ticks the engine with no window at a fixed 60 Hz for N frames (default
    /// 600) and prints a scene/log summary — the fast loop for chasing boot problems. --realtime (or
    /// COSMIC_SHORE_HEADLESS_REALTIME=1) paces those ticks to the wall clock, which a multiplayer run
    /// needs: other processes, the session folder and the sockets all run on wall time
    /// (<see cref="RealtimePacer"/>, docs/MULTIPLAYER.md §6.1).
    ///
    /// --train runs the game's own AI genetic training headless (see <see cref="TrainingHost"/>);
    /// "replay" re-scores the generation the session asset scored in Unity and reports the disparity.
    /// "train" evolves generation by generation over --workers processes (one per core), each
    /// genome flown --evals times per generation. "eval" flies the --genome FILE(s) --flights
    /// times each, racing each other, and reports each one's mean fitness and standard error.
    /// </summary>
    public static class Program
    {
        /// <summary>--realtime: a headless run keeps game time on the wall clock.</summary>
        static bool s_realtime = RealtimePacer.RequestedByEnvironment;

        /// <summary>
        /// Quality from the engine's own Project Settings (Port/ProjectSettings/PrismaProject.json),
        /// before environment variables and arguments, which override it for one run.
        /// </summary>
        static void ApplyProjectQuality()
        {
            var root = CosmicShore.Content.AssetDatabase.FindProjectRoot();
            if (root == null) return;
            var q = Prisma.PrismaProjectSettings.Load(root).Quality;
            if (q.Msaa is { } m) CosmicShore.Render.RenderQuality.Msaa = m;
            if (q.RenderScale is { } s) CosmicShore.Render.RenderQuality.RenderScale = s;
            if (q.Anisotropy is { } a) CosmicShore.Render.RenderQuality.Anisotropy = a;
            if (q.VSync is { } v) CosmicShore.Render.RenderQuality.VSync = v;
            if (q.TargetFps is { } f) CosmicShore.Render.RenderQuality.TargetFps = f;
        }

        public static int Main(string[] args)
        {
            string scene = null, screenshot = null;
            int frames = -1, width = 1600, height = 900;
            bool headless = false, quiet = false, reportRender = false;
            var dumps = new System.Collections.Generic.List<string>();
            var script = new InputScript();
            var shots = new System.Collections.Generic.SortedDictionary<int, string>();
            TrainingHost train = null;
            TrainingHost.Mode trainMode = TrainingHost.Mode.Train;
            bool wantTrain = false;
            int trainEpisodes = 0, trainRepeats = 1, seed = int.MinValue;
            int workers = 1, worker = -1, evals = 1, generations = 0;
            string trainDir = null, resume = null, evalPopulation = null;
            int recycleMb = 2500, controlPort = 0;
            string sessionReport = null;
            string replay = null, parityOut = null, randomGolden = null, seedList = null;
            var evalGenomes = new System.Collections.Generic.List<string>();
            int flights = 12;
            string trainOut = null, trainScenario = null;
            ApplyProjectQuality();
            CosmicShore.Render.RenderQuality.FromEnvironment();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--scene" when i + 1 < args.Length: scene = args[++i]; break;
                    case "--screenshot" when i + 1 < args.Length: screenshot = args[++i]; break;
                    case "--frames" when i + 1 < args.Length: int.TryParse(args[++i], out frames); break;
                    case "--headless": headless = true; break;
                    case "--realtime": s_realtime = true; break;
                    case "--render-from" when i + 1 < args.Length: int.TryParse(args[++i], out PlayerWindow.RenderFrom); break;
                    case "--quiet": quiet = true; break;
                    case "--yaml-roundtrip" when i + 2 < args.Length:
                        return TrainingHost.YamlRoundTrip(args[i + 1], args[i + 2]);
                    case "--train":
                        wantTrain = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                            trainMode = args[++i].ToLowerInvariant() switch
                            {
                                "replay" => TrainingHost.Mode.Replay,
                                "eval" => TrainingHost.Mode.Eval,
                                _ => TrainingHost.Mode.Train,
                            };
                        break;
                    case "--episodes" when i + 1 < args.Length: int.TryParse(args[++i], out trainEpisodes); break;
                    case "--train-out" when i + 1 < args.Length: trainOut = args[++i]; break;
                    case "--scenario" when i + 1 < args.Length: trainScenario = args[++i]; break;
                    case "--repeats" when i + 1 < args.Length: int.TryParse(args[++i], out trainRepeats); break;
                    case "--seed" when i + 1 < args.Length: int.TryParse(args[++i], out seed); break;
                    case "--workers" when i + 1 < args.Length: int.TryParse(args[++i], out workers); break;
                    case "--worker" when i + 1 < args.Length: int.TryParse(args[++i], out worker); break;
                    case "--evals" when i + 1 < args.Length: int.TryParse(args[++i], out evals); break;
                    case "--generations" when i + 1 < args.Length: int.TryParse(args[++i], out generations); break;
                    case "--train-dir" when i + 1 < args.Length: trainDir = args[++i]; break;
                    case "--resume" when i + 1 < args.Length: resume = args[++i]; break;
                    case "--recycle-mb" when i + 1 < args.Length: int.TryParse(args[++i], out recycleMb); break;
                    case "--genome" when i + 1 < args.Length: evalGenomes.Add(args[++i]); break;
                    case "--flights" when i + 1 < args.Length: int.TryParse(args[++i], out flights); break;
                    case "--population" when i + 1 < args.Length: evalPopulation = args[++i]; break;
                    case "--report-render": reportRender = true; break;
                    case "--dump-ui" when i + 1 < args.Length: dumps.Add(args[++i]); break;
                    case "--dump-ui-at" when i + 1 < args.Length: dumps.Add("@" + args[++i]); break;
                    case "--fullscreen": PlayerWindow.StartFullscreen = true; break;
                    case "--check-shaders": PlayerWindow.CheckShaders = true; break;
                    case "--shader-gallery" when i + 1 < args.Length:
                    {
                        var spec = args[++i];
                        int c = spec.IndexOf(':');
                        int.TryParse(c > 0 ? spec[..c] : spec, out PlayerWindow.GalleryFrame);
                        PlayerWindow.GalleryLegend = c > 0 ? spec[(c + 1)..] : null;
                        break;
                    }
                    case "--view-model" when i + 1 < args.Length: ModelViewer.Path = args[++i]; break;
                    case "--hidden": PlayerWindow.StartHidden = true; break;
                    case "--control-port" when i + 1 < args.Length: int.TryParse(args[++i], out controlPort); break;
                    case "--session-report" when i + 1 < args.Length: sessionReport = args[++i]; break;
                    case "--msaa" when i + 1 < args.Length: int.TryParse(args[++i], out CosmicShore.Render.RenderQuality.Msaa); break;
                    case "--render-scale" when i + 1 < args.Length:
                        float.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out CosmicShore.Render.RenderQuality.RenderScale); break;
                    case "--no-vsync": CosmicShore.Render.RenderQuality.VSync = false; break;
                    // --gc-latency low: .NET's SustainedLowLatency (no blocking gen-2 collections while
                    // memory allows), for an A/B of GC pauses against the default Interactive mode.
                    case "--gc-latency" when i + 1 < args.Length:
                        System.Runtime.GCSettings.LatencyMode = args[++i] is "low" or "sustained"
                            ? System.Runtime.GCLatencyMode.SustainedLowLatency : System.Runtime.GCLatencyMode.Interactive;
                        break;
                    case "--fps" when i + 1 < args.Length: int.TryParse(args[++i], out CosmicShore.Render.RenderQuality.TargetFps); break;
                    case "--aniso" when i + 1 < args.Length:
                        float.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out CosmicShore.Render.RenderQuality.Anisotropy); break;
                    case "--verbose": CosmicShore.Utility.CSDebug.VerboseChannels = (CosmicShore.Utility.CSLogChannel)~0; break;
                    case "--do" when i + 1 < args.Length: script.Add(args[++i]); break;
                    case "--replay" when i + 1 < args.Length: replay = args[++i]; break;
                    case "--parity-out" when i + 1 < args.Length: parityOut = args[++i]; break;
                    case "--random-golden" when i + 1 < args.Length: randomGolden = args[++i]; break;
                    case "--seeds" when i + 1 < args.Length: seedList = args[++i]; break;
                    case "--shot" when i + 1 < args.Length:
                    {
                        var spec = args[++i];
                        int c = spec.IndexOf(':');
                        if (c > 0 && int.TryParse(spec[..c], out int f)) shots[f] = spec[(c + 1)..];
                        break;
                    }
                    case "--record" when i + 1 < args.Length:
                        if (!FrameRecorder.TryAdd(args[++i])) Console.WriteLine($"[player] --record expects DIR:FROM-TO[:EVERY], got '{args[i]}'");
                        break;
                    case "--position" when i + 1 < args.Length:
                    {
                        // --position X,Y: where the window opens (the MULTIPLAYER panel tiles its players).
                        var xy = args[++i].Split(',');
                        if (xy.Length == 2 && int.TryParse(xy[0], out int px) && int.TryParse(xy[1], out int py)) PlayerWindow.StartPosition = (px, py);
                        break;
                    }
                    case "--size" when i + 1 < args.Length:
                    {
                        var wh = args[++i].Split('x');
                        if (wh.Length == 2) { int.TryParse(wh[0], out width); int.TryParse(wh[1], out height); }
                        break;
                    }
                }
            }

            if (randomGolden != null)
                return ParityRun.WriteRandomGoldens(randomGolden, (seedList ?? "0").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
            if (replay != null)
            {
                var record = ParityRun.LoadReplay(replay, script, ref scene, ref seed, ref frames);
                if (record != null && parityOut != null && !headless && !FrameRecorder.TryAdd(System.IO.Path.Combine(parityOut, "frames") + ":" + record))
                    Console.WriteLine($"[parity] bad record spec '{record}'");
            }
            if (parityOut != null) ParityRun.Begin(parityOut);

            CosmicShore.Render.RenderQuality.Clamp();
            if (!wantTrain) SessionReport.Begin(sessionReport);

            // A fixed seed makes a run reproducible (UnityEngine.Random otherwise seeds from the clock).
            // A training run always has one: its workers must evolve under the same seed.
            if (wantTrain && trainMode == TrainingHost.Mode.Train && seed == int.MinValue) seed = Environment.TickCount & 0x7FFFFFFF;
            int evoSeed = seed;
            if (seed != int.MinValue) CosmicShore.Engine.Random.InitState(unchecked(seed + Math.Max(0, worker) * 7919));
            try
            {
                if (screenshot != null) shots[frames < 0 ? 180 : frames] = screenshot;
                if (wantTrain)
                {
                    train = new TrainingHost(trainMode, trainEpisodes, trainOut, trainScenario, trainRepeats);
                    if (trainMode == TrainingHost.Mode.Eval) train.ConfigureEval(evalGenomes, flights, evalPopulation);
                    if (trainMode == TrainingHost.Mode.Train)
                    {
                        trainDir ??= System.IO.Path.Combine(trainOut ?? System.IO.Path.Combine(Environment.CurrentDirectory, "training"), "run");
                        if (worker < 0) // the launching process supervises; every worker is a child
                        {
                            if (System.IO.Directory.Exists(trainDir)) System.IO.Directory.Delete(trainDir, recursive: true);
                            System.IO.Directory.CreateDirectory(trainDir);
                            return TrainingWorkers.Supervise(args, Math.Max(1, workers), trainDir, evoSeed);
                        }
                        train.ConfigureParallel(workers, worker, evals, generations, evoSeed, trainDir);
                        train.ConfigureLifecycle(resume, recycleMb);
                    }
                    // Training is headless: a window would only cap the tick rate at vsync.
                    int result = RunHeadless(scene, frames < 0 ? int.MaxValue : frames, quiet, width, height, script, reportRender, dumps, train);
                    return train.Recycle ? TrainingWorkers.RecycleExitCode : result;
                }
                var control = ControlServer.StartIfRequested(controlPort, script);
                if (headless)
                {
                    // Under a control port a headless run lasts until told to quit (or --frames).
                    int count = control != null && frames < 0 ? int.MaxValue : Math.Max(frames < 0 ? 600 : frames, script.LastFrame);
                    using (control) return RunHeadless(scene, count, quiet, width, height, script, reportRender, dumps, control: control);
                }
                int last = Math.Max(shots.Count > 0 ? shots.Keys.Max() : -1, frames);
                last = Math.Max(last, FrameRecorder.LastFrame);
                new PlayerWindow(scene, width, height, shots, last, script) { Control = control }.Run();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("CRASH — " + e);
                SessionReport.Write("crash", e);
                return 2;
            }
            finally { ParityRun.End(); }
        }

        static int RunHeadless(string scene, int frames, bool quiet, int width, int height, InputScript script, bool reportRender, System.Collections.Generic.List<string> dumps, TrainingHost train = null, ControlServer control = null)
        {
            Screen.width = width;
            Screen.height = height;
            using var boot = new PlayerBoot();
            SessionReport.Log = boot.Log;
            boot.Log.Quiet = quiet;
            boot.Headless = true;
            if (!script.IsEmpty || control != null) script.EnsureDevices();
            bool quit = false;
            if (control != null) control.Quit = () => quit = true;
            boot.Start(scene);
            train?.Install(boot.Runtime);
            string lastScene = SceneManager.GetActiveScene().name;
            int frameNow = 0;
            SessionReport.Frame = () => frameNow;
            var pacer = s_realtime && train == null ? new RealtimePacer(1.0 / 60.0) : null;
            for (int f = 0; f < frames && !quit; f++)
            {
                frameNow = f;
                script.BeforeTick(f);
                control?.BeforeTick(f);
                long tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
                if (control is { WantsFrame: true }) control.AfterPresent(_ => throw new InvalidOperationException("a --headless player draws nothing; start it with a window (xvfb-run on a server) to take screenshots"), width, height);
                boot.Tick(1f / 60f);
                ParityRun.AfterTick(f);
                double tickMs = System.Diagnostics.Stopwatch.GetElapsedTime(tickStart).TotalMilliseconds;
                SessionReport.FrameTime(tickMs); // headless: a frame is one simulation tick
                SessionReport.SimTime(tickMs);
                pacer?.AfterTick();
                if (train != null)
                {
                    train.Poll(f);
                    if (train.Done) { frames = f + 1; break; }
                }
                var active = SceneManager.GetActiveScene().name;
                if (active != lastScene)
                {
                    Console.WriteLine($"[player] frame {f}: active scene '{lastScene}' → '{active}'");
                    lastScene = active;
                }
            }
            Console.WriteLine($"[player] {frames} frames, active scene '{lastScene}', {CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Length} live behaviours");
            if (train == null) SessionReport.Write(quit ? "quit" : "frames done");
            boot.Log.PrintSummary();
            if (reportRender) { RenderInventory.PrintNetwork(); RenderInventory.Print(); }
            foreach (var d in dumps) { if (d.StartsWith("@")) UiDump.PrintAt(d[1..]); else UiDump.Print(d); }
            foreach (var (k, n) in boot.Log.Unique.OrderByDescending(kv => kv.Value).Take(40))
                Console.WriteLine($"  ×{n,-4} {k.Replace('\n', ' ')}");
            return 0;
        }
    }
}
