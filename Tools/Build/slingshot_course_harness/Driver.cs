// Runs the SHIPPED HeadlongCircuit solver with SlingshotCourse's settings over 400 seeds per
// intensity, in the race shell the scene authors (480..1080), and checks what a Stoat can fly:
// eight gates, gate 0 on the spawn ring's pole, every gate inside the shell, no corner under the
// safety floor or under twice the cruise pivot. Prints the measured ladder (corners, legs, lap
// time at cruise and slung) that SLINGSHOT.md quotes.
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Driver
{
    const float Inner = 480f, Outer = 1080f;
    static float Corner(List<RaceGate> g, int i) { int n = g.Count; return RaceCourseGeometry.CornerRadius(g[(i - 1 + n) % n].Position, g[i].Position, g[(i + 1) % n].Position); }
    static float Median(List<float> v) { var s = v.OrderBy(x => x).ToList(); return s.Count == 0 ? 0 : s[s.Count / 2]; }

    public static int Main()
    {
        int failures = 0;
        Console.WriteLine($"Stoat: cruise {SlingshotCourse.SpeedAtSling(0):F0} u/s on {SlingshotCourse.CruiseRadius:F1} u, slung {SlingshotCourse.SpeedAtSling(1):F0} u/s on {SlingshotCourse.FullSlingRadius:F1} u");
        for (int i = 1; i <= 4; i++)
        {
            var mins = new List<float>(); var legs = new List<float>(); var laps = new List<float>();
            int underTwoSlung = 0, courses = 0;
            for (int k = 0; k < 400; k++)
            {
                var s = SlingshotCourse.ForIntensity(i);
                s.InnerRadius = Inner; s.OuterRadius = Outer; s.FirstGateDirection = Vector3.up;
                var gates = HeadlongCircuit.Generate(1000 + k * 7919 + i, s);
                courses++;
                if (gates.Count != SlingshotCourse.GatesPerLap) { failures++; Console.Error.WriteLine($"i{i} k{k}: {gates.Count} gates"); continue; }
                if (Vector3.Angle(gates[0].Position, Vector3.up) > 0.5f) { failures++; Console.Error.WriteLine($"i{i} k{k}: gate 0 off the pole"); }
                float lap = 0f, minC = float.MaxValue;
                for (int g = 0; g < gates.Count; g++)
                {
                    float r = gates[g].Position.magnitude;
                    if (r < Inner - 1f || r > Outer + 1f) { failures++; Console.Error.WriteLine($"i{i} k{k} gate {g}: radius {r:F0} outside the shell"); }
                    float c = Corner(gates, g);
                    minC = Mathf.Min(minC, c);
                    if (c < 2f * SlingshotCourse.FullSlingRadius) underTwoSlung++;
                    float leg = (gates[(g + 1) % gates.Count].Position - gates[g].Position).magnitude;
                    legs.Add(leg); lap += leg;
                }
                if (minC < 2f * SlingshotCourse.CruiseRadius) { failures++; Console.Error.WriteLine($"i{i} k{k}: corner {minC:F0} u under twice the cruise pivot"); }
                if (minC < s.CornerFloorRadius * 0.98f) { failures++; Console.Error.WriteLine($"i{i} k{k}: corner {minC:F0} u under the floor {s.CornerFloorRadius:F0}"); }
                mins.Add(minC); laps.Add(lap);
            }
            var st = SlingshotCourse.ForIntensity(i);
            Console.WriteLine($"intensity {i}: mouth {st.RingRadius:F0} u, floor {st.CornerFloorRadius:F0} u | tightest corner median {Median(mins):F0} u (min {mins.Min():F0}) | " +
                              $"corners under 2x the slung circle: {underTwoSlung / (float)courses:F2} per lap | legs {legs.Min():F0}..{Median(legs):F0}..{legs.Max():F0} u | " +
                              $"lap {Median(laps):F0} u = {Median(laps) / SlingshotCourse.SpeedAtSling(0):F0} s cruise, {Median(laps) / SlingshotCourse.SpeedAtSling(1):F0} s slung");
        }
        Console.WriteLine(failures == 0 ? "slingshot course harness: OK" : $"slingshot course harness: {failures} FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
