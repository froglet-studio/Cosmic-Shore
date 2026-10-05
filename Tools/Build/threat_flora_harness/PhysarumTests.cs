// Asserted tests of the SHIPPED PhysarumCore against the research (Tools/Ecology/flora/physarum.py, its searched
// best results/search_physarum_best.json, reroute.py and cost.py). Docs/THREAT_FLORA.md §6.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using CosmicShore.Gameplay;

/// <summary>The research species around the shipped core: physarum.py's arena side (food = arena mass, vessel
/// contacts burn on an excited tube and ram a resting one, the sclerotium beat stings within HeartGuard).</summary>
public sealed class PhysarumSpecies
{
    public readonly PhysarumCore Core;
    readonly FloraArena _ar;
    readonly Dictionary<(int, string), float> _waveSeen = new Dictionary<(int, string), float>();
    readonly Dictionary<string, float> _burnCd = new Dictionary<string, float>();
    public readonly List<float> Leads = new List<float>();
    public int Bumps, Burns, BeatBurns;
    ThreatVessel[] _vessels = new ThreatVessel[4];
    readonly HashSet<int> _vs = new HashSet<int>();

    public PhysarumSpecies(FloraArena ar, PhysarumParams p, ulong seed, int hearts = 5)
    {
        _ar = ar;
        Core = new PhysarumCore(p, ThreatGroveShape.MakeBall(FloraArena.GroveC, FloraArena.GroveR), seed);
        for (int k = 0; k < hearts; k++) Core.AddHeart(ar.Grove(80f), p.PlantVolume / hearts);
        SyncFood();
        Core.RunWarmup();
        foreach (var e in Core.Events)
            if (e.Kind == PhysarumEventKind.Digest) Core.CreditDigest(_ar.Consume(e.Food));
        Core.Events.Clear();
    }

    public double MassTotal => Core.Reserve + Core.TubeVolumeTotal + Core.HeartBodies;

    void SyncFood()
    {
        int n = _ar.MassPos.Count;
        Core.EnsureFood(n);
        for (int i = 0; i < n; i++)
        {
            Core.FoodPos[i] = _ar.MassPos[i];
            Core.FoodVol[i] = _ar.MassVol[i];
            Core.FoodAlive[i] = _ar.MassAlive[i];
        }
        Core.FoodCount = n;
    }

    public void Step(float dt)
    {
        SyncFood();
        int nv = _ar.Pilots.Count;
        if (_vessels.Length < nv) _vessels = new ThreatVessel[nv];
        for (int k = 0; k < nv; k++)
            _vessels[k] = new ThreatVessel { Position = _ar.Pilots[k].Pos, Previous = _ar.Pilots[k].Prev, Radius = 6f };
        Core.SetVessels(_vessels, nv);
        Core.Advance(dt);
        foreach (var e in Core.Events)
            if (e.Kind == PhysarumEventKind.Digest) Core.CreditDigest(_ar.Consume(e.Food));
        // telegraph: the first time each wave was visible (within VIEW) to each pilot
        foreach (var p in _ar.Pilots)
            for (int v = 0; v < Core.VoxelCount; v++)
            {
                if (!Core.Danger[v]) continue;
                var key = (Core.Wave[v], p.Name);
                if (_waveSeen.ContainsKey(key)) continue;
                if ((Core.Centre(v) - p.Pos).Length() < FloraArena.View) _waveSeen[key] = _ar.T;
            }
        // contacts: a pilot's segment, sampled every 4 u
        foreach (var p in _ar.Pilots)
        {
            Vector3 seg = p.Pos - p.Prev;
            int n = Math.Max(2, (int)(seg.Length() / 4f) + 1);
            _vs.Clear();
            for (int i = 0; i < n; i++) _vs.Add(Core.Vox(p.Prev + seg * (i / (float)(n - 1))));
            int hot = -1;
            var cold = new List<int>();
            foreach (int v in _vs)
            {
                if (!Core.Tube[v]) continue;
                if (Core.Danger[v]) { if (hot < 0) hot = v; }
                else cold.Add(v);
            }
            if (hot >= 0 && (!_burnCd.TryGetValue(p.Name, out float cd) || cd <= _ar.T))
            {
                _burnCd[p.Name] = _ar.T + 1f;
                float seen = _waveSeen.TryGetValue((Core.Wave[hot], p.Name), out float s) ? s : _ar.T;
                Leads.Add(MathF.Min(5f, _ar.T - seen));
                _ar.Hit(p, "burn");
                Burns++;
            }
            Bumps += cold.Count;
            foreach (int v in cold) Core.TubeLost(v);   // ram: flying through a resting tube breaks it
            if (Core.P.HeartGuard > 0f)
                for (int k = 0; k < Core.HeartPosition.Count; k++)
                {
                    if (!Core.Beating(k)) continue;
                    if (ThreatFloraMath.SegmentPointDistance(p.Prev, p.Pos, Core.HeartPosition[k]) >= Core.P.HeartGuard) continue;
                    if (_burnCd.TryGetValue(p.Name, out float cd2) && cd2 > _ar.T) continue;
                    _burnCd[p.Name] = _ar.T + 1f;
                    float last = Core.NextBeat[k] - Core.P.Period;
                    Leads.Add(MathF.Min(5f, Core.P.BeatGlow + (_ar.T - last)));
                    _ar.Hit(p, "burn");
                    BeatBurns++;
                    break;
                }
        }
    }
}

public static class PhysarumTests
{
    static PhysarumParams Best() => new PhysarumParams();    // the defaults ARE the research's searched best    // the defaults ARE the research's searched best

    public static void Run(bool quick)
    {
        Console.WriteLine("== physarum (PhysarumCore vs research physarum.py) ==");
        WaveSpeedAndRefractory();
        Telegraph(quick);
        Reroute(quick);
        GameGrove();
        FarCadence();
        Cost(quick);
    }

    /// <summary>The game grove's core, warmed up (P4's set-up).</summary>
    static PhysarumCore GameCore(int seed)
    {
        var p = ThreatGroveDefaults.Physarum().ForElement(ThreatGroveDefaults.PhysarumElement);
        var shape = ThreatGroveDefaults.SwarmCellGrove(Vector3.Zero);
        var c = new PhysarumCore(p, shape, (ulong)seed);
        var rng = new ThreatRng((ulong)seed);
        for (int k = 0; k < ThreatGroveDefaults.Sclerotia; k++)
            c.AddHeart(shape.Sample(ref rng, 40f), ThreatGroveDefaults.PlantedVolumePerSclerotium);
        int nf = 400;
        c.EnsureFood(nf);
        var centres = new Vector3[10];
        for (int i = 0; i < 10; i++) centres[i] = shape.Sample(ref rng, 30f);
        for (int i = 0; i < nf; i++)
        {
            c.FoodPos[i] = shape.Clamp(centres[i % 10] + rng.GaussianVector() * 25f, 8f);
            c.FoodVol[i] = rng.Range(8f, 40f) * 4f;
            c.FoodAlive[i] = true;
        }
        c.FoodCount = nf;
        c.RunWarmup();
        foreach (var e in c.Events) if (e.Kind == PhysarumEventKind.Digest) c.CreditDigest(c.FoodVol[e.Food]);
        c.Events.Clear();
        return c;
    }

    /// <summary>P6 (round 11f-2, Docs/ECOLOGY_LOD.md §6.3): flora is never LOD'd, but with nobody near the grove its
    /// network runs on slowed time (ThreatGrove.FarScale -> ThreatFloraMath.FarTimeScale). Same steps, fewer a second:
    /// the ledger closes every frame, the network time is exactly the dilated sum, the cost drops by the factor.</summary>
    static void FarCadence()
    {
        // the rule
        var centre = new Vector3(0, 0, 1000);
        Span<Vector3> pilots = stackalloc Vector3[2];
        pilots[0] = new Vector3(0, 0, 0); pilots[1] = centre + new Vector3(0, 399, 0);
        Program.Check(ThreatFloraMath.FarTimeScale(centre, 400f, 0.25f, pilots) == 1f
                      && ThreatFloraMath.FarTimeScale(centre, 400f, 0.25f, pilots.Slice(0, 1)) == 0.25f
                      && ThreatFloraMath.FarTimeScale(centre, 400f, 0.25f, ReadOnlySpan<Vector3>.Empty) == 0.25f
                      && ThreatFloraMath.FarTimeScale(centre, 400f, 1f, ReadOnlySpan<Vector3>.Empty) == 1f,
            "P6 far cadence: full rate while a pilot is within reach + margin, the far scale otherwise, never below 0");

        // the core under a near / far / near schedule (0.1 s frames, 60 s)
        const float scale = 0.25f, dt = 0.1f;
        var full = GameCore(5);
        var far = GameCore(5);
        double expect = far.Time, worst = 0, m0 = far.Reserve + far.TubeVolumeTotal + far.HeartBodies;
        long tFull = 0, tFar = 0;
        var sw = new Stopwatch();
        int farFrames = 0;
        for (int k = 0; k < 600; k++)
        {
            bool near = k >= 200 && k < 300;
            float s = near ? 1f : scale;
            if (!near) farFrames++;
            sw.Restart(); full.Advance(dt); tFull += near ? 0 : sw.ElapsedTicks;
            foreach (var e in full.Events) if (e.Kind == PhysarumEventKind.Digest) full.CreditDigest(full.FoodVol[e.Food]);
            sw.Restart(); far.Advance(dt * s); tFar += near ? 0 : sw.ElapsedTicks;
            foreach (var e in far.Events) if (e.Kind == PhysarumEventKind.Digest) far.CreditDigest(far.FoodVol[e.Food]);
            expect += dt * s;
            worst = Math.Max(worst, Math.Abs(far.Audit()));
        }
        double ratio = (double)tFar / Math.Max(1, tFull);
        Console.WriteLine($"   far cadence x{scale}: network time {far.Time:F2} s (expected {expect:F2}); {far.TubeCount} tubes " +
                          $"(full rate {full.TubeCount}); ledger worst {worst:E2} (full rate {full.Audit():E2}); " +
                          $"far-frame cost {ratio:P0} of full rate");
        Program.Check(Math.Abs(far.Time - expect) < 0.11, "P6 the network time is the dilated sum (to one 0.1 s step)");
        Program.Check(worst < 1e-6 && Math.Abs(full.Audit()) < 1e-6, $"P6 mass exact every frame on slowed time ({worst:E2})");
        Program.Check(far.TubeCount > 0 && far.TubeCount <= far.P.MaxTubes, "P6 the network keeps its tubes, under the cap");
        Program.Check(ratio < 0.45, $"P6 far frames cost {ratio:P0} of full rate (x{scale} expected, < 45%)");
    }

    /// <summary>A hand-laid straight cable through the research grove, a pacemaker at one end.</summary>
    static PhysarumCore Cable(PhysarumParams p, out List<int> line)
    {
        p.On = 1e9f; p.Off = -1f; p.AgentCount = 64; p.HeartSpeed = 0f; p.Warmup = 0;
        var c = new PhysarumCore(p, ThreatGroveShape.MakeBall(new Vector3(0, 0, 600), 450f), 11);
        int iy = c.NY / 2, iz = c.NZ / 2;
        line = new List<int>();
        c.AddHeart(c.Centre((6 * c.NY + iy) * c.NZ + iz), 1e6f);
        for (int ix = 8; ix < 48; ix++)
        {
            int v = (ix * c.NY + iy) * c.NZ + iz;
            c.LayTubeAt(v);
            line.Add(v);
        }
        c.Events.Clear();
        return c;
    }

    /// <summary>P1: the pulse runs down a tube at WaveSpeed (64.7 u/s), stings for ExciteTicks and cannot be
    /// re-excited for the refractory that follows; a beat faster than the refractory is skipped.</summary>
    static void WaveSpeedAndRefractory()
    {
        var p = Best();
        var c = Cable(p, out var line);
        var first = new float[line.Count];
        for (int i = 0; i < first.Length; i++) first[i] = -1f;
        var onsets = new List<float>();
        bool prevOn = false;
        int mid = line[20];
        float dangerSince = -1f, maxDanger = 0f;
        for (int k = 0; k < 1500; k++)
        {
            c.Advance(0.02f);
            for (int i = 0; i < line.Count; i++) if (first[i] < 0 && c.Danger[line[i]]) first[i] = c.Time;
            bool on = c.Danger[mid];
            if (on && !prevOn) { onsets.Add(c.Time); dangerSince = c.Time; }
            if (!on && prevOn) maxDanger = MathF.Max(maxDanger, c.Time - dangerSince);
            prevOn = on;
        }
        // fit the front's speed over the cable (first arrival at voxel 2 .. 37)
        float speed = (35 * c.H) / (first[37] - first[2]);
        float tick = c.H / p.WaveSpeed;
        Console.WriteLine($"   wave: {speed:F1} u/s over {35 * c.H:F0} u (research {p.WaveSpeed:F1}); voxel {c.H:F2} u, tick {tick:F3} s; " +
                          $"danger {maxDanger:F2} s per pass, then {p.RefractoryTicks * tick:F2} s refractory");
        Program.Near("P1 wave speed down a tube", speed, p.WaveSpeed, p.WaveSpeed * 0.04f);
        Program.Near("P1 a tube stings ExciteTicks wave ticks", maxDanger, p.ExciteTicks * tick, 0.03f);
        bool every = onsets.Count >= 2;
        // a pacemaker re-arms Period after it FIRED and fires on the first wave tick at or after that, and wave
        // ticks land on sim-step boundaries (physarum.py _waves: next_beat = t + period inside the tick loop), so
        // the pulse reaches mid-cable Period apart give or take one sim step, late by at most one wave tick
        float lo = p.Period - p.StepSeconds - 0.03f, hi = p.Period + tick + p.StepSeconds + 0.03f;
        for (int i = 1; i < onsets.Count; i++)
        {
            float gap = onsets[i] - onsets[i - 1];
            every &= gap >= lo && gap <= hi;
        }
        Program.Check(every && onsets.Count >= 5, $"P1 every {p.Period} s beat passes (period > refractory), each gap in [{lo:F2}, {hi:F2}] s: " +
                                                   $"onsets {string.Join(", ", onsets.ConvertAll(x => x.ToString("F2")))}");

        var fast = Best(); fast.Period = 1.0f;     // faster than ExciteTicks + RefractoryTicks = 1.49 s
        var c2 = Cable(fast, out var line2);
        var on2 = new List<float>();
        prevOn = false;
        for (int k = 0; k < 1500; k++)
        {
            c2.Advance(0.02f);
            bool on = c2.Danger[line2[20]];
            if (on && !prevOn) on2.Add(c2.Time);
            prevOn = on;
        }
        float minGap = float.MaxValue;
        for (int i = 1; i < on2.Count; i++) minGap = MathF.Min(minGap, on2[i] - on2[i - 1]);
        float refr = (fast.ExciteTicks + fast.RefractoryTicks) * tick;
        Program.Check(on2.Count >= 3 && minGap >= refr - 0.03f,
            $"P1 a 1 s pacemaker cannot beat faster than the refractory: pulses {minGap:F2} s apart (>= {refr:F2} s)");
        Program.Check(Math.Abs(c.Audit()) < 1e-6, $"P1 ledger closes ({c.Audit():E2})");
    }

    /// <summary>P2: the telegraph - a burned pilot saw that pulse coming. The research's wander runs, 3 seeds x 2 min;
    /// its p10 lead was 3.6 s (wander + reader), the best of any species that re-routes.</summary>
    static void Telegraph(bool quick)
    {
        var leads = new List<float>();
        int hits = 0, beat = 0;
        double worst = 0;
        float minutes = 0;
        foreach (ulong seed in quick ? new ulong[] { 7 } : new ulong[] { 7, 23, 41, 101, 202 })
        {
            var ar = new FloraArena(seed);
            ar.GroveMass(1600, 20);
            var sp = new PhysarumSpecies(ar, Best(), seed * 17 + 3);
            var pl = ar.AddPilot("wander", 120f, "wanderer");
            double m0 = ar.LiveVolume() + sp.MassTotal + sp.Core.Lost;
            for (int k = 0; k < 1200; k++) { sp.Step(0.1f); ar.Step(0.1f); }
            double m1 = ar.LiveVolume() + sp.MassTotal + sp.Core.Lost - ar.Created;
            worst = Math.Max(worst, Math.Abs(m1 - m0) / m0);
            worst = Math.Max(worst, Math.Abs(sp.Core.Audit()) / m0);
            leads.AddRange(sp.Leads);
            hits += pl.Hits.Count; beat += sp.BeatBurns; minutes += 2f;
            Console.WriteLine($"   seed {seed}: {sp.Core.TubeCount} tubes, {pl.Hits.Count} burns ({sp.BeatBurns} by a beat), " +
                              $"{sp.Bumps} tubes rammed, reserve {sp.Core.Reserve:F0}, digested {sp.Core.Digested:F0}");
        }
        float p10 = FloraArena.Percentile(leads, 10), med = FloraArena.Percentile(leads, 50);
        Console.WriteLine($"   telegraph: {leads.Count} burns, lead p10 {p10:F2} s, median {med:F2} s (research p10 3.60, median 5.0 capped); " +
                          $"{hits / minutes:F2} hits/min (research 1.33); mass drift {worst:E2}");
        Program.Check(leads.Count >= 2, $"P2 a wandering pilot meets the network ({leads.Count} burns)");
        // The research's own bar (harness.py score: a lead under 0.7 s is UNWARNED, p10 >= 0.7 s is full marks).
        // Its 3.6 s p10 is the decile of ~8 burns across wander + reader runs, and its element variants of the same
        // network scored 0.90-0.92 s (results/summary.md), so 3.6 is not a stable target; 0.7 s is.
        Program.Check(p10 >= 0.7f, $"P2 telegraph p10 {p10:F2} s >= 0.7 s (the research's unwarned bar): the worst decile still saw the pulse coming");
        Program.Check(med >= 2.0f, $"P2 telegraph median {med:F2} s >= 2.0 s: a typical burn was visible for seconds");
        Program.Check(worst < 1e-9, $"P2 mass audit closes to rounding ({worst:E2})");
    }

    /// <summary>P3: re-routing after a cut (reroute.py): at t = 60 s a 120 u ball through the densest tubes is cut;
    /// the threat inside it returns to 50% (research seeds 7/23/41/101/202: 26.1 / never / 17.1 / 17.1 / 11.6 s).</summary>
    static void Reroute(bool quick)
    {
        var t50s = new List<float>();
        var ends = new List<float>();
        foreach (ulong seed in quick ? new ulong[] { 7 } : new ulong[] { 7, 23, 41, 101, 202 })
        {
            var ar = new FloraArena(seed);
            ar.GroveMass(1600, 20);
            var sp = new PhysarumSpecies(ar, Best(), seed * 17 + 3);
            ar.AddPilot("wander", 120f, "wanderer");
            const float dt = 0.1f, tCut = 60f, after = 90f, r = 120f;
            Vector3 c = default;
            int pre = -1;
            float t50 = 999f, endFrac = 0f;
            var series = new List<(float, int)>();
            for (int k = 0; k < (int)((tCut + after) / dt); k++)
            {
                sp.Step(dt); ar.Step(dt);
                if (pre < 0 && ar.T >= tCut)
                {
                    var tubes = new List<Vector3>();
                    foreach (int v in sp.Core.TubeVoxels()) tubes.Add(sp.Core.Centre(v));
                    int stride = Math.Max(1, tubes.Count / 300);
                    pre = 0;
                    for (int i = 0; i < tubes.Count; i += stride)
                    {
                        int dens = 0;
                        foreach (var q in tubes) if ((q - tubes[i]).Length() < r) dens++;
                        if (dens > pre) { pre = dens; c = tubes[i]; }
                    }
                    sp.Core.RemoveBall(c, r);
                }
                else if (pre > 0 && k % 5 == 0)
                {
                    int n = 0;
                    foreach (int v in sp.Core.TubeVoxels()) if ((sp.Core.Centre(v) - c).Length() < r) n++;
                    series.Add((ar.T - tCut, n));
                }
            }
            foreach (var (t, n) in series) if (n >= 0.5f * pre) { t50 = t; break; }
            endFrac = series.Count > 0 ? series[series.Count - 1].Item2 / (float)Math.Max(pre, 1) : 0f;
            t50s.Add(t50); ends.Add(endFrac);
            Console.WriteLine($"   reroute seed {seed}: pre {pre} tubes in the ball, t50 {(t50 >= 999 ? "never" : t50.ToString("F1") + " s")}, " +
                              $"back to {endFrac:P0} at +90 s; ledger {sp.Core.Audit():E2}");
        }
        int back = 0;
        foreach (float t in t50s) if (t < 999f) back++;
        t50s.Sort(); ends.Sort();
        float med = t50s[t50s.Count / 2], endMed = ends[ends.Count / 2];
        // The research on the SAME five seeds (reroute.py one(), searched-best params): t50 26.1 / never / 17.1 /
        // 17.1 / 11.6 s (median 17.1, 4 of 5 back to half), end fraction 0.89 / 0.00 / 0.80 / 0.14 / 0.00 (median
        // 0.14) - recovery to half is reliable, holding it is not (the regrown tubes are resorbed again as the
        // agents re-route), so the asserted claim is the half-return, not the end fraction.
        Console.WriteLine($"   reroute: median t50 {med:F1} s, {back}/{t50s.Count} seeds half back within 90 s, median end {endMed:P0} " +
                          "(research same seeds: median t50 17.1 s, 4/5 half back, median end 14%)");
        Program.Check(med >= 5f && med <= 52f, $"P3 a cut cable is half back in a median {med:F1} s (research 17.1 on these seeds; band [5, 52])");
        Program.Check(back * 2 > t50s.Count, $"P3 most cuts re-find the gap: {back}/{t50s.Count} seeds back to half within 90 s (research 4/5)");
    }

    /// <summary>P4: the GAME grove - a sector of the Swarm cell's rim shell (Docs/THREAT_FLORA.md §4) - forms a
    /// network from its planted reserve plus the food in it, keeps under its tube cap, beats, and audits.</summary>
    static void GameGrove()
    {
        var p = ThreatGroveDefaults.Physarum().ForElement(ThreatGroveDefaults.PhysarumElement);   // as authored (Space)
        var shape = ThreatGroveDefaults.SwarmCellGrove(Vector3.Zero);
        var c = new PhysarumCore(p, shape, 5);
        var rng = new ThreatRng(5);
        for (int k = 0; k < ThreatGroveDefaults.Sclerotia; k++)
            c.AddHeart(shape.Sample(ref rng, 40f), ThreatGroveDefaults.PlantedVolumePerSclerotium);
        // food: the grove's own clumps (what the cell's flora, skeletons and trails leave in it)
        int nf = 400;
        c.EnsureFood(nf);
        var centres = new Vector3[10];
        for (int i = 0; i < 10; i++) centres[i] = shape.Sample(ref rng, 30f);
        for (int i = 0; i < nf; i++)
        {
            c.FoodPos[i] = shape.Clamp(centres[i % 10] + rng.GaussianVector() * 25f, 8f);
            c.FoodVol[i] = rng.Range(8f, 40f) * 4f;
            c.FoodAlive[i] = true;
        }
        c.FoodCount = nf;
        var sw = Stopwatch.StartNew();
        c.RunWarmup();
        double warmMs = sw.Elapsed.TotalMilliseconds;
        int lays = 0, resorbs = 0, dangerOn = 0, beats = 0;
        foreach (var e in c.Events)
        {
            if (e.Kind == PhysarumEventKind.Lay) lays++;
            if (e.Kind == PhysarumEventKind.Digest) c.CreditDigest(c.FoodVol[e.Food]);
        }
        int warmTubes = c.TubeCount;
        sw.Restart();
        int steps = 0;
        for (int k = 0; k < 600; k++)
        {
            c.Advance(0.1f); steps++;
            foreach (var e in c.Events)
            {
                if (e.Kind == PhysarumEventKind.Lay) lays++;
                if (e.Kind == PhysarumEventKind.Resorb) resorbs++;
                if (e.Kind == PhysarumEventKind.DangerOn) dangerOn++;
                if (e.Kind == PhysarumEventKind.BeatOn) beats++;
                if (e.Kind == PhysarumEventKind.Digest) c.CreditDigest(c.FoodVol[e.Food]);
            }
        }
        double ms = sw.Elapsed.TotalMilliseconds / steps;
        Console.WriteLine($"   game grove: {c.NX}x{c.NY}x{c.NZ} voxels ({c.InsideCount} inside), {c.AgentCount} agents; warm-up {warmMs:F0} ms -> {warmTubes} tubes; " +
                          $"after 60 s {c.TubeCount} tubes (cap {p.MaxTubes}), {lays} lays / {resorbs} resorbs, {dangerOn} pulse-voxel flips, {beats} beats; " +
                          $"{ms:F3} ms per 10 Hz step; ledger {c.Audit():E2}");
        Program.Check(c.TubeCount >= 60, $"P4 the Swarm-cell grove forms a network ({c.TubeCount} tubes)");
        Program.Check(c.TubeCount <= p.MaxTubes, $"P4 and keeps under its collider-budget cap ({p.MaxTubes})");
        Program.Check(dangerOn > 0 && beats > 0, "P4 pulses run and hearts beat");
        Program.Check(Math.Abs(c.Audit()) < 1e-6, $"P4 ledger closes ({c.Audit():E2})");
        Program.Check(ms < 2.0, $"P4 {ms:F3} ms per step (< 2 ms on one CoreCLR thread; 10 Hz -> {ms * 10:F1} ms of CPU per second)");
    }

    /// <summary>P5: cost per step at the research's sizes (cost.py: Burst 0.39 ms/frame @60 Hz at 16k / 56^3,
    /// 0.23 ms/frame @10 Hz at 32k / 64^3 with 4 workers). One CoreCLR thread, scalar.</summary>
    static void Cost(bool quick)
    {
        foreach (var (agents, g) in new[] { (16000, 56), (32000, 64) })
        {
            var p = Best();
            p.AgentCount = agents; p.Warmup = 0; p.VoxelSize = 900f / g;
            var c = new PhysarumCore(p, ThreatGroveShape.MakeBall(new Vector3(0, 0, 600), 450f), 3);
            var rng = new ThreatRng(3);
            for (int k = 0; k < 5; k++) c.AddHeart(new Vector3(0, 0, 600) + rng.OnUnitSphere() * 100f, 2400f);
            for (int k = 0; k < 20; k++) c.Advance(0.1f);
            var sw = Stopwatch.StartNew();
            int n = quick ? 20 : 60;
            for (int k = 0; k < n; k++) c.Advance(0.1f);
            double ms = sw.Elapsed.TotalMilliseconds / n;
            Console.WriteLine($"   cost {agents / 1000}k agents / {g}^3: {ms:F2} ms per step on one thread " +
                              $"(@10 Hz {ms * 10:F0} ms CPU/s; research numpy 20.7 ms/step at 16k/56^3)");
            Program.Check(ms < 40.0, $"P5 {agents / 1000}k / {g}^3 steps in {ms:F2} ms (< 40 ms, one scalar thread)");
        }
    }
}
