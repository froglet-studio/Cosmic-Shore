// Runs every [Test] of the compiled SHIPPED SerpentTimeCourseTests by reflection and prints a pass/fail line per test.
// Exit 0 = all passed. One more mode reads the shipped C#, never a copy:
//   --gates <intensity> <seed>...   one JSON line per course: its gates (serpent_time_parity.jsonl, which
//                                   Tools/Build/serpent_time_sim.py --self-test holds its Python course to)
// Run from the repository root: the tests read the Serpent's assets by relative path.
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
    const float Inner = 480f, Outer = 1080f;   // SerpentTimeCourseTests' shell

    static List<RaceGate> Circuit(int intensity, int seed)
    {
        var s = SerpentTimeCourse.ForIntensity(intensity);
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
        if (args.Length > 0 && args[0] == "--gates")
        {
            int intensity = int.Parse(args[1]);
            foreach (var s in args.Skip(2))
            {
                int seed = int.Parse(s);
                var c = Circuit(intensity, seed);
                var sb = new StringBuilder();
                sb.Append($"{{\"intensity\":{intensity},\"seed\":{seed},\"gates\":[");
                sb.Append(string.Join(",", c.Select(g => $"{{\"p\":{V(g.Position)},\"a\":{V(g.Axis)},\"r\":{F(g.Radius)}}}")));
                sb.Append("]}");
                Console.WriteLine(sb.ToString());
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
