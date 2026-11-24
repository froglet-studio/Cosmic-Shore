// Runs every [Test] of the compiled SHIPPED Breakwater ruin tests (BreakwaterRuinsTests) by reflection and prints a pass/fail line per test. Exit 0 = all passed.
//   --measure   instead: what the I4 courses of seeds 1-100 carry (ruins, prisms, wheels, kinds, the colour share).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    static void Measure()
    {
        var pads = BreakwaterCourse.SpawnPadRing(Vector3.zero, BreakwaterCourseSettings.DefaultSpawnRingRadius);
        int courses = 0, fewest = int.MaxValue, most = 0, wheelCourses = 0, minPrisms = int.MaxValue, maxPrisms = 0;
        long prisms = 0, offered = 0, worstGap = 0;
        var kinds = new int[SpearlineRuins.KindCount];
        var slotOf = new List<int>();
        var inSlot = new List<int>();
        for (int seed = 1; seed <= 100; seed++)
        {
            var settings = BreakwaterCourseSettings.ForIntensity(4);
            settings.StationCount = 15;
            var stations = BreakwaterCourse.Generate(seed, settings);
            if (stations == null || stations.Count < 15) continue;
            var course = stations.Select(s => new RaceGate(s.Position, s.Axis, s.PortRadius)).ToList();
            var lap = new List<RaceGate>();
            var parts = new List<SpearlineRuinPart>();
            var wheels = new List<SpearlineRuinWheel>();
            var ruin = new List<int>();
            BreakwaterRuins.Lay(course, 1, 4, pads, lap, parts, wheels, ruin);
            courses++;
            offered += parts.Count;
            int standing = ruin.Count(r => r >= 0), count = ruin.Count == 0 ? 0 : ruin.Max() + 1;
            prisms += standing;
            fewest = Math.Min(fewest, count); most = Math.Max(most, count);
            minPrisms = Math.Min(minPrisms, standing); maxPrisms = Math.Max(maxPrisms, standing);
            bool wheel = false;
            foreach (var w in wheels) if (ruin[w.First] >= 0) wheel = true;
            if (wheel) wheelCourses++;
            int leg = -1;
            for (int i = 0; i < parts.Count; i++)
                if (ruin[i] >= 0 && parts[i].Leg != leg) { leg = parts[i].Leg; kinds[SpearlineRuins.KindOf(leg, lap[0].Position)]++; }
            BreakwaterRuins.ShareOut(ruin, 3, slotOf, inSlot);
            if (inSlot.Count > 0) worstGap = Math.Max(worstGap, inSlot.Max() - inSlot.Min());
        }
        Console.WriteLine($"courses {courses}/100  ruins {fewest}-{most}  prisms mean {prisms / Math.Max(1, courses)} ({minPrisms}-{maxPrisms})" +
                          $"  offered mean {offered / Math.Max(1, courses)}  wheel on {wheelCourses} courses");
        Console.WriteLine($"legs by kind (pergola flower vines wall tunnel maze wheel; a wheel that does not fit is laid as a pergola): {string.Join(" ", kinds)}");
        Console.WriteLine($"ring margin {BreakwaterRuins.RingMargin(4):0.0}  station reach {BreakwaterCourse.StationReach(42f):0.0}  worst colour gap {worstGap} prisms");
    }

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--measure") { Measure(); return 0; }
        int pass = 0, fail = 0;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes().Where(t => t.Namespace == "CosmicShore.Tests").OrderBy(t => t.Name))
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                try { m.Invoke(Activator.CreateInstance(type), null); pass++; Console.WriteLine($"PASS {type.Name}.{m.Name}"); }
                catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
