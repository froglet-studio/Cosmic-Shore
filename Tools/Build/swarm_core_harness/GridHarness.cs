// Headless proof of the SHIPPED grid core (Assets/.../Swarm/SwarmGridCore.cs - the research's
// hgrid2). Two jobs:
//   GridHarness.Run(plans)  - asserted game-side tests (growth, funded laying, morph on selective
//                             kills, no self-inflicted death, vessel reaction, swimming, band, cost);
//   GridHarness.Export(...) - grow each plan from the research's seed in RESEARCH mode (the model
//                             exactly as hgrid2 runs it) and in GAME mode, and write every tadpole's
//                             state as JSON for Tools/Build/swarm_core_harness/score_grid.py, which
//                             scores it with the research's UNCHANGED scorer (swarm_nca.swarm_loss).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using CosmicShore.Gameplay;

static class GridHarness
{
    static int _fail;
    static readonly string[] Kinds = { "charge", "mass", "space", "time" };

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    /// <summary>hgrid2 exactly as the research runs it: three domain slots, hunger kills, free
    /// laying, the fixed frame, the 80-voxel membrane, capacity 280, lock 0.</summary>
    public static SwarmGridParams Research() => new()
    {
        DomainSlots = true, HungerKills = true, Funded = false, Oriented = false, Quant = true,
        Cruise = 0f, Membrane = 80f, Cap = 280, Lock = 0,
    };

    /// <summary>The game's settings (SwarmFauna builds the same from SwarmFaunaConfigSO).</summary>
    public static SwarmGridParams Game(SwarmPlanData[] plans) => Override(new SwarmGridParams
    {
        DomainSlots = false, HungerKills = false, Funded = true, Oriented = true, Quant = false,
        Cruise = 0.35f, Membrane = 600f, Cap = plans.Max(p => p.N), Lock = 30, LayMaxPerStep = 3,
        KillLayHoldSteps = 20,
    });

    /// <summary>The shipped SwarmFaunaConfig's SeedMembers (author_swarm_fauna.py SEED_MEMBERS).</summary>
    public const int GameSeed = 96;

    /// <summary>Experiments: SWARM_GRID_GAME="Field=value,..." overrides game-mode params by name.</summary>
    static SwarmGridParams Override(SwarmGridParams p)
    {
        var env = Environment.GetEnvironmentVariable("SWARM_GRID_GAME");
        if (string.IsNullOrEmpty(env)) return p;
        foreach (var kv in env.Split(','))
        {
            var a = kv.Split('='); var f = typeof(SwarmGridParams).GetField(a[0]);
            f.SetValue(p, f.FieldType == typeof(bool) ? a[1] == "1" || a[1] == "true"
                : f.FieldType == typeof(int) ? int.Parse(a[1]) : (object)float.Parse(a[1], CultureInfo.InvariantCulture));
        }
        return p;
    }

    /// <summary>The research seed (swarm_nca.seed_swarm): 16 hatched tadpoles at the plan's element
    /// and slot mix in a knot of randn x 2 at the origin.</summary>
    public static SwarmGridCore ResearchSwarm(SwarmPlanData[] plans, int planElement, int seed, int count = 16)
    {
        var c = new SwarmGridCore(plans, Research(), seed);
        var plan = plans[planElement];
        c.SeedWith(LR(plan.Mix, count), LR(plan.SlotMix, count), Vector3.Zero, 2f);
        return c;
    }

    public static SwarmGridCore GameSwarm(SwarmPlanData[] plans, int planElement, int seed, bool fed = true, SwarmGridParams p = null)
    {
        var c = new SwarmGridCore(plans, p ?? Game(plans), seed);
        c.Seed(planElement, GameSeed, new Vector3(200, 0, 0), Vector3.UnitX);
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

    static void Run(SwarmGridCore c, int steps, SwarmPredator[] preds = null)
    {
        preds ??= Array.Empty<SwarmPredator>();
        for (int t = 0; t < steps; t++) { c.Step(preds); c.Events.Clear(); }
    }

    static int Live(SwarmGridCore c) { int n = 0; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) n++; return n; }

    static int[] Mix(SwarmGridCore c) { var m = new int[4]; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) m[c.Elem[i]]++; return m; }

    static int Major(int[] m) { int b = 0; for (int e = 1; e < 4; e++) if (m[e] > m[b]) b = e; return b; }

    // ─────────────────────────────────────────────────────────────────────── tests

    public static int Run(SwarmPlanData[] plans)
    {
        Console.WriteLine("\n================ GRID CORE (SwarmGridCore - hgrid2) ================");

        Console.WriteLine("\nG1. research mode: each plan grows from the research seed (16) for 240 steps");
        for (int e = 0; e < 4; e++)
        {
            var c = ResearchSwarm(plans, e, 7);
            Run(c, 240);
            var m = Mix(c);
            Console.WriteLine($"  {plans[e].Kind,-7} n={Live(c),3}/{plans[e].N} mix=[{string.Join(",", m)}] vs [{string.Join(",", plans[e].Mix)}] plan={c.Plan.Kind} hunger deaths={c.Deaths}");
            Check(Live(c) >= 32, $"{plans[e].Kind}: grows past the research's 32-tadpole floor");
            Check(c.PlanIx == e, $"{plans[e].Kind}: grows its own plan");
        }

        Console.WriteLine("\nG2. game mode: growth, fed (one domain, funded, oriented, lock 30)");
        var grown = new SwarmGridCore[4];
        for (int e = 0; e < 4; e++)
        {
            var c = GameSwarm(plans, e, 11);
            var sw = Stopwatch.StartNew(); Run(c, 400); double ms = sw.Elapsed.TotalMilliseconds / 400;
            var m = Mix(c);
            Console.WriteLine($"  {plans[e].Kind,-7} n={Live(c),3}/{plans[e].N} mix=[{string.Join(",", m)}] plan={c.Plan.Kind} self-deaths={c.Deaths} {ms:F3} ms/step");
            Check(Live(c) >= 0.85f * plans[e].N, $"{plans[e].Kind}: reaches >= 85% of the plan's headcount");
            Check(c.PlanIx == e && Major(m) == e, $"{plans[e].Kind}: keeps its own plan and majority");
            Check(c.Deaths == 0, $"{plans[e].Kind}: no member dies by itself in game mode (hunger only picks a victim)");
            grown[e] = c;
        }

        Console.WriteLine("\nG3. funded laying: an empty stomach lays nothing; a fixed meal lays exactly what it pays for");
        {
            var c = GameSwarm(plans, 1, 12, fed: false);
            Run(c, 200);
            int seeded = Math.Min(GameSeed, plans[1].N);
            Check(c.AliveCount == seeded, $"unfed grid swarm stays at its seed (n={c.AliveCount})");
            c.Stomach[1] = 10f * c.C.EggCost[1];
            Run(c, 300);
            Console.WriteLine($"  10 Mass eggs of food -> laid {c.AliveCount - seeded}");
            Check(c.AliveCount - seeded <= 10 && c.AliveCount - seeded >= 5, "a meal funds at most its own eggs (cross-element ones at CrossCost)");
            Check(c.Stomach.Sum() < c.C.EggCost[1] * c.C.CrossCost + 1e-3f, "and spends it");
        }

        Console.WriteLine("\nG4. selective killing morphs the body (stomach empty; kill the majority until another leads)");
        for (int e = 0; e < 4; e++)
        {
            var c = grown[e];
            for (int q = 0; q < 4; q++) c.Stomach[q] = 0f;
            var cnt = Mix(c);
            int runner = Enumerable.Range(0, 4).Where(x => x != e).OrderByDescending(x => cnt[x]).First();
            int killed = 0, switches = 0, at = -1;
            for (int i = 0; i < c.Cap && Mix(c)[e] >= Mix(c)[runner]; i++)
                if (c.Active[i] && c.Hatched[i] && c.Elem[i] == e)
                {
                    c.Kill(i); killed++; c.Step(Array.Empty<SwarmPredator>());
                    foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) { switches++; at = killed; }
                    c.Events.Clear();
                }
            for (int t = 0; t < 400; t++)
            {
                c.Step(Array.Empty<SwarmPredator>());
                foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) switches++;
                c.Events.Clear();
            }
            Console.WriteLine($"  {plans[e].Kind,-7} killed {killed,3} -> plan {c.Plan.Kind} (committed at kill {at}, {switches} switch in 400 steps after) n={c.AliveCount} mix=[{string.Join(",", Mix(c))}] deaths={c.Deaths}");
            Check(c.PlanIx == runner, $"{plans[e].Kind}: morphs to the runner-up's plan ({plans[runner].Kind})");
            Check(switches == 1, $"{plans[e].Kind}: one commit, no flip-back");
        }

        Console.WriteLine("\nG4b. a FED swarm, a 4 s burst of kills: it morphs only because a wounded swarm holds its eggs");
        foreach (int hold in new[] { 0, 20 })
        {
            var p = Game(plans); p.KillLayHoldSteps = hold; p.LayMaxPerStep = 16;
            var c = GameSwarm(plans, 3, 9, p: p); Run(c, 400);   // dragonfly, bottomless food
            int killed = 0, switches = 0;
            for (int t = 0; t < 300; t++)
            {
                if (t < 40) for (int q = 0, i = 0; i < c.Cap && q < 2; i++) if (c.Active[i] && c.Hatched[i] && c.Elem[i] == 3) { c.Kill(i); killed++; q++; }
                c.Step(Array.Empty<SwarmPredator>());
                foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) switches++;
                c.Events.Clear();
            }
            Console.WriteLine($"  hold {hold,2} steps: killed {killed} Time -> plan {c.Plan.Kind}, n={c.AliveCount}, {switches} switches");
            if (hold > 0) Check(switches == 1 && c.PlanIx != 3, "with the hold, the burst morphs it");
        }

        Console.WriteLine("\nG5. starvation picks the hungriest (misplaced surplus) member - and only when asked");
        {
            var c = GameSwarm(plans, 1, 13); Run(c, 300);
            // force a surplus: 12 extra Time members laid outside the whale's Time class
            for (int q = 0; q < 4; q++) c.Stomach[q] = 0f;
            int freed = 0;   // the whale is at capacity: free 12 slots by killing Mass members first
            for (int i = 0; i < c.Cap && freed < 12; i++) if (c.Active[i] && c.Elem[i] == 1) { c.Kill(i); freed++; }
            int added = 0;
            for (int i = 0; i < c.Cap && added < 12; i++)
                if (!c.Active[i]) { c.Active[i] = true; c.Hatched[i] = true; c.Elem[i] = 3; c.Pos[i] = c.Anchor + new Vector3(40, 0, 0); added++; }
            Run(c, 60);
            int v = c.StarvationVictim();
            float maxH = Enumerable.Range(0, c.Cap).Where(i => c.Active[i]).Max(i => c.Hunger[i]);
            Console.WriteLine($"  victim={v} elem={(v >= 0 ? c.Elem[v] : -1)} hunger={(v >= 0 ? c.Hunger[v] : 0):F2} (max {maxH:F2}); deaths={c.Deaths}");
            Check(v >= 0 && c.Hunger[v] >= maxH - 1e-6f && c.Hunger[v] > 0f, "the victim is the hungriest member");
            Check(c.Deaths == 0, "nobody withered on the hunger clock");
        }

        Console.WriteLine("\nG6. vessel reaction: a ship flies straight through the body (no kills)");
        foreach (bool react in new[] { true, false })
            foreach (int e in new[] { 1, 0, 3 })
            {
                var c = GameSwarm(plans, e, 3); Run(c, 400);
                if (!react) c.C.Sense = 1e-4f;
                var ship = new SwarmPredator { C = c.Anchor - new Vector3(120, 0, 0), V = new Vector3(6, 0, 0), R = 4 };
                var touched = new bool[c.Cap]; float maxThreat = 0;
                for (int t = 0; t < 45; t++)
                {
                    c.Step(new[] { ship }); c.Events.Clear(); ship.C += ship.V;
                    for (int i = 0; i < c.Cap; i++) if (c.Active[i] && Vector3.Distance(c.Pos[i], ship.C) < ship.R) touched[i] = true;
                    maxThreat = Math.Max(maxThreat, c.ThreatLevel);
                }
                float frac = touched.Count(x => x) / (float)Math.Max(1, c.AliveCount);
                Console.WriteLine($"  {(react ? "reacting" : "inert   ")} {plans[e].Kind,-7} touched {frac:P0}  peak threat {maxThreat:F2}");
                if (react && e == 0) Check(maxThreat > 0.2f, "the pufferfish's threat rises (it inflates)");
            }

        Console.WriteLine("\nG7. swimming: the body travels to its target and keeps its plan");
        {
            var c = GameSwarm(plans, 1, 5); Run(c, 400);
            var start = c.Anchor; c.SwimTarget = start + new Vector3(0, 0, 150);
            Run(c, 1500);
            float left = Vector3.Distance(c.Anchor, c.SwimTarget);
            Console.WriteLine($"  travelled {Vector3.Distance(start, c.Anchor):F0} of 150, {left:F1} left, plan {c.Plan.Kind}, n={Live(c)}");
            Check(left < 25f, "arrives near its swim target");
            Check(c.PlanIx == 1 && Live(c) >= 0.85f * plans[1].N, "still a whale on arrival");
        }

        Console.WriteLine("\nG7b. hovering on its goal, the body holds its heading (it must not chase its own jitter)");
        for (int e = 0; e < 4; e++)
        {
            var c = GameSwarm(plans, e, 6); Run(c, 400);
            var prev = c.Heading; double turned = 0;
            for (int t = 0; t < 600; t++)
            {
                c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear();
                turned += Math.Acos(Math.Clamp(Vector3.Dot(prev, c.Heading), -1f, 1f)); prev = c.Heading;
            }
            Console.WriteLine($"  {plans[e].Kind,-7} heading turned {turned * 180 / Math.PI:F1} deg over 600 hovering steps");
            Check(turned * 180 / Math.PI < 5, $"{plans[e].Kind}: a hovering body does not spin (< 5 deg / minute)");
        }

        Console.WriteLine("\nG8. band: the body's centre is held in its radial band");
        {
            var p = Game(plans); p.BandInner = 150; p.BandOuter = 250;
            var c = GameSwarm(plans, 1, 7, p: p);
            c.SwimTarget = Vector3.Zero;
            float minR = 1e9f, maxR = 0;
            for (int t = 0; t < 800; t++) { c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear(); if (t > 50) { float r = c.Anchor.Length(); minR = Math.Min(minR, r); maxR = Math.Max(maxR, r); } }
            Console.WriteLine($"  centre radius stayed in [{minR:F1}, {maxR:F1}]");
            Check(minR >= 140f && maxR <= 260f, "centre within the band (a soft clamp: 10 voxels of give)");
        }

        Console.WriteLine("\nG9. cost per step, grown bodies (game mode, CoreCLR, Release)");
        for (int e = 0; e < 4; e++)
        {
            var c = grown[e] ?? GameSwarm(plans, e, 11);
            var c2 = GameSwarm(plans, e, 21); Run(c2, 400);
            var sw = Stopwatch.StartNew(); Run(c2, 300); double ms = sw.Elapsed.TotalMilliseconds / 300;
            Console.WriteLine($"  {plans[e].Kind,-7} n={Live(c2),3}  {ms:F3} ms/step");
        }

        Console.WriteLine($"\ngrid core: {(_fail == 0 ? "OK" : $"FAIL ({_fail})")}");
        return _fail;
    }

    // ─────────────────────────────────────────────────────────────────────── export

    /// <summary>export &lt;plans dir&gt; &lt;out.json&gt; &lt;seeds,comma&gt; [steps] [win]</summary>
    public static int Export(string[] args, Func<string, SwarmPlanData[]> load)
    {
        var plans = load(args[1]);
        string outPath = args[2];
        var seeds = args[3].Split(',').Select(int.Parse).ToArray();
        int steps = args.Length > 4 ? int.Parse(args[4]) : 240, win = args.Length > 5 ? int.Parse(args[5]) : 64;
        var sb = new StringBuilder();
        sb.Append("{\"runs\":[");
        bool first = true;
        var modes = args.Length > 6 ? args[6].Split(',') : new[] { "research", "game" };
        foreach (string mode in modes)
            foreach (int seed in seeds)
                for (int e = 0; e < 4; e++)
                {
                    if (mode == "field") { ExportFieldFeel(sb, ref first, plans, e, seed, win); continue; }
                    if (mode.StartsWith("sort")) { SortHarness.ExportRun(sb, ref first, plans, mode, e, seed, steps, win); continue; }
                    var c = mode == "research" ? ResearchSwarm(plans, e, seed) : GameSwarm(plans, e, seed);
                    var sw = Stopwatch.StartNew();
                    Run(c, steps);
                    double ms = sw.Elapsed.TotalMilliseconds / steps;
                    if (!first) sb.Append(','); first = false;
                    sb.Append($"{{\"mode\":\"{mode}\",\"kind\":\"{Kinds[e]}\",\"seed\":{seed},\"ms\":{F(ms)},\"plan\":\"{c.Plan.Kind}\",\"units\":[");
                    var centre = c.Anchor;
                    bool u1 = true;
                    for (int i = 0; i < c.Cap; i++)
                    {
                        if (!c.Active[i]) continue;
                        var p = c.InvRotate(c.Pos[i] - centre);
                        int o = i * SwarmGridCore.LOOK;
                        var fa = c.InvRotate(new Vector3(c.Look[o + 6], c.Look[o + 7], c.Look[o + 8]));
                        var vals = new List<float> { p.X, p.Y, p.Z, c.Elem[i], c.Dom[i], c.Hatched[i] ? 1 : 0, c.Alpha[i], c.Hunger[i],
                            c.Look[o], c.Look[o + 1], c.Look[o + 2], c.Look[o + 3], c.Look[o + 4], c.Look[o + 5],
                            fa.X, fa.Y, fa.Z, c.Look[o + 9], c.Look[o + 10] };
                        if (!u1) sb.Append(','); u1 = false;
                        sb.Append('[').Append(string.Join(",", vals.Select(F))).Append(']');
                    }
                    sb.Append("],\"win\":[");
                    // the feel window: positions over `win` more steps of the tadpoles alive (and of one
                    // element) throughout - swarm_feel.record's protocol
                    var alive = Enumerable.Range(0, c.Cap).Where(i => c.Active[i] && c.Hatched[i]).ToList();
                    var el = alive.ToDictionary(i => i, i => c.Elem[i]);
                    var order = alive.ToList();
                    var frames = new List<Vector3[]>();
                    for (int t = 0; t <= win; t++)
                    {
                        frames.Add(order.Select(i => c.Pos[i]).ToArray());
                        if (t == win) break;
                        c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear();
                        alive.RemoveAll(i => !(c.Active[i] && c.Hatched[i] && c.Elem[i] == el[i]));
                    }
                    var keep = new HashSet<int>(alive);
                    for (int t = 0; t < frames.Count; t++)
                    {
                        if (t > 0) sb.Append(',');
                        sb.Append('[');
                        bool f1 = true;
                        for (int q = 0; q < order.Count; q++)
                        {
                            if (!keep.Contains(order[q])) continue;
                            var x = frames[t][q];
                            if (!f1) sb.Append(','); f1 = false;
                            sb.Append($"[{F(x.X)},{F(x.Y)},{F(x.Z)}]");
                        }
                        sb.Append(']');
                    }
                    sb.Append("],\"winElem\":[").Append(string.Join(",", order.Where(keep.Contains).Select(i => el[i]))).Append("]}");
                    Console.WriteLine($"  exported {mode,-8} {Kinds[e],-6} seed {seed}: n={Live(c)} plan={c.Plan.Kind} {ms:F3} ms/step");
                }
        sb.Append("]}");
        File.WriteAllText(outPath, sb.ToString());
        return 0;
    }

    /// <summary>The FIELD core's feel window, for contrast (the lead: "lost too much organic
    /// imperfection"). Grown 600 steps fed (its own tests' protocol), then `win` steps recorded.</summary>
    static void ExportFieldFeel(StringBuilder sb, ref bool first, SwarmPlanData[] plans, int e, int seed, int win)
    {
        var c = new SwarmFieldCore(plans, new SwarmFieldParams { Membrane = 600f, Cap = plans.Max(p => p.N), Cruise = 0.35f }, seed);
        c.Seed(e, 24, new Vector3(200, 0, 0), Vector3.UnitX);
        c.SwimTarget = c.Anchor;
        for (int q = 0; q < 4; q++) c.Stomach[q] = 1e6f;
        for (int t = 0; t < 600; t++) { c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear(); }
        var alive = Enumerable.Range(0, c.Cap).Where(i => c.Alive[i]).ToList();
        var frames = new List<Vector3[]>();
        for (int t = 0; t <= win; t++)
        {
            frames.Add(alive.Select(i => c.Pos[i]).ToArray());
            if (t < win) { c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear(); }
        }
        if (!first) sb.Append(','); first = false;
        sb.Append($"{{\"mode\":\"field\",\"kind\":\"{Kinds[e]}\",\"seed\":{seed},\"ms\":0,\"plan\":\"{c.Plan.Kind}\",\"units\":[],\"win\":[");
        for (int t = 0; t < frames.Count; t++)
        {
            if (t > 0) sb.Append(',');
            sb.Append('[').Append(string.Join(",", frames[t].Select(x => $"[{F(x.X)},{F(x.Y)},{F(x.Z)}]"))).Append(']');
        }
        sb.Append("],\"winElem\":[").Append(string.Join(",", alive.Select(i => c.Elem[i]))).Append("]}");
    }

    static string F(float x) => x.ToString("G6", CultureInfo.InvariantCulture);
    static string F(double x) => x.ToString("G6", CultureInfo.InvariantCulture);
}
