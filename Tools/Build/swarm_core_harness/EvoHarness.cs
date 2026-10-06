// Headless proof of the SHIPPED evofate core (Assets/.../Swarm/SwarmEvoFateCore.cs - the research's
// `evofate`: the trained G2 network given a fate, Tools/NCA/evofate_model.py). Three jobs:
//   EvoHarness.Fixture(...)  - EXACTNESS: load the states evofate_fixture.py wrote, recompute every member's
//                              232 perception features and the network's 35 outputs, compare to Python;
//   EvoHarness.Run(plans)    - asserted game-side tests (growth, funded laying, morph on selective kills,
//                              ZERO self-inflicted deaths, eggs kept, swimming, vessel reaction, cost);
//   EvoHarness.Yardstick*    - the 16-transition yardstick records for score_combo.py (modes researchEvo /
//                              gameEvo, the same record format as the grid core's).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using CosmicShore.Gameplay;

static class EvoHarness
{
    static int _fail;
    static readonly string[] Kinds = { "charge", "mass", "space", "time" };
    public static int SelfDeaths;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    public static string RulePath(string plansDir) => Path.Combine(plansDir, "..", "SwarmEvoFateRule.json");

    static SwarmEvoRule _rule;
    public static SwarmEvoRule Rule(string plansDir) => _rule ??= SwarmEvoRule.Parse(File.ReadAllText(RulePath(plansDir)));

    /// <summary>evofate_model.EvoFate exactly as the research runs it (results/evofate/params.json): three
    /// domain regions, free laying, instant molts, frame 0, fixed frame, membrane 80, capacity 280, eggs lost.</summary>
    public static SwarmEvoFateParams Research() => Override(new SwarmEvoFateParams
    {
        DomainSlots = true, Funded = false, MoltSteps = 0, KeepEggs = false, Animate = false, Oriented = false,
        Cruise = 0f, Membrane = 80f, Cap = 280,
    }, "SWARM_EVO_RESEARCH");

    /// <summary>The game's settings (SwarmFauna.BuildEvoFateCore builds the same from SwarmFaunaConfigSO).</summary>
    public static SwarmEvoFateParams Game(SwarmPlanData[] plans) => Override(new SwarmEvoFateParams
    {
        DomainSlots = false, Funded = true, MoltSteps = 10, KeepEggs = true, Animate = true, Oriented = true,
        Cruise = 0.35f, Membrane = 600f, Cap = plans.Max(p => p.N), KillLayHoldSteps = 20,
    });

    /// <summary>Experiments: SWARM_EVO_GAME="Field=value,..." overrides game-mode params by name.</summary>
    static SwarmEvoFateParams Override(SwarmEvoFateParams p, string var = "SWARM_EVO_GAME")
    {
        var env = Environment.GetEnvironmentVariable(var);
        if (string.IsNullOrEmpty(env)) return p;
        foreach (var kv in env.Split(','))
        {
            var a = kv.Split('='); var f = typeof(SwarmEvoFateParams).GetField(a[0]);
            f.SetValue(p, f.FieldType == typeof(bool) ? a[1] == "1" || a[1] == "true"
                : f.FieldType == typeof(int) ? int.Parse(a[1]) : (object)float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture));
        }
        return p;
    }

    /// <summary>swarm_feel's `osc`: the share of (member, step) pairs whose displacement reverses
    /// (cos &lt; -0.5) over a window, members alive throughout (the window follows the members alive at its start).</summary>
    public static float Osc(SwarmEvoFateCore c, int win = 64) => Osc(c, out _, out _, win);

    /// <summary>... and swarm_feel's `stuck` (members whose mean speed is under 0.02 while the body's is over
    /// 0.05) and the body's mean member speed.</summary>
    public static float Osc(SwarmEvoFateCore c, out float stuck, out float speed, int win = 64)
    {
        var ids = Enumerable.Range(0, c.Cap).Where(i => c.Active[i] && c.Hatched[i]).ToList();
        var frames = new List<Vector3[]>();
        for (int t = 0; t <= win; t++) { frames.Add(ids.Select(i => c.Pos[i]).ToArray()); if (t < win) { Step(c); c.Events.Clear(); } }
        int rev = 0, tot = 0;
        for (int t = 2; t <= win; t++)
            for (int q = 0; q < ids.Count; q++)
            {
                var a = frames[t - 1][q] - frames[t - 2][q]; var b = frames[t][q] - frames[t - 1][q];
                // swarm_feel.metrics exactly: every pair counts, a still step reads as cos 0 (never a reversal)
                float la = a.Length(), lb = b.Length();
                tot++; if (Vector3.Dot(a, b) / MathF.Max(la * lb, 1e-6f) < -0.5f) rev++;
            }
        var ms = new float[ids.Count]; float body = 0;
        for (int q = 0; q < ids.Count; q++)
        {
            for (int t = 1; t <= win; t++) ms[q] += Vector3.Distance(frames[t][q], frames[t - 1][q]);
            ms[q] /= win; body += ms[q];
        }
        body /= Math.Max(1, ids.Count); speed = body;
        stuck = ids.Count == 0 || body <= 0.05f ? 0f : ms.Count(x => x < 0.02f) / (float)ids.Count;
        return tot > 0 ? rev / (float)tot : 0f;
    }

    public const int GameSeed = GridHarness.GameSeed;

    public static SwarmEvoFateCore ResearchSwarm(SwarmPlanData[] plans, SwarmEvoRule rule, int k, int seed, int count = 16)
    {
        var c = new SwarmEvoFateCore(plans, Research(), rule, seed);
        var plan = plans[k];
        c.SeedWith(LR(plan.Mix, count), LR(plan.SlotMix, count), Vector3.Zero, 2f);
        return c;
    }

    public static SwarmEvoFateCore GameSwarm(SwarmPlanData[] plans, SwarmEvoRule rule, int k, int seed, bool fed = true)
    {
        var c = new SwarmEvoFateCore(plans, Game(plans), rule, seed);
        c.Seed(k, GameSeed, new Vector3(200, 0, 0), Vector3.UnitX);
        c.SwimTarget = c.Anchor;
        if (fed) for (int e = 0; e < 4; e++) c.Stomach[e] = 1e6f;
        return c;
    }

    static int[] LR(int[] w, int n)
    {
        int K = w.Length; double sum = w.Sum(); var b = new int[K]; var rem = new double[K];
        for (int k = 0; k < K; k++) { double x = w[k] / sum * n; b[k] = (int)Math.Floor(x); rem[k] = x - b[k]; }
        for (int k = 0; k < K; k++) if (w[k] > 0 && b[k] == 0) b[k] = 1;
        while (b.Sum() < n) { int k = Array.IndexOf(rem, rem.Max()); b[k]++; rem[k] = -1; }
        while (b.Sum() > n) { int k = Array.IndexOf(b, b.Max()); b[k]--; }
        return b;
    }

    /// <summary>A HATCHED member alive before a step and gone after it, with nobody calling Kill (scorecard.py's
    /// count; an egg the research loses is the research's own behaviour and is counted separately).</summary>
    static void Step(SwarmEvoFateCore c, SwarmPredator[] preds = null)
    {
        var before = new bool[c.Cap];
        for (int i = 0; i < c.Cap; i++) before[i] = c.Active[i] && c.Hatched[i];
        c.Step(preds ?? Array.Empty<SwarmPredator>());
        for (int i = 0; i < c.Cap; i++) if (before[i] && !c.Active[i]) SelfDeaths++;
    }

    static void Run(SwarmEvoFateCore c, int steps, SwarmPredator[] preds = null)
    {
        for (int t = 0; t < steps; t++) { Step(c, preds); c.Events.Clear(); }
    }

    static int Live(SwarmEvoFateCore c) { int n = 0; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) n++; return n; }
    static int[] Mix(SwarmEvoFateCore c) { var m = new int[4]; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) m[c.EffectiveElement(i)]++; return m; }
    static int Major(int[] m) { int b = 0; for (int e = 1; e < 4; e++) if (m[e] > m[b]) b = e; return b; }

    static float CompositionError(SwarmEvoFateCore c)
    {
        var m = Mix(c); var p = c.Plan.Mix; float n = Math.Max(1, m.Sum()), pn = Math.Max(1, p.Sum()), s = 0;
        for (int e = 0; e < 4; e++) s += Math.Abs(m[e] / n - p[e] / pn);
        return s;
    }

    // ─────────────────────────────────────────────────────────────── exactness

    public static int Fixture(string plansDir, string fixturePath)
    {
        var plans = Program.LoadPlansPublic(plansDir);
        var rule = Rule(plansDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var root = doc.RootElement;
        Console.WriteLine("E1. exactness: perception + network vs evofate_model.EvoFate (evofate_fixture.py)");
        double worstF = 0, worstO = 0, worstOR = 0; int members = 0;
        var feat = new float[SwarmEvoRule.F];
        var h1 = new float[rule.H]; var h2 = new float[rule.H]; var o = new float[SwarmEvoRule.OUT];
        foreach (var st in root.GetProperty("states").EnumerateArray())
        {
            int n = st.GetProperty("n").GetInt32();
            var p = Research(); p.Cap = 280;
            var c = new SwarmEvoFateCore(plans, p, rule, 1);
            var pos = st.GetProperty("pos").EnumerateArray().ToArray();
            var s = st.GetProperty("s").EnumerateArray().ToArray();
            var el = st.GetProperty("elem").EnumerateArray().ToArray();
            var dm = st.GetProperty("dom").EnumerateArray().ToArray();
            var ht = st.GetProperty("hatched").EnumerateArray().ToArray();
            for (int i = 0; i < n; i++)
            {
                var q = pos[i].EnumerateArray().Select(x => x.GetSingle()).ToArray();
                c.Pos[i] = new Vector3(q[0], q[1], q[2]);
                var sv = s[i].EnumerateArray().Select(x => x.GetSingle()).ToArray();
                Array.Copy(sv, 0, c.S, i * SwarmEvoRule.C, SwarmEvoRule.C);
                c.Elem[i] = el[i].GetInt32(); c.Dom[i] = dm[i].GetInt32();
                c.Active[i] = true; c.Hatched[i] = ht[i].GetInt32() == 1;
            }
            var feats = st.GetProperty("feats").EnumerateArray().ToArray();
            var outs = st.GetProperty("out").EnumerateArray().ToArray();
            double sf = 0, so = 0, sor = 0;
            for (int i = 0; i < n; i++)
            {
                c.FeaturesOf(i, feat);
                int k = 0;
                foreach (var x in feats[i].EnumerateArray()) { sf = Math.Max(sf, Math.Abs(x.GetSingle() - feat[k])); k++; }
                rule.Forward(feat, h1, h2, o);
                k = 0;
                foreach (var x in outs[i].EnumerateArray())
                {
                    float py = x.GetSingle(); double d = Math.Abs(py - o[k]);
                    so = Math.Max(so, d); sor = Math.Max(sor, d / Math.Max(1.0, Math.Abs(py))); k++;
                }
            }
            members += n; worstF = Math.Max(worstF, sf); worstO = Math.Max(worstO, so); worstOR = Math.Max(worstOR, sor);
            Console.WriteLine($"  state n={n,3}: worst feature error {sf:E2}, worst output error {so:E2} (relative {sor:E2})");
        }
        Console.WriteLine($"  {members} members: features within {worstF:E2}, outputs within {worstO:E2} (relative {worstOR:E2})");
        Check(worstF < 1e-4, "perception equals swarm_nca.SwarmRule.perceive + EvoFate's glob row (float32 rounding)");
        Check(worstOR < 1e-4, "the network equals EvoRule.mlp on the shipped weights (float32 rounding)");
        return _fail;
    }

    // ─────────────────────────────────────────────────────────────── game tests

    public static int Run(SwarmPlanData[] plans, string plansDir)
    {
        var rule = Rule(plansDir);
        Console.WriteLine($"\nevofate core: G2 {SwarmEvoRule.F}->{rule.H}->{rule.H}->{SwarmEvoRule.OUT}, fire rate {rule.FireRate}, " +
                          $"SIMD {(Vector.IsHardwareAccelerated ? $"on ({Vector<float>.Count} lanes)" : "off (scalar path - Unity Mono's)")}");

        Console.WriteLine("\nE2. growth: research (16 seed, free laying) and game (fed) reach the plan");
        var grown = new SwarmEvoFateCore[4];
        for (int k = 0; k < 4; k++)
        {
            var r = ResearchSwarm(plans, rule, k, 7); Run(r, 600);
            var g = GameSwarm(plans, rule, k, 7); var sw = Stopwatch.StartNew(); Run(g, 600); double ms = sw.Elapsed.TotalMilliseconds / 600;
            Console.WriteLine($"  {Kinds[k],-6} research n={Live(r),3} mix=[{string.Join(",", Mix(r))}]  game n={Live(g),3}/{plans[k].N} mix=[{string.Join(",", Mix(g))}] " +
                              $"plan=[{string.Join(",", plans[k].Mix)}] comp err {CompositionError(g):F3}  {ms:F2} ms/step");
            Check(Live(r) >= 0.8 * plans[k].N, $"{Kinds[k]}: research grows to >= 80% of the plan");
            Check(Live(g) >= 0.8 * plans[k].N, $"{Kinds[k]}: game grows to >= 80% of the plan");
            Check(g.PlanIx == k, $"{Kinds[k]}: never leaves its own plan");
            grown[k] = g;
        }

        Console.WriteLine("\nE3. funded laying: an empty stomach lays nothing; a meal lays what it pays for");
        {
            var c = GameSwarm(plans, rule, 1, 3, fed: false);
            int n0 = c.AliveCount; Run(c, 200);
            Check(c.AliveCount == n0, $"starved swarm stays at its seed ({n0} -> {c.AliveCount})");
            c.Stomach[1] = 10f; Run(c, 300);
            Console.WriteLine($"  10 Mass eggs of food -> {c.AliveCount - n0} laid");
            Check(c.AliveCount - n0 >= 1 && c.AliveCount - n0 <= 10, "own-element food funds at most one egg per EggCost");
        }

        Console.WriteLine("\nE4. selective killing morphs the body; nothing withers on its own (lossless)");
        int before = SelfDeaths;
        foreach (var (from, to) in new[] { (1, 2), (0, 3), (3, 1), (2, 0) })
        {
            var c = GameSwarm(plans, rule, from, 11); Run(c, 600);
            if (!GridCull(c, to, new Random(5))) { Console.WriteLine($"  {Kinds[from]} -> {Kinds[to]}: n/a"); continue; }
            int nAfter = Live(c), switched = -1;
            for (int t = 0; t < 600; t++) { Step(c); foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched && switched < 0) switched = t; c.Events.Clear(); }
            Console.WriteLine($"  {Kinds[from]} -> {Kinds[to]}: n {nAfter} -> {Live(c)}, plan {c.Plan.Kind} (switched at step {switched}), " +
                              $"mix=[{string.Join(",", Mix(c))}] plan mix=[{string.Join(",", plans[to].Mix)}] comp err {CompositionError(c):F3}, eggs lost {c.EggsLost}");
            Check(c.PlanIx == to, $"{Kinds[from]} -> {Kinds[to]}: the body becomes the new majority's creature");
            Check(c.EggsLost == 0, $"{Kinds[from]} -> {Kinds[to]}: no paid-for egg is lost (KeepEggs)");
        }
        Check(SelfDeaths == before, $"zero self-inflicted deaths across the morphs ({SelfDeaths - before})");

        Console.WriteLine("\nE5. swimming: the body cruises to a target 150 away and turns to face it, shape held");
        {
            var c = GameSwarm(plans, rule, 1, 13); Run(c, 400);
            var start = c.Anchor; c.SwimTarget = c.Anchor + new Vector3(0, 0, 150);
            Run(c, 900);
            float left = Vector3.Distance(c.Anchor, c.SwimTarget);
            Console.WriteLine($"  travelled {Vector3.Distance(start, c.Anchor):F0} of 150, {left:F1} left, heading.z {c.Heading.Z:F2}, comp err {CompositionError(c):F3}");
            Check(left < 30f, "arrives near its swim target");
            Check(c.Heading.Z > 0.8f, "faces where it swam");
        }

        Console.WriteLine("\nE6. vessel reaction: a ship passing through startles the body; it recovers");
        {
            var c = GameSwarm(plans, rule, 0, 17); Run(c, 400);
            var preds = new[] { new SwarmPredator { C = c.Anchor - new Vector3(30, 0, 0), V = new Vector3(3f, 0, 0), R = 2f } };
            float peak = 0;
            for (int t = 0; t < 20; t++) { preds[0].C += preds[0].V; Step(c, preds); c.Events.Clear(); peak = MathF.Max(peak, c.ThreatLevel); }
            Run(c, 200);
            Console.WriteLine($"  peak threat {peak:F2}, after 200 calm steps {c.ThreatLevel:F2}, n={Live(c)}");
            Check(peak > 0.1f, "the swarm notices the ship");
            Check(c.ThreatLevel < 0.05f, "and settles once it has gone");
        }

        Console.WriteLine("\nE7. cost per step, grown game bodies (CoreCLR, Release) - and how much of it is the network");
        foreach (var k in Enumerable.Range(0, 4))
        {
            var c = grown[k]; c.NetMs = 0; c.NetCalls = 0;
            var sw = Stopwatch.StartNew(); Run(c, 300); double ms = sw.Elapsed.TotalMilliseconds / 300;
            Console.WriteLine($"  {Kinds[k],-6} n={Live(c),3}  {ms:F3} ms/step  network {c.NetMs / 300:F3} ms/step ({100 * c.NetMs / 300 / ms:F0}%), " +
                              $"{c.NetCalls / 300.0:F0} forward passes/step, {1000 * c.NetMs / Math.Max(1, c.NetCalls):F2} us/pass");
        }

        Console.WriteLine("\nE8. feel: reversals and freezes (swarm_feel osc / stuck) of a grown body, game vs research");
        float mo = 0, mst = 0, mor = 0;
        foreach (var k in Enumerable.Range(0, 4))
        {
            var g = GameSwarm(plans, rule, k, 21); Run(g, 240); float og = Osc(g, out float sg, out float vg);
            var r = ResearchSwarm(plans, rule, k, 21); Run(r, 240); float or = Osc(r, out float sr, out float vr);
            Console.WriteLine($"  {Kinds[k],-6} game osc {og:F3} stuck {sg:F3} speed {vg:F3}   research osc {or:F3} stuck {sr:F3} speed {vr:F3}");
            mo += og / 4; mst += sg / 4; mor += or / 4;
        }
        Console.WriteLine($"  mean   game osc {mo:F3} stuck {mst:F3}   research osc {mor:F3}");
        Check(mo <= 0.08f, "the game body does not twitch (mean osc <= 0.08, swarm_feel's organic band)");
        Check(mst <= 0.02f, "no frozen members (mean stuck <= 0.02, swarm_feel's organic band)");

        if (Environment.GetEnvironmentVariable("SWARM_EVO_PROBE") == "1")
        {
            var g = GameSwarm(plans, rule, 1, 21); Run(g, 240);
            var ids = Enumerable.Range(0, g.Cap).Where(i => g.Active[i] && g.Hatched[i]).ToList();
            var prev = ids.Select(i => g.Pos[i]).ToArray(); Vector3[] prevD = null; bool[] prevF = null;
            int rFF = 0, rFI = 0, rIF = 0, rII = 0, n = 0;
            for (int t = 0; t < 64; t++)
            {
                var a0 = g.Anchor; Step(g); g.Events.Clear();
                var d = ids.Select((i, q) => g.Pos[i] - prev[q]).ToArray(); var f = ids.Select(i => g.Fired[i]).ToArray();
                if (t % 8 == 0) Console.WriteLine($"   t{t} dT/R {Vector3.Distance(g.Anchor, g.SwimTarget) / g.Plan.Radius:F2} anchor step {Vector3.Distance(a0, g.Anchor):F3} mean|d| {d.Average(x => x.Length()):F3} idle mean|d| {d.Where((x, q) => !f[q]).DefaultIfEmpty().Average(x => x.Length()):F3}");
                if (prevD != null)
                    for (int q = 0; q < d.Length; q++)
                    {
                        n++;
                        float c = Vector3.Dot(d[q], prevD[q]) / MathF.Max(d[q].Length() * prevD[q].Length(), 1e-6f);
                        if (c < -0.5f) { if (prevF[q] && f[q]) rFF++; else if (prevF[q]) rFI++; else if (f[q]) rIF++; else rII++; }
                    }
                prevD = d; prevF = f; prev = ids.Select(i => g.Pos[i]).ToArray();
            }
            Console.WriteLine($"   reversals: fired->fired {rFF} fired->idle {rFI} idle->fired {rIF} idle->idle {rII} of {n}");
        }
        Console.WriteLine($"\nevofate core: {(_fail == 0 ? "OK" : $"FAIL ({_fail})")}");
        return _fail;
    }

    /// <summary>swarm_eval.cull_to (GridHarness.CullTo's rule) on the evofate core.</summary>
    public static bool GridCull(SwarmEvoFateCore c, int e, Random rng)
    {
        var cnt = Mix(c); int ce = cnt[e];
        if (ce < 2) return false;
        int lead = Math.Max(1, (int)Math.Round(0.1 * ce)), cap = Math.Max(0, ce - lead);
        var kills = new int[4]; for (int f = 0; f < 4; f++) kills[f] = f == e ? 0 : Math.Max(0, cnt[f] - cap);
        if (cnt.Sum() - kills.Sum() < 6) return false;
        for (int f = 0; f < 4; f++)
        {
            var idx = Enumerable.Range(0, c.Cap).Where(i => c.Active[i] && c.Hatched[i] && c.EffectiveElement(i) == f).ToList();
            for (int q = idx.Count - 1; q > 0; q--) { int j = rng.Next(q + 1); (idx[q], idx[j]) = (idx[j], idx[q]); }
            for (int q = 0; q < kills[f]; q++) c.Kill(idx[q]);
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────── yardstick records (score_combo.py)

    public static SwarmEvoFateCore Grow(SwarmPlanData[] plans, string plansDir, string mode, int k, int seed)
    {
        var rule = Rule(plansDir);
        var c = mode == "researchEvo" ? ResearchSwarm(plans, rule, k, seed) : GameSwarm(plans, rule, k, seed);
        Run(c, 240);
        return c;
    }

    public static void RunSteps(SwarmEvoFateCore c, int steps) => Run(c, steps);

    public static void AppendRecord(StringBuilder sb, ref bool first, SwarmEvoFateCore c, string mode, string tag, int k, int e, int seed, int r, bool window)
    {
        if (!first) sb.Append(','); first = false;
        sb.Append($"{{\"mode\":\"{mode}\",\"tag\":\"{tag}\",\"kind\":\"{Kinds[k]}\",\"want\":\"{Kinds[e]}\",\"seed\":{seed},\"sample\":{r},\"molts\":0,\"stray\":0,\"eggsLost\":{c.EggsLost},\"units\":[");
        var cen = Vector3.Zero; int n = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { cen += c.Pos[i]; n++; }
        cen /= Math.Max(1, n);
        bool u1 = true; const int C = SwarmEvoRule.C;
        for (int i = 0; i < c.Cap; i++)
        {
            if (!c.Active[i]) continue;
            int o = i * C;
            var p = c.InvRotate(c.Pos[i] - cen);
            var fa = new Vector3(c.S[o + 1], c.S[o + 2], c.S[o + 3]);   // G2's facing channels are already in the body frame
            var vals = new float[] { p.X, p.Y, p.Z, c.Elem[i], c.Dom[i], c.Hatched[i] ? 1 : 0, c.S[o], c.S[o + 31],
                c.S[o + 4], c.S[o + 5], c.S[o + 6], c.S[o + 7], c.S[o + 8], c.S[o + 9], fa.X, fa.Y, fa.Z, c.S[o + 10], c.S[o + 11] };
            if (!u1) sb.Append(','); u1 = false;
            sb.Append('[').Append(string.Join(",", vals.Select(F))).Append(']');
        }
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
                Step(c); c.Events.Clear();
                alive.RemoveAll(i => !(c.Active[i] && c.Hatched[i] && c.Elem[i] == el[i]));
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

    static string F(float x) => x.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
}
