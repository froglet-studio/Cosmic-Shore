// Round 7 (Docs/SWARM_FAUNA.md §14): the SHIPPED SwarmTickJob - the swarm's tick off the main thread -
// run headless. R7a..R7g are asserted; `run.sh tickjob <plans>` runs them (SWARM_DENSITY=5 is the cell).
// Round 11a (Docs/SWARM_FAUNA.md §19): R11b-R11e - the stored index point, the count-once entry ledger, the render
// entity's pose matrix and its per-frame cost, and the entity ledger - on the same live jobs.
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

        Console.WriteLine("\nR11b. the index's stored point vs where the body is DRAWN (Docs/SWARM_FAUNA.md §19.1)");
        {
            var j = Make(plans, 1, 5);
            for (int t = 0; t < 120; t++) Tick(j, inline: true);
            bool same = true; int alive = 0;
            double maxEps = 0, sumEps = 0, maxFrac = 0, sumHeart = 0, worstOverBound = 0; long nEps = 0, outside = 0;
            var eps = new System.Collections.Generic.List<double>();
            for (int i = 0; i < j.Instances.Length; i++)
            {
                if (!j.Instances[i].Alive) continue;
                alive++;
                var stored = j.IndexPoint[i];
                if (stored != SwarmBodyPose.Body(j.Instances[i], SwarmBodyPose.IndexAlpha) || stored != j.BodyAt(i, 0.5f)) same = false;
                float br = SwarmBodyPose.BoundingRadius(j.Instances[i].Scale);
                // the analytic bound §19.1 states: half the step's travel, plus the seat swung by the face's turn
                var si = j.Instances[i];
                var f5 = SwarmBodyPose.Face(si, 0.5f);
                double bound = 0.5 * Vector3.Distance(si.PrevPos, si.CurPos)
                             + Math.Abs(si.PrismZ) * Math.Max(Vector3.Distance(SwarmBodyPose.Face(si, 0f), f5), Vector3.Distance(SwarmBodyPose.Face(si, 1f), f5));
                for (int a = 0; a <= 10; a++)
                {
                    float alpha = a / 10f;
                    double e = Vector3.Distance(j.BodyAt(i, alpha), stored);
                    eps.Add(e); sumEps += e; nEps++;
                    if (e > maxEps) maxEps = e;
                    if (br > 0 && e / br > maxFrac) maxFrac = e / br;
                    if (e > br) outside++;
                    worstOverBound = Math.Max(worstOverBound, e - bound);
                    // negative control: a stored HEART (round 7's CurPos) instead of the body centre
                    sumHeart += Vector3.Distance(j.BodyAt(i, alpha), j.Instances[i].CurPos);
                }
            }
            eps.Sort();
            double p99 = eps.Count > 0 ? eps[(int)(eps.Count * 0.99)] : 0;
            Console.WriteLine($"  {alive} members x 11 display alphas: |drawn body - stored point| mean {sumEps / Math.Max(1, nEps):F3} u, p99 {p99:F3} u, max {maxEps:F3} u");
            Console.WriteLine($"  vs the body's own bounding radius: max {maxFrac:F2} radii, {outside * 100.0 / Math.Max(1, nEps):F2}% of (member, alpha) outside it; " +
                              $"a stored HEART instead would be off by {sumHeart / Math.Max(1, nEps):F2} u on average");
            Check(same && alive > 0, "the published IndexPoint is exactly SwarmBodyPose.Body(alpha 0.5) == SwarmTickJob.BodyAt(i, 0.5) - what the glue pushes");
            Check(worstOverBound <= 1e-3, "every displacement is within the stated bound: 0.5 |step| + |PrismZ| x the face's half-tick turn");
            Check(outside * 100.0 / Math.Max(1, nEps) < 1.0, "fewer than 1% of (member, display alpha) put the stored point outside the drawn body's bounding sphere");
            Check(sumHeart > 5 * sumEps, "negative control: storing the heart (no PrismZ seat) misplaces the body several times worse on average");

            // what UpdatePositionsBatch pays per tick: a bucket move (remove + add in the index's 8 u hash) for each
            // member whose stored point crossed a bucket boundary, a field write for the rest (§19.4's cost line)
            var before = (Vector3[])j.IndexPoint.Clone(); var wasAlive = j.Instances.Select(x => x.Alive).ToArray();
            int crossed = 0, moved = 0;
            const int CrossTicks = 20;
            for (int t = 0; t < CrossTicks; t++)
            {
                Tick(j, inline: true);
                for (int i = 0; i < j.Instances.Length; i++)
                {
                    if (!j.Instances[i].Alive || !wasAlive[i]) continue;
                    moved++;
                    var a0 = before[i] / 8f; var a1 = j.IndexPoint[i] / 8f;
                    if (MathF.Floor(a0.X) != MathF.Floor(a1.X) || MathF.Floor(a0.Y) != MathF.Floor(a1.Y) || MathF.Floor(a0.Z) != MathF.Floor(a1.Z)) crossed++;
                }
                before = (Vector3[])j.IndexPoint.Clone(); wasAlive = j.Instances.Select(x => x.Alive).ToArray();
            }
            Console.WriteLine($"  per tick, {crossed * 100.0 / Math.Max(1, moved):F1}% of members' stored points cross an 8 u index bucket (a remove + add each)");
        }

        Console.WriteLine("\nR11c. the entry ledger against a model of the index's virtual-entry contract: every member counted ONCE (§19.1)");
        {
            var j = Make(plans, 1, 5);
            int cap = j.Instances.Length;
            var idx = new FakeVirtualIndex();
            var led = new SwarmEntryLedger(cap);
            var real = new bool[cap];
            var gone = new bool[cap];
            var rng = new Random(19);
            float egg = SortHarness.Game(plans).EggCost[1];
            int violations = 0, reuses = 0, kills = 0, suspends = 0, resumes = 0, indexMaterialised = 0, ticks = 0;
            double worstVol = 0;
            var lastBirth = new float[cap]; var lastId = new int[cap];
            for (int i = 0; i < cap; i++) lastId[i] = -1;
            for (int t = 0; t < 400; t++)
            {
                if (t % 8 == 0) j.QueueDeposit(1, 4f * egg);   // fed, so the killed slots are laid into again
                Tick(j, inline: true);
                // MaskGone: a member killed between ticks stays dead in the frame until the kill is applied
                for (int i = 0; i < cap; i++)
                    if (gone[i]) { if (!j.Instances[i].Alive || j.BornThisTick(i)) gone[i] = false; else j.Instances[i].Flags = 0u; }
                // proxies come and go (a real body finished creation / retired): the ledger follows realBody
                for (int i = 0; i < cap; i++)
                {
                    bool alive = j.Instances[i].Alive;
                    if (!alive) { real[i] = false; continue; }
                    if (!real[i] && rng.NextDouble() < 0.03) { real[i] = true; suspends++; }
                    else if (real[i] && rng.NextDouble() < 0.10) { real[i] = false; resumes++; }
                }
                // SyncIndex: push the stored points of existing entries, then the ledger
                for (int i = 0; i < cap; i++) if (led.Ids[i] >= 0) idx.Move(led.Ids[i], j.IndexPoint[i]);
                // a slot that held a member before and now holds a NEW one (a different birth tick)
                for (int i = 0; i < cap; i++)
                    if (j.Instances[i].Alive && lastId[i] >= 0 && j.Instances[i].BirthTick != lastBirth[i]) reuses++;
                led.Sync(j.Instances, real, j.IndexPoint, idx);
                for (int i = 0; i < cap; i++)
                    if (j.Instances[i].Alive) { lastId[i] = led.Ids[i]; lastBirth[i] = j.Instances[i].BirthTick; }
                ticks++;
                violations += CountOnce(j, led, idx, real, out double volErr);
                worstVol = Math.Max(worstVol, volErr);
                if (idx.LiveOrSuspended != led.Registered) violations++;

                // between ticks: an AOE materialises a few members (the INDEX suspends), and a few die (HandleMemberDeath)
                for (int k = 0; k < 3; k++)
                {
                    int m = rng.Next(cap);
                    if (!j.Instances[m].Alive || led.Ids[m] < 0 || led.IsSuspended(m)) continue;
                    idx.SetSuspended(led.Ids[m], true);   // PrismSpatialIndex.MaterialiseVirtual suspends first
                    led.NoteSuspendedByIndex(m); real[m] = true; indexMaterialised++;
                }
                for (int k = 0; k < 2; k++)
                {
                    int m = rng.Next(cap);
                    if (!j.Instances[m].Alive || gone[m]) continue;
                    j.QueueKill(m); gone[m] = true; j.Instances[m].Flags = 0u;
                    led.Release(m, idx); real[m] = false; kills++;
                }
                violations += CountOnce(j, led, idx, real, out volErr);
                worstVol = Math.Max(worstVol, volErr);
            }
            Console.WriteLine($"  {ticks} ticks: {kills} kills, {reuses} reused slots re-registered, {suspends} proxies made / {resumes} retired, " +
                              $"{indexMaterialised} materialised by the index; {led.Registered} entries for {j.AliveCount} members");
            Console.WriteLine($"  count-once violations {violations}; worst |index volume + real bodies - every body| {worstVol:E2}");
            Check(violations == 0 && kills > 0 && reuses > 0 && suspends > 0 && resumes > 0 && indexMaterialised > 0,
                  "after every sync and every between-tick event: each living member is seen exactly once (live entry XOR real body), the dead never");
            Check(worstVol < 1e-6, "the cell's sum (live virtual volume + real body prisms) is every member's body exactly once");
            Check(idx.MaxId < cap + 64, $"entry ids are recycled, not leaked (highest id {idx.MaxId} for a {cap}-slot swarm)");

            // negative control: a ledger told nothing about proxies counts every proxied body twice
            var j2 = Make(plans, 1, 5);
            for (int t = 0; t < 60; t++) Tick(j2, inline: true);
            var idx2 = new FakeVirtualIndex(); var led2 = new SwarmEntryLedger(cap); var real2 = new bool[cap];
            for (int i = 0; i < cap; i++) real2[i] = j2.Instances[i].Alive && i % 5 == 0;
            led2.Sync(j2.Instances, null, j2.IndexPoint, idx2);
            int bad2 = CountOnce(j2, led2, idx2, real2, out double over);
            Console.WriteLine($"  negative control (realBody withheld): {bad2} members seen twice, volume over by {over * 100:F1}%");
            Check(bad2 > 0 && over > 0, "negative control fires: without the proxy suspension the index and the real bodies double-count");
        }

        Console.WriteLine("\nR11c-2. a puffed shield member stays shielded in the index (round 11d-2): the look shows danger, the mass keeps its shield");
        {
            var core = SortHarness.GameSwarm(plans, 2, 11, true, SortHarness.Game(plans));   // the Space body: its Charge members wear the shield
            var j = new SwarmTickJob(core, Settings(), 10f) { SwimTarget = core.SwimTarget };
            j.Prime();
            for (int t = 0; t < 300; t++) Tick(j, inline: true);
            int cap = j.Instances.Length;
            var idx = new FakeVirtualIndex(); var led = new SwarmEntryLedger(cap);
            float R = core.Plan.Radius;
            // aim the charge at the shield members' own centroid (the Space body's Charge members sit on its flank)
            var c0 = Vector3.Zero; int ns = 0;
            for (int i = 0; i < cap; i++) if (j.Instances[i].Alive && j.Instances[i].Shielded) { c0 += core.Pos[i]; ns++; }
            c0 = ns > 0 ? c0 / ns : core.Anchor;
            float maxSt = 0f;
            int puffedShield = 0, shieldTicks = 0, ledgerWrong = 0, oldRuleWrong = 0, flips = 0, violations = 0;
            var lastShield = new bool[cap]; var lastBirth = new float[cap];
            for (int t = 0; t < 160; t++)
            {
                // a vessel charges through the body (ticks 20-59) and leaves: members puff above DangerEnter, then calm
                // ticks 20-59 a vessel hounds the shield members; then it leaves
                bool rush = t >= 20 && t < 60;
                if (rush)
                {
                    // it noses at one shield member at a time (the first living one), so the case exists at any density
                    for (int i = 0; i < cap; i++) if (j.Instances[i].Alive && j.Instances[i].Shielded) { c0 = core.Pos[i] - new Vector3(1f, 0, 0); break; }
                }
                Vector3 pc = rush ? c0 : c0 + new Vector3(100f * R, 0, 0);
                j.Preds[0] = new SwarmPredator { C = pc, V = rush ? new Vector3(1f, 0, 0) : Vector3.Zero, R = 4.5f };
                j.PredCount = 1;
                Tick(j, inline: true);
                for (int i = 0; i < cap; i++) if (led.Ids[i] >= 0) idx.Move(led.Ids[i], j.IndexPoint[i]);
                led.Sync(j.Instances, null, j.IndexPoint, idx);
                violations += CountOnce(j, led, idx, new bool[cap], out _);
                for (int i = 0; i < cap; i++)
                {
                    var s = j.Instances[i];
                    if (!s.Alive) { lastShield[i] = false; continue; }
                    if (s.Shielded) maxSt = Math.Max(maxSt, core.Startle[i]);
                    if (t == 0) { lastShield[i] = s.Shielded; lastBirth[i] = s.BirthTick; }
                    if (s.Shielded) shieldTicks++;
                    if (s.Shielded && s.Tier == 1) puffedShield++;
                    if (led.Ids[i] < 0 || idx.E[led.Ids[i]].shield != s.Shielded) ledgerWrong++;
                    if (s.Shielded && s.Tier != 2) oldRuleWrong++;   // what round 11a's `shield = Tier == 2` would have filed
                    if (s.BirthTick == lastBirth[i] && s.Shielded != lastShield[i] && s.CurMolt <= 0f) flips++;
                    lastShield[i] = s.Shielded; lastBirth[i] = s.BirthTick;
                }
            }
            Console.WriteLine($"  {shieldTicks} shielded member-ticks, {puffedShield} of them puffed (showing danger); index entries filed wrong {ledgerWrong}; " +
                              $"round 11a's rule would have filed {oldRuleWrong} unshielded; shield changes outside a molt {flips}; peak startle on a shield member {maxSt:F2}");
            Check(puffedShield > 0, "the charge puffs shield members (tier 1 shown) - the case exists");
            Check(ledgerWrong == 0 && violations == 0, "every index entry's shield bit is the member's mass shield, puffed or not; every member counted once");
            Check(oldRuleWrong == puffedShield && oldRuleWrong > 0, "negative control: the old Tier == 2 rule files every puffed shield member unshielded");
            Check(flips == 0, "a member's shield does not blink with its puff");
            var x = new SwarmInstance { Flags = SwarmInstance.Pack(true, 1, 3, 2, 2, true) };
            var y = new SwarmInstance { Flags = SwarmInstance.Pack(true, 2, 3, 2, 2) };
            Check(x.Flags == (y.Flags & ~(3u << 1) | (1u << 1) | (1u << 9)) && x.Shielded && !y.Shielded
                  && x.Tier == 1 && x.HeartFrom == 3 && x.HeartTo == 2 && x.DomainSlot == 2,
                  "the shield is bit 9, beside the tier, domain and hearts the shader reads (bits 0-8 unchanged)");
        }

        Console.WriteLine("\nR11d. the render entity's matrix is the shipped body pose, and its per-frame cost (§19.2)");
        {
            var j = Make(plans, 1, 5);
            for (int t = 0; t < 120; t++) Tick(j, inline: true);
            var rng = new Random(5);
            var m = new float[16];
            double worst = 0, worstOrtho = 0, worstScale = 0; int n = 0;
            for (int i = 0; i < j.Instances.Length; i++)
            {
                if (!j.Instances[i].Alive) continue;
                float alpha = (float)rng.NextDouble();
                var s = j.Instances[i];
                SwarmBodyPose.Matrix(s, alpha, s.BirthTick + 1000f, 5f, j.BY, j.BZ, m, 0);
                var centre = new Vector3(m[12], m[13], m[14]);
                worst = Math.Max(worst, Vector3.Distance(centre, j.BodyAt(i, alpha)));
                var c0 = new Vector3(m[0], m[1], m[2]); var c1 = new Vector3(m[4], m[5], m[6]); var c2 = new Vector3(m[8], m[9], m[10]);
                worstOrtho = Math.Max(worstOrtho, Math.Abs(Vector3.Dot(c0, c1)) + Math.Abs(Vector3.Dot(c1, c2)) + Math.Abs(Vector3.Dot(c0, c2)));
                worstScale = Math.Max(worstScale, Math.Abs(c0.Length() - Math.Abs(s.Scale.X)) + Math.Abs(c1.Length() - Math.Abs(s.Scale.Y)) + Math.Abs(c2.Length() - Math.Abs(s.Scale.Z)));
                // the forward column is the drawn face: a vertex at local +z sits further along the face than the centre
                var face = SwarmBodyPose.Face(s, alpha);
                if (Vector3.Dot(Vector3.Normalize(c2), face) < 0.999f) worst = Math.Max(worst, 1.0);
                n++;
            }
            Console.WriteLine($"  {n} members: |matrix centre - BodyAt| max {worst:E2} u, basis orthogonality error {worstOrtho:E2}, |column| - |Scale| {worstScale:E2}");
            Check(n > 0 && worst < 1e-3, "the matrix's translation is the drawn body centre (the same point the proxy body and the index use) and +z is the face");
            Check(worstOrtho < 1e-3 && worstScale < 1e-3, "the basis is orthogonal and each column is the body's own scale (LookRotation(face, up) x Scale)");

            // round 11a-2: the shipped pose is now SwarmBodyPose.PoseMatrix - scalar, Burst-compilable, called by BOTH the
            // game's SwarmPoseJob and this harness. Against round 11a's System.Numerics formulation (kept here as the
            // reference), over every member, many alphas, both basis branches and newborns:
            double worstRef = 0; int refN = 0; bool sameStruct = true;
            var mr = new float[16];
            for (int i = 0; i < j.Instances.Length; i++)
            {
                if (!j.Instances[i].Alive) continue;
                for (int a = 0; a <= 8; a++)
                {
                    var s = j.Instances[i];
                    float alpha = a / 8f, clock = s.BirthTick + a * 0.7f;
                    var up = a == 8 ? Vector3.Normalize(s.CurFace) : j.BY;   // forces the upAlt branch once per member
                    SwarmBodyPose.Matrix(s, alpha, clock, 5f, up, j.BZ, m, 0);
                    ReferenceMatrix(s, alpha, clock, 5f, up, j.BZ, mr);
                    for (int c = 0; c < 16; c++) worstRef = Math.Max(worstRef, Math.Abs(m[c] - mr[c]));
                    SwarmBodyPose.PoseMatrix(s, alpha, clock, 5f, up, j.BZ, out var pm);
                    if (pm.C3X != m[12] || pm.C2Z != m[10] || pm.C0Y != m[1]) sameStruct = false;
                    refN++;
                }
            }
            Console.WriteLine($"  PoseMatrix vs the round-11a System.Numerics pose: {refN} poses, max |difference| {worstRef:E2}");
            Check(worstRef < 1e-4 && sameStruct, "the one shared pose function (Burst job + harness) is round 11a's proven pose, to float rounding");

            // the newborn's bloom and a dead slot
            int born = Enumerable.Range(0, j.Instances.Length).First(i => j.Instances[i].Alive);
            var sb = j.Instances[born];
            SwarmBodyPose.Matrix(sb, 0f, sb.BirthTick, 5f, j.BY, j.BZ, m, 0);
            float bloom0 = new Vector3(m[8], m[9], m[10]).Length() / Math.Max(1e-6f, Math.Abs(sb.Scale.Z));
            SwarmBodyPose.Matrix(sb, 0f, sb.BirthTick + 2.5f, 5f, j.BY, j.BZ, m, 0);
            float bloomMid = new Vector3(m[8], m[9], m[10]).Length() / Math.Max(1e-6f, Math.Abs(sb.Scale.Z));
            var dead = sb; dead.Flags = 0u;
            SwarmBodyPose.Matrix(dead, 0.5f, 0f, 5f, j.BY, j.BZ, m, 0);
            Console.WriteLine($"  bloom: {bloom0:F4} at birth, {bloomMid:F3} half way (SwarmSmooth01 -> 0.5)");
            Check(Math.Abs(bloom0 - 0.001f) < 1e-4 && Math.Abs(bloomMid - 0.5f) < 1e-3 && m.All(x => x == 0f),
                  "a newborn blooms from 0.001 about its HEART (the shader's smoothstep), a dead slot writes the zero matrix");

            // the cost: ~3,000 shown members, the pose pass + the copy into the render service's array
            var big = new SwarmInstance[3000]; var slots = new int[3000];
            for (int k = 0; k < big.Length; k++) { big[k] = j.Instances[Enumerable.Range(0, j.Instances.Length).Where(i => j.Instances[i].Alive).ElementAt(k % Math.Max(1, j.AliveCount))]; slots[k] = k; }
            var mats = new float[16 * big.Length]; var dst = new float[16 * big.Length];
            for (int w = 0; w < 50; w++) SwarmBodyPose.Matrices(big, slots, big.Length, 0.3f, 1000f, 5f, j.BY, j.BZ, mats);
            const int F = 400;
            var sw = Stopwatch.StartNew();
            for (int f = 0; f < F; f++)
            {
                SwarmBodyPose.Matrices(big, slots, big.Length, (f % 10) / 10f, 1000f + f, 5f, j.BY, j.BZ, mats);
                Array.Copy(mats, dst, mats.Length);
            }
            double ms = sw.Elapsed.TotalMilliseconds / F;
            Console.WriteLine($"  {big.Length} members: the SAME pose function, managed (CoreCLR, one thread) + copy: {ms:F3} ms per frame - the " +
                              "game runs it Burst-compiled across the job workers instead (§19.4)");
            Check(ms < 2.0, "the per-frame pose for ~3,000 members is cheap even unBursted (measured, recorded in §19.4)");
            // the job's SHAPE (IJobParallelFor, batches of SwarmPoseJob's 128) on the .NET pool - an upper bound on its wall
            // time without Burst's codegen; what the main thread waits for if nothing overlaps it
            var poseOut = new SwarmPoseMatrix[big.Length];
            int batches = (big.Length + 127) / 128;
            System.Threading.Tasks.Parallel.For(0, batches, bt => { });
            sw.Restart();
            for (int f = 0; f < F; f++)
            {
                float al = (f % 10) / 10f, ck = 1000f + f;
                System.Threading.Tasks.Parallel.For(0, batches, bt =>
                {
                    int end = Math.Min(big.Length, (bt + 1) * 128);
                    for (int k = bt * 128; k < end; k++) SwarmBodyPose.PoseMatrix(big[slots[k]], al, ck, 5f, j.BY, j.BZ, out poseOut[k]);
                });
            }
            double pms = sw.Elapsed.TotalMilliseconds / F;
            Console.WriteLine($"  the job's shape on the .NET pool ({Environment.ProcessorCount} cores, batches of 128, no Burst): {pms:F3} ms wall per frame");
        }

        Console.WriteLine("\nR11e. the entity ledger: one entity per slot, shown exactly while the member lives (§19.2)");
        {
            var j = Make(plans, 1, 5);
            int cap = j.Instances.Length;
            var led = new SwarmEntityLedger(cap);
            var made = new int[cap];
            var rng = new Random(23);
            int bad = 0, shows = 0, hides = 0, hiddenNow = 0, failedOnce = 0;
            var gone = new bool[cap];
            for (int t = 0; t < 300; t++)
            {
                Tick(j, inline: true);
                for (int i = 0; i < cap; i++)
                    if (gone[i]) { if (!j.Instances[i].Alive || j.BornThisTick(i)) gone[i] = false; else j.Instances[i].Flags = 0u; }
                led.Sync(j.Instances);
                bool ok = !(t == 3 && led.Create.Count > 0);   // one batch fails: those slots are retried next tick
                if (!ok) failedOnce = led.Create.Count;
                if (ok) foreach (var c in led.Create) made[c]++;
                led.Created(ok);
                shows += led.Show.Count; hides += led.Hide.Count;
                var shown = new System.Collections.Generic.HashSet<int>();
                for (int k = 0; k < led.ShownCount; k++) shown.Add(led.Shown[k]);
                for (int i = 0; i < cap; i++)
                {
                    bool alive = j.Instances[i].Alive;
                    if (alive != shown.Contains(i) || alive != led.Visible[i]) bad++;
                    if (alive && t != 3 && !led.HasEntity[i]) bad++;
                }
                if (shown.Count != led.ShownCount) bad++;
                for (int k = 0; k < 3; k++)
                {
                    int m = rng.Next(cap);
                    if (!j.Instances[m].Alive || gone[m]) continue;
                    j.QueueKill(m); gone[m] = true; j.Instances[m].Flags = 0u;
                    if (led.HideNow(m)) hiddenNow++;
                    if (led.HideNow(m)) bad++;   // a second HideNow is a no-op
                }
            }
            int twice = made.Count(c => c > 1);
            Console.WriteLine($"  300 ticks: {made.Count(c => c > 0)} entities made for {cap} slots, {shows} shows, {hides} hides, {hiddenNow} hidden at death, failed batch of {failedOnce} retried");
            Check(bad == 0 && twice == 0 && hiddenNow > 0 && failedOnce > 0, "shown == alive every tick, an entity is made once per slot and reused, a death hides at once");
        }

        Console.WriteLine(_fail == 0 ? "\ntick job: OK" : $"\ntick job: {_fail} FAILED");
        return _fail;
    }

    /// <summary>
    /// A model of PrismSpatialIndex's virtual-entry contract as SwarmEntryLedger meets it: ids from a free list (an id is
    /// recycled after Unregister, as the index's slots are), an entry either live (seen by every query and the cell's
    /// volume sum) or suspended (seen by nothing - the member's real body prism is), and Unregister removing it entirely.
    /// Any call on a dead id is recorded as a violation.
    /// </summary>
    sealed class FakeVirtualIndex : ISwarmEntrySink
    {
        public readonly System.Collections.Generic.Dictionary<int, (int slot, Vector3 p, int dom, float vol, bool shield, bool susp)> E = new();
        readonly System.Collections.Generic.Stack<int> _free = new();
        int _next;
        public int Misuse, MaxId;
        public int LiveOrSuspended => E.Count;
        public int Register(int slot, Vector3 point, int domainSlot, float volume, bool shielded, float radius)
        {
            int id = _free.Count > 0 ? _free.Pop() : _next++;
            MaxId = Math.Max(MaxId, id);
            E[id] = (slot, point, domainSlot, volume, shielded, false);
            return id;
        }
        public void Release(int id) { if (!E.Remove(id)) Misuse++; else _free.Push(id); }
        public void SetSuspended(int id, bool suspended) { if (!E.TryGetValue(id, out var e)) { Misuse++; return; } e.susp = suspended; E[id] = e; }
        public void SetShape(int id, float volume, float radius) { if (!E.TryGetValue(id, out var e)) { Misuse++; return; } e.vol = volume; E[id] = e; }
        public void SetShielded(int id, bool shielded) { if (!E.TryGetValue(id, out var e)) { Misuse++; return; } e.shield = shielded; E[id] = e; }
        public void SetDomainSlot(int id, int domainSlot) { if (!E.TryGetValue(id, out var e)) { Misuse++; return; } e.dom = domainSlot; E[id] = e; }
        public void Move(int id, Vector3 p) { if (!E.TryGetValue(id, out var e)) { Misuse++; return; } e.p = p; E[id] = e; }
    }

    /// <summary>Members seen other than exactly once (by a live entry XOR their real body), plus entries for no living
    /// member; <paramref name="volErr"/> = |live entry volume + real body volume - every living body|.</summary>
    static int CountOnce(SwarmTickJob j, SwarmEntryLedger led, FakeVirtualIndex idx, bool[] real, out double volErr)
    {
        int cap = j.Instances.Length, bad = idx.Misuse;
        idx.Misuse = 0;
        var seen = new int[cap];
        double counted = 0, truth = 0;
        foreach (var kv in idx.E)
        {
            var e = kv.Value;
            if (e.susp) continue;
            if (e.slot < 0 || e.slot >= cap || !j.Instances[e.slot].Alive) { bad++; continue; }
            seen[e.slot]++; counted += e.vol;
            if (Math.Abs(e.vol - SwarmBodyPose.BodyVolume(j.Instances[e.slot].Scale)) > 1e-3) bad++;
            if (e.dom != j.Instances[e.slot].DomainSlot || e.shield != j.Instances[e.slot].Shielded) bad++;
        }
        for (int i = 0; i < cap; i++)
        {
            if (!j.Instances[i].Alive) { if (seen[i] != 0) bad++; continue; }
            double v = SwarmBodyPose.BodyVolume(j.Instances[i].Scale);
            truth += v;
            if (real[i]) { seen[i]++; counted += v; }
            if (seen[i] != 1) bad++;
        }
        volErr = Math.Abs(counted - truth) / Math.Max(1.0, truth);
        return bad;
    }

    /// <summary>Round 11a's body pose (System.Numerics vector maths), kept as the harness's REFERENCE for the scalar
    /// SwarmBodyPose.PoseMatrix the game now Burst-compiles (R11d).</summary>
    static void ReferenceMatrix(in SwarmInstance s, float alpha, float clock, float bloomTicks, Vector3 up, Vector3 upAlt, float[] m)
    {
        if ((s.Flags & 1u) == 0u) { Array.Clear(m, 0, 16); return; }
        var p = Vector3.Lerp(s.PrevPos, s.CurPos, alpha);
        var face = Vector3.Lerp(s.PrevFace, s.CurFace, alpha);
        if (face.Length() < 1e-5f) face = s.CurFace;
        float l = face.Length();
        var bz = l > 1e-5f ? face / l : new Vector3(0, 0, 1);
        if (MathF.Abs(Vector3.Dot(bz, up)) > 0.98f) up = upAlt;
        var bx = Vector3.Cross(up, bz);
        float lx = bx.Length();
        bx = lx > 1e-6f ? bx / lx : new Vector3(1, 0, 0);
        var by = Vector3.Cross(bz, bx);
        float x = Math.Clamp((clock - s.BirthTick) / Math.Max(bloomTicks, 1e-3f), 0f, 1f);
        float b = Math.Max(0.001f, x * x * (3f - 2f * x));
        var c0 = bx * (s.Scale.X * b); var c1 = by * (s.Scale.Y * b); var c2 = bz * (s.Scale.Z * b); var c3 = p + bz * (s.PrismZ * b);
        m[0] = c0.X; m[1] = c0.Y; m[2] = c0.Z; m[3] = 0f; m[4] = c1.X; m[5] = c1.Y; m[6] = c1.Z; m[7] = 0f;
        m[8] = c2.X; m[9] = c2.Y; m[10] = c2.Z; m[11] = 0f; m[12] = c3.X; m[13] = c3.Y; m[14] = c3.Z; m[15] = 1f;
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
        public void Translate(Vector3 d) { }
    }
}
