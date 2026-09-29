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
    ///   CosmicShore --headless [--frames N] [--scene NAME] [--quiet]
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
            bool headless = false, quiet = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--scene" when i + 1 < args.Length: scene = args[++i]; break;
                    case "--screenshot" when i + 1 < args.Length: screenshot = args[++i]; break;
                    case "--frames" when i + 1 < args.Length: int.TryParse(args[++i], out frames); break;
                    case "--headless": headless = true; break;
                    case "--quiet": quiet = true; break;
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
                if (headless) return RunHeadless(scene, frames < 0 ? 600 : frames, quiet, width, height);
                new PlayerWindow(scene, width, height, screenshot, frames < 0 ? 180 : frames).Run();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("CRASH — " + e);
                return 2;
            }
        }

        static int RunHeadless(string scene, int frames, bool quiet, int width, int height)
        {
            Screen.width = width;
            Screen.height = height;
            using var boot = new PlayerBoot();
            boot.Log.Quiet = quiet;
            boot.Start(scene);
            string lastScene = SceneManager.GetActiveScene().name;
            for (int f = 0; f < frames; f++)
            {
                boot.Loop.Tick(1f / 60f);
                var active = SceneManager.GetActiveScene().name;
                if (active != lastScene)
                {
                    Console.WriteLine($"[player] frame {f}: active scene '{lastScene}' → '{active}'");
                    lastScene = active;
                }
            }
            Console.WriteLine($"[player] {frames} frames, active scene '{lastScene}', {CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Length} live behaviours");
            boot.Log.PrintSummary();
            foreach (var (k, n) in boot.Log.Unique.OrderByDescending(kv => kv.Value).Take(40))
                Console.WriteLine($"  ×{n,-4} {k.Replace('\n', ' ')}");
            return 0;
        }
    }
}
