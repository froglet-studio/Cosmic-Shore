// Headless proof of Tandava's swarm (Assets/_Scripts/Controller/Arcade/TANDAVA.md §8): the SHIPPED sort core in its
// scripted-plan mode (SwarmSortParams.Scripted) walking the Tandava forms, and the SHIPPED stage director
// (TandavaDirectorCore) racing that swarm down the route, up through the ascension, and out as the final form.
//
//   bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans dir>
//
// T1-T5 are the core: a scripted seed holds its form, the director walks Serpent S -> M -> L -> Bull -> Lord of the
// Dance -> Winged Lion through the ordinary commit with ZERO self-inflicted deaths and nothing lost, a killed majority
// never re-plans a scripted swarm, bad requests are ignored, and the majority mode is untouched. T6-T13 race the route
// with a simplified FOOD model that keeps the one thing SwarmFauna.Feed's arithmetic depends on - GEOMETRY: each tick
// BitersPerStep members are asked round-robin, and a member bites only if it is within BiteRadius of a plant that still
// has food (a plant is a ball of PlantRadius around its heart; the oasis holds PlantsAt plants of PrismsPerPlant prisms,
// each prism one bite of BiteVolume). So the intake is BitersPerStep x the share of the body touching a plant, exactly
// the shape of the real one. The swarm's own METABOLISM is SwarmFauna's rule (unfed StarvationSeconds while hungry ->
// shed one member every ShedIntervalSeconds, the core picking who), and the RING OF FIRE is the glue's: twelve flames
// placed off the dance plan's baked ring and the swarm's body axes at the moment the ascension begins, each guarded
// while enough Time members (the attendant packs) are near its guard post. What it is not: the game's bites are prism
// queries against real Borromean plants of a real (budget-capped) shape, and its pilots are people threading rings, so
// the race TIMES below are a model, not a measurement - QA-TANDAVA-1 and QA-TANDAVA-6 measure them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

/// <summary>The route author_tandava_assets.py lays (ROUTE there): start, oases and their plant counts, the dance
/// ground, the exit plane.</summary>
static class TandavaRoute
{
    public const float StartX = -2000f, ExitX = 2000f, DanceX = 1075f, DanceZ = 400f;   // the dance ground is beside the course
    public static readonly float[] OasisX = { -1650, -1250, -900, -550, 550, 900, 1250, 1650 };   // outside the nucleus (r ~392)
    public static readonly int[] PlantsAt = { 2, 2, 2, 2, 3, 3, 3, 3 };
}

static class TandavaHarness
{
    static int _fail;
    static readonly string[] Forms = { "serpent_s", "serpent_m", "serpent_l", "bull", "dancer", "lion" };
    const int BullIx = 3, DancerIx = 4, LionIx = 5;

    // the game's numbers (author_tandava_assets.py authors the same into the Tandava swarm config + settings)
    const float UnitScale = 2f, TickHz = 10f;
    const int Density = 3;                    // TandavaSwarmFaunaConfig PlanDensity (the design's budget note)
    const int SeedMembers = 48;               // x Density
    static readonly float[] EggVolume = { 20.45f, 40.31f, 22.18f, 12.8f };
    const float StomachCapacity = 240f * Density * (20.45f + 40.31f + 22.18f + 12.8f) * 0.25f;   // SwarmFauna.StomachCapacity
    const float SurplusFactor = 0.4f;         // the design: "plan count reached plus 40% of that again in banked volume"
    const float AscensionSurplus = 1.0f;      // the Bull's offering: a whole body's worth of Mass banked (stomach-capped)
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
    // the metabolism (TandavaSwarmFaunaConfig: SwarmFauna.Starving's dials)
    const float StarvationSeconds = 30f, ShedIntervalSeconds = 0.25f, ForageBelow = 0.5f;
    // the ascension (TandavaSettingsSO, authored from the dance plan's baked ring)
    const float DrumSeconds = 30f, GuardRadius = 40f;
    const int FlameCount = 12, FlamesToBreak = 9, GuardMembers = 6, GuardElement = 3;   // Time: the attendants
    // TandavaSwarmFaunaConfig SortWellsPerType: the dance and the lion are STROKE figures (limbs, hair, feathers) and
    // twelve Gaussian wells per element blur a stroke into a blob - 24 lifts both (statue 64-72% -> 70-73%, lion
    // 73% -> 77%) and leaves the blob forms where they were (96-99%)
    const int SortWellsPerType = 24;
    // TandavaSwarmFaunaConfig SortWellDead: no flat-bottomed wells. The sortfeel's 0.7-sigma dead zone lets a tissue
    // fill its well "as a liquid" - right for a blob, wrong for a stroke, which it widens into one (the design's own
    // note for the statue). At 0 the statue holds 70-77% over the drum's end (57-68% at 0.7) and the blob forms keep
    // their 97-99%; the noise and wander stay (zeroing them as well bought nothing on the statue)
    const float SortWellDead = 0f;
    // the bar a stroke figure is held to: calibrated on the harness's own picture (TANDAVA_DUMP) of the 72% statue,
    // which reads cleanly - crown, hair, drum, the raised leg, the pedestal, the four packs on patrol
    const float StrokeCoverage = 0.65f;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static SwarmPlanData[] LoadTandava(string dir) =>
        Forms.Select(k => JsonSerializer.Deserialize<SwarmPlanJson>(
            File.ReadAllText(Path.Combine(dir, $"SwarmPlan_tandava_{k}.json")), new JsonSerializerOptions { IncludeFields = true })
            .ToPlanData().Upsample(Density, SwarmPlanData.DefaultUpsampleRadius)).ToArray();

    /// <summary>The ring of fire the dance plan bakes ("ring"), in WORLD units relative to the swarm's anchor and body
    /// axes (x along BX, y along BY, z along BZ): the density upsample scales positions by cbrt(Density).</summary>
    sealed class Ring { public Vector3 Centre; public float Radius, GuardOrbit; }

    static Ring LoadRing(string dir)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "SwarmPlan_tandava_dancer.json")));
        var r = doc.RootElement.GetProperty("ring");
        var c = r.GetProperty("centre");
        float k = MathF.Pow(Density, 1f / 3f) * UnitScale;
        return new Ring
        {
            Centre = new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle()) * k,
            Radius = r.GetProperty("radius").GetSingle() * k, GuardOrbit = r.GetProperty("guardOrbit").GetSingle() * k,
        };
    }

    static SwarmSortParams Params(SwarmPlanData[] plans)
    {
        var p = SortHarness.Game(plans);
        p.Scripted = true;
        p.PlanPeriods = plans.Select(x => x.FrameSteps).ToArray();   // TandavaSwarmFaunaConfig ScriptedPlanPeriods
        p.K = SortWellsPerType;
        p.WellDead = SortWellDead;
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

    /// <summary>Shape coverage: the share of the plan's units (body frame, at the pose the core is steering to this step -
    /// the plan's frames interpolated exactly as SwarmSortCore.Step does) with a same-element member within
    /// <paramref name="reach"/> voxels - "every part of the animal is there, where it should be NOW". An animated form
    /// (the attendants' patrol, the lion's wings) measured against its nearest keyframe would read every limb mid-swing as
    /// missing.</summary>
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
        int per = c.C.PlanPeriods != null && c.PlanIx < c.C.PlanPeriods.Length && c.C.PlanPeriods[c.PlanIx] > 0 ? c.C.PlanPeriods[c.PlanIx] : 8;
        int L = plan.Order.Length, slot = (int)(c.Clock / per);
        int fA = plan.Order[slot % L], fB = plan.Order[(slot + 1) % L]; float fa = (c.Clock % per) / (float)per;
        var pt = new Vector3[n]; var fc = Vector3.Zero;
        for (int u = 0; u < n; u++) { pt[u] = plan.P[fA][u] + fa * (plan.P[fB][u] - plan.P[fA][u]); fc += pt[u]; }
        fc /= n;
        int hit = 0;
        for (int u = 0; u < n; u++)
        {
            var pu = pt[u] - fc; int eu = plan.Elem[u];
            foreach (var (p, e) in live) if (e == eu && Vector3.DistanceSquared(p, pu) <= reach * reach) { hit++; break; }
        }
        return hit / (float)n;
    }

    /// <summary>TANDAVA_DUMP=dir: the live body and the pose it is steering to, in body coordinates, for a picture.</summary>
    static void Dump(SwarmSortCore c, string name)
    {
        var dir = Environment.GetEnvironmentVariable("TANDAVA_DUMP");
        if (string.IsNullOrEmpty(dir)) return;
        var plan = c.Plan; int n = plan.N;
        Vector3 cen = Vector3.Zero; int k = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { cen += c.Pos[i]; k++; }
        cen /= Math.Max(1, k);
        int per = c.C.PlanPeriods[c.PlanIx], L = plan.Order.Length, slot = (int)(c.Clock / per);
        int fA = plan.Order[slot % L], fB = plan.Order[(slot + 1) % L]; float fa = (c.Clock % per) / (float)per;
        var fc = Vector3.Zero; var pt = new Vector3[n];
        for (int u = 0; u < n; u++) { pt[u] = plan.P[fA][u] + fa * (plan.P[fB][u] - plan.P[fA][u]); fc += pt[u]; }
        fc /= n;
        var sb = new System.Text.StringBuilder("{\"plan\":[");
        for (int u = 0; u < n; u++) { var q = pt[u] - fc; sb.Append(FormattableString.Invariant($"{(u > 0 ? "," : "")}[{q.X:F2},{q.Y:F2},{q.Z:F2},{plan.Elem[u]}]")); }
        sb.Append("],\"live\":[");
        bool first = true;
        for (int i = 0; i < c.Cap; i++)
            if (c.Active[i] && c.Hatched[i])
            {
                var d = c.Pos[i] - cen;
                sb.Append(FormattableString.Invariant($"{(first ? "" : ",")}[{Vector3.Dot(d, c.BX):F2},{Vector3.Dot(d, c.BY):F2},{Vector3.Dot(d, c.BZ):F2},{c.EffectiveElement(i)}]"));
                first = false;
            }
        sb.Append(FormattableString.Invariant($"],\"coverage\":{Coverage(c, 3.2f):F3}}}"));
        File.WriteAllText(Path.Combine(dir, name + ".json"), sb.ToString());
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
        var ring = LoadRing(dir);
        Console.WriteLine($"tandava: forms {string.Join(" / ", plans.Select(p => $"{p.Kind} {p.N}"))} at density {Density}; " +
                          $"ring of fire r {ring.Radius:F0} (guard posts r {ring.GuardOrbit:F0}) world units");

        // ── T1: a scripted seed hatches as the form it was seeded with, and holds it
        Console.WriteLine("T1 scripted seed");
        {
            var c = Make(plans, new Vector3(-900, 0, 0), 7, fed: true);
            Check(c.PlanIx == 0, $"seeded as form 0 ({c.Plan.Kind}), census majority {Array.IndexOf(Mix(c), Mix(c).Max())}");
            int t = GrowTo(c, 0.85f, 1200);
            Check(t >= 0 && c.PlanIx == 0, $"a fed serpent_s grows to 85% of its plan in {t} steps and keeps its form ({Live(c)}/{c.Plan.N})");
        }

        // ── T2: the director's walk S -> M -> L -> Bull -> Dance -> Lion through the ordinary commit; nothing lost, nobody dies
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
                float bar = k >= DancerIx ? StrokeCoverage : 0.7f;
                Check(cov >= bar, $"{plans[k].Kind}: at least {bar:P0} of the plan's units have a member of their element within 3.2 voxels");
                prevLive = Active(c);
                if (k == BullIx)
                {
                    int[] mb = Mix(c);
                    Check(mb[1] > mb[0] && mb[1] > mb[2] && mb[1] > mb[3], $"the bull is Mass-majority (C/M/S/T {string.Join("/", mb)})");
                }
            }
            Check(_selfDeaths == 0, $"zero self-inflicted deaths across the walk, the ascension included ({_selfDeaths})");
            Check(monotone, "the body never shrank through a commit (lossless: molts, not deaths)");
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

        // ── T6-T13: the race
        Console.WriteLine("T6 the unopposed race");
        var won = Race(plans, ring, 7, new RaceOpts { Tag = "unopposed" }, out var r6);
        Check(r6.Outcome == TandavaOutcome.Escaped && r6.FormIx == LionIx,
            $"unopposed: escapes as the final form ({plans[r6.FormIx].Kind}) after {r6.Clock:F0} s");
        Check(won.AscensionAt > 0f && won.Commits.Count == 4,
            $"through every form: commits at {string.Join(", ", won.Commits.Select(x => $"{x:F0}s"))}, the Bull rises into the dance at " +
            $"{won.RoseAt:F0} s and the ring lights at {won.AscensionAt:F0} s");
        Check(r6.Clock >= 150f && r6.Clock <= 360f, $"the unopposed race lasts 2.5-6 minutes ({r6.Clock:F0} s)");
        Check(won.Commits.Take(3).Zip(won.Commits.Skip(1).Take(2), (a, b) => b - a).All(g => g >= 20f),
            $"its eating forms are spread down the route, at least 20 s apart");
        Check(won.Stops >= 3, $"it stopped to feed at {won.Stops} oases (feeding means stopping)");
        Check(won.LionAt > 0f && won.LionAt - won.AscensionAt >= DrumSeconds - 0.5f,
            $"the final form is taken only when the drum ends ({won.LionAt - won.AscensionAt:F0} s after the ascension began)");

        Console.WriteLine("T7 starvation");
        var denied = Race(plans, ring, 7, new RaceOpts { Deny = _ => true }, out var r7);
        Check(r7.Outcome == TandavaOutcome.Starved, $"every oasis denied: it never evolves and STARVES ({r7.Outcome} as {plans[r7.FormIx].Kind} at {r7.Clock:F0} s, {denied.Sheds} shed)");
        Check(denied.Stops == 0, "a denied oasis is skipped, never fed at");
        var burnt = Race(plans, ring, 7, new RaceOpts { DenyAllWhen = d => d.FormIx == BullIx }, out var r7b);
        Check(r7b.Outcome == TandavaOutcome.Starved && r7b.FormIx == BullIx,
            $"all food burnt once it is the Bull: it cannot ascend and cannot leave - {r7b.Outcome} as {plans[r7b.FormIx].Kind} at {r7b.Clock:F0} s " +
            $"({burnt.Sheds} shed on the way; the membrane clock, not the body, says it has starved)");

        Console.WriteLine("T8 two oases denied");
        Race(plans, ring, 7, new RaceOpts { Deny = o => o == 1 || o == 2 }, out var r8);
        Check(r8.Outcome != TandavaOutcome.Escaped || r8.Clock > r6.Clock,
            $"denying two oases costs the swarm ({r8.Outcome} as {plans[r8.FormIx].Kind} at {r8.Clock:F0} s vs {r6.Clock:F0} s unopposed)");

        Console.WriteLine("T9 culling wins");
        Race(plans, ring, 7, new RaceOpts { Cull = (core, d) => d.Clock > 20f ? 1f : 0f }, out var r9);
        Check(r9.Outcome == TandavaOutcome.Wiped, $"a lobby that kills every member wins (outcome {r9.Outcome} at {r9.Clock:F0} s)");
        Race(plans, ring, 7, new RaceOpts { Cull = (core, d) => d.IsFinalForm ? 0.7f : 0f }, out var r9b);
        Check(r9b.Outcome == TandavaOutcome.Broken, $"cutting the final form below its break threshold wins (outcome {r9b.Outcome})");

        Console.WriteLine("T10 the sealed exit");
        {
            var fs = BuildForms(plans);
            var d = new TandavaDirectorCore(fs, Array.Empty<TandavaOasis>(), Settings());
            var st = new TandavaSwarmState { Alive = fs[0].PlanCount, Anchor = new Vector3(TandavaRoute.ExitX + 300f, 0, 0) };
            d.Tick(0.1f, st);
            Check(d.Outcome == TandavaOutcome.Running && d.Events.Any(e => e.Kind == TandavaEventKind.Sealed),
                $"a serpent PAST the exit plane has not escaped (outcome {d.Outcome}) and the membrane raised Sealed");
            var push = d.SealCorrection(st.Anchor);
            Check(Vector3.Dot(st.Anchor + push - d.S.ExitPoint, d.S.ExitNormal) <= -d.S.SealHold + 1e-3f,
                $"and its correction puts the anchor back {d.S.SealHold:F0} u inside ({push.X:F0} u)");
            Check(TandavaDirectorCore.SealCorrection(st.Anchor, finalForm: true, d.S) == Vector3.Zero, "the final form is never held");
        }
        var rogue = Race(plans, ring, 7, new RaceOpts { Rogue = true, MaxSeconds = 150f }, out var r10);
        Check(r10.Outcome != TandavaOutcome.Escaped && rogue.SealedBeats > 0 && rogue.MaxOutward <= -r10.S.SealHold + 1f,
            $"a swarm swimming straight at the exit for 150 s never crosses it as {plans[r10.FormIx].Kind} " +
            $"({rogue.SealedBeats} sealed beats, anchor at most {rogue.MaxOutward:F0} u from the plane)");

        Console.WriteLine("T11 the ascension");
        Check(won.DanceCovLate >= StrokeCoverage, $"the Lord of the Dance is assembled by the drum's end: shape coverage {won.DanceCovLate:P0} " +
              $"over its last 5 s (at 10 s {won.DanceCov10:P0}, 20 s {won.DanceCov20:P0}, the last instant {won.DanceCoverage:P0})");
        Check(won.AxisDrift <= 25f, $"the figure holds still inside its ring: the body axes drift {won.AxisDrift:F1} deg over the drum");
        Check(won.StatueInRing >= 0.9f && won.AttendantsOut >= 0.5f,
            $"the figure stands inside its ring of fire ({won.StatueInRing:P0} of the non-Time body) and the attendants patrol outside it " +
            $"({won.AttendantsOut:P0} of the Time members; the dwarf and the palm are Time too, and inside)");
        Check(won.GuardedShare >= 0.1f && won.GuardedShare <= 0.6f,
            $"the attendants guard part of the ring, never all of it: {won.GuardedShare:P0} of flame-seconds guarded");
        Check(won.FlamesEverGuarded >= 8, $"the guard PATROLS: {won.FlamesEverGuarded} of {FlameCount} flames were guarded at some point");
        Check(won.LionCoverage >= StrokeCoverage, $"the Winged Lion is assembled before it crosses: shape coverage {won.LionCoverage:P0}");

        Console.WriteLine("T12 breaking the dance");
        _selfDeaths = 0;
        var broke = Race(plans, ring, 7, new RaceOpts { FlameEvery = 1.5f, MoltBackSteps = 200 }, out var r12);
        Check(r12.Outcome == TandavaOutcome.DanceBroken && r12.FlamesOut == FlamesToBreak,
            $"a lobby threading an unguarded flame every 1.5 s breaks the dance ({r12.Outcome}, {r12.FlamesOut} out, " +
            $"{broke.FlameTries - broke.Repelled} of {broke.FlameTries} tries landed, at {r12.Clock:F0} s)");
        Check(broke.DeathsAfterBreak == 0 && broke.AliveAfterBreak >= broke.AliveAtBreak,
            $"the broken dance kills nobody: molted back to the {plans[BullIx].Kind} in 20 s with {broke.AliveAfterBreak}/{broke.AliveAtBreak} alive");
        Check(_selfDeaths == 0, $"and the core killed nobody across the whole race ({_selfDeaths})");
        var blind = Race(plans, ring, 7, new RaceOpts { FlameEvery = 1.5f, Blind = true }, out var r12b);
        Check(blind.Repelled > 0 && r12b.FlamesOut < r12.FlamesOut + 1 && (r12b.Outcome != TandavaOutcome.DanceBroken || r12b.Clock > r12.Clock),
            $"pilots who charge the attendants are repelled: {blind.Repelled} of {blind.FlameTries} tries hit a guarded flame, " +
            $"{r12b.FlamesOut} out ({r12b.Outcome} at {r12b.Clock:F0} s vs {r12.Clock:F0} s for the lobby that reads the patrol)");

        Console.WriteLine("T13 the drum wins a slow lobby");
        var slow = Race(plans, ring, 7, new RaceOpts { FlameEvery = 5f }, out var r13);
        Check(slow.LionAt > 0f && r13.FlamesOut < FlamesToBreak,
            $"one flame per 5 s puts out {r13.FlamesOut} before the drum stops; the swarm takes the {plans[LionIx].Kind} ({r13.Outcome} at {r13.Clock:F0} s)");

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "tandava: OK" : $"tandava: {_fail} FAILED");
        return _fail;
    }

    sealed class RaceOpts
    {
        /// <summary>Oases denied from the start.</summary>
        public Func<int, bool> Deny = _ => false;
        /// <summary>Burn every oasis the first tick this holds.</summary>
        public Func<TandavaDirectorCore, bool> DenyAllWhen;
        /// <summary>The share of the live body the pilots kill this second (0 = none).</summary>
        public Func<SwarmSortCore, TandavaDirectorCore, float> Cull;
        /// <summary>The swarm swims straight at the exit, whatever the director says (the seal must hold anyway).</summary>
        public bool Rogue;
        /// <summary>During the dance the pilots thread one lit flame this often (seconds; 0 = never)...</summary>
        public float FlameEvery;
        /// <summary>...charging a GUARDED flame whenever one is lit (the lobby that ignores the attendants), rather than
        /// choosing among the unguarded ones.</summary>
        public bool Blind;
        /// <summary>After a broken dance: steps to run on with the last eating form requested back (the glue's molt back).</summary>
        public int MoltBackSteps;
        public float MaxSeconds = 20f * 60f;
        /// <summary>Names the TANDAVA_DUMP pictures of this race (null: none).</summary>
        public string Tag;
    }

    sealed class RaceLog
    {
        public readonly List<float> Commits = new();
        public int Stops, Sheds, SealedBeats, FlameTries, Repelled, FlamesEverGuarded;
        public float RoseAt = -1f, AscensionAt = -1f, LionAt = -1f, MaxOutward = float.NegativeInfinity;
        public float DanceCoverage, DanceCov10, DanceCov20, DanceCovLate, LionCoverage, AxisDrift, StatueInRing, AttendantsOut, GuardedShare;
        public int AliveAtBreak, AliveAfterBreak, DeathsAfterBreak;
    }

    static TandavaDirectorSettings Settings() => new()
    {
        ExitPoint = new Vector3(TandavaRoute.ExitX, 0, 0), ExitNormal = Vector3.UnitX, MaxFeedSeconds = MaxFeedSeconds,
        DancePoint = new Vector3(TandavaRoute.DanceX, 0, TandavaRoute.DanceZ), DrumSeconds = DrumSeconds, FlameCount = FlameCount, FlamesToBreak = FlamesToBreak,
    };

    /// <summary>The forms as the generator authors them (author_tandava_assets.py forms(), the same arithmetic): the bank
    /// is the design's surplus of the form's own body - and the last EATING form's (the Bull's) is the ascension's, a
    /// whole body's worth; a STAGE is the NEW food the form must eat to evolve - grow from the body it arrived with (the
    /// previous form's evolve fill, or the seed) to its own evolve fill at its own mix's egg prices, plus its bank, less
    /// the previous form's bank (which the commit spends on exactly that growth) - and a meal is the stage over
    /// MealsPerForm, so a denied oasis is a meal the stage is short. The dance and final forms do not eat on the route:
    /// no bank, the minimum meal.</summary>
    internal static List<TandavaForm> BuildForms(SwarmPlanData[] plans)
    {
        var forms = new List<TandavaForm>();
        int lastEater = plans.Length >= 3 ? plans.Length - 3 : plans.Length - 1;
        float arrived = SeedMembers * Density, banked = 0f;
        for (int k = 0; k < plans.Length; k++)
        {
            var f = new TandavaForm { Name = plans[k].Kind, PlanIndex = k, PlanCount = plans[k].N };
            if (k > lastEater) { f.MealVolume = 1f; forms.Add(f); continue; }
            float avgEgg = 0f;
            for (int e = 0; e < 4; e++) avgEgg += plans[k].Mix[e] * EggVolume[e];
            avgEgg /= plans[k].N;
            float fillTo = f.FillToEvolve * plans[k].N;
            float surplus = k < lastEater ? SurplusFactor : AscensionSurplus;
            f.BankToEvolve[1] = MathF.Min(surplus * plans[k].N * EggVolume[1], 0.9f * StomachCapacity);
            float stage = MathF.Max(0f, MathF.Max(0f, fillTo - arrived) * avgEgg + f.BankToEvolve[1] - banked);
            f.MealVolume = MathF.Max(1f, stage / MealsPerForm);   // TandavaFormSpec.MealVolume is [Min(1)]
            arrived = fillTo; banked = f.BankToEvolve[1];
            forms.Add(f);
        }
        return forms;
    }

    /// <summary>One race on the route with the simplified food model, the swarm's own metabolism and the glue's ring of
    /// fire (see the file header).</summary>
    static RaceLog Race(SwarmPlanData[] plans, Ring ring, int seed, RaceOpts o, out TandavaDirectorCore dir)
    {
        float[] oasisX = TandavaRoute.OasisX;
        var oases = oasisX.Select(x => new TandavaOasis { Centre = new Vector3(x, 0, 0), Radius = 100f }).ToArray();
        // plants: PlantsAt[o] per oasis, scattered (seeded) inside it; each a ball of PlantRadius with its own store
        var prng = new Random(seed * 31 + 7);
        var plants = new List<(int oasis, Vector3 at)>();
        for (int q = 0; q < oases.Length; q++)
            for (int k = 0; k < TandavaRoute.PlantsAt[q]; k++)
            {
                double a = prng.NextDouble() * 2 * Math.PI, r = PlantSpread * Math.Sqrt(prng.NextDouble());
                plants.Add((q, oases[q].Centre + new Vector3((float)(r * Math.Cos(a)), (float)((prng.NextDouble() - 0.5) * 40), (float)(r * Math.Sin(a)))));
            }
        var plantStore = plants.Select(_ => PrismsPerPlant * BiteVolume).ToArray();
        var forms = BuildForms(plans);
        var s = Settings();
        dir = new TandavaDirectorCore(forms, oases, s);   // the director holds THIS array: a denial below is seen by both
        for (int q = 0; q < oases.Length; q++) dir.SetDenied(q, o.Deny(q));

        var c = new SwarmSortCore(plans, Params(plans), seed);
        c.Seed(0, SeedMembers * Density, new Vector3(TandavaRoute.StartX, 0, 0) / UnitScale, Vector3.UnitX);
        var log = new RaceLog();
        float sinceBite = 0f, sinceFed = 0f, eaten = 0f, lastShed = -1e9f, dt = 1f / TickHz, nextTry = 0f;
        int biteCursor = 0;
        bool burnt = false;
        var rng = new Random(seed);
        // the ring of fire, placed once when the ascension begins
        Vector3[] post = null; Vector3 ringC = default, by0 = default; int guardedSamples = 0, flameSamples = 0; var everGuarded = new bool[FlameCount];

        int GuardCount(int k)
        {
            int n = 0; float r2 = GuardRadius * GuardRadius;
            for (int i = 0; i < c.Cap; i++)
                if (c.Active[i] && c.Hatched[i] && c.EffectiveElement(i) == GuardElement && Vector3.DistanceSquared(c.Pos[i] * UnitScale, post[k]) <= r2) n++;
            return n;
        }
        bool Guarded(int k) => GuardCount(k) >= GuardMembers;

        int step = 0;
        for (; step < o.MaxSeconds * TickHz && dir.Outcome == TandavaOutcome.Running; step++)
        {
            float now = step * dt;
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
            float fill = (c.Stomach[0] + c.Stomach[1] + c.Stomach[2] + c.Stomach[3]) / StomachCapacity;
            sinceFed = bit || fill >= 1f ? 0f : sinceFed + dt;   // SwarmFauna._lastFedTime: a bite, or sated
            // the glue's denial: an oasis with nothing left to eat is denied (TandavaController asks SwarmFauna.CanEat)
            for (int q = 0; q < oases.Length; q++)
                if (!oases[q].Denied && plants.Select((p, j) => (p, j)).Where(x => x.p.oasis == q).All(x => plantStore[x.j] <= 0f)) dir.SetDenied(q, true);
            if (!burnt && o.DenyAllWhen != null && o.DenyAllWhen(dir))
            {
                burnt = true;
                for (int q = 0; q < oases.Length; q++) dir.SetDenied(q, true);
            }
            // the metabolism (SwarmFauna.Starving): unfed and hungry sheds one member per interval, the core choosing who
            bool starving = sinceFed >= StarvationSeconds && fill < ForageBelow;
            if (starving && now - lastShed >= ShedIntervalSeconds)
            {
                lastShed = now;
                int v = c.StarvationVictim();
                if (v >= 0) { c.Kill(v); log.Sheds++; }
            }
            // pilots: culling
            if (o.Cull != null && step % 10 == 0)
            {
                float share = o.Cull(c, dir);
                if (share > 0f)
                {
                    int n = (int)MathF.Ceiling(share * Live(c));
                    for (int i = 0; i < c.Cap && n > 0; i++) if (c.Active[i] && c.Hatched[i]) { c.Kill(i); n--; }
                }
            }
            // pilots: the ring of fire
            if (dir.InDance && post != null)
            {
                if (step % 10 == 0)
                    for (int k = 0; k < FlameCount; k++)
                    {
                        if (dir.FlameIsOut(k)) continue;
                        flameSamples++;
                        if (Guarded(k)) { guardedSamples++; everGuarded[k] = true; }
                    }
                if (o.FlameEvery > 0f && now >= nextTry)
                {
                    nextTry = now + o.FlameEvery;
                    var d = dir;   // an out parameter cannot be captured
                    var lit = Enumerable.Range(0, FlameCount).Where(k => !d.FlameIsOut(k) && (o.Blind ? Guarded(k) : !Guarded(k))).ToList();
                    if (o.Blind && lit.Count == 0) lit = Enumerable.Range(0, FlameCount).Where(k => !d.FlameIsOut(k)).ToList();
                    if (lit.Count > 0)
                    {
                        int k = lit[rng.Next(lit.Count)];
                        log.FlameTries++;
                        if (Guarded(k)) log.Repelled++;
                        else dir.BreakFlame(k);
                    }
                }
            }
            var st = new TandavaSwarmState
            {
                Alive = Live(c), Anchor = c.Anchor * UnitScale,
                Stomach0 = c.Stomach[0], Stomach1 = c.Stomach[1], Stomach2 = c.Stomach[2], Stomach3 = c.Stomach[3],
                StomachFill = fill, SinceBite = sinceBite, EatenTotal = eaten, Starving = starving,
            };
            dir.Tick(dt, st);
            foreach (var e in dir.Events)
            {
                if (e.Kind == TandavaEventKind.FormCommitted)
                {
                    if (e.A == DancerIx)
                    {
                        log.DanceCoverage = Coverage(c, 3.2f);
                        float cosD = Math.Clamp(Vector3.Dot(Vector3.Normalize(by0), Vector3.Normalize(c.BY)), -1f, 1f);
                        log.AxisDrift = MathF.Acos(cosD) * 180f / MathF.PI;
                        int inside = 0, statue = 0, outside = 0, guards = 0;
                        for (int i = 0; i < c.Cap; i++)
                            if (c.Active[i] && c.Hatched[i])
                            {
                                var d = c.Pos[i] * UnitScale - ringC;
                                float along = Vector3.Dot(d, c.BX);
                                bool inRing = (d - c.BX * along).Length() <= ring.Radius;
                                if (c.EffectiveElement(i) == GuardElement) { guards++; if (!inRing) outside++; }
                                else { statue++; if (inRing) inside++; }
                            }
                        log.StatueInRing = statue > 0 ? inside / (float)statue : 0f;
                        log.AttendantsOut = guards > 0 ? outside / (float)guards : 0f;
                        if (o.Tag != null) Dump(c, o.Tag + "_dancer");
                        log.LionAt = dir.Clock;
                    }
                    c.RequestPlan(forms[e.B].PlanIndex); log.Commits.Add(dir.Clock);
                }
                if (e.Kind == TandavaEventKind.ReadyToDance) { c.RequestPlan(forms[e.B].PlanIndex); log.RoseAt = dir.Clock; }
                if (e.Kind == TandavaEventKind.AscensionBegun)
                {
                    log.AscensionAt = dir.Clock;
                    // TandavaController's placement: the dance ground, the plan's ring offset in the body axes of the moment
                    ringC = s.DancePoint + c.BX * ring.Centre.X + c.BY * ring.Centre.Y + c.BZ * ring.Centre.Z;
                    by0 = c.BY;
                    post = new Vector3[FlameCount];
                    for (int k = 0; k < FlameCount; k++)
                    {
                        float th = 2f * MathF.PI * k / FlameCount;
                        post[k] = ringC + (c.BY * MathF.Cos(th) + c.BZ * MathF.Sin(th)) * ring.GuardOrbit;
                    }
                }
                if (e.Kind == TandavaEventKind.OasisReached) log.Stops++;
                if (e.Kind == TandavaEventKind.Sealed) log.SealedBeats++;
            }
            dir.Events.Clear();
            if (dir.InDance && log.AscensionAt >= 0f)
            {
                float into = dir.Clock - log.AscensionAt;
                if (log.DanceCov10 == 0f && into >= 10f) log.DanceCov10 = Coverage(c, 3.2f);
                if (log.DanceCov20 == 0f && into >= 20f) log.DanceCov20 = Coverage(c, 3.2f);
                // the figure as it stands over the drum's last 5 s: one instant reads the packs mid-swing between frames
                if (into >= DrumSeconds - 5f && step % 10 == 0) log.DanceCovLate = MathF.Max(log.DanceCovLate, Coverage(c, 3.2f));
            }
            if (dir.IsFinalForm && step % 10 == 0)
            {
                float lc = Coverage(c, 3.2f);
                if (lc > log.LionCoverage && o.Tag != null) Dump(c, o.Tag + "_lion");
                log.LionCoverage = MathF.Max(log.LionCoverage, lc);
            }
            c.SwimTarget = (o.Rogue && !dir.IsFinalForm ? s.ExitPoint + s.ExitNormal * 400f : dir.Goal) / UnitScale;
            Step(c);
            c.Events.Clear();
            // the sealed exit, as every peer applies it to its own swarm (TandavaController -> SwarmFauna.TryNudge)
            var hold = dir.SealCorrection(c.Anchor * UnitScale);
            if (hold != Vector3.Zero) c.Translate(hold / UnitScale);
            if (!dir.IsFinalForm) log.MaxOutward = MathF.Max(log.MaxOutward, Vector3.Dot(c.Anchor * UnitScale - s.ExitPoint, s.ExitNormal));
        }
        log.FlamesEverGuarded = everGuarded.Count(x => x);
        log.GuardedShare = flameSamples > 0 ? guardedSamples / (float)flameSamples : 0f;
        if (dir.Outcome == TandavaOutcome.DanceBroken && o.MoltBackSteps > 0)
        {
            // the glue on a broken dance: the figure falls back into the last beast it ate its way to - a molt, never a kill
            log.AliveAtBreak = Live(c);
            int before = _selfDeaths;
            c.RequestPlan(forms[dir.LastEaterIx].PlanIndex);
            for (int q = 0; q < o.MoltBackSteps; q++) { Step(c); c.Events.Clear(); }
            log.DeathsAfterBreak = _selfDeaths - before;
            log.AliveAfterBreak = Live(c);
        }
        Console.WriteLine($"    race: {dir.Outcome} as {plans[dir.FormIx].Kind} at {dir.Clock:F0} s ({dir.Phase}), {log.Stops} stops, " +
                          $"oasis {dir.OasisIx}/{oases.Length}, {Live(c)} alive, {log.Sheds} shed, flames out {dir.FlamesOut}, " +
                          $"food left by oasis {string.Join("/", Enumerable.Range(0, oases.Length).Select(q => (int)plants.Select((p, j) => p.oasis == q ? plantStore[j] : 0f).Sum()))}");
        return log;
    }
}
