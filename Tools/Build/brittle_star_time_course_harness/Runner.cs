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

    // --ruins [N]: the I4 garden ruins over N seeds (BRITTLESTARTIME.md section 0a) - what stands on each course, and
    // how near the model's pilots (the coached AIs and a reading human) come to a standing ruin prism while they race.
    static int Ruins(int seeds)
    {
        const int intensity = 4;
        float top = RaceSweep.TopSpeed(intensity);
        string[] kindNames = { "pergola", "flower head", "vines", "wall", "tunnel", "maze", "wheel" };
        var kinds = new int[SpearlineRuins.KindCount];
        int total = 0, min = int.MaxValue, max = 0, courses = 0, withWheel = 0, twoOrMore = 0;
        var nearest = new float[5];
        var hits = new int[5];
        for (int i = 0; i < nearest.Length; i++) nearest[i] = float.MaxValue;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var course = Lap(intensity, seed);
            var posts = BrittleStarTimeLine.Marks(course, top);
            var parts = new List<SpearlineRuinPart>();
            var wheels = new List<SpearlineRuinWheel>();
            var lap = BrittleStarTimeRuins.Lay(course, intensity, BrittleStarTimeCourse.CornerRadiusAt(top), posts, parts, wheels);
            if (parts.Count == 0) { Console.WriteLine($"seed {seed}: no ruin"); continue; }
            courses++;
            total += parts.Count; min = Math.Min(min, parts.Count); max = Math.Max(max, parts.Count);
            var legs = parts.Select(p => p.Leg).Distinct().ToList();
            if (legs.Count > 1) twoOrMore++;
            foreach (int leg in legs)
                kinds[wheels.Any(w => parts[w.First].Leg == leg) ? SpearlineRuins.Wheel : SpearlineRuins.KindOf(leg, lap[0].Position)]++;
            if (wheels.Count > 0) withWheel++;
            var standing = new List<SpearlineRuinPart>();
            for (int i = 0; i < parts.Count; i++)
                if (!wheels.Any(w => i >= w.First && i < w.First + w.Count)) standing.Add(parts[i]);
            if (standing.Count == 0) continue;
            for (int pilot = 0; pilot < 5; pilot++)
            {
                FlightSim.Options o;
                FlightSim.SpeedPolicy policy;
                if (pilot < 4)
                {
                    var skill = (BrittleStarTimeCoach.Skill)pilot;
                    o = RaceSweep.Autopilot(intensity, FlightSim.Steering.Line);
                    o.NoseFollow = BrittleStarTimeCoach.NoseFollowFor(intensity, skill);
                    policy = RaceSweep.Coach(intensity, 0, skill);
                }
                else
                {
                    o = RaceSweep.Human(intensity, FlightSim.Steering.Line);
                    policy = (in FlightSim.State s, IReadOnlyList<RaceGate> l, int r) => s.AimAngle <= 25f;
                }
                o.Laps = 2;
                float best = float.MaxValue;
                o.Trace = pos =>
                {
                    foreach (var part in standing)
                    {
                        if ((part.Position - pos).sqrMagnitude > 200f * 200f) continue;
                        Vector3 half = part.Forward * (0.5f * part.Scale.z);
                        Vector3 a = part.Position - half, ab = half * 2f;
                        float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                        // Clearance to the prism's skin, taken as a rod as thick as its cross-section.
                        float d = (pos - (a + ab * t)).magnitude - 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
                        if (d < best) best = d;
                    }
                };
                FlightSim.Fly(course, policy, o);
                nearest[pilot] = Math.Min(nearest[pilot], best);
                if (best < 8f) hits[pilot]++;
            }
        }
        Console.WriteLine($"I4 ruins over {seeds} seeds: {courses} courses carry one, {twoOrMore} carry two or more; " +
                          $"{(courses > 0 ? total / (float)courses : 0f):F0} prisms a course (min {min}, max {max}); a wheel on {withWheel}");
        Console.WriteLine("kinds: " + string.Join(", ", kinds.Select((c, k) => $"{kindNames[k]} {c}")));
        string[] pilots = { "Easy AI", "Medium AI", "Hard AI", "Legendary AI", "reading human" };
        for (int p = 0; p < 5; p++)
            Console.WriteLine($"  {pilots[p]}: nearest a standing ruin prism {nearest[p]:F0} u; courses flown within 8 u of one: {hits[p]}");
        return 0;
    }

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
        if (args.Length > 0 && args[0] == "--ruins") return Ruins(args.Length > 1 ? int.Parse(args[1]) : 40);
        if (args.Length > 0 && args[0] == "--race") return RaceSweep.Run(args);
        if (args.Length > 0 && args[0] == "--tune") return RaceSweep.Tune(args);
        if (args.Length > 0 && args[0] == "--check-race") return RaceSweep.CheckRace(args);
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
