// Round 9 (Docs/SWARM_FAUNA.md §17): regional LINEAGES in the sort core, through the tick job the game runs.
//   bash Tools/Build/swarm_core_harness/run.sh <plans> lineage              # R9a-R9d, asserted
//   bash Tools/Build/swarm_core_harness/run.sh <plans> lineage <out.json>   # also exports grown bodies for the picture
// Food carries no domain any more: every deposit below is plain volume, exactly what SwarmFauna.Feed queues.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using CosmicShore.Gameplay;

static class LineageHarness
{
    static int _fail;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static SwarmTickSettings Settings() => new()
    {
        Centre = Vector3.Zero, UnitScale = 2f, PrismScale = 1f, HeartPrismGap = 0.6f,
        DefaultHalf = new[] { new Vector3(1f, 0.6f, 0.6f), new Vector3(0.9f, 0.8f, 0.7f), new Vector3(2f, 0.3f, 0.3f), new Vector3(0.8f, 0.4f, 0.4f) },
        MultiDomain = true,
    };

    public sealed class Run
    {
        public SwarmSortCore Core;
        public SwarmTickJob Job;
        public int Seeds, FoundedAt = -1, Founder = -1;
        public readonly List<int[]> Timeline = new();   // members by domain every 50 steps
    }

    /// <summary>A whale (or any plan) grown from the game's seed on plain food; after <paramref name="grow"/> steps,
    /// <paramref name="turnover"/> steps of the game's turnover (a vessel or predator takes a random member now and
    /// then, the stomach is topped up the way grazing tops it up).</summary>
    public static Run Grow(SwarmPlanData[] plans, int plan, int seed, bool lineages, int grow, int turnover, float killRate = 0.0003f)
    {
        var p = SortHarness.Game(plans);
        p.LayMax = Math.Max(p.LayMax, 5 * Math.Max(1, plans[1].N / 192));
        p.DomainSlots = lineages; p.Lineages = lineages;
        // the cell's seed: SEED_MEMBERS (48) per plan density unit (author_swarm_fauna.py)
        var core = new SwarmSortCore(plans, p, seed);
        core.Seed(plan, 48 * Program.Density, new Vector3(200, 0, 0), Vector3.UnitX);
        core.SwimTarget = core.Anchor;
        var st = Settings(); st.MultiDomain = lineages;
        var j = new SwarmTickJob(core, st, 10f) { SwimTarget = core.SwimTarget };
        j.Prime();
        var r = new Run { Core = core, Job = j, Seeds = j.AliveCount };
        var rng = new Random(seed * 31 + 5);
        float per = 0.6f * plans[plan].N / 192f * 10f;
        for (int t = 0; t < grow + turnover; t++)
        {
            if (t % 10 == 0) for (int e = 0; e < 4; e++) j.QueueDeposit(e, per * plans[plan].Mix[e] / (float)plans[plan].N * 40f);
            if (t >= grow && rng.NextDouble() < killRate * core.AliveCount)
            {
                int i = rng.Next(core.Cap);
                if (core.Active[i] && core.Hatched[i]) j.QueueKill(i);
            }
            j.Kick(true);
            var spin = new System.Threading.SpinWait();
            while (j.State != SwarmJobState.Done) spin.SpinOnce();
            j.Collect();
            if (j.Error != null) throw j.Error;
            if (r.FoundedAt < 0)
                for (int i = 0; i < core.Cap; i++) if (core.Active[i] && core.Dom[i] != 0) { r.FoundedAt = t; r.Founder = core.Dom[i]; break; }
            if (t % 25 == 0) r.Timeline.Add(Census(core));
        }
        return r;
    }

    public static int[] Census(SwarmSortCore c)
    {
        var n = new int[3];
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) n[Math.Clamp(c.Dom[i], 0, 2)]++;
        return n;
    }

    /// <summary>[region * 3 + domain] over hatched members (regions -1 dropped).</summary>
    public static int[] ByRegion(SwarmSortCore c)
    {
        var n = new int[9];
        for (int i = 0; i < c.Cap; i++)
        {
            if (!c.Active[i] || !c.Hatched[i]) continue;
            int r = c.RegionOf(i);
            if (r >= 0 && r < 3) n[r * 3 + Math.Clamp(c.Dom[i], 0, 2)]++;
        }
        return n;
    }

    /// <summary>Share of members whose nearest neighbour wears their domain, and the same for random labels.</summary>
    static (double same, double rnd) Neighbours(SwarmSortCore c)
    {
        var live = new List<int>(); var cnt = new int[3];
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { live.Add(i); cnt[c.Dom[i]]++; }
        int same = 0;
        foreach (int i in live)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (int k in live) { if (k == i) continue; float d = Vector3.DistanceSquared(c.Pos[i], c.Pos[k]); if (d < bd) { bd = d; best = k; } }
            if (best >= 0 && c.Dom[best] == c.Dom[i]) same++;
        }
        double n = Math.Max(1, live.Count), rnd = 0; for (int d = 0; d < 3; d++) rnd += (cnt[d] / n) * (cnt[d] / n);
        return (same / n, rnd);
    }

    static string Row(int[] byRegion, int ns)
    {
        var sb = new StringBuilder();
        for (int s = 0; s < ns; s++) sb.Append($"{(s > 0 ? "  " : "")}region {s}: {byRegion[s * 3]}/{byRegion[s * 3 + 1]}/{byRegion[s * 3 + 2]}");
        return sb.ToString();
    }

    public static int RunAll(SwarmPlanData[] plans, string exportPath)
    {
        const int GrowSteps = 900, TurnSteps = 2400;
        var names = new[] { "charge", "mass", "space", "time" };

        Console.WriteLine("\nR9a. one colour at birth: every seed wears the anchor's domain and food never colours anyone");
        {
            var off = Grow(plans, 1, 7, lineages: false, GrowSteps, 0);
            var c0 = Census(off.Core);
            Console.WriteLine($"  lineages off: {off.Seeds} seeds -> {off.Job.AliveCount} members, by domain {c0[0]}/{c0[1]}/{c0[2]}");
            Check(c0[1] == 0 && c0[2] == 0 && c0[0] > off.Seeds, "lineages off: the body grows ONE colour (the round-8 diet colouring is gone)");
            var on = Grow(plans, 1, 7, lineages: true, 0, 0);
            var c1 = Census(on.Core);
            Check(c1[0] == on.Seeds && c1[1] == 0 && c1[2] == 0, $"lineages on: all {on.Seeds} seeds wear slot 0 (the cell's controlling domain)");
        }

        Console.WriteLine("\nR9b. a second lineage FOUNDS itself in tissue nobody owns and grows into a region of its own (whale, 8 seeds)");
        var whales = new List<Run>();
        int contrast = 0, foundedBoth = 0; double puritySum = 0; var founders = new int[3]; var seedRegion = new int[3];
        for (int sd = 0; sd < 8; sd++)
        {
            var r = Grow(plans, 1, 100 + sd, lineages: true, GrowSteps, TurnSteps);
            whales.Add(r);
            var c = r.Core; int ns = SwarmSortCode.For(c.Plan, c.C).NSlots;
            var br = ByRegion(c);
            // each region's majority domain and how much of the region it holds
            var maj = new int[ns]; double pur = 0; int regions = 0;
            for (int s = 0; s < ns; s++)
            {
                int tot = br[s * 3] + br[s * 3 + 1] + br[s * 3 + 2];
                maj[s] = br[s * 3] >= br[s * 3 + 1] && br[s * 3] >= br[s * 3 + 2] ? 0 : br[s * 3 + 1] >= br[s * 3 + 2] ? 1 : 2;
                if (tot > 0) { pur += br[s * 3 + maj[s]] / (double)tot; regions++; }
            }
            pur /= Math.Max(1, regions); puritySum += pur;
            bool differ = ns >= 2 && maj[0] != maj[1];
            if (differ) contrast++;
            if (r.Founder > 0) { founders[r.Founder]++; foundedBoth++; }
            seedRegion[c.RoleOfDom[0] < 0 ? 2 : c.RoleOfDom[0]]++;
            var nb = Neighbours(c);
            Console.WriteLine($"  seed {100 + sd}: founded {(r.FoundedAt < 0 ? "never" : $"step {r.FoundedAt}, slot {r.Founder}")}; " +
                              $"{Row(br, ns)}; region purity {pur:P0}; neighbour shares domain {nb.same:P0} (random {nb.rnd:P0})");
        }
        Check(foundedBoth >= 6, $"a second lineage founds itself in most bodies ({foundedBoth}/8)");
        Check(contrast >= 6, $"back and belly end up DIFFERENT domains in most bodies ({contrast}/8)");
        Check(puritySum / 8 >= 0.8, $"each region is mostly one lineage after turnover (mean purity {puritySum / 8:P0})");
        Check(founders[1] > 0 && founders[2] > 0, $"nothing picks the colour: founders were slot 1 x{founders[1]}, slot 2 x{founders[2]}");
        Check(seedRegion[0] > 0 && seedRegion[1] > 0, $"nothing picks the part: the anchor's lineage holds the back in {seedRegion[0]}, the belly in {seedRegion[1]}");

        Console.WriteLine("\nR9c. it GROWS: the second lineage's headcount as the body grows (whale, first seed; anchor/slot 1/slot 2)");
        {
            var t = whales[0].Timeline; var sb = new StringBuilder("  ");
            foreach (int k in new[] { 0, 1, 2, 4, 8, 12, 16, 24, 40, 80, t.Count - 1 })
                if (k < t.Count) sb.Append($"t{k * 25}:{t[k][0]}/{t[k][1]}/{t[k][2]}  ");
            Console.WriteLine(sb.ToString());
            var last = t[t.Count - 1];
            int second = last[1] >= last[2] ? 1 : 2;
            Check(last[second] > 0.25f * whales[0].Job.AliveCount, $"the second lineage grows from one founder to a subpopulation ({last[second]} of {whales[0].Job.AliveCount})");
        }

        Console.WriteLine("\nR9d. every creature: lineages on the other three plans");
        var others = new List<(string, Run)>();
        foreach (int pl in new[] { 0, 2, 3 })
        {
            var r = Grow(plans, pl, 300 + pl, lineages: true, GrowSteps, TurnSteps);
            others.Add((names[pl], r));
            int ns = SwarmSortCode.For(r.Core.Plan, r.Core.C).NSlots;
            var c = Census(r.Core);
            Console.WriteLine($"  {names[pl],-6}: {r.Job.AliveCount} members by domain {c[0]}/{c[1]}/{c[2]}; {Row(ByRegion(r.Core), ns)}");
            Check(r.Job.AliveCount >= 0.75f * plans[pl].N, $"{names[pl]}: the body still grows to its plan under turnover");
        }

        if (exportPath != null)
        {
            var sb = new StringBuilder("{\"bodies\":[");
            bool first = true;
            void Add(string label, Run r)
            {
                var c = r.Core;
                if (!first) sb.Append(',');
                first = false;
                sb.Append($"{{\"label\":\"{label}\",\"plan\":\"{c.Plan.Kind}\",\"founder\":{r.Founder},\"members\":[");
                bool f2 = true;
                var bx = c.BX; var by = c.BY; var bz = c.BZ; var a = c.Anchor;
                for (int i = 0; i < c.Cap; i++)
                {
                    if (!c.Active[i] || !c.Hatched[i]) continue;
                    var d = c.Pos[i] - a;
                    float x = Vector3.Dot(d, bx), y = Vector3.Dot(d, by), z = Vector3.Dot(d, bz);
                    if (!f2) sb.Append(',');
                    f2 = false;
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "[{0:F2},{1:F2},{2:F2},{3},{4},{5}]", x, y, z, c.Dom[i], c.RegionOf(i), c.EffectiveElement(i)));
                }
                sb.Append("],\"timeline\":[");
                for (int k = 0; k < r.Timeline.Count; k++) sb.Append($"{(k > 0 ? "," : "")}[{r.Timeline[k][0]},{r.Timeline[k][1]},{r.Timeline[k][2]}]");
                sb.Append("]}");
            }
            for (int k = 0; k < whales.Count; k++) Add($"whale seed {100 + k}", whales[k]);
            foreach (var (n, r) in others) Add(n, r);
            sb.Append("]}");
            File.WriteAllText(exportPath, sb.ToString());
            Console.WriteLine($"\n  exported {whales.Count + others.Count} bodies to {exportPath}");
        }

        Console.WriteLine(_fail == 0 ? "\nlineages: OK" : $"\nlineages: {_fail} FAILED");
        return _fail;
    }
}
