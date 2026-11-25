// Runs every [Test] of the compiled SHIPPED Lantern Run ruins tests (LanternRunRuinsTests) by reflection and prints a
// pass/fail line per test. Exit 0 = all passed.
//   --measure   instead: one line per I4 seed (1-60) with the legs that carry a ruin, the prisms laid and made, and the
//               wheels turning and made - the numbers the tests' floors and LANTERNRUN.md are stated from.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

public static class Runner
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--measure") return Measure();
        int failed = 0, ran = 0;
        foreach (var type in typeof(Runner).Assembly.GetTypes().OrderBy(t => t.FullName))
        {
            var tests = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .Where(m => m.GetCustomAttribute<TestAttribute>() != null).OrderBy(m => m.Name).ToList();
            if (tests.Count == 0) continue;
            object fixture = Activator.CreateInstance(type);
            foreach (var test in tests)
            {
                ran++;
                try
                {
                    test.Invoke(fixture, null);
                    Console.WriteLine($"PASS {type.Name}.{test.Name}");
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine($"FAIL {type.Name}.{test.Name}: {e.InnerException?.Message}");
                }
            }
        }
        Console.WriteLine($"{ran - failed} of {ran} passed");
        return failed == 0 && ran > 0 ? 0 : 1;
    }

    static int Measure()
    {
        int minLegs = int.MaxValue, minPrisms = int.MaxValue, sumLegs = 0, sumLaid = 0, sumMade = 0, wheels = 0, turning = 0;
        const int seeds = 60;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var s = LanternRunCourse.ForIntensity(4);
            s.InnerRadius = 480f;
            s.OuterRadius = 1080f;
            s.FirstGateDirection = Vector3.up;
            var rings = HeadlongCircuit.Generate(seed, s);
            var clusters = LanternRunCourse.Clusters(rings, 4);
            var parts = new List<SpearlineRuinPart>();
            var wheelList = new List<SpearlineRuinWheel>();
            var laid = new List<bool>();
            LanternRunRuins.Lay(rings, 4, clusters, parts, wheelList, laid);
            var legs = new HashSet<int>();
            int count = 0;
            for (int i = 0; i < parts.Count; i++)
                if (laid[i]) { count++; legs.Add(parts[i].Leg); }
            int turn = wheelList.Count(w => laid[w.First]);
            Console.WriteLine($"seed {seed}: legs {legs.Count}, prisms {count} of {parts.Count}, wheels {turn} of {wheelList.Count}");
            minLegs = Math.Min(minLegs, legs.Count);
            minPrisms = Math.Min(minPrisms, count);
            sumLegs += legs.Count;
            sumLaid += count;
            sumMade += parts.Count;
            wheels += wheelList.Count;
            turning += turn;
        }
        Console.WriteLine($"min legs {minLegs}, mean legs {sumLegs / (float)seeds:0.00}, min prisms {minPrisms}, " +
                          $"mean prisms {sumLaid / (float)seeds:0.0}, kept {sumLaid} of {sumMade}, wheels {turning} of {wheels}");
        return 0;
    }
}
