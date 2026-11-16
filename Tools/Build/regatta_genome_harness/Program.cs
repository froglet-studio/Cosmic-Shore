// The Regatta course genome, run through the GAME'S OWN CODE with no editor: Amoebius compiles Assets/_Scripts, this
// harness drives it headless. Four jobs (run.sh wraps them):
//
//   measure <draft.json> <out.json>      every catalogue kind generated at its defaults (and with every number at its
//                                        high end): prisms, bounding radius, finest prism, milliseconds, or the failure
//   lay     <genome.json> <out.json>     lay one genome in a real SpawnableRegattaRails: rings, rails, every placement
//                                        prism, the reports (what the stills and the judge's logs read)
//   repair  <in.jsonl> <out.jsonl>       RegattaCourseGenome.Repair on each line (the Python mirror's parity fixture)
//   check   <genomes.jsonl>              the invariants: the empty genome lays today's course prism for prism, and in
//                                        every genome no laid placement prism breaks a carve rule (brute force)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;

static class Program
{
    static string Root => Environment.GetEnvironmentVariable("COSMIC_SHORE_PROJECT") ?? ".";
    static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string F3(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);

    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length < 1) { Console.Error.WriteLine("usage: measure|lay|repair|check ..."); return 2; }
        using var loop = new GameLoop("RegattaGenomeHarness");
        switch (args[0])
        {
            case "measure": return Measure(args[1], args[2]);
            case "lay": return Lay(args[1], args[2]);
            case "repair": return RepairAll(args[1], args[2]);
            case "check": return Check(args[1]);
        }
        Console.Error.WriteLine($"unknown job {args[0]}");
        return 2;
    }

    static RegattaGeneCatalogue Catalogue(string path = null)
    {
        path ??= Path.Combine(Root, "Assets", "Resources", "Regatta", "RegattaGeneCatalogue.json");
        var cat = RegattaGeneCatalogue.Parse(File.ReadAllText(path));
        RegattaGeneCatalogue.Shipped = cat;
        return cat;
    }

    // ── measure ────────────────────────────────────────────────────────────────────────────────────────────────

    static int Measure(string draftPath, string outPath)
    {
        // The draft lists every scanned kind as usable-or-not from the code alone; measure every one the code allows.
        var cat = RegattaGeneCatalogue.Parse(File.ReadAllText(draftPath));
        var sb = new StringBuilder();
        sb.Append("{\"engine\": \"amoebius\", \"kinds\": [\n");
        bool first = true;
        foreach (var kind in cat.Kinds)
        {
            if (!kind.Usable) continue;
            var line = new List<string> { $"\"id\": \"{kind.Id}\"" };
            var (n, r, ax, ms, fail) = Gen(kind, new RegattaPlacement { Kind = kind.Id });
            if (fail != null) line.Add($"\"failure\": \"{fail.Replace("\"", "'").Replace("\\", "/")}\"");
            else line.AddRange(new[] { $"\"prisms\": {n}", $"\"radius\": {F3(r)}", $"\"min_axis\": {F3(ax)}",
                                       $"\"min_axis_p5\": {F3(_p5)}", $"\"ms\": {F3(ms)}" });
            // Every number at its high end: the most a gene can ask of this kind.
            var hi = new RegattaPlacement { Kind = kind.Id, Density = 2f };
            foreach (var p in kind.Params)
                if (p.Type == RegattaGeneCatalogue.ParamType.Float || p.Type == RegattaGeneCatalogue.ParamType.Int)
                    hi.Params[p.Name] = p.Hi;
            var (nh, rh, axh, msh, failh) = fail == null ? Gen(kind, hi) : (0, 0f, 0f, 0.0, null);
            if (fail == null)
            {
                if (failh != null) line.Add($"\"failure_hi\": \"{failh.Replace("\"", "'").Replace("\\", "/")}\"");
                else line.AddRange(new[] { $"\"prisms_hi\": {nh}", $"\"radius_hi\": {F3(rh)}", $"\"ms_hi\": {F3(msh)}" });
            }
            sb.Append(first ? "  {" : ",\n  {").Append(string.Join(", ", line)).Append('}');
            first = false;
            Console.Error.WriteLine($"  {kind.Id,-18} {(fail ?? $"{n} prisms r={r:F0} min={ax:F2} {ms:F0} ms | hi {nh} prisms {msh:F0} ms {failh}")}");
        }
        sb.Append("\n]}\n");
        File.WriteAllText(outPath, sb.ToString());
        return 0;
    }

    static float _p5;

    static (int n, float r, float ax, double ms, string fail) Gen(RegattaGeneCatalogue.Kind kind, RegattaPlacement p)
    {
        var lays = new List<PrismLay>();
        var sw = Stopwatch.StartNew();
        bool ok = RegattaPlacementBuilder.TryGenerateNative(kind, p, lays, out var failure);
        double ms = sw.Elapsed.TotalMilliseconds;
        if (!ok) return (0, 0, 0, ms, failure);
        RegattaPlacementBuilder.Bounds(lays, out _, out float r);
        var axes = lays.Select(l => Math.Min(Math.Abs(l.Point.Scale.x), Math.Min(Math.Abs(l.Point.Scale.y), Math.Abs(l.Point.Scale.z))))
                       .OrderBy(x => x).ToList();
        float ax = axes.Count > 0 ? axes[0] : 0f;
        _p5 = axes.Count > 0 ? axes[Math.Min(axes.Count - 1, axes.Count / 20)] : 0f;
        return (lays.Count, r, lays.Count > 0 ? ax : 0f, ms, null);
    }

    // ── lay ────────────────────────────────────────────────────────────────────────────────────────────────────

    sealed class Laid
    {
        public SpawnableRegattaRails Arena;
        public List<PrismLay> Lays;
        public List<RaceGate> Gates;
        public RegattaRailLayout Layout;
    }

    static Laid LayArena(RegattaCourseGenome genome)
    {
        RegattaCourseGenome.Override = genome;
        var go = new GameObject("RegattaArena");
        go.SetActive(false);
        var arena = go.AddComponent<SpawnableRegattaRails>();
        arena.InvalidateCache();
        arena.GetTrailData();
        var lays = arena.CachedLays.ToList();
        var gates = arena.BuildGatesNow();
        var layout = RegattaCourse.BuildRails(gates, arena.RailSettings, arena.Features);
        var laid = new Laid { Arena = arena, Lays = lays, Gates = gates, Layout = layout };
        RegattaCourseGenome.Override = null;
        return laid;
    }

    static int Lay(string genomePath, string outPath)
    {
        var cat = Catalogue();
        var raw = RegattaCourseGenome.FromJson(File.ReadAllText(genomePath));
        var notes = new List<string>();
        var genome = raw.Repair(cat, notes);
        var sw = Stopwatch.StartNew();
        var laid = LayArena(genome);
        double ms = sw.Elapsed.TotalMilliseconds;
        int railCount = laid.Layout.PrismCount;
        var sb = new StringBuilder();
        sb.Append("{\"ms\": ").Append(F3(ms)).Append(", \"genome\": ").Append(genome.ToJson());
        sb.Append(", \"notes\": [").Append(string.Join(", ", notes.Select(n => "\"" + n.Replace("\"", "'") + "\""))).Append(']');
        sb.Append(", \"gates\": [");
        sb.Append(string.Join(", ", laid.Gates.Select(g =>
            $"{{\"p\": [{F3(g.Position.x)}, {F3(g.Position.y)}, {F3(g.Position.z)}], \"a\": [{F3(g.Axis.x)}, {F3(g.Axis.y)}, {F3(g.Axis.z)}], \"r\": {F3(g.Radius)}}}")));
        sb.Append("], \"reports\": [");
        sb.Append(string.Join(", ", laid.Arena.PlacementReports.Select(r =>
            r.Failure != null
                ? $"{{\"kind\": \"{r.Kind}\", \"failure\": \"{r.Failure.Replace("\"", "'")}\"}}"
                : $"{{\"kind\": \"{r.Kind}\", \"native\": {r.Native}, \"laid\": {r.Laid}, \"thinned\": {r.Thinned}, " +
                  $"\"carved\": {{\"line\": {r.Carved[1]}, \"ring\": {r.Carved[2]}, \"rail\": {r.Carved[3]}, \"shell\": {r.Carved[4]}, \"overlap\": {r.Carved[5]}, \"fine\": {r.Carved[6]}}}}}")));
        sb.Append("], \"rail_prisms\": ").Append(railCount);
        sb.Append(", \"prisms\": [");
        // Each laid prism: x, y, z, size (longest axis), domain, kind, group (-1 = rails, else placement index).
        int group = -1, groupLeft = railCount;
        var counts = laid.Arena.PlacementReports.Where(r => r.Failure == null && r.Laid > 0).Select(r => r.Laid).ToList();
        int gi = 0;
        for (int i = 0; i < laid.Lays.Count; i++)
        {
            while (groupLeft <= 0 && gi < counts.Count) { group = gi; groupLeft = counts[gi++]; }
            var l = laid.Lays[i];
            float size = Math.Max(l.Point.Scale.x, Math.Max(l.Point.Scale.y, l.Point.Scale.z));
            if (i > 0) sb.Append(", ");
            sb.Append('[').Append(F3(l.Point.Position.x)).Append(", ").Append(F3(l.Point.Position.y)).Append(", ")
              .Append(F3(l.Point.Position.z)).Append(", ").Append(F3(size)).Append(", ").Append((int)l.Domain).Append(", ")
              .Append((int)l.Kind).Append(", ").Append(group).Append(']');
            groupLeft--;
        }
        sb.Append("], \"sites\": ").Append(laid.Arena.SownSiteCount).Append("}\n");
        File.WriteAllText(outPath, sb.ToString());
        Console.Error.WriteLine($"laid {laid.Lays.Count} prisms ({railCount} rail) in {ms:F0} ms: " +
                                string.Join("; ", laid.Arena.PlacementReports.Select(r => r.ToString())));
        return 0;
    }

    // ── repair ─────────────────────────────────────────────────────────────────────────────────────────────────

    static int RepairAll(string inPath, string outPath)
    {
        var cat = Catalogue();
        var outLines = new List<string>();
        foreach (var line in File.ReadAllLines(inPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var notes = new List<string>();
            var g = RegattaCourseGenome.FromJson(line).Repair(cat, notes);
            outLines.Add($"{{\"genome\": {g.ToJson()}, \"notes\": {notes.Count}}}");
        }
        File.WriteAllLines(outPath, outLines);
        return 0;
    }

    // ── check ──────────────────────────────────────────────────────────────────────────────────────────────────

    static int Check(string genomesPath)
    {
        var cat = Catalogue();
        int failures = 0;

        // 1. The empty genome is today's course, prism for prism.
        var plain = LayArena(null);
        var empty = LayArena(new RegattaCourseGenome());
        bool same = plain.Lays.Count == empty.Lays.Count;
        for (int i = 0; same && i < plain.Lays.Count; i++)
        {
            var a = plain.Lays[i];
            var b = empty.Lays[i];
            same = a.Point.Position == b.Point.Position && a.Point.Rotation == b.Point.Rotation
                   && a.Point.Scale == b.Point.Scale && a.Domain == b.Domain && a.Kind == b.Kind;
        }
        Console.WriteLine($"{(same ? "pass" : "FAIL")}  empty genome lays today's course ({plain.Lays.Count} prisms)");
        if (!same) failures++;

        // 2. Every genome: no laid placement prism breaks a carve rule (brute force, no grid).
        int n = 0, prisms = 0, kinds = 0;
        var seenKinds = new HashSet<string>();
        foreach (var line in File.ReadAllLines(genomesPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var genome = RegattaCourseGenome.FromJson(line).Repair(cat, null);
            var laid = LayArena(genome);
            var bad = BruteForceViolations(laid, genome);
            n++;
            int placed = laid.Lays.Count - laid.Layout.PrismCount;
            prisms += placed;
            foreach (var r in laid.Arena.PlacementReports)
                if (r.Failure == null && r.Laid > 0) seenKinds.Add(r.Kind);
                else if (r.Failure != null) { Console.WriteLine($"FAIL  genome {n}: {r}"); failures++; }
            if (placed > RegattaCourseGenome.TotalPrismBudget) { Console.WriteLine($"FAIL  genome {n}: {placed} placement prisms over the budget"); failures++; }
            if (bad != null) { Console.WriteLine($"FAIL  genome {n}: {bad}"); failures++; }
        }
        kinds = seenKinds.Count;
        Console.WriteLine($"{(failures == 0 ? "pass" : "FAIL")}  {n} genomes laid, {prisms} placement prisms, {kinds} kinds laid, " +
                          "none in the line tube, a mouth, a rail, the nucleus or the membrane");
        return failures == 0 ? 0 : 1;
    }

    static string BruteForceViolations(Laid laid, RegattaCourseGenome genome)
    {
        int railCount = laid.Layout.PrismCount;
        var spine = laid.Layout.SpinePositions;
        var rails = laid.Layout.LanePositions.SelectMany(x => x).ToList();
        var rings = new List<RaceGate>(laid.Gates);
        for (int g = 0; g < laid.Gates.Count; g++)
            if (laid.Arena.Features.TryAlternate(laid.Gates, g, out var alt)) rings.Add(alt);
        for (int i = railCount; i < laid.Lays.Count; i++)
        {
            var l = laid.Lays[i];
            var p = l.Point.Position;
            float r = RegattaPlacementCarve.RadiusOf(l.Point.Scale);
            float d = p.magnitude;
            if (d - r < RegattaCourseGenome.NucleusCarve - 0.01f || d + r > RegattaCourseGenome.MembraneCarve + 0.01f)
                return $"prism {i} at {d:F1} u from the centre breaks the shell";
            float best = float.MaxValue;
            for (int j = 0; j < spine.Length; j++)
            {
                var a = spine[j];
                var b = spine[(j + 1) % spine.Length];
                var ab = b - a;
                float t = Math.Clamp(Vector3.Dot(p - a, ab) / Math.Max(1e-8f, Vector3.Dot(ab, ab)), 0f, 1f);
                best = Math.Min(best, (p - (a + ab * t)).magnitude);
            }
            if (best < genome.LineClearance + r - 0.01f) return $"prism {i} is {best:F1} u from the racing line (tube {genome.LineClearance})";
            foreach (var q in rails)
                if ((q - p).magnitude < RegattaPlacementCarve.RailRadius + r - 0.01f) return $"prism {i} touches a rail";
            foreach (var g in rings)
            {
                var ax = g.Axis.normalized;
                var v = p - g.Position;
                float along = Vector3.Dot(v, ax);
                if (Math.Abs(along) < RegattaPlacementCarve.RingSlab + r - 0.01f
                    && (v - ax * along).magnitude < g.Radius + RegattaPlacementCarve.RingMargin + r - 0.01f)
                    return $"prism {i} is in a ring's mouth";
            }
        }
        return null;
    }
}
