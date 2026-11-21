// Round 11d-2 (Docs/SWARM_FAUNA.md §27): export a SHIPPED builder colony's or thief nest's encounter with a scripted
// pilot for the research's emotion probe (Tools/Build/emotion_range/emotion_range.py). Nothing is asserted here.
//
//   bash Tools/Build/builders_harness/run.sh emotion <jobs.txt>     one job per line: "<track> <out> <fortress|thief> <seed>"
//
// fortress: CutRun's world (1,500 prisms, the colony anchored 450 u out), 120 s of building first, then the encounter.
// thief: ThiefRun's world (3,000 prisms, pilot trails every 15 u / 10 vol - what thieves steal), 2 s warm.
// The pilot is REPLAYED (placed from the track before each colony step; the arena then lays its trail). Sizes are the
// research's (fortress workers 3.0, thieves ThiefParams.Size 2.2); no heading is published (zeros), as the research.
// Phase codes - fortress: 0 build, 1 strike (a worker's intent >= 1, the sting). thief: 0 forage, 1 laden (carrying).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static partial class Program
{
    static int Emotion(string jobs)
    {
        foreach (var line in File.ReadAllLines(jobs))
        {
            var a = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (a.Length < 4 || a[0].StartsWith("#")) continue;
            EmotionOne(a[0], a[1], a[2], int.Parse(a[3]));
        }
        return 0;
    }

    static void EmotionOne(string trackPath, string outPath, string kind, int seed)
    {
        var track = new List<(Vector3 off, Vector3 vel)>();
        foreach (var l in File.ReadAllLines(trackPath))
        {
            var f = l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            if (f.Length >= 6) track.Add((new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5])));
        }
        var ar = new Arena(seed);
        BuilderColonyCore col = null; ThiefNestCore th = null;
        float size;
        if (kind == "fortress")
        {
            ar.Scatter(1500);
            var A = new BuilderRng(seed + 5).OnSphere() * 450f;
            col = new BuilderColonyCore(ar, new BuilderColonyParams { Containment = ar.R * 0.95f }, A, 2, 1, seed);
            size = 3f;
        }
        else
        {
            ar.Scatter(3000);
            ar.TrailSpacing = 15f; ar.TrailSpacingVol = 10f;
            var p = new ThiefParams();
            th = new ThiefNestCore(ar, p, ar.Pos[ar.Rng.Range(ar.Count)], 3, 2, seed);
            size = p.Size;
        }
        var vessels = new BuilderVessel[4];
        int warm = (int)((kind == "fortress" ? 120f : 2f) / Dt);
        for (int s = 0; s < warm; s++)
        {
            if (col != null) col.Step(Dt, vessels, 0); else th.Step(Dt, vessels, 0);
            ar.Step(Dt);
        }
        Vector3[] pos = col != null ? col.Pos : th.Pos, vel = col != null ? col.Vel : th.Vel;
        bool[] alive = col != null ? col.Alive : th.Alive;
        var c = Vector3.Zero; int n0 = 0;
        for (int i = 0; i < alive.Length; i++) if (alive[i]) { c += pos[i]; n0++; }
        c /= Math.Max(1, n0);
        var pilot = ar.AddPilot(new Pilot { Policy = "replay", Name = "replay", Speed = track[0].vel.Length() });
        using var wr = new EmotionWriter(outPath);
        for (int k = 0; k < track.Count; k++)
        {
            pilot.Pos = c + track[k].off; pilot.Vel = track[k].vel;
            var pp = pilot.Pos; var pv = pilot.Vel;
            ar.Targets.Clear();
            for (int i = 0; i < alive.Length; i++) if (alive[i]) ar.Targets.Add(pos[i]);
            int nv = ar.Vessels(vessels);
            if (col != null) col.Step(Dt, vessels, nv); else th.Step(Dt, vessels, nv);
            ar.Step(Dt);
            int n = 0, phase = 0;
            for (int i = 0; i < alive.Length; i++)
            {
                if (!alive[i]) continue;
                n++;
                if (col != null ? col.Striking(i) : th.Carry[i] >= 0) phase = 1;
            }
            wr.Frame(k * Dt, phase, pp, pv, n);
            for (int i = 0; i < alive.Length; i++)
                if (alive[i]) wr.Agent(pos[i], vel[i], size, 1.5f, Vector3.Zero, 0);
        }
        Console.WriteLine($"emotion export: {kind} seed {seed}, {n0} alive at the encounter, {track.Count} frames -> {outPath}");
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
