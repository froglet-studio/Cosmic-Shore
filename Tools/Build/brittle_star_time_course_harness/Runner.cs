// Runs every [Test] of the compiled SHIPPED BrittleStarTimeCourseTests by reflection and prints a pass/fail line per test.
// Exit 0 = all passed. Two more modes read the shipped C#, never a copy:
//   --sweep                      each rung's movement numbers (what BrittleStarTimeCourse and BRITTLESTARTIME.md state)
//   --gates <intensity> <seed>   one JSON line: that lap's rings (position, axis, mouth) - what the pictures are drawn from
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

    static List<RaceGate> Lap(int intensity, int seed)
    {
        if (BrittleStarTimeCourse.BuildLap(intensity, seed, Inner, Outer, BrittleStarTimeCourse.GatesPerLap, out var lap))
            return lap;
        var s = BrittleStarTimeCourse.Gearbox();
        s.InnerRadius = Inner;
        s.OuterRadius = Outer;
        s.FirstGateDirection = Vector3.up;
        return HeadlongCircuit.Generate(seed, s);
    }

    static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--gates")
        {
            int intensity = int.Parse(args[1]), seed = int.Parse(args[2]);
            var c = Lap(intensity, seed);
            Console.WriteLine($"{{\"intensity\":{intensity},\"seed\":{seed},\"gates\":[" +
                string.Join(",", c.Select(g => $"{{\"p\":{V(g.Position)},\"a\":{V(g.Axis)},\"r\":{F(g.Radius)}}}")) + "]}");
            return 0;
        }
        if (args.Length > 0 && args[0] == "--sweep")
        {
            float cruise = BrittleStarTimeCourse.CruiseRadius, speed = BrittleStarTimeCourse.SpeedModeRadius;
            Console.WriteLine($"cruise circle {cruise:F0} u at {BrittleStarTimeCourse.CruiseSpeed:F0} u/s; Speed Mode {speed:F0} u at " +
                              $"{BrittleStarTimeCourse.SpeedModeSpeed:F0} u/s; at Time 5 {BrittleStarTimeCourse.SpeedModeRadiusAtTime5:F0} u at " +
                              $"{BrittleStarTimeCourse.SpeedModeSpeedAtTime5:F0} u/s, rolling and pulling {BrittleStarTimeCourse.SpeedRollRadiusAtTime5:F0} u");
            var tight = Enumerable.Range(1, 400).Select(seed => { var c = Lap(1, seed); int n = c.Count;
                return Enumerable.Range(0, n).Count(k => RaceCourseGeometry.CornerRadius(c[(k - 1 + n) % n].Position, c[k].Position, c[(k + 1) % n].Position) < speed); })
                .OrderBy(x => x).ToList();
            Console.WriteLine($"I1 Gearbox: median {tight[tight.Count / 2]} corners a lap tighter than Speed Mode's circle (400 seeds)");
            float m2 = BrittleStarTimeCourse.MouthRadius(2);
            float rim = BrittleStarTimeCourse.SlalomBendRadius(m2), half = BrittleStarTimeCourse.SlalomBendRadius(m2 / 2), centre = BrittleStarTimeCourse.SlalomBendRadius(0);
            Console.WriteLine($"I2 Feather slalom: bend {centre:F0} u through the centres, {half:F0} u through the half-mouth line " +
                              $"(held at {BrittleStarTimeCourse.SpeedForRadius(half):F0} u/s), {rim:F0} u brushing the rims " +
                              $"(held at {BrittleStarTimeCourse.SpeedForRadius(rim):F0} u/s)");
            float fly = BrittleStarTimeCourse.HairpinFlyableRadius(BrittleStarTimeCourse.MouthRadius(3));
            float apex = BrittleStarTimeCourse.SpeedForRadius(fly);
            Console.WriteLine($"I3 Coast-in hairpins: widest line {fly:F0} u, held at {apex:F0} u/s; let go of Speed Mode " +
                              $"{BrittleStarTimeCourse.CoastDistance(BrittleStarTimeCourse.SpeedModeSpeed, apex):F0} u before the turn " +
                              $"({BrittleStarTimeCourse.HairpinStraight:F0} u straights)");
            float m4 = BrittleStarTimeCourse.MouthRadius(4);
            Console.WriteLine($"I4 Corkscrew: bend {BrittleStarTimeCourse.CoilBendRadius(0):F0} u through the centres, " +
                              $"{BrittleStarTimeCourse.CoilBendRadius(m4):F0} u brushing the rims; Speed Mode at Time 5 needs " +
                              $"{BrittleStarTimeCourse.SpeedModeRadiusAtTime5:F0} u, rolling {BrittleStarTimeCourse.SpeedRollRadiusAtTime5:F0} u");
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
