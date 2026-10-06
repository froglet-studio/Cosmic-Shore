// THE NESTED GYROID - the growth rule of NestedGyroidFlora, as a pure function of its settings.
// Plain C# with System.Numerics and NO UnityEngine, so this exact file compiles and RUNS headless in
// Tools/Build/nested_gyroid_harness (budget, one spindle tree per plant, every bond inside the Urchin's reach, zero
// overlaps across a whole colony, the template's loops on every sheet, slice timing). Docs/ECOSYSTEM.md §58.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>What a laid prism IS in the stack: a plate of one sheet (the skin) or a strut of a
    /// gradient-flow fiber (the warp that stitches the sheets together).</summary>
    public enum NestedGyroidPrismKind : byte
    {
        Sheet = 1,
        Fiber = 2,
    }

    /// <summary>How the stack reads through its thickness. The contrast is drawn in the domain's own two prism
    /// states - plain and DANGER - plus a darkening shade, never a lighter or whitened one: a colour is a team,
    /// and a lit prism reads as a state the game does not have.</summary>
    public enum NestedGyroidColorMode
    {
        /// <summary>Plain prisms darken from +tMax (unshaded) to -tMax; fibers are danger prisms.</summary>
        GradedByLevel = 1,
        /// <summary>Odd sheets darkened; fibers are danger prisms.</summary>
        AlternatingSheets = 2,
    }

    /// <summary>
    /// Every number the growth rule reads. Copied out of <c>NestedGyroidConfigSO</c> and the plant's element (its
    /// leaf, its lattice scale) plus the rider model the Urchin's ride kernel is built on, so the lattice is a pure
    /// function of this object and nothing else - which is what lets one build be cached for every plant with the
    /// same settings.
    /// </summary>
    public sealed class NestedGyroidSettings
    {
        /// <summary>|t| ceiling. The only critical values of G are ±√2 and ±1.5, so inside |t| &lt; √2 the
        /// gradient never vanishes: the sheets never touch and every gradient line crosses each sheet once.</summary>
        public const float TMaxCeiling = 1.40f;

        /// <summary>World size of one gyroid period - <see cref="NestedGyroidTemplate.Period"/> × the gyroid
        /// flora's lattice scale this plant is grown at.</summary>
        public float CellSize = 240f;
        public int SheetCount = 7;
        public float TMax = 1.2f;
        public int MaxSheets = 7;
        /// <summary>The element's leaf on the t = 0 sheet, in world units (x along the template's own long axis,
        /// z the sheet normal) - the gyroid flora's per-element prism, scaled with its lattice.</summary>
        public Vector3 Leaf = new Vector3(18f, 6.8f, 3f);
        public float FiberSeedSpacing = 60f;
        public float FiberPrismSpacing = 8f;
        public float PlywoodTwistDegrees = 0f;
        /// <summary>Most prisms ONE plant (one octagon tile, every sheet) may hold.</summary>
        public int PrismBudget = 400;

        /// <summary>A plate is never thicker than this share of its local layer gap.</summary>
        public float PlateThicknessOfGap = 0.45f;
        public float FiberThickness = 1.6f;
        public float FiberFill = 0.8f;
        public float Clearance = 0.5f;
        public float HeartClearance = 8f;

        /// <summary>The Urchin ride kernel's ground search (BlockscapeFollower.groundSearchRadiusScale and
        /// hoverHeight): a prism's reach is <c>max(1, largest extent) * scale + hover</c>.</summary>
        public float RiderGroundSearchScale = 2.5f;
        public float RiderHoverHeight = 2f;

        public int Seed = 1;

        public NestedGyroidSettings Clone() => (NestedGyroidSettings)MemberwiseClone();

        /// <summary>Clamps every field into the range the rule is defined on. Returns a copy.</summary>
        public NestedGyroidSettings Sanitized()
        {
            var s = Clone();
            s.CellSize = Math.Max(10f, s.CellSize);
            s.SheetCount = Math.Max(2, Math.Min(15, s.SheetCount));
            s.TMax = Math.Max(0.05f, Math.Min(TMaxCeiling, s.TMax));
            s.MaxSheets = Math.Max(1, Math.Min(s.SheetCount, s.MaxSheets));
            s.Leaf = Vector3.Max(new Vector3(0.1f), s.Leaf);
            s.FiberSeedSpacing = Math.Max(1f, s.FiberSeedSpacing);
            s.FiberPrismSpacing = Math.Max(0.5f, s.FiberPrismSpacing);
            s.PrismBudget = Math.Max(16, s.PrismBudget);
            s.PlateThicknessOfGap = Math.Max(0.02f, Math.Min(0.9f, s.PlateThicknessOfGap));
            s.FiberThickness = Math.Max(0.1f, s.FiberThickness);
            s.FiberFill = Math.Max(0.1f, Math.Min(0.95f, s.FiberFill));
            s.Clearance = Math.Max(0f, s.Clearance);
            s.HeartClearance = Math.Max(0f, s.HeartClearance);
            s.RiderGroundSearchScale = Math.Max(0.1f, s.RiderGroundSearchScale);
            s.RiderHoverHeight = Math.Max(0f, s.RiderHoverHeight);
            return s;
        }

        /// <summary>Identity of the period these settings grow - the build cache key.</summary>
        public string Key() => string.Join("|", new object[]
        {
            CellSize, SheetCount, TMax, MaxSheets, Leaf.X, Leaf.Y, Leaf.Z, FiberSeedSpacing, FiberPrismSpacing,
            PlywoodTwistDegrees, PrismBudget, PlateThicknessOfGap, FiberThickness, FiberFill, Clearance,
            HeartClearance, RiderGroundSearchScale, RiderHoverHeight, Seed,
        });
    }

    /// <summary>The measurements of a period build (and, per plant, of its plan).</summary>
    public sealed class NestedGyroidStats
    {
        public int SheetPrisms, FiberPrisms, DangerPrisms, Fibers;
        public int SheetsGrown, TemplateSites;
        /// <summary>The world period the stack was built at.</summary>
        public float CellSize;
        public int OverlapsBeforeFit, CrossSheetOverlapsBeforeFit, DroppedByFit, DroppedStruts;
        public int RemainingOverlaps, RemainingCrossSheetOverlaps;
        public int TruncatedByBudget;
        /// <summary>Plant: prisms that hang off the heart through an unbroken chain of spindles (must be all).</summary>
        public int RootedPrisms;
        /// <summary>Per plant: the spindle bonds' lengths, min / median / max.</summary>
        public float BondMin, BondMedian, BondMax;
        /// <summary>Worst bond length over the smaller of its two endpoints' rider reach. &lt; 1 = every
        /// bond is within the Urchin's reach.</summary>
        public float MaxEdgeOverReach;
        public float ReachMin;
        public float SheetGapWorldMin, SheetGapWorldMax;
        public float ThicknessMin, ThicknessMax;
        /// <summary>How far the template stretches in-plane as it is carried out along ∇G (min / max over prisms).</summary>
        public float StretchMin, StretchMax;
        public double BuildMilliseconds;
    }

    /// <summary>
    /// ONE PLANT: one octagon tile of the template on every grown sheet, plus the struts of its strutted columns,
    /// in growth order. Positions are in the plant's local space with the CRYSTAL at the origin (the octagon's
    /// centre on the t = 0 level). Every prism's parent is laid before it (<c>Parent[i] &lt; i</c>, -1 = the heart),
    /// and EVERY bond to a parent is a limb - a spindle - so every prism hangs off the crystal through an unbroken
    /// chain of spindles. A prism's local +z (Forward) is its RIDE NORMAL, pointing UP G; +y (Up) is the template's.
    /// </summary>
    public sealed class NestedGyroidLattice
    {
        public int Octagon;
        public int Count;
        public Vector3[] Position, Forward, Up, Size;
        public NestedGyroidPrismKind[] Kind;
        /// <summary>Sheet index 0..N-1 (level -tMax..+tMax). A fiber strut carries the OUTER sheet of its gap.</summary>
        public int[] Sheet;
        /// <summary>Growth ring: |sheet - centre|. 0 = the t = 0 sheet (or the two central sheets for even N).</summary>
        public int[] Rank;
        /// <summary>Position THROUGH the stack: sheet i is 2i, a strut between sheets i and i+1 is 2i+1. Increases
        /// with G, i.e. along every prism's Forward. The same numbers in every plant of a colony - what the Urchin's
        /// layered ride reads to tell "beside me" from "the next layer".</summary>
        public int[] Stack;
        /// <summary>The template site a plate is the image of (-1 for a strut).</summary>
        public int[] Site;
        /// <summary>True for a plate of one of the template's four danger block types - the octagon ring.</summary>
        public bool[] DangerRing;
        /// <summary>True where the zero-overlap fit had to shrink this prism below its rule size.</summary>
        public bool[] Fitted;
        public int[] Parent;
        public float[] Reach;
        public float[] SheetLevels;
        public int SheetCount;
        public NestedGyroidStats Stats;

        public float Gradient01(int i) =>
            SheetCount <= 1 ? 0.5f : Sheet[i] / (float)(SheetCount - 1);

        /// <summary>The plant's position in its colony: where tile (cell, octagon)'s crystal sits, relative to the
        /// colony's lattice origin, in the colony's frame (world units).</summary>
        public static Vector3 TileCenter(int cx, int cy, int cz, int octagon, float cellSize) =>
            (NestedGyroidTemplate.OctagonCenter[octagon] + new Vector3(cx, cy, cz)) * cellSize;
    }

    /// <summary>
    /// ONE PERIOD of the whole nested stack - every tile of the cubic cell, every sheet, every strut - built and
    /// FITTED as a periodic structure (minimum-image distances), so a plant laid at any tile of any cell clears
    /// its neighbours exactly as the colony lays them. <see cref="Plant"/> cuts one tile out of it.
    /// </summary>
    public sealed class NestedGyroidPeriod
    {
        public NestedGyroidSettings Settings;
        public int Count;
        public Vector3[] Position, Forward, Up, Size, Size0;
        public NestedGyroidPrismKind[] Kind;
        public int[] Sheet, Rank, Stack, Site;
        public bool[] Danger, Alive;
        /// <summary>Per strutted column and gap: the inner plate, its struts in order, the outer plate (period node ids).</summary>
        public List<List<int>> Chains;
        /// <summary>[site][sheet] -> period node id, -1 if that image does not exist (dropped by the fit).</summary>
        public int[][] PlateAt;
        /// <summary>Per base site: its template mates (the bond table's neighbours) - the in-sheet tree edges.</summary>
        public List<int>[] Mates;
        public float[] Levels;
        public int[] RankOf;
        public int MaxRank;
        public NestedGyroidStats Stats;

        readonly Dictionary<int, NestedGyroidLattice> _plants = new Dictionary<int, NestedGyroidLattice>();

        public float ReachOf(int node)
        {
            var s = Size[node];
            float extent = Math.Max(s.X, Math.Max(s.Y, s.Z));
            return Math.Max(1f, extent) * Settings.RiderGroundSearchScale + Settings.RiderHoverHeight;
        }

        /// <summary>
        /// The plant that owns octagon <paramref name="octagon"/>: its tile's plates on every sheet and the struts of
        /// its strutted columns, positioned with the crystal (the octagon centre) at the origin, grown as ONE spindle
        /// tree out of the crystal - ring images on the heart's sheet(s) first, then along the tile's own bonds,
        /// then out along each site's column ring by ring. Cached per octagon.
        /// </summary>
        public NestedGyroidLattice Plant(int octagon)
        {
            if (_plants.TryGetValue(octagon, out var cached)) return cached;
            float a = Settings.CellSize;
            var centre = NestedGyroidTemplate.OctagonCenter[octagon] * a;

            // The tile's nodes, each with the whole-cell shift that puts it beside this octagon.
            var members = new List<int>();
            for (int s = 0; s < NestedGyroidTemplate.SiteCount; s++)
                if (NestedGyroidTemplate.SiteOwner[s] == octagon) members.Add(s);
            var memberSet = new HashSet<int>(members);
            var local = new Dictionary<int, Vector3>();            // period node -> plant-local position
            Vector3 Shift(int site) => NestedGyroidTemplate.SiteShift[site] * a;
            foreach (int s in members)
                for (int sheet = 0; sheet < Levels.Length; sheet++)
                {
                    int node = PlateAt[s][sheet];
                    if (node >= 0 && Alive[node]) local[node] = Position[node] + Shift(s) - centre;
                }
            var chainsOf = new List<List<int>>();
            foreach (var chain in Chains)
            {
                int s = Site[chain[0]];
                if (!memberSet.Contains(s)) continue;
                chainsOf.Add(chain);
                foreach (int node in chain)
                    if (Alive[node] && Kind[node] == NestedGyroidPrismKind.Fiber) local[node] = Position[node] + Shift(s) - centre;
            }

            // The plant's bond graph: in-sheet template bonds within the tile, and each column's chain.
            var adj = new Dictionary<int, List<int>>();
            void Link(int x, int y)
            {
                if (!adj.TryGetValue(x, out var lx)) adj[x] = lx = new List<int>(6);
                if (!adj.TryGetValue(y, out var ly)) adj[y] = ly = new List<int>(6);
                if (!lx.Contains(y)) lx.Add(y);
                if (!ly.Contains(x)) ly.Add(x);
            }
            foreach (int s in members)
                foreach (int m in Mates[s])
                {
                    if (!memberSet.Contains(m)) continue;
                    for (int sheet = 0; sheet < Levels.Length; sheet++)
                    {
                        int x = PlateAt[s][sheet], y = PlateAt[m][sheet];
                        if (x >= 0 && y >= 0 && Alive[x] && Alive[y]) Link(x, y);
                    }
                }
            // Every member's column: consecutive images on adjacent sheets are a bond (the gradient line through the
            // site), routed through the column's struts where it carries them.
            var strutted = new HashSet<(int, int)>();
            foreach (var chain in chainsOf)
            {
                int prev = -1;
                foreach (int node in chain)
                {
                    if (!Alive[node]) continue;
                    if (prev >= 0) Link(prev, node);
                    prev = node;
                }
                strutted.Add((chain[0], chain[chain.Count - 1]));
            }
            foreach (int s in members)
                for (int sheet = 0; sheet < Levels.Length; sheet++)
                {
                    if (RankOf[sheet] == 0) continue;
                    int inner = InnerSheet(sheet);
                    int x = PlateAt[s][inner], y = PlateAt[s][sheet];
                    if (x >= 0 && y >= 0 && Alive[x] && Alive[y] && !strutted.Contains((x, y))) Link(x, y);
                }
            // Even N: the two central sheets are both ring 0 - the column between them is a bond too.
            if (Settings.SheetCount % 2 == 0)
            {
                int lo = Settings.SheetCount / 2 - 1, hi = lo + 1;
                foreach (int s in members)
                {
                    int x = PlateAt[s][lo], y = PlateAt[s][hi];
                    if (x >= 0 && y >= 0 && Alive[x] && Alive[y] && !strutted.Contains((x, y))) Link(x, y);
                }
            }

            // Growth: out of the CRYSTAL to the ring's images on the heart's own sheet(s), then ring by ring, each ring
            // a multi-source BFS from everything already standing. Every step is a spindle.
            var order = new List<int>();
            var parentOf = new List<int>();
            var orderOf = new Dictionary<int, int>();
            var ringSites = new List<int>();
            foreach (int s in members)
                if (NestedGyroidTemplate.IsDangerType(NestedGyroidTemplate.BlockType[s])) ringSites.Add(s);
            for (int sheet = 0; sheet < Levels.Length; sheet++)
            {
                if (RankOf[sheet] != 0) continue;
                foreach (int s in ringSites)
                {
                    int node = PlateAt[s][sheet];
                    if (node < 0 || !Alive[node] || orderOf.ContainsKey(node)) continue;
                    orderOf[node] = order.Count;
                    order.Add(node);
                    parentOf.Add(-1);
                }
            }
            for (int ring = 0; ring <= MaxRank; ring++)
            {
                var queue = new Queue<int>(order);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    if (!adj.TryGetValue(i, out var li)) continue;
                    foreach (int j in li)
                    {
                        if (orderOf.ContainsKey(j) || Rank[j] > ring) continue;
                        orderOf[j] = order.Count;
                        order.Add(j);
                        parentOf.Add(orderOf[i]);
                        queue.Enqueue(j);
                    }
                }
            }

            int n = Math.Min(order.Count, Settings.PrismBudget);
            var stats = new NestedGyroidStats
            {
                CellSize = a, SheetsGrown = Stats.SheetsGrown, TemplateSites = members.Count,
                TruncatedByBudget = order.Count - n,
            };
            var L = new NestedGyroidLattice
            {
                Octagon = octagon, Count = n,
                Position = new Vector3[n], Forward = new Vector3[n], Up = new Vector3[n], Size = new Vector3[n],
                Kind = new NestedGyroidPrismKind[n], Sheet = new int[n], Rank = new int[n], Stack = new int[n],
                Site = new int[n], DangerRing = new bool[n], Fitted = new bool[n], Parent = new int[n],
                Reach = new float[n], SheetLevels = (float[])Levels.Clone(), SheetCount = Settings.SheetCount,
                Stats = stats,
            };
            var bonds = new List<float>();
            float worst = 0f;
            stats.ThicknessMin = float.MaxValue;
            stats.ReachMin = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                int node = order[k];
                L.Position[k] = local[node];
                L.Forward[k] = Forward[node];
                L.Up[k] = Up[node];
                L.Size[k] = Size[node];
                L.Kind[k] = Kind[node];
                L.Sheet[k] = Sheet[node];
                L.Rank[k] = Rank[node];
                L.Stack[k] = Stack[node];
                L.Site[k] = Kind[node] == NestedGyroidPrismKind.Sheet ? Site[node] : -1;
                L.DangerRing[k] = Kind[node] == NestedGyroidPrismKind.Sheet && Danger[node];
                L.Fitted[k] = Size[node] != Size0[node];
                L.Parent[k] = parentOf[k];
                L.Reach[k] = ReachOf(node);
                stats.ReachMin = Math.Min(stats.ReachMin, L.Reach[k]);
                if (L.Kind[k] == NestedGyroidPrismKind.Sheet)
                {
                    stats.SheetPrisms++;
                    if (L.DangerRing[k]) stats.DangerPrisms++;
                    stats.ThicknessMin = Math.Min(stats.ThicknessMin, L.Size[k].Z);
                    stats.ThicknessMax = Math.Max(stats.ThicknessMax, L.Size[k].Z);
                }
                else stats.FiberPrisms++;
                if (L.Parent[k] >= 0)
                {
                    int p = L.Parent[k];
                    float d = Vector3.Distance(L.Position[k], L.Position[p]);
                    bonds.Add(d);
                    worst = Math.Max(worst, d / Math.Min(L.Reach[k], L.Reach[p]));
                }
            }
            // Rooted: every prism's parent chain ends at the heart (true by construction; counted, not assumed).
            for (int k = 0; k < n; k++)
            {
                int p = k, guard = 0;
                while (p >= 0 && guard++ <= n) p = L.Parent[p];
                if (p == -1 && guard <= n + 1) stats.RootedPrisms++;
            }
            bonds.Sort();
            stats.BondMin = bonds.Count > 0 ? bonds[0] : 0f;
            stats.BondMedian = bonds.Count > 0 ? bonds[bonds.Count / 2] : 0f;
            stats.BondMax = bonds.Count > 0 ? bonds[bonds.Count - 1] : 0f;
            stats.MaxEdgeOverReach = worst;
            if (stats.ThicknessMin == float.MaxValue) stats.ThicknessMin = 0f;
            if (stats.ReachMin == float.MaxValue) stats.ReachMin = 0f;
            var columns = new HashSet<int>();
            foreach (var chain in chainsOf) columns.Add(Site[chain[0]]);
            stats.Fibers = columns.Count;
            _plants[octagon] = L;
            return L;
        }

        int InnerSheet(int sheet)
        {
            float c = (Settings.SheetCount - 1) * 0.5f;
            if (RankOf[sheet] == 0) return sheet;
            return sheet < c ? sheet + 1 : sheet - 1;
        }
    }

    /// <summary>
    /// Builds a <see cref="NestedGyroidPeriod"/> in TIME SLICES: <see cref="Step"/> runs the rule until its
    /// millisecond budget is spent and returns, so a spawning plant spreads the build over frames and never
    /// hitches. <see cref="BuildNow"/> runs it to completion (tests, the harness).
    ///
    /// <para>The rule. The BASE SHEET is the gyroid flora's own tiling (<see cref="NestedGyroidTemplate"/>: 576 sites per
    /// period, its block types, its frames, its 24 octagon tiles), Newton-snapped onto G = 0. Every site is carried
    /// along ∇G/|∇G| (RK4) to every nested level G = t_i, so each sheet is the SAME tiling - the same loop subdivisions,
    /// the same octagon rings - and the gradient line through a site is literally its column through the stack. A
    /// Poisson subset of those lines carries struts. Every plate takes the element's leaf scaled by how far the
    /// template has stretched there and thickened with the local gap Δt/|∇G|, and an exact OBB fit, PERIODIC
    /// (minimum-image), guarantees no two prisms interpenetrate - in one plant or between any two plants of a
    /// colony, since a colony is this period tiled.</para>
    /// </summary>
    public sealed class NestedGyroidBuilder
    {
        const float TwoPi = (float)(2.0 * Math.PI);

        readonly NestedGyroidSettings _s;
        readonly IEnumerator<int> _work;
        readonly Stopwatch _total = new Stopwatch();

        public NestedGyroidPeriod Result { get; private set; }
        public int Slices { get; private set; }
        public double MaxSliceMilliseconds { get; private set; }
        /// <summary>The stage the build is in - diagnostics (which stage a long slice belonged to).</summary>
        public string Stage { get; private set; } = "start";

        public NestedGyroidBuilder(NestedGyroidSettings settings)
        {
            _s = (settings ?? new NestedGyroidSettings()).Sanitized();
            _work = Run();
        }

        /// <summary>Runs until <paramref name="budgetMilliseconds"/> is spent. True once the build is done.</summary>
        public bool Step(double budgetMilliseconds)
        {
            if (Result != null) return true;
            var slice = Stopwatch.StartNew();
            _total.Start();
            // At least one chunk per call, whatever the budget: a budget smaller than one chunk must slow the build,
            // never stall it.
            bool done = false;
            do
            {
                if (!_work.MoveNext()) { done = true; break; }
            } while (slice.Elapsed.TotalMilliseconds < budgetMilliseconds);
            _total.Stop();
            Slices++;
            MaxSliceMilliseconds = Math.Max(MaxSliceMilliseconds, slice.Elapsed.TotalMilliseconds);
            if (done && Result != null) Result.Stats.BuildMilliseconds = _total.Elapsed.TotalMilliseconds;
            return done;
        }

        public static NestedGyroidPeriod BuildNow(NestedGyroidSettings settings)
        {
            var b = new NestedGyroidBuilder(settings);
            while (!b.Step(1e9)) { }
            return b.Result;
        }

        // ------------------------------------------------------------------ the field

        public static float G(Vector3 q) =>
            MathF.Sin(q.X) * MathF.Cos(q.Y) + MathF.Sin(q.Y) * MathF.Cos(q.Z) + MathF.Sin(q.Z) * MathF.Cos(q.X);

        public static Vector3 Grad(Vector3 q)
        {
            float sx = MathF.Sin(q.X), cx = MathF.Cos(q.X);
            float sy = MathF.Sin(q.Y), cy = MathF.Cos(q.Y);
            float sz = MathF.Sin(q.Z), cz = MathF.Cos(q.Z);
            return new Vector3(cx * cy - sz * sx, cy * cz - sx * sy, cz * cx - sy * sz);
        }

        /// <summary>One Newton step onto G = t along the gradient: p ← p − (G − t)·∇G / |∇G|².</summary>
        static Vector3 Newton(Vector3 q, float t)
        {
            var g = Grad(q);
            float gg = g.LengthSquared();
            if (gg < 1e-8f) return q;
            return q - (G(q) - t) / gg * g;
        }

        /// <summary>The periodic minimum image of a difference vector.</summary>
        public static Vector3 MinImage(Vector3 d, float period) => new Vector3(
            d.X - period * MathF.Round(d.X / period),
            d.Y - period * MathF.Round(d.Y / period),
            d.Z - period * MathF.Round(d.Z / period));

        // ------------------------------------------------------------------ build state

        struct Node
        {
            public Vector3 P, F, U, Size, Size0;
            public NestedGyroidPrismKind Kind;
            public int Sheet, Rank, Site;
            public bool Danger;
            public float Gap;
            public bool Alive;
        }

        /// <summary>One template site's gradient line: its image on every sheet, and the polyline between consecutive
        /// images (from the INNER one).</summary>
        sealed class Column
        {
            public Vector3 SeedWorld;
            public readonly Dictionary<int, Vector3> Crossing = new Dictionary<int, Vector3>();
            public readonly Dictionary<int, List<Vector3>> Segment = new Dictionary<int, List<Vector3>>();
            public bool Strutted;
        }

        float _a;              // world period
        float _scale;          // world units per field unit
        float[] _levels;
        int[] _rankOf;
        int _maxRank;
        float _dt;
        List<Node> _nodes;
        Column[] _columns;
        int[][] _plateAt;
        List<int>[] _mates;
        List<List<int>> _chains;
        NestedGyroidStats _stats;
        ulong _rng;
        int Yields;

        IEnumerator<int> Run()
        {
            _stats = new NestedGyroidStats { CellSize = _s.CellSize };
            _nodes = new List<Node>(8192);
            _chains = new List<List<int>>();
            _rng = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)_s.Seed;
            _a = _s.CellSize;
            _scale = _a / TwoPi;

            int n = _s.SheetCount;
            _levels = new float[n];
            _rankOf = new int[n];
            float c = (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float t = -_s.TMax + i * (2f * _s.TMax / (n - 1));
                if (MathF.Abs(t) < 1e-6f) t = 0f;
                _levels[i] = t;
                _rankOf[i] = (int)MathF.Floor(MathF.Abs(i - c) + 1e-4f);
            }
            _dt = 2f * _s.TMax / (n - 1);
            _maxRank = 0;
            for (int r = 0; r <= n; r++)
            {
                int count = 0;
                for (int i = 0; i < n; i++) if (_rankOf[i] <= r) count++;
                if (count <= _s.MaxSheets) _maxRank = r; else break;
            }
            for (int i = 0; i < n; i++) if (_rankOf[i] <= _maxRank) _stats.SheetsGrown++;

            // ---- the base sheet: the gyroid flora's tiling, one period, snapped onto G = 0
            Stage = "template";
            int sites = NestedGyroidTemplate.SiteCount;
            var basePos = new Vector3[sites];
            var baseUp = new Vector3[sites];
            for (int k = 0; k < sites; k++)
            {
                var q = Newton(Newton(NestedGyroidTemplate.Position[k] * TwoPi, 0f), 0f);
                var normal = Vector3.Normalize(Grad(q));
                var up = ProjectOnPlane(NestedGyroidTemplate.Up[k], normal);
                basePos[k] = q * _scale;
                baseUp[k] = up.LengthSquared() > 1e-6f ? Vector3.Normalize(up) : AnyTangent(normal);
                if ((k & 127) == 127) yield return 0;
            }
            _stats.TemplateSites = sites;
            _mates = TemplateMates(basePos);
            yield return 0;

            // ---- every site's column out to every grown level on both sides
            Stage = "transport";
            int centre = Array.IndexOf(_levels, 0f);
            bool centreGrown = centre >= 0 && _rankOf[centre] <= _maxRank;
            _columns = new Column[sites];
            for (int k = 0; k < sites; k++)
            {
                var col = new Column { SeedWorld = basePos[k] };
                if (centreGrown)
                {
                    col.Crossing[centre] = basePos[k];
                    col.Segment[centre] = new List<Vector3> { basePos[k] };
                }
                Trace(col, +1f);
                Trace(col, -1f);
                _columns[k] = col;
                if ((++Yields & 15) == 0) yield return 0;
            }

            // ---- struts ride a periodic Poisson subset of the columns - and EVERY tile carries at least one, because
            // a tile is a plant and a plant without a strut is a stack of separate plates on one crystal
            Stage = "fiber seeds";
            var chosen = new List<int>();
            var thin = PoissonSites(basePos, _s.FiberSeedSpacing, chosen);
            while (thin.MoveNext()) yield return 0;
            foreach (int k in chosen) _columns[k].Strutted = true;

            // ---- plates
            Stage = "plates";
            float twist = _s.PlywoodTwistDegrees * (float)Math.PI / 180f;
            _plateAt = new int[sites][];
            for (int k = 0; k < sites; k++)
            {
                _plateAt[k] = new int[n];
                for (int i = 0; i < n; i++) _plateAt[k][i] = -1;
                foreach (var kv in _columns[k].Crossing)
                {
                    int sheet = kv.Key;
                    var p = kv.Value;
                    var g = Grad(p / _scale);
                    float gl = g.Length();
                    if (gl < 1e-4f) continue;
                    var normal = g / gl;
                    var up = ProjectOnPlane(baseUp[k], normal);
                    up = up.LengthSquared() > 1e-6f ? Vector3.Normalize(up) : AnyTangent(normal);
                    up = Vector3.Normalize(Rotate(up, normal, twist * (sheet - c)));
                    _plateAt[k][sheet] = _nodes.Count;
                    _nodes.Add(new Node
                    {
                        P = p, F = normal, U = up,
                        Kind = NestedGyroidPrismKind.Sheet,
                        Sheet = sheet, Rank = _rankOf[sheet], Site = k,
                        Danger = NestedGyroidTemplate.IsDangerType(NestedGyroidTemplate.BlockType[k]),
                        Gap = GapWorld(p), Alive = !NearAnyHeart(p),
                    });
                }
                if ((++Yields & 31) == 0) yield return 0;
            }

            // ---- sizes, then the struts between the plates they join
            Stage = "sizing";
            SizeSheets(basePos);
            LayStruts();
            yield return 0;

            // ---- fit: no two prisms interpenetrate, across the periodic boundary too
            Stage = "fit";
            var fit = Fit();
            while (fit.MoveNext()) yield return 0;

            Stage = "pack";
            Result = Pack();
        }

        /// <summary>A plate inside the clearance of any octagon centre (a crystal's seat, on any sheet).</summary>
        bool NearAnyHeart(Vector3 p)
        {
            float r2 = _s.HeartClearance * _s.HeartClearance;
            for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++)
                if (MinImage(p - NestedGyroidTemplate.OctagonCenter[o] * _a, _a).LengthSquared() < r2) return true;
            return false;
        }

        /// <summary>Each base site's template mates: its nearest sites within 1.4× the template's own bond length (at
        /// most four - the bond table's four corner sites), periodic.</summary>
        List<int>[] TemplateMates(Vector3[] basePos)
        {
            float bond = 8.0f * _a / NestedGyroidTemplate.Period;
            var grid = new PGrid(_a, 1.4f * bond);
            for (int i = 0; i < basePos.Length; i++) grid.Add(basePos[i], i);
            var mates = new List<int>[basePos.Length];
            var cand = new List<(float d, int j)>(16);
            for (int i = 0; i < basePos.Length; i++)
            {
                grid.Near(basePos[i], _near);
                cand.Clear();
                foreach (int j in _near)
                {
                    if (j == i) continue;
                    float d = MinImage(basePos[j] - basePos[i], _a).Length();
                    if (d < 1.4f * bond) cand.Add((d, j));
                }
                cand.Sort((x, y) => x.d != y.d ? x.d.CompareTo(y.d) : x.j.CompareTo(y.j));
                var l = new List<int>(4);
                for (int m = 0; m < cand.Count && m < 4; m++) l.Add(cand[m].j);
                mates[i] = l;
            }
            return mates;
        }

        int InnerNeighbour(int sheet)
        {
            float c = (_s.SheetCount - 1) * 0.5f;
            if (_rankOf[sheet] == 0) return -1;
            return sheet < c ? sheet + 1 : sheet - 1;
        }

        int StrutInnerSheet(int outer)
        {
            int inner = InnerNeighbour(outer);
            return inner >= 0 ? inner : outer - 1;
        }

        static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - Vector3.Dot(v, n) * n;

        static Vector3 AnyTangent(Vector3 n)
        {
            var a = MathF.Abs(n.X) < 0.8f ? Vector3.UnitX : Vector3.UnitY;
            return Vector3.Normalize(ProjectOnPlane(a, n));
        }

        static Vector3 Rotate(Vector3 v, Vector3 axis, float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }

        float GapWorld(Vector3 world)
        {
            float gl = Grad(world / _scale).Length();
            return _scale * _dt / Math.Max(gl, 1e-3f);
        }

        // ------------------------------------------------------------------ the periodic grid

        /// <summary>A hash grid over ONE PERIOD: positions are wrapped into the cube and neighbour cells wrap, so
        /// <see cref="Near"/> sees across the periodic boundary.</summary>
        sealed class PGrid
        {
            readonly float _period, _cell;
            readonly int _m;
            readonly Dictionary<int, List<int>> _cells = new Dictionary<int, List<int>>();
            readonly HashSet<int> _dedupe = new HashSet<int>();

            public PGrid(float period, float minCell)
            {
                _period = period;
                _m = Math.Max(1, (int)MathF.Floor(period / Math.Max(1e-3f, minCell)));
                _cell = period / _m;
            }

            int Wrap(int i) => ((i % _m) + _m) % _m;
            int Index(float x) => (int)MathF.Floor((x - _period * MathF.Floor(x / _period)) / _cell);
            int Key(int x, int y, int z) => (Wrap(x) * _m + Wrap(y)) * _m + Wrap(z);

            public void Add(Vector3 p, int id)
            {
                int k = Key(Index(p.X), Index(p.Y), Index(p.Z));
                if (!_cells.TryGetValue(k, out var l)) _cells[k] = l = new List<int>(4);
                l.Add(id);
            }

            public void Near(Vector3 p, List<int> into)
            {
                into.Clear();
                int bx = Index(p.X), by = Index(p.Y), bz = Index(p.Z);
                bool small = _m < 3;
                if (small) _dedupe.Clear();
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int k = Key(bx + dx, by + dy, bz + dz);
                    if (small && !_dedupe.Add(k)) continue;
                    if (_cells.TryGetValue(k, out var l)) into.AddRange(l);
                }
            }
        }

        readonly List<int> _near = new List<int>(64);

        // ------------------------------------------------------------------ Poisson over the sites

        ulong NextRandom()
        {
            // splitmix64: deterministic on every runtime, never UnityEngine.Random.
            ulong z = (_rng += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>A periodic Poisson subset of the template's sites at <paramref name="radius"/>, seeded with one
        /// ANCHOR per octagon tile - a random site of its patch (never its danger ring) - accepted before the
        /// spacing test, so every tile is in the subset whatever the spacing. The rest fill in around the anchors.</summary>
        IEnumerator<int> PoissonSites(Vector3[] pos, float radius, List<int> chosen)
        {
            var anchors = new List<int>(NestedGyroidTemplate.OctagonCount);
            var patch = new List<int>();
            for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++)
            {
                patch.Clear();
                for (int i = 0; i < pos.Length; i++)
                    if (NestedGyroidTemplate.SiteOwner[i] == o && !NestedGyroidTemplate.IsDangerType(NestedGyroidTemplate.BlockType[i]))
                        patch.Add(i);
                if (patch.Count > 0) anchors.Add(patch[(int)(NextRandom() % (ulong)patch.Count)]);
            }
            var isAnchor = new HashSet<int>(anchors);
            var order = new List<int>(pos.Length);
            for (int i = 0; i < pos.Length; i++) if (!isAnchor.Contains(i)) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = (int)(NextRandom() % (ulong)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }
            order.InsertRange(0, anchors);
            var grid = new PGrid(_a, radius);
            float r2 = radius * radius;
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                if (k < anchors.Count) { grid.Add(pos[i], i); chosen.Add(i); continue; }
                grid.Near(pos[i], _near);
                bool ok = true;
                foreach (int j in _near) if (MinImage(pos[j] - pos[i], _a).LengthSquared() < r2) { ok = false; break; }
                if (ok) { grid.Add(pos[i], i); chosen.Add(i); }
                if ((k & 255) == 255) yield return 0;
            }
        }

        // ------------------------------------------------------------------ columns

        void Trace(Column col, float dir)
        {
            var targets = new List<int>();
            for (int i = 0; i < _s.SheetCount; i++)
            {
                if (_rankOf[i] > _maxRank) continue;
                if (dir > 0 ? _levels[i] > 0f : _levels[i] < 0f) targets.Add(i);
            }
            targets.Sort((a, b) => MathF.Abs(_levels[a]).CompareTo(MathF.Abs(_levels[b])));
            if (targets.Count == 0) return;

            Vector3 q = col.SeedWorld / _scale;
            float gq = G(q);
            const float ds = 0.03f;
            int next = 0;
            var poly = new List<Vector3> { q * _scale };
            for (int step = 0; step < 2000 && next < targets.Count; step++)
            {
                Vector3 q1 = Rk4(q, dir, ds);
                float g1 = G(q1);
                poly.Add(q1 * _scale);
                while (next < targets.Count)
                {
                    float t = _levels[targets[next]];
                    bool crossed = dir > 0 ? (gq < t && g1 >= t) : (gq > t && g1 <= t);
                    if (!crossed) break;
                    float u = (t - gq) / (g1 - gq);
                    Vector3 x = Newton(Newton(Vector3.Lerp(q, q1, u), t), t);
                    int sheet = targets[next];
                    poly[poly.Count - 1] = x * _scale;
                    col.Crossing[sheet] = x * _scale;
                    col.Segment[sheet] = poly;
                    poly = new List<Vector3> { x * _scale, q1 * _scale };
                    next++;
                }
                q = q1;
                gq = g1;
            }
        }

        static Vector3 Flow(Vector3 q, float dir)
        {
            var g = Grad(q);
            float l = g.Length();
            return l < 1e-6f ? Vector3.Zero : g * (dir / l);
        }

        static Vector3 Rk4(Vector3 q, float dir, float h)
        {
            var k1 = Flow(q, dir);
            var k2 = Flow(q + 0.5f * h * k1, dir);
            var k3 = Flow(q + 0.5f * h * k2, dir);
            var k4 = Flow(q + h * k3, dir);
            return q + h / 6f * (k1 + 2f * k2 + 2f * k3 + k4);
        }

        // ------------------------------------------------------------------ sizing + struts

        void SizeSheets(Vector3[] basePos)
        {
            var baseGaps = new List<float>();
            foreach (var p in basePos) baseGaps.Add(GapWorld(p));
            baseGaps.Sort();
            float gap0 = baseGaps[baseGaps.Count / 2];

            var gaps = new List<float>();
            float smin = float.MaxValue, smax = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var nd = _nodes[i];
                if (nd.Kind != NestedGyroidPrismKind.Sheet) continue;
                gaps.Add(nd.Gap);

                // In-plane stretch: this image's distance to its mates' images on the same sheet, over the base's.
                double sum = 0, sum0 = 0;
                foreach (int m in _mates[nd.Site])
                {
                    if (!_columns[m].Crossing.TryGetValue(nd.Sheet, out var pm)) continue;
                    // The mate's image beside this one (the mate may be a periodic image).
                    var baseD = MinImage(basePos[m] - basePos[nd.Site], _a);
                    var wrap = baseD - (basePos[m] - basePos[nd.Site]);
                    sum += Vector3.Distance(nd.P, pm + wrap);
                    sum0 += baseD.Length();
                }
                float stretch = sum0 > 1e-6 ? (float)(sum / sum0) : 1f;
                stretch = Math.Max(0.5f, Math.Min(2f, stretch));
                smin = Math.Min(smin, stretch);
                smax = Math.Max(smax, stretch);

                // Thickness follows the layer, but a plate stays a PLATE: at most twice the element's own thickness,
                // and never more than its share of the gap.
                float thick = _s.Leaf.Z * (nd.Gap / gap0);
                thick = Math.Max(0.3f, Math.Min(Math.Min(_s.PlateThicknessOfGap * nd.Gap, 2f * _s.Leaf.Z), thick));
                nd.Size = new Vector3(_s.Leaf.X * stretch, _s.Leaf.Y * stretch, thick);
                nd.Size0 = nd.Size;
                _nodes[i] = nd;
            }
            gaps.Sort();
            _stats.SheetGapWorldMin = gaps.Count > 0 ? gaps[0] : 0f;
            _stats.SheetGapWorldMax = gaps.Count > 0 ? gaps[gaps.Count - 1] : 0f;
            _stats.StretchMin = smin == float.MaxValue ? 1f : smin;
            _stats.StretchMax = smax;
        }

        void LayStruts()
        {
            for (int k = 0; k < _columns.Length; k++)
            {
                var col = _columns[k];
                if (!col.Strutted) continue;
                _stats.Fibers++;
                for (int sheet = 0; sheet < _s.SheetCount; sheet++)
                {
                    int outer = _plateAt[k][sheet];
                    if (outer < 0) continue;
                    int inner = InnerNeighbour(sheet);
                    List<Vector3> poly;
                    if (inner < 0)
                    {
                        // Even N: the two central sheets are both ring 0; their gap is the two half-traces, joined.
                        if (_s.SheetCount % 2 != 0 || _levels[sheet] < 0f) continue;
                        inner = sheet - 1;
                        if (!col.Segment.TryGetValue(inner, out var down)) continue;
                        poly = new List<Vector3>(down);
                        poly.Reverse();
                        poly.AddRange(col.Segment[sheet].GetRange(1, col.Segment[sheet].Count - 1));
                    }
                    else poly = col.Segment[sheet];
                    int innerNode = _plateAt[k][inner];
                    if (innerNode < 0) continue;
                    LayChain(k, innerNode, outer, poly, sheet);
                }
            }
        }

        void LayChain(int site, int innerNode, int outerNode, List<Vector3> poly, int outerSheet)
        {
            var cum = new float[poly.Count];
            for (int k = 1; k < poly.Count; k++) cum[k] = cum[k - 1] + Vector3.Distance(poly[k - 1], poly[k]);
            float total = cum[poly.Count - 1];
            float s0 = 0.5f * _nodes[innerNode].Size.Z + _s.Clearance;
            float s1 = total - 0.5f * _nodes[outerNode].Size.Z - _s.Clearance;
            float usable = s1 - s0;
            var chain = new List<int> { innerNode };
            if (usable >= 1f)
            {
                int m = Math.Max(1, (int)MathF.Round(usable / _s.FiberPrismSpacing));
                float slot = usable / m;
                for (int j = 0; j < m; j++)
                {
                    float at = s0 + (j + 0.5f) * slot;
                    var (p, tangent) = Along(poly, cum, at);
                    // Every prism's Forward points up G, so the stack has one orientation on both sides of t = 0.
                    if (Vector3.Dot(tangent, Grad(p / _scale)) < 0f) tangent = -tangent;
                    var up = ProjectOnPlane(_nodes[innerNode].U, tangent);
                    up = up.LengthSquared() > 1e-6f ? Vector3.Normalize(up) : AnyTangent(tangent);
                    var size = new Vector3(_s.FiberThickness, _s.FiberThickness, slot * _s.FiberFill);
                    chain.Add(_nodes.Count);
                    _nodes.Add(new Node
                    {
                        P = p, F = tangent, U = up, Size = size, Size0 = size,
                        Kind = NestedGyroidPrismKind.Fiber,
                        Sheet = outerSheet, Rank = _rankOf[outerSheet], Site = site,
                        Gap = total, Alive = true,
                    });
                }
            }
            chain.Add(outerNode);
            _chains.Add(chain);
        }

        static (Vector3 p, Vector3 tangent) Along(List<Vector3> poly, float[] cum, float at)
        {
            for (int k = 1; k < poly.Count; k++)
            {
                if (cum[k] < at && k < poly.Count - 1) continue;
                float seg = cum[k] - cum[k - 1];
                float u = seg > 1e-6f ? (at - cum[k - 1]) / seg : 0f;
                var d = poly[k] - poly[k - 1];
                var tangent = d.LengthSquared() > 1e-10f ? Vector3.Normalize(d) : Vector3.UnitZ;
                return (Vector3.Lerp(poly[k - 1], poly[k], Math.Max(0f, Math.Min(1f, u))), tangent);
            }
            return (poly[0], Vector3.UnitZ);
        }

        // ------------------------------------------------------------------ the fit (exact OBB SAT, periodic)

        /// <summary>True when two oriented boxes (centre, local axes x/y/z, half-extents) intersect.</summary>
        public static bool ObbOverlap(Vector3 ca, Vector3 xa, Vector3 ya, Vector3 za, Vector3 ha,
                                      Vector3 cb, Vector3 xb, Vector3 yb, Vector3 zb, Vector3 hb)
        {
            Span<Vector3> a = stackalloc Vector3[] { xa, ya, za };
            Span<Vector3> b = stackalloc Vector3[] { xb, yb, zb };
            Span<float> ea = stackalloc float[] { ha.X, ha.Y, ha.Z };
            Span<float> eb = stackalloc float[] { hb.X, hb.Y, hb.Z };
            var d = cb - ca;
            for (int i = 0; i < 3; i++) if (Separated(a[i], d, a, ea, b, eb)) return false;
            for (int i = 0; i < 3; i++) if (Separated(b[i], d, a, ea, b, eb)) return false;
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                var axis = Vector3.Cross(a[i], b[j]);
                if (axis.LengthSquared() < 1e-8f) continue;
                if (Separated(Vector3.Normalize(axis), d, a, ea, b, eb)) return false;
            }
            return true;
        }

        static bool Separated(Vector3 axis, Vector3 d, Span<Vector3> a, Span<float> ea, Span<Vector3> b, Span<float> eb)
        {
            float ra = 0f, rb = 0f;
            for (int k = 0; k < 3; k++)
            {
                ra += ea[k] * MathF.Abs(Vector3.Dot(a[k], axis));
                rb += eb[k] * MathF.Abs(Vector3.Dot(b[k], axis));
            }
            return MathF.Abs(Vector3.Dot(d, axis)) > ra + rb;
        }

        bool Overlaps(int i, int j, float margin)
        {
            var a = _nodes[i];
            var b = _nodes[j];
            var xa = Vector3.Cross(a.U, a.F);
            var xb = Vector3.Cross(b.U, b.F);
            var m = new Vector3(margin);
            var cb = a.P + MinImage(b.P - a.P, _a);
            return ObbOverlap(a.P, xa, a.U, a.F, a.Size * 0.5f + m, cb, xb, b.U, b.F, b.Size * 0.5f + m);
        }

        static float Radius(Node n) => 0.5f * n.Size.Length();

        IEnumerator<int> OverlappingPairs(float margin, List<(int, int)> pairs)
        {
            pairs.Clear();
            float maxR = 0f;
            foreach (var nd in _nodes) if (nd.Alive) maxR = Math.Max(maxR, Radius(nd));
            var grid = new PGrid(_a, Math.Max(1f, 2f * maxR + 2f * margin));
            for (int i = 0; i < _nodes.Count; i++) if (_nodes[i].Alive) grid.Add(_nodes[i].P, i);
            for (int i = 0; i < _nodes.Count; i++)
            {
                if ((i & 127) == 127) yield return 0;
                if (!_nodes[i].Alive) continue;
                grid.Near(_nodes[i].P, _near);
                foreach (int j in _near)
                {
                    if (j <= i || !_nodes[j].Alive) continue;
                    float reach = Radius(_nodes[i]) + Radius(_nodes[j]) + 2f * margin;
                    if (MinImage(_nodes[j].P - _nodes[i].P, _a).LengthSquared() > reach * reach) continue;
                    if (Overlaps(i, j, margin)) pairs.Add((i, j));
                }
            }
        }

        bool CrossSheet(int i, int j) =>
            _nodes[i].Kind == NestedGyroidPrismKind.Sheet && _nodes[j].Kind == NestedGyroidPrismKind.Sheet &&
            _nodes[i].Sheet != _nodes[j].Sheet;

        IEnumerator<int> Fit()
        {
            float margin = 0.5f * _s.Clearance;
            var pairs = new List<(int, int)>();
            var scan = OverlappingPairs(margin, pairs);
            while (scan.MoveNext()) yield return 0;
            _stats.OverlapsBeforeFit = pairs.Count;
            foreach (var (i, j) in pairs) if (CrossSheet(i, j)) _stats.CrossSheetOverlapsBeforeFit++;
            yield return 0;

            for (int pass = 0; pass < 18 && pairs.Count > 0; pass++)
            {
                var touched = new HashSet<int>();
                foreach (var (i, j) in pairs) { touched.Add(i); touched.Add(j); }
                // STRUTS NEVER GIVE WAY - they are the stack's stitches. A shrunk strut has a shorter rider reach than
                // the bond it spans, and a dropped one leaves the chain bridging two slots. The plates beside it shrink.
                foreach (int i in touched)
                    if (_nodes[i].Kind == NestedGyroidPrismKind.Sheet) Shrink(i, 0.88f);
                scan = OverlappingPairs(margin, pairs);
                while (scan.MoveNext()) yield return 0;
            }

            // Anything still touching at its floor: a plate touching a strut goes; otherwise the outer ring's.
            foreach (var (i, j) in pairs)
            {
                if (!_nodes[i].Alive || !_nodes[j].Alive) continue;
                int drop = _nodes[j].Rank > _nodes[i].Rank || (_nodes[j].Rank == _nodes[i].Rank && j > i) ? j : i;
                if (_nodes[i].Kind != _nodes[j].Kind)
                    drop = _nodes[i].Kind == NestedGyroidPrismKind.Sheet ? i : j;
                var nd = _nodes[drop];
                nd.Alive = false;
                _nodes[drop] = nd;
                _stats.DroppedByFit++;
                if (nd.Kind == NestedGyroidPrismKind.Fiber) _stats.DroppedStruts++;
            }

            var remaining = new List<(int, int)>();
            scan = OverlappingPairs(0f, remaining);
            while (scan.MoveNext()) yield return 0;
            _stats.RemainingOverlaps = remaining.Count;
            foreach (var (i, j) in remaining) if (CrossSheet(i, j)) _stats.RemainingCrossSheetOverlaps++;
        }

        void Shrink(int i, float k)
        {
            var nd = _nodes[i];
            const float floor = 0.35f;
            if (nd.Kind == NestedGyroidPrismKind.Sheet)
            {
                // The footprint gives way first; a plate whose footprint is already at its floor and still touches
                // is touching THROUGH its thickness, so the thickness gives way next.
                bool footprintAtFloor = nd.Size.X <= nd.Size0.X * floor * 1.0001f && nd.Size.Y <= nd.Size0.Y * floor * 1.0001f;
                nd.Size.X = Math.Max(nd.Size0.X * floor, nd.Size.X * k);
                nd.Size.Y = Math.Max(nd.Size0.Y * floor, nd.Size.Y * k);
                if (footprintAtFloor) nd.Size.Z = Math.Max(nd.Size0.Z * floor, nd.Size.Z * k);
            }
            else nd.Size.Z = Math.Max(nd.Size0.Z * floor, nd.Size.Z * k);
            _nodes[i] = nd;
        }

        // ------------------------------------------------------------------ pack

        NestedGyroidPeriod Pack()
        {
            int count = _nodes.Count;
            var P = new NestedGyroidPeriod
            {
                Settings = _s, Count = count,
                Position = new Vector3[count], Forward = new Vector3[count], Up = new Vector3[count],
                Size = new Vector3[count], Size0 = new Vector3[count], Kind = new NestedGyroidPrismKind[count],
                Sheet = new int[count], Rank = new int[count], Stack = new int[count], Site = new int[count],
                Danger = new bool[count], Alive = new bool[count],
                Chains = _chains, PlateAt = _plateAt, Mates = _mates,
                Levels = (float[])_levels.Clone(), RankOf = (int[])_rankOf.Clone(), MaxRank = _maxRank,
                Stats = _stats,
            };
            for (int i = 0; i < count; i++)
            {
                var nd = _nodes[i];
                P.Position[i] = nd.P;
                P.Forward[i] = nd.F;
                P.Up[i] = nd.U;
                P.Size[i] = nd.Size;
                P.Size0[i] = nd.Size0;
                P.Kind[i] = nd.Kind;
                P.Sheet[i] = nd.Sheet;
                P.Rank[i] = nd.Rank;
                P.Site[i] = nd.Site;
                P.Danger[i] = nd.Kind == NestedGyroidPrismKind.Sheet && nd.Danger;
                P.Alive[i] = nd.Alive;
                P.Stack[i] = nd.Kind == NestedGyroidPrismKind.Sheet ? 2 * nd.Sheet : nd.Sheet + StrutInnerSheet(nd.Sheet);
                if (!nd.Alive) continue;
                if (nd.Kind == NestedGyroidPrismKind.Sheet)
                {
                    _stats.SheetPrisms++;
                    if (nd.Danger) _stats.DangerPrisms++;
                }
                else _stats.FiberPrisms++;
            }
            return P;
        }

        /// <summary>One line for the console - the counts a plant reports.</summary>
        public static string Describe(NestedGyroidLattice L, NestedGyroidPeriod period)
        {
            var s = L.Stats;
            var p = period.Stats;
            return $"plant {L.Count} prisms ({s.SheetPrisms} plate / {s.FiberPrisms} strut, {s.DangerPrisms} danger-ring, " +
                   $"{s.TemplateSites}-site tile, {s.SheetsGrown} sheets), {s.RootedPrisms}/{L.Count} on the crystal's spindle tree, " +
                   $"bonds min/median/max {s.BondMin:F2}/{s.BondMedian:F2}/{s.BondMax:F2} (worst {s.MaxEdgeOverReach:P0} of reach), " +
                   $"budget cut {s.TruncatedByBudget}; period: {p.SheetPrisms + p.FiberPrisms} prisms, overlaps {p.RemainingOverlaps} " +
                   $"({p.RemainingCrossSheetOverlaps} cross-sheet), fit dropped {p.DroppedByFit}, stretch {p.StretchMin:F2}..{p.StretchMax:F2}, " +
                   $"build {p.BuildMilliseconds:F0} ms";
        }
    }
}
