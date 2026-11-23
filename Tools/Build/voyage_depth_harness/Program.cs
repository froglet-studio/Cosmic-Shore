// Runs every [Test] in the compiled voyage suites (exit code = failures), or with `--scorecard [voyages] [galaxies]`
// plays the depth harness headless and prints the scorecard (Docs/Voyages/DEPTH.md section 11).
using System;
using System.Collections.Generic;

public static class Program
{
    public static int Main(string[] args)
    {
        var rest = new List<string>(args);
        int at = rest.IndexOf("--scorecard");
        if (at >= 0)
        {
            int voyages = at + 1 < rest.Count && int.TryParse(rest[at + 1], out int v) ? v : 40;
            int galaxies = at + 2 < rest.Count && int.TryParse(rest[at + 2], out int g) ? g : 200;
            int c = rest.IndexOf("--content");
            var content = c >= 0 && c + 1 < rest.Count
                ? CosmicShore.Tests.VoyageDepthHarness.LoadContent(rest[c + 1])
                : CosmicShore.Tests.VoyageDepthHarness.LoadShippedContent();
            var card = CosmicShore.Tests.VoyageDepthHarness.Play(content, voyages, galaxies);
            Console.WriteLine(card.Report());
            return card.Failures().Count;
        }
        return new NUnitLite.AutoRun(typeof(Program).Assembly).Execute(args);
    }
}
