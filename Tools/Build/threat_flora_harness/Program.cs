// The threat-flora harness (round 11c, Docs/THREAT_FLORA.md §6): the SHIPPED pure-C# cores SnapTrapCore and
// PhysarumCore, run headless against the research's numbers, every claim ASSERTED. Exit code = failures.
//   bash Tools/Build/threat_flora_harness/run.sh            # everything (~1-2 min)
//   bash Tools/Build/threat_flora_harness/run.sh snap       # the snap trap only
//   bash Tools/Build/threat_flora_harness/run.sh physarum   # the physarum network only
//   bash Tools/Build/threat_flora_harness/run.sh quick      # one seed where the research used three
//   bash Tools/Build/threat_flora_harness/run.sh emotion <jobs.txt>   # the emotion-probe export (EmotionFlora.cs, SWARM_FAUNA.md §27)
using System;
using System.Linq;

public static partial class Program
{
    static int _fails, _passes;

    public static void Check(bool ok, string what)
    {
        if (ok) { _passes++; Console.WriteLine("   ok    " + what); }
        else { _fails++; Console.WriteLine("   FAIL  " + what); }
    }

    public static void Near(string what, float value, float target, float tol) =>
        Check(Math.Abs(value - target) <= tol, $"{what}: {value:F3} vs {target:F3} (+-{tol:F3})");

    public static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == "emotion") return Emotion(args[1]);   // round 11d-2 (SWARM_FAUNA.md §27): the emotion-probe export
        bool quick = args.Contains("quick");
        bool snap = args.Contains("snap") || !args.Contains("physarum");
        bool phys = args.Contains("physarum") || !args.Contains("snap");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (snap) SnapTrapTests.Run();
        if (phys) PhysarumTests.Run(quick);
        Console.WriteLine($"\n{_passes} passed, {_fails} failed ({sw.Elapsed.TotalSeconds:F0} s)");
        Console.WriteLine(_fails == 0 ? "OK" : "FAIL");
        return _fails;
    }
}
