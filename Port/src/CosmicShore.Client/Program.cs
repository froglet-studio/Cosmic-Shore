using System;

namespace CosmicShore.Client
{
    /// <summary>
    /// SkimRace — Cosmic Shore port, playable slice.
    ///
    ///   SkimRace [--seed N] [--crystals N] [--screenshot out.png [--frames N]]
    ///
    /// Controls: WASD / arrows steer · Space boost · R restart · Esc quit.
    /// Rules (HexRace): collect the crystal target; your time is your score.
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            int seed = 42;
            int crystals = 30;
            string screenshot = null;
            int screenshotFrame = 240;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seed" when i + 1 < args.Length: int.TryParse(args[++i], out seed); break;
                    case "--crystals" when i + 1 < args.Length: int.TryParse(args[++i], out crystals); break;
                    case "--screenshot" when i + 1 < args.Length: screenshot = args[++i]; break;
                    case "--frames" when i + 1 < args.Length: int.TryParse(args[++i], out screenshotFrame); break;
                }
            }

            Console.WriteLine($"SkimRace — seed {seed}, crystal target {crystals}");
            Console.WriteLine("WASD/arrows steer · Space boost · R restart · Esc quit");

            try
            {
                new RaceWindow(seed, crystals, screenshot, screenshotFrame).Run();
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
