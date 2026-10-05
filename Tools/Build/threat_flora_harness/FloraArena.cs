// A C# port of the research's flora arena (Tools/Ecology/flora/harness.py FloraArena on common/arena.py,
// branch cece/eco-flora) - only what the asserted tests need: the 450 u GROVE, its 1,600 food prisms in 20
// clumps, scripted pilots (the WANDERER and the COURIER), their TRAILS (a 6-volume prism every 24 u, booked as
// created mass), the danger slow, and the omni crystals a wanderer diverts to. The cores under test are the
// SHIPPED files (SnapTrapCore.cs / PhysarumCore.cs); this file only stands in for the world around them.
using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Gameplay;

public sealed class Pilot
{
    public string Name, Policy;
    public Vector3 Pos, Vel, Prev, Goal;
    public float Speed, BaseSpeed, Turn = 2f, Radius = 6f;
    public readonly List<(float t, string kind)> Hits = new List<(float, string)>();
    public float GoalT, TrailAcc, SlowT = -1e9f;
    public int CourierI;
    public Vector3? BaitPos; public float BaitT;
    public readonly List<(Vector3 q, float until)> BaitSkip = new List<(Vector3, float)>();
}

public sealed class FloraArena
{
    public static readonly Vector3 GroveC = new Vector3(0f, 0f, 600f);
    public const float GroveR = 450f, View = 450f, R = 1200f;
    public const float TrailGap = 24f, TrailVol = 6f, Divert = 300f;

    public ThreatRng Rng;
    public float T;
    public readonly List<Vector3> MassPos = new List<Vector3>();
    public readonly List<float> MassVol = new List<float>();
    public readonly List<bool> MassAlive = new List<bool>();
    public double Created, Eaten;
    public readonly List<Pilot> Pilots = new List<Pilot>();
    public readonly List<Vector3> Crystals = new List<Vector3>();
    public Vector3[] CourierRoute;
    public int Collected;
    public bool Trails = true;

    public FloraArena(ulong seed)
    {
        Rng = new ThreatRng(seed);
        for (int i = 0; i < 6; i++) Crystals.Add(Grove(30f));
        CourierRoute = new[] { Grove(60f), Grove(60f), Grove(60f) };
    }

    /// <summary>A point volume-uniform in the grove ball shrunk by margin (harness.grove).</summary>
    public Vector3 Grove(float margin)
    {
        Vector3 d = Rng.OnUnitSphere();
        float r = MathF.Cbrt(Rng.NextFloat()) * (GroveR - margin);
        return GroveC + d * r;
    }

    public void GroveMass(int n, int clumps, float volLo = 8f, float volHi = 40f, float spread = 25f)
    {
        var centres = new Vector3[clumps];
        for (int i = 0; i < clumps; i++) centres[i] = Grove(40f);
        for (int i = 0; i < n; i++)
        {
            Vector3 c = centres[Rng.Range(0, clumps)];
            MassPos.Add(c + Rng.GaussianVector() * spread);
            MassVol.Add(Rng.Range(volLo, volHi));
            MassAlive.Add(true);
        }
    }

    public double LiveVolume()
    {
        double v = 0;
        for (int i = 0; i < MassVol.Count; i++) if (MassAlive[i]) v += MassVol[i];
        return v;
    }

    public float Consume(int i)
    {
        if (i < 0 || !MassAlive[i]) return 0f;
        MassAlive[i] = false;
        Eaten += MassVol[i];
        return MassVol[i];
    }

    /// <summary>The nearest live food prism within r of q, or -1 (snaptrap.py roots: argmin over mass_near).</summary>
    public int NearestMass(Vector3 q, float r)
    {
        int best = -1;
        float bd = r * r;
        for (int i = 0; i < MassPos.Count; i++)
        {
            if (!MassAlive[i]) continue;
            float d = (MassPos[i] - q).LengthSquared();
            if (d <= bd) { bd = d; best = i; }
        }
        return best;
    }

    public Pilot AddPilot(string policy, float speed, string name)
    {
        var p = new Pilot { Name = name, Policy = policy, Speed = speed, BaseSpeed = speed };
        p.Pos = Grove(0f);
        p.Goal = Grove(0f);
        p.Vel = Rng.OnUnitSphere() * speed;
        p.Prev = p.Pos;
        Pilots.Add(p);
        return p;
    }

    public void Hit(Pilot p, string kind)
    {
        p.Hits.Add((T, kind));
        p.SlowT = T;
    }

    float SpeedFactor(Pilot p)
    {
        float x = (T - p.SlowT) / 3f;
        return x >= 1f ? 1f : MathF.Max(0.1f, 1f - 1.5f * (1f - x));
    }

    Vector3 PilotGoal(Pilot p)
    {
        if (p.Policy == "courier")
        {
            Vector3 g = CourierRoute[p.CourierI % 3];
            if ((g - p.Pos).Length() < 60f) p.CourierI++;
            return CourierRoute[p.CourierI % 3];
        }
        if ((p.Goal - p.Pos).Length() < 60f) { p.Goal = Grove(0f); p.GoalT = T; }
        else if (T - p.GoalT > 15f) { p.Goal = Grove(0f); p.GoalT = T; }
        Vector3? bait = Bait(p);
        return bait ?? p.Goal;
    }

    Vector3? Bait(Pilot p)
    {
        for (int k = 0; k < Crystals.Count; k++)
            if ((Crystals[k] - p.Pos).Length() < 12f) { Collected++; Crystals[k] = Grove(30f); }
        int best = -1;
        float bd = Divert;
        for (int k = 0; k < Crystals.Count; k++)
        {
            float d = (Crystals[k] - p.Pos).Length();
            if (d >= bd) continue;
            bool skipped = false;
            foreach (var (q, until) in p.BaitSkip)
                if (until > T && (Crystals[k] - q).Length() <= 40f) { skipped = true; break; }
            if (skipped) continue;
            bd = d; best = k;
        }
        if (best < 0) return null;
        Vector3 target = Crystals[best];
        if (p.BaitPos == null || (target - p.BaitPos.Value).Length() > 40f) { p.BaitPos = target; p.BaitT = T; }
        else if (T - p.BaitT > 10f)
        {
            p.BaitSkip.Add((target, T + 20f));
            p.BaitPos = null;
            return null;
        }
        return target;
    }

    /// <summary>harness.FloraArena.step + common Arena.step: steer, move, clamp, lay trails.</summary>
    public void Step(float dt)
    {
        foreach (var p in Pilots)
        {
            p.Speed = p.BaseSpeed * SpeedFactor(p);
            Vector3 before = p.Pos;
            Vector3 want = PilotGoal(p) - p.Pos;
            float n = want.Length();
            if (n > 1e-6f)
            {
                Vector3 v = p.Vel / MathF.Max(p.Vel.Length(), 1e-6f), w = want / n;
                float ang = MathF.Acos(Math.Clamp(Vector3.Dot(v, w), -1f, 1f));
                float k = MathF.Min(1f, p.Turn * dt / MathF.Max(ang, 1e-6f));
                Vector3 nd = v + (w - v) * k;
                p.Vel = nd / MathF.Max(nd.Length(), 1e-6f) * p.Speed;
            }
            p.Pos += p.Vel * dt;
            float r = p.Pos.Length();
            if (r > R * 0.97f) p.Pos *= R * 0.97f / r;
            float L = (p.Pos - before).Length();
            p.Prev = before;
            p.TrailAcc += L;
            if (Trails && p.TrailAcc >= TrailGap)
            {
                p.TrailAcc = 0f;
                Vector3 back = before - (p.Pos - before) / MathF.Max(L, 1e-6f) * 8f;
                MassPos.Add(back); MassVol.Add(TrailVol); MassAlive.Add(true);
                Created += TrailVol;
            }
        }
        T += dt;
    }

    /// <summary>harness.FloraProbe lanes: 64 random chords through the grove (its own seeded rng, 12345).</summary>
    public static Vector3[][] Lanes(int n = 64)
    {
        var rng = new ThreatRng(12345);
        var lanes = new Vector3[n][];
        for (int i = 0; i < n; i++)
        {
            lanes[i] = new Vector3[2];
            for (int e = 0; e < 2; e++)
                lanes[i][e] = GroveC + rng.OnUnitSphere() * (MathF.Cbrt(rng.NextFloat()) * GroveR);
        }
        return lanes;
    }

    public static float Percentile(List<float> xs, float q)
    {
        if (xs.Count == 0) return float.NaN;
        var s = new List<float>(xs);
        s.Sort();
        float pos = (s.Count - 1) * q / 100f;
        int lo = (int)MathF.Floor(pos), hi = Math.Min(lo + 1, s.Count - 1);
        return s[lo] + (s[hi] - s[lo]) * (pos - lo);
    }
}
