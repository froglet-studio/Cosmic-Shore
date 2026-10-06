// Headless proof of Tandava's swarm (Assets/_Scripts/Controller/Arcade/TANDAVA.md §8): the SHIPPED sort core in its
// scripted-plan mode (SwarmSortParams.Scripted) walking the Tandava forms, and the SHIPPED stage director
// (TandavaDirectorCore) racing that swarm down the route.
//
//   bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans dir>
//
// T1-T5 are the core: a scripted seed holds its form, the director walks Serpent S -> M -> L -> Bull through the
// ordinary commit with ZERO self-inflicted deaths and nothing lost, a killed majority never re-plans a scripted swarm,
// bad requests are ignored, and the majority mode is untouched. T6-T9 race the route with a simplified FOOD model that
// keeps the one thing SwarmFauna.Feed's arithmetic depends on - GEOMETRY: each tick BitersPerStep members are asked
// round-robin, and a member bites only if it is within BiteRadius of a plant that still has food (a plant is a ball of
// PlantRadius around its heart; the oasis holds PlantsAt plants of PrismsPerPlant prisms, each prism one bite of
// BiteVolume). So the intake is BitersPerStep x the share of the body touching a plant, exactly the shape of the real
// one. What it is not: the game's bites are prism queries against real Borromean plants of a real (budget-capped)
// shape, so the race TIMES below are a model, not a measurement - QA-TANDAVA-1 measures them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

/// <summary>The route author_tandava_assets.py lays (ROUTE there): start, oases and their plant counts, the exit plane.</summary>
static class TandavaRoute
{
    public const float StartX = -2000f, ExitX = 2000f;
    public static readonly float[] OasisX = { -1650, -1250, -900, -550, 550, 900, 1250, 1650 };   // outside the nucleus (r ~392)
    public static readonly int[] PlantsAt = { 2, 2, 2, 2, 3, 3, 3, 3 };
}

static class TandavaHarness
{
    static int _fail;
    static readonly string[] Forms = { "serpent_s", "serpent_m", "serpent_l", "bull" };

    // the game's numbers (author_tandava_assets.py authors the same into the Tandava swarm config + settings)
    const float UnitScale = 2f, TickHz = 10f;
    const int Density = 3;                    // TandavaSwarmFaunaConfig PlanDensity (the design's budget note)
    const int SeedMembers = 48;               // x Density
    static readonly float[] EggVolume = { 20.45f, 40.31f, 22.18f, 12.8f };
    const float StomachCapacity = 240f * Density * (20.45f + 40.31f + 22.18f + 12.8f) * 0.25f;   // SwarmFauna.StomachCapacity
    const float SurplusFactor = 0.4f;         // the design: "plan count reached plus 40% of that again in banked volume"
    // the tuning dials (author_tandava_assets.py authors the same), overridable for a sweep: TANDAVA_CRUISE,
    // TANDAVA_MAX_FEED. The 2026-10-06 sweep: cruise 2.0 / 2.5 / 3.0 all hold T6-T9; 2.5 x interval-2 grazing failed T6
    // (stops hit the cap, the unopposed swarm stalls a form short) - one biter EVERY tick is the shipped intake.
    static readonly float Cruise = Env("TANDAVA_CRUISE", 2.5f);          // voxels/step: 2.5 x UnitScale 2 x 10 Hz = 50 u/s
    const int BitersPerStep = 1;                                       // SwarmFaunaConfigSO.BitersPerStep
    static readonly float MaxFeedSeconds = Env("TANDAVA_MAX_FEED", 45f);
    static float Env(string k, float d) => float.TryParse(Environment.GetEnvironmentVariable(k), System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
    const float BiteVolume = 5.60606f * 4.67172f * 2.7822f;   // one Borromean Mass leaf prism (the shipped Variant.LeafSize)
    const int PrismsPerPlant = 60;                 // the Tandava flora configs' MaxTotalSpawnedObjectsOverride
    const float PlantRadius = 28f, BiteRadius = 10f, PlantSpread = 70f;   // world units
    const float MealsPerForm = 2.5f;               // the design: "the swarm needs 2 to 3 [oases] per stage"

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static SwarmPlanData[] LoadTandava(string dir) =>
        Forms.Select(k => JsonSerializer.Deserialize<SwarmPlanJson>(
            File.ReadAllText(Path.Combine(dir, $"SwarmPlan_tandava_{k}.json")), new JsonSerializerOptions { IncludeFields = true })
            .ToPlanData().Upsample(Density, SwarmPlanData.DefaultUpsampleRadius)).ToArray();

    static SwarmSortParams Params(SwarmPlanData[] plans)
    {
        var p = SortHarness.Game(plans);
        p.Scripted = true;
        p.Cap = plans.Max(x => x.N);
        p.LayMax = 5 * Density;
        p.ThreatGain = 3f * MathF.Pow(Density, 2f / 3f);
        p.Membrane = 3600f * 0.97f / UnitScale;
        p.Cruise = Cruise;
        for (int e = 0; e < 4; e++) p.EggCost[e] = EggVolume[e];
        return p;
    }

    static SwarmSortCore Make(SwarmPlanData[] plans, Vector3 at, int seed, bool fed)
    {
        var c = new SwarmSortCore(plans, Params(plans), seed);
        c.Seed(0, SeedMembers * Density, at, Vector3.UnitX);
        c.SwimTarget = c.Anchor;
        if (fed) for (int e = 0; e < 4; e++) c.Stomach[e] = 1e6f;
        return c;
    }

    static int Live(SwarmSortCore c) { int n = 0; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) n++; return n; }
    static int Active(SwarmSortCore c) { int n = 0; for (int i = 0; i < c.Cap; i++) if (c.Active[i]) n++; return n; }
    static int[] Mix(SwarmSortCore c) { var m = new int[4]; for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) m[c.EffectiveElement(i)]++; return m; }

    /// <summary>Shape coverage: the share of the plan's units (body frame, the frame that fits best) with a same-element
    /// member within <paramref name="reach"/> voxels - "every part of the animal is there".</summary>
    static float Coverage(SwarmSortCore c, float reach)
    {
        var plan = c.Plan; int n = plan.N;
        var live = new List<(Vector3 p, int e)>();
        Vector3 cen = Vector3.Zero; int k = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { cen += c.Pos[i]; k++; }
        if (k == 0) return 0f;
        cen /= k;
        for (int i = 0; i < c.Cap; i++)
            if (c.Active[i] && c.Hatched[i])
            {
                var d = c.Pos[i] - cen;
                live.Add((new Vector3(Vector3.Dot(d, c.BX), Vector3.Dot(d, c.BY), Vector3.Dot(d, c.BZ)), c.EffectiveElement(i)));
            }
        float best = 0f;
        for (int f = 0; f < plan.P.Length; f++)
        {
            var fc = Vector3.Zero; for (int u = 0; u < n; u++) fc += plan.P[f][u]; fc /= n;
            int hit = 0;
            for (int u = 0; u < n; u++)
            {
                var pu = plan.P[f][u] - fc; int eu = plan.Elem[u];
                foreach (var (p, e) in live) if (e == eu && Vector3.DistanceSquared(p, pu) <= reach * reach) { hit++; break; }
            }
            best = MathF.Max(best, hit / (float)n);
        }
        return best;
    }

    static int _selfDeaths;
    static void Step(SwarmSortCore c)
    {
        var before = (bool[])c.Active.Clone();
        c.Step(ReadOnlySpan<SwarmPredator>.Empty);
        for (int i = 0; i < c.Cap; i++) if (before[i] && !c.Active[i]) _selfDeaths++;
    }

    /// <summary>Steps until the body is at least <paramref name="share"/> of its plan (or the budget runs out).</summary>
    static int GrowTo(SwarmSortCore c, float share, int budget, List<SwarmEvent> seen = null)
    {
        for (int t = 0; t < budget; t++)
        {
            if (Live(c) >= share * c.Plan.N) return t;
            Step(c);
            if (seen != null) seen.AddRange(c.Events);
            c.Events.Clear();
        }
        return -1;
    }

    public static int Run(SwarmPlanData[] basePlans, string dir)
    {
        var plans = LoadTandava(dir);
        Console.WriteLine($"tandava: forms {string.Join(" / ", plans.Select(p => $"{p.Kind} {p.N}"))} at density {Density}");

        // ── T1: a scripted seed hatches as the form it was seeded with, and holds it
        Console.WriteLine("T1 scripted seed");
        {
            var c = Make(plans, new Vector3(-900, 0, 0), 7, fed: true);
            Check(c.PlanIx == 0, $"seeded as form 0 ({c.Plan.Kind}), census majority {Array.IndexOf(Mix(c), Mix(c).Max())}");
            int t = GrowTo(c, 0.85f, 1200);
            Check(t >= 0 && c.PlanIx == 0, $"a fed serpent_s grows to 85% of its plan in {t} steps and keeps its form ({Live(c)}/{c.Plan.N})");
        }

        // ── T2: the director's walk S -> M -> L -> Bull through the ordinary commit; nothing lost, nobody dies
        Console.WriteLine("T2 the walk");
        {
            _selfDeaths = 0;
            var c = Make(plans, new Vector3(-900, 0, 0), 23, fed: true);
            GrowTo(c, 0.85f, 1200);
            int prevLive = Live(c);
            bool monotone = true;
            for (int k = 1; k < plans.Length; k++)
            {
                c.RequestPlan(k);
                var seen = new List<SwarmEvent>();
                Step(c); seen.AddRange(c.Events); c.Events.Clear();
                bool switched = seen.Any(e => e.Kind == SwarmEventKind.Switched && e.Index == k - 1 && e.Other == k);
                Check(c.PlanIx == k && switched, $"request {plans[k].Kind}: committed next step with Switched {k - 1} -> {k}");
                int t = GrowTo(c, 0.85f, 2400, seen);
                for (int q = 0; q < 300; q++) { Step(c); seen.AddRange(c.Events); c.Events.Clear(); }   // 30 s to re-sort
                monotone &= Active(c) >= prevLive;
                int molts = seen.Count(e => e.Kind == SwarmEventKind.MoltBegan);
                float cov = Coverage(c, 3.2f);
                Check(t >= 0, $"{plans[k].Kind}: grows to 85% in {t} steps, then 30 s to re-sort ({Live(c)}/{plans[k].N}, {molts} molts), shape coverage {cov:P0}");
                Check(cov >= 0.7f, $"{plans[k].Kind}: at least 70% of the plan's units have a member of their element within 3.2 voxels");
                prevLive = Active(c);
            }
            Check(_selfDeaths == 0, $"zero self-inflicted deaths across the walk ({_selfDeaths})");
            Check(monotone, "the body never shrank through a commit (lossless: molts, not deaths)");
            int[] m = Mix(c);
            Check(m[1] > m[0] && m[1] > m[2] && m[1] > m[3], $"the bull is Mass-majority after the walk (C/M/S/T {string.Join("/", m)})");
        }

        // ── T3: killing the majority never re-plans a scripted swarm (the census does not choose the form)
        Console.WriteLine("T3 kills never re-plan");
        {
            var c = Make(plans, new Vector3(-900, 0, 0), 41, fed: true);
            GrowTo(c, 0.85f, 1200);
            int killed = 0;
            for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i] && c.EffectiveElement(i) == 1 && killed < (int)(0.9f * Mix(c)[1])) { c.Kill(i); killed++; }
            var seen = new List<SwarmEvent>();
            for (int t = 0; t < 120; t++) { Step(c); seen.AddRange(c.Events); c.Events.Clear(); }
            Check(c.PlanIx == 0 && !seen.Any(e => e.Kind == SwarmEventKind.Switched), $"90% of the Mass killed ({killed}): still {c.Plan.Kind}, no Switched in 12 s");
        }

        // ── T4: bad requests and the majority mode
        Console.WriteLine("T4 requests");
        {
            var c = Make(plans, new Vector3(-900, 0, 0), 101, fed: true);
            c.RequestPlan(-1); c.RequestPlan(99); Step(c); c.Events.Clear();
            Check(c.PlanIx == 0, "a request outside the form list is ignored");
            var m = SortHarness.GameSwarm(basePlans, 1, 7);
            m.RequestPlan(2); SortHarness.Step(m); m.Events.Clear();
            Check(m.PlanIx == 1, "the majority mode ignores RequestPlan (every shipped swarm)");
        }

        // ── T5: the shipped Swarm cell is untouched - a Scripted=false core still morphs by majority (S4's shape)
        Console.WriteLine("T5 majority mode still morphs");
        {
            var m = SortHarness.GameSwarm(basePlans, 3, 23);
            SortHarness.Run(m, 600);
            int killed = 0, want = Mix(m)[3];
            for (int i = 0; i < m.Cap && killed < want; i++) if (m.Active[i] && m.Hatched[i] && m.EffectiveElement(i) == 3) { m.Kill(i); killed++; }
            var seen = new List<SwarmEvent>();
            for (int t = 0; t < 60; t++) { SortHarness.Step(m); seen.AddRange(m.Events); m.Events.Clear(); }
            Check(seen.Any(e => e.Kind == SwarmEventKind.Switched) && m.PlanIx != 3, $"a dragonfly with its Time killed morphs ({string.Join(", ", seen.Where(e => e.Kind == SwarmEventKind.Switched).Select(e => $"{e.Index}->{e.Other}"))})");
        }

        // ── T6-T9: the race
        Console.WriteLine("T6 the unopposed race");
        var won = Race(plans, seed: 7, deny: _ => false, cull: null, out var r6);
        Check(r6.Outcome == TandavaOutcome.Escaped && r6.FormIx == plans.Length - 1,
            $"unopposed: escapes as the final form ({plans[r6.FormIx].Kind}) after {r6.Clock:F0} s, forms committed at {string.Join(", ", won.Commits.Select(x => $"{x:F0}s"))}");
        Check(r6.Clock >= 120f && r6.Clock <= 300f, $"the unopposed race lasts 2-5 minutes ({r6.Clock:F0} s)");
        Check(won.Commits.Count == plans.Length - 1 && won.Commits.Zip(won.Commits.Skip(1), (a, b) => b - a).All(g => g >= 20f),
            $"its forms are spread down the route, at least 20 s apart ({string.Join(", ", won.Commits.Select(x => $"{x:F0}s"))})");
        Check(won.Stops >= 3, $"it stopped to feed at {won.Stops} oases (feeding means stopping)");

        Console.WriteLine("T7 every oasis denied");
        var denied = Race(plans, seed: 7, deny: _ => true, cull: null, out var r7);
        Check(r7.Outcome == TandavaOutcome.Escaped && r7.FormIx == 0, $"denied: it never evolves and escapes as {plans[r7.FormIx].Kind} (the weakest loss) after {r7.Clock:F0} s");
        Check(denied.Stops == 0, "a denied oasis is skipped, never fed at");

        Console.WriteLine("T8 two oases denied");
        Race(plans, seed: 7, deny: o => o == 1 || o == 2, cull: null, out var r8);
        Check(r8.FormIx < r6.FormIx || r8.Clock > r6.Clock || r8.Outcome != TandavaOutcome.Escaped,
            $"denying two oases costs the swarm (form {plans[r8.FormIx].Kind} vs {plans[r6.FormIx].Kind}, {r8.Clock:F0} s vs {r6.Clock:F0} s)");

        Console.WriteLine("T9 culling wins");
        Race(plans, seed: 7, deny: _ => false, cull: (core, dir) => dir.Clock > 20f ? 1f : 0f, out var r9);
        Check(r9.Outcome == TandavaOutcome.Wiped, $"a lobby that kills every member wins (outcome {r9.Outcome} at {r9.Clock:F0} s)");
        Race(plans, seed: 7, deny: _ => false, cull: (core, dir) => dir.IsFinalForm && dir.Clock > 0f ? 0.7f : 0f, out var r9b);
        Check(r9b.Outcome == TandavaOutcome.Broken, $"cutting the final form below its break threshold wins (outcome {r9b.Outcome})");

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "tandava: OK" : $"tandava: {_fail} FAILED");
        return _fail;
    }

    sealed class RaceLog { public readonly List<float> Commits = new(); public int Stops; }

    /// <summary>The forms as the generator authors them (author_tandava_assets.py forms(), the same arithmetic): the bank
    /// is the design's surplus of the form's own body; a STAGE is the NEW food the form must eat to evolve - grow from the
    /// body it arrived with (the previous form's evolve fill, or the seed) to its own evolve fill at its own mix's egg
    /// prices, plus its bank, less the previous form's bank (which the commit spends on exactly that growth) - and a meal
    /// is the stage over MealsPerForm, so a denied oasis is a meal the stage is short.</summary>
    internal static List<TandavaForm> BuildForms(SwarmPlanData[] plans)
    {
        var forms = new List<TandavaForm>();
        float arrived = SeedMembers * Density, banked = 0f;
        for (int k = 0; k < plans.Length; k++)
        {
            var f = new TandavaForm { Name = plans[k].Kind, PlanIndex = k, PlanCount = plans[k].N };
            float avgEgg = 0f;
            for (int e = 0; e < 4; e++) avgEgg += plans[k].Mix[e] * EggVolume[e];
            avgEgg /= plans[k].N;
            float fillTo = f.FillToEvolve * plans[k].N;
            if (k < plans.Length - 1) f.BankToEvolve[1] = MathF.Min(SurplusFactor * plans[k].N * EggVolume[1], 0.9f * StomachCapacity);
            float stage = MathF.Max(0f, MathF.Max(0f, fillTo - arrived) * avgEgg + f.BankToEvolve[1] - banked);
            f.MealVolume = MathF.Max(1f, stage / MealsPerForm);   // TandavaFormSpec.MealVolume is [Min(1)]
            arrived = fillTo; banked = f.BankToEvolve[1];
            forms.Add(f);
        }
        return forms;
    }

    /// <summary>One race on the route with the simplified food model (see the file header). <paramref name="cull"/>
    /// returns the share of the live body to kill this second (0 = none).</summary>
    static RaceLog Race(SwarmPlanData[] plans, int seed, Func<int, bool> deny, Func<SwarmSortCore, TandavaDirectorCore, float> cull,
                        out TandavaDirectorCore dir)
    {
        // the route: start at x = -2000, six oases, the exit membrane at x = +2000 (author_tandava_assets.py ROUTE)
        float[] oasisX = TandavaRoute.OasisX;
        var oases = oasisX.Select(x => new TandavaOasis { Centre = new Vector3(x, 0, 0), Radius = 100f }).ToArray();
        // plants: PlantsAt[o] per oasis, scattered (seeded) inside it; each a ball of PlantRadius with its own store
        var prng = new Random(seed * 31 + 7);
        var plants = new List<(int oasis, Vector3 at)>();
        for (int o = 0; o < oases.Length; o++)
            for (int k = 0; k < TandavaRoute.PlantsAt[o]; k++)
            {
                double a = prng.NextDouble() * 2 * Math.PI, r = PlantSpread * Math.Sqrt(prng.NextDouble());
                plants.Add((o, oases[o].Centre + new Vector3((float)(r * Math.Cos(a)), (float)((prng.NextDouble() - 0.5) * 40), (float)(r * Math.Sin(a)))));
            }
        var plantStore = plants.Select(_ => PrismsPerPlant * BiteVolume).ToArray();
        var forms = BuildForms(plans);
        var s = new TandavaDirectorSettings { ExitPoint = new Vector3(TandavaRoute.ExitX, 0, 0), ExitNormal = Vector3.UnitX, MaxFeedSeconds = MaxFeedSeconds };
        dir = new TandavaDirectorCore(forms, oases, s);
        for (int o = 0; o < oases.Length; o++) dir.SetDenied(o, deny(o));

        var c = new SwarmSortCore(plans, Params(plans), seed);
        c.Seed(0, SeedMembers * Density, new Vector3(TandavaRoute.StartX, 0, 0) / UnitScale, Vector3.UnitX);
        var log = new RaceLog();
        float sinceBite = 0f, eaten = 0f, dt = 1f / TickHz;
        int biteCursor = 0;
        var rng = new Random(seed);
        for (int step = 0; step < 20 * 60 * 10 && dir.Outcome == TandavaOutcome.Running; step++)
        {
            // feed (SwarmFauna.Feed's shape): BitersPerStep members asked round-robin; each bites only if it touches a
            // plant with food left in an undenied oasis. A full stomach does not graze.
            bool bit = false;
            float fill0 = (c.Stomach[0] + c.Stomach[1] + c.Stomach[2] + c.Stomach[3]) / StomachCapacity;
            if (fill0 < 1f)
                for (int b = 0; b < BitersPerStep; b++)
                {
                    int i = -1;
                    for (int tries = 0; tries < c.Cap; tries++)
                    {
                        biteCursor = (biteCursor + 1) % c.Cap;
                        if (c.Active[biteCursor] && c.Hatched[biteCursor]) { i = biteCursor; break; }
                    }
                    if (i < 0) break;
                    var at = c.Pos[i] * UnitScale;
                    for (int q = 0; q < plants.Count; q++)
                    {
                        if (plantStore[q] <= 0f || oases[plants[q].oasis].Denied) continue;
                        if (Vector3.DistanceSquared(at, plants[q].at) > (PlantRadius + BiteRadius) * (PlantRadius + BiteRadius)) continue;
                        float v = MathF.Min(plantStore[q], BiteVolume);
                        plantStore[q] -= v; c.Stomach[1] += v; eaten += v; bit = true;
                        break;
                    }
                }
            sinceBite = bit ? 0f : sinceBite + dt;
            // pilots
            if (cull != null && step % 10 == 0)
            {
                float share = cull(c, dir);
                if (share > 0f)
                {
                    int n = (int)MathF.Ceiling(share * Live(c));
                    for (int i = 0; i < c.Cap && n > 0; i++) if (c.Active[i] && c.Hatched[i]) { c.Kill(i); n--; }
                }
            }
            var st = new TandavaSwarmState
            {
                Alive = Live(c), Anchor = c.Anchor * UnitScale,
                Stomach0 = c.Stomach[0], Stomach1 = c.Stomach[1], Stomach2 = c.Stomach[2], Stomach3 = c.Stomach[3],
                StomachFill = (c.Stomach[0] + c.Stomach[1] + c.Stomach[2] + c.Stomach[3]) / StomachCapacity,
                SinceBite = sinceBite, EatenTotal = eaten,
            };
            dir.Tick(dt, st);
            foreach (var e in dir.Events)
            {
                if (e.Kind == TandavaEventKind.FormCommitted) { c.RequestPlan(forms[e.B].PlanIndex); log.Commits.Add(dir.Clock); }
                if (e.Kind == TandavaEventKind.OasisReached) log.Stops++;
            }
            dir.Events.Clear();
            c.SwimTarget = dir.Goal / UnitScale;
            c.Step(ReadOnlySpan<SwarmPredator>.Empty);
            c.Events.Clear();
        }
        Console.WriteLine($"    race: {dir.Outcome} as {plans[dir.FormIx].Kind} at {dir.Clock:F0} s, {log.Stops} stops, oasis {dir.OasisIx}/{oases.Length}, " +
                          $"{Live(c)} alive, food left by oasis {string.Join("/", Enumerable.Range(0, oases.Length).Select(o => (int)plants.Select((p, q) => p.oasis == o ? plantStore[q] : 0f).Sum()))}");
        return log;
    }
}
