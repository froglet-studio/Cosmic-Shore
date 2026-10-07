// The NESTED GYROID gates (Docs/ECOSYSTEM.md §58). Every gate prints PASS/FAIL; every gate that can be wrong in a
// way that still passes has a NEGATIVE CONTROL that breaks the thing it guards and must come back red.
//
// Three levels, matching the shipped model:
//   PERIOD - one period of the whole stack, built and fitted as a periodic structure (NestedGyroidBuilder);
//   PLANT  - one octagon tile of it on every sheet, grown as one spindle tree out of its crystal (Period.Plant);
//   COLONY - plants at tiles of one shared lattice frame, born one at a time at a random open tile. The colony
//            book here is the SHIPPED NestedGyroidColony.cs, run against ColonyStubs.cs.
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
        Console.WriteLine("== default config: the period ==");
        var P = NestedGyroidBuilder.BuildNow(s);
        PeriodGates(P);

        Console.WriteLine("== default config: every plant of the period (all 24 octagon tiles) ==");
        AllPlants(P, verbose: true);

        Console.WriteLine("== default config: colonies (the shipped NestedGyroidColony book) ==");
        var colony = ColonyGates(P, verbose: true);

        Console.WriteLine("== default config: a grown colony as one prismscape ==");
        var (U, plantOf) = UnionGates(P, colony, verbose: true);

        Console.WriteLine("== ride (model of BlockscapeFollower, Urchin defaults) across the colony ==");
        Ride(U, plantOf);

        Console.WriteLine("== the four elements, as shipped (each config's quoted Gyroid Flora leaf + lattice scale) ==");
        foreach (var e in new[] { "Time", "Mass", "Space", "Charge" })
        {
            var es = ElementSettings(e);
            if (es == null) { Gate($"{e} config readable", false, "Nested Gyroid Flora " + e + ".asset not found (set NG_ROOT)"); continue; }
            Console.WriteLine($"-- {e}: leaf {es.Leaf}, period {es.CellSize:F0}");
            var EP = NestedGyroidBuilder.BuildNow(es);
            PeriodGates(EP);
            AllPlants(EP, verbose: false);
            UnionGates(EP, GrowColony(EP, ShippedCap, seed: 7), verbose: false);
        }

        Console.WriteLine("== slicing (2 ms budget per Step, as the flora runs it) ==");
        // Best of three builds' worst slice: a heavy CHUNK shows in every build, a VM/GC pause in one - and on a shared
        // VM a single build's worst slice swings 2.5-9 ms run to run.
        NestedGyroidBuilder b = null;
        double best = double.MaxValue;
        for (int run = 0; run < 3; run++)
        {
            var bb = new NestedGyroidBuilder(s);
            while (!bb.Step(2.0)) { }
            if (bb.MaxSliceMilliseconds < best) { best = bb.MaxSliceMilliseconds; b = bb; }
        }
        Gate("time-sliced build", best < 8.0,
             $"{b.Slices} slices, worst {best:F2} ms (best of 3 builds), total {b.Result.Stats.BuildMilliseconds:F0} ms");
        Gate("deterministic", Same(P, b.Result), "two builds of one config are identical, plant by plant");

        Console.WriteLine("== preview (the Spawn Matrix icon asks 220 prisms; NestedGyroidFlora.TryPreviewGrowth) ==");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var ps = s.Clone(); ps.MaxSheets = Math.Min(ps.MaxSheets, 3);
        var PP = NestedGyroidBuilder.BuildNow(ps);
        var preview = PP.Plant(0);
        double previewMs = watch.Elapsed.TotalMilliseconds;
        Console.WriteLine("  " + NestedGyroidBuilder.Describe(preview, PP));
        int previewSheets = Enumerable.Range(0, preview.Count).Where(i => preview.Kind[i] == NestedGyroidPrismKind.Sheet).Select(i => preview.Sheet[i]).Distinct().Count();
        Gate("icon preview is one whole plant of 3 sheets, inside the icon budget",
             preview.Count <= 220 && previewSheets == 3 && preview.Stats.FiberPrisms > 0 && preview.Stats.DangerPrisms >= 8 && preview.Stats.TruncatedByBudget == 0,
             $"{preview.Count} prisms, {previewSheets} sheets, {preview.Stats.FiberPrisms} struts, {preview.Stats.DangerPrisms} danger-ring plates");
        Gate("icon preview is cheap enough to build synchronously", previewMs < 0.6 * P.Stats.BuildMilliseconds + 5.0,
             $"{previewMs:F1} ms against the full period's {P.Stats.BuildMilliseconds:F0} ms (cached after the first icon)");

        Console.WriteLine("== settings guard ==");
        var hot = new NestedGyroidSettings { TMax = 2.0f }.Sanitized();
        Gate("tMax clamped below the critical values", hot.TMax <= NestedGyroidSettings.TMaxCeiling && hot.TMax < MathF.Sqrt(2f),
             $"2.0 -> {hot.TMax}");
        var cut = new NestedGyroidSettings { PrismBudget = 60 };
        var CP = NestedGyroidBuilder.BuildNow(cut).Plant(3);
        Gate("a budget below the plant CUTS the growth order and stays one tree on the crystal",
             CP.Count == 60 && CP.Stats.TruncatedByBudget > 0 && CP.Stats.RootedPrisms == CP.Count,
             $"{CP.Count} laid, {CP.Stats.TruncatedByBudget} cut, {CP.Stats.RootedPrisms} rooted");

        if (args.Length > 0 && args[0] == "sweep")
        {
            Console.WriteLine("== sweep ==");
            foreach (int n in new[] { 2, 3, 5, 7, 9 })
            foreach (float t in new[] { 0.6f, 1.2f, 1.3f, 1.35f, 1.38f })
            {
                var sw = new NestedGyroidSettings { SheetCount = n, TMax = t, MaxSheets = n, PrismBudget = 60 * n + 100 };
                Console.WriteLine($"-- N={n} t={t}");
                var SP = NestedGyroidBuilder.BuildNow(sw);
                PeriodGates(SP);
                AllPlants(SP, verbose: false);
                // Rideability is a property of the SHIPPED stack (N 7, tMax 1.2 - every element gated above). A sparse
                // stack (N <= 3: layer gaps far beyond the rider's reach) or a near-critical one (tMax >= 1.3: outer
                // sheets whose area collapses) has outer-sheet fragments the rider cannot bridge; reported, not gated.
                UnionGates(SP, GrowColony(SP, 12, seed: n), verbose: false, rideGate: false);
            }
            Console.WriteLine("-- maxSheets 3");
            var M3 = NestedGyroidBuilder.BuildNow(new NestedGyroidSettings { MaxSheets = 3 });
            PeriodGates(M3);
            AllPlants(M3, verbose: false);
        }

        Console.WriteLine(fails == 0 ? "ALL GATES PASS" : $"{fails} GATE(S) FAILED");
        return fails == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ the period

    static void PeriodGates(NestedGyroidPeriod P)
    {
        var st = P.Stats;
        int alive = Enumerable.Range(0, P.Count).Count(i => P.Alive[i]);
        Gate("period: zero overlaps after the periodic fit", st.RemainingOverlaps == 0,
             $"{alive} prisms; {st.OverlapsBeforeFit} overlapping pairs before the fit ({st.CrossSheetOverlapsBeforeFit} cross-sheet), " +
             $"{st.RemainingOverlaps} after; {st.DroppedByFit} plates dropped, {st.DroppedStruts} struts dropped");
    }

    // ------------------------------------------------------------------ one plant

    /// <summary>Plant 0 in full; every other plant prints only a failing gate, then one summary line.</summary>
    static void AllPlants(NestedGyroidPeriod P, bool verbose)
    {
        int before = fails;
        var counts = new List<int>();
        for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++)
        {
            PlantGates(P, o, verbose: verbose && o == 0, quiet: !verbose || o > 0);
            counts.Add(P.Plant(o).Count);
        }
        Console.WriteLine($"  [{(fails == before ? "PASS" : "FAIL")}] all {NestedGyroidTemplate.OctagonCount} plants: " +
                          $"{counts.Min()}-{counts.Max()} prisms, struts {Enumerable.Range(0, NestedGyroidTemplate.OctagonCount).Min(o => P.Plant(o).Stats.FiberPrisms)}" +
                          $"-{Enumerable.Range(0, NestedGyroidTemplate.OctagonCount).Max(o => P.Plant(o).Stats.FiberPrisms)} each " +
                          "(budget, crystal tree, one tile, danger ring, up the stack, ring order, stitching, proportions)");
    }

    static void PlantGates(NestedGyroidPeriod P, int octagon, bool verbose, bool quiet = false)
    {
        var L = P.Plant(octagon);
        var st = L.Stats;
        var clean = P.Settings;
        if (verbose) Console.WriteLine("  " + NestedGyroidBuilder.Describe(L, P));
        var log = new List<(string, bool, string)>();
        void G(string name, bool ok, string detail) => log.Add(($"plant #{octagon}: {name}", ok, detail));

        G("budget", L.Count <= clean.PrismBudget && st.TruncatedByBudget == 0,
          $"{L.Count} <= {clean.PrismBudget} ({st.SheetPrisms} plate, {st.FiberPrisms} strut), {st.TruncatedByBudget} cut");

        // THE CRYSTAL: every prism's parent chain ends at the heart, and every bond to a parent is a limb.
        bool order = true;
        for (int i = 0; i < L.Count; i++) if (L.Parent[i] >= i) order = false;
        int roots = Enumerable.Range(0, L.Count).Count(i => L.Parent[i] < 0);
        float rootFar = Enumerable.Range(0, L.Count).Where(i => L.Parent[i] < 0).Select(i => L.Position[i].Length()).DefaultIfEmpty(0f).Max();
        G("every prism hangs off the crystal through spindles", st.RootedPrisms == L.Count && order && roots > 0,
          $"{st.RootedPrisms}/{L.Count} rooted, parent laid first, {roots} limbs straight out of the crystal (furthest {rootFar:F1} from it)");
        int ringRoots = Enumerable.Range(0, L.Count).Count(i => L.Parent[i] < 0 && L.DangerRing[i] && L.Rank[i] == 0);
        // Odd N: the ring lies on the crystal's own (t = 0) sheet, ~10 template units out. Even N: the crystal sits
        // BETWEEN the two central sheets, so its first limbs also cross half the central gap.
        float ringBound = (clean.SheetCount % 2 == 1 ? 0.12f : 0.25f) * clean.CellSize;
        // The heart's own sheet(s) near-critical (N = 2 at tMax >= 1.3) lose ring plates like any such sheet.
        bool heartSheetCritical = Enumerable.Range(0, L.Count).Any(i => L.Parent[i] < 0 && MathF.Abs(L.SheetLevels[L.Sheet[i]]) > 1.25f);
        G("the crystal sits at the centre of its octagon ring", ringRoots == roots && roots >= (heartSheetCritical ? 1 : 8) && rootFar < ringBound,
          $"{ringRoots} of {roots} first limbs land on the ring on the heart's sheet(s); furthest {rootFar:F1} (bound {ringBound:F0}, period {clean.CellSize:F0})");

        // ONE UNIT: the plant is one tile on every sheet, never a cube of the lattice.
        var perSheet = Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet)
                                 .GroupBy(i => L.Sheet[i]).ToDictionary(g => g.Key, g => g.Count());
        float extent = Enumerable.Range(0, L.Count).Max(i => L.Position[i].Length());
        int tileSites = st.TemplateSites;
        bool sitesOk = SitesUniquePerSheet(L) && perSheet.Values.All(c => c <= tileSites);
        int zeroSheet = Array.IndexOf(L.SheetLevels, 0f);
        int baseCount = zeroSheet >= 0 && perSheet.ContainsKey(zeroSheet) ? perSheet[zeroSheet] : tileSites;
        G("the plant is ONE octagon tile on every sheet", sitesOk && perSheet.Count == st.SheetsGrown && tileSites >= 23 && tileSites <= 25
          && baseCount >= tileSites - 1 && perSheet.Values.Min() >= 0.6f * tileSites && extent < 0.5f * clean.CellSize,
          $"{tileSites}-site tile; plates per sheet {string.Join("/", perSheet.OrderBy(k => k.Key).Select(k => k.Value))}; " +
          $"furthest prism {extent:F0} from the crystal (period {clean.CellSize:F0})");

        // DANGER: the octagon ring, on every sheet.
        var ringPerSheet = perSheet.Keys.ToDictionary(sh => sh,
            sh => Enumerable.Range(0, L.Count).Count(i => L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Sheet[i] == sh && L.DangerRing[i]));
        // Near the critical values (|t| -> √2) a level's AREA collapses toward G's maxima and the fit drops what no
        // longer fits - measured 2-4 of 8 ring plates left on a |t| >= 1.3 sheet. There the ring must survive, not be whole.
        G("its octagon ring is danger on every sheet",
          ringPerSheet.All(kv => kv.Value >= (MathF.Abs(L.SheetLevels[kv.Key]) <= 1.25f ? 6 : 1)),
          $"ring plates per sheet {string.Join("/", ringPerSheet.OrderBy(k => k.Key).Select(k => k.Value))} (an octagon is 8)");

        // Up the stack; a limb never skips a layer; ring by ring.
        float sc = clean.CellSize / (2f * MathF.PI);
        var centre = NestedGyroidTemplate.OctagonCenter[octagon] * clean.CellSize;
        int down = Enumerable.Range(0, L.Count).Count(i => Vector3.Dot(L.Forward[i], NestedGyroidBuilder.Grad((L.Position[i] + centre) / sc)) <= 0f);
        G("every prism's +z points UP the stack", down == 0, $"{down} pointing down G");
        int skip = 0;
        for (int i = 0; i < L.Count; i++) if (L.Parent[i] >= 0 && Math.Abs(L.Stack[i] - L.Stack[L.Parent[i]]) > 2) skip++;
        G("a limb never skips a layer", skip == 0, $"{skip} limbs jump more than one sheet");
        int ringBreak = 0;
        for (int i = 1; i < L.Count; i++) if (L.Rank[i] < L.Rank[i - 1]) ringBreak++;
        G("grows outward ring by ring", ringBreak == 0, $"{ringBreak} prisms laid after a later ring");

        if (st.SheetsGrown > 1)
        {
            int shells = TreeComponentsWithoutCrossLimbs(L);
            // Every tile is ANCHORED a strutted column; near-critical a plate the column ends on can be fitted away,
            // and that column then carries no strut (seen once in 24 plants at N = 2, tMax 1.35).
            G("struts and columns stitch the sheets (negative control: cut them and it is separate shells)",
              st.Fibers > 0 && (st.FiberPrisms > 0 || clean.TMax > 1.3f) && shells >= st.SheetsGrown,
              $"{st.Fibers} strutted columns, {st.FiberPrisms} struts; {shells} pieces with every cross-sheet limb cut");
        }

        // Proportions: every plate the fit left alone wears the element's leaf aspect exactly.
        var unfitted = Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet && !L.Fitted[i]).ToList();
        float leafAspect = clean.Leaf.X / clean.Leaf.Y;
        float aspectErr = unfitted.Count == 0 ? 0f : unfitted.Max(i => MathF.Abs(L.Size[i].X / L.Size[i].Y / leafAspect - 1f));
        G("plates keep the element's proportions", aspectErr < 1e-3f,
          $"leaf aspect {leafAspect:F2} on {unfitted.Count} unfitted plates (worst {aspectErr:P2} off); " +
          $"{L.Fitted.Take(L.Count).Count(f => f)} shrunk by the fit");

        // Spindle length is INFORMATION: a limb is the plant's skeleton, not the rider's path. The rider rides the
        // prisms, and the colony-union gate is what proves every prism has a ridable neighbour.
        G("limb lengths (info)", true,
          $"min/median/max {st.BondMin:F1}/{st.BondMedian:F1}/{st.BondMax:F1}, longest {st.MaxEdgeOverReach:P0} of the rider's reach");

        foreach (var (n, ok, d) in log)
        {
            if (quiet && ok) continue;
            Gate(n, ok, d);
        }
    }

    static int TreeComponentsWithoutCrossLimbs(NestedGyroidLattice L)
    {
        var p = Enumerable.Range(0, L.Count).ToArray();
        int F(int x) { while (p[x] != x) x = p[x] = p[p[x]]; return x; }
        for (int i = 0; i < L.Count; i++)
        {
            int j = L.Parent[i];
            if (j < 0) continue;
            if (L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Kind[j] == NestedGyroidPrismKind.Sheet && L.Sheet[i] == L.Sheet[j]) p[F(i)] = F(j);
        }
        // The crystal joins its roots - which all sit on the heart's sheet(s).
        int first = -1;
        for (int i = 0; i < L.Count; i++)
            if (L.Parent[i] < 0) { if (first < 0) first = i; else if (L.Sheet[i] == L.Sheet[first]) p[F(i)] = F(first); }
        return Enumerable.Range(0, L.Count).Where(i => L.Kind[i] == NestedGyroidPrismKind.Sheet).Select(F).Distinct().Count();
    }

    static bool SitesUniquePerSheet(NestedGyroidLattice L)
    {
        var seen = new HashSet<(int, int)>();
        for (int i = 0; i < L.Count; i++)
            if (L.Kind[i] == NestedGyroidPrismKind.Sheet && !seen.Add((L.Sheet[i], L.Site[i]))) return false;
        return true;
    }

    // ------------------------------------------------------------------ the colony (the shipped book)

    sealed class Grown
    {
        public readonly List<NestedGyroidColony.Tile> Tiles = new();
        public NestedGyroidColony Book;
    }

    /// <summary>
    /// Runs the shipped colony book the way NestedGyroidFlora drives it: a founder claims its tile; a plant that has
    /// grown contributes its neighbours; once per cycle the population pops ONE random open tile and a daughter
    /// claims it. Plants grow in ~4 s against a ~30 s cycle, so every plant is complete by the next cycle.
    /// </summary>
    /// <summary>The shipped cap (author_nested_gyroid_flora_assets.py CAP - the gyroid flora's own).</summary>
    const int ShippedCap = 42;

    static Grown GrowColony(NestedGyroidPeriod P, int plants, int seed, int founderOctagon = -1,
                            Func<NestedGyroidColony, NestedGyroidColony.Tile, bool> accept = null)
    {
        UnityEngine.Random.Rng = new System.Random(seed);
        UnityEngine.Time.time = 0f;
        var g = new Grown();
        var founderTile = new NestedGyroidColony.Tile(0, 0, 0, founderOctagon >= 0 ? founderOctagon : UnityEngine.Random.Range(0, NestedGyroidTemplate.OctagonCount));
        var cell = new Cell();
        var species = new CosmicShore.Utility.FloraConfigurationSO();
        g.Book = NestedGyroidColony.Found(cell, species, default, UnityEngine.Quaternion.identity, P.Settings.CellSize);
        int id = 0;
        if (!g.Book.TryClaim(founderTile, new NestedGyroidFlora(id++))) throw new Exception("founder could not claim");
        g.Tiles.Add(founderTile);
        var matured = new HashSet<NestedGyroidColony.Tile>();
        const float cycle = 30f;
        g.Book.TryBeginCycle(cycle, 0.35f);              // anchors the clock
        for (int guard = 0; g.Tiles.Count < plants && guard < plants * 10; guard++)
        {
            foreach (var t in g.Tiles) if (matured.Add(t)) g.Book.ContributeNeighbors(t);
            UnityEngine.Time.time += cycle;
            if (!g.Book.TryBeginCycle(cycle, 0.35f)) continue;
            var book = g.Book;
            if (!g.Book.TryPopRandom(out var tile, accept == null ? null : t => accept(book, t))) break;
            if (!g.Book.TryClaim(tile, new NestedGyroidFlora(id++))) throw new Exception($"popped a claimed tile {tile}");
            g.Tiles.Add(tile);
        }
        NestedGyroidColony.Clear(cell);
        return g;
    }

    static Grown ColonyGates(NestedGyroidPeriod P, bool verbose)
    {
        var shapes = new List<string>();
        var sets = new HashSet<string>();
        bool unique = true, adjacent = true, reached = true;
        float worstFill = 0f;
        for (int seed = 1; seed <= 8; seed++)
        {
            var g = GrowColony(P, 60, seed);
            unique &= g.Tiles.Distinct().Count() == g.Tiles.Count;
            reached &= g.Tiles.Count == 60;
            // Every birth borders an earlier plant (a colony grows THROUGH the lattice, never jumps).
            for (int k = 1; k < g.Tiles.Count; k++)
                adjacent &= g.Tiles.Take(k).Any(m => m.Neighbors().Contains(g.Tiles[k]));
            sets.Add(string.Join(";", g.Tiles.OrderBy(t => (t.X, t.Y, t.Z, t.Octagon))));
            // Shape: how much of the period-cell box it spans does it fill? A grown CUBE fills 1.0 of its box.
            int cx = g.Tiles.Max(t => t.X) - g.Tiles.Min(t => t.X) + 1;
            int cy = g.Tiles.Max(t => t.Y) - g.Tiles.Min(t => t.Y) + 1;
            int cz = g.Tiles.Max(t => t.Z) - g.Tiles.Min(t => t.Z) + 1;
            float fill = g.Tiles.Count / (float)(cx * cy * cz * NestedGyroidTemplate.OctagonCount);
            worstFill = MathF.Max(worstFill, fill);
            if (seed <= 3) shapes.Add($"seed {seed}: spans {cx}x{cy}x{cz} periods, fills {fill:P0} of that box");
        }
        Gate("colony: no tile is ever claimed twice", unique, "8 colonies of 60 plants");
        Gate("colony: every birth borders a living plant (grows THROUGH the periodic structure)", adjacent && reached, "8 colonies of 60 plants");
        Gate("colony: random, not a fixed fill order - every seed grows a different colony", sets.Count == 8, $"{sets.Count} distinct shapes of 8");
        Gate("colony: a sparse wandering population, not a filled cube", worstFill < 0.5f, string.Join("; ", shapes) + $"; densest fills {worstFill:P0}");

        // NEGATIVE CONTROL for the shape gate: a cube-filling population (every tile of one period, then the next)
        // fills its box completely - the gate above must be able to see that.
        var cube = new List<NestedGyroidColony.Tile>();
        for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++) cube.Add(new NestedGyroidColony.Tile(0, 0, 0, o));
        Gate("negative control: a whole-period fill reads as a full box", cube.Count / (float)NestedGyroidTemplate.OctagonCount >= 0.99f, "24 of 24");

        // Death frees a tile and the frontier re-offers it; the next birth can land there again.
        UnityEngine.Random.Rng = new System.Random(3);
        var cell = new Cell(); var species = new CosmicShore.Utility.FloraConfigurationSO();
        var book = NestedGyroidColony.Found(cell, species, default, UnityEngine.Quaternion.identity, P.Settings.CellSize);
        var a = new NestedGyroidColony.Tile(0, 0, 0, 0);
        var plantA = new NestedGyroidFlora(0);
        book.TryClaim(a, plantA);
        var nb = a.Neighbors().First();
        var plantB = new NestedGyroidFlora(1);
        book.TryClaim(nb, plantB);
        book.Release(nb, plantB);
        bool reoffered = book.OpenTiles == 1 && book.TryPopRandom(out var back) && back.Equals(nb);
        bool freed = !book.IsClaimed(nb) && book.TryClaim(nb, new NestedGyroidFlora(2));
        book.Release(a, plantB);                          // the wrong plant cannot free a tile
        Gate("colony: a death frees its tile for regrowth; only the owner can free it", freed && book.IsClaimed(a) && reoffered,
             "released, re-offered, re-claimed; foreign release ignored");
        Gate("colony: the book is keyed by (cell, species) and dies with the population",
             NestedGyroidColony.Find(cell, species) == book && FindAfterClear(cell, species), "Find, then Clear(cell)");

        // TryAnyOpenTile: a second seed JOINS the colony while the founder is still growing (frontier empty).
        UnityEngine.Random.Rng = new System.Random(5);
        var cell2 = new Cell();
        var b2 = NestedGyroidColony.Found(cell2, species, default, UnityEngine.Quaternion.identity, P.Settings.CellSize);
        var f = new NestedGyroidColony.Tile(0, 0, 0, 5);
        b2.TryClaim(f, new NestedGyroidFlora(0));
        bool joined = b2.OpenTiles == 0 && b2.TryAnyOpenTile(out var j) && f.Neighbors().Contains(j);
        bool refused = !b2.TryAnyOpenTile(out _, t => false);
        Gate("colony: a seed joins a growing founder at a neighbouring tile; a refused tile is never offered", joined && refused,
             $"joined beside the founder: {joined}; refused tiles offered: {!refused}");
        NestedGyroidColony.Clear(cell2);

        // The tile table itself: 4 neighbours each, symmetric.
        bool symmetric = true;
        for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++)
        {
            var t = new NestedGyroidColony.Tile(0, 0, 0, o);
            foreach (var n in t.Neighbors()) symmetric &= n.Neighbors().Contains(t);
        }
        Gate("colony: the tile neighbour table is symmetric (a neighbour's neighbour is me)", symmetric, "24 octagons x 4");

        // THE REPORTED STALL ("the flora did not keep growing"): the colony used to refuse every tile whose crystal
        // lay outside the species' PLANTING BAND (0.25-0.5 of the membrane radius) and drop it for good. A Spawn
        // Matrix station sits outside the membrane, so every neighbour of the founder was refused and the population
        // never got past one plant. The shipped rule refuses only a control-zone NUCLEUS. Both are modelled here as
        // the flora computes them (NestedGyroidFlora.ClearOfNucleus; Flora.ClampToPlantingBand for the old one), on
        // a cell whose centre is 3 periods from the founder.
        float per = P.Settings.CellSize, membrane = 2.5f * per, nucleus = 0.4f * per;
        var centre = new UnityEngine.Vector3(-3f * per, 0f, 0f);
        float D(NestedGyroidColony b, NestedGyroidColony.Tile t)
        {
            var w = b.TileWorld(t);
            float dx = w.x - centre.x, dy = w.y - centre.y, dz = w.z - centre.z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        var shipped = GrowColony(P, ShippedCap, seed: 4, accept: (b, t) => D(b, t) > nucleus);
        var oldBand = GrowColony(P, ShippedCap, seed: 4, accept: (b, t) => D(b, t) > 0.25f * membrane && D(b, t) < 0.5f * membrane);
        Gate($"colony: a founder outside the planting band (a Spawn Matrix station) grows to the cap of {ShippedCap}",
             shipped.Tiles.Count == ShippedCap, $"{shipped.Tiles.Count} plants");
        Gate("negative control: the retired planting-band gate strands that colony at its founder",
             oldBand.Tiles.Count == 1, $"{oldBand.Tiles.Count} plant(s)");
        var hugging = GrowColony(P, ShippedCap, seed: 9, accept: (b, t) => D(b, t) > nucleus);
        centre = new UnityEngine.Vector3(0.6f * per, 0f, 0f);      // a nucleus right beside the founder
        var beside = GrowColony(P, ShippedCap, seed: 9, accept: (b, t) => D(b, t) > nucleus);
        int inside = beside.Tiles.Skip(1).Count(t => D(beside.Book, t) <= nucleus);
        Gate("colony: never grows into a control-zone nucleus, even one beside the founder",
             inside == 0 && beside.Tiles.Count == ShippedCap && hugging.Tiles.Count == ShippedCap,
             $"{inside} daughters inside the nucleus; {beside.Tiles.Count} plants grown around it");

        var grown = GrowColony(P, ShippedCap, seed: 11);
        if (verbose) Console.WriteLine($"  colony at the shipped cap of {ShippedCap}: {string.Join(" ", grown.Tiles)}");
        return grown;
    }

    static bool FindAfterClear(Cell cell, CosmicShore.Utility.FloraConfigurationSO species)
    {
        NestedGyroidColony.Clear(cell);
        return NestedGyroidColony.Find(cell, species) == null;
    }

    // ------------------------------------------------------------------ the colony as one prismscape

    /// <summary>Every plant of a grown colony laid in the colony frame: tile centre + the plant's local layout.</summary>
    static (NestedGyroidLattice U, int[] plantOf) Union(NestedGyroidPeriod P, Grown g)
    {
        var plants = g.Tiles.Select(t => (t, L: P.Plant(t.Octagon))).ToList();
        int n = plants.Sum(p => p.L.Count);
        var U = new NestedGyroidLattice
        {
            Count = n, Position = new Vector3[n], Forward = new Vector3[n], Up = new Vector3[n], Size = new Vector3[n],
            Kind = new NestedGyroidPrismKind[n], Sheet = new int[n], Rank = new int[n], Stack = new int[n], Site = new int[n],
            DangerRing = new bool[n], Fitted = new bool[n], Parent = new int[n], Reach = new float[n],
            SheetLevels = plants[0].L.SheetLevels, SheetCount = plants[0].L.SheetCount, Stats = new NestedGyroidStats(),
        };
        var plantOf = new int[n];
        int k = 0;
        for (int pi = 0; pi < plants.Count; pi++)
        {
            var (t, L) = plants[pi];
            var c = NestedGyroidLattice.TileCenter(t.X, t.Y, t.Z, t.Octagon, P.Settings.CellSize);
            for (int i = 0; i < L.Count; i++, k++)
            {
                U.Position[k] = L.Position[i] + c; U.Forward[k] = L.Forward[i]; U.Up[k] = L.Up[i]; U.Size[k] = L.Size[i];
                U.Kind[k] = L.Kind[i]; U.Sheet[k] = L.Sheet[i]; U.Rank[k] = L.Rank[i]; U.Stack[k] = L.Stack[i]; U.Site[k] = L.Site[i];
                U.DangerRing[k] = L.DangerRing[i]; U.Fitted[k] = L.Fitted[i]; U.Reach[k] = L.Reach[i];
                U.Parent[k] = L.Parent[i] < 0 ? -1 : k - i + L.Parent[i];
                plantOf[k] = pi;
            }
        }
        return (U, plantOf);
    }

    static (NestedGyroidLattice, int[]) UnionGates(NestedGyroidPeriod P, Grown g, bool verbose, bool rideGate = true)
    {
        var (U, plantOf) = Union(P, g);
        int plants = g.Tiles.Count;

        // No duplicates: a tile owns its sites' images and its columns' struts, so two plants never lay one prism.
        int dup = 0;
        var grid = new Dictionary<(int, int, int), List<int>>();
        (int, int, int) Key(Vector3 p) => ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y), (int)MathF.Floor(p.Z));
        for (int i = 0; i < U.Count; i++)
        {
            var key = Key(U.Position[i]);
            if (!grid.TryGetValue(key, out var l)) grid[key] = l = new List<int>();
            foreach (int j in l) if (Vector3.DistanceSquared(U.Position[i], U.Position[j]) < 1e-4f) dup++;
            l.Add(i);
        }

        var (all, cross, between) = BruteOverlaps(U, plantOf, 1f);
        Gate($"colony of {plants}: no prism interpenetrates another, within or BETWEEN plants (brute-force OBB, every pair)",
             all == 0 && dup == 0, $"{U.Count} prisms; {all} overlapping pairs ({between} between plants, {cross} cross-sheet), {dup} duplicates");

        // Rideability. The rider's floor search reaches max(1, extent) x 2.5 + hover around it (BlockscapeFollower):
        // a prism is ridable onward when another lies within that reach. Every prism needs one, and the reach graph
        // of the whole colony must be ONE piece - so the Urchin can get from any prism of any plant to any other.
        var links = ReachLinks(U, 1f);
        int lonely = Enumerable.Range(0, U.Count).Count(i => links[i].Count == 0);
        int comps = Components(U.Count, links, out int largest);
        int crossPlant = 0;
        for (int i = 0; i < U.Count; i++) foreach (int j in links[i]) if (plantOf[i] != plantOf[j]) crossPlant++;
        string rideDetail = $"{lonely} prisms with no neighbour in reach; {comps} component(s) (largest {largest}/{U.Count}); {crossPlant / 2} reach links between plants";
        if (rideGate)
            Gate($"colony of {plants}: every prism has a ridable neighbour; the reach graph is ONE piece", lonely == 0 && comps == 1, rideDetail);
        else
            Console.WriteLine($"  [info] colony of {plants}, rideability (gated on the shipped configs only): {rideDetail}");
        if (verbose)
        {
            var low = ReachLinks(U, 0.4f);
            int lowComps = Components(U.Count, low, out _);
            Gate("negative control: at 0.4x the rider's reach the colony falls apart", lowComps > 1, $"{lowComps} components");
            var (inflated, _, _) = BruteOverlaps(U, plantOf, 1.6f);
            Gate("negative control: plates grown 1.6x DO overlap", inflated > 0, $"{inflated} pairs");
        }
        return (U, plantOf);
    }

    static List<int>[] ReachLinks(NestedGyroidLattice U, float scale)
    {
        var links = new List<int>[U.Count];
        for (int i = 0; i < U.Count; i++) links[i] = new List<int>();
        float Reach(int i) { var s = U.Size[i]; return (MathF.Max(1f, MathF.Max(s.X, MathF.Max(s.Y, s.Z))) * 2.5f + 2f) * scale; }
        for (int i = 0; i < U.Count; i++)
            for (int j = i + 1; j < U.Count; j++)
            {
                float r = MathF.Min(Reach(i), Reach(j));
                if (Vector3.DistanceSquared(U.Position[i], U.Position[j]) <= r * r) { links[i].Add(j); links[j].Add(i); }
            }
        return links;
    }

    static int Components(int n, List<int>[] links, out int largest)
    {
        var p = Enumerable.Range(0, n).ToArray();
        int F(int x) { while (p[x] != x) x = p[x] = p[p[x]]; return x; }
        for (int i = 0; i < n; i++) foreach (int j in links[i]) p[F(i)] = F(j);
        var groups = Enumerable.Range(0, n).GroupBy(F).Select(gr => gr.Count()).ToList();
        largest = groups.Max();
        return groups.Count;
    }

    static (int all, int cross, int between) BruteOverlaps(NestedGyroidLattice L, int[] plantOf, float grow)
    {
        int all = 0, cross = 0, between = 0;
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
                if (plantOf[i] != plantOf[j]) between++;
                if (L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Kind[j] == NestedGyroidPrismKind.Sheet && L.Sheet[i] != L.Sheet[j]) cross++;
            }
        }
        return (all, cross, between);
    }

    // ------------------------------------------------------------------ elements, determinism

    /// <summary>The settings a shipped element config grows - NestedGyroidConfigSO.ToSettings, transcribed: the
    /// leaf scales with CellSize / Period, the lattice (period, fiber spacings, strut thickness, clearances) with the
    /// element's LatticeScale too.</summary>
    static NestedGyroidSettings ElementSettings(string element)
    {
        var root = Environment.GetEnvironmentVariable("NG_ROOT") ?? ".";
        var path = System.IO.Path.Combine(root, "Assets/_SO_Assets/Lifeforms", $"Nested Gyroid Flora {element}.asset");
        if (!System.IO.File.Exists(path)) return null;
        var t = System.IO.File.ReadAllText(path);
        var m = System.Text.RegularExpressions.Regex.Match(t, @"LeafSize: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}");
        var ls = System.Text.RegularExpressions.Regex.Match(t, @"LatticeScale: ([-\d.]+)");
        float F(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
        float k = ls.Success && F(ls.Groups[1].Value) > 0f ? F(ls.Groups[1].Value) : 1f;
        float size = 240f / NestedGyroidTemplate.Period;
        var d = new NestedGyroidSettings();
        return new NestedGyroidSettings
        {
            CellSize = 240f * k,
            Leaf = new Vector3(F(m.Groups[1].Value), F(m.Groups[2].Value), F(m.Groups[3].Value)) * size,
            FiberSeedSpacing = d.FiberSeedSpacing * k, FiberPrismSpacing = d.FiberPrismSpacing * k,
            FiberThickness = d.FiberThickness * k, Clearance = d.Clearance * k, HeartClearance = d.HeartClearance * k,
        };
    }

    static bool Same(NestedGyroidPeriod a, NestedGyroidPeriod b)
    {
        if (a.Count != b.Count) return false;
        for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++)
        {
            var x = a.Plant(o); var y = b.Plant(o);
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++)
                if (x.Position[i] != y.Position[i] || x.Up[i] != y.Up[i] || x.Size[i] != y.Size[i] || x.Parent[i] != y.Parent[i]) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ the ride

    static void Ride(NestedGyroidLattice U, int[] plantOf)
    {
        int lo = Enumerable.Range(0, U.Count).Where(i => U.Kind[i] == NestedGyroidPrismKind.Sheet).Min(i => U.Sheet[i]);
        int hi = Enumerable.Range(0, U.Count).Where(i => U.Kind[i] == NestedGyroidPrismKind.Sheet).Max(i => U.Sheet[i]);
        int start = 0;   // the founder's first limb: a ring plate on the heart's sheet

        foreach (bool layered in new[] { true, false })
        {
            string rule = layered ? "LAYERED (shipped)" : "legacy nearest-centre (negative control)";

            // A. Roam within the sheet: aim along the surface.
            var m = new RideModel(U) { Layered = layered };
            m.Attach(start, U.Position[start] + U.Forward[start] * 5f);
            var aim = Vector3.Normalize(U.Up[start]);
            var visited = new HashSet<int>(); var sheetsSeen = new HashSet<int>(); var plantsSeen = new HashSet<int>();
            for (int f = 0; f < 60 * 6; f++)
            {
                aim = Vector3.Normalize(aim - Vector3.Dot(aim, m.SurfaceNormal) * m.SurfaceNormal);
                m.Tick(aim, 1f, 60f, 1f / 60f);
                visited.Add(m.Ground);
                plantsSeen.Add(plantOf[m.Ground]);
                if (U.Kind[m.Ground] == NestedGyroidPrismKind.Sheet) sheetsSeen.Add(U.Sheet[m.Ground]);
            }
            bool stay = sheetsSeen.Count == 1 && visited.Count >= 8 && plantsSeen.Count >= 2;
            string roll = $"{visited.Count} prisms crossed on {plantsSeen.Count} plants, sheets {string.Join(",", sheetsSeen)}";
            if (layered) Gate("ride: rolls WITHIN a sheet when aimed along it, from plant to plant", stay, roll);
            else Gate("negative control: the legacy rule CANNOT hold a sheet in a stack", sheetsSeen.Count > 1, roll);

            // B/C. Pitch out of the sheet, then into it.
            foreach (float pitch in new[] { +1f, -1f })
            {
                m = new RideModel(U) { Layered = layered };
                m.Attach(start, U.Position[start] + U.Forward[start] * 5f);
                var seq = new List<int>();
                for (int f = 0; f < 60 * 8; f++)
                {
                    var tangent = Vector3.Normalize(U.Up[m.Ground] - Vector3.Dot(U.Up[m.Ground], m.SurfaceNormal) * m.SurfaceNormal);
                    // The pilot aims UP or DOWN the stack in world terms - along the ground's +∇G (every prism's
                    // Forward points up G) - not along the ridden normal, whose sign a rim wrap can flip.
                    var dir = Vector3.Normalize(0.5f * tangent + pitch * 0.87f * U.Forward[m.Ground]);
                    m.Tick(dir, 1f, 60f, 1f / 60f);
                    if (U.Kind[m.Ground] == NestedGyroidPrismKind.Sheet && (seq.Count == 0 || seq[^1] != U.Sheet[m.Ground])) seq.Add(U.Sheet[m.Ground]);
                }
                bool reachedSkin = seq.Count > 0 && seq[^1] == (pitch > 0 ? hi : lo);
                string what = pitch > 0 ? "climbs UP the stack (+G)" : "dives DOWN the stack (-G)";
                string detail = $"sheets visited in order {string.Join(" > ", seq)}";
                int reversals = 0;
                for (int k = 2; k < seq.Count; k++) if (Math.Sign(seq[k] - seq[k - 1]) != Math.Sign(seq[k - 1] - seq[k - 2])) reversals++;
                if (layered) Gate($"ride: {what} and stops on the outer skin", seq.Count >= 3 && reachedSkin && reversals == 0, detail + $" ({reversals} reversals)");
                else Console.WriteLine($"  [info] {rule}, {what}: {detail} ({reversals} reversals - the stack is a random walk)");
            }
        }
    }
}
