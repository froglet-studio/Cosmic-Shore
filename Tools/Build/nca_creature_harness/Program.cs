// Tools/Build/nca_creature_harness - the SHIPPED NCA creature core (NcaVoxelCore.cs) against the lab's own runtime,
// plus the behaviours the game glue leans on. Run through run.sh; exit code non-zero on any failure.
//
//   parity  <weights asset> <wasm ref> <js ref>     exactness vs the lab's runtime, both backends (and a negative control)
//   gates   <weights asset>                         growth, the swim, cut-and-regrow, scars held until paid, the
//                                                   threaded ticker == the inline core, the body frame and segments
//   measure <weights asset> [--assert]              the grown size and the steps to grow it (what the author script
//                                                   writes as grown_voxels / grow_steps), across seeds
//   bench   <weights asset>                         ms per step of a grown creature
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Gameplay;

static class Program
{
    static int s_fail;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
        if (!ok) s_fail++;
    }

    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("usage: parity|gates|measure|bench <weights> [...]"); return 2; }
        var weights = NcaVoxelWeights.Parse(File.ReadAllText(args[1]));
        Console.WriteLine($"{weights.Species}: grid {weights.D}x{weights.H}x{weights.W}, C={weights.C}, hidden={weights.Hid}, " +
                          $"fire {weights.FireRate}, grown {weights.GrownVoxels} voxels in {weights.GrowSteps} steps");
        switch (args[0])
        {
            case "parity": Parity(weights, args[2], args[3], args[1]); break;
            case "gates": Gates(weights); break;
            case "measure": Measure(weights, args.Contains("--assert")); break;
            case "bench": Bench(weights); break;
            default: Console.Error.WriteLine($"unknown mode {args[0]}"); return 2;
        }
        Console.WriteLine(s_fail == 0 ? "OK" : $"FAILED: {s_fail}");
        return s_fail == 0 ? 0 : 1;
    }

    // ───────────────────────────────────────────────────────────── parity

    static float[] Decode(string b64)
    {
        var bytes = Convert.FromBase64String(b64);
        var f = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, f, 0, bytes.Length);
        return f;
    }

    /// <summary>Replay the lab reference: same seed, same steps, same cut. Returns the worst relative state error.</summary>
    static (double worstState, int countMiss, double worstSum, int removed) Replay(NcaVoxelWeights w, JsonElement r, bool print)
    {
        var core = new NcaVoxelCore(w, (uint)r.GetProperty("seed").GetInt32());
        var track = r.GetProperty("track").EnumerateArray().Select(e => (s: e[0].GetInt32(), n: e[1].GetInt32(), sum: e[2].GetDouble()))
                     .ToDictionary(t => t.s);
        var states = r.GetProperty("states");
        var hit = r.GetProperty("hit");
        double worstState = 0, worstSum = 0; int countMiss = 0, removed = -1;
        for (int s = 1; s <= 300; s++)
        {
            if (s == 201)
            {
                removed = core.Hit((float)hit.GetProperty("gz").GetDouble(), (float)hit.GetProperty("gy").GetDouble(),
                                   (float)hit.GetProperty("gx").GetDouble(), (float)hit.GetProperty("r").GetDouble());
            }
            core.Step();
            if (track.TryGetValue(s, out var t))
            {
                int n = core.Count();
                double sum = core.Sum();
                double rel = Math.Abs(sum - t.sum) / Math.Max(1.0, Math.Abs(t.sum));
                if (n != t.n) countMiss++;
                worstSum = Math.Max(worstSum, rel);
                if (print && (s % 50 == 0 || s == 1))
                    Console.WriteLine($"    step {s,3}: count {n,5} (lab {t.n,5})  sum {sum,12:F4} (lab {t.sum,12:F4})");
            }
            if (states.TryGetProperty(s.ToString(CultureInfo.InvariantCulture), out var st))
            {
                var lab = Decode(st.GetString()!);
                var mine = core.State;
                double worst = 0, scale = 1e-6;
                for (int i = 0; i < lab.Length; i++) scale = Math.Max(scale, Math.Abs(lab[i]));
                for (int i = 0; i < lab.Length; i++) worst = Math.Max(worst, Math.Abs(lab[i] - mine[i]));
                worstState = Math.Max(worstState, worst / scale);
                if (print) Console.WriteLine($"    step {s,3}: full state max |diff| / max |x| = {worst / scale:E2}");
            }
        }
        return (worstState, countMiss, worstSum, removed);
    }

    static void Parity(NcaVoxelWeights w, string wasmRef, string jsRef, string weightsPath)
    {
        JsonElement r = default;
        foreach (var (path, exact) in new[] { (wasmRef, true), (jsRef, false) })
        {
            r = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
            string backend = r.GetProperty("backend").GetString()!;
            double tol = exact ? 0.0 : 1e-5;
            Console.WriteLine($"parity vs the lab runtime (nca_creature.js, {backend} backend, seed 7) - " +
                              (exact ? "the kernel this core mirrors, so EXACT:" : "f64 accumulation, so to 1e-5:"));
            var (worstState, countMiss, worstSum, removed) = Replay(w, r, true);
            int labRemoved = r.GetProperty("hit").GetProperty("removed").GetInt32();
            Check(worstState <= tol, $"full 16-channel state at steps 1, 50, 200 (before the cut) and 300 (100 steps after it): " +
                                     $"worst |diff| / max|x| = {worstState:E2} <= {tol:E0}");
            Check(countMiss == 0, $"body voxel count equal at every checkpoint ({countMiss} misses of {r.GetProperty("track").GetArrayLength()})");
            Check(worstSum <= tol, $"whole-state sum within {tol:E0} relative at every checkpoint (worst {worstSum:E2})");
            Check(removed == labRemoved, $"the radius-7 cut removed {removed} body voxels (lab {labRemoved})");
        }

        // negative control: one weight nudged by 1e-3 must be caught by the same comparisons
        var bent = NcaVoxelWeights.Parse(File.ReadAllText(weightsPath).Replace("\"species\"", "\"species_nudged\": 1, \"species\""));
        bent.W2 = (float[])bent.W2.Clone();
        bent.W2[3 * bent.Hid + 7] += 1e-3f;   // alpha channel, one hidden unit
        bent.Rebuild();
        var (ns, nc, nsum, _) = Replay(bent, r, false);
        Check(ns > 1e-4 || nc > 0 || nsum > 1e-4,
              $"negative control: alpha weight nudged by 1e-3 is detected (state {ns:E2}, count misses {nc}, sum {nsum:E2})");
    }

    // ───────────────────────────────────────────────────────────── gates

    static void Gates(NcaVoxelWeights w)
    {
        Console.WriteLine("behaviour gates (the lab's test_nca_creature.js, plus what the game glue relies on):");
        var core = new NcaVoxelCore(w, 1) { Respawn = false };
        var counts = new List<int>();
        for (int s = 0; s < 600; s++) { core.Step(); counts.Add(core.Count()); }
        int at200 = counts[199];
        Check(at200 > 500, $"grows from one seed cell: {at200} body voxels at step 200 (lab gate > 500)");

        // keeps animating: voxels flip in and out every step while the size holds
        var prev = new bool[core.N];
        var grid = new bool[core.N];
        long flips = 0; int frames = 0;
        for (int s = 0; s < 136; s++)
        {
            core.Step();
            var st = core.State;
            for (int i = 0; i < core.N; i++) grid[i] = st[i * core.C + 3] > 0.3f;
            if (s > 0) { for (int i = 0; i < core.N; i++) if (grid[i] != prev[i]) flips++; frames++; }
            Array.Copy(grid, prev, core.N);
        }
        double flipRate = (double)flips / frames;
        Check(flipRate > 1, $"keeps swimming when grown: {flipRate:F1} voxels flip per step (lab gate > 1)");

        // the swim's tail beat: bend changes sign over a cycle
        var fr = new NcaVoxelFrame(core.N, 6);
        float bMin = float.MaxValue, bMax = float.MinValue;
        for (int s = 0; s < 80; s++) { core.Step(); core.BuildFrame(fr); bMin = Math.Min(bMin, fr.Bend); bMax = Math.Max(bMax, fr.Bend); }
        Check(bMax - bMin > 1f, $"the tail beats: bend swings {bMin:F2} .. {bMax:F2} voxels over 80 steps");

        // cut and regrow
        int before = core.Count();
        core.BuildFrame(fr);
        w.BodyToGrid(fr.SegCentre[3 * 2], fr.SegCentre[3 * 2 + 1], fr.SegCentre[3 * 2 + 2], out float gz, out float gy, out float gx);
        int removed = core.Hit(gz, gy, gx, 5f);
        int after = core.Count();
        int back = -1;
        for (int s = 1; s <= 200; s++) { core.Step(); if (back < 0 && core.Count() >= 0.9 * before) back = s; }
        Check(removed > 0 && after == before - removed, $"a radius-5 cut through the trunk removes {removed} of {before} body voxels");
        Check(back > 0 && back <= 80, $"it regrows to 90% in {back} steps (<= 80)");

        // scars: a cut held open until paid for
        var tick = new NcaVoxelTicker(new NcaVoxelCore(w, 3) { Respawn = false }, 6);
        tick.Grow(400);
        int whole = tick.Core.Count();
        tick.Cur.Reset(); tick.Core.BuildFrame(tick.Cur);
        var c0 = tick.Cur;
        w.BodyToGrid(c0.SegCentre[3 * 3], c0.SegCentre[3 * 3 + 1], c0.SegCentre[3 * 3 + 2], out gz, out gy, out gx);
        int cut = tick.Hit(gz, gy, gx, 5f);
        tick.AddScar(new NcaScar { Z = gz, Y = gy, X = gx, R = 5f, Debt = cut });
        for (int s = 0; s < 120; s++) { tick.Kick(false); tick.Collect(); }
        int held = tick.Core.Count();
        float left = tick.PayScars(cut * 0.5f);
        bool stillOpen = tick.Scars.Count == 1 && left == 0f;
        float spare = tick.PayScars(cut);
        for (int s = 0; s < 120; s++) { tick.Kick(false); tick.Collect(); }
        int healed = tick.Core.Count();
        Check(held <= whole - cut * 0.6, $"a scar holds the wound open: {held} body voxels after 120 steps (whole {whole}, cut {cut})");
        Check(stillOpen && tick.Scars.Count == 0 && Math.Abs(spare - cut * 0.5f) < 1e-3f,
              $"paying half keeps it open; paying the rest heals it and returns the change ({spare:F1})");
        Check(healed >= 0.9 * whole, $"once paid it regrows: {healed} of {whole}");

        // the threaded ticker steps exactly as the core does inline
        var a = new NcaVoxelCore(w, 11);
        var tb = new NcaVoxelTicker(new NcaVoxelCore(w, 11), 6);
        int kicked = 0;
        while (kicked < 60)
        {
            if (tb.Kick(true)) kicked++;
            while (tb.Busy) System.Threading.Thread.Sleep(0);
            tb.Collect();
        }
        for (int s = 0; s < 60; s++) a.Step();
        bool same = a.Count() == tb.Core.Count() && Math.Abs(a.Sum() - tb.Core.Sum()) < 1e-9;
        Check(same && tb.Error == null, $"the off-thread ticker matches the inline core after 60 steps ({tb.Core.Count()} vs {a.Count()})");
        Check(tb.Cur.Steps == 60 && tb.Prev.Steps == 59, $"frames rotate: current is step {tb.Cur.Steps}, previous {tb.Prev.Steps}");

        // the body frame: head at +Z, segments ordered head to tail, the head slab at the front
        core.BuildFrame(fr);
        bool ordered = true;
        for (int g = 1; g < fr.Segments; g++)
            if (fr.SegN[g] > 0 && fr.SegN[g - 1] > 0 && fr.SegCentre[3 * g + 2] > fr.SegCentre[3 * (g - 1) + 2]) ordered = false;
        int filled = fr.SegN.Count(n => n > 0);
        Check(filled == fr.Segments && ordered, $"{filled}/{fr.Segments} body segments filled, centroids ordered head (+Z) to tail");
        Check(fr.FrontZ - fr.BackZ > 20f, $"the body is long along its axis: {fr.FrontZ - fr.BackZ:F1} voxels head to tail");
        Check(fr.HeadZ > fr.FrontZ - 2.01f && fr.HeadZ <= fr.FrontZ, $"the head slab sits at the front ({fr.HeadZ:F1} of {fr.FrontZ:F1})");
        int surface = 0, interior = 0;
        for (int k = 0; k < fr.Count; k++) { if (fr.Interior[fr.List[k]] != 0) interior++; else surface++; }
        Check(interior > 0 && surface > 0, $"surface cull: {surface} skin cells drawn, {interior} interior culled");

        // grid <-> body round trip
        double worst = 0;
        for (int z = 0; z < w.D; z += 5) for (int y = 0; y < w.H; y += 7) for (int x = 0; x < w.W; x += 7)
        {
            w.GridToBody(z, y, x, out float bx, out float by, out float bz);
            w.BodyToGrid(bx, by, bz, out float rz, out float ry, out float rx);
            worst = Math.Max(worst, Math.Abs(rz - z) + Math.Abs(ry - y) + Math.Abs(rx - x));
        }
        Check(worst < 1e-3, $"grid -> body -> grid round trip (worst {worst:E1} voxels)");

        // death: a creature cut to nothing stays dead with Respawn off
        var dead = new NcaVoxelCore(w, 5) { Respawn = false };
        for (int s = 0; s < 150; s++) dead.Step();
        dead.Hit(w.D / 2f, w.H / 2f, w.W / 2f, 60f);
        dead.Step(); dead.Step();
        Check(dead.Extinct && dead.Count() == 0 && dead.Respawns == 0, "cut to nothing it stays dead (Respawn off): no reseed");
    }

    // ───────────────────────────────────────────────────────────── measure

    static void Measure(NcaVoxelWeights w, bool assert)
    {
        var grown = new List<double>();
        var steps = new List<int>();
        foreach (uint seed in new uint[] { 1, 2, 3, 4 })
        {
            var core = new NcaVoxelCore(w, seed) { Respawn = false };
            var c = new List<int>();
            for (int s = 0; s < 600; s++) { core.Step(); c.Add(core.Count()); }
            double g = c.Skip(300).Average();
            int reach = c.FindIndex(n => n >= 0.9 * g) + 1;
            grown.Add(g); steps.Add(reach);
            Console.WriteLine($"  seed {seed}: grown {g:F0} body voxels (steps 300-600), 90% reached at step {reach}");
        }
        int G = (int)Math.Round(grown.Average());
        int S = (int)Math.Ceiling(steps.Max() / 10.0) * 10;
        Console.WriteLine($"  -> grown_voxels {G}, grow_steps {S}");
        if (assert)
        {
            Check(Math.Abs(G - w.GrownVoxels) <= 0.03 * w.GrownVoxels,
                  $"committed grown_voxels {w.GrownVoxels} within 3% of the measured {G} (rerun author_nca_creatures.py if not)");
            Check(Math.Abs(S - w.GrowSteps) <= 20, $"committed grow_steps {w.GrowSteps} within 20 of the measured {S}");
        }
    }

    // ───────────────────────────────────────────────────────────── bench

    static void Bench(NcaVoxelWeights w)
    {
        var core = new NcaVoxelCore(w, 1) { Respawn = false };
        for (int s = 0; s < 200; s++) core.Step();
        long u0 = core.CellUpdates;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int n = 100;
        for (int s = 0; s < n; s++) core.Step();
        sw.Stop();
        var fr = new NcaVoxelFrame(core.N, 6);
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        for (int s = 0; s < n; s++) core.BuildFrame(fr);
        sw2.Stop();
        Console.WriteLine($"  grown {core.Count()} body voxels: {sw.Elapsed.TotalMilliseconds / n:F2} ms/step " +
                          $"({(core.CellUpdates - u0) / n} cell updates/step), frame build {sw2.Elapsed.TotalMilliseconds / n:F2} ms " +
                          $"[CoreCLR here; IL2CPP differs]");
    }
}
