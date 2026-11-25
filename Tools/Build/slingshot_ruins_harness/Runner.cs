// Runs every [Test] of the compiled SHIPPED SlingshotRuinsTests by reflection and prints a pass/fail line per test.
// Exit 0 = all passed. `--report` also prints, per seed, the I4 lap's ruins and ruin prisms (what the tests' bounds
// and SLINGSHOT.md quote).
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
        if (args.Contains("--report")) Report();
        if (args.Contains("--lanes")) Lanes();
        int failed = 0, ran = 0;
        var fixture = typeof(CosmicShore.Tests.SlingshotRuinsTests);
        var instance = Activator.CreateInstance(fixture);
        foreach (var m in fixture.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null))
        {
            ran++;
            try
            {
                m.Invoke(instance, null);
                Console.WriteLine($"PASS {m.Name}");
            }
            catch (TargetInvocationException e)
            {
                failed++;
                Console.WriteLine($"FAIL {m.Name}: {e.InnerException?.Message}");
            }
        }
        Console.WriteLine($"{ran - failed}/{ran} passed");
        return failed == 0 && ran > 0 ? 0 : 1;
    }

    // What a wider clear lane leaves standing: per lane radius, the fewest and most ruins and prisms a lap over the seeds.
    static void Lanes()
    {
        foreach (float lane in new[] { 60f, 80f, 100f, 120f, 140f, 160f, 200f })
        {
            int minRuins = int.MaxValue, maxRuins = 0, minPrisms = int.MaxValue, maxPrisms = 0;
            foreach (int seed in new[] { 1, 2, 3, 7, 11, 17, 23, 42, 101, 102, 103, 104, 2026, 90210 })
            {
                var c = SlingshotCourse.ForIntensity(4);
                c.InnerRadius = 480f;
                c.OuterRadius = 1080f;
                c.FirstGateDirection = Vector3.up;
                var lap = HeadlongCircuit.Generate(seed, c);
                var parts = new List<SpearlineRuinPart>();
                var wheels = new List<SpearlineRuinWheel>();
                var s = SpearlineRuins.ForIntensity(4).With(lane, SpearlineRuins.MinLegLength, SpearlineRuins.RingMargin);
                SpearlineRuins.Lay(lap, s, parts, wheels);
                int ruins = parts.Select(p => p.Leg).Distinct().Count();
                minRuins = Math.Min(minRuins, ruins); maxRuins = Math.Max(maxRuins, ruins);
                minPrisms = Math.Min(minPrisms, parts.Count); maxPrisms = Math.Max(maxPrisms, parts.Count);
            }
            Console.WriteLine($"lane {lane}: {minRuins}-{maxRuins} ruins, {minPrisms}-{maxPrisms} prisms");
        }
    }

    static void Report()
    {
        foreach (int seed in new[] { 1, 2, 3, 7, 11, 17, 23, 42, 101, 102, 103, 104, 2026, 90210 })
        {
            var s = SlingshotCourse.ForIntensity(4);
            s.InnerRadius = 480f;
            s.OuterRadius = 1080f;
            s.FirstGateDirection = Vector3.up;
            var lap = HeadlongCircuit.Generate(seed, s);
            var parts = new List<SpearlineRuinPart>();
            var wheels = new List<SpearlineRuinWheel>();
            SlingshotRuins.Lay(lap, 4, parts, wheels);
            int longLegs = 0;
            for (int i = 0; i < lap.Count - 1; i++)
                if ((lap[i + 1].Position - lap[i].Position).magnitude >= SpearlineRuins.MinLegLength) longLegs++;
            Console.WriteLine($"seed {seed}: {parts.Select(p => p.Leg).Distinct().Count()} ruins on {longLegs} long legs, " +
                              $"{parts.Count} prisms, {wheels.Count} wheel(s)");
        }
    }
}
