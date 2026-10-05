// Round 11d-2 (Docs/SWARM_FAUNA.md §27): export a SHIPPED substrate species' encounter with a scripted pilot for the
// research's emotion probe (Tools/Build/emotion_range/emotion_range.py). Nothing is asserted here; the scorer asserts.
// The species are the game's ports (SubstrateResearch.GamePack / GameLocust / GameLurker) in this harness's World.
//
//   bash Tools/Build/substrate_harness/run.sh emotion <jobs.txt>     one job per line: "<track> <out> <condition> <seed>"
//
// Conditions: pack (the research port) | pack_hold (the demo cell's pack as authored: + RingHoldSeconds) | locust_sparse (40, spread 300, fed, hunger 0.1) | locust_dense (300, spread 40, unfed, hunger 0.9)
// | lurker (12). Sizes follow the research's convention (regime size lerped by phase x grow; the lurker's calm mimic
// sliver, SubstrateTickJob's draw rule, on top). Heading is not published (zeros): the research's substrate publishes
// none, so the probe holds the velocity direction through stops for both.
// Phase codes - pack: 0 stalk, 1 ring held (>= min(4, live) inside CloseR, none striking or winded), 2 strike (any
// danger), 3 winded (resting). locust: 0 solitary, 1 storm (>= 20% gregarious). lurker: 0 still, 1 creep, 2 gape
// (phase > 0.05), 3 snap (danger), 4 spent (resting).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static partial class SubstrateHarness
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

    static void EmotionOne(string trackPath, string outPath, string cond, int seed)
    {
        var track = new List<(Vector3 off, Vector3 vel)>();
        foreach (var l in File.ReadAllLines(trackPath))
        {
            var f = l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            if (f.Length >= 6) track.Add((new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5])));
        }
        var home = new Vector3(0f, 0f, 300f);
        var rng = new Random(seed);
        SubstrateSpeciesParams P; World w; int q;
        switch (cond)
        {
            case "pack":
            case "pack_hold":
                P = cond == "pack" ? SubstrateResearch.GamePack() : DemoPack(); w = new World(64, seed); q = w.Core.AddPopulation(P, 3);
                w.Scatter(rng, 2500, 0.1f * R, 0.8f * R, 24);
                w.Core.Seed(q, P.N0, home, 50f);
                break;
            case "locust_sparse":
            case "locust_dense":
            {
                bool dense = cond == "locust_dense";
                P = SubstrateResearch.GameLocust(); P.Capacity = 400; P.N0 = dense ? 300 : 40;
                w = new World(400, seed); q = w.Core.AddPopulation(P, 2);
                if (!dense) w.Scatter(rng, 900, 0.1f, 0.5f, 6);
                w.Core.Seed(q, P.N0, home, dense ? 40f : 300f);
                foreach (int i in LiveOf(w.Core, q)) w.Core.Hunger[i] = dense ? 0.9f : 0.1f;
                break;
            }
            case "lurker":
                P = SubstrateResearch.GameLurker(); w = new World(32, seed); q = w.Core.AddPopulation(P, 1);
                w.Scatter(rng, 1500, 0.1f * R, 0.8f * R, 24);
                w.Core.Seed(q, P.N0, home, 120f);
                break;
            default: throw new ArgumentException("no such emotion condition: " + cond);
        }
        for (int t = 0; t < 20; t++) w.Step();   // 2 s warm, no pilot (the research's warmup)
        var c = Vector3.Zero; int n0 = 0;
        foreach (int i in LiveOf(w.Core, q)) { c += w.Core.Pos[i]; n0++; }
        c /= Math.Max(1, n0);
        var pil = new Pilot(seed, c + track[0].off, track[0].vel) { Mode = "replay", Id = 1 };
        w.Pilots.Add(pil);
        using var wr = new EmotionWriter(outPath);
        for (int k = 0; k < track.Count; k++)
        {
            pil.Pos = c + track[k].off; pil.Vel = track[k].vel;
            w.Step();
            var live = LiveOf(w.Core, q).ToArray();
            int phase = 0;
            if (cond.StartsWith("pack"))
            {
                bool striking = live.Any(i => w.Core.Danger[i]), resting = live.Any(i => w.Core.Rest[i] > 0f);
                int around = live.Count(i => Vector3.Distance(w.Core.Pos[i], pil.Pos) < P.CloseR);
                phase = striking ? 2 : resting ? 3 : live.Length > 0 && around >= Math.Min(4, live.Length) ? 1 : 0;
            }
            else if (cond.StartsWith("locust"))
                phase = live.Count(i => w.Core.Phase[i] > 0.5f) >= 0.2f * Math.Max(1, live.Length) ? 1 : 0;
            else
                phase = live.Any(i => w.Core.Danger[i]) ? 3 : live.Any(i => w.Core.Rest[i] > 0f) ? 4
                      : live.Any(i => w.Core.Phase[i] > 0.05f) ? 2 : live.Any(i => w.Core.Creeping[i]) ? 1 : 0;
            wr.Frame(k * Dt, phase, pil.Pos, pil.Vel, live.Length);
            foreach (int i in live)
            {
                float ph = w.Core.Phase[i];
                float size = (P.Solitary.Size + (P.Gregarious.Size - P.Solitary.Size) * ph) * Math.Clamp(w.Core.Grow[i], 0f, 1f);
                if (P.MimicBody < 1f)
                {
                    float wake = Math.Clamp(ph * 5f + (w.Core.Rest[i] > 0f ? 1f : 0f), 0f, 1f);
                    size *= P.MimicBody + (1f - P.MimicBody) * wake;
                }
                float aspect = P.Solitary.Aspect + (P.Gregarious.Aspect - P.Solitary.Aspect) * ph;
                wr.Agent(w.Core.Pos[i], w.Core.Vel[i] + w.Core.GaitV[i], size, aspect, Vector3.Zero, 0);
            }
        }
        Console.WriteLine($"emotion export: substrate {cond} seed {seed}, {LiveOf(w.Core, q).Count()} alive at the end, {track.Count} frames -> {outPath}");
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
