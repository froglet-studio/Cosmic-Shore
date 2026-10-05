// THE SHOWCASE CELL, ALL TOGETHER (Docs/SWARM_FAUNA.md §26, QA-SWARM-ROUND11-9). Every shipped creature core of the
// Swarm cell in ONE world laid out from its authored assets (layout.json, written by layout.py), flora shared as food,
// three scripted pilots flying it: CARELESS (straight lines between random points), SKILLED (the same, steering clear of
// any armed striker), RAIDER (raids the thief hoard, then cuts through the fortress wall, repeat).
//
// Asserted, per run:
//   C1 engaged colliders (observed, every tick) <= the authored worst case <= the 1200 ceiling
//   C2 combined CPU: sum of every core's step cost at its tick rate, per 60 Hz frame, within the cell's budget
//   C3 the global mass ledger closes (every volume booked in a named account; residual ~ 0)
//   C4 no creature class goes extinct or above its cap over the long run
//   C5 burns per minute and telegraphed share per pilot under the cell's PetalBurnRule (reported; the skilled pilot
//      must burn less than the careless one, and most burns must be telegraphed)
//   + unit groups for the cross-system fixes this round (U1 substrate engagement per population, U2 plan density).
//
//   bash Tools/Build/showcase_cell_harness/run.sh            # unit + 3 seeds x 5 min + 1 x 30 min + the snapshot
//   bash Tools/Build/showcase_cell_harness/run.sh quick      # unit + 1 seed x 2 min
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

static class Program
{
    static int s_fail;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) s_fail++;
    }

    /// <summary>An ECOLOGY or GAME-FEEL outcome (a population's fate, a pilot's burns): reported, never tuned here
    /// (CLAUDE.md "don't cheat emergence"). It fails the run only under SHOWCASE_STRICT=1; the laws (colliders, CPU,
    /// ledger, caps, the LOD contract, the unit groups) always do.</summary>
    static void Finding(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FINDING")}] {what}");
        if (ok) return;
        s_findings++;
        if (Environment.GetEnvironmentVariable("SHOWCASE_STRICT") == "1") s_fail++;
    }
    static int s_findings;

    public static void WriteVec(Utf8JsonWriter w, string name, Vector3 v)
    {
        w.WriteStartArray(name);
        w.WriteNumberValue(MathF.Round(v.X, 1)); w.WriteNumberValue(MathF.Round(v.Y, 1)); w.WriteNumberValue(MathF.Round(v.Z, 1));
        w.WriteEndArray();
    }

    static readonly string[] Kinds = { "charge", "mass", "space", "time" };

    /// <summary>The plans as SwarmPlanLibrary.Load hands them to the swarm: baked JSON, upsampled to PlanDensity.</summary>
    static SwarmPlanData[] LoadPlans(string dir, int density)
    {
        var o = new JsonSerializerOptions { IncludeFields = true };
        return Kinds.Select(k => JsonSerializer.Deserialize<SwarmPlanJson>(File.ReadAllText(Path.Combine(dir, $"SwarmPlan_{k}.json")), o)!
            .ToPlanData().Upsample(Math.Max(1, density))).ToArray();
    }

    static int Main(string[] args)
    {
        var layout = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
        string plansDir = args[1];
        string mode = args.Length > 2 ? args[2] : "all";
        string snapOut = args.Length > 3 ? args[3] : "";
        int density = (int)layout.GetProperty("swarm_config").GetProperty("PlanDensity").GetDouble();
        var plans = LoadPlans(plansDir, density);
        var raw = LoadPlans(plansDir, 1);
        Console.WriteLine($"plans at density {density}: " + string.Join(", ", plans.Select((p, i) => $"{p.Kind} N={p.N} (baked {raw[i].N})")));

        Console.WriteLine("\nU1 substrate engagement is each population's own (SubstratePopulation.EngageRadius / MaxEngaged)");
        UnitEngagement();
        Console.WriteLine("\nU2 plan density: the swarm's Cap and seed count agree (SwarmPlanLibrary.Load upsamples)");
        UnitDensity(layout, plans, raw, density);
        Console.WriteLine("\nU3 substrate: a meal queued in the pass that kills its eater leaves with the body (SubstrateTickJob feeds before kills)");
        UnitLateMeal();
        Console.WriteLine("\nU4 builders: a death carries the stomach it died with (BuilderDeath.Stomach)");
        UnitDeathStomach(layout);
        Console.WriteLine("\nU5 substrate fields: a decaying signal never ends as subnormal float dust (SubstrateFields.Blur)");
        UnitDenormal();
        Console.WriteLine("\nU6 ecology LOD: a collapsed body its macro tick moves into a pilot's prefetch expands in that same tick (EcologyLodDirector.Tick)");
        UnitMacroDrift();
        Console.WriteLine("\nU7 builder ledgers are exact: a 30-min torpor in float stomachs books exactly what they lost (BuilderLedger)");
        UnitBuilderLedger();

        var runs = new List<RunResult>();
        if (mode == "quick") runs.Add(Run(layout, plans, density, 1, 120f, null));
        else if (mode.StartsWith("seed"))   // diagnostics: "seed7x10" = seed 7 for 10 minutes
        {
            var parts = mode.Substring(4).Split('x');
            runs.Add(Run(layout, plans, density, int.Parse(parts[0]), 60f * float.Parse(parts[1]), null));
        }
        else if (mode != "unit")
        {
            for (int seed = 1; seed <= 3; seed++) runs.Add(Run(layout, plans, density, seed, 300f, seed == 1 ? snapOut : null));
            runs.Add(Run(layout, plans, density, 7, 1800f, null));
        }
        if (runs.Count > 0) Summary(layout, runs);
        if (s_findings > 0) Console.WriteLine($"\n{s_findings} FINDING(S) - ecology / game-feel outcomes, reported (SHOWCASE_STRICT=1 makes them fail)");
        Console.WriteLine(s_fail == 0 ? "\nALL OK" : $"\n{s_fail} FAILED");
        return s_fail == 0 ? 0 : 1;
    }

    // ═════════════════════════════════════════════════════════════════════════════════════ unit groups

    /// <summary>U1: two populations in one core, the first with a short reach and a small cap. The second's agents must
    /// engage out to ITS radius and up to ITS cap. Negative control: with no per-population numbers (the pre-fix job), the
    /// second population is clipped to the first one's.</summary>
    static void UnitEngagement()
    {
        (int a, int b, float farB) Measure(bool perPop)
        {
            var core = new SubstrateCore(64, 1200f, 0.1f, 40, 3);
            var pa = SubstrateResearch.ByName("lurker", game: true).Clone(); pa.Capacity = 16;
            var pb = SubstrateResearch.ByName("pack", game: true).Clone(); pb.Capacity = 16;
            int qa = core.AddPopulation(pa, 1), qb = core.AddPopulation(pb, 3);
            if (perPop)
            {
                core.Pops[qa].EngageRadius = 100f; core.Pops[qa].MaxEngaged = 2;
                core.Pops[qb].EngageRadius = 300f; core.Pops[qb].MaxEngaged = 10;
            }
            var at = new Vector3[16];
            for (int k = 0; k < 16; k++) at[k] = new Vector3(30f + 15f * k, 0f, 0f);
            core.SeedAt(qa, at, 0f); core.SeedAt(qb, at, 0f);
            var job = new SubstrateTickJob(core, new SubstrateTickSettings { EngageRadius = 100f, MaxEngaged = 2 });
            job.Pilots[0] = new SubstratePilot { Pos = Vector3.Zero, Radius = 6f, Id = 0 };
            job.PilotCount = 1;
            job.Prime();
            job.Kick(true); job.Collect();
            float far = 0f;
            for (int q = 0; q < job.EngagedCount[qb]; q++) far = MathF.Max(far, job.Instances[job.Engaged[core.Pops[qb].Start + q]].CurPos.Length());
            return (job.EngagedCount[qa], job.EngagedCount[qb], far);
        }
        var fixedRun = Measure(true);
        var preFix = Measure(false);
        Console.WriteLine($"   per-population: first {fixedRun.a} engaged (cap 2), second {fixedRun.b} (cap 10), farthest {fixedRun.farB:F0} u; " +
                          $"pre-fix job: second {preFix.b} engaged, farthest {preFix.farB:F0} u");
        Check(fixedRun.a <= 2 && fixedRun.b == 10 && fixedRun.farB > 100f,
              "U1 each population engages to its own radius and cap (second: 10 engaged, beyond the first's 100 u)");
        Check(preFix.b <= 2, "U1 negative control: without per-population numbers the second population is clipped to the first's cap");
    }

    /// <summary>U3: one agent bites (QueueFeed) and is killed (QueueKill) in the same main-thread pass - the glue's Feed
    /// then Hunt / ShedStarving order. The job must pay the meal into the body before the kill, so the core's books carry
    /// it in (MassIn) and out (MassOut, the Killed stock). Negative control: the pre-fix order (kill, then feed) loses it.</summary>
    static void UnitLateMeal()
    {
        const float meal = 7.5f;
        var core = new SubstrateCore(16, 1200f, 0.1f, 40, 3);
        var pp = SubstrateResearch.ByName("locust", game: true).Clone(); pp.Capacity = 4;
        int q = core.AddPopulation(pp, 2);
        core.SeedAt(q, new[] { new Vector3(500, 0, 0) }, 0f);
        var job = new SubstrateTickJob(core, new SubstrateTickSettings());
        job.Prime();
        job.Kick(true); job.Collect();
        int i = core.Pops[q].Start;
        var b = job.Body[i];
        float published = b.X * b.Y * b.Z;
        double in0 = core.MassIn;
        job.QueueFeed(i, meal);
        job.QueueKill(i);
        job.Kick(true); job.Collect();
        float killed = job.Killed.Count == 1 && job.Killed[0].Index == i ? job.Killed[0].Volume : -1f;
        double audit = core.MassIn - core.MassOut - core.MassHeld();
        Console.WriteLine($"   published body {published:F3}, meal {meal}, Killed stock {killed:F3}, MassIn +{core.MassIn - in0:F3}, self-audit {audit:E1}");
        Check(MathF.Abs(killed - (published + meal)) < 1e-3f && Math.Abs(core.MassIn - in0 - meal) < 1e-6 && Math.Abs(audit) < 1e-6,
              "U3 the meal reaches the body and leaves with it (Killed = published body + meal; MassIn counts it)");
        // negative control: the pre-fix order, against the core directly - a feed that meets a dead agent is dropped
        var core2 = new SubstrateCore(16, 1200f, 0.1f, 40, 3);
        int q2 = core2.AddPopulation(pp, 2);
        core2.SeedAt(q2, new[] { new Vector3(500, 0, 0) }, 0f);
        int j = core2.Pops[q2].Start;
        double in2 = core2.MassIn;
        core2.Kill(j);
        core2.Feed(j, meal);
        Check(Math.Abs(core2.MassIn - in2) < 1e-9, $"U3 negative control: kill-then-feed (the pre-fix order) drops the {meal} meal - no book holds it");
    }

    static bool Subnormal(float v) => v != 0f && MathF.Abs(v) < 1.17549435e-38f;

    /// <summary>U5: a pilot hovering in the cell deposits its wake into THREAT every tick (SubstrateFields.Update) and
    /// the blur spreads it with decay 0.9: the steady state falls off exponentially with distance, so a band of cells
    /// far from the pilot sits forever in float's subnormal range - the showcase cell's 30-min run, where the
    /// substrate's step grew from 2 to 32 ms. Every value must stay normal or 0. Negative control: the same
    /// source-blur-decay without the flush (the pre-fix arithmetic, replayed on one row of cells) holds subnormals.</summary>
    /// <summary>A fake collapsed population drifting 30 u a macro tick toward a pilot that flies past it sideways.</summary>
    sealed class DriftPop : IMacroPopulation
    {
        public Vector3 C;
        public bool Col = true;
        public Vector3 MacroCentre => C;
        public float MacroExtent => 0f;
        public bool IsCollapsed => Col;
        public bool CanCollapse => !Col;
        public bool NeedsIndividuals => false;
        public MacroPopulationTotals Totals => default;
        public bool Collapse() { Col = true; return true; }
        public void Expand() => Col = false;
        public void MacroTick(float dt) => C -= new Vector3(30f * dt, 0f, 0f);
    }

    /// <summary>U6: the pilot sits at the origin flying +Y (the body is abeam, outside its forward cone); the body starts
    /// 15 u outside the director's expand radius and its macro tick moves it 30 u in. The director must leave it expanded
    /// after that Tick. Negative control: the pre-fix tick (MacroTick, no re-check) leaves it collapsed inside the radius
    /// until the next frame's Guard.</summary>
    static void UnitMacroDrift()
    {
        bool After(bool fixedTick)
        {
            var dir = new EcologyLodDirector();
            var pop = new DriftPop { C = new Vector3((float)dir.P.ExpandRadius + 15f, 0f, 0f) };
            dir.Register(pop);
            dir.SetPilots(new[] { new EcologyPilot { Vy = 120, Speed = 120 } });
            if (fixedTick) dir.Tick(1f);
            else pop.MacroTick(1f);   // what Tick did before round 11-10 for a collapsed body nobody wanted yet
            bool inside = pop.C.Length() < dir.P.ExpandRadius;
            return inside && pop.IsCollapsed;
        }
        bool fixedLeft = After(true), preLeft = After(false);
        Console.WriteLine($"   after the tick that moved it inside the expand radius: collapsed {fixedLeft} (pre-fix order: {preLeft})");
        Check(!fixedLeft, "U6 a body the macro tick moves into prefetch is expanded within that tick");
        Check(preLeft, "U6 negative control: ticking without the re-check leaves it collapsed inside the radius");
    }

    /// <summary>U7: 16 roosting members (12-29 volume stomachs) at the wearers' torpor for 30 min of 10 Hz macro-equivalent burns; Metabolised must
    /// equal exactly what the float stomachs lost. Negative control: the pre-fix float bookkeeping drifts.</summary>
    static void UnitBuilderLedger()
    {
        var alive = Enumerable.Repeat(true, 16).ToArray();
        var st = Enumerable.Range(0, 16).Select(k => 12f + 1.13f * k).ToArray();   // 12-29 volume stomachs, the cores' range
        double before = st.Sum(v => (double)v), booked = 0;
        float preFix = 0f;
        var st2 = (float[])st.Clone();
        for (int t = 0; t < 18000; t++)
        {
            booked += BuilderRoost.Burn(alive, st, 16, 0.004f, 0.1f);
            for (int k = 0; k < 16; k++) { float b = MathF.Min(st2[k], 0.004f * 0.1f); st2[k] -= b; preFix += b; }
        }
        double lost = before - st.Sum(v => (double)v), lost2 = before - st2.Sum(v => (double)v);
        Console.WriteLine($"   lost {lost:F4}, booked {booked:F4} (error {Math.Abs(lost - booked):E1}); pre-fix float books {preFix:F4} vs lost {lost2:F4} (error {Math.Abs(lost2 - preFix):E1})");
        Check(Math.Abs(lost - booked) < 1e-9 * before, "U7 Metabolised books exactly what the stomachs lost");
        Check(Math.Abs(lost2 - preFix) > 1e-3, "U7 negative control: float bookkeeping drifts measurably");
    }

    static void UnitDenormal()
    {
        var f = new SubstrateFields(1200f, 40);
        var none = ReadOnlySpan<SubstrateFood>.Empty;
        var noPilots = new[] { new SubstratePilot { Pos = new Vector3(0, 0, 600), Radius = 6f, Id = 0 } };
        f.Update(none, noPilots);
        var sw = Stopwatch.StartNew();
        for (int t = 0; t < 10; t++) f.Update(none, noPilots);
        double early = sw.Elapsed.TotalMilliseconds / 10;
        for (int t = 0; t < 600; t++) f.Update(none, noPilots);
        int n = f.G * f.G * f.G, sub = 0;
        for (int ch = 1; ch < SubstrateFields.Channels; ch++)
            for (int i = 0; i < n; i++) if (Subnormal(f.Sample(ch, i))) sub++;
        sw.Restart();
        for (int t = 0; t < 10; t++) f.Update(none, noPilots);
        double late = sw.Elapsed.TotalMilliseconds / 10;
        // the pre-fix arithmetic: a 3-tap blur (a = 0.25) with decay 0.8 along a row, no flush
        var row = new float[4096]; var tmp = new float[4096];
        for (int t = 0; t < 611; t++)
        {
            row[2048] += 1f;
            for (int i = 0; i < row.Length; i++)
                tmp[i] = 0.9f * (0.6f * row[i] + 0.2f * (i > 0 ? row[i - 1] : 0f) + 0.2f * (i < row.Length - 1 ? row[i + 1] : 0f));
            (row, tmp) = (tmp, row);
        }
        int subPre = row.Count(Subnormal);
        Console.WriteLine($"   after 611 updates with a hovering pilot: {sub} subnormal field values (update {early:F2} ms early, {late:F2} ms late); " +
                          $"the unflushed arithmetic leaves {subPre} of 4096 subnormal");
        Check(sub == 0, "U5 no field value is subnormal (the far tail of a steady wake is flushed to 0)");
        Check(subPre > 0, "U5 negative control: blur-and-decay without the flush ends in subnormals");
    }

    /// <summary>U4: a member dies and a birth re-uses its slot in the same step (MaybeBreed writes the newborn's stomach
    /// into the dead slot). The death record must still carry what the dead member held. Negative control: reading
    /// Stomach[slot] after the step (the pre-fix ledger) books the newborn's stomach instead.</summary>
    static void UnitDeathStomach(JsonElement layout)
    {
        var world = new CellWorld(1, 1200f);
        var thief = new ThiefNestCore(world, new ThiefParams
        {
            Founders = 2, MaxThieves = 2,
            Stomach = new BuilderStomachParams { Capacity = 40f, FounderFill = 0.5f, Metabolism = 0.02f, Torpor = 0.02f, HungryBelow = 0.5f, BirthAbove = 0.9f, BirthCost = 16f },
        }, new Vector3(1100, 0, 0), 2, 1, 5);
        float held = thief.Stomach[0];
        thief.Kill(0, 3);
        thief.Stomach[0] = 8f;   // what MaybeBreed writes into a re-used slot (BirthCost x 0.5)
        var d = thief.Deaths[^1];
        Console.WriteLine($"   died holding {held:F3}; death record {d.Stomach:F3}; the slot now reads {thief.Stomach[0]:F3}");
        Check(d.Agent == 0 && MathF.Abs(d.Stomach - held) < 1e-6f, "U4 the death record carries the stomach the member died with");
        Check(MathF.Abs(thief.Stomach[d.Agent] - held) > 1e-3f, "U4 negative control: the array read after a same-step re-use books the newborn's stomach");
    }

    /// <summary>U2: SwarmFauna seeds SeedMembers x PlanDensity members; with the upsampled plans the core holds them all.</summary>
    static void UnitDensity(JsonElement layout, SwarmPlanData[] plans, SwarmPlanData[] raw, int density)
    {
        int seed = (int)layout.GetProperty("swarm_config").GetProperty("SeedMembers").GetDouble() * density;
        int Seeded(SwarmPlanData[] ps)
        {
            var core = new SwarmSortCore(ps, new SwarmSortParams { Cap = ps.Max(p => p.N), Membrane = 580f }, 5);
            core.Seed(1, seed, new Vector3(300f, 0f, 0f), Vector3.UnitX);
            return core.AliveCount;
        }
        int dense = Seeded(plans), baked = Seeded(raw);
        Console.WriteLine($"   seed {seed}: upsampled whale cap {plans[1].N} holds {dense}; baked cap {raw[1].N} holds {baked}");
        Check(plans[1].N == raw[1].N * density && dense == seed, $"U2 whale cap {plans[1].N} = {density} x baked, and all {seed} seeds hatch");
        Check(baked < seed, $"U2 negative control: the baked plans (the pre-fix loader) drop {seed - baked} of {seed} seeds");
    }

    // ═════════════════════════════════════════════════════════════════════════════════════ a run

    sealed class RunResult
    {
        public int Seed;
        public float Minutes;
        public int CollidersMax;
        public double CollidersMean;
        public double MsPerFrameMean, MsPerFrameP99, MsPerFrameMax;
        public readonly Dictionary<string, double> MsBySystem = new();
        public double LedgerWorst, LedgerScale;
        public readonly Dictionary<string, (int min, int max, int cap)> Pop = new();
        public readonly List<string> Extinct = new(), Over = new();
        /// <summary>The first census minute a class read zero (the cores alone: the cell's spawner, which re-hatches an
        /// emptied fauna slot after BaseFaunaSpawnTime, is not modelled).</summary>
        public readonly Dictionary<string, float> ExtinctAt = new();
        public Pilot[] Pilots = Array.Empty<Pilot>();
        public List<Dictionary<string, int>[]> Met = new();
        public readonly List<string> Reports = new();
        public readonly List<(string, double)> LedgerLines = new();
        public long LodSeen, LodTouched, LodCollapses;
        public readonly Dictionary<string, double> LodShare = new();
    }

    static RunResult Run(JsonElement layout, SwarmPlanData[] plans, int density, int seed, float seconds, string? snapOut)
    {
        Console.WriteLine($"\n── seed {seed}, {seconds / 60f:F0} min ──");
        var c = new Cell(layout, seed);
        var flora = new FloraSystem(c);
        var swarm = new SwarmSystem(c, plans, density);
        var sub = new SubstrateSystem(c, flora);
        var systems = new List<ICellSystem> { flora, swarm, sub };
        Func<Vector3, float, Vector3> outside = (p, clr) => p;
        GroveSystem? grove = null;
#if THREAT_FLORA
        grove = new GroveSystem(c);
        outside = (p, clr) => grove.Shape.PushOutside(p, clr);
        sub.ExtraHearts = grove.Hearts;
#endif
        var builders = new BuilderSystem(c, outside) { FortWorld = c.World };
        systems.Add(builders);
        if (grove != null) systems.Add(grove);

        string[] kinds = { "careless", "skilled", "raider" };
        int pilots = int.TryParse(Environment.GetEnvironmentVariable("SHOWCASE_PILOTS"), out int np) ? np : 3;
        for (int k = 0; k < 3; k++)
        {
            var p = new Pilot { Id = k, Kind = kinds[k] };
            if (k >= pilots) p.Speed = 0f;   // diagnostics: a parked pilot far outside the membrane (SHOWCASE_PILOTS)
            p.Pos = p.Prev = c.RandomInShell(600f, 1000f);
            if (k >= pilots) p.Pos = p.Prev = new Vector3(0f, 0f, 6000f);
            p.Vel = c.Rng.OnUnitSphere() * p.Speed;
            p.Goal = c.RandomInShell(450f, 1150f);
            c.Pilots.Add(p);
        }

        c.World.VesselDomain = k => k >= 0 && k < c.Pilots.Count ? c.Pilots[k].Domain : -1;
        var r = new RunResult { Seed = seed, Minutes = seconds / 60f };
        int worst = layout.GetProperty("colliders").GetProperty("worst").GetInt32();
        int burnRule = layout.GetProperty("petal_burn_rule").GetInt32();
        int perElement = burnRule == 1 ? 1 : 5;   // CellConfigDataSO.PetalBurnRule: Tuned 1 petal per element, Shipped 5
        var frameMs = new List<double>();
        long colliderSum = 0, ticks = 0;
        var census = new List<(string cls, int alive, int cap)>();
        int steps = (int)MathF.Round(seconds / Cell.Dt);
        for (int step = 0; step < steps; step++)
        {
            foreach (var p in c.Pilots) Fly(c, p, builders, grove);
            c.World.T = c.T;
            c.World.Rebuild();
            c.Contacts.Clear();
            c.AdvanceLod(LodTrace ? Console.WriteLine : null);
            double ms = c.LodMs;
            r.MsBySystem.TryGetValue("lod", out double lodAcc);
            r.MsBySystem["lod"] = lodAcc + c.LodMs;
            foreach (var s in systems)
            {
                s.Tick(c);
                ms += s.LastMs;
                r.MsBySystem.TryGetValue(s.Name, out double acc);
                r.MsBySystem[s.Name] = acc + s.LastMs;
            }
            frameMs.Add(ms * 10.0 / 60.0);   // every core steps at 10 Hz (the grove's 20 Hz traps are inside its LastMs)
            c.StrikersNow.Clear();
            foreach (var s in systems) s.Strikers(c, c.StrikersNow);
            c.TrackIntent();
            Burns(c, perElement);
            int col = 0;
            foreach (var s in systems) col += s.Colliders(c);
            r.CollidersMax = Math.Max(r.CollidersMax, col);
            colliderSum += col; ticks++;
            c.T += Cell.Dt;

            if (TraceEvery > 0 && step % TraceEvery == 0)
            {
                var parts = new List<string>();
                foreach (var pop in sub.Core.Pops)
                {
                    int n = 0; float ph = 0, hu = 0; var cen = Vector3.Zero;
                    for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                        if (sub.Core.Alive[i]) { n++; ph += sub.Core.Phase[i]; hu += sub.Core.Hunger[i]; cen += sub.Core.Pos[i]; }
                    if (n == 0) continue;
                    cen /= n;
                    float best = float.MaxValue;
                    foreach (var pl in c.World.Plants) if (pl.Alive) best = MathF.Min(best, Vector3.Distance(pl.Heart, cen));
                    var dl = new List<float>(); int noGrad = 0; var q = new List<int>();
                    for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                    {
                        if (!sub.Core.Alive[i]) continue;
                        if (sub.Core.GFood[i].Length() < 1e-6f) noGrad++;
                        float bd = 999f;
                        int fnd = c.World.QuerySphere(sub.Core.Pos[i], 300f, q);
                        for (int k = 0; k < fnd; k++) if (c.World.IsFloraTissue(q[k])) bd = MathF.Min(bd, Vector3.Distance(c.World.Pos[q[k]], sub.Core.Pos[i]));
                        dl.Add(bd);
                    }
                    dl.Sort();
                    parts.Add($"{pop.P.Name} n={n} phase {ph / n:F2} hunger {hu / n:F2} centroid r={cen.Length():F0} plant {best:F0}u" +
                              $" tissue min {dl[0]:F0} med {dl[dl.Count / 2]:F0} <=24u {dl.Count(x => x <= 24f)} zeroGrad {noGrad}");
                }
                Console.WriteLine($"   t={c.T:F0} " + string.Join(" | ", parts));
            }
            if (step % 600 == 599 || step == steps - 1)   // each minute: census + ledger
            {
                census.Clear();
                foreach (var s in systems) s.Census(census);
                if (Environment.GetEnvironmentVariable("SHOWCASE_TRACE") == "1")
                {
                    var plates = new int[3];
                    foreach (var pl in c.World.Plants) if (pl.Alive) plates[pl.Region] += pl.Plates;
                    var k = sub.Core;
                    Console.WriteLine($"   substrate cumulative ms: fields {k.MsFields:F0} hash {k.MsHash:F0} agents {k.MsAgents:F0} world {k.MsWorld:F0} kernel {k.MsKernel:F0}; food points {sub.Job.FoodCount}");
                    Console.WriteLine($"   t={c.T + Cell.Dt:F0}s " + string.Join(" ", census.Select(x => $"{x.cls}={x.alive}")) +
                                      $" plates={string.Join("/", plates)} eaten " + string.Join(" ", c.World.EatenBy.Select(kv => $"{kv.Key}:{kv.Value:F0}")) +
                                      $" births {string.Join("/", sub.Core.Pops.Select(q => q.Births))}" + NearestPlantStats(c, sub));
                }
                foreach (var (cls, alive, cap) in census)
                {
                    var cur = r.Pop.TryGetValue(cls, out var v) ? v : (int.MaxValue, 0, cap);
                    r.Pop[cls] = (Math.Min(cur.Item1, alive), Math.Max(cur.Item2, alive), cap);
                    if (alive == 0 && !r.ExtinctAt.ContainsKey(cls)) r.ExtinctAt[cls] = (c.T + Cell.Dt) / 60f;
                }
                swarm.SyncBook(c.World);
                double res = Ledger(c, swarm, sub, builders, grove, out double scale, null);
                r.LedgerWorst = Math.Max(r.LedgerWorst, Math.Abs(res));
                r.LedgerScale = Math.Max(r.LedgerScale, scale);
            }
            if (snapOut != null && snapOut.Length > 0 && MathF.Abs(c.T - 300f) < Cell.Dt * 0.5f) Snapshot(c, systems, builders, snapOut);
        }
        swarm.SyncBook(c.World);
        Ledger(c, swarm, sub, builders, grove, out _, r.LedgerLines);
        r.CollidersMean = colliderSum / (double)Math.Max(1, ticks);
        frameMs.Sort();
        r.MsPerFrameMean = frameMs.Average();
        r.MsPerFrameP99 = frameMs[(int)(0.99 * (frameMs.Count - 1))];
        r.MsPerFrameMax = frameMs[^1];
        foreach (var k in r.MsBySystem.Keys.ToList()) r.MsBySystem[k] = r.MsBySystem[k] / ticks * 10.0 / 60.0;
        foreach (var (cls, (min, max, cap)) in r.Pop)
        {
            if (min == 0) r.Extinct.Add(r.ExtinctAt.TryGetValue(cls, out float m) ? $"{cls} (by min {m:F0})" : cls);
            if (max > cap) r.Over.Add(cls);
        }
        r.Pilots = c.Pilots.ToArray();
        r.Met = c.Met;
        r.Reports.Add("swarm: " + swarm.Report());
        r.Reports.Add(!c.LodOn ? "ecology LOD off" :
            $"ecology LOD: {c.Lod.Collapses} collapses, {c.Lod.Expands} expands, {c.Lod.MacroTicks} macro ticks, refused while seen {c.Lod.RefusedVisible}; " +
            "share of ticks collapsed: " + string.Join(", ", c.LodCollapsedTicks.Select(kv => $"{kv.Key} {Pct((int)kv.Value, (int)c.LodTicks)}")) +
            $"; seen while collapsed {c.LodSeen}, pilot inside a collapsed extent {c.LodTouched}");
        r.LodSeen = c.LodSeen; r.LodTouched = c.LodTouched; r.LodCollapses = c.Lod.Collapses;
        foreach (var kv in c.LodCollapsedTicks) r.LodShare[kv.Key] = kv.Value / (double)Math.Max(1, c.LodTicks);
        r.Reports.Add("substrate: " + sub.Report());
        r.Reports.Add("builders: " + builders.Report());
        if (grove != null) r.Reports.Add("grove: " + grove.Report());

        Console.WriteLine($"   colliders: max {r.CollidersMax}, mean {r.CollidersMean:F0} (authored worst case {worst}, ceiling 1200)");
        Console.WriteLine($"   cpu per 60 Hz frame: mean {r.MsPerFrameMean:F3} ms, p99 {r.MsPerFrameP99:F3}, max {r.MsPerFrameMax:F3}  [" +
                          string.Join(", ", r.MsBySystem.Select(kv => $"{kv.Key} {kv.Value:F3}")) + "]");
        foreach (var line in r.Reports) Console.WriteLine("   " + line);
        Console.WriteLine($"   population min..max/cap: " + string.Join(", ", r.Pop.Select(kv => $"{kv.Key} {kv.Value.min}..{kv.Value.max}/{kv.Value.cap}")));
        Console.WriteLine($"   ledger: worst residual {r.LedgerWorst:E2} against {r.LedgerScale:E3} booked");
        foreach (var (line, v) in r.LedgerLines) Console.WriteLine($"     {line,-62} {v,14:F1}");
        foreach (var p in c.Pilots)
            Console.WriteLine($"   {p.Kind,-8}: {p.Landed / r.Minutes:F2} burns/min ({p.Landed} landed of {p.Contacts} contacts), " +
                              $"telegraphed {Pct(p.Telegraphed, p.Landed)}, petals {p.Petals.Sum():0.##}/20 left; by class " +
                              string.Join(" ", p.ByClass.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}:{kv.Value}")));
        return r;
    }

    /// <summary>Diagnostics (SHOWCASE_TRACE): per substrate population, the median distance of its agents to the nearest
    /// live plant heart and the fraction within the food field's reach (~250 u).</summary>
    static string NearestPlantStats(Cell c, SubstrateSystem sub)
    {
        var parts = new List<string>();
        foreach (var pop in sub.Core.Pops)
        {
            var d = new List<float>();
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
            {
                if (!sub.Core.Alive[i]) continue;
                float best = float.MaxValue;
                foreach (var pl in c.World.Plants) if (pl.Alive) best = MathF.Min(best, Vector3.Distance(pl.Heart, sub.Core.Pos[i]));
                d.Add(best);
            }
            if (d.Count == 0) continue;
            d.Sort();
            parts.Add($"{pop.P.Name} median {d[d.Count / 2]:F0} u, <250 u {100f * d.Count(x => x < 250f) / d.Count:F0}%");
        }
        return "; nearest plant: " + string.Join(", ", parts);
    }

    /// <summary>SHOWCASE_LOD_TRACE=1: every 10 s, each LOD'd population's state and what holds it expanded.</summary>
    static readonly bool LodTrace = Environment.GetEnvironmentVariable("SHOWCASE_LOD_TRACE") == "1";
    static readonly int TraceEvery = int.TryParse(Environment.GetEnvironmentVariable("SHOWCASE_TRACE_EVERY"), out int te) ? te : 0;

    static string Pct(int a, int b) => b == 0 ? "-" : $"{100.0 * a / b:F0}%";

    /// <summary>Global ledger: every volume that entered the cell's books = where it is now. Prints the per-boundary
    /// sub-residuals when <paramref name="lines"/> is given.</summary>
    static double Ledger(Cell c, SwarmSystem swarm, SubstrateSystem sub, BuilderSystem b, GroveSystem? grove, out double scale,
                         List<(string, double)>? lines)
    {
        var w = c.World;
        double E(string k) => w.EatenBy.TryGetValue(k, out var v) ? v : 0;
        double La(string k) => w.LaidBy.TryGetValue(k, out var v) ? v : 0;
        double src = w.Grown + w.Trail + sub.Seeded + b.Founders + (grove?.Planted ?? 0);
        double live = w.LiveVolume();
        double acct = live + w.Destroyed + swarm.Held() + sub.Held() + b.Held() + (grove?.Held() ?? 0);
        scale = src;
        if (lines != null)
        {
            lines.Add(("SOURCES flora grown", w.Grown));
            lines.Add(("SOURCES vessel wake", w.Trail));
            lines.Add(("SOURCES substrate seed + builder founders + grove planted", sub.Seeded + b.Founders + (grove?.Planted ?? 0)));
            lines.Add(("ACCOUNT world prisms live", live));
            lines.Add(("ACCOUNT destroyed by vessels", w.Destroyed));
            lines.Add(("(info) taken back by vessels from a hoard or a wearer - changed hands, still live", w.Reclaimed));
            lines.Add(("ACCOUNT swarm (bites - skeletons)", swarm.Held()));
            lines.Add(("ACCOUNT substrate (living stock + rammed bodies)", sub.Held()));
            lines.Add(("ACCOUNT builders (stomachs, dead stomachs, metabolised, bodies)", b.Held()));
            if (grove != null) lines.Add(("ACCOUNT grove reserves", grove.Held()));
            lines.Add(("residual: world store (in - live - eaten - destroyed)", w.Audit()));
            lines.Add(("residual: substrate (seed + eaten - laid - held)", sub.Seeded + E("substrate") - La("substrate") - sub.Held()));
            lines.Add(("residual: builders (founders + eaten - held)", b.Founders + E("builders") - b.Held()));
            if (grove != null) lines.Add(("residual: grove (planted + eaten - laid - held)", grove.Planted + E("grove") - La("grove") - grove.Held()));
            lines.Add(("RESIDUAL global (sources - accounts)", src - acct));
            sub.Ledger(lines);
            b.Ledger(lines);
            grove?.Ledger(lines);
        }
        return src - acct;
    }

    /// <summary>Every contact this tick: one cooldown of 1 s per vessel across all of them; a landed contact burns
    /// perElement petals from every element (clamped at 0) and is rated telegraphed or not.</summary>
    static void Burns(Cell c, int perElement)
    {
        foreach (var x in c.Contacts)
        {
            var p = c.Pilots[x.Pilot];
            p.Contacts++;
            c.Meet(x.Pilot, x.Cls, true);
            if (c.T < p.Cool) continue;
            p.Cool = c.T + 1f;
            p.Landed++;
            p.ByClass.TryGetValue(x.Cls, out int n);
            p.ByClass[x.Cls] = n + 1;
            bool tele = x.Telegraphed ?? c.Telegraphed(x.Cls, p.Pos);
            if (tele) p.Telegraphed++;
            float w = x.Weight > 0f ? x.Weight : 1f;   // the danger effect scales its size by the contact's weight
            for (int e = 0; e < 4; e++) p.Petals[e] = MathF.Max(0f, p.Petals[e] - perElement * w);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════════════ pilots

    static void Fly(Cell c, Pilot p, BuilderSystem b, GroveSystem? grove)
    {
        p.Prev = p.Pos;
        if (p.Speed <= 0f) return;
        Vector3 goal = p.Kind == "raider" ? RaiderGoal(c, p, b) : WanderGoal(c, p);
        var want = goal - p.Pos;
        if (p.Kind == "skilled")
        {
            // steer clear of anything showing intent: every armed striker inside 220 u pushes, nearer pushes harder
            var away = Vector3.Zero;
            foreach (var s in c.StrikersNow)
            {
                if (s.Intent <= 0.5f) continue;
                var d = p.Pos - s.Pos;
                float l = d.Length();
                if (l < 220f && l > 1e-3f) away += d / l * ((220f - l) / 220f);
            }
#if THREAT_FLORA
            // and the grove it knows is there: it skirts the rim sector
            if (grove != null && grove.Shape.Contains(p.Pos + p.Vel * 1.5f, -40f)) away += Vector3.Normalize(-p.Pos) * 1.5f;
#endif
            if (want.LengthSquared() > 1e-6f) want = Vector3.Normalize(want) + away * 2.5f;
        }
        float n = want.Length();
        if (n > 1e-6f)
        {
            var v = p.Vel / MathF.Max(p.Vel.Length(), 1e-6f);
            var w = want / n;
            float ang = MathF.Acos(Math.Clamp(Vector3.Dot(v, w), -1f, 1f));
            float k = MathF.Min(1f, p.Turn * Cell.Dt / MathF.Max(ang, 1e-6f));
            var nd = v + (w - v) * k;
            p.Vel = nd / MathF.Max(nd.Length(), 1e-6f) * p.Speed;
        }
        p.Pos += p.Vel * Cell.Dt;
        // the membrane and the nucleus: a vessel that meets either BOUNCES (its radial velocity reflects). Before round
        // 11-10 the pilot was only projected back, so a pilot flying straight out sat pinned at the rim, motionless, laying
        // a pile of fresh wake under itself - and every thief that dived for that warm wake touched it and died.
        float r = p.Pos.Length();
        if (r > 1e-3f && (r > c.Membrane * 0.97f || r < 420f))
        {
            var nrm = p.Pos / r;
            p.Pos = nrm * Math.Clamp(r, 420f, c.Membrane * 0.97f);
            float vr = Vector3.Dot(p.Vel, nrm);
            if ((r > 420f && vr > 0f) || (r <= 420f && vr < 0f)) p.Vel -= 2f * vr * nrm;
        }
        // the wake: a 6-volume trail prism every 0.25 s (the builders harness's pilot, Tools/Build/builders_harness/Arena.cs)
        p.TrailT += Cell.Dt;
        while (p.TrailT >= 0.25f)
        {
            p.TrailT -= 0.25f;
            var back = p.Prev - Vector3.Normalize(p.Vel) * 8f;
            c.World.LayTrail(back, 6f, p.Domain, p.Id);
        }
        if (c.T >= 270f && c.T <= 300f) p.Path.Add(p.Pos);
    }

    static Vector3 WanderGoal(Cell c, Pilot p)
    {
        if (Vector3.Distance(p.Goal, p.Pos) < 40f || c.T - p.GoalT > 20f) { p.Goal = c.RandomInShell(450f, 1150f); p.GoalT = c.T; }
        return p.Goal;
    }

    /// <summary>The raider: to the hoard, weave through it for 8 s taking prisms back, then a straight cut through the
    /// fortress wall and out the other side, a random point, and again.</summary>
    static Vector3 RaiderGoal(Cell c, Pilot p, BuilderSystem b)
    {
        switch (p.Phase)
        {
            case 0:
                if (Vector3.Distance(p.Pos, b.Nest) < 30f) { p.Phase = 1; p.PhaseT = c.T; }
                return b.Nest;
            case 1:
            {
                if (c.T - p.PhaseT > 8f) { p.Phase = 2; return b.FortAnchor; }
                var hoard = b.Thief.Hoard;
                if (hoard.Count > 0) return c.World.Pos[hoard[(int)(c.T * 2f) % hoard.Count]];
                return b.Nest + c.Rng.OnUnitSphere() * 20f;
            }
            case 2:
                if (Vector3.Distance(p.Pos, b.FortAnchor) < 15f) { p.Phase = 3; p.Goal = b.FortAnchor + Vector3.Normalize(p.Vel) * 220f; }
                return b.FortAnchor;
            case 3:
                if (Vector3.Distance(p.Pos, p.Goal) < 30f) { p.Phase = 4; p.Goal = c.RandomInShell(450f, 1150f); p.GoalT = c.T; }
                return p.Goal;
            default:
                if (Vector3.Distance(p.Goal, p.Pos) < 40f || c.T - p.GoalT > 15f) p.Phase = 0;
                return p.Goal;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════════════ snapshot + summary

    static void Snapshot(Cell c, List<ICellSystem> systems, BuilderSystem b, string path)
    {
        using var fs = File.Create(path);
        using var w = new Utf8JsonWriter(fs);
        w.WriteStartObject();
        w.WriteNumber("t", c.T);
        w.WriteNumber("membrane", c.Membrane);
        w.WriteNumber("nucleus", c.L.GetProperty("nucleus").GetDouble());
        w.WriteStartArray("bands");
        foreach (var rg in c.L.GetProperty("regions").EnumerateArray())
        {
            w.WriteStartObject();
            w.WriteString("key", rg.GetProperty("key").GetString());
            w.WriteNumber("inner", rg.GetProperty("band")[0].GetDouble());
            w.WriteNumber("outer", rg.GetProperty("band")[1].GetDouble());
            w.WriteEndObject();
        }
        w.WriteEndArray();
        foreach (var s in systems) s.Snapshot(w);
        w.WriteStartArray("trail");
        for (int h = 0; h < c.World.Count; h++)
            if (c.World.AliveL[h] && c.World.Kind[h] == MassKind.Trail && c.World.Owner[h] >= 0)
            {
                w.WriteStartArray();
                w.WriteNumberValue(MathF.Round(c.World.Pos[h].X)); w.WriteNumberValue(MathF.Round(c.World.Pos[h].Y)); w.WriteNumberValue(MathF.Round(c.World.Pos[h].Z));
                w.WriteNumberValue(c.World.Owner[h]);
                w.WriteEndArray();
            }
        w.WriteEndArray();
        w.WriteStartArray("pilots");
        foreach (var p in c.Pilots)
        {
            w.WriteStartObject();
            w.WriteString("kind", p.Kind);
            WriteVec(w, "at", p.Pos);
            w.WriteStartArray("path");
            for (int i = 0; i < p.Path.Count; i += 5)
            {
                w.WriteStartArray();
                w.WriteNumberValue(MathF.Round(p.Path[i].X)); w.WriteNumberValue(MathF.Round(p.Path[i].Y)); w.WriteNumberValue(MathF.Round(p.Path[i].Z));
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
        Console.WriteLine($"   snapshot at t={c.T:F0} s -> {path}");
    }

    static void Summary(JsonElement layout, List<RunResult> runs)
    {
        int worst = layout.GetProperty("colliders").GetProperty("worst").GetInt32();
        int ceiling = layout.GetProperty("colliders").GetProperty("ceiling").GetInt32();
        float budget = (float)layout.GetProperty("swarm_config").GetProperty("SimBudgetMsPerFrame").GetDouble();
        Console.WriteLine("\n══ SUMMARY ══");
        Console.WriteLine($"C1 colliders: authored worst case {worst} / ceiling {ceiling}; observed max per run " +
                          string.Join(", ", runs.Select(r => $"s{r.Seed}:{r.CollidersMax} (mean {r.CollidersMean:F0})")));
        Check(worst < ceiling, $"C1 the authored worst case {worst} is under the ceiling {ceiling}");
        Check(runs.All(r => r.CollidersMax <= worst), $"C1 every observed tick <= the authored worst case {worst}");
        double cpuMean = runs.Max(r => r.MsPerFrameMean), cpuP99 = runs.Max(r => r.MsPerFrameP99);
        Console.WriteLine($"C2 cpu: worst run mean {cpuMean:F3} ms / p99 {cpuP99:F3} ms per 60 Hz frame (sum of every core's step at 10 Hz); " +
                          $"the cell's inline budget is {budget} ms per frame (SwarmFaunaConfigSO.SimBudgetMsPerFrame)");
        Check(cpuMean <= budget, $"C2 combined mean step cost {cpuMean:F3} ms/frame <= {budget} ms (fits even run inline on one thread)");
        double ledgerRel = runs.Max(r => r.LedgerWorst / Math.Max(1.0, r.LedgerScale));
        Check(ledgerRel < 1e-6, $"C3 the global mass ledger closes: worst residual {ledgerRel:E2} of the booked volume (< 1e-6)");
        var longRun = runs.OrderByDescending(r => r.Minutes).First();
        Console.WriteLine($"C4 populations over the {longRun.Minutes:F0}-min run (min..max / cap):");
        foreach (var kv in longRun.Pop) Console.WriteLine($"     {kv.Key,-22} {kv.Value.min,5} .. {kv.Value.max,5} / {kv.Value.cap}");
        Finding(longRun.Extinct.Count == 0, $"C4 no class went extinct over {longRun.Minutes:F0} min" + (longRun.Extinct.Count > 0 ? ": " + string.Join(", ", longRun.Extinct) : ""));
        Check(longRun.Over.Count == 0, "C4 no class exceeded its cap" + (longRun.Over.Count > 0 ? ": " + string.Join(", ", longRun.Over) : ""));
        foreach (var run in runs.Where(r => r.Seed != longRun.Seed || r.Minutes == longRun.Minutes))
            if (run.Extinct.Count > 0) Console.WriteLine($"     (seed {run.Seed}: extinct {string.Join(", ", run.Extinct)})");

        Console.WriteLine("C7 ecology LOD (rounds 11f, 11f-2), share of ticks each population spent collapsed:");
        foreach (var run in runs)
            Console.WriteLine($"     s{run.Seed} ({run.Minutes:F0} min, {run.LodCollapses} collapses): " +
                              string.Join(", ", run.LodShare.Select(kv => $"{kv.Key} {100 * kv.Value:F0}%")));
        Check(runs.All(r => r.LodSeen == 0 && r.LodTouched == 0),
              "C7 no collapsed population was ever visible to a pilot or had a pilot inside it (ECOLOGY_LOD §4.1 clause 2)");
        Console.WriteLine("C5 burns (Tuned rule: 1 petal x 4 elements per landed contact, 1 s cooldown per vessel), pooled over runs:");
        string[] kinds = { "careless", "skilled", "raider" };
        var rate = new Dictionary<string, double>();
        foreach (string k in kinds)
        {
            double minutes = runs.Sum(r => r.Minutes);
            int landed = runs.Sum(r => r.Pilots.First(p => p.Kind == k).Landed);
            int tele = runs.Sum(r => r.Pilots.First(p => p.Kind == k).Telegraphed);
            var by = new Dictionary<string, int>();
            foreach (var r in runs) foreach (var kv in r.Pilots.First(p => p.Kind == k).ByClass) { by.TryGetValue(kv.Key, out int v); by[kv.Key] = v + kv.Value; }
            rate[k] = landed / minutes;
            Console.WriteLine($"     {k,-8} {landed / minutes,6:F2} burns/min = {4 * landed / minutes,6:F2} petals/min, telegraphed {Pct(tele, landed)}; " +
                              string.Join(" ", by.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}:{kv.Value}")));
        }
        Finding(rate["skilled"] < rate["careless"], "C5 the skilled pilot burns less than the careless one");
        int allLanded = runs.Sum(r => r.Pilots.Sum(p => p.Landed)), allTele = runs.Sum(r => r.Pilots.Sum(p => p.Telegraphed));
        Console.WriteLine($"     all pilots: {Pct(allTele, allLanded)} of {allLanded} burns telegraphed (the flight sim's stakes_eval measured 92-93%)");

        Console.WriteLine("C6 encounter diversity, seed " + runs[0].Seed + " (per minute: classes met; * = a contact, otherwise seen armed within 150 u):");
        var met = runs[0].Met;
        for (int m = 0; m < met.Count; m++)
        {
            var cells = new List<string>();
            for (int k = 0; k < 3; k++)
                cells.Add($"{kinds[k]}: " + (met[m][k].Count == 0 ? "-" :
                    string.Join(" ", met[m][k].OrderBy(kv => kv.Key).Select(kv => kv.Key + (kv.Value > 1 ? "*" : "")))));
            Console.WriteLine($"     min {m + 1,2} | " + string.Join(" | ", cells));
        }
        var lr = longRun.Met;
        Console.WriteLine($"     {longRun.Minutes:F0}-min run, distinct classes met per minute (mean): " + string.Join(", ",
            Enumerable.Range(0, 3).Select(k => $"{kinds[k]} {lr.Average(row => row[k].Count):F1}")) +
            "; classes ever met: " + string.Join(", ", Enumerable.Range(0, 3).Select(k =>
                $"{kinds[k]} {lr.SelectMany(row => row[k].Keys).Distinct().Count()}")));
    }
}
