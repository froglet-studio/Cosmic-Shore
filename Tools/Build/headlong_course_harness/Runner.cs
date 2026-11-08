// Runs every [Test] of the compiled SHIPPED Headlong tests (HeadlongHedgesTests) by reflection and prints a pass/fail
// line per test. Exit 0 = all passed. Two more modes read the shipped C#, never a copy:
//   --export <intensity> <seed>...   one JSON line per course: its gates, its hedges (with every slab's place and
//                                    kind folded into a count) and its Time crystals - what
//                                    Tools/Build/headlong_stampede_model.py races
//   --sweep                          the stampede ladder per intensity over 400 seeds: hedges, danger, crystals, prisms
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    const float Inner = 480f, Outer = 1080f;

    static List<RaceGate> Circuit(int intensity, int seed)
    {
        var s = HeadlongCircuitSettings.ForIntensity(intensity);
        s.InnerRadius = Inner;
        s.OuterRadius = Outer;
        s.FirstGateDirection = Vector3.up;
        return HeadlongCircuit.Generate(seed, s);
    }

    static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--export")
        {
            int intensity = int.Parse(args[1]);
            var hedges = new List<HeadlongHedges.Hedge>();
            var crystals = new List<Vector3>();
            var parts = new List<HeadlongHedges.Part>();
            foreach (var s in args.Skip(2))
            {
                int seed = int.Parse(s);
                var c = Circuit(intensity, seed);
                HeadlongHedges.Place(c, intensity, hedges);
                HeadlongHedges.PlaceCrystals(c, intensity, hedges, crystals);
                var sb = new StringBuilder();
                sb.Append($"{{\"intensity\":{intensity},\"seed\":{seed},\"start_time\":{HeadlongHedges.StartingTimeLevel(intensity)},\"gates\":[");
                sb.Append(string.Join(",", c.Select(g => $"{{\"p\":{V(g.Position)},\"a\":{V(g.Axis)},\"r\":{F(g.Radius)}}}")));
                sb.Append("],\"hedges\":[");
                sb.Append(string.Join(",", hedges.Select(h =>
                {
                    parts.Clear();
                    HeadlongHedges.Build(h, parts);
                    return $"{{\"leg\":{h.Leg},\"c\":{V(h.Centre)},\"n\":{V(h.Normal)},\"r\":{F(h.Radius)},\"kind\":\"{h.Kind}\"," +
                           $"\"w\":{V(h.Window)},\"wr\":{F(h.WindowRadius)},\"along\":{F(h.AlongLeg)},\"prisms\":{parts.Count}," +
                           $"\"danger\":{parts.Count(p => p.Kind == PrismKind.Danger)}}}";
                })));
                sb.Append("],\"crystals\":[").Append(string.Join(",", crystals.Select(V))).Append("]}");
                Console.WriteLine(sb.ToString());
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "--sweep")
        {
            var hedges = new List<HeadlongHedges.Hedge>();
            var parts = new List<HeadlongHedges.Part>();
            for (int i = 1; i <= 4; i++)
            {
                var counts = new List<int>(); var danger = new List<int>(); var prisms = new List<int>(); var radii = new List<float>();
                for (int seed = 1; seed <= 400; seed++)
                {
                    HeadlongHedges.Place(Circuit(i, seed), i, hedges);
                    counts.Add(hedges.Count);
                    danger.Add(hedges.Count(h => h.Danger));
                    int p = 0;
                    foreach (var h in hedges) { parts.Clear(); HeadlongHedges.Build(h, parts); p += parts.Count; radii.Add(h.Radius); }
                    prisms.Add(p);
                }
                int Med(List<int> xs) { var s = xs.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
                radii.Sort();
                Console.WriteLine($"I{i}: hedges median {Med(counts)} (min {counts.Min()}), danger {Med(danger)}, " +
                                  $"crystals {HeadlongHedges.CrystalsPerLap(i)}, prisms median {Med(prisms)} max {prisms.Max()}, " +
                                  $"radius p10 {radii[radii.Count / 10]:F0} median {radii[radii.Count / 2]:F0}, start Time {HeadlongHedges.StartingTimeLevel(i)}");
            }
            return 0;
        }

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
}
