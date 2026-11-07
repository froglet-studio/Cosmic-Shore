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
