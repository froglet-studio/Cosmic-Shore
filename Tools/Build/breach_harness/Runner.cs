// Runs every [Test] of the compiled SHIPPED Breach tests (BreachCourseTests) by reflection and prints a pass/fail line per
// test. Exit 0 = all passed. Two more modes read the shipped C#, never a copy:
//   --sweep        the ladder per intensity over 200 seeds, and the race clock of three Orca helms flying the same pursuit
//                  line on the shipped numbers (cruise 29.75 u/s, burn x5, turn speed*0.055+44 deg/s, a 3 s tank refilling
//                  0.25 a second only while the burn is let go, a danger prism a dead stop recovering over 3 s): NEVER
//                  burns (the shipped AI helm), HOLD holds the burn the whole race (one tank, then the tank stays empty),
//                  PULSE burns every full tank to empty wherever it is (blind to the course), and BURN is
//                  BreachCourse.BurnWanted (the AI coach). Every helm steers on what it saw 0.3 s ago.
//   --trace I seed one course's log for the greedy and burning helms.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--sweep") { Sweep(); return 0; }
        if (args.Length > 1 && args[0] == "--why") { Why(int.Parse(args[1])); return 0; }
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

    // ── The race clock ───────────────────────────────────────────────

    enum Helm { Never, Hold, Pulse, Burn }

    /// <summary>The hull's collider is taken as this radius: a crossing nearer the wall than this hits it.</summary>
    const float HullRadius = 4f;
    const float Dt = 0.02f, Limit = 600f;

    /// <summary>Every helm steers on what it saw this long ago (a person's reaction; the AI's think step): at the burn's
    /// speed the hull covers four times the ground before a correction lands.</summary>
    const float Reaction = BreachCourse.ReactionSeconds;
    static bool _trace;

    struct Result { public float Time; public int Hits, Misses; public float BurnSeconds; }

    static Result Clock(List<RaceGate> rings, BreachSettings s, Helm helm)
    {
        var start = BreachCourse.StartPoses(1)[0];
        Vector3 pos = start.position, fwd = BreachCourse.StartAxis;
        float tank = BreachCourse.BoostTank, throttle = 1f, t = 0f;
        bool burning = false;
        var r = new Result();
        int next = 0;
        var seen = new Queue<Vector3>();
        int lag = Mathf.Max(1, Mathf.RoundToInt(Reaction / Dt));
        while (next < rings.Count && t < Limit)
        {
            bool want = helm switch
            {
                Helm.Hold => true,
                Helm.Pulse => burning || tank >= BreachCourse.BoostTank - 1e-4f,
                Helm.Burn => BreachCourse.BurnWanted(pos, fwd, rings, next, s, tank, burning),
                _ => false,
            };
            // OrcaCrew.StepHelm, line for line: a held burn spends the tank; the frame it empties is not a burn; the tank
            // refills only on a frame the burn is not held or the tank is empty
            if (want && tank > 0f)
            {
                tank = Mathf.Max(0f, tank - Dt);
                burning = tank > 0f;
            }
            else
            {
                burning = false;
                tank = Mathf.Min(BreachCourse.BoostTank, tank + BreachCourse.BoostRefill * Dt);
            }
            if (burning) r.BurnSeconds += Dt;
            float speed = (burning ? BreachCourse.BoostSpeed : BreachCourse.CruiseSpeed) * throttle;
            throttle = Mathf.Min(1f, throttle + Dt / BreachCourse.DangerRecovery);

            // pursuit: turn the nose toward the aim point at the hull's turn rate
            Vector3 aim = BreachCourse.AimPoint(pos, rings, next, s);
            seen.Enqueue((aim - pos).normalized);
            Vector3 want3 = seen.Count > lag ? seen.Dequeue() : seen.Peek();
            float ang = Vector3.Angle(fwd, want3);
            float step = BreachCourse.TurnRate(speed) * Dt;
            if (ang > 1e-3f)
            {
                Vector3 axis = Vector3.Cross(fwd, want3);
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
                fwd = RaceCourseGeometry.RotateAbout(fwd, axis.normalized, Mathf.Min(ang, step) * Mathf.Deg2Rad).normalized;
            }
            Vector3 was = pos;
            pos += fwd * speed * Dt;
            t += Dt;

            // the wall's plane, crossed this step inside the wall?
            var g = rings[next];
            if (BreachCourse.IsWalled(next, s))
            {
                Vector3 c = BreachCourse.WallCentre(rings, next, s);
                float w0 = Vector3.Dot(was - c, g.Axis), w1 = Vector3.Dot(pos - c, g.Axis);
                if (w0 < 0f && w1 >= 0f)
                {
                    Vector3 cross = Vector3.Lerp(was, pos, w0 / (w0 - w1));
                    if ((cross - c).magnitude < BreachCourse.WallRadius(g, s) + HullRadius)
                    {
                        throttle = 0f;   // a danger prism: a dead stop, recovering over DangerRecovery
                        r.Hits++;
                        if (_trace) Console.WriteLine($"  t {t,6:F1}  WALL   ring {next,2}  tank {tank:F1}");
                    }
                }
            }
            // the ring's plane, crossed this step?
            float a0 = Vector3.Dot(was - g.Position, g.Axis), a1 = Vector3.Dot(pos - g.Position, g.Axis);
            if (a0 < 0f && a1 >= 0f || a0 > 0f && a1 <= 0f)
            {
                Vector3 cross = Vector3.Lerp(was, pos, a0 / (a0 - a1));
                float fromCentre = (cross - g.Position).magnitude;
                if (fromCentre <= g.Radius)
                {
                    if (_trace) Console.WriteLine($"  t {t,6:F1}  ring   {next,2}  tank {tank:F1}  {(burning ? "burning" : "")}");
                    next++;
                }
                else if (a0 < 0f)
                {
                    r.Misses++;
                    if (_trace) Console.WriteLine($"  t {t,6:F1}  MISS   ring {next,2}  by {fromCentre - g.Radius:F0} u");
                }
            }
        }
        r.Time = t;
        return r;
    }

    static void Trace(int intensity, int seed)
    {
        var s = BreachCourse.ForIntensity(intensity);
        var rings = BreachCourse.Generate(seed, s);
        _trace = true;
        foreach (var h in new[] { Helm.Pulse, Helm.Burn })
        {
            Console.WriteLine($"{h}:");
            var r = Clock(rings, s, h);
            Console.WriteLine($"  = {r.Time:F1} s, {r.Hits} wall hits, {r.Misses} misses, {r.BurnSeconds:F1} s burning");
        }
        _trace = false;
    }

    /// <summary>Why walks fail at an intensity: the first rule each of 800 walks breaks (TryWalk by reflection).</summary>
    static void Why(int intensity)
    {
        var s = BreachCourse.ForIntensity(intensity);
        var tally = new Dictionary<string, int>();
        var walk = typeof(BreachCourse).GetMethod("TryWalk", BindingFlags.NonPublic | BindingFlags.Static);
        for (int seed = 1; seed <= 50; seed++)
            for (int attempt = 0; attempt < 16; attempt++)
            {
                object rng = new RaceCourseGeometry.Rng(unchecked(seed * 7919 + attempt * 104729 + 71));
                var p = new List<Vector3>(); var a = new List<Vector3>();
                var argv = new object[] { rng, s, p, a };
                if (!(bool)walk.Invoke(null, argv)) { tally["walk"] = tally.GetValueOrDefault("walk") + 1; continue; }
                var rings = new List<RaceGate>();
                for (int i = 0; i < p.Count; i++) rings.Add(new RaceGate(p[i], a[i], s.RingRadius));
                BreachCourse.Valid(s, rings, out string why);
                string key = why == null ? "OK" : Regex.Replace(why, @"\d+", "#");
                tally[key] = tally.GetValueOrDefault(key) + 1;
            }
        foreach (var kv in tally.OrderByDescending(x => x.Value)) Console.WriteLine($"{kv.Value,5}  {kv.Key}");
    }

    static void Sweep()
    {
        for (int i = 1; i <= 4; i++)
        {
            var s = BreachCourse.ForIntensity(i);
            int fails = 0, n = 0, burnWins = 0, burnLoses = 0;
            var sum = new Dictionary<Helm, (double t, double hits, double misses, double burn)>();
            foreach (Helm h in Enum.GetValues(typeof(Helm))) sum[h] = (0, 0, 0, 0);
            for (int seed = 1; seed <= 200; seed++)
            {
                var rings = BreachCourse.Generate(seed, s);
                if (rings == null) { fails++; continue; }
                n++;
                var res = new Dictionary<Helm, Result>();
                foreach (Helm h in Enum.GetValues(typeof(Helm)))
                {
                    var r = Clock(rings, s, h);
                    res[h] = r;
                    var a = sum[h];
                    sum[h] = (a.t + r.Time, a.hits + r.Hits, a.misses + r.Misses, a.burn + r.BurnSeconds);
                }
                if (res[Helm.Burn].Time < res[Helm.Pulse].Time - 0.05f) burnWins++;
                else if (res[Helm.Burn].Time > res[Helm.Pulse].Time + 0.05f) burnLoses++;
            }
            string Row(Helm h) => $"{sum[h].t / n:F1} s ({sum[h].hits / n:F1} wall hits, {sum[h].misses / n:F1} misses, {sum[h].burn / n:F1} s burning)";
            double gain = (sum[Helm.Pulse].t - sum[Helm.Burn].t) / sum[Helm.Pulse].t * 100.0;
            Console.WriteLine($"I{i}: fails {fails}/200  radius {s.RingRadius}  hard every {s.SharpEvery}  walls every {s.WallEvery} (margin {s.WallMargin}, {s.WallAhead} u ahead)");
            Console.WriteLine($"     never  {Row(Helm.Never)}");
            Console.WriteLine($"     hold   {Row(Helm.Hold)}");
            Console.WriteLine($"     pulse  {Row(Helm.Pulse)}");
            Console.WriteLine($"     burn   {Row(Helm.Burn)}");
            Console.WriteLine($"     the burn saves {gain:F1}% on the pulsing helm (wins {burnWins}, loses {burnLoses} of {n})");
        }
    }
}
