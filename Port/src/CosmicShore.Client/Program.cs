using System;

namespace CosmicShore.Client
{
    /// <summary>
    /// Cosmic Shore port — playable client.
    ///
    ///   [--mode race|freestyle] [--seed N] [--crystals N] [--rivals N]
    ///   [--screenshot out.png [--frames N]]
    ///
    /// RACE (default): SkimRace — closed circuit, persistent skimmable trails, energy
    /// raises top speed; elemental crystals respawn — first to the target wins, time is
    /// the score. Controls: WASD/arrows steer · Space boost · Shift drift · R restart.
    ///
    /// FREESTYLE: the lava lamp — one living Cell (flora, fauna, cytoplasm motes,
    /// membrane), your vessel on autopilot until you take the stick, and the toybox on
    /// the membrane ring (vessel changer, domain changer, fly-by-numbers painting).
    /// No score, no end condition. Controls: Tab / gamepad Y toggles autopilot ↔ flying
    /// · WASD/arrows steer · Space boost · Shift drift · Esc quit.
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            string mode = "race";
            string game = "hexrace";
            string pilot = "ai";
            string ready = "auto";
            string screen = null;
            int seed = 42;
            int crystals = 30;
            int rivals = 3;
            int players = 4;
            int target = 6;
            string screenshot = null;
            int screenshotFrame = 240;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--mode" when i + 1 < args.Length: mode = args[++i].ToLowerInvariant(); break;
                    case "--game" when i + 1 < args.Length: game = args[++i].ToLowerInvariant(); break;
                    case "--pilot" when i + 1 < args.Length: pilot = args[++i].ToLowerInvariant(); break;
                    case "--ready" when i + 1 < args.Length: ready = args[++i].ToLowerInvariant(); break;
                    case "--screen" when i + 1 < args.Length: screen = args[++i].ToLowerInvariant(); break;
                    case "--seed" when i + 1 < args.Length: int.TryParse(args[++i], out seed); break;
                    case "--crystals" when i + 1 < args.Length: int.TryParse(args[++i], out crystals); break;
                    case "--rivals" when i + 1 < args.Length: int.TryParse(args[++i], out rivals); break;
                    case "--players" when i + 1 < args.Length: int.TryParse(args[++i], out players); break;
                    case "--target" when i + 1 < args.Length: int.TryParse(args[++i], out target); break;
                    case "--screenshot" when i + 1 < args.Length: screenshot = args[++i]; break;
                    case "--frames" when i + 1 < args.Length: int.TryParse(args[++i], out screenshotFrame); break;
                }
            }

            try
            {
                if (mode == "uidemo")
                {
                    Console.WriteLine("UI demo — engine canvas rendered through the Arc-C bridge");
                    new UiDemoWindow(screenshot, screenshotFrame).Run();
                    return 0;
                }

                if (mode == "menushell")
                {
                    Console.WriteLine("Menu shell — the REAL ScreenSwitcher on the first-party UI stack");
                    new MenuShellWindow(screenshot, screenshotFrame, screen).Run();
                    return 0;
                }

                if (mode == "play")
                {
                    Console.WriteLine($"Mode host — {game}, the CLI-proven round stepped in a window (seed {seed}, {players} pilots, first domain to {target})");
                    new ModeHostWindow(game, seed, players, target, pilot == "human", ready == "manual", screenshot, screenshotFrame).Run();
                    return 0;
                }

                if (mode == "freestyle")
                {
                    Console.WriteLine($"Freestyle — seed {seed}");
                    Console.WriteLine("Tab (or gamepad Y) toggles autopilot/flying · WASD/arrows steer · Space boost · Shift drift · Esc quit");
                    new FreestyleWindow(seed, screenshot, screenshotFrame).Run();
                    return 0;
                }

                Console.WriteLine($"SkimRace — seed {seed}, crystal target {crystals}, rivals {rivals}");
                Console.WriteLine("WASD/arrows steer · Space boost · Shift drift · R restart · Esc quit");
                new RaceWindow(seed, crystals, rivals, screenshot, screenshotFrame).Run();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("CRASH — please send this text (or skimrace-crash.txt next to the exe):");
                Console.WriteLine(e);
                try
                {
                    string dir = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skimrace-crash.txt"), e.ToString());
                }
                catch { /* log location unavailable — console output still shows it */ }
                if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
                {
                    Console.Write("Press Enter to exit...");
                    Console.ReadLine();
                }
                return 2;
            }
        }
    }
}
