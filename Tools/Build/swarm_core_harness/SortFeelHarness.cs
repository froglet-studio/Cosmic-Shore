// Headless proof of SwarmSortCore's round-6 options - sortfeel's flat-bottomed wells + OU wander and the
// fractional (1-in-k) update (Docs/SWARM_FAUNA.md §12). Three jobs, all driven by score_sortfeel.py:
//   yardstick modes  (GridHarness.ExportYardstick dispatches any mode containing "Sort" here): the
//                    research's 16-transition yardstick on the sort core in any of its settings;
//   smoothsort       swarm_smooth.py's events (the 4 standard switches + a strike on each plan) recorded
//                    step by step, so the research's smoothness numbers can be computed on C# trajectories;
//   bench            ms per swarm-step and per tadpole-step for B = 1, 4, 16, 64 independent swarms.
// Mode names: research|game + Sort [+ Feel [+ D0]] [+ N0 (game: no velocity noise)] [+ F<k>] - e.g. researchSort (the published sort, as
// ported in round 3), researchSortFeel (sortfeel, frac 1), researchSortFeelF8 (the held lite config:
// well_dead 0.7 everywhere), researchSortFeelD0F8 (sortfeel v2's dragonfly override), gameSortFeelD0F8
// (what ships = SortHarness.Game), gameSortFeelD0N0F8 (the same with the velocity noise off), gameSort (round 5).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;

static class SortFeelHarness
{
    static readonly string[] Kinds = { "charge", "mass", "space", "time" };

    public static SwarmSortParams ParamsFor(SwarmPlanData[] plans, string mode)
    {
        // game modes start from the PRE-round-6 game settings so each round-6 piece is its own suffix;
        // the shipped config is gameSortFeelD0F8 (= SortHarness.Game)
        var p = mode.StartsWith("research") ? SortHarness.Override(SortHarness.Research(), "SWARM_SORT_RESEARCH") : SortHarness.Override(SortHarness.GameRound5(plans));
        if (mode.Contains("N0")) p.Noise = 0f;
        if (mode.Contains("Dom3")) p.RolesFromAnyDomain = true;   // round 11d: a region may go to any of the three domains
        if (mode.Contains("Fn")) p.FateNear = true;               // round 11d: a fate is the nearest short well
        if (mode.Contains("Wnd")) p.BudAtWound = true;
        var lrm = Regex.Match(mode, @"Lr(\d+)"); if (lrm.Success) p.LayRamp = int.Parse(lrm.Groups[1].Value);   // round 11d: lay ramp
        if (mode.Contains("Feel"))
        {
            var m = Regex.Match(mode, @"F(\d+)$");
            p.WithSortFeel(m.Success ? int.Parse(m.Groups[1].Value) : 1, mode.Contains("D0") ? 0f : -1f);
        }
        else
        {
            var m = Regex.Match(mode, @"F(\d+)$");
            if (m.Success) p.Frac = int.Parse(m.Groups[1].Value);   // the schedule without the feel
        }
        return p;
    }

    public static SwarmSortCore Make(SwarmPlanData[] plans, string mode, int k, int seed)
    {
        var p = ParamsFor(plans, mode);
        if (mode.StartsWith("research"))
        {
            var c = new SwarmSortCore(plans, p, seed);
            var plan = plans[k];
            c.SeedWith(SortHarness.LR(plan.Mix, 16), SortHarness.LR(plan.SlotMix, 16), Vector3.Zero, 2f);
            return c;
        }
        return SortHarness.GameSwarm(plans, k, seed, p: p);
    }

    public static SwarmSortCore Grow(SwarmPlanData[] plans, string mode, int k, int seed, int steps = 240)
    {
        var c = Make(plans, mode, k, seed);
        SortHarness.Run(c, steps);
        return c;
    }

    static int[] Mix(SwarmSortCore c) { var m = new int[4]; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) m[c.Elem[i]]++; return m; }

    /// <summary>swarm_eval.cull_to (GridHarness.CullTo's rule) on the sort core.</summary>
    public static bool CullTo(SwarmSortCore c, int e, Random rng)
    {
        var cnt = Mix(c); int ce = cnt[e];
        if (ce < 2) return false;
        int lead = Math.Max(1, (int)Math.Round(0.1 * ce)), cap = Math.Max(0, ce - lead);
        var kills = new int[4]; for (int f = 0; f < 4; f++) kills[f] = f == e ? 0 : Math.Max(0, cnt[f] - cap);
        if (cnt.Sum() - kills.Sum() < 6) return false;
        for (int f = 0; f < 4; f++)
        {
            var idx = Enumerable.Range(0, c.Cap).Where(i => c.Active[i] && c.Hatched[i] && c.Elem[i] == f).ToList();
            for (int q = idx.Count - 1; q > 0; q--) { int j = rng.Next(q + 1); (idx[q], idx[j]) = (idx[j], idx[q]); }
            for (int q = 0; q < kills[f]; q++) c.Kill(idx[q]);
        }
        return true;
    }

    /// <summary>swarm_probe.strike: remove every live member inside a sphere of the body's RMS radius,
    /// centred one RMS radius off the centroid along a seeded random direction.</summary>
    public static int Strike(SwarmSortCore c, Random rng)
    {
        var live = Enumerable.Range(0, c.Cap).Where(i => c.Active[i] && c.Hatched[i]).ToList();
        if (live.Count < 4) return 0;
        var cen = Vector3.Zero; foreach (int i in live) cen += c.Pos[i]; cen /= live.Count;
        float rms = MathF.Sqrt(live.Average(i => (c.Pos[i] - cen).LengthSquared()));
        var d = new Vector3(G(rng), G(rng), G(rng)); d /= MathF.Max(d.Length(), 1e-6f);
        var hc = cen + rms * d; int n = 0;
        foreach (int i in live) if ((c.Pos[i] - hc).LengthSquared() < rms * rms) { c.Kill(i); n++; }
        return n;
    }

    static float G(Random r) { double u = 1 - r.NextDouble(), v = r.NextDouble(); return (float)(Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v)); }

    // ─────────────────────────────────────────────────────────────── yardstick (one mode, one seed)

    public static void Yardstick(StringBuilder sb, ref bool first, SwarmPlanData[] plans, string mode, int seed, int samples, ref int steps)
    {
        for (int k = 0; k < 4; k++)
            for (int r = 0; r < samples; r++)
            {
                int gseed = seed + 101 * r;
                var c = Grow(plans, mode, k, gseed); steps += 240;
                Append(sb, ref first, c, mode, "own", k, k, seed, r, true);
                for (int e = 0; e < 4; e++)
                {
                    if (e == k) continue;
                    var c2 = Grow(plans, mode, k, gseed);
                    if (!CullTo(c2, e, new Random(gseed * 31 + e)))
                    {
                        if (!first) sb.Append(','); first = false;
                        sb.Append($"{{\"mode\":\"{mode}\",\"tag\":\"switch\",\"kind\":\"{Kinds[k]}\",\"want\":\"{Kinds[e]}\",\"seed\":{seed},\"sample\":{r},\"na\":true}}");
                        continue;
                    }
                    int sw = int.TryParse(Environment.GetEnvironmentVariable("SWARM_SWITCH_STEPS"), out int ss) ? ss : 240;   // diagnosis only
                    SortHarness.Run(c2, sw); steps += sw;
                    Append(sb, ref first, c2, mode, "switch", k, e, seed, r, false);
                }
            }
    }

    static void Append(StringBuilder sb, ref bool first, SwarmSortCore c, string mode, string tag, int k, int e, int seed, int r, bool window)
    {
        if (!first) sb.Append(','); first = false;
        sb.Append($"{{\"mode\":\"{mode}\",\"tag\":\"{tag}\",\"kind\":\"{Kinds[k]}\",\"want\":\"{Kinds[e]}\",\"seed\":{seed},\"sample\":{r},\"molts\":0,\"stray\":0,\"units\":[");
        Units(sb, c);
        sb.Append(']');
        if (window)
        {
            var alive = Enumerable.Range(0, c.Cap).Where(i => c.Active[i] && c.Hatched[i]).ToList();
            var el = alive.ToDictionary(i => i, i => c.Elem[i]);
            var order = alive.ToList(); var frames = new List<Vector3[]>();
            for (int t = 0; t <= 64; t++)
            {
                frames.Add(order.Select(i => c.Pos[i]).ToArray());
                if (t == 64) break;
                SortHarness.Step(c); c.Events.Clear();
                alive.RemoveAll(i => !(c.Active[i] && c.Hatched[i] && c.Elem[i] == el[i] && c.Molt[i] <= 0f));
            }
            var keep = new HashSet<int>(alive);
            sb.Append(",\"win\":[");
            for (int t = 0; t < frames.Count; t++)
            {
                if (t > 0) sb.Append(',');
                sb.Append('['); bool f1 = true;
                for (int q = 0; q < order.Count; q++)
                {
                    if (!keep.Contains(order[q])) continue;
                    var x = frames[t][q];
                    if (!f1) sb.Append(','); f1 = false;
                    sb.Append($"[{F(x.X)},{F(x.Y)},{F(x.Z)}]");
                }
                sb.Append(']');
            }
            sb.Append("],\"winElem\":[").Append(string.Join(",", order.Where(keep.Contains).Select(i => el[i]))).Append(']');
        }
        sb.Append('}');
    }

    /// <summary>Every member's state in score_grid.build_swarm's row format (SortHarness.ExportRun's).</summary>
    static readonly float[] _look = new float[11];
    static void Units(StringBuilder sb, SwarmSortCore c)
    {
        var centre = Vector3.Zero; int nl = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { centre += c.Pos[i]; nl++; }
        centre /= Math.Max(1, nl);
        bool u1 = true;
        for (int i = 0; i < c.Cap; i++)
        {
            if (!c.Active[i]) continue;
            var p = c.InvRotate(c.Pos[i] - centre);
            c.LookState(i, _look);
            if (!u1) sb.Append(','); u1 = false;
            sb.Append('[').Append(F(p.X)).Append(',').Append(F(p.Y)).Append(',').Append(F(p.Z)).Append(',')
              .Append(c.EffectiveElement(i)).Append(',').Append(c.Dom[i]).Append(',').Append(c.Hatched[i] ? 1 : 0).Append(',')
              .Append(c.Hatched[i] ? "1" : "0.2").Append(",-12");
            for (int q = 0; q < 11; q++) sb.Append(',').Append(F(_look[q]));
            sb.Append(']');
        }
    }

    // ─────────────────────────────────────────────────────────────── smoothness events

    /// <summary>smoothsort &lt;plans&gt; &lt;out.json&gt; &lt;seed&gt; &lt;modes&gt; &lt;pairs k:e,...&gt; - swarm_smooth's events
    /// on the C# core. Per event: the grown body's 16-step steady-state trace (for the reference speed),
    /// then every step's slot positions / alive / element / hatched over the 240-step window, a full unit
    /// snapshot every 16 steps (the loss series) and 8 more snapshots past the window (the settled wiggle).</summary>
    public static int Smooth(string[] args, Func<string, SwarmPlanData[]> load)
    {
        var plans = load(args[1]); string outPath = args[2]; int seed = int.Parse(args[3]);
        var modes = args[4].Split(',');
        var pairs = args[5].Split(',').Select(s => s.Split(':').Select(int.Parse).ToArray()).ToArray();
        var sb = new StringBuilder(); sb.Append("{\"events\":["); bool first = true;
        foreach (string mode in modes)
        {
            var sw = Stopwatch.StartNew();
            foreach (var pr in pairs) Event(sb, ref first, plans, mode, seed, pr[0], pr[1]);
            for (int k = 0; k < 4; k++) Event(sb, ref first, plans, mode, seed + 1, k, -1);
            Console.WriteLine($"  smooth {mode}: {sw.Elapsed.TotalSeconds:F0}s, self-deaths so far {SortHarness._selfDeaths}");
        }
        sb.Append($"],\"selfDeaths\":{SortHarness._selfDeaths}}}");
        File.WriteAllText(outPath, sb.ToString());
        return 0;
    }

    static void Event(StringBuilder sb, ref bool first, SwarmPlanData[] plans, string mode, int seed, int k, int e)
    {
        var c = Grow(plans, mode, k, seed);
        if (!first) sb.Append(','); first = false;
        string name = e < 0 ? $"heal {Kinds[k]}" : $"switch {Kinds[k]}->{Kinds[e]}";
        sb.Append($"{{\"mode\":\"{mode}\",\"event\":\"{name}\",\"kind\":\"{Kinds[k]}\",\"goal\":\"{(e < 0 ? Kinds[k] : Kinds[e])}\",\"cap\":{c.Cap},");
        // reference: the grown body's p95 step over 16 steps (swarm_smooth._grown)
        sb.Append("\"ref\":["); Trace(sb, c, 16); sb.Append("],");
        bool ok = e < 0 ? Strike(c, new Random(seed * 977 + k)) > 0 : CullTo(c, e, new Random(seed * 31 + e));
        if (!ok) { sb.Append("\"na\":true}"); return; }
        int d0 = SortHarness._selfDeaths;
        sb.Append("\"snap\":[");
        sb.Append('['); Units(sb, c); sb.Append(']');
        sb.Append("],\"trace\":[");
        // 240 steps, a snapshot every 16, recorded per step
        var snaps = new StringBuilder();
        TraceWithSnaps(sb, snaps, c, 240, 16);
        sb.Append("],\"snaps\":[").Append(snaps).Append("],\"tail\":[");
        for (int t = 0; t < 8; t++)
        {
            SortHarness.Run(c, 16);
            if (t > 0) sb.Append(',');
            sb.Append('['); Units(sb, c); sb.Append(']');
        }
        sb.Append($"],\"deaths\":{SortHarness._selfDeaths - d0}}}");
    }

    static void Frame(StringBuilder sb, SwarmSortCore c)
    {
        sb.Append("{\"p\":[");
        for (int i = 0; i < c.Cap; i++) { if (i > 0) sb.Append(','); var x = c.Pos[i]; sb.Append(F4(x.X)).Append(',').Append(F4(x.Y)).Append(',').Append(F4(x.Z)); }
        sb.Append("],\"a\":\"");
        for (int i = 0; i < c.Cap; i++) sb.Append(c.Active[i] && c.Hatched[i] ? '1' : '0');
        sb.Append("\",\"h\":\"");
        for (int i = 0; i < c.Cap; i++) sb.Append(c.Hatched[i] ? '1' : '0');
        sb.Append("\",\"e\":\"");
        for (int i = 0; i < c.Cap; i++) sb.Append((char)('0' + c.EffectiveElement(i)));
        sb.Append("\"}");
    }

    static void Trace(StringBuilder sb, SwarmSortCore c, int steps)
    {
        Frame(sb, c);
        for (int t = 0; t < steps; t++) { SortHarness.Step(c); c.Events.Clear(); sb.Append(','); Frame(sb, c); }
    }

    static void TraceWithSnaps(StringBuilder sb, StringBuilder snaps, SwarmSortCore c, int steps, int every)
    {
        Frame(sb, c);
        for (int t = 0; t < steps; t++)
        {
            SortHarness.Step(c); c.Events.Clear(); sb.Append(','); Frame(sb, c);
            if ((t + 1) % every == 0) { if (snaps.Length > 0) snaps.Append(','); snaps.Append('['); Units(snaps, c); snaps.Append(']'); }
        }
    }

    // ─────────────────────────────────────────────────────────────── the jolt (round 11d)

    /// <summary>swarm_smooth._track's LURCH on one event, computed here so it can gate the harness without torch:
    /// per step the p95 of every member's step length (members alive at both ends, at least 4), and
    /// lurch = the worst step's p95 over the event's own median p95 (torch.quantile's linear interpolation,
    /// torch.median's lower middle). Also returns the step index of the worst step and the same lurch on
    /// motion RELATIVE to the moving centroid (what is left once the whole body's glide is removed).</summary>
    public static (float lurch, int at, float lurchRel, float med) Lurch(List<Vector3[]> P, List<bool[]> A)
    {
        int T = P.Count - 1; var p95 = new float[T]; var p95r = new float[T];
        var buf = new List<float>(); var bufr = new List<float>();
        for (int t = 0; t < T; t++)
        {
            buf.Clear(); bufr.Clear();
            Vector3 c0 = Vector3.Zero, c1 = Vector3.Zero; int nb = 0;
            for (int i = 0; i < P[t].Length; i++) if (A[t][i] && A[t + 1][i]) { c0 += P[t][i]; c1 += P[t + 1][i]; nb++; }
            if (nb < 4) continue;
            c0 /= nb; c1 /= nb;
            for (int i = 0; i < P[t].Length; i++)
                if (A[t][i] && A[t + 1][i]) { buf.Add((P[t + 1][i] - P[t][i]).Length()); bufr.Add((P[t + 1][i] - c1 - (P[t][i] - c0)).Length()); }
            p95[t] = Q95(buf); p95r[t] = Q95(bufr);
        }
        static float Q95(List<float> v)
        {
            v.Sort(); float pos = 0.95f * (v.Count - 1); int lo = (int)pos; int hi = Math.Min(lo + 1, v.Count - 1);
            return v[lo] + (pos - lo) * (v[hi] - v[lo]);
        }
        static float Med(float[] a)
        {
            var v = a.Where(x => x > 0).OrderBy(x => x).ToList();
            return v.Count == 0 ? 1e-4f : v[(v.Count - 1) / 2];
        }
        float med = Med(p95), medr = Med(p95r);
        int at = 0; for (int t = 1; t < T; t++) if (p95[t] > p95[at]) at = t;
        return (p95.Max() / MathF.Max(med, 1e-4f), at, p95r.Max() / MathF.Max(medr, 1e-4f), med);
    }

    /// <summary>One smoothsort event (switch k->e, or e &lt; 0: a strike on plan k) on a grown body, its lurch over
    /// the <paramref name="steps"/> steps after the cull.</summary>
    public static (float lurch, int at, float lurchRel, float med) JoltEvent(SwarmPlanData[] plans, string mode, int seed, int k, int e, int steps = 240)
    {
        var c = Grow(plans, mode, k, seed);
        bool ok = e < 0 ? Strike(c, new Random(seed * 977 + k)) > 0 : CullTo(c, e, new Random(seed * 31 + e));
        if (!ok) return (float.NaN, -1, float.NaN, 0f);
        var P = new List<Vector3[]>(); var A = new List<bool[]>();
        void Rec() { P.Add((Vector3[])c.Pos.Clone()); var a = new bool[c.Cap]; for (int i = 0; i < c.Cap; i++) a[i] = c.Active[i] && c.Hatched[i]; A.Add(a); }
        Rec();
        var hatchM = new List<float>(); var wasLive = new bool[c.Cap]; for (int i = 0; i < c.Cap; i++) wasLive[i] = c.Active[i] && c.Hatched[i];
        for (int t = 0; t < steps; t++)
        {
            SortHarness.Step(c); c.Events.Clear(); Rec();
            for (int i = 0; i < c.Cap; i++) { bool l = c.Active[i] && c.Hatched[i]; if (l && !wasLive[i] && t < 60) hatchM.Add(MathF.Sqrt(2 * c.Energy[i])); wasLive[i] = l; }
        }
        if (Environment.GetEnvironmentVariable("JOLT_HATCH") == "1" && hatchM.Count > 0) { hatchM.Sort(); Console.WriteLine($"   newborns {hatchM.Count}: well distance (sigma) median {hatchM[hatchM.Count / 2]:F2} p90 {hatchM[(int)(0.9 * (hatchM.Count - 1))]:F2}"); }
        if (Environment.GetEnvironmentVariable("JOLT_SURVIVORS") == "1")   // diagnosis: only the members alive at the cull
            for (int t = 1; t < A.Count; t++) for (int i = 0; i < c.Cap; i++) A[t][i] &= A[0][i];
        if (Environment.GetEnvironmentVariable("JOLT_NEWBORN") == "1")   // diagnosis: only the members born after it
        {
            var a0 = (bool[])A[0].Clone();
            for (int t = 0; t < A.Count; t++) for (int i = 0; i < c.Cap; i++) A[t][i] &= !a0[i];
        }
        return Lurch(P, A);
    }

    /// <summary>jolt &lt;plans&gt; &lt;seeds&gt; &lt;modes&gt; - the post-cull jolt per event (swarm_smooth's 4 standard switches and a
    /// strike on every plan), lurch and where it peaks, for each mode; SWARM_SORT_GAME overrides apply.</summary>
    public static int Jolt(string[] args, Func<string, SwarmPlanData[]> load)
    {
        var plans = load(args[1]);
        var seeds = args[2].Split(',').Select(int.Parse).ToArray();
        var modes = args[3].Split(',');
        var pairs = new[] { (1, 2), (2, 0), (0, 3), (3, 1) };
        foreach (string mode in modes)
        {
            var all = new List<float>(); var allR = new List<float>();
            foreach (int seed in seeds)
            {
                var sb = new StringBuilder($"  {mode,-24} seed {seed,4}:");
                foreach (var (k, e) in pairs)
                {
                    var r = JoltEvent(plans, mode, seed, k, e);
                    sb.Append($" {Kinds[k][0]}>{Kinds[e][0]} {r.lurch:F2}@{r.at}");
                    if (!float.IsNaN(r.lurch)) { all.Add(r.lurch); allR.Add(r.lurchRel); }
                }
                for (int k = 0; k < 4; k++)
                {
                    var r = JoltEvent(plans, mode, seed + 1, k, -1);
                    sb.Append($" heal {Kinds[k][0]} {r.lurch:F2}@{r.at}");
                    if (!float.IsNaN(r.lurch)) { all.Add(r.lurch); allR.Add(r.lurchRel); }
                }
                Console.WriteLine(sb.ToString());
            }
            all.Sort();
            Console.WriteLine($"JOLT mode={mode} events={all.Count} lurch_mean={all.Average():F3} lurch_p90={all[(int)(0.9 * (all.Count - 1))]:F3} lurch_worst={all.Max():F3} rel_mean={allR.Average():F3} rel_worst={allR.Max():F3} over_2.5={all.Count(x => x > 2.5f)}");
        }
        return 0;
    }

    /// <summary>switchdiag &lt;plans&gt; &lt;mode&gt; &lt;k&gt; &lt;e&gt; &lt;seeds&gt; - the body a k->e switch grows: headcount, (element x domain) and
    /// (domain -> region) after the cull and after 240 steps; the share of the body in a domain the plan has no region for.</summary>
    public static int SwitchDiag(string[] args, Func<string, SwarmPlanData[]> load)
    {
        var plans = load(args[1]); string mode = args[2]; int k = int.Parse(args[3]), e = int.Parse(args[4]);
        foreach (int seed in args[5].Split(',').Select(int.Parse))
            for (int r = 0; r < 3; r++)
            {
                int gseed = seed + 101 * r;
                var c = Grow(plans, mode, k, gseed);
                if (!CullTo(c, e, new Random(gseed * 31 + e))) { Console.WriteLine($"seed {seed} s{r}: na"); continue; }
                string Dump()
                {
                    var m = new int[4, 3]; int orphan = 0, n = 0;
                    for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { m[c.EffectiveElement(i), Math.Clamp(c.Dom[i], 0, 2)]++; n++; if (c.RoleOfDom[Math.Clamp(c.Dom[i], 0, 2)] < 0) orphan++; }
                    var sb = new StringBuilder($"n={n} plan={c.PlanIx} roleOfDom=[{c.RoleOfDom[0]},{c.RoleOfDom[1]},{c.RoleOfDom[2]}] orphanDomShare={(float)orphan / Math.Max(1, n):F2} elem x dom:");
                    for (int q = 0; q < 4; q++) sb.Append($" {Kinds[q][0]}[{m[q, 0]},{m[q, 1]},{m[q, 2]}]");
                    return sb.ToString();
                }
                Console.WriteLine($"seed {seed} s{r} after cull: {Dump()}");
                string last = "";
                for (int t = 0; t < 240; t++)
                {
                    SortHarness.Step(c); c.Events.Clear();
                    string now = $"{c.PlanIx}:{c.RoleOfDom[0]},{c.RoleOfDom[1]},{c.RoleOfDom[2]}";
                    if (now != last && Environment.GetEnvironmentVariable("DIAG_TRACE") == "1") Console.WriteLine($"   t={t} {Dump()}");
                    last = now;
                }
                Console.WriteLine($"seed {seed} s{r} +240     : {Dump()}");
            }
        return 0;
    }

    // ─────────────────────────────────────────────────────────────── bench

    /// <summary>bench &lt;plans&gt; &lt;modes&gt; &lt;Bs&gt; - B independent grown swarms (a mix of the four plans), stepped
    /// round-robin for 200 steps after a 240-step grow: ms per swarm-step and us per tadpole-step.</summary>
    public static int Bench(string[] args, Func<string, SwarmPlanData[]> load)
    {
        var plans = load(args[1]);
        var modes = args[2].Split(',');
        var Bs = args[3].Split(',').Select(int.Parse).ToArray();
        int steps = args.Length > 4 ? int.Parse(args[4]) : 200;
        foreach (string mode in modes)
            foreach (int B in Bs)
            {
                var sw = new SwarmSortCore[B];
                for (int b = 0; b < B; b++) sw[b] = Grow(plans, mode, b % 4, 1000 + b);
                for (int b = 0; b < B; b++) SortHarness.Run(sw[b], 20);   // warm
                long tad = 0; for (int b = 0; b < B; b++) for (int i = 0; i < sw[b].Cap; i++) if (sw[b].Active[i] && sw[b].Hatched[i]) tad++;
                var preds = Array.Empty<SwarmPredator>();
                var t0 = Stopwatch.StartNew();
                for (int t = 0; t < steps; t++)
                    for (int b = 0; b < B; b++) { sw[b].Step(preds); sw[b].Events.Clear(); }
                double ms = t0.Elapsed.TotalMilliseconds;
                double perSwarm = ms / (steps * B), perTad = ms * 1000.0 / (steps * (double)tad);
                Console.WriteLine($"BENCH mode={mode} B={B} tadpoles={tad} ms_per_swarm_step={perSwarm:F4} us_per_tadpole_step={perTad:F3} ms_per_frame_all={ms / steps:F3}");
            }
        return 0;
    }

    static string F(float x) => x.ToString("G6", CultureInfo.InvariantCulture);
    static string F4(float x) => x.ToString("G5", CultureInfo.InvariantCulture);
}
