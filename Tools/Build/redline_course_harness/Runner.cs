// Runs every [Test] of the compiled SHIPPED Redline tests (RedlineCourseTests, RedlineTimeCrystalsTests) by reflection
// and prints a pass/fail line per test. Exit 0 = all passed. Two more modes read the shipped C#, never a copy:
//   --crystals <intensity> <seed>...   one JSON line per course: its gates and its Time crystals (redline_parity.json,
//                                       which Tools/Build/manta_redline_sim.py --self-test holds its Python port to)
//   --sweep                            the corner ladder per intensity over 400 seeds (what RedlineCourse documents)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    const float Inner = 480f, Outer = 1080f;

    static List<RaceGate> Circuit(int intensity, int seed)
    {
        var s = RedlineCourse.ForIntensity(intensity);
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
        if (args.Length > 0 && args[0] == "--crystals")
        {
            int intensity = int.Parse(args[1]);
            var into = new List<Vector3>();
            foreach (var s in args.Skip(2))
            {
                int seed = int.Parse(s);
                var c = Circuit(intensity, seed);
                RedlineTimeCrystals.Place(c, intensity, into);
                var sb = new StringBuilder();
                sb.Append($"{{\"intensity\":{intensity},\"seed\":{seed},\"gates\":[");
                sb.Append(string.Join(",", c.Select(g => $"{{\"p\":{V(g.Position)},\"a\":{V(g.Axis)},\"r\":{F(g.Radius)}}}")));
                sb.Append("],\"crystals\":[").Append(string.Join(",", into.Select(V))).Append("]}");
                Console.WriteLine(sb.ToString());
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "--sweep")
        {
            float full = RedlineCourse.FullBoostRadius;
            for (int i = 1; i <= 4; i++)
            {
                var sorted = new List<float[]>();
                for (int seed = 1; seed <= 400; seed++)
                {
                    var c = Circuit(i, seed);
                    int n = c.Count;
                    var r = new float[n];
                    for (int k = 0; k < n; k++)
                        r[k] = RaceCourseGeometry.CornerRadius(c[(k - 1 + n) % n].Position, c[k].Position, c[(k + 1) % n].Position);
                    Array.Sort(r);
                    sorted.Add(r);
                }
                float Med(int k) { var xs = sorted.Select(r => r[k]).OrderBy(x => x).ToList(); return xs[xs.Count / 2]; }
                var costing = sorted.Select(r => r.Count(x => x < full)).OrderBy(x => x).ToList();
                Console.WriteLine($"I{i}: costing {costing[costing.Count / 2]}  tightest " +
                    string.Join(" / ", Enumerable.Range(0, 3).Select(k => $"{Med(k):F0}u ({RedlineCourse.FastestSoarForCorner(Med(k)):P0})")) +
                    $"  min {sorted.Min(r => r[0]):F1}u");
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
