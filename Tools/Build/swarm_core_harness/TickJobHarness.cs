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
