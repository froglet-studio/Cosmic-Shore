// Headless proof of the SHIPPED sort core (Assets/.../Swarm/SwarmSortCore.cs - the research's
// emergent cell sorting, Tools/NCA/sort_model.py). Two jobs:
//   SortHarness.Run(plans)      - asserted game-side tests (growth, funded laying, morph on selective
//                                 kills, the lossless corrector after a morph, ZERO self-inflicted
//                                 deaths, animated molts, wells that travel with the body, vessel
//                                 reaction, swimming, band, cost);
//   SortHarness.ExportRun(...)  - grow a plan from the research's seed in RESEARCH mode (the model
//                                 exactly as sort_model runs it) or in GAME mode, and write every
//                                 tadpole's state for Tools/Build/swarm_core_harness/score_sort.py.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using CosmicShore.Gameplay;

static class SortHarness
{
    static int _fail;
    static readonly string[] Kinds = { "charge", "mass", "space", "time" };

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    /// <summary>sort_model.SortSwarm exactly as the research runs it (results/sort/params.json): domain
    /// regions, free laying, instant molts, frame 0, the fixed frame, no membrane, capacity 280.</summary>
    public static SwarmSortParams Research() => new()
    {
        DomainSlots = true, Funded = false, MoltSteps = 0, MoltWindow = -1, Animate = false, KWellFF = 0f,
        WellLook = false, Oriented = false, Cruise = 0f, Cap = 280,
    };

    /// <summary>The game's settings (SwarmFauna.BuildSortCore builds the same from SwarmFaunaConfigSO).</summary>
    public static SwarmSortParams Game(SwarmPlanData[] plans) => Override(new SwarmSortParams
    {
        DomainSlots = false, Funded = true, MoltSteps = 10, MoltWindow = -1, Animate = true, KWellFF = 1f, Noise = 0.1f,
        WellLook = true, Oriented = true, Cruise = 0.35f, Membrane = 600f, Cap = plans.Max(p => p.N),
        LayMax = 5, KillLayHoldSteps = 20,
    });

    public const int GameSeed = GridHarness.GameSeed;

    /// <summary>Experiments: SWARM_SORT_GAME="Field=value,..." overrides game-mode params by name.</summary>
    static SwarmSortParams Override(SwarmSortParams p)
    {
        var env = Environment.GetEnvironmentVariable("SWARM_SORT_GAME");
        if (string.IsNullOrEmpty(env)) return p;
        foreach (var kv in env.Split(','))
        {
            var a = kv.Split('='); var f = typeof(SwarmSortParams).GetField(a[0]);
            f.SetValue(p, f.FieldType == typeof(bool) ? a[1] == "1" || a[1] == "true"
                : f.FieldType == typeof(int) ? int.Parse(a[1]) : (object)float.Parse(a[1], CultureInfo.InvariantCulture));
        }
        return p;
    }

    /// <summary>The research seed (swarm_nca.seed_swarm): 16 hatched tadpoles at the plan's element
    /// and slot mix in a knot of randn x 2 at the origin.</summary>
    public static SwarmSortCore ResearchSwarm(SwarmPlanData[] plans, int planElement, int seed, int count = 16)
    {
        var c = new SwarmSortCore(plans, Research(), seed);
        var plan = plans[planElement];
        c.SeedWith(LR(plan.Mix, count), LR(plan.SlotMix, count), Vector3.Zero, 2f);
        return c;
    }

    public static SwarmSortCore GameSwarm(SwarmPlanData[] plans, int planElement, int seed, bool fed = true, SwarmSortParams p = null)
    {
        var c = new SwarmSortCore(plans, p ?? Game(plans), seed);
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

    /// <summary>Self-inflicted deaths, counted the way Tools/NCA/scorecard.py counts them: a member
    /// active before a step and inactive after it, when nobody outside called Kill.</summary>
    static int _selfDeaths;

    static void Step(SwarmSortCore c, SwarmPredator[] preds = null)
    {
        var before = (bool[])c.Active.Clone();
        c.Step(preds ?? Array.Empty<SwarmPredator>());
        for (int i = 0; i < c.Cap; i++) if (before[i] && !c.Active[i]) _selfDeaths++;
    }

    static void Run(SwarmSortCore c, int steps, SwarmPredator[] preds = null)
    {
        for (int t = 0; t < steps; t++) { Step(c, preds); c.Events.Clear(); }
    }

    static int Live(SwarmSortCore c) { int n = 0; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) n++; return n; }

    static int[] Mix(SwarmSortCore c) { var m = new int[4]; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) m[c.EffectiveElement(i)]++; return m; }

    static int Major(int[] m) { int b = 0; for (int e = 1; e < 4; e++) if (m[e] > m[b]) b = e; return b; }

    /// <summary>How far the body is from its plan's composition: members over each element's plan count.</summary>
    static int Surplus(SwarmSortCore c)
    {
        var m = Mix(c); var p = c.Plan.Mix; int s = 0;
        for (int e = 0; e < 4; e++) s += Math.Max(0, m[e] - p[e]);
        return s;
    }

    /// <summary>Median distance (voxels, body frame) from each member to its fated well's centre, at the
    /// code frame the core is reading - the "wells travel with the body" measure.</summary>
    static float WellError(SwarmSortCore c)
    {
        var code = c.Code; var plan = c.Plan;
        int per = c.C.Periods[c.PlanIx], L = plan.Order.Length, slot = c.Clock / per;
        int fA = c.C.Animate ? plan.Order[slot % L] : 0, fB = c.C.Animate ? plan.Order[(slot + 1) % L] : 0;
        float fa = c.C.Animate ? (c.Clock % per) / (float)per : 0f;
        var cen = Vector3.Zero; int n = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { cen += c.Pos[i]; n++; }
        cen /= Math.Max(1, n);
        var d = new List<float>();
        for (int i = 0; i < c.Cap; i++)
        {
            if (!c.Active[i] || !c.Hatched[i] || c.Fate[i] < 1) continue;
            int t = c.EffectiveElement(i) * 3, k = code.WStart[t] + c.Fate[i] - 1;
            if (k >= code.WStart[t + 1]) continue;
            var mu = code.Mu[fA][k] + fa * (code.Mu[fB][k] - code.Mu[fA][k]);
            d.Add(Vector3.Distance(c.InvRotate(c.Pos[i] - cen), mu));
        }
        if (d.Count == 0) return float.NaN;
        d.Sort(); return d[d.Count / 2];
    }

    // ─────────────────────────────────────────────────────────────────────── tests

    public static int Run(SwarmPlanData[] plans)
    {
        Console.WriteLine("\n================ SORT CORE (SwarmSortCore - emergent cell sorting) ================");
        _selfDeaths = 0;

        Console.WriteLine("\nS0. the code: wells per type (K <= 12, at most one per 4 units)");
        for (int e = 0; e < 4; e++)
        {
            var rc = SwarmSortCode.For(plans[e], Research()); var gc = SwarmSortCode.For(plans[e], Game(plans));
            Console.WriteLine($"  {plans[e].Kind,-7} research {rc.W.Length,3} wells over {Enumerable.Range(0, 12).Count(t => rc.HasType[t])} types; game (one region) {gc.W.Length,3} wells over {Enumerable.Range(0, 12).Count(t => gc.HasType[t])} types");
            Check(gc.W.Length > 0 && gc.WStart[12] == gc.W.Length, $"{plans[e].Kind}: a code exists");
        }

        Console.WriteLine("\nS1. research mode: each plan grows from the research seed (16) for 240 steps");
        for (int e = 0; e < 4; e++)
        {
            var c = ResearchSwarm(plans, e, 7);
            Run(c, 240);
            var m = Mix(c);
            Console.WriteLine($"  {plans[e].Kind,-7} n={Live(c),3}/{plans[e].N} mix=[{string.Join(",", m)}] vs [{string.Join(",", plans[e].Mix)}] plan={c.Plan.Kind}");
            Check(Live(c) >= 32, $"{plans[e].Kind}: grows past the research's 32-tadpole floor");
            Check(c.PlanIx == e, $"{plans[e].Kind}: grows its own plan");
        }

        Console.WriteLine("\nS2. game mode: growth, fed (one region, funded, oriented, animated wells, molts of 10 steps)");
        var grown = new SwarmSortCore[4];
        for (int e = 0; e < 4; e++)
        {
            var c = GameSwarm(plans, e, 11);
            var sw = Stopwatch.StartNew(); Run(c, 400); double ms = sw.Elapsed.TotalMilliseconds / 400;
            var m = Mix(c);
            Console.WriteLine($"  {plans[e].Kind,-7} n={Live(c),3}/{plans[e].N} mix=[{string.Join(",", m)}] vs [{string.Join(",", plans[e].Mix)}] plan={c.Plan.Kind} well err {WellError(c):F2} {ms:F3} ms/step");
            Check(Live(c) >= 0.85f * plans[e].N, $"{plans[e].Kind}: reaches >= 85% of the plan's headcount");
            Check(c.PlanIx == e && Major(m) == e, $"{plans[e].Kind}: keeps its own plan and majority");
            grown[e] = c;
        }

        Console.WriteLine("\nS3. funded laying: an empty stomach lays nothing; a fixed meal lays at most what it pays for");
        {
            var c = GameSwarm(plans, 1, 12, fed: false);
            Run(c, 200);
            int seeded = Math.Min(GameSeed, plans[1].N);
            Check(c.AliveCount == seeded, $"unfed sort swarm stays at its seed (n={c.AliveCount})");
            c.Stomach[1] = 10f * c.C.EggCost[1];
            Run(c, 300);
            Console.WriteLine($"  10 Mass eggs of food -> laid {c.AliveCount - seeded}");
            Check(c.AliveCount - seeded <= 10 && c.AliveCount - seeded >= 5, "a meal funds at most its own eggs (cross-element ones at CrossCost)");
            Check(c.Stomach.Sum() < c.C.EggCost[1] * c.C.CrossCost + 1e-3f, "and spends it");
        }

        Console.WriteLine("\nS4. selective killing morphs the body (stomach empty; kill the majority until another leads) - molting ALWAYS on");
        var morphed = new SwarmSortCore[4];
        for (int e = 0; e < 4; e++)
        {
            var c = grown[e];
            for (int q = 0; q < 4; q++) c.Stomach[q] = 0f;
            var cnt = Mix(c);
            int runner = Enumerable.Range(0, 4).Where(x => x != e).OrderByDescending(x => cnt[x]).First();
            int killed = 0, switches = 0, at = -1;
            for (int i = 0; i < c.Cap && Mix(c)[e] >= Mix(c)[runner]; i++)
                if (c.Active[i] && c.Hatched[i] && c.EffectiveElement(i) == e)
                {
                    c.Kill(i); killed++; Step(c);
                    foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) { switches++; at = killed; }
                    c.Events.Clear();
                }
            int surplus0 = -1;
            for (int t = 0; t < 400; t++)
            {
                Step(c);
                foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) { switches++; if (at < 0) at = killed; }
                c.Events.Clear();
                if (surplus0 < 0 && c.PlanIx != e) surplus0 = Surplus(c);
            }
            Console.WriteLine($"  {plans[e].Kind,-7} killed {killed,3} -> plan {c.Plan.Kind} ({switches} switch) n={c.AliveCount} mix=[{string.Join(",", Mix(c))}] vs plan [{string.Join(",", c.Plan.Mix)}]  surplus at commit {surplus0} -> {Surplus(c)}");
            Check(c.PlanIx == runner, $"{plans[e].Kind}: morphs to the runner-up's plan ({plans[runner].Kind})");
            Check(switches == 1, $"{plans[e].Kind}: one commit, no flip-back");
            morphed[e] = c;
        }

        Console.WriteLine("\nS4b. a FED swarm, a 4 s burst of kills: it morphs only because a wounded swarm holds its eggs");
        foreach (int hold in new[] { 0, 20 })
        {
            var p = Game(plans); p.KillLayHoldSteps = hold; p.LayMax = 16;
            var c = GameSwarm(plans, 3, 9, p: p); Run(c, 400);   // dragonfly, bottomless food
            int killed = 0, switches = 0;
            for (int t = 0; t < 300; t++)
            {
                if (t < 40) for (int q = 0, i = 0; i < c.Cap && q < 2; i++) if (c.Active[i] && c.Hatched[i] && c.EffectiveElement(i) == 3) { c.Kill(i); killed++; q++; }
                Step(c);
                foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) switches++;
                c.Events.Clear();
            }
            Console.WriteLine($"  hold {hold,2} steps: killed {killed} Time -> plan {c.Plan.Kind}, n={c.AliveCount}, {switches} switches");
            if (hold > 0) Check(switches == 1 && c.PlanIx != 3, "with the hold, the burst morphs it");
        }

        Console.WriteLine("\nS5. the LOSSLESS corrector: after a morph, surplus members MOLT into the new plan's deficits (an animation)");
        {
            // the grid model's finding 17, re-run here: dragonfly killed into a jellyfish, UNFED (so laying
            // cannot be what fixes it) - the old majority's surplus must re-form, not linger and not die
            var c = GameSwarm(plans, 3, 31); Run(c, 400);
            for (int q = 0; q < 4; q++) c.Stomach[q] = 0f;
            var cnt = Mix(c);
            for (int i = 0; i < c.Cap && Mix(c)[3] >= Mix(c)[2]; i++)
                if (c.Active[i] && c.Hatched[i] && c.EffectiveElement(i) == 3) { c.Kill(i); Step(c); c.Events.Clear(); }
            int began = 0, done = 0, n0 = c.AliveCount, s0 = -1, committedAt = -1;
            var moltStart = new Dictionary<int, int>(); var durations = new List<int>();
            for (int t = 0; t < 600; t++)
            {
                Step(c);
                foreach (var ev in c.Events)
                {
                    if (ev.Kind == SwarmEventKind.Switched) { committedAt = t; s0 = Surplus(c); }
                    if (ev.Kind == SwarmEventKind.MoltBegan) { began++; moltStart[ev.Index] = t; }
                    if (ev.Kind == SwarmEventKind.MoltDone) { done++; if (moltStart.TryGetValue(ev.Index, out int t0)) durations.Add(t - t0); }
                }
                c.Events.Clear();
            }
            Console.WriteLine($"  plan {c.Plan.Kind} (committed at step {committedAt}); surplus over the jellyfish's counts at commit {s0} -> {Surplus(c)}; n {n0} -> {c.AliveCount}; molts begun {began}, finished {done}, duration {(durations.Count > 0 ? durations.Min() : 0)}..{(durations.Count > 0 ? durations.Max() : 0)} steps");
            Check(c.PlanIx == 2, "it became a jellyfish");
            Check(began > 0 && done > 0, "surplus members molt");
            Check(s0 >= 5 && Surplus(c) <= s0 / 5, "the surplus is corrected (>= 80% gone) with no food at all");
            Check(c.AliveCount == n0, "and nobody died for it (headcount unchanged)");
            Check(durations.Count > 0 && durations.All(d => d >= c.C.MoltSteps - 1 && d <= c.C.MoltSteps + 1), $"a molt is an animation of MoltSteps ({c.C.MoltSteps}) steps, not a jump");
        }

        Console.WriteLine("\nS6. LOSSLESS: zero self-inflicted deaths while fed, across every run above (scorecard.py's count)");
        {
            // and one more, long and fed, with predators and a kill campaign
            var c = GameSwarm(plans, 1, 41); Run(c, 300);
            var ship = new SwarmPredator { C = c.Anchor - new Vector3(80, 0, 0), V = new Vector3(4, 0, 0), R = 4 };
            for (int t = 0; t < 1200; t++)
            {
                if (t % 30 == 0) for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { c.Kill(i); break; }
                Step(c, t < 60 ? new[] { ship } : null); c.Events.Clear(); ship.C += ship.V;
            }
            Console.WriteLine($"  self-inflicted deaths over every sort run in this harness: {_selfDeaths}; core Deaths counter {c.Deaths}");
            Check(_selfDeaths == 0 && c.Deaths == 0, "LOSSLESS: nothing died that nobody killed");
        }

        Console.WriteLine("\nS7. vessel reaction: a ship flies straight through the body (no kills)");
        foreach (bool react in new[] { true, false })
            foreach (int e in new[] { 1, 0, 3 })
            {
                var c = GameSwarm(plans, e, 3); Run(c, 400);
                if (!react) c.C.Sense = 1e-4f;
                var ship = new SwarmPredator { C = c.Anchor - new Vector3(120, 0, 0), V = new Vector3(6, 0, 0), R = 4 };
                var touched = new bool[c.Cap]; float maxThreat = 0;
                for (int t = 0; t < 45; t++)
                {
                    Step(c, new[] { ship }); c.Events.Clear(); ship.C += ship.V;
                    for (int i = 0; i < c.Cap; i++) if (c.Active[i] && Vector3.Distance(c.Pos[i], ship.C) < ship.R) touched[i] = true;
                    maxThreat = Math.Max(maxThreat, c.ThreatLevel);
                }
                float frac = touched.Count(x => x) / (float)Math.Max(1, c.AliveCount);
                Console.WriteLine($"  {(react ? "reacting" : "inert   ")} {plans[e].Kind,-7} touched {frac:P0}  peak threat {maxThreat:F2}");
                if (react && e == 0) Check(maxThreat > 0.2f, "the pufferfish's threat rises (it inflates)");
            }

        Console.WriteLine("\nS8. swimming: the body travels to its target, the wells travel with it, and it keeps its plan");
        {
            var c = GameSwarm(plans, 1, 5); Run(c, 400);
            float still = WellError(c);
            var start = c.Anchor; c.SwimTarget = start + new Vector3(0, 0, 150);
            float worst = 0;
            for (int t = 0; t < 1500; t++) { Step(c); c.Events.Clear(); if (t > 200 && t < 900) worst = Math.Max(worst, WellError(c)); }
            float left = Vector3.Distance(c.Anchor, c.SwimTarget);
            Console.WriteLine($"  travelled {Vector3.Distance(start, c.Anchor):F0} of 150, {left:F1} left, plan {c.Plan.Kind}, n={Live(c)}; well error at rest {still:F2}, worst while swimming and turning {worst:F2}");
            Check(left < 25f, "arrives near its swim target");
            Check(c.PlanIx == 1 && Live(c) >= 0.85f * plans[1].N, "still a whale on arrival");
            Check(worst < still + 3f, "members stay on their wells while the body swims and turns (the code rides the body)");
        }

        Console.WriteLine("\nS8b. hovering on its goal, the body holds its heading (it must not chase its own jitter)");
        for (int e = 0; e < 4; e++)
        {
            var c = GameSwarm(plans, e, 6); Run(c, 400);
            var prev = c.Heading; double turned = 0;
            for (int t = 0; t < 600; t++)
            {
                Step(c); c.Events.Clear();
                turned += Math.Acos(Math.Clamp(Vector3.Dot(prev, c.Heading), -1f, 1f)); prev = c.Heading;
            }
            Console.WriteLine($"  {plans[e].Kind,-7} heading turned {turned * 180 / Math.PI:F1} deg over 600 hovering steps");
            Check(turned * 180 / Math.PI < 5, $"{plans[e].Kind}: a hovering body does not spin (< 5 deg / minute)");
        }

        Console.WriteLine("\nS9. band: the body's centre is held in its radial band");
        {
            var p = Game(plans); p.BandInner = 150; p.BandOuter = 250;
            var c = GameSwarm(plans, 1, 7, p: p);
            c.SwimTarget = Vector3.Zero;
            float minR = 1e9f, maxR = 0;
            for (int t = 0; t < 800; t++) { Step(c); c.Events.Clear(); if (t > 50) { float r = c.Anchor.Length(); minR = Math.Min(minR, r); maxR = Math.Max(maxR, r); } }
            Console.WriteLine($"  centre radius stayed in [{minR:F1}, {maxR:F1}]");
            Check(minR >= 140f && maxR <= 260f, "centre within the band (a soft clamp: 10 voxels of give)");
        }

        Console.WriteLine("\nS10. cost per step, grown bodies (game mode, CoreCLR, Release)");
        for (int e = 0; e < 4; e++)
        {
            var c2 = GameSwarm(plans, e, 21); Run(c2, 400);
            var sw = Stopwatch.StartNew(); Run(c2, 300); double ms = sw.Elapsed.TotalMilliseconds / 300;
            Console.WriteLine($"  {plans[e].Kind,-7} n={Live(c2),3}  {ms:F3} ms/step");
        }

        Console.WriteLine($"\nsort core: {(_fail == 0 ? "OK" : $"FAIL ({_fail})")}");
        return _fail;
    }

    // ─────────────────────────────────────────────────────────────────────── export

    /// <summary>One run in the export's format (GridHarness.Export's): every member's decoded state
    /// after <paramref name="steps"/>, then a <paramref name="win"/>-step feel window.
    /// Modes: "sortresearch" (sort_model as the research runs it) and "sortgame" (what ships).</summary>
    public static void ExportRun(StringBuilder sb, ref bool first, SwarmPlanData[] plans, string mode, int e, int seed, int steps, int win)
    {
        var c = mode == "sortresearch" ? ResearchSwarm(plans, e, seed) : GameSwarm(plans, e, seed);
        _selfDeaths = 0;
        var sw = Stopwatch.StartNew();
        Run(c, steps);
        double ms = sw.Elapsed.TotalMilliseconds / steps;
        // cost at the grown size, timed alone (scorecard.py's protocol)
        var c2 = mode == "sortresearch" ? ResearchSwarm(plans, e, seed) : GameSwarm(plans, e, seed);
        Run(c2, steps);
        sw.Restart(); Run(c2, 32); double msGrown = sw.Elapsed.TotalMilliseconds / 32;
        if (!first) sb.Append(','); first = false;
        sb.Append($"{{\"mode\":\"{mode}\",\"kind\":\"{Kinds[e]}\",\"seed\":{seed},\"ms\":{F(msGrown)},\"msGrow\":{F(ms)},\"selfDeaths\":{_selfDeaths},\"plan\":\"{c.Plan.Kind}\",\"units\":[");
        var centre = Vector3.Zero; int nl = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { centre += c.Pos[i]; nl++; }
        centre /= Math.Max(1, nl);
        var look = new float[11];
        bool u1 = true;
        for (int i = 0; i < c.Cap; i++)
        {
            if (!c.Active[i]) continue;
            var p = c.InvRotate(c.Pos[i] - centre);
            c.LookState(i, look);
            var vals = new List<float> { p.X, p.Y, p.Z, c.EffectiveElement(i), c.Dom[i], c.Hatched[i] ? 1 : 0, c.Hatched[i] ? 1f : 0.2f, -12f };
            vals.AddRange(look);
            if (!u1) sb.Append(','); u1 = false;
            sb.Append('[').Append(string.Join(",", vals.Select(F))).Append(']');
        }
        sb.Append("],\"win\":[");
        var alive = Enumerable.Range(0, c.Cap).Where(i => c.Active[i] && c.Hatched[i]).ToList();
        var el = alive.ToDictionary(i => i, i => c.Elem[i]);
        var order = alive.ToList();
        var frames = new List<Vector3[]>();
        for (int t = 0; t <= win; t++)
        {
            frames.Add(order.Select(i => c.Pos[i]).ToArray());
            if (t == win) break;
            Step(c); c.Events.Clear();
            alive.RemoveAll(i => !(c.Active[i] && c.Hatched[i] && c.Elem[i] == el[i] && c.Molt[i] <= 0f));
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
        Console.WriteLine($"  exported {mode,-12} {Kinds[e],-6} seed {seed}: n={Live(c)} plan={c.Plan.Kind} {msGrown:F3} ms/step grown, self-deaths {_selfDeaths}");
    }

    static string F(float x) => x.ToString("G6", CultureInfo.InvariantCulture);
    static string F(double x) => x.ToString("G6", CultureInfo.InvariantCulture);
}
