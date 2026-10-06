// THE NESTED GYROID - the growth rule of NestedGyroidFlora, as a pure function of its settings.
// Plain C# with System.Numerics and NO UnityEngine, so this exact file compiles and RUNS headless in
// Tools/Build/nested_gyroid_harness (the acceptance gates: budget, one component, gap < reach, zero
// cross-sheet overlaps, slice timing). The flora is the only Unity-side reader. Docs/ECOSYSTEM.md §58.
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

    /// <summary>How sheet plates are shaded inside their domain colour (the hue stays the domain's - a
    /// colour is a team, never a decoration). Fibers always take the third, whitened treatment.</summary>
    public enum NestedGyroidColorMode
    {
        GradedByLevel = 1,
        AlternatingSheets = 2,
    }

    /// <summary>
    /// Every number the growth rule reads. Copied out of <c>NestedGyroidConfigSO</c> (plus the rider model
    /// the Urchin's ride kernel is built on) so the lattice is a pure function of this object and nothing
    /// else - which is what lets one plant's build be cached for every plant with the same settings.
    /// </summary>
    public sealed class NestedGyroidSettings
    {
        /// <summary>|t| ceiling. The only critical values of G are ±√2 and ±1.5, so inside |t| &lt; √2 the
        /// gradient never vanishes: the sheets never touch and every gradient line crosses each sheet once.</summary>
        public const float TMaxCeiling = 1.40f;

        public float CellSize = 240f;
        public int CellsPerSide = 1;
        public int SheetCount = 7;
        public float TMax = 1.2f;
        public int MaxSheets = 7;
        public float SheetPoissonSpacing = 20f;
        public float FiberSeedSpacing = 60f;
        public float FiberPrismSpacing = 8f;
        public float PlywoodTwistDegrees = 25f;
        public int PrismBudget = 2600;

        public float PlateLengthOfSpacing = 0.8f;
        public float PlateWidthOfSpacing = 0.45f;
        public float PlateThicknessOfGap = 0.22f;
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

        /// <summary>Clamps every field into the range the rule is defined on. Returns this.</summary>
        public NestedGyroidSettings Sanitized()
        {
            var s = Clone();
            s.CellSize = Math.Max(10f, s.CellSize);
            s.CellsPerSide = Math.Max(1, Math.Min(4, s.CellsPerSide));
            s.SheetCount = Math.Max(2, Math.Min(15, s.SheetCount));
            s.TMax = Math.Max(0.05f, Math.Min(TMaxCeiling, s.TMax));
            s.MaxSheets = Math.Max(1, Math.Min(s.SheetCount, s.MaxSheets));
            s.SheetPoissonSpacing = Math.Max(1f, s.SheetPoissonSpacing);
            s.FiberSeedSpacing = Math.Max(s.SheetPoissonSpacing, s.FiberSeedSpacing);
            s.FiberPrismSpacing = Math.Max(0.5f, s.FiberPrismSpacing);
            s.PrismBudget = Math.Max(16, s.PrismBudget);
            s.PlateLengthOfSpacing = Math.Max(0.05f, Math.Min(1.5f, s.PlateLengthOfSpacing));
            s.PlateWidthOfSpacing = Math.Max(0.05f, Math.Min(1.5f, s.PlateWidthOfSpacing));
            s.PlateThicknessOfGap = Math.Max(0.02f, Math.Min(0.6f, s.PlateThicknessOfGap));
            s.FiberThickness = Math.Max(0.1f, s.FiberThickness);
            s.FiberFill = Math.Max(0.1f, Math.Min(0.95f, s.FiberFill));
            s.Clearance = Math.Max(0f, s.Clearance);
            s.HeartClearance = Math.Max(0f, s.HeartClearance);
            s.RiderGroundSearchScale = Math.Max(0.1f, s.RiderGroundSearchScale);
            s.RiderHoverHeight = Math.Max(0f, s.RiderHoverHeight);
            return s;
        }

        /// <summary>Identity of the lattice these settings grow - the build cache key.</summary>
        public string Key() => string.Join("|", new object[]
        {
            CellSize, CellsPerSide, SheetCount, TMax, MaxSheets, SheetPoissonSpacing, FiberSeedSpacing,
            FiberPrismSpacing, PlywoodTwistDegrees, PrismBudget, PlateLengthOfSpacing, PlateWidthOfSpacing,
            PlateThicknessOfGap, FiberThickness, FiberFill, Clearance, HeartClearance,
            RiderGroundSearchScale, RiderHoverHeight, Seed,
        });
    }

    /// <summary>The measurements a build reports - what the flora logs and the harness asserts.</summary>
    public sealed class NestedGyroidStats
    {
        public int SheetPrisms, FiberPrisms, Fibers, FibersTruncated;
        public int SheetsGrown;
        public int ComponentsBeforePrune, PrunedIslandPrisms, Components;
        public int OverlapsBeforeFit, CrossSheetOverlapsBeforeFit, DroppedByFit;
        public int RemainingOverlaps, RemainingCrossSheetOverlaps;
        public int TruncatedByBudget, CoarsenPasses;
        public float EffectiveSheetSpacing;
        public int InSheetEdges, CrossSheetEdges;
        /// <summary>Per prism, the distance to its nearest graph neighbour: min / median / max.</summary>
        public float GapMin, GapMedian, GapMax;
        public float EdgeMin, EdgeMedian, EdgeMax;
        /// <summary>Worst edge length over the smaller of its two endpoints' rider reach. &lt; 1 = every
        /// bond is within the Urchin's reach.</summary>
        public float MaxEdgeOverReach;
        public float ReachMin;
        public float SheetGapWorldMin, SheetGapWorldMax;
        public float ThicknessMin, ThicknessMax;
        /// <summary>Mean neighbour misalignment of the plate long axes (a LINE field, 0-90°), for the
        /// naive per-site projection (the negative control) and the combed field that ships.</summary>
        public float CombBeforeDegrees, CombAfterDegrees;
        public double BuildMilliseconds;
    }

    /// <summary>
    /// The finished stack. Every array is indexed by GROWTH ORDER: prism <c>i</c>'s parent is always
    /// <c>&lt; i</c> (or -1 = the heart), so any prefix is one connected object hanging off the crystal.
    /// Positions are in the plant's local space, heart at the origin (G(0) = 0: the crystal sits on the
    /// t = 0 sheet). A prism's local +z (Forward) is its RIDE NORMAL - the sheet normal ∇G for a plate,
    /// the fiber tangent (also ∇G) for a strut - which is what the Urchin's ride kernel reads.
    /// </summary>
    public sealed class NestedGyroidLattice
    {
        public int Count;
        public Vector3[] Position, Forward, Up, Size;
        public NestedGyroidPrismKind[] Kind;
        /// <summary>Sheet index 0..N-1 (level -tMax..+tMax). A fiber strut carries the OUTER sheet of its gap.</summary>
        public int[] Sheet;
        /// <summary>Growth ring: |sheet - centre|. 0 = the t = 0 sheet (or the two central sheets for even N).</summary>
        public int[] Rank;
        /// <summary>Position THROUGH the stack: sheet i is 2i, a strut in the gap between sheets i and i+1 is
        /// 2i+1. Increases with G, i.e. along every prism's Forward. What the Urchin's layered ride reads to
        /// tell "beside me" from "the next layer".</summary>
        public int[] Stack;
        public int[] Parent;
        /// <summary>True where this prism's bond to its parent is a LIMB (a spindle): the heart bonds and
        /// every bond along a fiber. A plate-to-plate bond inside a sheet is tiling, not a limb.</summary>
        public bool[] LimbBond;
        public int[] AdjStart, Adj;
        public float[] Reach;
        public float[] SheetLevels;
        public int SheetCount;
        public NestedGyroidStats Stats;

        public float Gradient01(int i) =>
            SheetCount <= 1 ? 0.5f : Sheet[i] / (float)(SheetCount - 1);
    }

    /// <summary>
    /// Builds a <see cref="NestedGyroidLattice"/> in TIME SLICES: <see cref="Step"/> runs the rule until its
    /// millisecond budget is spent and returns, so a spawning plant spreads the build over frames and never
    /// hitches. <see cref="BuildNow"/> runs it to completion (tests, the harness).
    ///
    /// <para>The rule, in order: the field on a voxel grid; sign changes of G - t_i refined by Newton onto
    /// each sheet; fiber seeds Poisson-thinned on t = 0 and traced along ∇G/|∇G| to ±tMax, their sheet
    /// crossings FORCED into each sheet's Poisson set so a fiber always lands on a plate; the plates'
    /// long axes combed and twisted per sheet (helicoidal plywood); struts laid between consecutive
    /// crossings; every size taken from the local gap Δt/|∇G| and then FITTED (exact OBB separating-axis
    /// test) so no two prisms interpenetrate; the rider graph (in-sheet links within reach, cross-sheet
    /// links along fibers); islands pruned; growth ordered outward from the heart, ring by ring.</para>
    /// </summary>
    public sealed class NestedGyroidBuilder
    {
        const float TwoPi = (float)(2.0 * Math.PI);

        readonly NestedGyroidSettings _s;
        readonly IEnumerator<int> _work;
        readonly Stopwatch _total = new Stopwatch();

        public NestedGyroidLattice Result { get; private set; }
        public int Slices { get; private set; }
        public double MaxSliceMilliseconds { get; private set; }

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
            bool done = false;
            while (slice.Elapsed.TotalMilliseconds < budgetMilliseconds)
            {
                if (!_work.MoveNext()) { done = true; break; }
            }
            _total.Stop();
            Slices++;
            MaxSliceMilliseconds = Math.Max(MaxSliceMilliseconds, slice.Elapsed.TotalMilliseconds);
            if (done && Result != null) Result.Stats.BuildMilliseconds = _total.Elapsed.TotalMilliseconds;
            return done;
        }

        public static NestedGyroidLattice BuildNow(NestedGyroidSettings settings)
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

        // ------------------------------------------------------------------ build state

        struct Node
        {
            public Vector3 P, F, U, Size, Size0;
            public NestedGyroidPrismKind Kind;
            public int Sheet, Rank, Fiber;
            public float Gap;
            public bool Alive;
        }

        sealed class FiberPath
        {
            public Vector3 SeedWorld;
            // Per sheet index: the crossing (world) and the polyline from the INNER neighbour crossing.
            public readonly Dictionary<int, Vector3> Crossing = new Dictionary<int, Vector3>();
            public readonly Dictionary<int, List<Vector3>> Segment = new Dictionary<int, List<Vector3>>();
            public readonly Dictionary<int, int> NodeAt = new Dictionary<int, int>();
            public bool Truncated;
        }

        float _scale;          // world units per field unit
        float _half;           // field half-extent of the bounding cube
        float[] _levels;
        int[] _rankOf;
        int _maxRank;
        float _dt;
        float _spacing, _fiberSeedSpacing, _fiberPrismSpacing;
        List<Node> _nodes;
        List<FiberPath> _fibers;
        List<(int a, int b)> _chain;
        NestedGyroidStats _stats;
        ulong _rng;

        int Yields;

        IEnumerator<int> Run()
        {
            float coarsen = 1f;
            NestedGyroidLattice lattice = null;
            int passes = 0;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                var once = BuildOnce(coarsen);
                while (once.MoveNext()) yield return 0;
                lattice = _built;
                if (lattice.Count <= _s.PrismBudget) break;
                // Over budget: coarsen the TILING - sheets and fibers together - never truncate the stack
                // first: a truncated stack has no outer skin. Count goes as 1/spacing², so one corrected pass
                // usually lands it.
                coarsen *= MathF.Sqrt(lattice.Count / (float)_s.PrismBudget) * 1.03f;
                passes++;
            }

            if (lattice.Count > _s.PrismBudget)
            {
                // Last resort: the growth-order PREFIX is connected by construction, so a cut is a plant
                // that simply stopped growing early - never an island.
                lattice.Stats.TruncatedByBudget = lattice.Count - _s.PrismBudget;
                Truncate(lattice, _s.PrismBudget);
            }
            lattice.Stats.CoarsenPasses = passes;
            Recount(lattice);
            Result = lattice;
        }

        NestedGyroidLattice _built;

        IEnumerator<int> BuildOnce(float coarsen)
        {
            float spacing = _s.SheetPoissonSpacing * coarsen;
            _spacing = spacing;
            _fiberSeedSpacing = _s.FiberSeedSpacing * coarsen;
            _fiberPrismSpacing = _s.FiberPrismSpacing * coarsen;
            _stats = new NestedGyroidStats { EffectiveSheetSpacing = spacing };
            _nodes = new List<Node>(4096);
            _fibers = new List<FiberPath>();
            _chain = new List<(int, int)>();
            _rng = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)_s.Seed;

            _scale = _s.CellSize / TwoPi;
            _half = (float)Math.PI * _s.CellsPerSide;

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

            // maxSheets: the widest ring whose sheet count still fits.
            _maxRank = 0;
            for (int r = 0; r <= n; r++)
            {
                int count = 0;
                for (int i = 0; i < n; i++) if (_rankOf[i] <= r) count++;
                if (count <= _s.MaxSheets) _maxRank = r; else break;
            }
            int grown = 0;
            for (int i = 0; i < n; i++) if (_rankOf[i] <= _maxRank) grown++;
            _stats.SheetsGrown = grown;

            // ---- the field on a voxel grid
            float spacingField = spacing / _scale;
            float h = spacingField * 0.3f;
            int g = Math.Min(160, (int)MathF.Ceiling(2f * _half / h) + 1);
            h = 2f * _half / (g - 1);
            var field = new float[g * g * g];
            for (int ix = 0; ix < g; ix++)
            {
                for (int iy = 0; iy < g; iy++)
                for (int iz = 0; iz < g; iz++)
                    field[(ix * g + iy) * g + iz] = G(GridPoint(ix, iy, iz, h));
                if ((++Yields & 3) == 0) yield return 0;
            }

            // ---- sheet candidates (sign changes refined by Newton) for every grown sheet, and t = 0
            var candidates = new Dictionary<int, List<Vector3>>();
            for (int i = 0; i < n; i++)
            {
                if (_rankOf[i] > _maxRank) continue;
                var list = new List<Vector3>(8192);
                var scan = ScanLevel(field, g, h, _levels[i], list);
                while (scan.MoveNext()) yield return 0;
                candidates[i] = list;
            }
            List<Vector3> zero;
            int centre = Array.IndexOf(_levels, 0f);
            if (centre >= 0 && candidates.ContainsKey(centre)) zero = candidates[centre];
            else
            {
                zero = new List<Vector3>(8192);
                var scan = ScanLevel(field, g, h, 0f, zero);
                while (scan.MoveNext()) yield return 0;
            }

            // ---- fibers: Poisson seeds on t = 0, traced along ±∇G/|∇G|
            var seeds = new List<Vector3>();
            var thin = Poisson(zero, _fiberSeedSpacing, null, seeds);
            while (thin.MoveNext()) yield return 0;
            foreach (var seed in seeds)
            {
                var fiber = new FiberPath { SeedWorld = seed };
                if (centre >= 0 && _rankOf[centre] <= _maxRank)
                {
                    fiber.Crossing[centre] = seed;
                    fiber.Segment[centre] = new List<Vector3> { seed };
                }
                Trace(fiber, +1f);
                Trace(fiber, -1f);
                if (fiber.Crossing.Count >= 2) _fibers.Add(fiber);
                if ((++Yields & 7) == 0) yield return 0;
            }
            _stats.Fibers = _fibers.Count;

            // ---- sheets: Poisson per sheet, inner rings first, fiber crossings FORCED in
            var order = new List<int>();
            for (int r = 0; r <= _maxRank; r++)
                for (int i = 0; i < n; i++) if (_rankOf[i] == r) order.Add(i);
            foreach (int i in order)
            {
                var forced = new List<(Vector3 p, FiberPath f)>();
                foreach (var f in _fibers)
                {
                    if (!f.Crossing.TryGetValue(i, out var x)) continue;
                    int inner = InnerNeighbour(i);
                    // A fiber is contiguous from t = 0 outward: it reaches this sheet only if it was admitted
                    // on the sheet inside it.
                    if (inner >= 0 && !f.NodeAt.ContainsKey(inner)) { f.Truncated = true; continue; }
                    forced.Add((x, f));
                }
                var add = AddSheet(i, candidates[i], forced);
                while (add.MoveNext()) yield return 0;
            }
            foreach (var f in _fibers) if (f.Truncated) _stats.FibersTruncated++;

            // ---- orientation: comb the plates' long axes, then twist per sheet (plywood)
            var comb = Comb();
            while (comb.MoveNext()) yield return 0;

            // ---- fiber struts between consecutive crossings
            LayFibers();
            yield return 0;

            // ---- fit: no two prisms interpenetrate
            var fit = Fit();
            while (fit.MoveNext()) yield return 0;

            // ---- rider graph, islands, growth order
            var assemble = Assemble();
            while (assemble.MoveNext()) yield return 0;
        }

        Vector3 GridPoint(int ix, int iy, int iz, float h) =>
            new Vector3(-_half + ix * h, -_half + iy * h, -_half + iz * h);

        /// <summary>The inner sheet of the gap whose outer sheet is <paramref name="outer"/> (even N's central
        /// gap included, whose "outer" is the upper central sheet).</summary>
        int StrutInnerSheet(int outer)
        {
            int inner = InnerNeighbour(outer);
            return inner >= 0 ? inner : outer - 1;
        }

        int InnerNeighbour(int sheet)
        {
            int n = _s.SheetCount;
            float c = (n - 1) * 0.5f;
            if (_rankOf[sheet] == 0) return -1;
            return sheet < c ? sheet + 1 : sheet - 1;
        }

        IEnumerator<int> ScanLevel(float[] field, int g, float h, float t, List<Vector3> into)
        {
            float clear = _s.HeartClearance / _scale;
            for (int ix = 0; ix < g; ix++)
            {
                for (int iy = 0; iy < g; iy++)
                for (int iz = 0; iz < g; iz++)
                {
                    float a = field[(ix * g + iy) * g + iz] - t;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int jx = ix + (axis == 0 ? 1 : 0), jy = iy + (axis == 1 ? 1 : 0), jz = iz + (axis == 2 ? 1 : 0);
                        if (jx >= g || jy >= g || jz >= g) continue;
                        float b = field[(jx * g + jy) * g + jz] - t;
                        if ((a < 0f) == (b < 0f)) continue;
                        float u = a / (a - b);
                        var q = Vector3.Lerp(GridPoint(ix, iy, iz, h), GridPoint(jx, jy, jz, h), u);
                        q = Newton(Newton(q, t), t);
                        if (MathF.Abs(G(q) - t) > 1e-3f || !InBox(q) || q.Length() < clear) continue;
                        into.Add(q * _scale);
                    }
                }
                if ((++Yields & 1) == 0) yield return 0;
            }
        }

        bool InBox(Vector3 q) =>
            MathF.Abs(q.X) <= _half && MathF.Abs(q.Y) <= _half && MathF.Abs(q.Z) <= _half;

        // ------------------------------------------------------------------ Poisson thinning

        ulong NextRandom()
        {
            // splitmix64: deterministic on every runtime, never UnityEngine.Random.
            ulong z = (_rng += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = (int)(NextRandom() % (ulong)(i + 1));
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static long CellKey(Vector3 p, float cell)
        {
            long x = (long)MathF.Floor(p.X / cell) + (1 << 20);
            long y = (long)MathF.Floor(p.Y / cell) + (1 << 20);
            long z = (long)MathF.Floor(p.Z / cell) + (1 << 20);
            return (x << 42) | (y << 21) | z;
        }

        sealed class Grid
        {
            readonly float _cell;
            readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
            public Grid(float cell) { _cell = cell; }
            public void Add(Vector3 p, int id)
            {
                long k = CellKey(p, _cell);
                if (!_cells.TryGetValue(k, out var l)) _cells[k] = l = new List<int>(4);
                l.Add(id);
            }
            public void Near(Vector3 p, List<int> into)
            {
                into.Clear();
                long bx = (long)MathF.Floor(p.X / _cell), by = (long)MathF.Floor(p.Y / _cell), bz = (long)MathF.Floor(p.Z / _cell);
                for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                for (long dz = -1; dz <= 1; dz++)
                {
                    long k = ((bx + dx + (1 << 20)) << 42) | ((by + dy + (1 << 20)) << 21) | (bz + dz + (1 << 20));
                    if (_cells.TryGetValue(k, out var l)) into.AddRange(l);
                }
            }
        }

        readonly List<int> _near = new List<int>(64);

        IEnumerator<int> Poisson(List<Vector3> pool, float radius, List<Vector3> forced, List<Vector3> accepted)
        {
            accepted.Clear();
            var grid = new Grid(radius);
            float r2 = radius * radius;
            if (forced != null)
                foreach (var p in forced) { grid.Add(p, accepted.Count); accepted.Add(p); }
            var shuffled = new List<Vector3>(pool);
            Shuffle(shuffled);
            for (int k = 0; k < shuffled.Count; k++)
            {
                var p = shuffled[k];
                grid.Near(p, _near);
                bool ok = true;
                foreach (int j in _near) if (Vector3.DistanceSquared(accepted[j], p) < r2) { ok = false; break; }
                if (ok)
                {
                    grid.Add(p, accepted.Count);
                    accepted.Add(p);
                }
                if ((k & 1023) == 1023) yield return 0;
            }
        }

        // ------------------------------------------------------------------ fibers

        void Trace(FiberPath fiber, float dir)
        {
            // Targets: the grown sheets on this side of t = 0, nearest first.
            var targets = new List<int>();
            for (int i = 0; i < _s.SheetCount; i++)
            {
                if (_rankOf[i] > _maxRank) continue;
                if (dir > 0 ? _levels[i] > 0f : _levels[i] < 0f) targets.Add(i);
            }
            targets.Sort((a, b) => MathF.Abs(_levels[a]).CompareTo(MathF.Abs(_levels[b])));
            if (targets.Count == 0) return;

            Vector3 q = fiber.SeedWorld / _scale;
            float gq = G(q);
            const float ds = 0.03f;
            int next = 0;
            var poly = new List<Vector3> { q * _scale };
            for (int step = 0; step < 400 && next < targets.Count; step++)
            {
                Vector3 q1 = Rk4(q, dir, ds);
                if (!InBox(q1)) return;   // left the cube: the fiber ends at the last sheet it reached
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
                    fiber.Crossing[sheet] = x * _scale;
                    fiber.Segment[sheet] = poly;
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

        // ------------------------------------------------------------------ sheets

        float GapWorld(Vector3 world)
        {
            float gl = Grad(world / _scale).Length();
            return _scale * _dt / Math.Max(gl, 1e-3f);
        }

        IEnumerator<int> AddSheet(int sheet, List<Vector3> pool, List<(Vector3 p, FiberPath f)> forced)
        {
            // Forced crossings first; two that crowd each other truncate the LATER fiber from here out.
            var admitted = new List<Vector3>();
            var fgrid = new Grid(_spacing);
            float minForced = 0.7f * _spacing;
            var admittedFibers = new List<FiberPath>();
            foreach (var (p, f) in forced)
            {
                fgrid.Near(p, _near);
                bool ok = true;
                foreach (int j in _near) if (Vector3.Distance(admitted[j], p) < minForced) { ok = false; break; }
                if (!ok) { f.Truncated = true; continue; }
                fgrid.Add(p, admitted.Count);
                admitted.Add(p);
                admittedFibers.Add(f);
            }

            var points = new List<Vector3>();
            var thin = Poisson(pool, _spacing, admitted, points);
            while (thin.MoveNext()) yield return 0;
            for (int k = 0; k < points.Count; k++)
            {
                var p = points[k];
                var g = Grad(p / _scale);
                float gl = g.Length();
                if (gl < 1e-4f) continue;
                var node = new Node
                {
                    P = p,
                    F = g / gl,
                    Kind = NestedGyroidPrismKind.Sheet,
                    Sheet = sheet,
                    Rank = _rankOf[sheet],
                    Fiber = -1,
                    Gap = GapWorld(p),
                    Alive = true,
                };
                if (k < admittedFibers.Count)
                {
                    node.Fiber = _fibers.IndexOf(admittedFibers[k]);
                    admittedFibers[k].NodeAt[sheet] = _nodes.Count;
                }
                _nodes.Add(node);
            }
        }

        // ------------------------------------------------------------------ combing + plywood

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

        /// <summary>Line-field angle between two tangents (0-90°): a plate is symmetric under 180°.</summary>
        static float LineAngle(Vector3 a, Vector3 b) =>
            MathF.Acos(Math.Min(1f, MathF.Abs(Vector3.Dot(a, b)))) * (180f / (float)Math.PI);

        IEnumerator<int> Comb()
        {
            int count = _nodes.Count;
            var grid = new Grid(1.5f * _spacing);
            for (int i = 0; i < count; i++) grid.Add(_nodes[i].P, i);

            // Combing graph: same-sheet neighbours within 1.5 spacings, plus the adjacent sheets' plates
            // directly over/under (one base field for the whole stack, so the per-sheet twist reads).
            var nbr = new List<int>[count];
            float same2 = (1.5f * _spacing) * (1.5f * _spacing);
            for (int i = 0; i < count; i++)
            {
                var ni = _nodes[i];
                grid.Near(ni.P, _near);
                var l = new List<int>(12);
                foreach (int j in _near)
                {
                    if (j == i) continue;
                    var nj = _nodes[j];
                    float d2 = Vector3.DistanceSquared(ni.P, nj.P);
                    if (nj.Sheet == ni.Sheet ? d2 < same2 : (Math.Abs(nj.Sheet - ni.Sheet) == 1 && d2 < 0.5f * same2))
                        l.Add(j);
                }
                nbr[i] = l;
                if ((i & 255) == 0) yield return 0;
            }

            // Negative control: the per-site choice (a fixed axis projected into each plane).
            var t = new Vector3[count];
            var reference = Vector3.Normalize(new Vector3(0.31f, 0.83f, 0.46f));
            for (int i = 0; i < count; i++)
            {
                var p = ProjectOnPlane(reference, _nodes[i].F);
                t[i] = p.LengthSquared() > 1e-6f ? Vector3.Normalize(p) : AnyTangent(_nodes[i].F);
            }
            _stats.CombBeforeDegrees = MeanSameSheetAngle(t, nbr);

            // Seed: parallel transport outward from the plate nearest the heart (BFS over the comb graph),
            // so neighbours start agreeing instead of starting from a per-site choice.
            var seen = new bool[count];
            var queue = new Queue<int>();
            for (int start = NearestToOrigin(); start >= 0; start = FirstUnseen(seen))
            {
                seen[start] = true;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    foreach (int j in nbr[i])
                    {
                        if (seen[j]) continue;
                        seen[j] = true;
                        var p = ProjectOnPlane(t[i], _nodes[j].F);
                        t[j] = p.LengthSquared() > 1e-6f ? Vector3.Normalize(p) : AnyTangent(_nodes[j].F);
                        queue.Enqueue(j);
                    }
                }
            }
            yield return 0;

            // Smooth: doubled-angle (sign-free) average over neighbours, Jacobi passes.
            var next = new Vector3[count];
            for (int pass = 0; pass < 12; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    var n = _nodes[i].F;
                    var e1 = t[i];
                    var e2 = Vector3.Cross(n, e1);
                    float cs = 1f, sn = 0f;
                    foreach (int j in nbr[i])
                    {
                        var v = ProjectOnPlane(t[j], n);
                        float l = v.Length();
                        if (l < 1e-4f) continue;
                        float th = MathF.Atan2(Vector3.Dot(v, e2), Vector3.Dot(v, e1));
                        cs += MathF.Cos(2f * th);
                        sn += MathF.Sin(2f * th);
                    }
                    float half = 0.5f * MathF.Atan2(sn, cs);
                    next[i] = Vector3.Normalize(e1 * MathF.Cos(half) + e2 * MathF.Sin(half));
                }
                (t, next) = (next, t);
                yield return 0;
            }
            _stats.CombAfterDegrees = MeanSameSheetAngle(t, nbr);

            // Helicoidal plywood: each sheet's long axis turned a fixed angle per sheet index about ∇G.
            float c = (_s.SheetCount - 1) * 0.5f;
            float step = _s.PlywoodTwistDegrees * (float)Math.PI / 180f;
            for (int i = 0; i < count; i++)
            {
                var node = _nodes[i];
                node.U = Vector3.Normalize(Rotate(t[i], node.F, step * (node.Sheet - c)));
                _nodes[i] = node;
            }
        }

        float MeanSameSheetAngle(Vector3[] t, List<int>[] nbr)
        {
            double sum = 0;
            int pairs = 0;
            for (int i = 0; i < t.Length; i++)
                foreach (int j in nbr[i])
                {
                    if (j <= i || _nodes[j].Sheet != _nodes[i].Sheet) continue;
                    sum += LineAngle(t[i], t[j]);
                    pairs++;
                }
            return pairs > 0 ? (float)(sum / pairs) : 0f;
        }

        int NearestToOrigin()
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].Rank != 0) continue;
                float d = _nodes[i].P.LengthSquared();
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static int FirstUnseen(bool[] seen)
        {
            for (int i = 0; i < seen.Length; i++) if (!seen[i]) return i;
            return -1;
        }

        // ------------------------------------------------------------------ sizing + fiber struts

        void SizeSheets()
        {
            var gaps = new List<float>();
            foreach (var nd in _nodes) if (nd.Kind == NestedGyroidPrismKind.Sheet) gaps.Add(nd.Gap);
            gaps.Sort();
            float median = gaps.Count > 0 ? gaps[gaps.Count / 2] : 1f;
            _stats.SheetGapWorldMin = gaps.Count > 0 ? gaps[0] : 0f;
            _stats.SheetGapWorldMax = gaps.Count > 0 ? gaps[gaps.Count - 1] : 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var nd = _nodes[i];
                if (nd.Kind != NestedGyroidPrismKind.Sheet) continue;
                // Thickness-aware: the layer is Δt/|∇G| thick here, so the plate takes a fixed share of it
                // (thicker near the saddles, where |∇G| is smallest), and its footprint leans the same way.
                float f = Math.Max(0.85f, Math.Min(1.25f, MathF.Sqrt(nd.Gap / median)));
                float width = _spacing * _s.PlateWidthOfSpacing * f;
                // A plate stays a plate: never thicker than three quarters of its own width, however wide
                // the layer it sits in (few sheets / a high tMax make very deep layers).
                float thick = Math.Max(0.3f, Math.Min(0.75f * width, _s.PlateThicknessOfGap * nd.Gap));
                nd.Size = new Vector3(width, _spacing * _s.PlateLengthOfSpacing * f, thick);
                nd.Size0 = nd.Size;
                _nodes[i] = nd;
            }
        }

        void LayFibers()
        {
            SizeSheets();
            for (int fi = 0; fi < _fibers.Count; fi++)
            {
                var f = _fibers[fi];
                foreach (var kv in f.NodeAt)
                {
                    int sheet = kv.Key;
                    int inner = InnerNeighbour(sheet);
                    List<Vector3> poly;
                    if (inner < 0)
                    {
                        // Even N has no t = 0 sheet: its two central sheets are both ring 0, and the
                        // fiber's segment between them is the two half-traces out of the seed, joined.
                        if (_s.SheetCount % 2 != 0 || _levels[sheet] < 0f) continue;
                        inner = sheet - 1;
                        if (!f.Segment.TryGetValue(inner, out var down)) continue;
                        poly = new List<Vector3>(down);
                        poly.Reverse();
                        poly.AddRange(f.Segment[sheet].GetRange(1, f.Segment[sheet].Count - 1));
                    }
                    else poly = f.Segment[sheet];
                    if (!f.NodeAt.TryGetValue(inner, out int innerNode)) continue;
                    LayStruts(fi, innerNode, kv.Value, poly, sheet);
                }
            }
        }

        void LayStruts(int fiber, int innerNode, int outerNode, List<Vector3> poly, int outerSheet)
        {
            // Arc length along the traced polyline, inner crossing -> outer crossing.
            var cum = new float[poly.Count];
            for (int k = 1; k < poly.Count; k++) cum[k] = cum[k - 1] + Vector3.Distance(poly[k - 1], poly[k]);
            float total = cum[poly.Count - 1];
            float s0 = 0.5f * _nodes[innerNode].Size.Z + _s.Clearance;
            float s1 = total - 0.5f * _nodes[outerNode].Size.Z - _s.Clearance;
            float usable = s1 - s0;
            int prev = innerNode;
            if (usable >= 1f)
            {
                int m = Math.Max(1, (int)MathF.Round(usable / _fiberPrismSpacing));
                float slot = usable / m;
                for (int j = 0; j < m; j++)
                {
                    float at = s0 + (j + 0.5f) * slot;
                    var (p, tangent) = Along(poly, cum, at);
                    // The outer side of a NEGATIVE-t gap is the lower G, so the traced tangent runs down the
                    // gradient there. Every prism's Forward points up G, so the stack has one orientation.
                    if (Vector3.Dot(tangent, Grad(p / _scale)) < 0f) tangent = -tangent;
                    var up = ProjectOnPlane(_nodes[innerNode].U, tangent);
                    up = up.LengthSquared() > 1e-6f ? Vector3.Normalize(up) : AnyTangent(tangent);
                    var size = new Vector3(_s.FiberThickness, _s.FiberThickness, slot * _s.FiberFill);
                    int id = _nodes.Count;
                    _nodes.Add(new Node
                    {
                        P = p, F = tangent, U = up, Size = size, Size0 = size,
                        Kind = NestedGyroidPrismKind.Fiber,
                        Sheet = outerSheet, Rank = _rankOf[outerSheet], Fiber = fiber,
                        Gap = total, Alive = true,
                    });
                    _chain.Add((prev, id));
                    prev = id;
                }
            }
            _chain.Add((prev, outerNode));
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

        // ------------------------------------------------------------------ the fit (exact OBB SAT)

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
            return ObbOverlap(a.P, xa, a.U, a.F, a.Size * 0.5f + m, b.P, xb, b.U, b.F, b.Size * 0.5f + m);
        }

        static float Radius(Node n) => 0.5f * n.Size.Length();

        IEnumerator<int> OverlappingPairs(float margin, List<(int, int)> pairs)
        {
            pairs.Clear();
            float maxR = 0f;
            foreach (var nd in _nodes) if (nd.Alive) maxR = Math.Max(maxR, Radius(nd));
            var grid = new Grid(Math.Max(1f, 2f * maxR + 2f * margin));
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
                    if (Vector3.DistanceSquared(_nodes[i].P, _nodes[j].P) > reach * reach) continue;
                    if (Overlaps(i, j, margin)) pairs.Add((i, j));
                }
            }
        }

        bool CrossSheet(int i, int j) =>
            _nodes[i].Kind == NestedGyroidPrismKind.Sheet && _nodes[j].Kind == NestedGyroidPrismKind.Sheet &&
            _nodes[i].Sheet != _nodes[j].Sheet;

        IEnumerator<int> Fit()
        {
            // Fit with half the clearance as margin on each box, so what ships keeps a real gap.
            float margin = 0.5f * _s.Clearance;
            var pairs = new List<(int, int)>();
            var scan = OverlappingPairs(margin, pairs);
            while (scan.MoveNext()) yield return 0;
            _stats.OverlapsBeforeFit = pairs.Count;
            foreach (var (i, j) in pairs) if (CrossSheet(i, j)) _stats.CrossSheetOverlapsBeforeFit++;
            yield return 0;

            for (int pass = 0; pass < 10 && pairs.Count > 0; pass++)
            {
                var touched = new HashSet<int>();
                foreach (var (i, j) in pairs) { touched.Add(i); touched.Add(j); }
                foreach (int i in touched) Shrink(i, 0.88f);
                scan = OverlappingPairs(margin, pairs);
                while (scan.MoveNext()) yield return 0;
            }

            // Anything still touching at its floor: the outer (later-ring) prism gives way.
            foreach (var (i, j) in pairs)
            {
                if (!_nodes[i].Alive || !_nodes[j].Alive) continue;
                int drop = _nodes[j].Rank > _nodes[i].Rank || (_nodes[j].Rank == _nodes[i].Rank && j > i) ? j : i;
                // A fiber crossing is a stitch; never drop it in favour of a free plate.
                if (_nodes[drop].Fiber >= 0 && _nodes[drop].Kind == NestedGyroidPrismKind.Sheet)
                    drop = drop == i ? j : i;
                var nd = _nodes[drop];
                nd.Alive = false;
                _nodes[drop] = nd;
                _stats.DroppedByFit++;
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
                nd.Size.X = Math.Max(nd.Size0.X * floor, nd.Size.X * k);
                nd.Size.Y = Math.Max(nd.Size0.Y * floor, nd.Size.Y * k);
            }
            else nd.Size.Z = Math.Max(nd.Size0.Z * floor, nd.Size.Z * k);
            _nodes[i] = nd;
        }

        // ------------------------------------------------------------------ graph, prune, growth order

        /// <summary>An in-sheet link is drawn only inside this share of the smaller rider reach.</summary>
        public const float LinkReachFraction = 0.9f;

        float ReachOf(Node n)
        {
            float extent = Math.Max(n.Size.X, Math.Max(n.Size.Y, n.Size.Z));
            return Math.Max(1f, extent) * _s.RiderGroundSearchScale + _s.RiderHoverHeight;
        }

        IEnumerator<int> Assemble()
        {
            int count = _nodes.Count;
            var adj = new List<int>[count];
            var cross = new HashSet<long>();
            for (int i = 0; i < count; i++) adj[i] = new List<int>(8);

            // Cross-sheet links: along the fibers, over any strut the fit dropped.
            var chainNext = new Dictionary<int, int>();
            foreach (var (a, b) in _chain) chainNext[a] = b;
            foreach (var (a0, _) in _chain)
            {
                if (!_nodes[a0].Alive) continue;
                int b = chainNext[a0];
                while (!_nodes[b].Alive && chainNext.TryGetValue(b, out int nb)) b = nb;
                if (!_nodes[b].Alive || b == a0) continue;
                Link(adj, a0, b);
                cross.Add(PairKey(a0, b));
            }

            // In-sheet links: same sheet, within both endpoints' rider reach.
            float maxReach = 0f;
            foreach (var nd in _nodes) if (nd.Alive) maxReach = Math.Max(maxReach, ReachOf(nd));
            var grid = new Grid(maxReach);
            for (int i = 0; i < count; i++)
                if (_nodes[i].Alive && _nodes[i].Kind == NestedGyroidPrismKind.Sheet) grid.Add(_nodes[i].P, i);
            for (int i = 0; i < count; i++)
            {
                if ((i & 127) == 127) yield return 0;
                var ni = _nodes[i];
                if (!ni.Alive || ni.Kind != NestedGyroidPrismKind.Sheet) continue;
                grid.Near(ni.P, _near);
                foreach (int j in _near)
                {
                    if (j <= i) continue;
                    var nj = _nodes[j];
                    if (nj.Sheet != ni.Sheet) continue;
                    // Strictly INSIDE the rider's reach, with margin: a bond at exactly the search radius is
                    // one the ride kernel finds only when the vessel sits dead on the near prism.
                    float r = LinkReachFraction * Math.Min(ReachOf(ni), ReachOf(nj));
                    if (Vector3.DistanceSquared(ni.P, nj.P) <= r * r) Link(adj, i, j);
                }
            }

            // Islands: union-find, keep the component that holds the plate nearest the heart.
            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            for (int i = 0; i < count; i++) foreach (int j in adj[i]) parent[Find(i)] = Find(j);
            var roots = new HashSet<int>();
            for (int i = 0; i < count; i++) if (_nodes[i].Alive) roots.Add(Find(i));
            _stats.ComponentsBeforePrune = roots.Count;
            int start = NearestToOrigin();
            while (start >= 0 && !_nodes[start].Alive) start = -1;
            if (start < 0)
            {
                for (int i = 0; i < count; i++) if (_nodes[i].Alive) { start = i; break; }
            }
            int keep = start >= 0 ? Find(start) : -1;
            for (int i = 0; i < count; i++)
            {
                if (!_nodes[i].Alive || Find(i) == keep) continue;
                var nd = _nodes[i];
                nd.Alive = false;
                _nodes[i] = nd;
                _stats.PrunedIslandPrisms++;
            }

            // Growth order: ring by ring outward from the heart; within a ring, multi-source BFS from
            // everything already laid - so each new sheet spreads out of the fibers that reached it, and
            // every prism's parent is already standing.
            var orderOf = new int[count];
            for (int i = 0; i < count; i++) orderOf[i] = -1;
            var order = new List<int>(count);
            var growParent = new List<int>(count);
            if (start >= 0)
            {
                orderOf[start] = 0;
                order.Add(start);
                growParent.Add(-1);
            }
            for (int ring = 0; ring <= _maxRank; ring++)
            {
                var queue = new Queue<int>(order);
                yield return 0;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    foreach (int j in adj[i])
                    {
                        if (orderOf[j] >= 0 || !_nodes[j].Alive || _nodes[j].Rank > ring) continue;
                        orderOf[j] = order.Count;
                        order.Add(j);
                        growParent.Add(orderOf[i]);
                        queue.Enqueue(j);
                    }
                }
            }

            int n = order.Count;
            var L = new NestedGyroidLattice
            {
                Count = n,
                Position = new Vector3[n], Forward = new Vector3[n], Up = new Vector3[n], Size = new Vector3[n],
                Kind = new NestedGyroidPrismKind[n], Sheet = new int[n], Rank = new int[n], Stack = new int[n], Parent = new int[n],
                LimbBond = new bool[n], Reach = new float[n],
                SheetLevels = (float[])_levels.Clone(), SheetCount = _s.SheetCount,
                Stats = _stats,
            };
            for (int k = 0; k < n; k++)
            {
                var nd = _nodes[order[k]];
                L.Position[k] = nd.P;
                L.Forward[k] = nd.F;
                L.Up[k] = nd.U;
                L.Size[k] = nd.Size;
                L.Kind[k] = nd.Kind;
                L.Sheet[k] = nd.Sheet;
                L.Rank[k] = nd.Rank;
                L.Stack[k] = nd.Kind == NestedGyroidPrismKind.Sheet ? 2 * nd.Sheet : nd.Sheet + StrutInnerSheet(nd.Sheet);
                L.Parent[k] = growParent[k];
                L.Reach[k] = ReachOf(nd);
                L.LimbBond[k] = growParent[k] < 0 || cross.Contains(PairKey(order[k], order[growParent[k]]));
            }

            var start2 = new int[n + 1];
            var flat = new List<int>(n * 6);
            for (int k = 0; k < n; k++)
            {
                start2[k] = flat.Count;
                foreach (int j in adj[order[k]])
                    if (orderOf[j] >= 0) flat.Add(orderOf[j]);
            }
            start2[n] = flat.Count;
            L.AdjStart = start2;
            L.Adj = flat.ToArray();
            _built = L;
        }

        static void Link(List<int>[] adj, int a, int b)
        {
            if (adj[a].Contains(b)) return;
            adj[a].Add(b);
            adj[b].Add(a);
        }

        static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // ------------------------------------------------------------------ budget + stats

        static void Truncate(NestedGyroidLattice L, int keep)
        {
            L.Count = keep;
            var start = new int[keep + 1];
            var flat = new List<int>();
            for (int k = 0; k < keep; k++)
            {
                start[k] = flat.Count;
                for (int e = L.AdjStart[k]; e < L.AdjStart[k + 1]; e++)
                    if (L.Adj[e] < keep) flat.Add(L.Adj[e]);
            }
            start[keep] = flat.Count;
            L.AdjStart = start;
            L.Adj = flat.ToArray();
        }

        /// <summary>Recomputes the counts, gap statistics and component count over the shipped prefix.</summary>
        public static void Recount(NestedGyroidLattice L)
        {
            var s = L.Stats;
            int n = L.Count;
            s.SheetPrisms = s.FiberPrisms = s.InSheetEdges = s.CrossSheetEdges = 0;
            s.ThicknessMin = float.MaxValue;
            s.ThicknessMax = 0f;
            s.ReachMin = float.MaxValue;
            var gaps = new List<float>(n);
            var edges = new List<float>(n * 3);
            float worst = 0f;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }

            for (int i = 0; i < n; i++)
            {
                if (L.Kind[i] == NestedGyroidPrismKind.Sheet)
                {
                    s.SheetPrisms++;
                    s.ThicknessMin = Math.Min(s.ThicknessMin, L.Size[i].Z);
                    s.ThicknessMax = Math.Max(s.ThicknessMax, L.Size[i].Z);
                }
                else s.FiberPrisms++;
                s.ReachMin = Math.Min(s.ReachMin, L.Reach[i]);

                float nearest = float.MaxValue;
                for (int e = L.AdjStart[i]; e < L.AdjStart[i + 1]; e++)
                {
                    int j = L.Adj[e];
                    float d = Vector3.Distance(L.Position[i], L.Position[j]);
                    nearest = Math.Min(nearest, d);
                    parent[Find(i)] = Find(j);
                    if (j <= i) continue;
                    edges.Add(d);
                    worst = Math.Max(worst, d / Math.Min(L.Reach[i], L.Reach[j]));
                    bool inSheet = L.Kind[i] == NestedGyroidPrismKind.Sheet && L.Kind[j] == NestedGyroidPrismKind.Sheet &&
                                   L.Sheet[i] == L.Sheet[j];
                    if (inSheet) s.InSheetEdges++; else s.CrossSheetEdges++;
                }
                gaps.Add(nearest);
            }
            var roots = new HashSet<int>();
            for (int i = 0; i < n; i++) roots.Add(Find(i));
            s.Components = roots.Count;
            s.MaxEdgeOverReach = worst;

            gaps.Sort();
            edges.Sort();
            s.GapMin = gaps.Count > 0 ? gaps[0] : 0f;
            s.GapMedian = gaps.Count > 0 ? gaps[gaps.Count / 2] : 0f;
            s.GapMax = gaps.Count > 0 ? gaps[gaps.Count - 1] : 0f;
            s.EdgeMin = edges.Count > 0 ? edges[0] : 0f;
            s.EdgeMedian = edges.Count > 0 ? edges[edges.Count / 2] : 0f;
            s.EdgeMax = edges.Count > 0 ? edges[edges.Count - 1] : 0f;
            if (s.ThicknessMin == float.MaxValue) s.ThicknessMin = 0f;
            if (s.ReachMin == float.MaxValue) s.ReachMin = 0f;
        }

        /// <summary>One line for the console - the counts the acceptance asks for.</summary>
        public static string Describe(NestedGyroidLattice L)
        {
            var s = L.Stats;
            return $"{L.Count} prisms ({s.SheetPrisms} sheet / {s.FiberPrisms} fiber, {s.Fibers} fibers, " +
                   $"{s.SheetsGrown} sheets), {s.Components} component(s) [{s.PrunedIslandPrisms} island prisms pruned], " +
                   $"neighbour gap min/median/max {s.GapMin:F2}/{s.GapMedian:F2}/{s.GapMax:F2} " +
                   $"(worst bond {s.MaxEdgeOverReach:P0} of reach), overlaps {s.RemainingOverlaps} " +
                   $"({s.RemainingCrossSheetOverlaps} cross-sheet), fit dropped {s.DroppedByFit}, " +
                   $"spacing {s.EffectiveSheetSpacing:F1} after {s.CoarsenPasses} coarsen pass(es), " +
                   $"budget cut {s.TruncatedByBudget}, comb {s.CombBeforeDegrees:F1}° -> {s.CombAfterDegrees:F1}°, " +
                   $"build {s.BuildMilliseconds:F0} ms";
        }
    }
}
