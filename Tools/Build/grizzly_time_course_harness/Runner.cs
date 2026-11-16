// Runs every [Test] of the compiled SHIPPED Grizzly Time tests (GrizzlyTimeCourseTests, GrizzlyTimeKegsTests) by
// reflection and prints a pass/fail line per test. Exit 0 = all passed. One more mode reads the shipped C#, never a copy:
//   --sweep   the powder-keg ladder per intensity over 400 seeds: prisms, clearances, the bank shot's worth, and the
//             throw chain (each gate's keg taken from where the last throw landed).
// The throw chain is a MODEL (GrizzlyTimeKegs.ThrowCost: cruise to the blow point, one full carry, cruise on). It is not
// the hull and not the AI; the real race is measured in the editor (POWDER_KEGS_SPEC.md section 9).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    const float Inner = 480f, Outer = 1080f;
    const int Seeds = 400;

    static List<RaceGate> Circuit(int intensity, int seed)
    {
        var s = GrizzlyTimeCourse.ForIntensity(intensity);
        s.InnerRadius = Inner;
        s.OuterRadius = Outer;
        s.FirstGateDirection = Vector3.up;
        return HeadlongCircuit.Generate(seed, s);
    }

    static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        float t = d.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude) : 0f;
        return (a + d * t - p).magnitude;
    }

    static float Pct(List<float> xs, float q) { var s = xs.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, (int)(q * s.Count))]; }

    /// <summary>Seconds for one lap of the throw chain: from where the last throw landed, cruise to the keg's blow point,
    /// one second of launch, land a full carry down its lane. No carry bomb, no turning: an upper bound on a reader's lap.</summary>
    static float Chain(List<GrizzlyTimeKegs.Keg> kegs, ref Vector3 from)
    {
        float t = 0f;
        foreach (var k in kegs)
        {
            t += Vector3.Distance(from, GrizzlyTimeKegs.BlowPoint(k)) / GrizzlyTimeCourse.GrizzlyThrottleScaler + GrizzlyTimeCourse.ImpulseDuration;
            from = GrizzlyTimeKegs.ThrowLanding(k);
        }
        return t;
    }

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--sweep")
        {
            float carry = GrizzlyTimeCourse.CarryAfterLaunch(1f);
            Console.WriteLine($"full reach {GrizzlyTimeKegs.FullReach:F0} u, tap reach {GrizzlyTimeKegs.TapReach:F0} u, full carry {carry:F0} u, cruise {GrizzlyTimeCourse.GrizzlyThrottleScaler:F0} u/s");
            var kegs = new List<GrizzlyTimeKegs.Keg>();
            var parts = new List<GrizzlyTimeKegs.Part>();
            for (int i = 1; i <= 4; i++)
            {
                var mouthClear = new List<float>(); var chalkLine = new List<float>(); var waste = new List<float>();
                var legs = new List<float>(); var deflect = new List<float>();
                var reader = new List<float>(); var chalkDots = new List<int>();
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var lap = Circuit(i, seed);
                    int n = lap.Count;
                    GrizzlyTimeKegs.KegsFor(lap, kegs);
                    for (int r = 0; r < n; r++)
                    {
                        legs.Add(Vector3.Distance(lap[r].Position, lap[(r + 1) % n].Position));
                        deflect.Add(RaceCourseGeometry.Angle(lap[r].Position - lap[(r - 1 + n) % n].Position, lap[(r + 1) % n].Position - lap[r].Position));
                    }
                    foreach (var keg in kegs)
                    {
                        GrizzlyTimeKegs.Build(keg, i, parts);
                        float m = float.MaxValue;
                        foreach (var p in parts) foreach (var g in lap) m = Math.Min(m, Vector3.Distance(p.Position, g.Position) - g.Radius);
                        if (GrizzlyTimeKegs.HasChalk(i))
                        {
                            GrizzlyTimeKegs.Chalk(keg, lap, parts);
                            float c = float.MaxValue;
                            foreach (var p in parts)
                            {
                                foreach (var g in lap) m = Math.Min(m, Vector3.Distance(p.Position, g.Position) - g.Radius);
                                for (int r = 0; r < n; r++) c = Math.Min(c, SegmentDistance(p.Position, lap[r].Position, lap[(r + 1) % n].Position));
                            }
                            chalkLine.Add(c);
                            chalkDots.Add(parts.Count);
                        }
                        mouthClear.Add(m);
                        Vector3 prev = lap[(keg.Ring - 1 + n) % n].Position, at = lap[keg.Ring].Position, next = lap[(keg.Ring + 1) % n].Position;
                        if (RaceCourseGeometry.Angle(at - prev, next - at) > 60f)
                        {
                            Vector3 ap = (keg.Centre - prev).normalized;
                            waste.Add(Vector3.Distance(keg.Centre - ap * 60f + ap * carry, next) - Vector3.Distance(GrizzlyTimeKegs.ThrowLanding(keg), next));
                        }
                    }
                    Vector3 f0 = lap[n - 1].Position;
                    float tr = Chain(kegs, ref f0);
                    tr += Chain(kegs, ref f0);
                    reader.Add(tr);
                }
                Console.WriteLine($"I{i}: mouth {GrizzlyTimeCourse.ForIntensity(i).RingRadius:F0} u, credit {GrizzlyTimeKegs.CreditRadius(i):F0} u, " +
                                  $"regrow {GrizzlyTimeKegs.RegrowSeconds(i):F1} s, core {GrizzlyTimeKegs.CoreKind(i)}, prisms/lap {GrizzlyTimeKegs.PrismsPerLap(i)}");
                Console.WriteLine($"    legs p10 {Pct(legs, 0.1f):F0} median {Pct(legs, 0.5f):F0} p90 {Pct(legs, 0.9f):F0} u; corners over 60 deg: {deflect.Count(d => d > 60f) / (float)deflect.Count:P0}");
                Console.WriteLine($"    nearest keg/chalk prism outside a mouth: min {mouthClear.Min():F0} u, p10 {Pct(mouthClear, 0.1f):F0} u");
                if (chalkLine.Count > 0)
                    Console.WriteLine($"    chalk dots a lane keeps (of {GrizzlyTimeKegs.ChalkPerKeg}): all {chalkDots.Count(c => c == GrizzlyTimeKegs.ChalkPerKeg) / (float)chalkDots.Count:P1}, fewest {chalkDots.Min()}");
                if (chalkLine.Count > 0)
                    Console.WriteLine($"    chalk to the nearest ring-to-ring line: min {chalkLine.Min():F0} u, p10 {Pct(chalkLine, 0.1f):F0} u, median {Pct(chalkLine, 0.5f):F0} u");
                if (waste.Count > 0)
                    Console.WriteLine($"    straight blow vs bank shot at corners over 60 deg: lands min {waste.Min():F0} u, median {Pct(waste, 0.5f):F0} u farther from the next ring");
                Console.WriteLine($"    throw chain, 2 laps (model: one launch a gate, no carry bomb): median {Pct(reader, 0.5f):F1} s, p90 {Pct(reader, 0.9f):F1} s");
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
