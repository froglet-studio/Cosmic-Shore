// THE NESTED GYROID - the growth rule of NestedGyroidFlora, as a pure function of its settings.
// Plain C# with System.Numerics and NO UnityEngine, so this exact file compiles and RUNS headless in
// Tools/Build/nested_gyroid_harness (the acceptance gates: budget, one component, gap < reach, zero
// cross-sheet overlaps, the template's loops on every sheet, slice timing). Docs/ECOSYSTEM.md §58.
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
        public int CellsPerSide = 1;
        /// <summary>Share of the CellsPerSide cube that is kept (1 = all). The PREVIEW uses it: a smaller window of
        /// the same tiling, so an icon shows the template's own subdivisions rather than a coarsened copy.</summary>
        public float ClipFraction = 1f;
        public int SheetCount = 7;
        public float TMax = 1.2f;
        public int MaxSheets = 7;
        /// <summary>The element's leaf on the t = 0 sheet, in world units (x along the template's own long axis,
        /// z the sheet normal) - the gyroid flora's per-element prism, scaled with its lattice.</summary>
        public Vector3 Leaf = new Vector3(18f, 6.8f, 3f);
        public float FiberSeedSpacing = 60f;
        public float FiberPrismSpacing = 8f;
        public float PlywoodTwistDegrees = 0f;
        public int PrismBudget = 4600;

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
            s.CellsPerSide = Math.Max(1, Math.Min(4, s.CellsPerSide));
            s.ClipFraction = Math.Max(0.1f, Math.Min(1f, s.ClipFraction));
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

        /// <summary>
        /// The same stack in a smaller window - for an ICON, which asks for a few hundred prisms and must show the
        /// whole stack (every sheet, the struts between them) at the template's own subdivision, never a coarsened
        /// copy. The window is chosen from a MEASURED count model rather than a pure cube root, because a transported
        /// image may leave the window by the stack's depth (<c>TransportMargin</c>): count ≈ full × ((f + 0.18) / 1.18)³
        /// (fits f = 0.15-0.4 within ~25%, Tools/Build/nested_gyroid_harness). Aimed at 90% of the request so the
        /// budget cut, if any, is a few outer prisms.
        /// </summary>
        public NestedGyroidSettings PreviewOf(int prisms)
        {
            var s = Sanitized();
            int target = Math.Max(16, prisms);
            // At most THREE sheets - the heart's and one either side: enough to read as a stack, and it leaves an icon's
            // budget a window wide enough to hold whole octagon rings (24 a period; seven sheets in 220 prisms is a
            // window of 15 sites a sheet, which holds none).
            s.MaxSheets = Math.Min(s.MaxSheets, 3);
            float full = 1.05f * NestedGyroidTemplate.SiteCount * s.MaxSheets * s.CellsPerSide * s.CellsPerSide * s.CellsPerSide;
            float f = 1.18f * MathF.Pow(0.9f * target / full, 1f / 3f) - 0.18f;
            s.ClipFraction = Math.Max(0.15f, Math.Min(1f, f));
            // Fibers thin more slowly than the window: they are what joins it.
            s.FiberSeedSpacing *= MathF.Sqrt(s.ClipFraction);
            s.PrismBudget = target;
            return s;
        }

        /// <summary>Identity of the lattice these settings grow - the build cache key.</summary>
        public string Key() => string.Join("|", new object[]
        {
            CellSize, CellsPerSide, ClipFraction, SheetCount, TMax, MaxSheets, Leaf.X, Leaf.Y, Leaf.Z, FiberSeedSpacing,
            FiberPrismSpacing, PlywoodTwistDegrees, PrismBudget, PlateThicknessOfGap, FiberThickness, FiberFill,
            Clearance, HeartClearance, RiderGroundSearchScale, RiderHoverHeight, Seed,
        });
    }

    /// <summary>The measurements a build reports - what the flora logs and the harness asserts.</summary>
    public sealed class NestedGyroidStats
    {
        public int SheetPrisms, FiberPrisms, DangerPrisms, Fibers;
        public int SheetsGrown, TemplateSites;
        /// <summary>The world period the stack was built at (for readers that need the field's scale).</summary>
        public float CellSize;
        public int ComponentsBeforePrune, PrunedIslandPrisms, Components;
        public int OverlapsBeforeFit, CrossSheetOverlapsBeforeFit, DroppedByFit, DroppedStruts;
        public int RemainingOverlaps, RemainingCrossSheetOverlaps;
        public int TruncatedByBudget;
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
        /// <summary>How far the template stretches in-plane as it is carried out along ∇G (min / max over prisms).</summary>
        public float StretchMin, StretchMax;
        /// <summary>Worst |G - t| of a laid plate before its Newton snap, in field units: how far off its level the
        /// transported template lands (0 = exactly on the sheet).</summary>
        public float WorstLevelError;
        public double BuildMilliseconds;
    }

    /// <summary>
    /// The finished stack. Every array is indexed by GROWTH ORDER: prism <c>i</c>'s parent is always
    /// <c>&lt; i</c> (or -1 = the heart), so any prefix is one connected object hanging off the crystal.
    /// Positions are in the plant's local space, heart at the origin (G(0) = 0: the crystal sits on the
    /// t = 0 sheet). A prism's local +z (Forward) is its RIDE NORMAL, pointing UP G - the sheet normal for a plate,
    /// the fiber tangent for a strut - which is what the Urchin's ride kernel reads; +y (Up) is the template's own.
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
        /// <summary>The template site a plate is the image of (-1 for a strut): the same index on every sheet.</summary>
        public int[] Site;
        /// <summary>True for a plate of one of the template's four danger block types - the gyroid flora's
        /// octagon rings, carried onto every sheet.</summary>
        public bool[] DangerRing;
        /// <summary>True where the zero-overlap fit had to shrink this prism below its rule size.</summary>
        public bool[] Fitted;
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
    /// <para>The rule, in order. The BASE SHEET is the gyroid flora's own tiling (<see cref="NestedGyroidTemplate"/>:
    /// 576 sites per period, its block types, its frames), tiled over the bounding cube and Newton-snapped onto G = 0.
    /// Every site is carried along ∇G/|∇G| (RK4) to every nested level G = t_i, so each sheet is the SAME tiling - the
    /// same loop subdivisions, the same octagon rings - and the gradient line through a site is literally the fiber
    /// that site lies on. A Poisson subset of those lines carries struts. The frames ride along (projected onto each
    /// sheet; the helicoidal-plywood turn per sheet defaults to 0, because turning crosses plates over their loops), every plate takes the element's leaf scaled by how far the
    /// template has stretched there and thickened with the local gap Δt/|∇G|, and an exact OBB fit guarantees no two
    /// prisms interpenetrate. Then the rider graph (in-sheet links within reach, cross-sheet links along fibers),
    /// islands pruned, growth ordered outward from the heart, ring by ring.</para>
    /// </summary>
    public sealed class NestedGyroidBuilder
    {
        const float TwoPi = (float)(2.0 * Math.PI);

        readonly NestedGyroidSettings _s;
        readonly IEnumerator<int> _work;
        readonly Stopwatch _total = new Stopwatch();

        public NestedGyroidLattice Result { get; private set; }
        /// <summary>The stage the build is in - diagnostics (which stage a long slice belonged to).</summary>
        public string Stage { get; private set; } = "start";
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
            public int Sheet, Rank, Fiber, Site;
            public bool Danger;
            public float Gap, Stretch;
            public bool Alive;
        }

        /// <summary>One template site's gradient line: its image on every sheet it reaches, and the polyline between
        /// consecutive images (from the INNER one). Every site has one; only a Poisson subset carries struts.</summary>
        sealed class FiberPath
        {
            public Vector3 SeedWorld;
            public readonly Dictionary<int, Vector3> Crossing = new Dictionary<int, Vector3>();
            public readonly Dictionary<int, List<Vector3>> Segment = new Dictionary<int, List<Vector3>>();
            public readonly Dictionary<int, int> NodeAt = new Dictionary<int, int>();
            public bool Strutted;
        }

        float _scale;          // world units per field unit
        float _half;           // field half-extent of the bounding cube
        float[] _levels;
        int[] _rankOf;
        int _maxRank;
        float _dt;
        List<Node> _nodes;
        List<FiberPath> _fibers;
        /// <summary>One sequence per strutted gap: inner plate, its struts in order, outer plate. A SEQUENCE and
        /// not an inner->next map: a t = 0 plate starts two chains (up the stack and down it), and a map keyed on
        /// the inner node kept only whichever was laid last - half the stack lost its stitch.</summary>
        List<List<int>> _chains;
        NestedGyroidStats _stats;
        ulong _rng;
        NestedGyroidLattice _built;

        int Yields;

        IEnumerator<int> Run()
        {
            var once = BuildOnce();
            while (once.MoveNext()) yield return 0;
            var lattice = _built;
            Stage = "stats";
            yield return 0;
            if (lattice.Count > _s.PrismBudget)
            {
                // The tiling is the template's and is not ours to coarsen, so an over-budget stack is CUT: the
                // growth-order prefix is connected by construction, so a cut is a plant that stopped growing early
                // (its outermost ring incomplete) - never an island.
                lattice.Stats.TruncatedByBudget = lattice.Count - _s.PrismBudget;
                Truncate(lattice, _s.PrismBudget);
            }
            var recount = RecountSteps(lattice);
            while (recount.MoveNext()) yield return 0;
            Result = lattice;
        }

        IEnumerator<int> BuildOnce()
        {
            _stats = new NestedGyroidStats { CellSize = _s.CellSize };
            _nodes = new List<Node>(8192);
            _fibers = new List<FiberPath>();
            _chains = new List<List<int>>();
            _rng = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)_s.Seed;

            _scale = _s.CellSize / TwoPi;
            _half = (float)Math.PI * _s.CellsPerSide * _s.ClipFraction;

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

            Stage = "template";
            // ---- the base sheet: the gyroid flora's tiling, tiled over the cube and snapped onto G = 0
            var basePos = new List<Vector3>();
            var baseFwd = new List<Vector3>();
            var baseUp = new List<Vector3>();
            var baseType = new List<byte>();
            int span = (int)MathF.Ceiling(_s.CellsPerSide * 0.5f) + 1;
            for (int cx = -span; cx < span; cx++)
            for (int cy = -span; cy < span; cy++)
            for (int cz = -span; cz < span; cz++)
            {
                for (int k = 0; k < NestedGyroidTemplate.SiteCount; k++)
                {
                    var q = (NestedGyroidTemplate.Position[k] + new Vector3(cx, cy, cz)) * TwoPi;
                    if (!InBox(q)) continue;
                    q = Newton(Newton(q, 0f), 0f);
                    if (!InBox(q)) continue;
                    var g = Grad(q);
                    float gl = g.Length();
                    if (gl < 1e-4f) continue;
                    var normal = g / gl;
                    // The flora built its normals with either sign; here every +z points UP G.
                    var up = ProjectOnPlane(NestedGyroidTemplate.Up[k], normal);
                    up = up.LengthSquared() > 1e-6f ? Vector3.Normalize(up) : AnyTangent(normal);
                    basePos.Add(q * _scale);
                    baseFwd.Add(normal);
                    baseUp.Add(up);
                    baseType.Add(NestedGyroidTemplate.BlockType[k]);
                    if ((k & 127) == 127) yield return 0;
                }
            }
            _stats.TemplateSites = basePos.Count;

            // The template's own bonds (its four mates), for the in-plane stretch the flow imposes per sheet.
            var mates = TemplateMates(basePos);
            yield return 0;

            Stage = "transport";
            // ---- every site's gradient line, out to every grown level on both sides
            int centre = Array.IndexOf(_levels, 0f);
            bool centreGrown = centre >= 0 && _rankOf[centre] <= _maxRank;
            for (int k = 0; k < basePos.Count; k++)
            {
                var fiber = new FiberPath { SeedWorld = basePos[k] };
                if (centreGrown)
                {
                    fiber.Crossing[centre] = basePos[k];
                    fiber.Segment[centre] = new List<Vector3> { basePos[k] };
                }
                Trace(fiber, +1f);
                Trace(fiber, -1f);
                _fibers.Add(fiber);
                if ((++Yields & 15) == 0) yield return 0;
            }

            Stage = "fiber seeds";
            // ---- struts ride a Poisson subset of the lines (all lines are fibers; these are the visible ones)
            var seeds = new List<Vector3>();
            var thin = Poisson(basePos, _s.FiberSeedSpacing, null, seeds);
            while (thin.MoveNext()) yield return 0;
            var seedSet = new HashSet<Vector3>(seeds);
            for (int k = 0; k < _fibers.Count; k++) _fibers[k].Strutted = seedSet.Contains(basePos[k]);

            Stage = "plates";
            // ---- plates: each site's image on each sheet, the template frame carried along and twisted per sheet
            float twist = _s.PlywoodTwistDegrees * (float)Math.PI / 180f;
            float clear = _s.HeartClearance;
            for (int k = 0; k < _fibers.Count; k++)
            {
                var f = _fibers[k];
                foreach (var kv in f.Crossing)
                {
                    int sheet = kv.Key;
                    var p = kv.Value;
                    if (p.Length() < clear) continue;          // the crystal's seat
                    var g = Grad(p / _scale);
                    float gl = g.Length();
                    if (gl < 1e-4f) continue;
                    var normal = g / gl;
                    var up = ProjectOnPlane(baseUp[k], normal);
                    up = up.LengthSquared() > 1e-6f ? Vector3.Normalize(up) : AnyTangent(normal);
                    up = Vector3.Normalize(Rotate(up, normal, twist * (sheet - c)));
                    _stats.WorstLevelError = Math.Max(_stats.WorstLevelError, MathF.Abs(G(p / _scale) - _levels[sheet]));
                    f.NodeAt[sheet] = _nodes.Count;
                    _nodes.Add(new Node
                    {
                        P = p, F = normal, U = up,
                        Kind = NestedGyroidPrismKind.Sheet,
                        Sheet = sheet, Rank = _rankOf[sheet], Fiber = k, Site = k,
                        Danger = NestedGyroidTemplate.IsDangerType(baseType[k]),
                        Gap = GapWorld(p), Alive = true,
                    });
                }
                if ((++Yields & 31) == 0) yield return 0;
            }

            Stage = "sizing";
            // ---- sizes (leaf x stretch, thickness from the gap), then the struts between the plates they join
            SizeSheets(basePos, mates);
            LayFibers();
            yield return 0;

            Stage = "fit";
            // ---- fit: no two prisms interpenetrate
            var fit = Fit();
            while (fit.MoveNext()) yield return 0;

            Stage = "graph";
            // ---- rider graph, islands, growth order
            var assemble = Assemble();
            while (assemble.MoveNext()) yield return 0;
        }

        /// <summary>Each base site's template mates: its nearest sites within 1.4× the template's own bond length,
        /// at most four (the bond table's four corner sites).</summary>
        List<int>[] TemplateMates(List<Vector3> basePos)
        {
            // The gyroid flora's bond is ~8.0 units at its native period 120; scale it to this lattice.
            float bond = 8.0f * _s.CellSize / NestedGyroidTemplate.Period;
            var grid = new Grid(1.4f * bond);
            for (int i = 0; i < basePos.Count; i++) grid.Add(basePos[i], i);
            var mates = new List<int>[basePos.Count];
            var cand = new List<(float d, int j)>(16);
            for (int i = 0; i < basePos.Count; i++)
            {
                grid.Near(basePos[i], _near);
                cand.Clear();
                foreach (int j in _near)
                {
                    if (j == i) continue;
                    float d = Vector3.Distance(basePos[i], basePos[j]);
                    if (d < 1.4f * bond) cand.Add((d, j));
                }
                cand.Sort((a, b) => a.d.CompareTo(b.d));
                var l = new List<int>(4);
                for (int m = 0; m < cand.Count && m < 4; m++) l.Add(cand[m].j);
                mates[i] = l;
            }
            return mates;
        }

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

        bool InBox(Vector3 q, float margin = 0f) =>
            MathF.Abs(q.X) <= _half + margin && MathF.Abs(q.Y) <= _half + margin && MathF.Abs(q.Z) <= _half + margin;

        /// <summary>How far (field units) a transported image may leave the base cube: as far again as the cube's own
        /// half-size. The stack's depth is tMax over the smallest |∇G| on its outermost level, which is ~1.7 at
        /// tMax 1.2 and grows without bound toward √2 (|∇G| -> 0); an image stops at its own level anyway, so the
        /// margin only has to be large enough never to be the thing that ends a line.</summary>
        float TransportMargin => _half;

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

        int NearestToOrigin()
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].Rank != 0 || _nodes[i].Kind != NestedGyroidPrismKind.Sheet) continue;
                float d = _nodes[i].P.LengthSquared();
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        float GapWorld(Vector3 world)
        {
            float gl = Grad(world / _scale).Length();
            return _scale * _dt / Math.Max(gl, 1e-3f);
        }

        // ------------------------------------------------------------------ sizing + fiber struts

        void SizeSheets(List<Vector3> basePos, List<int>[] mates)
        {
            // The reference: the t = 0 layer's median gap. A plate there wears the element's leaf exactly; out along
            // the flow it stretches with the template and thickens with the layer.
            var baseGaps = new List<float>();
            foreach (var p in basePos) baseGaps.Add(GapWorld(p));
            baseGaps.Sort();
            float gap0 = baseGaps.Count > 0 ? baseGaps[baseGaps.Count / 2] : 1f;

            var gaps = new List<float>();
            float smin = float.MaxValue, smax = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var nd = _nodes[i];
                if (nd.Kind != NestedGyroidPrismKind.Sheet) continue;
                gaps.Add(nd.Gap);

                // In-plane stretch: this image's distance to its mates' images on the same sheet, over the base's.
                double sum = 0, sum0 = 0;
                foreach (int m in mates[nd.Site])
                {
                    if (!_fibers[m].Crossing.TryGetValue(nd.Sheet, out var pm)) continue;
                    sum += Vector3.Distance(nd.P, pm);
                    sum0 += Vector3.Distance(basePos[nd.Site], basePos[m]);
                }
                float stretch = sum0 > 1e-6 ? (float)(sum / sum0) : 1f;
                stretch = Math.Max(0.5f, Math.Min(2f, stretch));
                smin = Math.Min(smin, stretch);
                smax = Math.Max(smax, stretch);

                // Thickness follows the layer, but a plate stays a PLATE: at most twice the element's own thickness
                // (deep layers near tMax -> √2 are ten times the base gap, and a plate that followed them became a
                // block that swallowed its neighbours' struts), and never more than its share of the gap.
                float thick = _s.Leaf.Z * (nd.Gap / gap0);
                thick = Math.Max(0.3f, Math.Min(Math.Min(_s.PlateThicknessOfGap * nd.Gap, 2f * _s.Leaf.Z), thick));
                nd.Stretch = stretch;
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

        void LayFibers()
        {
            for (int fi = 0; fi < _fibers.Count; fi++)
            {
                var f = _fibers[fi];
                if (!f.Strutted) continue;
                _stats.Fibers++;
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
            for (int step = 0; step < 2000 && next < targets.Count; step++)
            {
                Vector3 q1 = Rk4(q, dir, ds);
                // The STACK is the base cube's tiling carried outward, so an image may leave the cube by the depth
                // of the stack: the sheets' edges then lie over the base sheet's edge instead of each outer sheet
                // losing the third of the template that flows out of the face (measured: 426 -> 576 of 576 sites).
                if (!InBox(q1, TransportMargin)) return;
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

        void LayStruts(int fiber, int innerNode, int outerNode, List<Vector3> poly, int outerSheet)
        {
            // Arc length along the traced polyline, inner crossing -> outer crossing.
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
                    chain.Add(id);
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

            for (int pass = 0; pass < 18 && pairs.Count > 0; pass++)
            {
                var touched = new HashSet<int>();
                foreach (var (i, j) in pairs) { touched.Add(i); touched.Add(j); }
                // STRUTS NEVER GIVE WAY - they are the stack's stitches. A shrunk strut has a shorter rider reach than
                // the bond it spans, and a dropped one leaves the chain bridging two slots (measured: up to 123% of
                // reach near tMax 1.38). The plates beside it shrink instead; a strut-strut touch (two fibers crossing
                // in a converging layer) is resolved below by dropping the outer one.
                foreach (int i in touched)
                    if (_nodes[i].Kind == NestedGyroidPrismKind.Sheet) Shrink(i, 0.88f);
                scan = OverlappingPairs(margin, pairs);
                while (scan.MoveNext()) yield return 0;
            }

            // Anything still touching at its floor: the outer (later-ring) prism gives way.
            foreach (var (i, j) in pairs)
            {
                if (!_nodes[i].Alive || !_nodes[j].Alive) continue;
                int drop = _nodes[j].Rank > _nodes[i].Rank || (_nodes[j].Rank == _nodes[i].Rank && j > i) ? j : i;
                // A plate touching a strut is the one that goes.
                if (_nodes[i].Kind != _nodes[j].Kind)
                    drop = _nodes[i].Kind == NestedGyroidPrismKind.Sheet ? i : j;
                // A fiber crossing is a stitch; never drop it in favour of a free plate.
                else if (_nodes[drop].Kind == NestedGyroidPrismKind.Sheet && _fibers[_nodes[drop].Fiber].Strutted)
                    drop = drop == i ? j : i;
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
                // is touching THROUGH its thickness (two template neighbours meeting at an angle, on a deep layer),
                // so the thickness gives way next.
                bool footprintAtFloor = nd.Size.X <= nd.Size0.X * floor * 1.0001f && nd.Size.Y <= nd.Size0.Y * floor * 1.0001f;
                nd.Size.X = Math.Max(nd.Size0.X * floor, nd.Size.X * k);
                nd.Size.Y = Math.Max(nd.Size0.Y * floor, nd.Size.Y * k);
                if (footprintAtFloor) nd.Size.Z = Math.Max(nd.Size0.Z * floor, nd.Size.Z * k);
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

            // Cross-sheet links: along the fibers, bridging any strut the fit dropped.
            foreach (var chain in _chains)
            {
                int prev = -1;
                foreach (int node in chain)
                {
                    if (!_nodes[node].Alive) continue;
                    if (prev >= 0)
                    {
                        Link(adj, prev, node);
                        cross.Add(PairKey(prev, node));
                    }
                    prev = node;
                }
            }

            // In-sheet links: same sheet, within both endpoints' rider reach.
            float maxReach = 0f;
            foreach (var nd in _nodes) if (nd.Alive) maxReach = Math.Max(maxReach, ReachOf(nd));
            // One grid per SHEET: an in-sheet link never looks at another layer, and at the default size every layer
            // lies inside one reach-sized cell of every other, so a shared grid scanned the whole stack per plate.
            var grids = new Dictionary<int, Grid>();
            for (int i = 0; i < count; i++)
            {
                if (!_nodes[i].Alive || _nodes[i].Kind != NestedGyroidPrismKind.Sheet) continue;
                if (!grids.TryGetValue(_nodes[i].Sheet, out var g)) grids[_nodes[i].Sheet] = g = new Grid(maxReach);
                g.Add(_nodes[i].P, i);
            }
            for (int i = 0; i < count; i++)
            {
                if ((i & 31) == 31) yield return 0;
                var ni = _nodes[i];
                if (!ni.Alive || ni.Kind != NestedGyroidPrismKind.Sheet) continue;
                grids[ni.Sheet].Near(ni.P, _near);
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
            for (int i = 0; i < count; i++)
            {
                foreach (int j in adj[i]) parent[Find(i)] = Find(j);
                if ((i & 63) == 63) yield return 0;
            }
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
                int popped = 0;
                while (queue.Count > 0)
                {
                    if ((++popped & 255) == 255) yield return 0;
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
                Site = new int[n], DangerRing = new bool[n], Fitted = new bool[n],
                LimbBond = new bool[n], Reach = new float[n],
                SheetLevels = (float[])_levels.Clone(), SheetCount = _s.SheetCount,
                Stats = _stats,
            };
            yield return 0;
            for (int k = 0; k < n; k++)
            {
                if ((k & 511) == 511) yield return 0;
                var nd = _nodes[order[k]];
                L.Position[k] = nd.P;
                L.Forward[k] = nd.F;
                L.Up[k] = nd.U;
                L.Size[k] = nd.Size;
                L.Kind[k] = nd.Kind;
                L.Sheet[k] = nd.Sheet;
                L.Rank[k] = nd.Rank;
                L.Site[k] = nd.Kind == NestedGyroidPrismKind.Sheet ? nd.Site : -1;
                L.DangerRing[k] = nd.Kind == NestedGyroidPrismKind.Sheet && nd.Danger;
                L.Fitted[k] = nd.Size != nd.Size0;
                L.Stack[k] = nd.Kind == NestedGyroidPrismKind.Sheet ? 2 * nd.Sheet : nd.Sheet + StrutInnerSheet(nd.Sheet);
                L.Parent[k] = growParent[k];
                L.Reach[k] = ReachOf(nd);
                L.LimbBond[k] = growParent[k] < 0 || cross.Contains(PairKey(order[k], order[growParent[k]]));
            }

            var start2 = new int[n + 1];
            var flat = new List<int>(n * 6);
            yield return 0;
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
            var steps = RecountSteps(L);
            while (steps.MoveNext()) { }
        }

        static IEnumerator<int> RecountSteps(NestedGyroidLattice L)
        {
            var s = L.Stats;
            int n = L.Count;
            s.SheetPrisms = s.FiberPrisms = s.DangerPrisms = s.InSheetEdges = s.CrossSheetEdges = 0;
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
                if ((i & 63) == 63) yield return 0;
                if (L.Kind[i] == NestedGyroidPrismKind.Sheet)
                {
                    s.SheetPrisms++;
                    if (L.DangerRing[i]) s.DangerPrisms++;
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

            yield return 0;
            gaps.Sort();
            yield return 0;
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
                   $"{s.DangerPrisms} danger-ring plates, template {s.TemplateSites} sites/sheet, stretch {s.StretchMin:F2}..{s.StretchMax:F2}, " +
                   $"budget cut {s.TruncatedByBudget}, " +
                   $"build {s.BuildMilliseconds:F0} ms";
        }
    }
}

