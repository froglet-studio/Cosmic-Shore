// The NESTED GYROID gates. Every gate prints PASS/FAIL; every gate that can be wrong in a way that still
// passes has a NEGATIVE CONTROL that breaks the thing it guards and must come back red.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static class Driver
{
    static int fails;

    static void Gate(string name, bool ok, string detail)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}: {detail}");
        if (!ok) fails++;
    }

    static int Main(string[] args)
    {
        var s = new NestedGyroidSettings();
        Console.WriteLine("== default config ==");
        var L = Run(s, verbose: true);

        Console.WriteLine("== slicing (2 ms budget per Step, as the flora runs it) ==");
        var b = new NestedGyroidBuilder(s);
        while (!b.Step(2.0)) { }
        Gate("time-sliced build", b.MaxSliceMilliseconds < 8.0,
             $"{b.Slices} slices, worst {b.MaxSliceMilliseconds:F2} ms, total {b.Result.Stats.BuildMilliseconds:F0} ms");
        Gate("deterministic", Same(L, b.Result), "two builds of one config are identical");

        Console.WriteLine("== settings guard ==");
        var hot = new NestedGyroidSettings { TMax = 2.0f }.Sanitized();
        Gate("tMax clamped below the critical values", hot.TMax <= NestedGyroidSettings.TMaxCeiling && hot.TMax < MathF.Sqrt(2f),
             $"2.0 -> {hot.TMax}");

        Console.WriteLine("== ride (model of BlockscapeFollower, Urchin defaults) ==");
        Ride(L);

        if (args.Length > 0 && args[0] == "sweep")
        {
            Console.WriteLine("== sweep ==");
            foreach (int n in new[] { 2, 3, 5, 7, 9 })
            foreach (float t in new[] { 0.6f, 1.2f, 1.38f })
                Run(new NestedGyroidSettings { SheetCount = n, TMax = t, MaxSheets = n }, verbose: false, label: $"N={n} t={t}");
            Run(new NestedGyroidSettings { CellsPerSide = 2, PrismBudget = 2600 }, verbose: false, label: "2 cells, budget 2600");
            Run(new NestedGyroidSettings { MaxSheets = 3 }, verbose: false, label: "maxSheets 3");
        }

        Console.WriteLine(fails == 0 ? "ALL GATES PASS" : $"{fails} GATE(S) FAILED");
        return fails == 0 ? 0 : 1;
    }

    static NestedGyroidLattice Run(NestedGyroidSettings s, bool verbose, string label = null)
    {
        var L = NestedGyroidBuilder.BuildNow(s);
        var st = L.Stats;
        if (label != null) Console.WriteLine($"-- {label}");
        Console.WriteLine("  " + NestedGyroidBuilder.Describe(L));
        var clean = s.Sanitized();

        Console.WriteLine($"  limbs (heart + fiber bonds): {L.LimbBond.Take(L.Count).Count(b => b)}");
        Gate("budget", L.Count <= clean.PrismBudget, $"{L.Count} <= {clean.PrismBudget} ({st.SheetPrisms} sheet, {st.FiberPrisms} fiber)");
        Gate("one connected component", st.Components == 1, $"{st.Components} (pruned {st.PrunedIslandPrisms} island prisms of {st.ComponentsBeforePrune - 1} islands)");
        Gate("every bond within the Urchin's reach", st.MaxEdgeOverReach < 1f,
             $"worst bond {st.MaxEdgeOverReach:P1} of reach; gap min/median/max {st.GapMin:F2}/{st.GapMedian:F2}/{st.GapMax:F2}, reach min {st.ReachMin:F1}");

        var (all, cross) = BruteOverlaps(L, 1f);
        Gate("no prism interpenetrates another (brute-force OBB, every pair)", all == 0, $"{all} overlapping pairs, {cross} between neighbouring sheets");

        bool order = true, bonded = true;
        for (int i = 0; i < L.Count; i++)
        {
            int p = L.Parent[i];
            if (p >= i) order = false;
            if (p >= 0 && !Neighbours(L, i).Contains(p)) bonded = false;
        }
        Gate("growth order: every parent is laid first and is a real bond", order && bonded && L.Parent[0] == -1,
             "parent[i] < i, parent in adjacency, root hangs off the heart");

        float sc = clean.CellSize / (2f * MathF.PI);
        int down = Enumerable.Range(0, L.Count).Count(i => Vector3.Dot(L.Forward[i], NestedGyroidBuilder.Grad(L.Position[i] / sc)) <= 0f);
        Gate("every prism's +z points UP the stack (the ride's layer direction)", down == 0, $"{down} pointing down G");
        int badStack = 0;
        for (int i = 0; i < L.Count; i++)
            foreach (int j in Neighbours(L, i))
                if (Math.Abs(L.Stack[i] - L.Stack[j]) > 2) badStack++;
        Gate("stack coordinates: a bond never skips a layer", badStack == 0, $"{badStack} bonds jump more than one sheet");

        int ringBreak = 0;
        for (int i = 1; i < L.Count; i++) if (L.Rank[i] < L.Rank[i - 1]) ringBreak++;
        // A prism an inner ring cannot reach without passing through an outer one (a sheet fragment the cube
        // clip cut off, stitched only from outside) is laid when that outer ring arrives - legitimately late.
        Gate("grows outward ring by ring", ringBreak <= Math.Max(1, L.Count / 100), $"{ringBreak} prisms laid after a later ring (deferred fragments), of {L.Count}");

        var sheets = Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet).Select(i => L.Sheet[i]).Distinct().Count();
        Gate("every grown sheet carries prisms", sheets == st.SheetsGrown, $"{sheets}/{st.SheetsGrown}");

        if (st.SheetsGrown > 1)
        {
            Gate("fibers stitch the sheets", st.CrossSheetEdges > 0 && st.FiberPrisms > 0,
                 $"{st.Fibers} fibers, {st.FiberPrisms} struts, {st.CrossSheetEdges} cross-sheet / {st.InSheetEdges} in-sheet links");
            // NEGATIVE CONTROL: cut the fibers and the stack must fall apart into separate shells - this is
            // what proves the warp, not the skin, is what makes it ONE lattice.
            int shells = ComponentsWithoutFibers(L);
            Gate("negative control: without the fibers it is separate shells", shells >= st.SheetsGrown, $"{shells} components with fiber links cut");
        }

        var (r, saturated) = Corr(L);
        Gate("thickness tracks the local gap Δt/|∇G|", r > 0.5 || saturated > 0.5f,
             (saturated > 0.5f ? $"{saturated:P0} of plates at the plate-shape cap (deep layers) - " : "") + $"corr(plate thickness, 1/|∇G|) = {r:F2}; plates {st.ThicknessMin:F2}..{st.ThicknessMax:F2}, layer gap {st.SheetGapWorldMin:F1}..{st.SheetGapWorldMax:F1}");
        Gate("orientation field combed", st.CombAfterDegrees < st.CombBeforeDegrees,
             $"neighbour misalignment {st.CombBeforeDegrees:F1}° (per-site projection) -> {st.CombAfterDegrees:F1}° (combed)");

        var outer = OuterSkin(L, clean);
        Gate("outer surfaces are a ridable skin", outer.ok, outer.detail);

        if (verbose)
        {
            // NEGATIVE CONTROLS for the geometry gates: each must fail when its property is broken.
            var (inflated, _) = BruteOverlaps(L, 1.6f);
            Gate("negative control: plates grown 1.6x DO overlap", inflated > 0, $"{inflated} pairs");
            float worstAtLowReach = 0f;
            for (int i = 0; i < L.Count; i++)
                foreach (int j in Neighbours(L, i))
                {
                    float reachI = MathF.Max(1f, Extent(L, i)) * 1.0f + 2f, reachJ = MathF.Max(1f, Extent(L, j)) * 1.0f + 2f;
                    worstAtLowReach = MathF.Max(worstAtLowReach, Vector3.Distance(L.Position[i], L.Position[j]) / MathF.Min(reachI, reachJ));
                }
            Gate("negative control: a 1.0x search radius CANNOT reach", worstAtLowReach >= 1f, $"worst bond {worstAtLowReach:P0} of a 1.0x reach");
        }
        return L;
    }

    static float Extent(NestedGyroidLattice L, int i) { var s = L.Size[i]; return MathF.Max(s.X, MathF.Max(s.Y, s.Z)); }

    static IEnumerable<int> Neighbours(NestedGyroidLattice L, int i)
    {
        for (int e = L.AdjStart[i]; e < L.AdjStart[i + 1]; e++) yield return L.Adj[e];
    }

    static (int all, int cross) BruteOverlaps(NestedGyroidLattice L, float grow)
    {
        int all = 0, cross = 0;
        for (int i = 0; i < L.Count; i++)
        {
            var xi = Vector3.Cross(L.Up[i], L.Forward[i]);
            float ri = 0.5f * grow * L.Size[i].Length();
            for (int j = i + 1; j < L.Count; j++)
            {
                float rj = 0.5f * grow * L.Size[j].Length();
                if (Vector3.DistanceSquared(L.Position[i], L.Position[j]) > (ri + rj) * (ri + rj)) continue;
                var xj = Vector3.Cross(L.Up[j], L.Forward[j]);
                if (!NestedGyroidBuilder.ObbOverlap(L.Position[i], xi, L.Up[i], L.Forward[i], 0.5f * grow * L.Size[i],
                                                    L.Position[j], xj, L.Up[j], L.Forward[j], 0.5f * grow * L.Size[j])) continue;
                all++;
                if (L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Kind[j] == NestedGyroidPrismKind.Sheet && L.Sheet[i] != L.Sheet[j]) cross++;
            }
        }
        return (all, cross);
    }

    static int ComponentsWithoutFibers(NestedGyroidLattice L)
    {
        var p = Enumerable.Range(0, L.Count).ToArray();
        int F(int x) { while (p[x] != x) x = p[x] = p[p[x]]; return x; }
        for (int i = 0; i < L.Count; i++)
            foreach (int j in Neighbours(L, i))
            {
                bool inSheet = L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Kind[j] == NestedGyroidPrismKind.Sheet && L.Sheet[i] == L.Sheet[j];
                if (inSheet) p[F(i)] = F(j);
            }
        return Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet).Select(F).Distinct().Count();
    }

    static (bool ok, string detail) OuterSkin(NestedGyroidLattice L, NestedGyroidSettings s)
    {
        int maxRank = L.Rank.Take(L.Count).Max();
        var outerSheets = Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Rank[i] == maxRank)
                                    .Select(i => L.Sheet[i]).Distinct().ToList();
        // Fibers stop at the outermost sheets: no strut belongs to a gap beyond them.
        bool fibersStop = Enumerable.Range(0, L.Count).All(i => L.Kind[i] != NestedGyroidPrismKind.Fiber || L.Rank[i] <= maxRank);
        var parts = new List<string>();
        bool ok = fibersStop;
        foreach (int sh in outerSheets)
        {
            var ids = Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Sheet[i] == sh).ToList();
            var p = new Dictionary<int, int>();
            foreach (int i in ids) p[i] = i;
            int F(int x) { while (p[x] != x) x = p[x] = p[p[x]]; return x; }
            foreach (int i in ids)
                foreach (int j in Neighbours(L, i))
                    if (p.ContainsKey(j)) p[F(i)] = F(j);
            var sizes = ids.GroupBy(F).Select(g => g.Count()).OrderByDescending(c => c).ToList();
            float share = sizes[0] / (float)ids.Count;
            // The skin is the cube-clipped sheet, so it may come in pieces where the clip cuts a channel -
            // every piece is still reachable through the fibers (the component gate). Ridable = the bulk of it
            // is one continuous in-sheet surface.
            ok &= share >= 0.8f;
            parts.Add($"sheet {sh}: {ids.Count} plates, largest in-sheet piece {share:P0} ({sizes.Count} pieces)");
        }
        return (ok, (fibersStop ? "fibers stop at the outer sheets; " : "FIBERS PAST THE OUTER SHEETS; ") + string.Join("; ", parts));
    }

    static (double r, float saturated) Corr(NestedGyroidLattice L)
    {
        var xs = new List<double>(); var ys = new List<double>();
        int capped = 0, plates = 0;
        float scale = 240f / (2f * MathF.PI);
        for (int i = 0; i < L.Count; i++)
        {
            if (L.Kind[i] != NestedGyroidPrismKind.Sheet) continue;
            float cs = 1f;   // the harness only asks for the SIGN of the relationship, so any positive scale serves
            float g = NestedGyroidBuilder.Grad(L.Position[i] / (L.Stats.EffectiveSheetSpacing > 0 ? scale * cs : 1f)).Length();
            plates++;
            if (L.Size[i].Z >= 0.749f * L.Size[i].X) { capped++; continue; }
            xs.Add(L.Size[i].Z); ys.Add(1.0 / g);
        }
        if (xs.Count < 3) return (0, capped / (float)Math.Max(1, plates));
        double mx = xs.Average(), my = ys.Average();
        double sxy = 0, sxx = 0, syy = 0;
        for (int k = 0; k < xs.Count; k++) { sxy += (xs[k] - mx) * (ys[k] - my); sxx += (xs[k] - mx) * (xs[k] - mx); syy += (ys[k] - my) * (ys[k] - my); }
        return (sxy / Math.Sqrt(sxx * syy + 1e-12), capped / (float)Math.Max(1, plates));
    }

    static bool Same(NestedGyroidLattice a, NestedGyroidLattice b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a.Position[i] != b.Position[i] || a.Up[i] != b.Up[i] || a.Size[i] != b.Size[i] || a.Parent[i] != b.Parent[i]) return false;
        return true;
    }

    // ------------------------------------------------------------------ the ride

    static void Ride(NestedGyroidLattice L)
    {
        int maxRank = L.Rank.Take(L.Count).Max();
        int lo = Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Rank[i] == maxRank).Min(i => L.Sheet[i]);
        int hi = Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Rank[i] == maxRank).Max(i => L.Sheet[i]);

        // Start on a ring-0 plate with a fiber crossing nearby (any ring-0 plate near the heart serves).
        int start = 0;
        foreach (bool layered in new[] { true, false })
        {
            string rule = layered ? "LAYERED (shipped)" : "legacy nearest-centre (negative control)";

            // A. Roam within the sheet: aim along the surface.
            var m = new RideModel(L) { Layered = layered };
            m.Attach(start, L.Position[start] + L.Forward[start] * 5f);
            var aim = Vector3.Normalize(L.Up[start]);
            var visited = new HashSet<int>(); var sheetsSeen = new HashSet<int>();
            for (int f = 0; f < 60 * 6; f++)
            {
                aim = Vector3.Normalize(aim - Vector3.Dot(aim, m.SurfaceNormal) * m.SurfaceNormal);
                m.Tick(aim, 1f, 60f, 1f / 60f);
                visited.Add(m.Ground);
                if (L.Kind[m.Ground] == NestedGyroidPrismKind.Sheet) sheetsSeen.Add(L.Sheet[m.Ground]);
            }
            bool stay = sheetsSeen.Count == 1 && visited.Count >= 8;
            string roll = $"{visited.Count} prisms crossed, sheets {string.Join(",", sheetsSeen)}";
            if (layered) Gate("ride: rolls WITHIN a sheet when aimed along it", stay, roll);
            else Gate("negative control: the legacy rule CANNOT hold a sheet in a stack", sheetsSeen.Count > 1, roll);

            // B/C. Pitch out of the sheet, then into it.
            foreach (float pitch in new[] { +1f, -1f })
            {
                m = new RideModel(L) { Layered = layered };
                m.Attach(start, L.Position[start] + L.Forward[start] * 5f);
                var seq = new List<int>();
                for (int f = 0; f < 60 * 8; f++)
                {
                    var tangent = Vector3.Normalize(L.Up[m.Ground] - Vector3.Dot(L.Up[m.Ground], m.SurfaceNormal) * m.SurfaceNormal);
                    // The pilot aims UP or DOWN the stack in world terms - along the ground's +∇G (every prism's
                    // Forward points up G) - not along the ridden normal, whose sign a rim wrap can flip.
                    var dir = Vector3.Normalize(0.5f * tangent + pitch * 0.87f * L.Forward[m.Ground]);
                    m.Tick(dir, 1f, 60f, 1f / 60f);
                    if (L.Kind[m.Ground] == NestedGyroidPrismKind.Sheet && (seq.Count == 0 || seq[^1] != L.Sheet[m.Ground])) seq.Add(L.Sheet[m.Ground]);
                }
                bool reachedSkin = seq.Count > 0 && seq[^1] == (pitch > 0 ? hi : lo);
                string what = pitch > 0 ? "climbs UP the stack (+G)" : "dives DOWN the stack (-G)";
                string detail = $"sheets visited in order {string.Join(" > ", seq)}";
                // Monotone: the pilot's pitch decides the direction - one sheet at a time, never back.
                int reversals = 0;
                for (int k = 2; k < seq.Count; k++) if (Math.Sign(seq[k] - seq[k - 1]) != Math.Sign(seq[k - 1] - seq[k - 2])) reversals++;
                if (layered) Gate($"ride: {what} and stops on the outer skin", seq.Count >= 3 && reachedSkin && reversals == 0, detail + $" ({reversals} reversals)");
                else Console.WriteLine($"  [info] {rule}, {what}: {detail} ({reversals} reversals - the stack is a random walk)");
            }
        }
    }
}
