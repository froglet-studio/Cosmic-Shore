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
    ///   CosmicShore --headless [--frames N] [--scene NAME] [--quiet] [--do FRAME:ACTION]...
    ///   CosmicShore --train [train|replay|eval] [--episodes N] [--repeats K] [--scenario NAME] [--train-out DIR]
    ///               [--seed S] [--frames CAP] [--workers N] [--evals K] [--generations G] [--recycle-mb MB]
    ///
    /// --verbose opens every CSDebug log channel (a development build's bring-up traces).
    /// --do scripts input (see <see cref="InputScript"/>); --shot captures extra frames.
    ///
    /// --headless ticks the engine with no window at a fixed 60 Hz for N frames (default
    /// 600) and prints a scene/log summary — the fast loop for chasing boot problems.
    ///
    /// --train runs the game's own AI genetic training headless (see <see cref="TrainingHost"/>);
    /// "replay" re-scores the generation the session asset scored in Unity and reports the disparity.
    /// "train" evolves generation by generation over --workers processes (one per core), each
    /// genome flown --evals times per generation. "eval" flies the --genome FILE(s) --flights
    /// times each, racing each other, and reports each one's mean fitness and standard error.
    /// </summary>
    public static class Program
    {
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
            int recycleMb = 2500;
            var evalGenomes = new System.Collections.Generic.List<string>();
            int flights = 12;
            string trainOut = null, trainScenario = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--scene" when i + 1 < args.Length: scene = args[++i]; break;
                    case "--screenshot" when i + 1 < args.Length: screenshot = args[++i]; break;
                    case "--frames" when i + 1 < args.Length: int.TryParse(args[++i], out frames); break;
                    case "--headless": headless = true; break;
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
                    case "--verbose": CosmicShore.Utility.CSDebug.VerboseChannels = (CosmicShore.Utility.CSLogChannel)~0; break;
                    case "--do" when i + 1 < args.Length: script.Add(args[++i]); break;
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
                    case "--size" when i + 1 < args.Length:
                    {
                        var wh = args[++i].Split('x');
                        if (wh.Length == 2) { int.TryParse(wh[0], out width); int.TryParse(wh[1], out height); }
                        break;
                    }
                }
            }

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
                if (headless) return RunHeadless(scene, Math.Max(frames < 0 ? 600 : frames, script.LastFrame), quiet, width, height, script, reportRender, dumps);
                int last = Math.Max(shots.Count > 0 ? shots.Keys.Max() : -1, frames);
                last = Math.Max(last, FrameRecorder.LastFrame);
                new PlayerWindow(scene, width, height, shots, last, script).Run();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("CRASH — " + e);
                return 2;
            }
        }

        static int RunHeadless(string scene, int frames, bool quiet, int width, int height, InputScript script, bool reportRender, System.Collections.Generic.List<string> dumps, TrainingHost train = null)
        {
            Screen.width = width;
            Screen.height = height;
            using var boot = new PlayerBoot();
            boot.Log.Quiet = quiet;
            boot.Headless = true;
            if (!script.IsEmpty) script.EnsureDevices();
            boot.Start(scene);
            train?.Install(boot.Runtime);
            string lastScene = SceneManager.GetActiveScene().name;
            for (int f = 0; f < frames; f++)
            {
                script.BeforeTick(f);
                boot.Tick(1f / 60f);
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
            boot.Log.PrintSummary();
            if (reportRender) { RenderInventory.PrintNetwork(); RenderInventory.Print(); }
            foreach (var d in dumps) UiDump.Print(d);
            foreach (var (k, n) in boot.Log.Unique.OrderByDescending(kv => kv.Value).Take(40))
                Console.WriteLine($"  ×{n,-4} {k.Replace('\n', ' ')}");
            return 0;
        }
    }
}
