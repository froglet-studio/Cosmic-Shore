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
    ///
    /// --verbose opens every CSDebug log channel (a development build's bring-up traces).
    /// --do scripts input (see <see cref="InputScript"/>); --shot captures extra frames.
    ///
    /// --headless ticks the engine with no window at a fixed 60 Hz for N frames (default
    /// 600) and prints a scene/log summary — the fast loop for chasing boot problems.
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

            try
            {
                if (screenshot != null) shots[frames < 0 ? 180 : frames] = screenshot;
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

        static int RunHeadless(string scene, int frames, bool quiet, int width, int height, InputScript script, bool reportRender, System.Collections.Generic.List<string> dumps)
        {
            Screen.width = width;
            Screen.height = height;
            using var boot = new PlayerBoot();
            boot.Log.Quiet = quiet;
            boot.Headless = true;
            if (!script.IsEmpty) script.EnsureDevices();
            boot.Start(scene);
            string lastScene = SceneManager.GetActiveScene().name;
            for (int f = 0; f < frames; f++)
            {
                script.BeforeTick(f);
                boot.Tick(1f / 60f);
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
