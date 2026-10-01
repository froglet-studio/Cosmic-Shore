// The `field` swarm step, ported to plain C# (no Unity) as the shape a Burst job would take:
// struct-of-arrays per swarm, a uniform hash grid for neighbours (no O(n^2)), greedy slot assignment
// (one sort, no Hungarian), plan choice by element ratios with time hysteresis, true-breeding laying,
// molting, the morph vortex and the vessel reaction. Mirrors Tools/NCA/field_swarm.py (mode=slots).
//
// Driven by Tools/NCA/field_port_check.py, which exports the plans + seeds, runs the yardstick protocol
// here (grow 240, cull the majority as swarm_nca.lose_majority "excess" does, 240 more), and scores the
// dumped states with the unchanged Python scorer.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

public sealed class Plan
{
    public string Kind = "";
    public int N, F;
    public Vector3[][] P = Array.Empty<Vector3[]>();   // [frame][slot], centred
    public int[] Order = Array.Empty<int>();
    public int[] Elem = Array.Empty<int>(), Slot = Array.Empty<int>(), Mix = new int[4], SlotMix = new int[3];
    public int NSlots;
    public int FrameSteps = 6;

    public void At(float t, Vector3[] outP, Vector3[] outV)
    {
        float u = t / FrameSteps; int L = Order.Length;
        int i = ((int)MathF.Floor(u)) % L; int j = (i + 1) % L; float a = u - MathF.Floor(u);
        var pi = P[Order[i]]; var pj = P[Order[j]];
        for (int k = 0; k < N; k++) { outP[k] = Vector3.Lerp(pi[k], pj[k], a); outV[k] = (pj[k] - pi[k]) / FrameSteps; }
    }
}

public struct Predator { public Vector3 C, V; public float R; }

public sealed class Cfg
{
    public int Dwell = 12; public float KArrive = 0.35f, Accel = 0.45f, SepR = 2f, SepK = 0.5f, AlignR = 5f, AlignK = 0.15f;
    public float[] VMax = { 0.8f, 0.8f, 0.8f, 2f };
    public int ReassignEvery = 8; public float Sticky = 4f;
    public float LayRate = 0.04f; public int LayMax = 4; public float RBud = 2.6f;
    public float MoltRate = 0.03f; public int MoltSteps = 10;
    public int MorphSteps = 60; public float Swirl = 0.9f;
    public float Sense = 2.2f, Relay = 0.8f, StartleDecay = 0.9f, Lookahead = 10f, FleeSwirl = 0.8f;
    public float[] Flee = { 0.6f, 0.5f, 1.4f, 2f };
    public float Membrane = 80f;
}

public sealed class FieldSwarm
{
    public static readonly int[] Major = { 1, 2, 0, 3 };            // plan index (mass, space, charge, time) -> element
    public readonly Plan[] Plans; public readonly Cfg C;
    public readonly int Cap;
    // per tadpole (struct of arrays)
    public Vector3[] Pos, Vel; public int[] Elem, Dom, Home, MoltTo; public float[] Molt, Startle; public bool[] Alive;
    // per swarm
    public int PlanIx, Cand, CandN, MorphFrom = -1, MorphT, LastAssign = -1000000, Clock; public float T;
    public Vector3 Anchor; public int[] Perm = { 0, 1, 2 }; public bool PermSet; public List<string> Switches = new();
    public float Threat;
    readonly Vector3[] _sp, _sv; readonly Random _rng;
    // grid
    readonly int[] _cellStart, _cellCount, _sorted, _cellOf; const int G = 4096;

    public FieldSwarm(Plan[] plans, Cfg c, int cap, int seed)
    {
        Plans = plans; C = c; Cap = cap; _rng = new Random(seed);
        Pos = new Vector3[cap]; Vel = new Vector3[cap]; Elem = new int[cap]; Dom = new int[cap]; Home = new int[cap];
        MoltTo = new int[cap]; Molt = new float[cap]; Startle = new float[cap]; Alive = new bool[cap];
        int mx = plans.Max(p => p.N); _sp = new Vector3[mx]; _sv = new Vector3[mx];
        _cellStart = new int[G]; _cellCount = new int[G]; _sorted = new int[cap]; _cellOf = new int[cap];
        for (int i = 0; i < cap; i++) Home[i] = -1;
    }

    public void Begin()
    {
        var c = Counts(false); PlanIx = Array.IndexOf(Major, ArgMax(c)); Cand = PlanIx;
        Vector3 s = Vector3.Zero; int n = 0; for (int i = 0; i < Cap; i++) if (Alive[i]) { s += Pos[i]; n++; }
        Anchor = s / Math.Max(n, 1);
    }

    int[] Counts(bool eff)
    {
        var c = new int[4];
        for (int i = 0; i < Cap; i++) if (Alive[i]) c[eff && Molt[i] > 0 ? MoltTo[i] : Elem[i]]++;
        return c;
    }
    static int ArgMax(int[] a) { int b = 0; for (int i = 1; i < a.Length; i++) if (a[i] > a[b]) b = i; return b; }

    public void Step(Predator[] preds)
    {
        var plan = Plans[PlanIx];
        // 1. plan by element ratios + hysteresis
        var counts = Counts(true);
        int cur = Major[PlanIx], top = ArgMax(counts); int maj = counts[cur] == counts[top] ? cur : top;
        bool contested = false;
        if (maj != cur)
        {
            int cp = Array.IndexOf(Major, maj);
            if (Cand == cp) CandN++; else { Cand = cp; CandN = 1; }
            if (CandN >= C.Dwell)
            {
                Switches.Add($"{Clock}:{PlanIx}->{cp}"); MorphFrom = PlanIx; PlanIx = cp; MorphT = 0; LastAssign = -1000000; PermSet = false;
                for (int i = 0; i < Cap; i++) Molt[i] = 0; plan = Plans[PlanIx];
            }
            else contested = true;
        }
        else { Cand = PlanIx; CandN = 0; }
        if (!PermSet) { SetPerm(plan); PermSet = true; }
        T += 1f; plan.At(T, _sp, _sv);
        float swell = 1f + (PlanIx == 2 ? 0.45f : 0f) * Threat;
        // 2. composition
        if (!contested) { MoltStep(plan, counts); Lay(plan); }
        for (int i = 0; i < Cap; i++) if (Alive[i] && Molt[i] > 0)
        {
            Molt[i] += 1f / C.MoltSteps;
            if (Molt[i] >= 1f) { Elem[i] = MoltTo[i]; Molt[i] = 0; LastAssign = -1000000; }
        }
        // 3. homes
        if (Clock - LastAssign >= C.ReassignEvery) { AssignGreedy(plan); LastAssign = Clock; }
        // 4. steering
        BuildGrid();
        float u = MorphFrom >= 0 ? (float)MorphT / C.MorphSteps : 1f;
        if (MorphFrom >= 0 && u >= 1f) MorphFrom = -1;
        float amp = MorphFrom >= 0 ? C.Swirl * MathF.Sin(MathF.PI * u) : 0f;
        if (MorphFrom >= 0) MorphT++;
        Vector3 mean = Vector3.Zero, spMean = Vector3.Zero; int n = 0; float stSum = 0;
        for (int k = 0; k < plan.N; k++) spMean += _sp[k]; spMean /= plan.N;
        var newVel = new Vector3[Cap]; var newSt = new float[Cap];
        for (int i = 0; i < Cap; i++)
        {
            if (!Alive[i]) continue;
            Vector3 x = Pos[i], v = Vel[i], desired = Vector3.Zero;
            if (Home[i] >= 0) { var h = _sp[Home[i]] * swell + Anchor; desired = _sv[Home[i]] + C.KArrive * (h - x); }
            if (amp > 0)
            {
                var r = x - Anchor; var tang = Vector3.Cross(Vector3.UnitY, r); float tl = tang.Length();
                if (tl > 1e-3f) desired += amp * tang / tl * MathF.Sqrt(r.Length()) * 0.3f;
            }
            // neighbours from the grid
            Vector3 sep = Vector3.Zero, vs = Vector3.Zero; int cnt = 0; float st = Startle[i] * C.StartleDecay, relay = 0;
            Cell(x, out int cx, out int cy, out int cz);
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
            {
                int g = Hash(cx + dx, cy + dy, cz + dz);
                for (int q = _cellStart[g], e = q + _cellCount[g]; q < e; q++)
                {
                    int j = _sorted[q]; if (j == i) continue;
                    var d = x - Pos[j]; float dist = d.Length();
                    if (dist < C.SepR) sep += d / MathF.Max(dist, 1e-3f) * (C.SepR - dist);
                    if (dist < C.AlignR) { vs += Vel[j]; cnt++; relay = MathF.Max(relay, Startle[j]); }
                }
            }
            Vector3 align = cnt > 0 ? vs / cnt - v : Vector3.Zero;
            Vector3 flee = Vector3.Zero;
            foreach (var p in preds)
            {
                var rel = x - p.C; float dd = rel.Length(), sense = C.Sense * p.R, spd = MathF.Max(p.V.Length(), 1e-6f);
                var pvn = p.V / spd; float along = Vector3.Dot(rel, pvn); var lat = rel - along * pvn; float dl = lat.Length();
                var latn = lat / MathF.Max(dl, 1e-3f);
                float ahead = along > -p.R ? Math.Clamp(1 - along / (C.Lookahead * spd + p.R), 0, 1) : 0;
                float w = MathF.Max(Math.Clamp(1 - dl / sense, 0, 1) * ahead, Math.Clamp(1 - dd / sense, 0, 1));
                st = MathF.Max(st, Math.Clamp(1.4f * w, 0, 1));
                var radial = rel / MathF.Max(dd, 1e-3f);
                flee += w * C.Flee[Elem[i]] * (0.75f * latn + 0.25f * radial + C.FleeSwirl * 0.5f * Vector3.Cross(pvn, latn));
            }
            st = MathF.Max(st, relay * C.Relay);
            var steer = (1 - 0.8f * st) * desired + C.SepK * sep + C.AlignK * align + 2f * flee;
            v = (1 - C.Accel) * v + C.Accel * steer;
            float vmax = C.VMax[Elem[i]] * (1 + 0.8f * st), sp = v.Length();
            if (sp > vmax) v *= vmax / sp;
            newVel[i] = v; newSt[i] = st; stSum += st;
        }
        for (int i = 0; i < Cap; i++)
        {
            if (!Alive[i]) continue;
            Vel[i] = newVel[i]; Startle[i] = newSt[i]; var x = Pos[i] + Vel[i];
            float r = x.Length(); if (r > C.Membrane) x *= C.Membrane / r;
            Pos[i] = x; mean += x; n++;
        }
        if (n > 0) Anchor = 0.9f * Anchor + 0.1f * (mean / n - spMean);
        Threat = 0.85f * Threat + 0.15f * MathF.Min(1f, 3f * (n > 0 ? stSum / n : 0));
        Clock++;
    }

    // ---------------------------------------------------------------- grid
    void Cell(Vector3 x, out int cx, out int cy, out int cz)
    { cx = (int)MathF.Floor(x.X / C.AlignR); cy = (int)MathF.Floor(x.Y / C.AlignR); cz = (int)MathF.Floor(x.Z / C.AlignR); }
    static int Hash(int x, int y, int z) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791)) % G);
    void BuildGrid()
    {
        Array.Clear(_cellCount);
        for (int i = 0; i < Cap; i++) if (Alive[i]) { Cell(Pos[i], out var a, out var b, out var c); _cellOf[i] = Hash(a, b, c); _cellCount[_cellOf[i]]++; }
        int s = 0; for (int g = 0; g < G; g++) { _cellStart[g] = s; s += _cellCount[g]; }
        var fill = new int[G];
        for (int i = 0; i < Cap; i++) if (Alive[i]) { int g = _cellOf[i]; _sorted[_cellStart[g] + fill[g]++] = i; }
    }

    // ---------------------------------------------------------------- composition + assignment
    void SetPerm(Plan plan)
    {
        var dc = new int[3]; for (int i = 0; i < Cap; i++) if (Alive[i]) dc[Dom[i]]++;
        int[][] perms = plan.NSlots == 1 ? new[] { new[] { 0 } } : plan.NSlots == 2 ? new[] { new[] { 0, 1 }, new[] { 1, 0 } }
            : new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        int best = int.MaxValue;
        foreach (var p in perms)
        {
            int cost = 0; for (int s = 0; s < plan.NSlots; s++) cost += Math.Abs(dc[p[s]] - plan.SlotMix[s]);
            if (cost < best) { best = cost; Perm = new[] { p[0], p.Length > 1 ? p[1] : 1, p.Length > 2 ? p[2] : 2 }; }
        }
    }

    float Cost(int i, int k, Plan plan)
    {
        var w = _sp[k] + Anchor; float d2 = Vector3.DistanceSquared(Pos[i], w) / 8f;
        float c = d2 < 16 ? d2 : 8 * MathF.Sqrt(d2) - 16;
        if (Elem[i] != plan.Elem[k]) c += 60; if (Dom[i] != Perm[plan.Slot[k]]) c += 25;
        if (Home[i] == k) c -= C.Sticky;
        return c;
    }

    void AssignGreedy(Plan plan)
    {
        var alive = new List<int>(); for (int i = 0; i < Cap; i++) if (Alive[i]) alive.Add(i);
        int A = alive.Count, M = plan.N; var keys = new float[A * M]; var ids = new int[A * M];
        for (int a = 0; a < A; a++) for (int k = 0; k < M; k++) { keys[a * M + k] = Cost(alive[a], k, plan); ids[a * M + k] = a * M + k; }
        Array.Sort(keys, ids);
        var ru = new bool[A]; var cu = new bool[M]; var nh = new int[A]; Array.Fill(nh, -1); int got = 0, need = Math.Min(A, M);
        for (int q = 0; q < ids.Length && got < need; q++)
        {
            int a = ids[q] / M, k = ids[q] % M; if (ru[a] || cu[k]) continue;
            ru[a] = cu[k] = true; nh[a] = k; got++;
        }
        for (int a = 0; a < A; a++)
        {
            if (nh[a] < 0) { float b = float.MaxValue; for (int k = 0; k < M; k++) { float c = Cost(alive[a], k, plan); if (c < b) { b = c; nh[a] = k; } } }
            Home[alive[a]] = nh[a];
        }
    }

    void Lay(Plan plan)
    {
        int n = 0; for (int i = 0; i < Cap; i++) if (Alive[i]) n++;
        if (n >= plan.N) return;
        int k = Math.Min(C.LayMax, Math.Min(plan.N - n, Math.Max(1, (int)MathF.Ceiling(C.LayRate * n))));
        var want = new int[4, 3]; var have = new int[4, 3];
        for (int s = 0; s < plan.N; s++) want[plan.Elem[s], Perm[plan.Slot[s]]]++;
        for (int i = 0; i < Cap; i++) if (Alive[i]) have[Molt[i] > 0 ? MoltTo[i] : Elem[i], Dom[i]]++;
        var taken = new HashSet<int>(); for (int i = 0; i < Cap; i++) if (Alive[i] && Home[i] >= 0) taken.Add(Home[i]);
        for (int egg = 0; egg < k; egg++)
        {
            int be = -1, bd = -1, bdef = 0;
            for (int e = 0; e < 4; e++) for (int d = 0; d < 3; d++)
            {
                int def = want[e, d] - have[e, d]; if (def <= bdef) continue;
                bool par = false; for (int i = 0; i < Cap && !par; i++) par = Alive[i] && Elem[i] == e && Dom[i] == d && Molt[i] == 0;
                if (par) { be = e; bd = d; bdef = def; }
            }
            if (be < 0) return;
            int free = Array.IndexOf(Alive, false); if (free < 0) return;
            int bp = -1, bs = -1; float bdist = float.MaxValue;
            for (int s = 0; s < plan.N; s++)
            {
                if (plan.Elem[s] != be || Perm[plan.Slot[s]] != bd || taken.Contains(s)) continue;
                var w = _sp[s] + Anchor;
                for (int i = 0; i < Cap; i++) if (Alive[i] && Elem[i] == be && Dom[i] == bd && Molt[i] == 0)
                { float d2 = Vector3.DistanceSquared(Pos[i], w); if (d2 < bdist) { bdist = d2; bp = i; bs = s; } }
            }
            if (bp < 0) { for (int i = 0; i < Cap; i++) if (Alive[i] && Elem[i] == be && Dom[i] == bd) { bp = i; break; } }
            var dir = bs >= 0 ? _sp[bs] + Anchor - Pos[bp] : new Vector3((float)_rng.NextDouble() - .5f, (float)_rng.NextDouble() - .5f, (float)_rng.NextDouble() - .5f);
            dir /= MathF.Max(dir.Length(), 1e-6f);
            Alive[free] = true; Pos[free] = Pos[bp] + C.RBud * dir; Vel[free] = Vel[bp]; Elem[free] = be; Dom[free] = bd;
            Home[free] = bs; Molt[free] = 0; Startle[free] = 0;
            have[be, bd]++; if (bs >= 0) taken.Add(bs); LastAssign = -1000000;
        }
    }

    void MoltStep(Plan plan, int[] counts)
    {
        int n = counts.Sum(); var surplus = new float[4]; int tot = plan.Mix.Sum();
        for (int e = 0; e < 4; e++) surplus[e] = counts[e] - (float)plan.Mix[e] / tot * Math.Max(n, 1);
        int k = Math.Max(1, (int)MathF.Ceiling(C.MoltRate * n));
        for (int q = 0; q < k; q++)
        {
            int ef = 0, et = 0; for (int e = 1; e < 4; e++) { if (surplus[e] > surplus[ef]) ef = e; if (surplus[e] < surplus[et]) et = e; }
            if (surplus[ef] < 1 || surplus[et] > -1) return;
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Cap; i++)
            {
                if (!Alive[i] || Elem[i] != ef || Molt[i] > 0) continue;
                for (int s = 0; s < plan.N; s++) if (plan.Elem[s] == et)
                { float d2 = Vector3.DistanceSquared(Pos[i], _sp[s] + Anchor); if (d2 < bd) { bd = d2; best = i; } }
            }
            if (best < 0) return;
            Molt[best] = 1e-3f; MoltTo[best] = et; surplus[ef]--; surplus[et]++;
        }
    }

    // the yardstick's cull: swarm_nca.lose_majority(mode="excess", to=...)
    public void LoseMajority(int to)
    {
        var c = Counts(false); int maj = ArgMax(c), n = c.Sum();
        int extra = _rng.Next(0, Math.Max(1, c[to] / 3 + 1));
        int excess = c[maj] - c[to] + 1 + extra;
        if (n - excess < 6 || excess <= 0 || excess > c[maj]) return;
        var idx = Enumerable.Range(0, Cap).Where(i => Alive[i] && Elem[i] == maj).OrderBy(_ => _rng.Next()).Take(excess);
        foreach (var i in idx) { Alive[i] = false; Home[i] = -1; }
    }
}

public static class Program
{
    sealed class Dump { public float[][] pos = Array.Empty<float[]>(); public int[] elem = Array.Empty<int>(), dom = Array.Empty<int>(), home = Array.Empty<int>();
        public int plan; public float t; public string[] switches = Array.Empty<string>(); }

    static Dump Snap(FieldSwarm s)
    {
        var ix = Enumerable.Range(0, s.Cap).Where(i => s.Alive[i]).ToArray();
        return new Dump { pos = ix.Select(i => new[] { s.Pos[i].X, s.Pos[i].Y, s.Pos[i].Z }).ToArray(), elem = ix.Select(i => s.Elem[i]).ToArray(),
            dom = ix.Select(i => s.Dom[i]).ToArray(), home = ix.Select(i => s.Home[i]).ToArray(), plan = s.PlanIx, t = s.T, switches = s.Switches.ToArray() };
    }

    public static void Main(string[] args)
    {
        var doc = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
        var plans = doc.GetProperty("plans").EnumerateArray().Select(p => new Plan
        {
            Kind = p.GetProperty("kind").GetString()!, N = p.GetProperty("n").GetInt32(), NSlots = p.GetProperty("nslots").GetInt32(),
            Order = p.GetProperty("order").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
            Elem = p.GetProperty("elem").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
            Slot = p.GetProperty("slot").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
            Mix = p.GetProperty("mix").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
            SlotMix = p.GetProperty("slot_mix").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
            P = p.GetProperty("P").EnumerateArray().Select(fr => fr.EnumerateArray().Select(v => new Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle())).ToArray()).ToArray(),
        }).ToArray();
        int[] switchTo = { 2, 0, 3, 1 };
        var outp = new Dictionary<string, object>(); var cfg = new Cfg();
        foreach (var seed in doc.GetProperty("seeds").EnumerateArray())
        {
            int pk = seed.GetProperty("plan").GetInt32();
            var s = new FieldSwarm(plans, cfg, 280, 7 + pk);
            var sp = seed.GetProperty("pos").EnumerateArray().ToArray();
            var se = seed.GetProperty("elem").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var sd = seed.GetProperty("dom").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            for (int i = 0; i < se.Length; i++) { s.Alive[i] = true; s.Pos[i] = new Vector3(sp[i][0].GetSingle(), sp[i][1].GetSingle(), sp[i][2].GetSingle()); s.Elem[i] = se[i]; s.Dom[i] = sd[i]; }
            s.Begin();
            var none = Array.Empty<Predator>(); var sw = Stopwatch.StartNew();
            for (int t = 0; t < 240; t++) s.Step(none);
            double growMs = sw.Elapsed.TotalMilliseconds / 240;
            var pre = Snap(s);
            int nPre = pre.elem.Length;
            s.LoseMajority(switchTo[pk]);
            for (int t = 0; t < 240; t++) s.Step(none);
            var post = Snap(s);
            // steady-state cost, idle and with a vessel passing through
            sw.Restart(); for (int t = 0; t < 200; t++) s.Step(none); double idleMs = sw.Elapsed.TotalMilliseconds / 200;
            var c = Vector3.Zero; int nn = 0; for (int i = 0; i < s.Cap; i++) if (s.Alive[i]) { c += s.Pos[i]; nn++; } c /= Math.Max(nn, 1);
            var preds = new[] { new Predator { C = c - new Vector3(40, 0, 0), V = new Vector3(3, 0, 0), R = 9 } };
            sw.Restart(); for (int t = 0; t < 30; t++) { s.Step(preds); preds[0].C += preds[0].V; } double predMs = sw.Elapsed.TotalMilliseconds / 30;
            outp[plans[pk].Kind] = new { pre, post, grow_ms = growMs, idle_ms = idleMs, pred_ms = predMs, n_pre = nPre, n_post = post.elem.Length, n_idle = nn };
        }
        File.WriteAllText(args[1], JsonSerializer.Serialize(outp, new JsonSerializerOptions { IncludeFields = true }));
    }
}
