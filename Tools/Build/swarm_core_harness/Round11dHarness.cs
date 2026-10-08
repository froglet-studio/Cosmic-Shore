// Round 11d (Docs/SWARM_FAUNA.md §22), asserted: the post-cull jolt (R11d-J), the time -> space switch (R11d-T) and
// round 10's bestiary strike rules, hardened (R11d-B). Runs at the end of the sort suite (`run.sh <plans> sort`, and the
// full run). The jolt is measured with swarm_smooth's own LURCH (the worst step's p95 speed over the change's own median
// p95, SortFeelHarness.Lurch) on the research's events: the 4 standard switches by cull_to and a strike on every plan.
// The loss-8 half of the time -> space proof needs the research's torch scorer: score_sortfeel.py --assert-pass.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using CosmicShore.Gameplay;

static class Round11dHarness
{
    static int _fail;
    const float LURCH_OK = 2.5f;   // swarm_smooth.LURCH_OK

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static readonly (int k, int e)[] Events = { (1, 2), (2, 0), (0, 3), (3, 1), (0, -1), (1, -1), (2, -1), (3, -1) };

    static List<float> Lurches(SwarmPlanData[] plans, string mode, int[] seeds)
    {
        var l = new List<float>();
        foreach (int sd in seeds)
            foreach (var (k, e) in Events)
            {
                var r = SortFeelHarness.JoltEvent(plans, mode, e < 0 ? sd + 1 : sd, k, e);
                if (!float.IsNaN(r.lurch)) l.Add(r.lurch);
            }
        return l;
    }

    public static int Run(SwarmPlanData[] plans)
    {
        _fail = 0;
        Jolt(plans);
        TimeToSpace(plans);
        Bestiary(plans);
        Console.WriteLine($"\nround 11d: {(_fail == 0 ? "OK" : $"FAIL ({_fail})")}");
        return _fail;
    }

    // ───────────────────────────────────────────────────────────── R11d-J: the post-cull jolt

    static void Jolt(SwarmPlanData[] plans)
    {
        Console.WriteLine("\nR11d-J. the post-cull jolt: swarm_smooth's lurch after every cull and strike (seeds 7, 23, 41; 24 events a config)");
        var seeds = new[] { 7, 23, 41 };
        // round 6's ship vs round 11d's, at the shipped schedule and at the two dials QA-SWARM-ROUND6 names
        foreach (var (dial, before, after, strict) in new[]
        {
            ("shipped (1-in-8, noise 0.1)", "gameSortFeelD0F8", "gameSortFeelWndFnLr120D0F8", true),
            ("dial SortUpdateFraction 4", "gameSortFeelD0F4", "gameSortFeelWndFnLr120D0F4", true),
            ("dial SortNoise 0", "gameSortFeelD0N0F8", "gameSortFeelWndFnLr120D0N0F8", false),
            ("every member every step", "gameSortFeelD0F1", "gameSortFeelWndFnLr120D0F1", false),
        })
        {
            var a = Lurches(plans, before, seeds); var b = Lurches(plans, after, seeds);
            int oa = a.Count(x => x > LURCH_OK), ob = b.Count(x => x > LURCH_OK);
            Console.WriteLine($"  {dial,-28} round 6: mean {a.Average():F2} worst {a.Max():F2} over {LURCH_OK} {oa}/{a.Count}   round 11d: mean {b.Average():F2} worst {b.Max():F2} over {ob}/{b.Count}");
            Check(b.Average() < a.Average() && ob <= oa, $"{dial}: round 11d eases every config in (lower mean lurch, no more jolts)");
            if (strict) Check(b.Max() <= LURCH_OK, $"{dial}: no event jolts past swarm_smooth's comfort line ({b.Max():F2} <= {LURCH_OK})");
        }
        // the cause, isolated: in the research's own sortfeel (every member every step, no noise) a body that does not
        // regrow does not jolt - the 4-7x is the regrowth
        {
            var with = Lurches(plans, "researchSortFeel", new[] { 7 });
            Console.WriteLine($"  research sortfeel (frac 1): mean lurch {with.Average():F2}, worst {with.Max():F2} - the research's 4-7x");
            Check(with.Average() > 4f, "the harness reproduces the research's jolt (sortfeel, mean lurch > 4)");
        }
        // a WOUND sets laying back in proportion to the share lost: one kill of many costs ~nothing, a third of the body all
        {
            int[] laid = new int[2]; float u0 = 0, u1 = 0, u2 = 0, share = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                var p = SortHarness.Game(plans); if (pass == 0) p.LayRamp = 0;   // pass 0: round 10's full cap at once
                var c = SortHarness.GameSwarm(plans, 1, 41, true, p); SortHarness.Run(c, 300);
                if (pass == 1) u0 = c.LayEase;
                int victim = Enumerable.Range(0, c.Cap).First(i => c.Active[i] && c.Hatched[i]);
                c.Kill(victim); if (pass == 1) u1 = c.LayEase;
                int n0 = c.AliveCount; int killed = SortFeelHarness.Strike(c, new Random(5));
                if (pass == 1) { u2 = c.LayEase; share = killed / (float)n0; }
                for (int t = 0; t < 28; t++) { SortHarness.Step(c); if (t >= 20) laid[pass] += c.Events.Count(ev => ev.Kind == SwarmEventKind.Laid); c.Events.Clear(); }
            }
            Console.WriteLine($"  grown whale: lay ease {u0:F2}; after one kill {u1:F3}; after a strike of {share:P0} of the body {u2:F2}; eggs in the 8 steps after its 20-step hold lifts: round 10 {laid[0]}, round 11d {laid[1]}");
            Check(u0 >= 0.999f && u1 > 0.97f, "one member picked off a grown body leaves laying at full rate (steady grazing is not throttled)");
            Check(MathF.Abs(u2 - MathF.Max(0f, u1 - 3f * share)) < 0.05f, "a strike sets laying back by three times the share it carved out (a third of the body: all the way)");
            Check(laid[1] <= 0.8f * laid[0], "no flood of eggs the moment the hold lifts: laying eases in (deterministic seed)");
        }
    }

    // ───────────────────────────────────────────────────────────── R11d-T: dragonfly -> jellyfish

    static float OrphanShare(SwarmSortCore c)
    {
        int n = 0, o = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { n++; if (c.RoleOfDom[Math.Clamp(c.Dom[i], 0, 2)] < 0) o++; }
        return (float)o / Math.Max(1, n);
    }

    static void TimeToSpace(SwarmPlanData[] plans)
    {
        Console.WriteLine("\nR11d-T. dragonfly -> jellyfish (time -> space): the share of the regrown body on a domain the jellyfish has no region for");
        var before = new List<float>(); var after = new List<float>();
        foreach (int seed in new[] { 7, 23, 101 })
            for (int r = 0; r < 3; r++)
            {
                int g = seed + 101 * r;
                foreach (var (mode, list) in new[] { ("researchSortFeelD0F8", before), ("researchSortFeelDom3D0F8", after) })
                {
                    var c = SortFeelHarness.Grow(plans, mode, 3, g);
                    if (!SortFeelHarness.CullTo(c, 2, new Random(g * 31 + 2))) continue;
                    SortHarness.Run(c, 240);
                    list.Add(OrphanShare(c));
                }
            }
        Console.WriteLine($"  sort's region map (ids 0..n-1): orphan share mean {before.Average():F2} [{string.Join(" ", before.Select(x => x.ToString("F2")))}]");
        Console.WriteLine($"  round 11d (any domain, orphans weighed): mean {after.Average():F2} [{string.Join(" ", after.Select(x => x.ToString("F2")))}]");
        Check(before.Average() > 0.2f, "the research's map orphans a fifth or more of the jellyfish (its loss floor is ~25 x that share)");
        Check(after.Max() <= 0.08f, "round 11d keeps the orphan team under 8% of the body on every sample (the dragonfly's smallest team)");
        // with no populated domain left out, the cost is sort's own: every own-plan body is bit-identical
        bool same = true;
        for (int k = 0; k < 4 && same; k++)
        {
            var a = SortFeelHarness.Grow(plans, "researchSortFeelD0F8", k, 7); var b = SortFeelHarness.Grow(plans, "researchSortFeelDom3D0F8", k, 7);
            for (int i = 0; i < a.Cap && same; i++) same &= a.Active[i] == b.Active[i] && a.Pos[i] == b.Pos[i] && a.Dom[i] == b.Dom[i];
        }
        Check(same, "every own-plan body grows bit-identical under both maps (the fix changes nothing until a team would be orphaned)");
    }

    // ───────────────────────────────────────────────────────────── R11d-B: round 10's strikes, hardened

    static SwarmTickSettings S() => new() { Bestiary = true, DangerEnter = 0.45f, DangerExit = 0.15f, HuntEnter = 0.2f, LurkCalm = 0.05f, LocustPhaseTicks = 20 };

    /// <summary>Round 10's rule verbatim (commit 52740cc7), for the before/after counts.</summary>
    static bool Round10(int eff, int i, long tick, float st, bool was, SwarmTickSettings s) => eff switch
    {
        1 => st > s.LurkCalm && st < s.DangerEnter,
        2 => ((i * 7919L + tick / Math.Max(1, s.LocustPhaseTicks)) & 3L) == 0L,
        3 => was ? st >= s.DangerExit : st > s.HuntEnter,
        _ => false,
    };

    static (int on, int toggles, int ticksOn) Drive(int eff, float[] st, SwarmTickSettings s, bool round10)
    {
        byte state = 0; bool was = false; int toggles = 0, on = 0, firstOn = -1;
        for (int t = 0; t < st.Length; t++)
        {
            bool now;
            if (round10) now = Round10(eff, 0, t, st[t], was, s);
            else { state = SwarmTickJob.StrikeState(eff, 0, t, st[t], state, s); now = state == 2; }
            if (now != was) toggles++;
            if (now) { on++; if (firstOn < 0) firstOn = t; }
            was = now;
        }
        return (firstOn, toggles, on);
    }

    static float[] Decay(float from, int n) { var a = new float[n]; float x = from; for (int t = 0; t < n; t++) { a[t] = x; x *= 0.9f; } return a; }   // SwarmSortParams.StartleDecay

    static void Bestiary(SwarmPlanData[] plans)
    {
        Console.WriteLine("\nR11d-B. round 10's strike rules (SwarmTickJob.StrikeState), hardened");
        var s = S();
        // a RUSH: the lurker bolts (startle jumps past DangerEnter) and decays home
        {
            var st = new float[60]; Array.Copy(Decay(0.95f, 55), 0, st, 5, 55);
            var r10 = Drive(1, st, s, true); var r11 = Drive(1, st, s, false);
            Console.WriteLine($"  lurker rushed (startle 0 -> 0.95, decay 0.9/tick): round 10 struck {r10.ticksOn} ticks after the bolt; round 11d {r11.ticksOn}");
            Check(r10.ticksOn > 10, "round 10 bit the pilot who rushed it, on the way back down (the bug)");
            Check(r11.ticksOn == 0, "round 11d: a lurker that bolts stays safe until it is calm");
        }
        // a CREEP: startle climbs slowly into the band and stays
        {
            var st = new float[80]; for (int t = 0; t < 80; t++) st[t] = MathF.Min(0.3f, 0.01f * t);
            var r = Drive(1, st, s, false);
            Console.WriteLine($"  lurker crept up on (startle +0.01/tick to 0.3): strikes from tick {r.on}, {r.ticksOn} ticks on, {r.toggles} toggles");
            Check(r.on > 0 && r.on <= 8 && r.toggles == 1, "creeping up on a lurker gets bitten (the plate rises once, a tick after it is noticed)");
        }
        // jitter at the band's edges: hysteresis, not flicker
        {
            var rng = new Random(3);
            foreach (var (eff, name, centre) in new[] { (1, "lurker at LurkCalm", 0.05f), (1, "lurker at DangerEnter", 0.45f), (3, "hunter at HuntEnter", 0.2f), (0, "pufferfish at DangerEnter", 0.45f) })
            {
                var st = new float[400]; for (int t = 0; t < 400; t++) st[t] = centre + 0.012f * (float)(rng.NextDouble() * 2 - 1);
                var r10 = Drive(eff, st, s, true); var r11 = Drive(eff, st, s, false);
                Console.WriteLine($"  {name,-26} +-0.012 for 400 ticks: plate toggles round 10 {r10.toggles}, round 11d {r11.toggles}");
                Check(r11.toggles <= 2, $"{name}: no flicker at a threshold (<= 2 toggles)");
            }
            var inv = S(); inv.DangerExit = 0.3f;   // a designer's exit ABOVE the hunter's entry
            var st2 = new float[400]; for (int t = 0; t < 400; t++) st2[t] = 0.25f + 0.04f * (float)(rng.NextDouble() * 2 - 1);
            var a10 = Drive(3, st2, inv, true); var a11 = Drive(3, st2, inv, false);
            Console.WriteLine($"  hunter, DangerExit 0.3 > HuntEnter 0.2, startle 0.25 +- 0.04: toggles round 10 {a10.toggles}, round 11d {a11.toggles}");
            Check(a11.toggles <= 1, "an inverted exit cannot invert the hysteresis");
        }
        // NaN and negative startle read as calm
        {
            bool ok = true;
            for (int eff = 0; eff < 4; eff++)
                foreach (byte state in new byte[] { 0, 1, 2, 3 })
                    foreach (float st in new[] { float.NaN, -1f, float.NegativeInfinity })
                    {
                        byte n = SwarmTickJob.StrikeState(eff, 5, 0, st, state, s);
                        if (eff != 2 && n == 2) ok = false;
                    }
            Check(ok, "a NaN or negative startle never raises a plate");
        }
        // locust: exactly one phase in four, a quarter of the cloud at any tick, switching only on phase boundaries
        {
            int slots = 400, P = s.LocustPhaseTicks; bool everyQuarter = true, exactlyOne = true, onBoundary = true;
            var prev = new byte[slots];
            for (long t = 0; t < 8 * P; t++)
            {
                int n = 0;
                for (int i = 0; i < slots; i++)
                {
                    byte b = SwarmTickJob.StrikeState(2, i, t, 0f, prev[i], s);
                    if (b == 2) n++;
                    if (t > 0 && b != prev[i] && t % P != 0) onBoundary = false;
                    prev[i] = b;
                }
                everyQuarter &= n >= 0.2f * slots && n <= 0.3f * slots;
            }
            for (int i = 0; i < slots; i++)
            {
                int k = 0; for (long ph = 0; ph < 4; ph++) if (SwarmTickJob.StrikeState(2, i, ph * P, 0f, 0, s) == 2) k++;
                exactlyOne &= k == 1;
            }
            Check(everyQuarter && exactlyOne && onBoundary, "locust: each member strikes one phase in four, a quarter of the cloud at a time, changing only on phase boundaries");
        }
        // the pack hunter's WIND-UP (lab fair burns, bestiary pack.py WINDUP 0.4 s = 4 ticks at 10 Hz): the plate goes up
        // only on the 4th tick the startle has shown above HuntEnter; a dip holds the count, a calm resets it
        {
            var w = S(); w.HuntWindupTicks = 4;
            byte Run(float[] seq) { byte x = 0; foreach (var v in seq) x = SwarmTickJob.StrikeState(3, 0, 0, v, x, w); return x; }
            bool firstAt4 = true;
            byte state = 0; int upAt = -1;
            for (int t = 0; t < 10 && upAt < 0; t++) { state = SwarmTickJob.StrikeState(3, 0, t, 0.5f, state, w); if (state == 2) upAt = t + 1; }
            firstAt4 &= upAt == 4;
            bool dipHolds = Run(new[] { 0.5f, 0.5f, 0.15f, 0.5f, 0.5f }) == 2;          // 4 shown ticks around a dip above 0.08
            bool calmResets = Run(new[] { 0.5f, 0.5f, 0.5f, 0.02f, 0.5f, 0.5f, 0.5f }) != 2;   // a calm in between: only 3 since
            var z = S(); z.HuntWindupTicks = 0;
            bool zeroIsRound10 = SwarmTickJob.StrikeState(3, 0, 0, 0.5f, 0, z) == 2;
            Check(firstAt4 && dipHolds && calmResets && zeroIsRound10,
                $"pack hunter wind-up: the plate goes up on the 4th shown tick (got {upAt}), a dip holds the count, a calm resets it, 0 strikes at once");
            // the pufferfish winds up the same way above DangerEnter (0.45; a calm is below 0.18)
            var pw = S(); pw.PuffWindupTicks = 4;
            byte state0 = 0; int puffAt = -1;
            for (int t = 0; t < 10 && puffAt < 0; t++) { state0 = SwarmTickJob.StrikeState(0, 0, t, 0.6f, state0, pw); if (state0 == 2) puffAt = t + 1; }
            byte held = 2; held = SwarmTickJob.StrikeState(0, 0, 0, 0.3f, held, pw);
            Check(puffAt == 4 && held == 2 && SwarmTickJob.StrikeState(0, 0, 0, 0.6f, 0, S()) == 2,
                $"pufferfish wind-up: the plate goes up on the 4th shown tick (got {puffAt}), a lit plate keeps its exit hysteresis, 0 puffs at once");
        }
        // off: no bestiary, only the pufferfish strikes
        {
            var off = S(); off.Bestiary = false; bool ok = true;
            for (int eff = 1; eff < 4; eff++) for (int t = 0; t < 40; t++) ok &= SwarmTickJob.StrikeState(eff, t, t, 0.3f, 2, off) == 0;
            Check(ok && SwarmTickJob.StrikeState(0, 0, 0, 0.9f, 0, off) == 2, "Bestiary off: Mass, Space and Time never strike; the pufferfish still does");
        }
        // the live job: a vessel creeps past a grown Mass body and then rushes it; every published tier agrees with the rule
        {
            var p = SortHarness.Game(plans);
            var core = SortHarness.GameSwarm(plans, 1, 61, true, p);
            var st = new SwarmTickSettings { Centre = Vector3.Zero, UnitScale = 2f, Bestiary = true, MultiDomain = false };
            var j = new SwarmTickJob(core, st, 10f) { SwimTarget = core.SwimTarget };
            j.Prime();
            for (int t = 0; t < 300; t++) Tick(j);
            int cap = core.Cap; var lastTier = new int[cap]; var flips = new int[cap]; int struck = 0, nan = 0, struckAfterRush = 0;
            var c0 = core.Anchor; float R = core.Plan.Radius;
            for (int t = 0; t < 160; t++)
            {
                // ticks 0-79: a vessel drifts slowly along the body's flank (creeping); 80-99: it charges through; then leaves
                Vector3 pc, pv;
                if (t < 80) { pc = c0 + new Vector3(-R + 0.03f * R * t, 1.6f * R, 0); pv = new Vector3(0.03f * R, 0, 0); }
                else if (t < 100) { pc = c0 + new Vector3(-2f * R + 0.2f * R * (t - 80), 0, 0); pv = new Vector3(0.2f * R, 0, 0); }
                else { pc = c0 + new Vector3(100f * R, 0, 0); pv = Vector3.Zero; }
                j.Preds[0] = new SwarmPredator { C = pc, V = pv, R = 4.5f }; j.PredCount = 1;
                Tick(j);
                for (int i = 0; i < cap; i++)
                {
                    var inst = j.Instances[i];
                    if (!inst.Alive) { lastTier[i] = 0; continue; }
                    if (float.IsNaN(inst.CurPos.X)) nan++;
                    int tier = inst.Tier;
                    if (tier == 1) { struck++; if (t >= 110) struckAfterRush++; }
                    if (tier != lastTier[i]) flips[i]++;
                    lastTier[i] = tier;
                }
            }
            int maxFlips = flips.Max();
            Console.WriteLine($"  live whale job: {struck} member-ticks struck during the pass, {struckAfterRush} after the vessel left; most plate changes on one member {maxFlips} in 160 ticks; NaN positions {nan}");
            Check(struck > 0, "a vessel nosing along a lurker's flank raises plates");
            Check(nan == 0, "no NaN in the published frame");
            Check(maxFlips <= 6, "no member's plate flickers (at most 3 rises in 16 s of creep, rush and calm)");
        }
    }

    static void Tick(SwarmTickJob j)
    {
        j.Kick(true);
        var spin = new SpinWait();
        while (j.State != SwarmJobState.Done) spin.SpinOnce();
        j.Collect();
        if (j.Error != null) throw j.Error;
    }
}
