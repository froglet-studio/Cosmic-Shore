using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Driver
{
    static float Corner(List<RaceGate> g, int i) { int n = g.Count; return RaceCourseGeometry.CornerRadius(g[(i-1+n)%n].Position, g[i].Position, g[(i+1)%n].Position); }

    public static int Main(string[] args)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("{\n  \"intensities\": {\n");
        int failures = 0;
        var rs = RegattaCourse.DefaultRails;
        for (int i = 1; i <= 4; i++)
        {
            int seed = RegattaCourse.SeedForIntensity(RegattaCourse.DefaultSeed, i);
            var gates = RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, i);
            var rails = RegattaCourse.BuildRails(gates, rs);
            var s = RegattaCourse.ForIntensity(i);
            int n = gates.Count;
            var corners = new List<float>(); var legs = new List<float>();
            for (int k = 0; k < n; k++) { corners.Add(Corner(gates, k)); legs.Add((gates[(k+1)%n].Position - gates[k].Position).magnitude); }
            // gate coverage check, mirrors RegattaCourseTests
            for (int gi = 0; gi < n; gi++) for (int lane = 0; lane < rails.LanePositions.Count; lane++)
            {
                float bestDist = float.MaxValue, bestPlane = 0f, lat = 0f;
                foreach (var p in rails.LanePositions[lane]) { var d = p - gates[gi].Position; float dist = d.magnitude; if (dist < bestDist) { bestDist = dist; float along = Vector3.Dot(d, gates[gi].Axis); bestPlane = Mathf.Abs(along); lat = (d - gates[gi].Axis * along).magnitude; } }
                if (bestPlane > rs.PrismSpacing || Mathf.Abs(lat - rs.LaneOffset) > rs.PrismSpacing || lat + 6f > gates[gi].Radius) { failures++; Console.Error.WriteLine($"i{i} gate {gi} lane {lane}: plane {bestPlane:F2} lateral {lat:F2}"); }
            }
            float minSep = float.MaxValue;
            for (int a = 0; a < 3; a++) for (int b = a+1; b < 3; b++) { var pa = rails.LanePositions[a]; var pb = rails.LanePositions[b]; for (int x = 0; x < pa.Length; x += 2) for (int y = 0; y < pb.Length; y += 2) minSep = Mathf.Min(minSep, (pa[x]-pb[y]).magnitude); }
            float maxStepErr = 0f; foreach (var lp in rails.LanePositions) for (int k = 0; k < lp.Length; k++) maxStepErr = Mathf.Max(maxStepErr, Mathf.Abs((lp[(k+1)%lp.Length]-lp[k]).magnitude - rs.PrismSpacing));
            sb.Append($"    \"{i}\": {{\n");
            sb.Append($"      \"seed\": {seed},\n      \"rings\": {n},\n      \"ringRadius\": {s.RingRadius.ToString(ci)},\n      \"cornerFloor\": {s.CornerFloorRadius.ToString(ci)},\n");
            sb.Append($"      \"spineLength\": {rails.SpineLength.ToString("F1", ci)},\n      \"laneLengths\": [{string.Join(", ", rails.LaneLengths.ConvertAll(v => v.ToString("F1", ci)))}],\n");
            sb.Append($"      \"prismCount\": {rails.PrismCount},\n      \"minLaneSeparation\": {minSep.ToString("F2", ci)},\n      \"maxSpacingError\": {maxStepErr.ToString("F3", ci)},\n");
            sb.Append($"      \"cornerRadii\": [{string.Join(", ", corners.ConvertAll(v => (float.IsInfinity(v) ? 1e9f : v).ToString("F1", ci)))}],\n");
            sb.Append($"      \"legLengths\": [{string.Join(", ", legs.ConvertAll(v => v.ToString("F1", ci)))}],\n");
            sb.Append($"      \"gate0\": [{gates[0].Position.x.ToString("F2", ci)}, {gates[0].Position.y.ToString("F2", ci)}, {gates[0].Position.z.ToString("F2", ci)}]\n");
            sb.Append(i < 4 ? "    },\n" : "    }\n");
            Console.Error.WriteLine($"i{i}: seed {seed} spine {rails.SpineLength:F0} lanes {string.Join("/", rails.LaneLengths.ConvertAll(v=>v.ToString("F0")))} prisms {rails.PrismCount} minSep {minSep:F1} stepErr {maxStepErr:F3} corners {string.Join(",", corners.ConvertAll(v=>v.ToString("F0")))}");
        }
        sb.Append("  }\n}\n");
        // The sweep the editor tests run, mirrored: 60 seeds x 4 intensities.
        int sweepFail = 0; float worstSpread = 0f, worstStep = 0f;
        for (int i = 1; i <= 4; i++) for (int k = 1; k <= 60; k++)
        {
            int seed = RegattaCourse.SeedForIntensity(RegattaCourse.DefaultSeed + k * 104729, i);
            var gates = HeadlongCircuit.Generate(seed, RegattaCourse.ForIntensity(i));
            var rails = RegattaCourse.BuildRails(gates, rs);
            if (gates.Count != 8) { sweepFail++; Console.Error.WriteLine($"sweep i{i} k{k}: {gates.Count} gates"); }
            if (Vector3.Angle(gates[0].Position, Vector3.up) > 0.5f) { sweepFail++; Console.Error.WriteLine($"sweep i{i} k{k}: gate0 off pole"); }
            float mn = float.MaxValue, mx = 0f; foreach (var l in rails.LaneLengths) { mn = Mathf.Min(mn, l); mx = Mathf.Max(mx, l); }
            worstSpread = Mathf.Max(worstSpread, (mx-mn)/mn);
            if ((mx-mn)/mn > 0.03f) { sweepFail++; Console.Error.WriteLine($"sweep i{i} k{k}: lane spread {(mx-mn)/mn:P1}"); }
            foreach (var lp in rails.LanePositions) for (int q = 0; q < lp.Length; q++) { float e = Mathf.Abs((lp[(q+1)%lp.Length]-lp[q]).magnitude - rs.PrismSpacing); worstStep = Mathf.Max(worstStep, e); if (e > rs.PrismSpacing*0.25f) { sweepFail++; Console.Error.WriteLine($"sweep i{i} k{k}: step err {e:F2}"); break; } }
            for (int gi = 0; gi < gates.Count; gi++) for (int lane = 0; lane < 3; lane++)
            {
                float bestDist = float.MaxValue, bestPlane = 0f, lat = 0f;
                foreach (var p in rails.LanePositions[lane]) { var dd = p - gates[gi].Position; float dist = dd.magnitude; if (dist < bestDist) { bestDist = dist; float along = Vector3.Dot(dd, gates[gi].Axis); bestPlane = Mathf.Abs(along); lat = (dd - gates[gi].Axis * along).magnitude; } }
                if (bestPlane > rs.PrismSpacing || Mathf.Abs(lat - rs.LaneOffset) > rs.PrismSpacing || lat + 6f > gates[gi].Radius) { sweepFail++; Console.Error.WriteLine($"sweep i{i} k{k} gate {gi} lane {lane}: plane {bestPlane:F2} lateral {lat:F2}"); }
            }
        }
        Console.Error.WriteLine($"sweep: {(sweepFail == 0 ? "OK" : sweepFail + " FAILURES")} worst lane spread {worstSpread:P2} worst step err {worstStep:F3}");
        failures += sweepFail;
        Console.Out.Write(sb.ToString());
        Console.Error.WriteLine(failures == 0 ? "gate coverage: OK" : $"gate coverage: {failures} FAILURES");
        return failures == 0 ? 0 : 1;
    }
}
