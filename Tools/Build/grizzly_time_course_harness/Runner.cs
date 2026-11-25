// Runs every [Test] of the compiled SHIPPED Grizzly Time tests (GrizzlyTimeCourseTests, GrizzlyTimeKegsTests) by
// reflection and prints a pass/fail line per test. Exit 0 = all passed. One more mode reads the shipped C#, never a copy:
//   --sweep   the powder-keg ladder per intensity over 400 seeds: prisms, clearances, the blow point (on the keg's exit
//             side), the bank shot's worth, and the throw chain (each gate's keg taken from where the last throw
//             landed), without a Rush and with one on every straight the Energy allows.
// The throw chain is a MODEL (cruise in straight lines to the blow point, one full carry, cruise on; no turning, no
// threading). It is not the hull and not the AI; the real race is measured in the editor (POWDER_KEGS_SPEC.md section 9).
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

    // The Rush, as MODEL numbers (the shipped values on 2026-11-20; only the ram's radius and seconds are read back by a
    // test, because only they decide anything in the game): a burst of 80 u/s for 1 s under the transformer's 100 u/s
    // ceiling carries about 76 u past cruise; Energy starts at 0, gains 0.05/s and a Rush costs 0.25 (one per 5 s);
    // 2 s between two.
    const float RushCarry = 76f, RushSeconds = 1f, RushCost = 0.25f, EnergyPerSecond = 0.05f, EnergyMax = 1f, RushCooldown = 2f;

    /// <summary>The same chain with a Rush on every straight the Energy allows (the AI's rule:
    /// <see cref="GrizzlyTimeKegs.AIRushMinRun"/> u of straight left, never during the launch). <paramref name="t"/>,
    /// <paramref name="energy"/> and <paramref name="lastRush"/> run on across laps; returns the Rushes spent.</summary>
    static int ChainWithRush(List<GrizzlyTimeKegs.Keg> kegs, ref Vector3 from, ref float t, ref float energy, ref float lastRush)
    {
        float cruise = GrizzlyTimeCourse.GrizzlyThrottleScaler;
        int rushes = 0;
        foreach (var k in kegs)
        {
            float left = Vector3.Distance(from, GrizzlyTimeKegs.BlowPoint(k));
            while (left > 0f)
            {
                float wait = Math.Max(Math.Max(0f, (RushCost - energy) / EnergyPerSecond), lastRush + RushCooldown - t);
                if (left - wait * cruise < GrizzlyTimeKegs.AIRushMinRun)
                {
                    float dt = left / cruise;
                    t += dt; energy = Math.Min(EnergyMax, energy + EnergyPerSecond * dt); left = 0f;
                    break;
                }
                t += wait; energy = Math.Min(EnergyMax, energy + EnergyPerSecond * wait); left -= wait * cruise;
                energy -= RushCost; lastRush = t; rushes++;
                t += RushSeconds; energy = Math.Min(EnergyMax, energy + EnergyPerSecond * RushSeconds);
                left -= cruise * RushSeconds + RushCarry;
            }
            t += GrizzlyTimeCourse.ImpulseDuration;
            energy = Math.Min(EnergyMax, energy + EnergyPerSecond * GrizzlyTimeCourse.ImpulseDuration);
            from = GrizzlyTimeKegs.ThrowLanding(k);
        }
        return rushes;
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
                var rusher = new List<float>(); var rushCount = new List<float>();
                var standClear = new List<float>(); var lands = new List<float>();
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
                        float sc = float.MaxValue;
                        foreach (var g in lap) sc = Math.Min(sc, Vector3.Distance(GrizzlyTimeKegs.BlowPoint(keg), g.Position) - g.Radius);
                        standClear.Add(sc);
                        // Signed along the exit lane (which points at the next ring's centre): + short of it, - past it.
                        lands.Add(Vector3.Dot(next - GrizzlyTimeKegs.ThrowLanding(keg), keg.ExitDir));
                        if (RaceCourseGeometry.Angle(at - prev, next - at) > 60f)
                        {
                            // The straight blow: fly on past the keg the way you came in and blow it from there.
                            Vector3 ap = (keg.Centre - prev).normalized;
                            waste.Add(Vector3.Distance(keg.Centre + ap * (GrizzlyTimeKegs.BlowStandOff + carry), next) - Vector3.Distance(GrizzlyTimeKegs.ThrowLanding(keg), next));
                        }
                    }
                    Vector3 f0 = lap[n - 1].Position;
                    float tr = Chain(kegs, ref f0);
                    tr += Chain(kegs, ref f0);
                    reader.Add(tr);
                    Vector3 f1 = lap[n - 1].Position;
                    float t1 = 0f, energy = 0f, lastRush = -RushCooldown;
                    int spent = ChainWithRush(kegs, ref f1, ref t1, ref energy, ref lastRush);
                    spent += ChainWithRush(kegs, ref f1, ref t1, ref energy, ref lastRush);
                    rusher.Add(t1); rushCount.Add(spent);
                }
                Console.WriteLine($"I{i}: mouth {GrizzlyTimeCourse.ForIntensity(i).RingRadius:F0} u, credit {GrizzlyTimeKegs.CreditRadius(i):F0} u, " +
                                  $"regrow {GrizzlyTimeKegs.RegrowSeconds(i):F1} s, core {GrizzlyTimeKegs.CoreKind(i)}, prisms/lap {GrizzlyTimeKegs.PrismsPerLap(i)}");
                Console.WriteLine($"    legs p10 {Pct(legs, 0.1f):F0} median {Pct(legs, 0.5f):F0} p90 {Pct(legs, 0.9f):F0} u; corners over 60 deg: {deflect.Count(d => d > 60f) / (float)deflect.Count:P0}");
                Console.WriteLine($"    nearest keg/chalk prism outside a mouth: min {mouthClear.Min():F0} u, p10 {Pct(mouthClear, 0.1f):F0} u");
                if (chalkLine.Count > 0)
                    Console.WriteLine($"    chalk dots a lane keeps (of {GrizzlyTimeKegs.ChalkPerKeg}): all {chalkDots.Count(c => c == GrizzlyTimeKegs.ChalkPerKeg) / (float)chalkDots.Count:P1}, fewest {chalkDots.Min()}");
                if (chalkLine.Count > 0)
                    Console.WriteLine($"    chalk to the nearest ring-to-ring line: min {chalkLine.Min():F0} u, p10 {Pct(chalkLine, 0.1f):F0} u, median {Pct(chalkLine, 0.5f):F0} u");
                Console.WriteLine($"    blow point (exit side, {GrizzlyTimeKegs.BlowStandOff:F0} u out) outside the nearest mouth: min {standClear.Min():F0} u, p10 {Pct(standClear, 0.1f):F0} u; " +
                                  $"a full throw ends short (+) or past (-) the next ring's centre: p10 {Pct(lands, 0.1f):F0} u, median {Pct(lands, 0.5f):F0} u, p90 {Pct(lands, 0.9f):F0} u; " +
                                  $"{lands.Count(l => l < 0f) / (float)lands.Count:P1} of throws end past it");
                if (waste.Count > 0)
                    Console.WriteLine($"    straight blow vs bank shot at corners over 60 deg: lands min {waste.Min():F0} u, median {Pct(waste, 0.5f):F0} u farther from the next ring");
                Console.WriteLine($"    throw chain, 2 laps (model: one launch a gate, no carry bomb): median {Pct(reader, 0.5f):F1} s, p90 {Pct(reader, 0.9f):F1} s");
                Console.WriteLine($"    the same with a Rush on every straight the Energy allows: median {Pct(rusher, 0.5f):F1} s, p90 {Pct(rusher, 0.9f):F1} s, {Pct(rushCount, 0.5f):F0} Rushes a race");
            }
            // The I4 ruins (GrizzlyTimeRuins): what Spearline's generator lays on this circuit, and what is left once
            // the Grizzly's throw lanes, keg lines and keg spaces are cut out of it.
            var rawCount = new List<float>(); var kept = new List<float>(); var ruinLegs = new List<float>();
            var wheelCount = new List<float>(); var rawWheelCount = new List<float>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Circuit(4, seed);
                var raw = new List<SpearlineRuinPart>(); var rawWheels = new List<SpearlineRuinWheel>();
                SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(4), raw, rawWheels);
                var ruins = new List<SpearlineRuinPart>(); var wheels = new List<SpearlineRuinWheel>();
                GrizzlyTimeRuins.Lay(lap, 4, ruins, wheels);
                rawCount.Add(raw.Count); kept.Add(ruins.Count);
                ruinLegs.Add(ruins.Select(p => p.Leg).Distinct().Count());
                wheelCount.Add(wheels.Count); rawWheelCount.Add(rawWheels.Count);
            }
            Console.WriteLine($"I4 ruins: Spearline lays median {Pct(rawCount, 0.5f):F0} prisms a lap; kept min {kept.Min():F0}, median {Pct(kept, 0.5f):F0}, max {kept.Max():F0} " +
                              $"(mean {kept.Average():F0}); ruins a lap min {ruinLegs.Min():F0}, mean {ruinLegs.Average():F1}; wheels {wheelCount.Sum():F0} of {rawWheelCount.Sum():F0} kept over {Seeds} laps");
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
