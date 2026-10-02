// Round 7 (Docs/SWARM_FAUNA.md §14): the SHIPPED SwarmTickJob - the swarm's tick off the main thread -
// run headless. R7a..R7g are asserted; `run.sh tickjob <plans>` runs them (SWARM_DENSITY=5 is the cell).
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using CosmicShore.Gameplay;

static class TickJobHarness
{
    static int _fail;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static SwarmTickSettings Settings() => new()
    {
        Centre = new Vector3(10, -20, 30), UnitScale = 2f, PrismScale = 1f, HeartPrismGap = 0.6f,
        DefaultHalf = new[] { new Vector3(1f, 0.6f, 0.6f), new Vector3(0.9f, 0.8f, 0.7f), new Vector3(2f, 0.3f, 0.3f), new Vector3(0.8f, 0.4f, 0.4f) },
        EngageRadius = 160f, MaxEngaged = 160,
    };

    static SwarmTickJob Make(SwarmPlanData[] plans, int planElement, int seed)
    {
        var p = SortHarness.Game(plans);
        p.LayMax = Math.Max(p.LayMax, 5 * Math.Max(1, plans[1].N / 192));   // the cell lays at the body's own scale
        var core = SortHarness.GameSwarm(plans, planElement, seed, true, p);
        var job = new SwarmTickJob(core, Settings(), 10f);
        job.SwimTarget = core.SwimTarget;
        job.Prime();
        return job;
    }

    /// <summary>One tick, the way SwarmFauna drives it: kick, wait for Done, collect.</summary>
    static void Tick(SwarmTickJob j, bool inline)
    {
        j.Kick(inline);
        var spin = new SpinWait();
        while (j.State != SwarmJobState.Done) spin.SpinOnce();
        j.Collect();
        if (j.Error != null) throw j.Error;
    }

    static bool SameFrame(SwarmTickJob a, SwarmTickJob b)
    {
        for (int i = 0; i < a.Instances.Length; i++)
        {
            var x = a.Instances[i]; var y = b.Instances[i];
            if (x.Flags != y.Flags) return false;
            if (!x.Alive) continue;
            if (x.CurPos != y.CurPos || x.PrevPos != y.PrevPos || x.CurFace != y.CurFace || x.Scale != y.Scale
                || x.PrismZ != y.PrismZ || x.BirthTick != y.BirthTick || x.CurMolt != y.CurMolt) return false;
        }
        for (int e = 0; e < 4; e++) if (a.HeartCount[e] != b.HeartCount[e]) return false;
        return a.EngagedCount == b.EngagedCount;
    }

    public static int Run(SwarmPlanData[] plans)
    {
        Console.WriteLine("plans: " + string.Join(", ", plans.Select(p => $"{p.Kind} N={p.N} R={p.Radius:F1}")));

        Console.WriteLine("\nR7a. the worker thread and the inline path build the SAME frames (no shared state, no race)");
        {
            var a = Make(plans, 1, 77); var b = Make(plans, 1, 77);
            bool same = SameFrame(a, b);
            for (int t = 0; t < 300 && same; t++)
            {
                if (t % 50 == 25) { a.Preds[0] = b.Preds[0] = new SwarmPredator { C = a.Core.Anchor, V = new Vector3(1, 0, 0), R = 4.5f }; a.PredCount = b.PredCount = 1; }
                if (t % 50 == 40) a.PredCount = b.PredCount = 0;
                Tick(a, inline: false); Tick(b, inline: true);
                same = SameFrame(a, b);
            }
            Console.WriteLine($"  whale grown to {a.AliveCount} through the ThreadPool vs inline");
            Check(same, "every frame bit-identical across 300 ticks, a vessel passing through twice");
        }

        Console.WriteLine("\nR7b. the frame: world positions, interpolation pair, newborns, heart lists");
        {
            var j = Make(plans, 0, 5);
            for (int t = 0; t < 200; t++) Tick(j, false);
            var s = j.S; int alive = 0, bad = 0, listed = 0, born = 0;
            var seen = new int[j.Instances.Length];
            for (int e = 0; e < 4; e++)
                for (int q = 0; q < j.HeartCount[e]; q++)
                {
                    int i = (int)j.HeartIdx[j.HeartStart[e] + q]; listed++;
                    var inst = j.Instances[i];
                    if (!inst.Alive || (inst.HeartFrom != e && inst.HeartTo != e)) bad++;
                    seen[i]++;
                }
            for (int i = 0; i < j.Instances.Length; i++)
            {
                var inst = j.Instances[i];
                if (!inst.Alive) continue;
                alive++;
                var want = s.Centre + j.Core.Pos[i] * s.UnitScale;
                if (Vector3.Distance(want, inst.CurPos) > 1e-3f) bad++;
                int need = inst.HeartFrom == inst.HeartTo ? 1 : 2;
                if (seen[i] != need) bad++;
                if (inst.Scale.X <= 0f || inst.Scale.Z <= 0f || inst.PrismZ >= 0f) bad++;
                if (j.BornThisTick(i)) { born++; if (inst.PrevPos != inst.CurPos) bad++; }
            }
            Console.WriteLine($"  pufferfish {alive} alive, {listed} heart entries, {born} newborn this tick");
            Check(alive == j.AliveCount && alive > 0, "the published alive count is the frame's");
            Check(bad == 0, "every instance at centre + pos x UnitScale; every heart listed once (twice while molting), in its own element; a newborn does not slide in");
        }

        Console.WriteLine("\nR7c. a kill queued WHILE a tick runs is applied by the next tick (the main thread masks it until then)");
        {
            var j = Make(plans, 1, 9);
            for (int t = 0; t < 150; t++) Tick(j, false);
            int victim = Enumerable.Range(0, j.Instances.Length).First(i => j.Instances[i].Alive);
            j.Kick(inline: false);
            j.QueueKill(victim);   // arrives mid-tick, from a "physics callback"
            while (j.State != SwarmJobState.Done) Thread.Yield();
            j.Collect();
            bool stillShown = j.Instances[victim].Alive && !j.BornThisTick(victim);
            Tick(j, false);
            bool gone = !j.Instances[victim].Alive || j.BornThisTick(victim);
            Console.WriteLine($"  slot {victim}: shown alive by the running tick = {stillShown}, applied next tick = {gone}");
            Check(gone, "the queued kill reaches the core on the next tick");
        }

        Console.WriteLine("\nR7d. funded laying through the deposit queue (mass is conserved: no deposit, no egg)");
        {
            var p = SortHarness.Game(plans);
            var core = SortHarness.GameSwarm(plans, 1, 13, fed: false, p: p);
            var j = new SwarmTickJob(core, Settings(), 10f) { SwimTarget = core.SwimTarget };
            j.Prime();
            int n0 = j.AliveCount;
            for (int t = 0; t < 100; t++) Tick(j, false);
            int starved = j.AliveCount;
            j.QueueDeposit(1, 10f * p.EggCost[1]);
            for (int t = 0; t < 200; t++) Tick(j, false);
            int laid = j.AliveCount - starved;
            float left = j.Stomach.Sum();
            Console.WriteLine($"  unfed {n0} -> {starved}; after 10 eggs of Mass food -> {j.AliveCount} (laid {laid}), stomach left {left:F1}");
            // own-element eggs cost 1 egg of food, a cross-element egg CrossCost (2): 10 eggs of food buy 5..10
            Check(starved == n0, "an unfed swarm lays nothing");
            Check(laid >= 5 && laid <= 10 && left < p.EggCost[1] * p.CrossCost, "the deposit is spent on eggs at their price (5..10 eggs) and nothing is laid on credit");
        }

        Console.WriteLine("\nR7e. engagement: only members near a vessel get a proxy, nearest first, capped");
        {
            var j = Make(plans, 1, 21);
            for (int t = 0; t < 300; t++) Tick(j, false);
            Check(j.EngagedCount == 0, "no vessel, no proxies");
            int probe = Enumerable.Range(0, j.Instances.Length).First(i => j.Instances[i].Alive);
            var vw = j.Instances[probe].CurPos;
            j.Preds[0] = new SwarmPredator { C = (vw - j.S.Centre) / j.S.UnitScale, V = Vector3.Zero, R = 4.5f };
            j.PredCount = 1;
            Tick(j, false);
            var v = j.S.Centre + j.Preds[0].C * j.S.UnitScale;
            bool inRange = true, sorted = true; float last = -1f;
            for (int q = 0; q < j.EngagedCount; q++)
            {
                float d = Vector3.Distance(j.Instances[j.Engaged[q]].CurPos, v);
                if (d > j.S.EngageRadius + 1e-3f) inRange = false;
                if (d < last - 1e-3f) sorted = false;
                last = d;
            }
            int within = Enumerable.Range(0, j.Instances.Length).Count(i => j.Instances[i].Alive && Vector3.Distance(j.Instances[i].CurPos, v) <= j.S.EngageRadius);
            Console.WriteLine($"  vessel inside the body: {within} members within {j.S.EngageRadius}u, {j.EngagedCount} engaged (cap {j.S.MaxEngaged})");
            Check(j.EngagedCount == Math.Min(within, j.S.MaxEngaged) && inRange && sorted, "engaged = the nearest members within reach, at most the cap");
        }

        Console.WriteLine("\nR7f. cost: one tick (step + frame build) per swarm, and three swarms in parallel on the pool");
        {
            var jobs = new[] { Make(plans, 1, 31), Make(plans, 0, 32), Make(plans, 2, 33) };
            for (int t = 0; t < 400; t++) foreach (var j in jobs) Tick(j, true);
            int total = jobs.Sum(j => j.AliveCount);
            const int T = 200;
            var sw = Stopwatch.StartNew();
            for (int t = 0; t < T; t++) foreach (var j in jobs) Tick(j, true);
            double serial = sw.Elapsed.TotalMilliseconds / T;
            sw.Restart();
            for (int t = 0; t < T; t++)
            {
                foreach (var j in jobs) j.Kick(false);
                foreach (var j in jobs) { while (j.State != SwarmJobState.Done) Thread.SpinWait(20); j.Collect(); }
            }
            double parallel = sw.Elapsed.TotalMilliseconds / T;
            // what the MAIN thread pays per tick: Kick + Collect only (the worker's time is not on it)
            sw.Restart();
            long mainTicks = 0;
            for (int t = 0; t < T; t++)
            {
                long a0 = Stopwatch.GetTimestamp();
                foreach (var j in jobs) j.Kick(false);
                mainTicks += Stopwatch.GetTimestamp() - a0;
                foreach (var j in jobs) while (j.State != SwarmJobState.Done) Thread.SpinWait(20);
                long b0 = Stopwatch.GetTimestamp();
                foreach (var j in jobs) j.Collect();
                mainTicks += Stopwatch.GetTimestamp() - b0;
            }
            double mainMs = mainTicks * 1000.0 / Stopwatch.Frequency / T;
            Console.WriteLine($"  3 swarms, {total} members (whale {jobs[0].AliveCount}, puffer {jobs[1].AliveCount}, jelly {jobs[2].AliveCount})");
            Console.WriteLine($"  per tick: serial {serial:F3} ms, parallel wall {parallel:F3} ms, MAIN THREAD (kick + collect) {mainMs:F4} ms");
            Console.WriteLine($"  at 10 Hz: worker {serial * 10:F1} ms of CPU per second; main thread {mainMs * 10:F3} ms per second");
            Check(mainMs < 0.05, "the main thread's share of a tick is a pointer swap (< 0.05 ms for three swarms)");
        }

        Console.WriteLine("\nR7g. a worker exception is captured, not lost");
        {
            var j = new SwarmTickJob(new ThrowingCore(), Settings(), 10f);
            j.Kick(false);
            while (j.State != SwarmJobState.Done) Thread.Yield();
            j.Collect();
            Check(j.Error is InvalidOperationException, "Error carries the worker's exception for the main thread to report");
        }

        Console.WriteLine("\nR8c. QueryMembers on a live job == a brute-force scan of every drawn BODY (Docs/SWARM_FAUNA.md §16.1)");
        {
            var j = Make(plans, 1, 5);
            for (int t = 0; t < 60; t++) Tick(j, inline: true);
            var scratch = new System.Collections.Generic.List<int>(); var got = new System.Collections.Generic.List<int>();
            var rng = new Random(3); int cases = 0, wrong = 0;
            for (int v = 0; v < 400; v++)
            {
                int m; do m = rng.Next(j.Instances.Length); while (!j.Instances[m].Alive);
                float alpha = (float)rng.NextDouble();
                var c = j.BodyAt(m, alpha) + new Vector3((float)rng.NextDouble() * 8 - 4, 0, 0);
                var vol = v % 2 == 0 ? SwarmVolume.Sphere(c, 3f + 20f * (float)rng.NextDouble())
                                     : SwarmVolume.ConeSlab(c - new Vector3(60, 0, 0), Vector3.UnitX, Vector3.UnitY, 0f, 120f, 0.1f, 0.05f);
                got.Clear(); j.QueryMembers(vol, alpha, false, scratch, got);
                var want = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < j.Instances.Length; i++) if (j.Instances[i].Alive && vol.Contains(j.BodyAt(i, alpha))) want.Add(i);
                cases += want.Count;
                if (want.Count != got.Count || !want.SetEquals(got)) wrong++;
            }
            Console.WriteLine($"  {j.AliveCount} members, 400 volumes, {cases} member hits, {wrong} volumes disagreed");
            Check(wrong == 0 && cases > 0, "the grid + exact test returns exactly the brute-force set");
            double vol0 = 0; for (int i = 0; i < j.Instances.Length; i++) if (j.Instances[i].Alive) vol0 += SwarmVolumeLedger.BodyVolume(j.Instances[i].Scale);
            Check(Math.Abs(j.VolumeBySlot[0] - vol0) < 1e-6 * Math.Max(1, vol0) && j.VolumeBySlot[1] == 0 && j.VolumeBySlot[2] == 0,
                  $"VolumeBySlot sums every drawn body ({vol0:F0}), all in slot 0 for a one-colour swarm");
        }

        Console.WriteLine("\nR8d. the volume ledger never double-counts a member that has a proxy (Docs/SWARM_FAUNA.md §16.3)");
        {
            var j = Make(plans, 1, 5);
            for (int t = 0; t < 60; t++) Tick(j, inline: true);
            var rng = new Random(11);
            int[] map = { 1, 1, 1 };   // the swarm's domain slots all land in cell slot 1 (Ruby) here
            var excluded = new System.Collections.Generic.List<int>();
            double proxied = 0, all = 0;
            for (int i = 0; i < j.Instances.Length; i++)
            {
                if (!j.Instances[i].Alive) continue;
                double v = SwarmVolumeLedger.BodyVolume(j.Instances[i].Scale);
                all += v;
                if (rng.NextDouble() < 0.2) { excluded.Add(i); proxied += v; }   // these "have a finished proxy"
            }
            // a dead slot excluded too must take nothing out (it was never in the worker's sum)
            int dead = -1; for (int i = 0; i < j.Instances.Length; i++) if (!j.Instances[i].Alive) { dead = i; break; }
            if (dead >= 0) excluded.Add(dead);
            var outv = new double[4];
            SwarmVolumeLedger.State(j.VolumeBySlot, map, j.Counted, j.Instances, excluded, outv);
            double cell = outv[1] + proxied;   // what the cell sees: the stated virtual volume + the proxies' own prisms
            Console.WriteLine($"  {j.AliveCount} members, {excluded.Count} excluded, body volume {all:F0}, stated {outv[1]:F0} + proxies {proxied:F0}");
            Check(Math.Abs(cell - all) < 1e-6 * Math.Max(1, all) && outv[0] == 0 && outv[2] == 0 && outv[3] == 0,
                  "stated + proxied == every member's body exactly once, in the mapped cell slot");
            // negative control: a ledger that forgets the exclusions counts every proxied body twice
            var naive = new double[4];
            SwarmVolumeLedger.State(j.VolumeBySlot, map, j.Counted, j.Instances, new System.Collections.Generic.List<int>(), naive);
            double over = naive[1] + proxied - all;
            Check(proxied > 0 && Math.Abs(over - proxied) < 1e-6 * Math.Max(1, all),
                  $"negative control: without the exclusions the cell over-counts by exactly the proxied volume ({over:F0})");
        }

        Console.WriteLine("\nR8e. MultiDomain: seeds wear the anchor domain, a newborn wears the domain of the mass that FUNDED it (Docs/SWARM_FAUNA.md §16.4)");
        {
            ISwarmCore SortCore(bool multiDomain)
            {
                var p = SortHarness.Game(plans);
                p.LayMax = Math.Max(p.LayMax, 5 * Math.Max(1, plans[1].N / 192));
                p.DomainSlots = multiDomain; p.FoodDomains = multiDomain;
                return SortHarness.GameSwarm(plans, 1, 7, fed: false, p);
            }
            ISwarmCore GridCore(bool multiDomain)
            {
                var p = GridHarness.Game(plans);
                p.DomainSlots = multiDomain; p.FoodDomains = multiDomain;
                return GridHarness.GameSwarm(plans, 1, 7, fed: false, p);
            }
            ISwarmCore FieldCore(bool multiDomain)
            {
                var c = new SwarmFieldCore(plans, new SwarmFieldParams { FoodDomains = multiDomain, LayMax = 5, LayRate = 0.1f, Cap = plans.Max(q => q.N) }, 7);
                c.Seed(1, SortHarness.GameSeed, new Vector3(200, 0, 0), Vector3.UnitX);
                c.SwimTarget = c.Anchor;
                return c;
            }
            (int[] count, int seeds, double sameNN, double randomNN) RunMulti(Func<bool, ISwarmCore> make, bool multiDomain)
            {
                var core = make(multiDomain);
                var st = Settings(); st.MultiDomain = multiDomain;
                var j = new SwarmTickJob(core, st, 10f) { SwimTarget = core.SwimTarget };
                j.Prime();
                int seeds = j.AliveCount;
                // the eaten mass arrives a little at a time, the way grazing deposits it: first slot 1, then slot 2
                for (int t = 0; t < 400; t++)
                {
                    int slot = t < 200 ? 1 : 2;
                    if (t % 10 == 0) for (int e = 0; e < 4; e++) j.QueueDeposit(e, 0.6f * plans[1].Mix[e] / (float)plans[1].N * 10f, slot);
                    Tick(j, inline: true);
                }
                for (int t = 0; t < 300; t++) Tick(j, inline: true);
                var count = new int[3]; var live = new System.Collections.Generic.List<int>();
                for (int i = 0; i < j.Instances.Length; i++)
                    if (j.Instances[i].Alive) { count[j.Instances[i].DomainSlot]++; live.Add(i); }
                int same = 0;
                foreach (int i in live)
                {
                    int best = -1; float bd = float.MaxValue; var pi = j.Instances[i].CurPos;
                    foreach (int k in live) { if (k == i) continue; float d = Vector3.DistanceSquared(pi, j.Instances[k].CurPos); if (d < bd) { bd = d; best = k; } }
                    if (best >= 0 && j.Instances[best].DomainSlot == j.Instances[i].DomainSlot) same++;
                }
                double n = Math.Max(1, live.Count), rnd = 0; for (int d = 0; d < 3; d++) rnd += (count[d] / n) * (count[d] / n);
                return (count, seeds, same / n, rnd);
            }
            var on = RunMulti(SortCore, true);
            Console.WriteLine($"  MultiDomain on : {on.seeds} seeds; members by slot {on.count[0]}/{on.count[1]}/{on.count[2]}; " +
                              $"nearest neighbour shares the domain {on.sameNN:P0} (labels at random: {on.randomNN:P0})");
            Check(on.count[0] == on.seeds, "every seed - and only the seeds - wears slot 0 (the anchor's domain)");
            Check(on.count[1] > 0 && on.count[2] > 0, "newborns wear the slot of the mass that funded them (slot-1 food, then slot-2 food)");
            Check(on.sameNN > on.randomNN + 0.1, "domains sort into regions (nearest neighbour shares the domain well above chance)");
            var off = RunMulti(SortCore, false);
            Console.WriteLine($"  MultiDomain off: members by slot {off.count[0]}/{off.count[1]}/{off.count[2]}");
            Check(off.count[1] == 0 && off.count[2] == 0 && off.count[0] > off.seeds,
                  "negative control: the one-colour swarm fed the SAME mixed-domain food stays one colour");
            foreach (var (label, make) in new (string, Func<bool, ISwarmCore>)[] { ("grid", GridCore), ("field", FieldCore) })
            {
                var m = RunMulti(make, true); var o = RunMulti(make, false);
                Console.WriteLine($"  {label,-5} core: on {m.count[0]}/{m.count[1]}/{m.count[2]} ({m.seeds} seeds), off {o.count[0]}/{o.count[1]}/{o.count[2]}");
                Check(m.count[0] == m.seeds && m.count[1] > 0 && m.count[2] > 0 && o.count[1] == 0 && o.count[2] == 0,
                      $"the {label} core follows the same rule (and stays one colour with it off)");
            }
        }

        Console.WriteLine("\nR8f. the funding rule draws the domain split in proportion and names the slot that paid most");
        {
            var stomach = new float[] { 0f, 10f, 0f, 0f };
            var dom = new float[12]; dom[1 * 3 + 0] = 2f; dom[1 * 3 + 2] = 8f;   // Mass reserve: 2 of slot 0, 8 of slot 2
            bool ok1 = SwarmCoreShared.TryFund(stomach, dom, 1, 5f, 2f, out int s1);
            Check(ok1 && s1 == 2 && Math.Abs(dom[3] - 1f) < 1e-5 && Math.Abs(dom[5] - 4f) < 1e-5 && stomach[1] == 5f,
                  $"an egg costing half the reserve draws half of each domain's share and is the majority's (slot {s1})");
            var st2 = new float[] { 0f, 0f, 3f, 0f }; var d2 = new float[12]; d2[2 * 3 + 1] = 3f;
            bool ok2 = SwarmCoreShared.TryFund(st2, d2, 1, 1f, 2f, out int s2);   // cross-element: Space pays for a Mass egg
            Check(ok2 && s2 == 1 && Math.Abs(d2[7] - 1f) < 1e-5, $"a cross-element egg is credited to the domain of the reserve it drew (slot {s2})");
            var st3 = new float[] { 0f, 0f, 0f, 0f }; var d3 = new float[12];
            Check(!SwarmCoreShared.TryFund(st3, d3, 0, 1f, 2f, out int s3) && s3 == -1, "an unaffordable egg spends nothing and names no slot");
            var st4 = new float[] { 5f, 0f, 0f, 0f };
            Check(SwarmCoreShared.TryFund(st4, null, 0, 1f, 2f, out int s4) && s4 == -1, "a one-colour swarm (no split) names no slot: the parent's domain stands");
        }

        Console.WriteLine(_fail == 0 ? "\ntick job: OK" : $"\ntick job: {_fail} FAILED");
        return _fail;
    }

    sealed class ThrowingCore : ISwarmCore
    {
        public int Cap => 4;
        public Vector3[] Pos { get; } = new Vector3[4];
        public Vector3[] Vel { get; } = new Vector3[4];
        public Vector3[] Facing { get; } = new Vector3[4];
        public int[] Elem { get; } = new int[4];
        public bool[] Alive { get; } = new bool[4];
        public float[] Startle { get; } = new float[4];
        public float[] Molt { get; } = new float[4];
        public int[] MoltTo { get; } = new int[4];
        public float[] Stomach { get; } = new float[4];
        public float[] StomachDom { get; } = new float[12];
        public int[] Dom { get; } = new int[4];
        public System.Collections.Generic.List<SwarmEvent> Events { get; } = new();
        public int Clock => 0;
        public int PlanIx => 0;
        public SwarmPlanData Plan => null;
        public int AliveCount => 0;
        public Vector3 Anchor => Vector3.Zero;
        public Vector3 BX => Vector3.UnitX;
        public Vector3 BY => Vector3.UnitY;
        public Vector3 BZ => Vector3.UnitZ;
        public Vector3 SwimTarget { get; set; }
        public void Seed(int planElement, int count, Vector3 anchor, Vector3 heading) { }
        public void Step(ReadOnlySpan<SwarmPredator> preds) => throw new InvalidOperationException("boom");
        public void Kill(int i) { }
        public int EffectiveElement(int i) => 0;
        public int[] Counts(bool eff) => new int[4];
        public bool TryGetLook(int i, int element, out Vector3 half, out int tier) { half = default; tier = 0; return false; }
        public int StarvationVictim() => -1;
    }
}
