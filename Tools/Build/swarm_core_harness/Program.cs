// Headless proof of the SHIPPED swarm sim cores (Assets/.../Swarm/SwarmFieldCore.cs here; the grid and
// sort cores in GridHarness.cs and SortHarness.cs).
//   dotnet run -c Release -- <plans dir>
// Each test prints a line and the run exits non-zero if any assertion fails. What this does NOT
// prove: anything about Unity (rendering, prisms, crystals, colliders) - see Docs/SWARM_FAUNA.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

static class Program
{
    static int _fail;
    static readonly string[] Names = { "Charge", "Mass", "Space", "Time" };
    static readonly string[] Kinds = { "charge", "mass", "space", "time" };   // indexed by research element

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static SwarmPlanData[] LoadPlans(string dir)
    {
        var o = new JsonSerializerOptions { IncludeFields = true };
        return Kinds.Select(k => JsonSerializer.Deserialize<SwarmPlanJson>(
            File.ReadAllText(Path.Combine(dir, $"SwarmPlan_{k}.json")), o).ToPlanData()).ToArray();
    }

    static SwarmFieldCore Make(SwarmPlanData[] plans, int seedPlan, int seed = 1, bool fed = true)
    {
        var p = new SwarmFieldParams { Membrane = 600f, Cap = plans.Max(x => x.N) };
        var c = new SwarmFieldCore(plans, p, seed);
        c.Seed(seedPlan, 24, new Vector3(200, 0, 0), Vector3.UnitX);
        c.SwimTarget = c.Anchor;
        if (fed) for (int e = 0; e < 4; e++) c.Stomach[e] = 1e6f;
        return c;
    }

    static float ShapeError(SwarmFieldCore c, bool skipRunners = false)
    {
        // mean distance from each tadpole to its home slot in the current body frame (sim units)
        var plan = c.Plan; var sp = new Vector3[plan.N]; var sv = new Vector3[plan.N]; var sf = new Vector3[plan.N];
        plan.At(c.T, sp, sv, sf);
        // MEDIAN, because the dragonfly's Time runners lap the wings faster than their own top
        // speed (by the research's design) and always trail their slot - a mean reads that as a
        // broken body when it is the plan's own motion.
        var d = new List<float>();
        for (int i = 0; i < c.Cap; i++)
            if (c.Alive[i] && c.Home[i] >= 0 && !(skipRunners && c.Elem[i] == 3)) d.Add(Vector3.Distance(c.Pos[i], c.Rotate(sp[c.Home[i]]) + c.Anchor));
        if (d.Count == 0) return float.NaN;
        d.Sort(); return d[d.Count / 2];
    }

    static void Run(SwarmFieldCore c, int steps, SwarmPredator[] preds = null)
    {
        preds ??= Array.Empty<SwarmPredator>();
        for (int t = 0; t < steps; t++) { c.Step(preds); c.Events.Clear(); }
    }

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "export") return GridHarness.Export(args, LoadPlans);
        bool gridOnly = args.Length > 1 && args[1] == "grid", sortOnly = args.Length > 1 && args[1] == "sort";
        var plans = LoadPlans(args.Length > 0 ? args[0] : "../../../Assets/_SO_Assets/Swarm Fauna/Plans");
        if (gridOnly) return GridHarness.Run(plans) == 0 ? 0 : 1;
        if (sortOnly) return SortHarness.Run(plans) == 0 ? 0 : 1;
        Console.WriteLine("plans: " + string.Join(", ", plans.Select(p => $"{p.Kind} N={p.N} mix=[{string.Join(",", p.Mix)}] R={p.Radius:F1}")));

        Console.WriteLine("\n1. growth to the plan, fed (each plan seeded at its own majority, 24 tadpoles)");
        var grown = new SwarmFieldCore[4];
        for (int e = 0; e < 4; e++)
        {
            var c = Make(plans, e);
            var sw = Stopwatch.StartNew(); Run(c, 600); double ms = sw.Elapsed.TotalMilliseconds / 600;
            var cnt = c.Counts(false);
            Console.WriteLine($"  {plans[e].Kind,-7} n={c.AliveCount,3}/{plans[e].N} mix=[{string.Join(",", cnt)}] plan={plans[c.PlanIx].Kind} shapeErr={ShapeError(c):F2} {ms:F3} ms/step");
            Check(c.AliveCount == plans[e].N, $"{plans[e].Kind}: headcount reaches the plan's {plans[e].N}");
            Check(cnt.SequenceEqual(plans[e].Mix), $"{plans[e].Kind}: element mix equals the plan's");
            Check(c.PlanIx == e, $"{plans[e].Kind}: never leaves its own plan");
            // Time runners lap the dragonfly's wings at ~3 voxels/step against a 2.0 top speed (the
            // research's own numbers), so they trail by design; the frame they lap is what must hold.
            Check(ShapeError(c, skipRunners: e == 3) < 3f, $"{plans[e].Kind}: body holds its shape (median home error < 3 voxels{(e == 3 ? ", runners excluded" : "")})");
            grown[e] = c;
        }

        Console.WriteLine("\n2. funded laying: an empty stomach lays nothing; a fixed meal lays exactly what it pays for");
        {
            var c = Make(plans, 1, fed: false);
            Run(c, 200);
            Check(c.AliveCount == 24, $"starved swarm stays at its seed (n={c.AliveCount})");
            c.Stomach[1] = 10f * c.C.EggCost[1];
            Run(c, 300);
            int laid = c.AliveCount - 24;
            Console.WriteLine($"  10 Mass eggs of food -> laid {laid}, stomach left [{string.Join(",", c.Stomach.Select(x => x.ToString("F1")))}]");
            Check(laid == 10, "own-element food funds exactly one egg per EggCost");
            var d = Make(plans, 1, fed: false);
            d.Stomach[1] = 0; d.Stomach[2] = 10f * d.C.EggCost[1];   // Space food only, Mass body
            Run(d, 400);
            var cnt = d.Counts(false);
            Console.WriteLine($"  10 eggs of SPACE food in a whale -> laid {d.AliveCount - 24}, mix=[{string.Join(",", cnt)}]");
            Check(d.AliveCount - 24 >= 5 && d.AliveCount - 24 <= 10, "cross-element food funds at CrossCost (fewer eggs)");
        }

        Console.WriteLine("\n3. selective killing morphs the body (a fed-out swarm: kill its majority, one per step, until another element leads)");
        for (int e = 0; e < 4; e++)
        {
            var c = grown[e];
            // The swarm has digested its meal (a grown body stops eating once its stomach is full and
            // spends it on regrowth) - with a bottomless stomach it re-lays faster than one ship kills,
            // which is the research's homeostasis finding and the reason the game funds laying.
            for (int q = 0; q < 4; q++) c.Stomach[q] = 0f;
            var cnt = c.Counts(false);
            // a player kills the majority one at a time until it is one behind the runner-up
            int runner = Enumerable.Range(0, 4).Where(x => x != e).OrderByDescending(x => cnt[x]).First();
            int killed = 0;
            for (int i = 0; i < c.Cap && c.Counts(false)[e] >= c.Counts(false)[runner]; i++)
                if (c.Alive[i] && c.Elem[i] == e) { c.Kill(i); killed++; c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear(); }
            int switches = 0; int switchedAt = -1;
            for (int t = 0; t < 400; t++)
            {
                c.Step(Array.Empty<SwarmPredator>());
                foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) { switches++; if (switchedAt < 0) switchedAt = t; }
                c.Events.Clear();
            }
            var after = c.Counts(false);
            Console.WriteLine($"  {plans[e].Kind,-7} killed {killed,3} {Names[e]} -> plan {plans[c.PlanIx].Kind} (switch +{switchedAt} steps after the last kill, {switches} switch) n={c.AliveCount} mix=[{string.Join(",", after)}] vs [{string.Join(",", plans[c.PlanIx].Mix)}] shapeErr={ShapeError(c):F2}");
            Check(c.PlanIx == runner, $"{plans[e].Kind}: morphs to the runner-up's plan ({plans[runner].Kind})");
            Check(switches == 1, $"{plans[e].Kind}: exactly one commit, no flip-back");
        }

        Console.WriteLine("\n3b. homeostasis: the SAME kills against a swarm sitting on its own food do not convert it");
        {
            var c = Make(plans, 3, seed: 9); Run(c, 600);   // dragonfly, fed (bottomless Time food)
            int killed = 0, switches = 0;
            for (int t = 0; t < 200; t++)
            {
                if (t % 2 == 0) for (int i = 0; i < c.Cap; i++) if (c.Alive[i] && c.Elem[i] == 3) { c.Kill(i); killed++; break; }
                c.Step(Array.Empty<SwarmPredator>());
                foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) switches++;
                c.Events.Clear();
            }
            Console.WriteLine($"  killed {killed} Time over 200 steps (one per 2 steps) -> plan {c.Plan.Kind}, n={c.AliveCount}, {switches} switches");
            Check(switches == 0, "a fed dragonfly out-lays one ship's kill rate (the feeding ground is the second lever)");
        }

        Console.WriteLine("\n3c. OVERTUNED laying (LayRate 0.2 / LayMax 16, bottomless food): a burst of kills morphs it ONLY because a wounded swarm holds its eggs");
        foreach (int hold in new[] { 0, 20 })
        {
            var c = Make(plans, 3, seed: 9); Run(c, 600);   // dragonfly, fed
            c.C.LayRate = 0.2f; c.C.LayMax = 16; c.C.KillLayHoldSteps = hold;
            Run(c, 100);
            int killed = 0, switches = 0;
            for (int t = 0; t < 300; t++)
            {
                if (t < 40) for (int q = 0, i = 0; i < c.Cap && q < 2; i++) if (c.Alive[i] && c.Elem[i] == 3) { c.Kill(i); killed++; q++; }
                c.Step(Array.Empty<SwarmPredator>());
                foreach (var ev in c.Events) if (ev.Kind == SwarmEventKind.Switched) switches++;
                c.Events.Clear();
            }
            Console.WriteLine($"  hold {hold,2} steps: killed {killed} Time in a 4 s burst -> plan {c.Plan.Kind}, n={c.AliveCount}, {switches} switches");
            if (hold == 0) Check(switches == 0, "without the hold, an overtuned fed swarm out-lays a burst (the regression the hold fixes)");
            else Check(switches == 1 && c.Plan.Kind != plans[3].Kind, "with the hold, the same burst morphs it");
        }

        Console.WriteLine("\n4. vessel reaction: a ship flies straight through the body (no kills)");
        foreach (bool react in new[] { true, false })
        {
            foreach (int e in new[] { 1, 0, 3 })
            {
                var c = Make(plans, e, seed: 3); Run(c, 600);
                if (!react) { c.C.Sense = 1e-4f; }
                var ship = new SwarmPredator { C = c.Anchor - new Vector3(120, 0, 0), V = new Vector3(6, 0, 0), R = 4 };
                var touched = new bool[c.Cap]; float maxThreat = 0;
                for (int t = 0; t < 45; t++)
                {
                    c.Step(new[] { ship }); c.Events.Clear(); ship.C += ship.V;
                    for (int i = 0; i < c.Cap; i++) if (c.Alive[i] && Vector3.Distance(c.Pos[i], ship.C) < ship.R) touched[i] = true;
                    maxThreat = Math.Max(maxThreat, c.ThreatLevel);
                }
                float frac = touched.Count(x => x) / (float)c.AliveCount;
                Console.WriteLine($"  {(react ? "reacting" : "inert   ")} {plans[e].Kind,-7} touched {frac:P0}  peak threat {maxThreat:F2}");
                if (react && e == 0) Check(maxThreat > 0.2f, "the pufferfish's threat rises (it inflates)");
            }
        }

        Console.WriteLine("\n5. a LOITERING ship is mobbed by the dragonfly's Time runners");
        {
            var c = Make(plans, 3, seed: 4); Run(c, 600);
            // the research probe (field_probes.loiter): parked 1.2 body radii off the centroid along the
            // body's long axis, ship radius 0.3 body radii, drifting at 0.3/step
            double near = 0;
            foreach (bool mob in new[] { true, false })
            {
                var c2 = Make(plans, 3, seed: 4); Run(c2, 600);
                if (!mob) c2.C.Mob[3] = 0f;
                var at = c2.Anchor + c2.BX * 1.2f * c2.Plan.Radius;
                double n2 = 0;
                for (int t = 0; t < 80; t++)
                {
                    var ship = new SwarmPredator { C = at + new Vector3(0, 0, 0.3f * MathF.Sin(t / 10f)), V = new Vector3(0, 0, 0.3f), R = 0.3f * c2.Plan.Radius };
                    c2.Step(new[] { ship }); c2.Events.Clear();
                    if (t >= 20) for (int i = 0; i < c2.Cap; i++) if (c2.Alive[i] && Vector3.Distance(c2.Pos[i], ship.C) < 2 * ship.R) n2++;
                }
                Console.WriteLine($"  mob {(mob ? "on " : "off")}: mean tadpoles within 2 ship radii {n2 / 60:F1}");
                if (mob) near = n2 / 60; else Check(near > 2 * n2 / 60, "mobbing at least doubles the crowd around a parked ship");
            }
        }

        Console.WriteLine("\n6. swimming: the body travels to its target and keeps its shape");
        {
            var c = Make(plans, 1, seed: 5); Run(c, 600);
            var start = c.Anchor; c.SwimTarget = start + new Vector3(0, 0, 150);
            float worst = 0;
            for (int t = 0; t < 1500; t++) { c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear(); if (t > 300) worst = Math.Max(worst, ShapeError(c)); }
            float left = Vector3.Distance(c.Anchor, c.SwimTarget);
            Console.WriteLine($"  travelled {Vector3.Distance(start, c.Anchor):F0} of 150, {left:F1} left, worst shape error while cruising {worst:F2}");
            Check(left < 25f, "arrives near its swim target");
            Check(worst < 6f, "shape holds while swimming (worst home error < 6 voxels)");
        }

        Console.WriteLine("\n6b. hovering on its goal, the body holds its heading (it must not chase its own jitter)");
        for (int e = 0; e < 4; e++)
        {
            var c = Make(plans, e, seed: 6); Run(c, 600);
            var prev = c.Heading; double turned = 0;
            for (int t = 0; t < 600; t++)
            {
                c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear();
                turned += Math.Acos(Math.Clamp(Vector3.Dot(prev, c.Heading), -1f, 1f)); prev = c.Heading;
            }
            Console.WriteLine($"  {plans[e].Kind,-7} heading turned {turned * 180 / Math.PI:F1} deg over 600 hovering steps");
            Check(turned * 180 / Math.PI < 5, $"{plans[e].Kind}: a hovering body does not spin (< 5 deg / minute)");
        }

        Console.WriteLine("\n7. band: the anchor never leaves its radial band");
        {
            var p = new SwarmFieldParams { Membrane = 600f, Cap = 192, BandInner = 150, BandOuter = 250 };
            var c = new SwarmFieldCore(plans, p, 7);
            c.Seed(1, 24, new Vector3(200, 0, 0), Vector3.UnitX);
            for (int e = 0; e < 4; e++) c.Stomach[e] = 1e6f;
            c.SwimTarget = new Vector3(0, 0, 0);   // tries to swim to the cell centre
            float minR = 1e9f, maxR = 0;
            for (int t = 0; t < 800; t++) { c.Step(Array.Empty<SwarmPredator>()); c.Events.Clear(); float r = c.Anchor.Length(); minR = Math.Min(minR, r); maxR = Math.Max(maxR, r); }
            Console.WriteLine($"  anchor radius stayed in [{minR:F1}, {maxR:F1}]");
            Check(minR >= 149.9f && maxR <= 250.1f, "anchor clamped to [150, 250]");
        }

        Console.WriteLine($"\nfield core: {(_fail == 0 ? "OK" : $"FAIL ({_fail})")}");
        _fail += GridHarness.Run(plans);
        _fail += SortHarness.Run(plans);
        Console.WriteLine($"\n{(_fail == 0 ? "OK" : $"FAIL ({_fail})")}");
        return _fail == 0 ? 0 : 1;
    }
}
