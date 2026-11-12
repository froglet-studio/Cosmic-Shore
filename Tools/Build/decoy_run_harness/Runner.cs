// Runs every [Test] of the compiled SHIPPED Decoy Run tests (DecoyRunCourseTests) by reflection and prints a pass/fail line
// per test. Exit 0 = all passed. One more mode reads the shipped C#, never a copy:
//   --sweep   the ladder per intensity over 400 seeds, and the race clock of three pilots who act the moment they can:
//             the FLYER (no herding: the hull's own 50 u/s, a lower bound), the JUMPER (one ring a cycle: line up behind
//             it, one zip; never a skewer) and the COACH (the Unity AI's plan: a whole group a cycle, the take-off onto the
//             line and a zip to each decoy). A flat change costs the roll (90 deg/s, the rig's RollScaler) plus a second for the
//             board to settle (flightPlaneEase 3/s), less the swap's 0.75 s of flight it overlaps.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    const float RollDegreesPerSecond = 90f, SettleSeconds = 1f;

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

    /// <summary>The race clock of a pilot taking up to <paramref name="maxRings"/> rings a cycle in up to
    /// <paramref name="zips"/> zips; also the cycles flown and the longest single run.</summary>
    static float Clock(List<RaceGate> rings, in DecoyRunSettings s, int zips, int maxRings, out int cycles, out float longestRun)
    {
        Vector3 hull = DecoyRunCourse.StartPoses(1)[0].position;
        Vector3 board = DecoyRunCourse.StartFlatNormal;
        float t = 0f;
        cycles = 0;
        longestRun = 0f;
        int next = 0;
        while (next < rings.Count)
        {
            Vector3 flat = DecoyRunCourse.FlatNormal(rings, next, s);
            float roll = Vector3.Angle(board, flat);
            if (roll > 0.5f)
            {
                // the cycle before ended in flight; the roll and the settle run on past the swap's cooldown
                t += Mathf.Max(0f, roll / RollDegreesPerSecond + SettleSeconds - DecoyRunCourse.SwapSeconds);
                board = flat;
            }
            int want = Mathf.Min(maxRings, DecoyRunCourse.RingsACycleTakes(next, zips, s));
            int made = DecoyRunCourse.PlanCycle(hull, board, rings, next, rings.Count, zips, want, s, out var plan, out float run);
            if (made == 0) { t += 999f; break; }
            Vector3 m = 0.5f * (plan.Jack0To + plan.Jack1To);
            Vector3 d0 = plan.Jack0Via;
            Vector3 d1 = plan.SecondTravel == 1 ? plan.Jack1Via : d0;
            t += DecoyRunCourse.CycleSeconds(run, m, d0, d1, plan.SecondTravel == 1 ? 2 : 1);
            hull = plan.SecondTravel == 1 ? d1 : d0;
            longestRun = Mathf.Max(longestRun, run);
            cycles++;
            next += made;
        }
        return t;
    }

    static float Flyer(List<RaceGate> rings)
    {
        Vector3 at = DecoyRunCourse.StartPoses(1)[0].position;
        float d = 0f;
        foreach (var r in rings) { d += (r.Position - at).magnitude; at = r.Position; }
        return d / DecoyRunCourse.FlightSpeed;
    }

    static void Sweep()
    {
        for (int i = 1; i <= 4; i++)
        {
            var s = DecoyRunCourse.ForIntensity(i);
            int zips = s.StartTimeLevel >= 5 ? 2 : 1;
            var legs = new List<float>(); var inner = new List<float>(); var rolls = new List<float>();
            var flyer = new List<float>(); var jumper = new List<float>(); var coach = new List<float>();
            var save = new List<float>(); var runs = new List<float>();
            int fails = 0, cyclesJ = 0, cyclesC = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                var rings = DecoyRunCourse.Generate(seed, s);
                if (rings == null) { fails++; continue; }
                for (int k = 1; k < rings.Count; k++)
                {
                    legs.Add((rings[k].Position - rings[k - 1].Position).magnitude);
                    if (DecoyRunCourse.StartsGroup(k, s)) inner.Add(Vector3.Angle(rings[k - 1].Axis, rings[k].Axis));
                    if (DecoyRunCourse.FlatOf(k, s) != DecoyRunCourse.FlatOf(k - 1, s))
                        rolls.Add(Vector3.Angle(DecoyRunCourse.FlatNormal(rings, k, s), DecoyRunCourse.FlatNormal(rings, k - 1, s)));
                }
                flyer.Add(Flyer(rings));
                float j = Clock(rings, s, 1, 1, out int cj, out _);
                float c = Clock(rings, s, zips, 3, out int cc, out float longest);
                jumper.Add(j); coach.Add(c); save.Add(1f - c / j); runs.Add(longest);
                cyclesJ += cj; cyclesC += cc;
            }
            float Med(List<float> xs) { if (xs.Count == 0) return 0f; var o = xs.OrderBy(x => x).ToList(); return o[o.Count / 2]; }
            float P(List<float> xs, float q) { var o = xs.OrderBy(x => x).ToList(); return o[(int)Math.Min(o.Count - 1, q * o.Count)]; }
            int n = 400 - fails;
            Console.WriteLine($"I{i}: fails {fails}/400  rings {s.RingCount} in groups of {s.Group}  radius {s.RingRadius}  " +
                              $"legs {legs.Min():F0}-{legs.Max():F0} (med {Med(legs):F0})  " +
                              (inner.Count > 0 ? $"turns between groups med {Med(inner):F0}  " : "") +
                              (rolls.Count > 0 ? $"rolls {rolls.Min():F0}-{rolls.Max():F0} deg  " : "") +
                              $"decoys {zips}");
            Console.WriteLine($"     clock: flyer {Med(flyer):F1} s, jumper {Med(jumper):F1} s ({(float)cyclesJ / n:F1} cycles), " +
                              $"coach {Med(coach):F1} s ({(float)cyclesC / n:F1} cycles); the coach saves {100f * Med(save):F0}% " +
                              $"(p10 {100f * P(save, 0.1f):F0}%, p90 {100f * P(save, 0.9f):F0}%); longest run med {Med(runs):F1} s");
        }
    }
}
