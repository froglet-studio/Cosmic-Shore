// Runs the SHIPPED ThresherChainSolverTests (compiled from its real path) and prints the feel table
// the assertions are drawn from. Exit 1 on any failure, so it is usable as a gate.
using System;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using CosmicShore.Tests;
using NUnit.Framework;
using UnityEngine;

public static class Driver
{
    const float Dt = 1f / 60f;

    public static int Main(string[] args)
    {
        int failed = 0, passed = 0;
        foreach (var m in typeof(ThresherChainSolverTests).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var cases = m.GetCustomAttributes<TestCaseAttribute>().ToList();
            bool isTest = m.GetCustomAttribute<TestAttribute>() != null;
            if (!isTest && cases.Count == 0) continue;
            var runs = cases.Count > 0 ? cases.Select(c => c.Args).ToList() : new() { Array.Empty<object>() };
            foreach (var a in runs)
            {
                string name = m.Name + (a.Length > 0 ? "(" + string.Join(",", a) + ")" : "");
                try
                {
                    var args2 = m.GetParameters().Select((p, i) => Convert.ChangeType(a[i], p.ParameterType)).ToArray();
                    m.Invoke(new ThresherChainSolverTests(), args2);
                    passed++; Console.WriteLine("PASS " + name);
                }
                catch (TargetInvocationException e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.InnerException?.Message); }
            }
        }

        FeelTable();
        Console.WriteLine($"\n{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- feel table

    static void FeelTable()
    {
        var s = new ThresherDials().ToSettings();
        Console.WriteLine($"\nscale {new ThresherDials().Scale:F4}  cruise {s.CruiseSpeed}  smash {s.SmashSpeed:F1}  white-hot {s.WhiteHotSpeed:F1}  " +
                          $"rest {s.RestLength:F1}  max {s.MaxLength:F1}  ballR {s.BallRadius:F2}  maxBall {s.MaxBallSpeed:F1}");

        Console.WriteLine("\nsteady turn, 4 s — peak ball speed (x smash)");
        Console.WriteLine("  deg/s   reeled   let-out");
        foreach (float rate in new[] { 15f, 30f, 60f, 90f, 120f })
            Console.WriteLine($"  {rate,5}   {Turn(s, rate, false) / s.SmashSpeed,6:F2}   {Turn(s, rate, true) / s.SmashSpeed,6:F2}");

        Console.WriteLine("\nwind-up, turn T s at 120 deg/s, snap back 0.1 s, release — peak (x smash) before / after reel");
        foreach (float t in new[] { 0.3f, 0.5f, 0.8f, 1.2f })
        {
            var (pre, post) = Snap(s, t, -120f);
            Console.WriteLine($"  T={t:F1}  {pre / s.SmashSpeed:F2} / {post / s.SmashSpeed:F2}");
        }
        var (np, nq) = Snap(s, 0.6f, 120f);
        Console.WriteLine($"  same turn, NO snap (keep turning), release: {np / s.SmashSpeed:F2} / {nq / s.SmashSpeed:F2}");

        Console.WriteLine("\ncamera (offset 0,8,-55; 60 deg vFOV 16:9; margin 1.15) — chase distance needed, ball at length L");
        Console.WriteLine("  where        rest    max    max x1.5 (Space)");
        float vh = 30f * Mathf.Deg2Rad, hh = Mathf.Atan(Mathf.Tan(vh) * 16f / 9f), rr = s.BallRadius * 1.25f * 1.45f;
        foreach (var (label, dir) in new[] { ("behind", Vector3.back), ("abeam", Vector3.right), ("ahead", Vector3.forward), ("overhead", Vector3.up) })
        {
            float D(float l) => ThresherCameraFraming.RequiredDistance(dir * l, rr, 8f, vh, hh, 1.15f, 6f);
            Console.WriteLine($"  {label,-10} {D(s.RestLength),6:F0} {D(s.MaxLength),6:F0} {D(s.MaxLength * 1.5f),8:F0}");
        }
        Console.WriteLine($"  spin rate (rad/s): plant reeled-in at cruise {ThresherCameraFraming.SpinRate(s.CruiseSpeed, s.RestLength):F2}, " +
                          $"let-out at cap {ThresherCameraFraming.SpinRate(s.LockMaxSpeed, s.MaxLength):F2}, " +
                          $"let-out at cap x1.75 (Time) {ThresherCameraFraming.SpinRate(s.LockMaxSpeed * 1.75f, s.MaxLength):F2}");

        Console.WriteLine("\nlock from cruise — orbit speed (x cruise) at t seconds, RT up / RT held");
        foreach (float t in new[] { 0.5f, 1f, 2f, 3f })
            Console.WriteLine($"  t={t:F1}  {Lock(s, t, false) / s.CruiseSpeed:F2} / {Lock(s, t, true) / s.CruiseSpeed:F2}");
    }

    static Vector3 Rot(Vector3 v, float deg)
    {
        float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
        return new Vector3(v.x * c + v.z * sn, v.y, -v.x * sn + v.z * c);
    }

    sealed class Sim
    {
        public ThresherChainSolver S; public Vector3 P, F = Vector3.forward; public float V, Peak, Cruise;
        public Sim(ThresherChainSettings st) { S = new ThresherChainSolver(st); Cruise = V = st.CruiseSpeed; S.Reset(P, F, F * V); }
        public void Fly(float sec, float yaw, bool pay)
        {
            for (int i = 0; i < Mathf.RoundToInt(sec / Dt); i++)
            {
                F = Rot(F, yaw * Dt);
                float th = V + (Cruise - V) * 1.5f * Dt;
                var r = S.Step(P, F * th, pay, Cruise, Dt);
                if (S.Mode == ThresherMode.Pivot) F = r.ShipVelocity.normalized;
                V = r.ShipVelocity.magnitude; P += r.ShipVelocity * Dt;
                Peak = Mathf.Max(Peak, S.BallSpeed);
            }
        }
    }

    static float Turn(ThresherChainSettings s, float rate, bool pay) { var m = new Sim(s); m.Fly(1f, 0, pay); m.Peak = 0; m.Fly(4f, rate, pay); return m.Peak; }
    static (float, float) Snap(ThresherChainSettings s, float t, float back)
    {
        var m = new Sim(s); m.Fly(1f, 0, true); m.Peak = 0; m.Fly(t, 120f, true); m.Fly(0.1f, back, true);
        float pre = m.Peak; m.Fly(0.5f, 0, false); return (pre, m.Peak);
    }
    static float Lock(ThresherChainSettings s, float t, bool pay) { var m = new Sim(s); m.Fly(0.5f, 0, false); m.S.Plant(); m.Fly(t, 0, pay); return m.V; }
}
