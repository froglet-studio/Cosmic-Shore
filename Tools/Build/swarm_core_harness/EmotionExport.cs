// Round 11d-2 (Docs/SWARM_FAUNA.md §27): export a SHIPPED sort swarm's encounter with a scripted pilot, frame by
// frame, for the research's emotion probe (Tools/Build/emotion_range/emotion_range.py reads it). Nothing here is
// asserted - the scorer asserts the reads. The swarm runs exactly as SwarmFauna drives it: SortHarness.Game (the
// shipped sort settings), the off-thread tick job with round 10/11d strike states, the pilot sensed as a
// SwarmPredator the way SwarmFauna.SenseVessels builds one (VesselRadius 9, UnitScale 2, 10 Hz).
//
//   SWARM_DENSITY=5 run.sh <plans> emotion <jobs.txt>      one job per line: "<track.txt> <out.txt> <planElement> <seed>"
//
// track.txt: one line per 0.1 s - "ox oy oz vx vy vz", the pilot's offset from the body's centre and its velocity
// (world units). out (float32, EmotionWriter): per frame [t phase px py pz vx vy vz n] then n x [x y z vx vy vz size
// aspect hx hy hz body]. Phase codes here: 0 calm, 1 startled (a member's startle > 0.2, < 2% plated), 2 a few plates (2-10%),
// 3 strike (>= 10% of the body plated).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using CosmicShore.Gameplay;

static class EmotionExport
{
    const float UnitScale = 2f, TickHz = 10f, VesselRadius = 9f;

    public static List<(Vector3 off, Vector3 vel)> ReadTrack(string path)
    {
        var o = new List<(Vector3, Vector3)>();
        foreach (var line in File.ReadAllLines(path))
        {
            var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            if (f.Length < 6) continue;
            o.Add((new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5])));
        }
        return o;
    }

    public static int Run(string[] args, SwarmPlanData[] plans)
    {
        foreach (var line in File.ReadAllLines(args[2]))
        {
            var a = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (a.Length < 4 || a[0].StartsWith("#")) continue;
            One(plans, ReadTrack(a[0]), a[1], int.Parse(a[2]), int.Parse(a[3]), a.Length > 4 ? int.Parse(a[4]) : 1500);
        }
        return 0;
    }

    static void One(SwarmPlanData[] plans, List<(Vector3 off, Vector3 vel)> track, string outPath, int e, int seed, int warm)
    {
        var p = SortHarness.Game(plans);
        var core = SortHarness.GameSwarm(plans, e, seed, true, p);
        var st = new SwarmTickSettings
        {
            Centre = Vector3.Zero, UnitScale = UnitScale, PrismScale = 1f, HeartPrismGap = 0.6f,
            DefaultHalf = new[] { new Vector3(1f, 0.6f, 0.6f), new Vector3(0.9f, 0.8f, 0.7f), new Vector3(2f, 0.3f, 0.3f), new Vector3(0.8f, 0.4f, 0.4f) },
            DangerEnter = 0.45f, DangerExit = 0.15f, Bestiary = true, HuntEnter = 0.2f, LurkCalm = 0.05f, LocustPhaseTicks = 20,
            EngageRadius = 160f, MaxEngaged = 160,
        };
        var j = new SwarmTickJob(core, st, TickHz) { SwimTarget = core.SwimTarget };
        j.Prime();
        int want = (int)(0.95f * plans[e].N);
        int w = 0;
        for (; w < warm && !(w >= 300 && core.AliveCount >= want); w++) Tick(j);
        // the body's centre (world) when the pilot arrives; the pilot's track is laid relative to it
        var c = Vector3.Zero; int n0 = 0;
        for (int i = 0; i < j.Instances.Length; i++) if (j.Instances[i].Alive) { c += j.Instances[i].CurPos; n0++; }
        c /= Math.Max(1, n0);
        float senseR = plans[e].Radius * UnitScale * 1.6f + 400f;   // SenseVessels: plan radius x 1.6 + max(SenseMargin 400, EngageRadius 160)
        using var wr = new EmotionWriter(outPath);
        for (int k = 0; k < track.Count; k++)
        {
            var pp = c + track[k].off; var pv = track[k].vel;
            j.PredCount = 0;
            if (Vector3.Distance(pp, c) < senseR)
            {
                j.Preds[0] = new SwarmPredator { C = pp / UnitScale, V = pv / (UnitScale * TickHz), R = VesselRadius / UnitScale };
                j.PredCount = 1;
            }
            Tick(j);
            int alive = 0, struck = 0; float stMax = 0f;
            for (int i = 0; i < j.Instances.Length; i++)
            {
                if (!j.Instances[i].Alive) continue;
                alive++; if (j.Instances[i].Tier == 1) struck++;
                stMax = MathF.Max(stMax, core.Startle[i]);
            }
            float frac = struck / (float)Math.Max(1, alive);
            int phase = frac >= 0.10f ? 3 : frac >= 0.02f ? 2 : stMax > 0.2f ? 1 : 0;
            wr.Frame(k * 0.1f, phase, pp, pv, alive);
            for (int i = 0; i < j.Instances.Length; i++)
            {
                var s = j.Instances[i];
                if (!s.Alive) continue;
                float big = MathF.Max(s.Scale.X, MathF.Max(s.Scale.Y, s.Scale.Z));
                float asp = s.Scale.Z / MathF.Max(1e-3f, MathF.Sqrt(MathF.Abs(s.Scale.X * s.Scale.Y)));
                wr.Agent(s.CurPos, (s.CurPos - s.PrevPos) * TickHz, 0.5f * big, MathF.Max(1f, asp), s.CurFace, 0);
            }
        }
        Console.WriteLine($"emotion export: {plans[e].Kind} seed {seed}, warm {w} ticks, {core.AliveCount}/{plans[e].N} alive, {track.Count} frames -> {outPath}");
    }

    static void Tick(SwarmTickJob j)
    {
        j.Kick(true);
        var spin = new SpinWait();
        while (j.State != SwarmJobState.Done) spin.SpinOnce();
        j.Collect();
        if (j.Error != null) throw j.Error;
    }
}

/// <summary>The emotion export's frame stream (float32, little-endian): Tools/Build/emotion_range reads it.</summary>
sealed class EmotionWriter : IDisposable
{
    readonly BinaryWriter _w;
    public EmotionWriter(string path) { _w = new BinaryWriter(File.Create(path)); }
    public void Frame(float t, int phase, Vector3 pp, Vector3 pv, int n)
    {
        _w.Write(t); _w.Write((float)phase); V(pp); V(pv); _w.Write((float)n);
    }
    public void Agent(Vector3 p, Vector3 v, float size, float aspect, Vector3 heading, int body)
    {
        V(p); V(v); _w.Write(size); _w.Write(aspect); V(heading); _w.Write((float)body);
    }
    void V(Vector3 v) { _w.Write(v.X); _w.Write(v.Y); _w.Write(v.Z); }
    public void Dispose() => _w.Dispose();
}
