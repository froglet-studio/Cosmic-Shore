// Round 11d-2 (Docs/SWARM_FAUNA.md §27): export a SHIPPED threat-flora grove's encounter with a scripted pilot for
// the research's emotion probe (Tools/Build/emotion_range/emotion_range.py). Nothing is asserted here.
//
//   bash Tools/Build/threat_flora_harness/run.sh emotion <jobs.txt>   one job per line: "<track> <out> <snaptrap|physarum> <seed>"
//
// The grove is the harness's own (FloraArena, 1,600 food prisms in 20 clumps; SnapTrapSpecies 40 traps /
// PhysarumSpecies 5 hearts, both on the shipped cores). The encounter is centred on the grove (GroveC). The agents are
// what the RESEARCH's flora publish to its probe, so the two reads compare like for like: a snap trap is its heart
// (size 30, no velocity - flora/snaptrap.py), a physarum network its sclerotia (no velocity, default size).
// Phase codes - snaptrap: 0 rest, 1 snap (a trap priming, armed or closing). physarum: 0 rest, 1 pulse (a danger
// voxel inside 450 u of the pilot, FloraArena.View).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

public static partial class Program
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
        const float dt = 0.1f;
        var ar = new FloraArena((ulong)seed);
        ar.GroveMass(1600, 20);
        SnapTrapSpecies snap = null; PhysarumSpecies phys = null;
        if (kind == "snaptrap") snap = new SnapTrapSpecies(ar, new SnapTrapParams(), (ulong)seed);
        else phys = new PhysarumSpecies(ar, new PhysarumParams(), (ulong)seed);
        for (int t = 0; t < 20; t++) { if (snap != null) snap.Step(dt); else phys.Step(dt); ar.Step(dt); }
        var c = FloraArena.GroveC;
        var pilot = ar.AddPilot("replay", track[0].vel.Length(), "replay");
        var prev = c + track[0].off - track[0].vel * dt;
        using var wr = new EmotionWriter(outPath);
        for (int k = 0; k < track.Count; k++)
        {
            pilot.Prev = prev; pilot.Pos = c + track[k].off; pilot.Vel = track[k].vel;
            var pp = pilot.Pos; prev = pp;
            if (snap != null) snap.Step(dt); else phys.Step(dt);
            ar.Step(dt);
            if (snap != null)
            {
                var core = snap.Core;
                int n = 0, phase = 0;
                for (int i = 0; i < core.Count; i++)
                {
                    if (!core.Alive[i]) continue;
                    n++;
                    int s = core.State[i];
                    if (s == (int)SnapTrapState.Priming || s == (int)SnapTrapState.Armed || s == (int)SnapTrapState.Closing) phase = 1;
                }
                wr.Frame(k * dt, phase, pp, track[k].vel, n);
                for (int i = 0; i < core.Count; i++)
                    if (core.Alive[i]) wr.Agent(core.Heart[i], Vector3.Zero, 30f, 1.5f, Vector3.Zero, 0);
            }
            else
            {
                var core = phys.Core;
                int phase = 0;
                for (int v = 0; v < core.VoxelCount && phase == 0; v++)
                    if (core.Danger[v] && (core.Centre(v) - pp).Length() < FloraArena.View) phase = 1;
                int n = 0;
                for (int h = 0; h < core.HeartPosition.Count; h++) if (core.HeartAlive[h]) n++;
                wr.Frame(k * dt, phase, pp, track[k].vel, n);
                for (int h = 0; h < core.HeartPosition.Count; h++)
                    if (core.HeartAlive[h]) wr.Agent(core.HeartPosition[h], Vector3.Zero, 3f, 1.5f, Vector3.Zero, 0);
            }
        }
        Console.WriteLine($"emotion export: {kind} seed {seed}, {track.Count} frames -> {outPath}");
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
