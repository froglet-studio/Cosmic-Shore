// Runs every [Test] of the compiled SHIPPED Perch Run tests (PerchRunCourseTests) by reflection and prints a pass/fail line
// per test. Exit 0 = all passed. One more mode reads the shipped C#, never a copy:
//   --sweep   the ladder per intensity over 400 seeds, and the race clock of three pilots, all on the seat AI's numbers
//             (turn 90 deg/s, cruise 100 u/s, the dart's 1 s and 1.2 s cooldown, a perch's 1.5 s pin for the first three
//             islands): the CRUISER (never darts), the FULL-CHARGER (every dart at the ring's centre at a full charge: no
//             judgement of length or line) and the READER (the AI's PlanDart: exact length, rim shots, doubles).
//   --trace I seed   one course's dart log for the full-charger and the reader.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--sweep") { Sweep(); return 0; }
        if (args.Length > 2 && args[0] == "--trace") { Trace(int.Parse(args[1]), int.Parse(args[2])); return 0; }

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

    enum Pilot { Cruiser, FullCharger, Reader }

    /// <summary>The race clock of one pilot; also the darts it fired and the darts that stopped short of the ring (on coral, or out of reach).</summary>
    static bool _trace;

    static float Clock(List<RaceGate> rings, List<Coral> coral, Pilot pilot, out int darts, out int blocked)
    {
        Vector3 pos = PerchRunCourse.StartPoses(1)[0].position, fwd = PerchRunCourse.StartAxis, lastPerch = pos;
        float t = 0f, reloadLeft = 0f;
        int next = 0, perches = 0, guard = 0;
        darts = blocked = 0;
        while (next < rings.Count && guard++ < 400)
        {
            var g = rings[next];
            Vector3 aim; float length;
            if (pilot == Pilot.Reader) aim = PerchRunCourse.PlanDart(pos, rings, next, coral, out length);
            else
            {
                Vector3 to = g.Position - pos;
                aim = g.Position + to.normalized * PerchRunCourse.Overshoot;
                length = pilot == Pilot.Cruiser || to.magnitude + PerchRunCourse.Overshoot < PerchRunCourse.DartMinRange ? 0f : PerchRunCourse.DartRange;
            }
            Vector3 dir = (aim - pos).normalized;
            float turn = Vector3.Angle(fwd, dir) / PerchRunCourse.AiTurnRate;
            if (length <= 0f)
            {
                // cruise: through the ring (and through any coral: the Goby's flight ignores collision)
                float d = (aim - pos).magnitude;
                t += turn + d / PerchRunCourse.AiCruiseSpeed;
                reloadLeft = Mathf.Max(0f, reloadLeft - turn - d / PerchRunCourse.AiCruiseSpeed);
                Vector3 end = aim;
                while (next < rings.Count && PerchRunCourse.Threads(pos, end, rings[next])) next++;
                if (_trace) Console.WriteLine($"  t {t,6:F1}  CRUISE {d:F0}  rings ->{next}");
                pos = end; fwd = dir;
                continue;
            }
            t += Mathf.Max(turn, reloadLeft);
            float go = length;
            if (PerchRunCourse.FirstCoral(pos, dir, length, coral, out float hit)) go = hit;
            darts++;
            Vector3 stop = pos + dir * go;
            int before = next;
            while (next < rings.Count && PerchRunCourse.Threads(pos, stop, rings[next])) next++;
            if (next == before && go < (g.Position - pos).magnitude) blocked++;
            t += PerchRunCourse.DartSeconds;
            reloadLeft = PerchRunCourse.DartCooldown;
            if (go < 6f)
            {
                // nose on the coral: cruise past it, then dart again
                float d = 2f * PerchRunCourse.HalfDiagonal(PerchRunCourse.ForIntensity(4).CoralSize) + 12f;
                stop = pos + dir * d;
                t += d / PerchRunCourse.AiCruiseSpeed;
                reloadLeft = Mathf.Max(0f, reloadLeft - d / PerchRunCourse.AiCruiseSpeed);
            }
            if (perches < PerchRunCourse.IslandQueue && (stop - lastPerch).magnitude >= PerchRunCourse.IslandMinLength)
            {
                perches++; lastPerch = stop; t += PerchRunCourse.PerchSeconds;
                reloadLeft = Mathf.Max(0f, reloadLeft - PerchRunCourse.PerchSeconds);
            }
            if (_trace) Console.WriteLine($"  t {t,6:F1}  dart {darts,2} len {length,4:F0} went {go,4:F0} turn {turn:F2}s  rings {before}->{next}  perches {perches}");
            pos = stop; fwd = dir;
        }
        return next < rings.Count ? 999f : t;
    }

    static void Trace(int intensity, int seed)
    {
        var s = PerchRunCourse.ForIntensity(intensity);
        var rings = PerchRunCourse.Generate(seed, s);
        var coral = new List<Coral>();
        PerchRunCourse.CoralFor(rings, s, coral);
        _trace = true;
        foreach (var p in new[] { Pilot.FullCharger, Pilot.Reader })
        {
            Console.WriteLine($"{p}:");
            Console.WriteLine($"  = {Clock(rings, coral, p, out _, out _):F1} s");
        }
    }

    static void Sweep()
    {
        for (int i = 1; i <= 4; i++)
        {
            var s = PerchRunCourse.ForIntensity(i);
            var cruise = new List<float>(); var full = new List<float>(); var reader = new List<float>();
            var saveFull = new List<float>(); var legs = new List<float>();
            int fails = 0, dartsR = 0, blockedF = 0, stuck = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                var rings = PerchRunCourse.Generate(seed, s);
                if (rings == null) { fails++; continue; }
                var coral = new List<Coral>();
                PerchRunCourse.CoralFor(rings, s, coral);
                for (int k = 1; k < rings.Count; k++) legs.Add((rings[k].Position - rings[k - 1].Position).magnitude);
                float c0 = Clock(rings, coral, Pilot.Cruiser, out _, out _);
                float f = Clock(rings, coral, Pilot.FullCharger, out _, out int bf);
                float r = Clock(rings, coral, Pilot.Reader, out int dr, out _);
                if (f >= 999f || r >= 999f) { stuck++; continue; }
                cruise.Add(c0); full.Add(f); reader.Add(r); saveFull.Add(1f - r / f);
                dartsR += dr; blockedF += bf;
            }
            float Med(List<float> xs) { if (xs.Count == 0) return 0f; var o = xs.OrderBy(x => x).ToList(); return o[o.Count / 2]; }
            int n = Math.Max(1, cruise.Count);
            Console.WriteLine($"I{i}: fails {fails}/400 (stuck {stuck})  rings {s.RingCount}  radius {s.RingRadius}  legs {legs.Min():F0}-{legs.Max():F0} (med {Med(legs):F0})  " +
                              $"period {s.Period} (hairpin {s.HairpinSlot}, blocker {s.BlockerSlot}, double {s.DoubleSlot})  coral {s.CoralSize}");
            Console.WriteLine($"     clock: cruiser {Med(cruise):F1} s, full-charger {Med(full):F1} s ({(float)blockedF / n:F1} darts a race stop short of their ring)" +
                              $", reader {Med(reader):F1} s ({(float)dartsR / n:F1} darts); " +
                              $"the reader saves {100f * Med(saveFull):F0}% on the full-charger");
        }
    }
}
