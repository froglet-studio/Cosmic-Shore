// Runs every [Test] of the compiled SHIPPED Skein ruins tests (SkeinRuinsTests) by reflection and prints a pass/fail
// line per test. Exit 0 = all passed.
//   --measure   instead: one line per I4 cable (the arena prefabs' seed and the six after it) with the legs that carry
//               a ruin, the prisms laid of those made on the carried legs, and the wheels turning and made - the
//               numbers the tests' floors and SKEIN.md section 20 are stated from.
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
        const int shipped = 20260909;
        var settings = SkeinCourseSettings.ForIntensity(4);
        int ruins = SpearlineRuins.ForIntensity(4).PerLap;
        for (int cable = 0; cable < 7; cable++)
        {
            int seed = shipped + cable;
            SkeinCourse.SkeinBuild build = null;
            for (int attempt = 0; attempt < 6 && build == null; attempt++)
                build = SkeinCourse.BuildAll(unchecked(seed + attempt * 7919), settings);
            if (build == null)
            {
                Console.WriteLine($"cable {cable}: no cable");
                continue;
            }
            var rings = build.Gates.Select(g => new RaceGate(g.Position, g.Axis, g.Radius)).ToList();
            var keepOff = new List<Vector3>();
            SkeinRuins.TryKeepOff(seed, settings, Vector3.zero, keepOff);
            var parts = new List<SpearlineRuinPart>();
            var wheels = new List<SpearlineRuinWheel>();
            var laid = new List<bool>();
            SkeinRuins.Lay(rings, 4, keepOff, parts, wheels, laid);
            int made = 0, kept = 0;
            double volume = 0;
            var perLeg = new SortedDictionary<int, int>();
            for (int i = 0; i < parts.Count; i++)
            {
                if (!SkeinRuins.Carries(parts[i].Leg, ruins)) continue;
                made++;
                if (!laid[i]) continue;
                kept++;
                volume += (double)parts[i].Scale.x * parts[i].Scale.y * parts[i].Scale.z;
                perLeg[parts[i].Leg] = perLeg.TryGetValue(parts[i].Leg, out int n) ? n + 1 : 1;
            }
            int turning = wheels.Count(w => laid[w.First]);
            string legs = string.Join(" ", perLeg.Select(p => $"{p.Key}:{SpearlineRuins.KindOf(p.Key, rings[0].Position)}={p.Value}"));
            Console.WriteLine($"cable {cable}: rail prisms {build.PrismCount}, legs {perLeg.Count}, prisms {kept} of {made}, volume {volume:0}, " +
                              $"wheels {turning} of {wheels.Count}  (leg:kind=prisms {legs})");
        }
        return 0;
    }
}
