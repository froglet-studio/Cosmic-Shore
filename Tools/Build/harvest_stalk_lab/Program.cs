// Stalk lab: can a Thresher keep its ball off a sheaf's stalks while its chain mows them?
// Runs the SHIPPED ThresherChainSolver + ThresherChainLinks with the SHIPPED dials (ThresherConfig.asset), round one
// Harvest sheaf laid exactly as SpawnableHarvest lays it, with ThresherExecutor's contact rules (a prism is a sphere
// of half its box diagonal; the ball adds its radius, a chain link the cut radius). The pilot is a perfect steerer
// on a prescribed path (approach, one lap round the sheaf, then straight in through the core).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using UnityEngine;

static class Lab
{
    const string Asset = "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset";
    const float ChainCut = 0.6f;       // chainCutRadius
    const int Links = 24;              // chainLinks
    const float EnergyPerSlice = 1f, EnergyMax = 70f;
    const float ExplosionDiameter = 45f, PerUnit = 0.1f, RestCap = 4f, MinInterval = 0.08f;
    const float Dt = 1f / 60f;

    class P { public Vector3 C; public float Half; public bool Stalk, Shield, Dead; }

    static ThresherChainSettings LoadSettings()
    {
        var dials = new ThresherDials();
        bool inDials = false;
        foreach (var raw in File.ReadAllLines(Asset))
        {
            if (raw.StartsWith("  dials:")) { inDials = true; continue; }
            if (inDials && !raw.StartsWith("    ")) break;
            if (!inDials) continue;
            var kv = raw.Trim().Split(':', 2);
            var f = typeof(ThresherDials).GetField(kv[0], BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) { Console.Error.WriteLine($"no dial {kv[0]}"); continue; }
            var v = kv[1].Trim();
            if (f.FieldType == typeof(bool)) f.SetValue(dials, v == "1");
            else if (f.FieldType == typeof(float)) f.SetValue(dials, float.Parse(v, System.Globalization.CultureInfo.InvariantCulture));
            else if (f.FieldType == typeof(int)) f.SetValue(dials, int.Parse(v));
        }
        return dials.ToSettings();
    }

    static Vector3 Fib(int k, int n)
    {
        float y = 1f - 2f * (k + 0.5f) / n, s = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), a = 2.39996323f * k;
        return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s);
    }

    // One sheaf at the origin, laid as SpawnableHarvest.LaySheaf does (frame from a fixed axis).
    static List<P> Sheaf(float coreR, int blocks, bool shielded, int stalks, int stalkPrisms, Vector3 axis)
    {
        var x = axis.normalized;
        var helper = Mathf.Abs(x.z) < 0.9f ? Vector3.forward : Vector3.right;
        var y = Vector3.Cross(x, helper).normalized; var z = Vector3.Cross(x, y);
        Vector3 F(Vector3 f) => x * f.x + y * f.y + z * f.z;
        var list = new List<P>();
        float blockHalf = 0.5f * new Vector3(6, 6, 6).magnitude;
        for (int k = 0; k < blocks; k++) list.Add(new P { C = F(Fib(k, blocks)) * coreR, Half = blockHalf, Shield = shielded });
        float stalkHalf = 0.5f * new Vector3(3, 3, 20f * 0.995f).magnitude;
        float root = coreR + 6f;
        for (int k = 0; k < stalks; k++)
        {
            var d = Wheel ? WheelDir(k, stalks) : F(Fib(k, stalks));
            for (int j = 0; j < stalkPrisms; j++) list.Add(new P { C = d * (root + 20f * (j + 0.5f)), Half = stalkHalf, Stalk = true });
        }
        return list;
    }

    static float SegDist(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a; float L = ab.sqrMagnitude;
        float t = L > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / L) : 0f;
        return (a + ab * t - p).magnitude;
    }

    class Run
    {
        public int MowCuts, StalkSpends, BankLostOnStalks, StalksHitByBall, StalksOnWayIn;
        public float BankAtCore = -1, BankAtLapEnd; public int CoreKilledByBlast, CorePopped, CoreTotal;
        public float MaxBallDist, MeanBallDist;
    }

    static Run Fly(ThresherChainSettings s, List<P> prisms, float orbitR, float speed, float rt, float coreR)
    {
        var run = new Run { CoreTotal = prisms.Count(p => !p.Stalk) };
        foreach (var p in prisms) { p.Dead = false; }
        var shieldInit = prisms.Select(p => p.Shield).ToArray();
        var solver = new ThresherChainSolver(s);
        var links = new ThresherChainLinks(Links);
        Vector3 e1 = new Vector3(1, 0, 0), e2 = new Vector3(0, 0, 1), up = new Vector3(0, 1, 0);
        float w = speed / orbitR, approach = 3f, lap = 2f * Mathf.PI / w, strike = (AxisStrike ? 4.5f : 2.2f) * orbitR / speed;
        Vector3 start = e1 * orbitR, tangent = e2;     // the lap starts at +e1 heading +e2 (counter-clockwise from above)
        float bank = 0f, nextExplosion = 0f, distSum = 0f; int distN = 0;
        Vector3 shipPos = start - tangent * speed * approach, shipVel = tangent * speed;
        solver.Reset(shipPos, tangent, shipVel);
        links.Reset(shipPos, solver.BallPosition);
        bool coreHit = false;
        Vector3 strikeFrom = Vector3.zero, strikeDir = Vector3.zero;

        int steps = (int)((approach + lap + strike) / Dt);
        for (int i = 0; i < steps; i++)
        {
            float t = i * Dt;
            string phase;
            if (t < approach) { shipPos = start - tangent * speed * (approach - t); shipVel = tangent * speed; phase = "approach"; }
            else if (t < approach + lap)
            {
                float a = w * (t - approach);
                shipPos = (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * orbitR;
                shipVel = (e1 * -Mathf.Sin(a) + e2 * Mathf.Cos(a)) * speed; phase = "mow";
            }
            else
            {
                if (strikeDir == Vector3.zero) { strikeFrom = shipPos; strikeDir = (-shipPos).normalized; run.BankAtLapEnd = bank; }
                float ts = t - approach - lap;
                if (AxisStrike)
                {
                    // Up out of the wheel to a point on its axle, then straight down the axle through the core.
                    Vector3 A = up * (1.3f * orbitR);
                    float leg1 = (A - strikeFrom).magnitude / speed;
                    if (ts < leg1) { var d1 = (A - strikeFrom).normalized; shipPos = strikeFrom + d1 * speed * ts; shipVel = d1 * speed; }
                    else { shipPos = A - up * speed * (ts - leg1); shipVel = -up * speed; }
                }
                else { shipPos = strikeFrom + strikeDir * speed * ts; shipVel = strikeDir * speed; }
                phase = "strike";
            }

            solver.AnalogTriggers(0f, rt, 0.97f, 0.9f, shipPos, shipVel, up, Dt);
            var r = solver.Step(shipPos, shipVel, solver.Rigidity < 0.5f, speed, Dt);
            links.Step(shipPos + shipVel * Dt, solver.BallPosition, solver.Length, iterations: ThresherChainLinks.IterationsFor(Links));
            if (phase == "mow") { distSum += (solver.BallPosition - shipPos).magnitude; distN++; run.MaxBallDist = Mathf.Max(run.MaxBallDist, (solver.BallPosition - shipPos).magnitude); }

            // The ball's sweep, nearest first (ThresherExecutor.SweepBall).
            var hits = new List<(float d, P p)>();
            Vector3 from = r.BallFrom, to = solver.BallPosition;
            foreach (var p in prisms)
                if (!p.Dead && SegDist(p.C, from, to) <= s.BallRadius + p.Half) hits.Add(((p.C - from).sqrMagnitude, p));
            foreach (var (_, p) in hits.OrderBy(h => h.d))
            {
                if (p.Dead) continue;
                bool smash = solver.BallSpeed >= s.SmashSpeed;
                if (p.Stalk) { run.StalksHitByBall++; if (phase == "strike" && !coreHit) run.StalksOnWayIn++; }
                if (p.Shield) p.Shield = false; else p.Dead = true;
                solver.RegisterHit(ThresherChainSolver.KeepAfterHit(solver.BallSpeed, s));
                if ((bank > 0f || smash) && t >= nextExplosion)
                {
                    nextExplosion = t + MinInterval;
                    float radius = 0.5f * ExplosionDiameter * Mathf.Min(RestCap, 1f + PerUnit * bank);
                    if (!p.Stalk && !coreHit)
                    {
                        coreHit = true; run.BankAtCore = bank;
                        foreach (var q in prisms) if (!q.Dead && !q.Stalk && (q.C - p.C).magnitude <= radius)
                            { if (q.Shield) { q.Shield = false; run.CorePopped++; } else { q.Dead = true; run.CoreKilledByBlast++; } }
                    }
                    else if (p.Stalk && bank > 0f) { run.StalkSpends++; run.BankLostOnStalks += (int)bank; }
                    foreach (var q in prisms) if (!q.Dead && (q.C - p.C).magnitude <= radius && q.Stalk) q.Dead = true;
                    bank = 0f;
                }
                if (!p.Stalk && !coreHit) { coreHit = true; run.BankAtCore = bank; }
            }

            // The chain's cut (ThresherExecutor.CutWithChain): every link segment, every rival prism in reach.
            var pts = links.Points;
            for (int sgm = 0; sgm < pts.Length - 1; sgm++)
                foreach (var p in prisms)
                {
                    if (p.Dead || SegDist(p.C, pts[sgm], pts[sgm + 1]) > ChainCut + p.Half) continue;
                    if (p.Shield) { p.Shield = false; continue; }
                    p.Dead = true;
                    bank = Mathf.Min(EnergyMax, bank + EnergyPerSlice);
                    if (phase == "mow") run.MowCuts++;
                }
            if (coreHit && phase == "strike") break;
        }
        run.MeanBallDist = distN > 0 ? distSum / distN : 0f;
        for (int k = 0; k < prisms.Count; k++) prisms[k].Shield = shieldInit[k];
        return run;
    }

    static float[] Offsets = { -110f, -90f, -70f, -50f, -30f, 0f };
    static bool Wheel, AxisStrike; static float TiltDeg;
    static Vector3 WheelDir(int k, int n)
    {
        float a = 2f * Mathf.PI * k / n, tilt = TiltDeg * Mathf.Deg2Rad;
        // the orbit plane is (x, z); tilt the wheel about x
        return new Vector3(Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(tilt), Mathf.Sin(a) * Mathf.Cos(tilt));
    }
    static void Main(string[] args)
    {
        Wheel = args.Length > 0 && args[0] == "wheel";
        TiltDeg = args.Length > 1 ? float.Parse(args[1]) : 0f;
        AxisStrike = args.Length > 2 && args[2] == "axis";
        var s = LoadSettings();
        Console.WriteLine($"shipped dials: rest {s.RestLength:F1} u, max {s.MaxLength:F1} u, ball r {s.BallRadius:F1} u, smash {s.SmashSpeed:F0} u/s, analog {s.Analog}");
        var rungs = new (string name, float coreR, int blocks, bool shield, int stalks, int sp)[]
        {
            ("I1", 64, 160, false, 40, 6), ("I2", 58, 140, true, 24, 6), ("I3", 52, 110, true, 16, 5), ("I4", 46, 90, true, 12, 5),
        };
        var axes = new[] { new Vector3(0.3f, 0.5f, 0.8f), new Vector3(-0.7f, 0.2f, 0.4f), new Vector3(0.1f, -0.9f, 0.3f) };
        Console.WriteLine("rung speed RT  orbit(reach+) | ball-ship mean/max | mow cuts | stalk spends (bank lost) | bank at lap end | stalks on way in | bank at core | core killed/popped/total");
        foreach (var rg in rungs)
        {
            float reach = rg.coreR + 6f + rg.sp * 20f;
            foreach (float speed in new[] { 70f, 120f })
            foreach (float rt in new[] { 0f, 1f })
            foreach (float off in Offsets)
            {
                var runs = axes.Select(ax => Fly(s, Sheaf(rg.coreR, rg.blocks, rg.shield, rg.stalks, rg.sp, ax), reach + off, speed, rt, rg.coreR)).ToList();
                double M(Func<Run, double> f) => runs.Average(f);
                Console.WriteLine($"{rg.name} {speed,4:F0} {rt,3:F1} {off,+4:F0}      | {M(r => r.MeanBallDist),5:F0}/{M(r => r.MaxBallDist),4:F0}        | {M(r => r.MowCuts),6:F1}   | {M(r => r.StalkSpends),5:F1} ({M(r => r.BankLostOnStalks),5:F0})          | {M(r => r.BankAtLapEnd),5:F1}           | {M(r => r.StalksOnWayIn),5:F1}            | {M(r => Math.Max(0, r.BankAtCore)),5:F1}        | {M(r => r.CoreKilledByBlast),5:F1}/{M(r => r.CorePopped),5:F1}/{runs[0].CoreTotal}");
            }
        }
    }
}
