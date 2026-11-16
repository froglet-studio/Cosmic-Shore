// Runs every [Test] of the compiled SHIPPED Thornline tests (ThornlineCourseTests) by reflection and prints a pass/fail
// line per test. Exit 0 = all passed. One more mode reads the shipped C#, never a copy:
//   --sweep   the ladder per intensity over 400 seeds: legs, bends, snipes, plain thorns, crystals, and the race clock
//             of three pilots (dock every thorn, take the snipes, take the snipes and the crystals) at Time 0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--sweep") { Sweep(); return 0; }
        if (args.Length > 0 && args[0] == "--ai") return AiSweep(args);

        int pass = 0, fail = 0;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes().Where(t => t.Namespace == "CosmicShore.Tests").OrderBy(t => t.Name))
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                try { m.Invoke(Activator.CreateInstance(type), null); pass++; Console.WriteLine($"PASS {type.Name}.{m.Name}"); }
                catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 && pass > 0 ? 0 : 1;
    }

    /// <summary>The race clock of a pilot who slings the moment the cooldown is up (aim time not counted). Time rises
    /// one level per crystal; a crystal resets the cooldown.</summary>
    static float Clock(List<Thorn> thorns, in ThornlineSettings s, bool snipes, bool crystals, List<Vector3> crystalAt,
                       List<Vector3> thornAt)
    {
        float t = ThornlineCourse.SlingSeconds;   // the sling to the start thorn
        float time = 0f;
        int k = 0;
        var crystalLegs = new HashSet<int>();
        foreach (var c in crystalAt)
        {
            int best = 1; float bd = float.MaxValue;
            for (int j = 1; j < thornAt.Count; j++)
            {
                float d = ThornlineCourse.DistanceToSegment(c, thornAt[j - 1], thornAt[j]);
                if (d < bd) { bd = d; best = j; }
            }
            crystalLegs.Add(best);
        }
        bool reset = false;
        while (k < thorns.Count - 1)
        {
            int to = snipes && ThornlineCourse.IsSkipBend(k + 1, s.RingCount, s.SkipBends) ? k + 2 : k + 1;
            float cd = ThornlineCourse.CooldownAtTime(time);
            // docked on thorn k, waiting out the cooldown of the sling that brought us here
            float wait = reset ? 0f : Mathf.Max(0f, cd - ThornlineCourse.SlingSeconds) /
                (thorns[k].Kind == PrismKind.SuperShielded ? ThornlineCourse.DockBonusShielded : ThornlineCourse.DockBonus);
            t += wait + ThornlineCourse.SlingSeconds;
            reset = crystals && to == k + 1 && crystalLegs.Contains(to);
            if (reset) time += 1f;
            k = to;
        }
        return t;
    }

    /// <summary>
    /// run.sh --ai [--seeds N] [--pack] [--fps F] [--lead e,m,h,l] [--follow e,m,h,l] [--extra deg]
    ///             [--centre] [--norescue] [--lance R] [--check]
    /// AI Shrikes race whole courses through the shipped plan and aim law (RaceReplica). Solo: one AI per race, every
    /// difficulty on the same seeds. --pack: the four difficulties race together (trails, wakes, the lance). --check
    /// exits 1 unless every claim of THORNLINE.md section 4 holds (the self-test runs it and its negative controls).
    /// </summary>
    static int AiSweep(string[] args)
    {
        int seeds = 100; bool pack = false, check = false;
        float fps = 60f, lance = RaceReplica.LanceRadiusCharge0;
        float[] lead = null, follow = null;
        bool centre = false, norescue = false;
        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            float[] F(string v) => v.Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            if (a == "--seeds") seeds = int.Parse(args[++i]);
            else if (a == "--pack") pack = true;
            else if (a == "--check") check = true;
            else if (a == "--fps") fps = float.Parse(args[++i], CultureInfo.InvariantCulture);
            else if (a == "--lead") lead = F(args[++i]);
            else if (a == "--follow") follow = F(args[++i]);
            else if (a == "--centre") centre = true;
            else if (a == "--norescue") norescue = true;
            else if (a == "--lance") lance = float.Parse(args[++i], CultureInfo.InvariantCulture);
            else if (a == "--extra") ThornlineAIPlan.NoseLeadMaxExtraDegrees = float.Parse(args[++i], CultureInfo.InvariantCulture);
            else if (a == "--trace")
            {
                int ti = int.Parse(args[++i]), ts = int.Parse(args[++i]), td = int.Parse(args[++i]);
                var tr = new RaceReplica { Dt = 1f / fps, LanceRadius = lance, Trace = true };
                tr.Lay(ts, ti);
                tr.Seat(new[] { ThornlineAIPlan.RowFor(td, lead, follow) });
                var res = tr.Run()[0];
                Console.WriteLine($"finish {res.Finish:F1} threaded {res.Threaded}");
                return 0;
            }
        }
        string[] names = { "Easy", "Medium", "Hard", "Legendary" };
        var fails = new List<string>();
        var medians = new float[5, 5];
        for (int intensity = 1; intensity <= 4; intensity++)
        {
            var times = new List<float>[4]; var over = new List<float>[4];
            var finished = new int[4]; var space = new float[4]; var rescues = new int[4]; var detach = new int[4];
            var snipes = new int[4]; var crystals = new int[4]; var wakes = new int[4]; var slings = new int[4];
            for (int d = 0; d < 4; d++) { times[d] = new List<float>(); over[d] = new List<float>(); }
            for (int seed = 1; seed <= seeds; seed++)
            {
                var results = new RaceReplica.Result[4];
                if (pack)
                {
                    var rep = Make(seed, intensity);
                    rep.Seat(Enumerable.Range(1, 4).Select(l => ThornlineAIPlan.RowFor(l, lead, follow)).ToList());
                    var all = rep.Run();
                    for (int d = 0; d < 4; d++) results[d] = all[d];
                }
                else
                    for (int d = 0; d < 4; d++)
                    {
                        var rep = Make(seed, intensity);
                        rep.Seat(new[] { ThornlineAIPlan.RowFor(d + 1, lead, follow) });
                        results[d] = rep.Run()[0];
                    }
                for (int d = 0; d < 4; d++)
                {
                    var r = results[d];
                    if (r.Finish >= 0f) { finished[d]++; times[d].Add(r.Finish); }
                    else Console.WriteLine($"DNF I{intensity} {names[d]} seed {seed}: {r.Threaded} rings");
                    if (r.Slings > 0) over[d].Add(r.OverheadSeconds / r.Slings);
                    space[d] += r.SpaceSeconds; rescues[d] += r.Rescues; detach[d] += r.Detaches;
                    snipes[d] += r.Snipes; crystals[d] += r.Crystals; wakes[d] += r.WakeDocks; slings[d] += r.Slings;
                }
            }
            for (int d = 0; d < 4; d++)
            {
                float med = RaceReplica.Median(times[d]);
                medians[intensity, d + 1] = med;
                float p90 = times[d].Count > 0 ? times[d].OrderBy(x => x).ElementAt(Math.Min(times[d].Count - 1, (int)(times[d].Count * 0.9f))) : float.NaN;
                Console.WriteLine($"I{intensity} {names[d],-9} finished {finished[d],4}/{seeds}  median {med,6:F1} s  p90 {p90,6:F1} s  " +
                                  $"aim over reload {RaceReplica.Median(over[d]),5:F2} s/sling  lance {space[d] / seeds,5:F1} s/race  " +
                                  $"rescues {rescues[d] / (float)seeds:F2}  detaches {detach[d] / (float)seeds:F1}  snipes {snipes[d] / (float)seeds:F1}  " +
                                  $"crystals {crystals[d] / (float)seeds:F1}  wake docks {wakes[d] / (float)Math.Max(1, slings[d]) * 100f:F1}%");
                if (finished[d] < seeds) fails.Add($"I{intensity} {names[d]}: {finished[d]}/{seeds} finished");
                if (!(med <= RungBar[intensity])) fails.Add($"I{intensity} {names[d]}: median {med:F1} s over the rung bar {RungBar[intensity]} s");
            }
            for (int d = 1; d < 4; d++)
                if (!(medians[intensity, d + 1] < medians[intensity, d] * (1f - OrderGap)))
                    fails.Add($"I{intensity}: {names[d]} ({medians[intensity, d + 1]:F1} s) not {OrderGap * 100:F0}% under {names[d - 1]} ({medians[intensity, d]:F1} s)");
        }
        foreach (var f in fails) Console.WriteLine("CLAIM FAILS: " + f);
        Console.WriteLine(fails.Count == 0 ? "every AI claim holds" : $"{fails.Count} AI claim(s) fail");
        return check && fails.Count > 0 ? 1 : 0;

        RaceReplica Make(int seed, int intensity)
        {
            var rep = new RaceReplica { Dt = 1f / fps, LanceRadius = lance, CentreAim = centre, NoRescue = norescue };
            rep.Lay(seed, intensity);
            return rep;
        }
    }

    /// <summary>The rung bars (rung_bars.json's ceilings: an AI that cannot beat these is not racing), seconds.</summary>
    static readonly float[] RungBar = { 0f, 240f, 300f, 360f, 420f };

    /// <summary>Each harder difficulty's median is at least this much faster than the one below it.</summary>
    const float OrderGap = 0.05f;

    static void Sweep()
    {
        var thorns = new List<Vector3>();
        var laid = new List<Thorn>();
        var crystals = new List<Vector3>();
        for (int i = 1; i <= 4; i++)
        {
            var s = ThornlineCourse.ForIntensity(i);
            var legs = new List<float>(); var bends = new List<float>(); var spans = new List<float>();
            var dock = new List<float>(); var snipe = new List<float>(); var read = new List<float>();
            int fails = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                var rings = ThornlineCourse.Generate(seed, s, thorns);
                if (rings == null) { fails++; continue; }
                for (int k = 1; k < thorns.Count; k++)
                {
                    legs.Add((thorns[k] - thorns[k - 1]).magnitude);
                    if (k + 1 < thorns.Count)
                        bends.Add(Vector3.Angle(thorns[k] - thorns[k - 1], thorns[k + 1] - thorns[k]));
                    if (ThornlineCourse.IsSkipBend(k, s.RingCount, s.SkipBends))
                        spans.Add((thorns[k + 1] - thorns[k - 1]).magnitude);
                }
                ThornlineCourse.Lay(thorns, s, laid);
                ThornlineCourse.PlaceCrystals(thorns, s, crystals);
                dock.Add(Clock(laid, s, false, false, crystals, thorns));
                snipe.Add(Clock(laid, s, true, false, crystals, thorns));
                read.Add(Clock(laid, s, true, true, crystals, thorns));
            }
            float Med(List<float> xs) { if (xs.Count == 0) return 0f; var o = xs.OrderBy(x => x).ToList(); return o[o.Count / 2]; }
            Console.WriteLine($"I{i}: fails {fails}/400  legs {legs.Min():F0}-{legs.Max():F0} (med {Med(legs):F0})  " +
                              $"bends med {Med(bends):F0} max {bends.DefaultIfEmpty().Max():F0}  snipes {s.SkipBends} span med {Med(spans):F0}  " +
                              $"thorn {s.ThornSize} ring {s.RingRadius} plain every {s.PlainEvery} crystals {s.Crystals}");
            Console.WriteLine($"     clock at Time 0 (aim not counted): dock every thorn {Med(dock):F1} s, take the snipes {Med(snipe):F1} s, " +
                              $"snipes + crystals {Med(read):F1} s");
        }
    }
}
