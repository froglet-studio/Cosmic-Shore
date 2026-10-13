// Runs the SHIPPED ThresherChainSolver (compiled from its real path) through the scripted plant cycles in
// scenarios.json and prints one JSON trace per scenario: the reference the Thresher Plant Lab's JS port is
// held to by parity.cjs. The pilot is the chain harness's stand-in (Tools/Build/thresher_chain_harness/
// Driver.cs, class Sim): speed eases toward cruise at 1.5/s, yaw turns the heading, and while planted the
// heading is the solver's velocity.
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CosmicShore.Gameplay;
using UnityEngine;

public static class TraceDriver
{
    const float Dt = 1f / 60f;

    public static int Main(string[] args)
    {
        var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
        var s = new ThresherDials().ToSettings();
        var sb = new StringBuilder("{");
        bool firstScenario = true;
        foreach (var sc in doc.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            var solver = new ThresherChainSolver(s);
            Vector3 p = Vector3.zero, f = Vector3.forward;
            float v = s.CruiseSpeed, cruise = s.CruiseSpeed;
            solver.Reset(p, f, f * v);
            Vector3 last = f * v;
            if (!firstScenario) sb.Append(',');
            firstScenario = false;
            sb.Append('"').Append(sc.GetProperty("name").GetString()).Append("\":[");
            bool firstFrame = true;
            foreach (var st in sc.GetProperty("steps").EnumerateArray())
            {
                float yaw = st.GetProperty("yaw").GetSingle();
                bool pay = st.GetProperty("pay").GetBoolean();
                if (st.TryGetProperty("plant", out var pl) && pl.GetBoolean()) solver.Plant();
                if (st.TryGetProperty("release", out var rl) && rl.GetBoolean()) solver.Release(last);
                if (st.TryGetProperty("arm", out var arm))
                    solver.ArmLaunch(new Vector3(arm[0].GetSingle(), arm[1].GetSingle(), arm[2].GetSingle()));
                int n = Mathf.RoundToInt(st.GetProperty("t").GetSingle() / Dt);
                for (int i = 0; i < n; i++)
                {
                    f = Rot(f, yaw * Dt);
                    float th = v + (cruise - v) * 1.5f * Dt;
                    var r = solver.Step(p, f * th, pay, cruise, Dt);
                    if (solver.Mode == ThresherMode.Pivot) f = r.ShipVelocity.normalized;
                    else if (r.Launched) f = r.ShipVelocity.normalized;
                    v = r.ShipVelocity.magnitude; p += r.ShipVelocity * Dt; last = r.ShipVelocity;
                    if (!firstFrame) sb.Append(',');
                    firstFrame = false;
                    sb.Append('[').Append((int)solver.Mode).Append(',')
                      .Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',')
                      .Append(F(solver.BallPosition.x)).Append(',').Append(F(solver.BallPosition.y)).Append(',').Append(F(solver.BallPosition.z)).Append(',')
                      .Append(F(solver.BallSpeed)).Append(',').Append(F(solver.Length)).Append(']');
                }
            }
            sb.Append(']');
        }
        sb.Append('}');
        Console.WriteLine(sb.ToString());
        return 0;
    }

    static string F(float x) => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    static Vector3 Rot(Vector3 v, float deg)
    {
        float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
        return new Vector3(v.x * c + v.z * sn, v.y, -v.x * sn + v.z * c);
    }
}
