using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Cage" - <see cref="GameModes.Cleave"/>'s INTENSITY-3 arena, and the one rung of that
    /// mode's ladder that predates it: Cleave was called "Peel the Cage" when all four of its
    /// intensities were nested shells. A LAYERED ORANGE: one or more concentric hollow
    /// shells of prism bone, added INWARD from a fixed outer radius. Each shell is meridian ribs
    /// running pole to pole, latitude hoops binding them, a diagonal through every rib x hoop cell
    /// that splits it into TRIANGLES, chunky joints at each crossing, and a crown closing each pole.
    ///
    /// Four properties of this structure are load-bearing for the mode, not decoration:
    ///
    ///   • <see cref="shellCount"/> is the INTENSITY dial. The Cell picks one
    ///     <c>CellConfigDataSO</c> per intensity (<c>CellTypeChoiceOptions.IntensityWise</c>),
    ///     and each config points at a prefab variant of this component with a different shell
    ///     count - two rinds to peel at intensity 1, five at intensity 4. Shells are added
    ///     INWARD (<see cref="CageR"/> never moves) so the AI's aim point, the player spawn
    ///     ring and the arena's outer silhouette are identical at every intensity.
    ///   • The OUTER shell's weave is open - a ~188u x ~196u cell. This is a ribcage, not a prison
    ///     wall: you fly between the bones freely, and the gaps are what let you see the next rind
    ///     waiting behind this one.
    ///   • Each shell inward is <see cref="DensityStep"/> times denser than the one outside it, ON
    ///     TOP of the tightening that shrinking radius already gives - so the pith at the core is
    ///     a far harder skin than the rind at the surface (~188u cells outside, ~44u at the core
    ///     - a 4.3x tightening). Successive shells are also rotated by a fraction of a rib spacing
    ///     (<see cref="ShellLonOffsets"/>) so the gaps never line up radially - there is no free
    ///     corridor straight through to the core.
    ///   • Every shell inside the outermost is TILTED onto its own axis
    ///     (<see cref="ShellTilts"/>). A latitude-hoop sphere is inherently densest at its POLES,
    ///     where the ribs converge; if every rind shared world-up, that dense cap would stack
    ///     radially and the whole match would collapse into "everyone drills the top". Tilting each
    ///     inner rind puts its hard cap somewhere else, so no single approach is cheap all the way
    ///     down and the shape reads as a real orange rather than a stack of identical grids.
    ///   • Every bar is <see cref="PrismKind.Plain"/> except the sparse <see cref="PrismKind.Danger"/>
    ///     traps, so a bar is a ONE-hit prism. Nothing here is <see cref="PrismKind.Shielded"/> or
    ///     <see cref="PrismKind.SuperShielded"/>: a super-shielded prism is fully invulnerable
    ///     (<c>Prism.Damage</c> returns early), so one in the cage would be permanently unbreakable
    ///     mass and enough of them could put the destruction target out of reach. Do not "upgrade"
    ///     the bars.
    ///
    /// Painted across the full domain triad so the cage reads as contested neutral bone rather
    /// than any one team's property; the paint is cosmetic to scoring (StatsManager classifies
    /// every non-roster-owned prism hostile, so cage mass scores for whoever breaks it, in any
    /// colour). Deterministic per seed like every cell environment - clients build locally with
    /// no seed sync.
    ///
    /// Budget: this class now serves exactly ONE rung - Cleave's intensity 3, The Cage, three
    /// rinds at 14,731 prisms / 14,208,571 volume (flush since 2026-10-20; it was 23,357,561 while
    /// its bars passed through each other). The old 2..5-shell ladder (10,620 / 14,731 /
    /// 17,992 / 20,153) is retired: intensity stopped meaning "how many shells" and started
    /// meaning WHICH PLACE, so the other three rungs are unrelated arenas. The numbers are
    /// MEASURED by running this file - Tools/Build/cleave_arena_harness compiles it against a
    /// Unity shim and counts what it emits - and gated by Tools/Build/cleave_budget.py.
    /// Tools/Build/ribcage_budget.py, the analytic model this doc used to cite, is deleted: its
    /// jitter factor was wrong by exactly 2x and every volume threshold it produced described a
    /// cell twice as heavy as the one that exists (CLEAVE.md, "The predecessor model was wrong").
    /// See CLEAVE.md for the ladder and the collider-budget statement.
    /// </summary>
    public class SpawnableRibcage : CellEnvironmentSpawnableBase
    {
        // Outermost shell. Density is where the prism budget goes, not radius: a bigger sphere
        // would just move the arena out. The OUTER weave is deliberately open - see the summary.
        //
        // The radius itself belongs to SliceArenaGeometry, not to this file: all four Cleave
        // arenas are built to one envelope, and the AI's stations and the player spawn ring are
        // both derived from it. A local copy of the number is how one of them drifts.
        /// <summary>This arena IS intensity 3, so it carries that rung's scale as a constant.</summary>
        const float CageR = SliceArenaGeometry.OuterRadiusI3;

        /// <summary>Authored-units -> world-units; see <c>SliceArenaGeometry</c>. Every LENGTH here
        /// is multiplied by it. Counts (ribs, hoops, crowns, the danger stride) and ANGLES
        /// (latitudes, tilts) are deliberately bare: a shell's bar count is <c>arc / BarStep</c> and
        /// both halves scale together, so the weave is identical and only its size changes.
        ///
        /// <para>There is deliberately no GAP dial here. This arena's across-grain density is a
        /// rib/hoop COUNT on a sphere (<see cref="BaseRibCount"/> / <see cref="BaseHoopCount"/>,
        /// compounded inward by <see cref="DensityStep"/>), not a step a factor can multiply — the
        /// weave IS the arena, and thinning it is a different cage rather than a sparser one. The
        /// table authors 1 for this rung and <see cref="AssertNoGapScale"/> holds it.</para></summary>
        const float S = SliceArenaGeometry.LengthScaleI3;

        /// <summary>Authored-units -> world-units for a PRISM'S OWN DIMENSIONS and for the
        /// along-grain arc-length step that keeps a rib reading as one continuous bar. This rung's
        /// <see cref="S"/> is already 2, so here the split changes nothing at all — it is written
        /// out so a future envelope change cannot silently inflate this cage's prisms.</summary>
        const float P = SliceArenaGeometry.PrismScaleI3;

        /// <summary>Radial spacing between rinds. In authored units the shells land at
        /// 360 / 295 / 230 / 165 / 100; at the shipped <c>LengthScale</c> of 2 that is
        /// 720 / 590 / 460 / 330 / 200.</summary>
        const float ShellGap = 65f * S;

        /// <summary>
        /// Ceiling on <see cref="shellCount"/>. Intensity does NOT map 1:1 onto shells: the ramp
        /// is 2/3/4/5 rinds for intensity 1..4, because a single shell cannot reach the ~10k
        /// prism budget without closing the outer weave the design depends on. So even the
        /// easiest arena is already a layered orange.
        /// </summary>
        public const int MaxShells = 5;

        [Header("Cleave")]
        [Tooltip("How many concentric rinds to build, from the outer shell inward. It is NO " +
                 "LONGER an intensity dial - Cleave's intensity picks WHICH ARENA, and this class " +
                 "is one of four, wired to intensity 3 alone at three rinds. Changing it re-cuts " +
                 "that rung: re-run Tools/Build/cleave_arena_harness (which MEASURES what this " +
                 "file emits) and Tools/Build/cleave_budget.py --check, which gates the cell's " +
                 "PhaseThresholds and the destruction target against the new count.")]
        [SerializeField, Range(1, MaxShells)] int shellCount = 1;

        // Rib/hoop counts of the OUTERMOST shell. Every shell inward multiplies both by
        // DensityStep, so these two numbers set the surface weave and DensityStep sets the ramp.
        const int BaseRibCount = 24;    // meridian great circles (pole to pole)
        const int BaseHoopCount = 11;   // latitude hoops (kept ODD - equator plus symmetric pairs)

        /// <summary>
        /// Per-shell density multiplier, applied to BOTH rib and hoop counts: shell k has
        /// round(Base * DensityStep^k). This is what makes the core the hard part - it compounds
        /// with the tightening that shrinking radius already provides, so the four shells go from
        /// ~188u cells at the surface to ~44u at the core. Turn this DOWN before turning shells
        /// down if the collider budget bites; it is the second-cheapest dial after shellCount.
        /// </summary>
        const float DensityStep = 1.05f;

        const float BarStep = 17f * P;      // arc-length spacing along every rib and hoop
        const float StrutStep = 26f * P;    // arc-length spacing along a triangulating diagonal
        const float StrutLength = 24f * P;  // long axis of a diagonal prism - a CEILING now, see below
        const float CrownLat = 84f;
        const int CrownCount = 18;

        // ── Prism sizes, and flush ───────────────────────────────────────────
        // NO PRISM MAY INTERPENETRATE ANOTHER (Docs/ECOSYSTEM.md §49.10). Every bar here is a
        // member of a RAIL - a rib's great circle, a hoop, a crown ring, a strut's diagonal - and
        // its LENGTH is fitted to the rail: the most that keeps it on its own side of the planes
        // that bisect it from its neighbours (BuildRibs and friends). The authored lengths below
        // are ceilings the fit never exceeds. Where rails cross, ONE yields: a rib stops at a
        // joint, a hoop and a crown; a hoop stops at a joint; a strut stops at both ribs, both
        // hoops and both joints of its cell (TryFit). Each bar's one jitter draw goes to the one
        // axis no crossing is planned around - its radial height.
        /// <summary>Rib and hoop cross-section: width fixed (what a crossing stops at), height
        /// jittered.</summary>
        const float BarSection = 3.6f * P;
        const float BarLength = 16f * P;
        const float StrutSection = 2.4f * P;
        /// <summary>A joint's edge, jittered as it always was; every rail that meets a joint stops
        /// where the LARGEST joint would end (its draw comes after theirs), so a smaller knuckle
        /// shows a hair of daylight round it rather than costing its own size.</summary>
        const float JointSize = 5.4f * P;
        const float CrownSection = 3.2f * P;
        const float CrownLength = 12f * P;
        /// <summary>How close to exact contact a FITTED face is laid: a hair under, so float noise
        /// never turns a touching pair into an interpenetrating one.</summary>
        const float Flush = 0.995f;
        /// <summary>Clearance kept from a crossing on top of the exact geometry - float noise only.</summary>
        const float Clearance = 0.05f;
        /// <summary>The largest factor <c>Jit</c> can draw.</summary>
        const float JitMost = 1.2f;
        const float HoopSpanDeg = 78f;  // outermost hoop latitude; poles are closed by crowns

        /// <summary>
        /// Longitude offset of each shell, as a FRACTION of that shell's own rib spacing. No two
        /// of the four shells share a phase, so the open cells never line up radially and there is
        /// no free run from the outside to the core - you always have bone in front of you.
        /// </summary>
        static readonly float[] ShellLonOffsets = { 0f, 0.5f, 0.25f, 0.75f, 0.375f };

        /// <summary>
        /// Per-shell axis tilt as (degrees FROM world-up, azimuth of the tilt). Shell 0 is
        /// deliberately UNTILTED - the outer silhouette, the AI's aim point and the player spawn
        /// ring are all defined against it. Every shell inside it leans onto its own axis, and both
        /// the lean and its direction change per rind, so no two dense polar caps line up and there
        /// is no orientation from which the whole cage is easy.
        ///
        /// Angles are authored rather than generated: they are a gameplay surface (which approach
        /// is hard, and how much the shape "twists" as you peel it), and a formula would make them
        /// harder to tune than to read.
        /// </summary>
        static readonly Vector2[] ShellTilts =
        {
            new Vector2(0f,   0f),     // outer rind - canonical orientation
            new Vector2(34f,  0f),
            new Vector2(61f,  110f),
            new Vector2(48f,  215f),
            new Vector2(76f,  305f),
        };

        /// <summary>
        /// Every Nth rib prism is laid as a DANGER bar instead of a plain one - the arena's trap.
        /// Now that the bars are plain, a danger bar is not harder or softer than its neighbours;
        /// it is pure downside. What it costs you is contact: the standard danger-prism punishment
        /// (volume-independent full-stop slow, a 4s all-element debuff, boost reset). So "just ram
        /// everything" stops being the answer - you have to read the bar before you commit.
        /// </summary>
        const int DangerEveryNthRibPrism = 19;

        /// <summary>Per-shell phase offset for the trap walk, so traps don't stack radially.</summary>
        const int DangerShellPhase = 7919;

        // Structural triad - all three domains present, per the no-domain-asymmetry spirit.
        static readonly Domains[] BoneDoms = { Domains.Jade, Domains.Ruby, Domains.Gold };

        /// <summary>Everything one shell needs, resolved once so no Build* method re-derives it.</summary>
        readonly struct ShellSpec
        {
            public readonly int Index;
            public readonly float Radius;
            public readonly float LonOffset;   // radians
            public readonly int Ribs;
            public readonly float[] Lats;      // degrees, ASCENDING (banding needs adjacency)

            /// <summary>
            /// Rotation from this shell's LOCAL frame (poles on +/-Y) into cell space. Every
            /// position and direction the builders compute is local and passes through here, so
            /// tilting a rind is one multiply rather than five special cases.
            /// </summary>
            public readonly Quaternion Orientation;

            public ShellSpec(int index)
            {
                Index = index;
                Radius = CageR - index * ShellGap;
                float scale = Mathf.Pow(DensityStep, index);
                Ribs = Mathf.Max(3, Mathf.RoundToInt(BaseRibCount * scale));

                int hoops = Mathf.Max(3, Mathf.RoundToInt(BaseHoopCount * scale));
                if ((hoops & 1) == 0) hoops++;   // must stay odd: equator + symmetric pairs
                Lats = BuildHoopLats(hoops);

                LonOffset = ShellLonOffsets[index % ShellLonOffsets.Length] * Mathf.PI * 2f / Ribs;

                // Lean the shell's pole away from world-up by `tilt`, in the direction `azimuth`.
                // Composing azimuth THEN tilt keeps the two readable and independent: the second
                // number only decides WHICH way the cap leans, never how far.
                var t = ShellTilts[index % ShellTilts.Length];
                Orientation = Quaternion.AngleAxis(t.y, Vector3.up) * Quaternion.AngleAxis(t.x, Vector3.forward);
            }

            public float Longitude(int rib) => rib * Mathf.PI * 2f / Ribs + LonOffset;

            /// <summary>Local shell point -> cell space.</summary>
            public Vector3 ToCell(Vector3 local) => Orientation * local;
        }

        /// <summary>
        /// Latitude hoops, GENERATED rather than hand-listed so the count is one number to turn and
        /// the Python budget model can mirror it exactly. Returned ASCENDING because the
        /// triangulation walks ADJACENT bands.
        /// </summary>
        static float[] BuildHoopLats(int hoopCount)
        {
            int half = (hoopCount - 1) / 2;
            var lats = new float[1 + half * 2];
            for (int i = 0; i < lats.Length; i++)
                lats[i] = -HoopSpanDeg + 2f * HoopSpanDeg * i / (lats.Length - 1);
            return lats;
        }

        int Shells => Mathf.Clamp(shellCount, 1, MaxShells);

        protected override int DefaultSeed => 39;

        // Hashes the real generation parameters, not a bump-me constant: change the weave OR the
        // shell count and the SpawnableBase cache invalidates itself rather than serving a stale
        // point cloud. shellCount is in here because the four prefab variants share this script.
        protected override int BuildParameterHash() => System.HashCode.Combine(
            nameof(SpawnableRibcage), CageR, ShellGap, Shells,
            System.HashCode.Combine(BaseRibCount, BaseHoopCount, DensityStep, HoopSpanDeg),
            System.HashCode.Combine(BarStep, StrutStep, StrutLength, CrownLat, CrownCount,
                                    DangerEveryNthRibPrism, TiltHash()),
            System.HashCode.Combine(Flush, Clearance, JointSize));

        /// <summary>Folds the authored tilt table into the cache key - retuning a lean must
        /// invalidate a cached point cloud exactly like retuning the weave does.</summary>
        static int TiltHash()
        {
            int h = 17;
            foreach (var t in ShellTilts) h = h * 31 + t.GetHashCode();
            return h;
        }

        // Pre-size for the worst case this variant can build. Shells shrink inward but densify, so
        // they all land in the same 2-5.6k band; 5800 apiece clears the measured maximum (5,575)
        // and never over-allocates by more than a shell's worth.
        protected override int LayCapacity => 5800 * Shells;

        /// <summary>
        /// The Cage carries no gap dial, so this asserts the table has not grown one for it.
        ///
        /// A gap scale multiplies an ACROSS-grain STEP, which the Panes and the Swell both have
        /// (a rib spacing). This arena's across-grain density is a rib/hoop COUNT on a sphere
        /// compounded inward by <see cref="DensityStep"/> — there is no step to multiply, so a
        /// gap factor authored here would silently do NOTHING while the table said it was
        /// spacing the arena out. That is worse than either outcome: it reads as tuned and is
        /// inert. The guard is loud rather than a comment because the table lives in another
        /// file and the next person to open up a rung will edit that one.
        /// </summary>
        static void AssertNoGapScale()
        {
            if (Mathf.Approximately(SliceArenaGeometry.GapScaleI3, 1f)) return;
            CSDebug.LogError(
                $"{nameof(SpawnableRibcage)}: SliceArenaGeometry.GapScaleI3 is " +
                $"{SliceArenaGeometry.GapScaleI3}, but this arena has no across-grain STEP for a " +
                "gap scale to multiply — its density is a rib/hoop COUNT. The value is being " +
                "IGNORED. Either lower BaseRibCount/BaseHoopCount, or set GapScaleI3 back to 1.");
        }

        /// <summary>A Cleave arena STATES its prism sizes: scaling the arena is a
        /// SIMILARITY, so the prisms grow with the spacing and a rib keeps reading as a
        /// continuous bar. The Cage sits inside the shared prefab's window at 2 x, so this changes nothing it lays
        /// today; it is on so all four rungs of one mode answer the question the same way.</summary>
        protected override bool AdmitsAuthoredPrismScale => true;

        protected override void BuildEnvironment()
        {
            AssertNoGapScale();

            for (int shell = 0; shell < Shells; shell++)
            {
                var spec = new ShellSpec(shell);
                MeasureNodes(spec);
                BuildRibs(spec);
                BuildHoops(spec);
                BuildTriangulation(spec);
                BuildJoints(spec);
                BuildCrowns(spec);
            }
        }

        /// <summary>Point on a shell at longitude/latitude, in radians.</summary>
        static Vector3 Shell(float lonRad, float latRad, float radius) => new(
            radius * Mathf.Cos(latRad) * Mathf.Cos(lonRad),
            radius * Mathf.Sin(latRad),
            radius * Mathf.Cos(latRad) * Mathf.Sin(lonRad));

        /// <summary>Shell-local east (along a hoop) at a longitude.</summary>
        static Vector3 East(float lonRad) => new(-Mathf.Sin(lonRad), 0f, Mathf.Cos(lonRad));

        /// <summary>Shell-local north (along a rib) at a longitude/latitude.</summary>
        static Vector3 North(float lonRad, float latRad) => new(
            -Mathf.Sin(latRad) * Mathf.Cos(lonRad), Mathf.Cos(latRad), -Mathf.Sin(latRad) * Mathf.Sin(lonRad));

        /// <summary>
        /// Meridian ribs: full great circles through the poles. The bar's LONG axis (+z) runs
        /// along the rib's own tangent so the bones read as continuous bone, not as beads.
        ///
        /// A great circle at longitude <c>lon</c> is also the great circle at <c>lon + pi</c>, so
        /// on a shell with an EVEN rib count (the outer and inner rinds: 24 and 26) rib k and rib
        /// k + Ribs/2 are one circle laid twice, their bars interleaved half a step apart. That is
        /// the rail a bar is fitted to: its neighbours are the half-step ones, wherever they came
        /// from. Each bar is then kept inside its own WEDGE - the half-planes through the pole axis
        /// half-way to the next rib either side - which only binds near a pole, where every rib
        /// converges: there a bar narrows (and shortens) until it no longer reaches the next rib's.
        /// It stops at the hoop (and the joint, where there is one) at every node it reaches, and
        /// at the crown ring.
        /// </summary>
        void BuildRibs(ShellSpec s)
        {
            int perRib = Mathf.RoundToInt(2f * Mathf.PI * s.Radius / BarStep);
            int ribs = s.Ribs;
            bool paired = (ribs & 1) == 0;
            float step = 2f * Mathf.PI / perRib;
            // On a paired circle with an even bar count the two ribs' bars would COINCIDE (a
            // placement no scale fixes): the second rib's bars move half a step. The shipped rinds'
            // counts are all odd, so this only guards an edit.
            bool coincident = paired && (perRib & 1) == 0;
            float gap = paired ? 0.5f * step : step;
            float hx = 0.5f * BarSection;

            // Half a rail gap's sine and cosine: a neighbour plane's normal is the circle's tangent
            // there, by the angle-sum identities rather than two more trig calls per bar.
            float halfCos = Mathf.Cos(0.5f * gap), halfSin = Mathf.Sin(0.5f * gap);

            EnsureMembers(ribs * perRib);
            for (int rib = 0; rib < ribs; rib++)
            {
                float lon = s.Longitude(rib);
                float cosLon = Mathf.Cos(lon), sinLon = Mathf.Sin(lon);
                int nearRay = RayOfRib(s, rib), farRay = (nearRay + _rays / 2) % _rays;
                for (int i = 0; i < perRib; i++)
                {
                    int g = rib * perRib + i;
                    // Sweep the full great circle: theta is the angle around it, so the same
                    // loop covers both hemispheres and both poles.
                    float theta = i * step + (coincident && rib >= ribs / 2 ? 0.5f * step : 0f);
                    float cosT = Mathf.Cos(theta), sinT = Mathf.Sin(theta);
                    // Shell(lon, theta, r), term for term.
                    var local = new Vector3(s.Radius * cosT * cosLon, s.Radius * sinT, s.Radius * cosT * sinLon);
                    var pos = s.ToCell(local);
                    // Tangent along the great circle = d/dtheta of Shell, in the shell's own frame.
                    var tangent = s.ToCell(new Vector3(-sinT * cosLon, cosT, -sinT * sinLon));
                    Member(g, pos, tangent, pos.normalized, hx, 0.5f * BarSection * JitFactor());

                    // The rail: the planes through the centre half a gap either side - each the
                    // circle's tangent there (shell-local, then into the cell).
                    float lo = -0.5f * BarLength, hi = 0.5f * BarLength;
                    float cosA = cosT * halfCos - sinT * halfSin, sinA = sinT * halfCos + cosT * halfSin;
                    float cosB = cosT * halfCos + sinT * halfSin, sinB = sinT * halfCos - cosT * halfSin;
                    Bound(g, s.ToCell(new Vector3(-sinA * cosLon, cosA, -sinA * sinLon)), Vector3.zero, ref lo, ref hi);
                    Bound(g, s.ToCell(new Vector3(sinB * cosLon, -cosB, sinB * sinLon)), Vector3.zero, ref lo, ref hi);

                    // The wedge - the ray this bar is on (its own longitude while cos theta >= 0,
                    // the far one past the pole), half-way to the next ray either side.
                    int ray = cosT >= 0f ? nearRay : farRay;
                    Wedge(g, _wedgeAhead[ray], ref lo, ref hi);
                    Wedge(g, _wedgeBehind[ray], ref lo, ref hi);

                    // The nodes on this ray it can reach: the nearest hoop (and its joint), and
                    // the crown. Latitude: theta folded onto [-pi/2, pi/2].
                    ClearBlocks();
                    float lat = cosT >= 0f ? (theta > Mathf.PI ? theta - 2f * Mathf.PI : theta) : Mathf.PI - theta;
                    float band = (s.Lats[1] - s.Lats[0]) * Mathf.Deg2Rad;
                    int near = Mathf.RoundToInt((lat - s.Lats[0] * Mathf.Deg2Rad) / band);
                    for (int h = near - 1; h <= near + 1; h++)
                        if (h >= 0 && h < s.Lats.Length) BlockRibAtNode(g, ray * s.Lats.Length + h);
                    if (Mathf.Abs(lat) > (CrownLat - 10f) * Mathf.Deg2Rad)
                        BlockRibAtCrown(g, ray * 2 + (lat > 0f ? 0 : 1));
                    Plan(g, lo, hi);
                }
            }

            // The rails: each great circle, its bars in order round it.
            int circles = paired ? ribs / 2 : ribs;
            for (int c = 0; c < circles; c++)
            {
                int count = 0;
                for (int pass = 0; pass < (paired ? 2 : 1); pass++)
                {
                    int rib = c + pass * (ribs / 2);
                    for (int i = 0; i < perRib; i++)
                    {
                        float theta = i * step + (coincident && rib >= ribs / 2 ? 0.5f * step : 0f);
                        // The partner rib runs the circle the other way: its theta is pi - ours.
                        float key = pass == 0 ? theta : Mathf.PI - theta;
                        key -= Mathf.FloorToInt(key / (2f * Mathf.PI)) * 2f * Mathf.PI;
                        _rail[count] = rib * perRib + i; _railKey[count] = key; count++;
                    }
                }
                if (paired) System.Array.Sort(_railKey, _rail, 0, count);
                RehouseStuck(count, true, gap * s.Radius);
            }

            for (int rib = 0; rib < ribs; rib++)
            {
                var dom = BoneDoms[rib % BoneDoms.Length];
                for (int i = 0; i < perRib; i++)
                {
                    // Deterministic, evenly-spread trap bars (see DangerEveryNthRibPrism). Indexed
                    // on the GLOBAL rib prism counter so the pattern walks around the cage instead
                    // of stacking the traps at the same latitude on every rib, plus a per-shell
                    // phase so it doesn't stack radially either.
                    bool danger = (s.Index * DangerShellPhase + rib * perRib + i)
                                  % DangerEveryNthRibPrism == 0;
                    Lay(rib * perRib + i, dom, danger ? PrismKind.Danger : PrismKind.Plain);
                }
            }
        }

        /// <summary>Latitude hoops - the bands that bind the ribs. Blue: neutral binding bone. A
        /// hoop is a chord ring: each bar stops on the half-plane through the pole axis half-way to
        /// its neighbour, and at the joint of every node it reaches. Ribs stop at hoops, so a hoop
        /// runs on through a node without a joint.</summary>
        void BuildHoops(ShellSpec s)
        {
            for (int hoop = 0; hoop < s.Lats.Length; hoop++)
            {
                float lat = s.Lats[hoop] * Mathf.Deg2Rad;
                float ringR = s.Radius * Mathf.Cos(lat);
                int n = Mathf.RoundToInt(2f * Mathf.PI * ringR / BarStep);
                float dLon = 2f * Mathf.PI / n;
                float halfCos = Mathf.Cos(0.5f * dLon), halfSin = Mathf.Sin(0.5f * dLon);
                float cosLat = Mathf.Cos(lat), sinLat = Mathf.Sin(lat);
                EnsureMembers(n);
                for (int i = 0; i < n; i++)
                {
                    float lon = i * dLon + s.LonOffset;
                    float cosLon = Mathf.Cos(lon), sinLon = Mathf.Sin(lon);
                    // Shell(lon, lat, r), term for term.
                    var pos = s.ToCell(new Vector3(s.Radius * cosLat * cosLon, s.Radius * sinLat,
                                                   s.Radius * cosLat * sinLon));
                    var east = s.ToCell(new Vector3(-sinLon, 0f, cosLon));
                    var outward = s.ToCell(new Vector3(cosLon, 0f, sinLon));
                    Member(i, pos, east, pos.normalized, 0.5f * BarSection, 0.5f * BarSection * JitFactor());
                    // The half-planes through the pole axis half a step either side: east there,
                    // by the angle-sum identity.
                    float lo = -0.5f * BarLength, hi = 0.5f * BarLength;
                    Bound(i, east * halfCos - outward * halfSin, Vector3.zero, ref lo, ref hi);
                    Bound(i, -(east * halfCos + outward * halfSin), Vector3.zero, ref lo, ref hi);

                    // The joints either side: one sits on every rib longitude.
                    ClearBlocks();
                    float ribStep = 2f * Mathf.PI / s.Ribs;
                    int j0 = Mathf.RoundToInt((lon - s.LonOffset) / ribStep);
                    for (int j = j0 - 1; j <= j0 + 1; j++)
                    {
                        int node = RayOfRib(s, j) * s.Lats.Length + hoop;
                        if ((pos - _nodePos[node]).sqrMagnitude > NodeReach * NodeReach) continue;
                        var e = _rayEast[RayOfRib(s, j)];
                        BlockSlab(i, e, Vector3.Dot(e, _nodePos[node]), _nodeJointE[node]);
                    }
                    Plan(i, lo, hi);
                    _rail[i] = i;
                }
                RehouseStuck(n, true, dLon * ringR);
                for (int i = 0; i < n; i++) Lay(i, Domains.Blue, PrismKind.Plain);
            }
        }

        /// <summary>
        /// One diagonal through EVERY rib x hoop cell, which is what turns the weave's openings
        /// from quadrilaterals ("bubbles") into TRIANGLES - each quad is split into two. The
        /// diagonal's direction alternates on a checkerboard so the result zigzags like a truss
        /// instead of shearing uniformly one way.
        ///
        /// A strut is a member of its diagonal (stopping on the plane that bisects it from the
        /// next) and yields to everything at the cell's edges: both ribs' meridian slabs (and, on
        /// an odd-rib rind, the far side of the rib that runs down the middle of every cell), both
        /// hoops and both joints at its ends.
        /// </summary>
        void BuildTriangulation(ShellSpec s)
        {
            bool odd = (s.Ribs & 1) == 1;
            for (int band = 0; band < s.Lats.Length - 1; band++)
            {
                float latLo = s.Lats[band] * Mathf.Deg2Rad;
                float latHi = s.Lats[band + 1] * Mathf.Deg2Rad;

                for (int rib = 0; rib < s.Ribs; rib++)
                {
                    float lonA = s.Longitude(rib);
                    float lonB = s.Longitude(rib + 1);

                    // Alternating lean: "/" on one cell, "\" on its neighbours.
                    bool lean = ((rib + band) & 1) == 0;
                    float latFrom = lean ? latLo : latHi, latTo = lean ? latHi : latLo;
                    var from = s.ToCell(Shell(lonA, latFrom, s.Radius));
                    var to = s.ToCell(Shell(lonB, latTo, s.Radius));
                    var along = to - from;
                    var axis = along.normalized;
                    int n = Mathf.Max(1, Mathf.RoundToInt(along.magnitude / StrutStep));
                    int rayA = RayOfRib(s, rib), rayB = RayOfRib(s, rib + 1);

                    EnsureMembers(n);
                    for (int i = 0; i < n; i++)
                    {
                        // Push the strut back onto the shell - a straight chord would sag inside it.
                        var pos = StrutAt(from, to, (i + 0.5f) / n, s.Radius);
                        float hs = 0.5f * StrutSection * JitFactor();
                        Member(i, pos, axis, pos.normalized, hs, hs);

                        float lo = -0.5f * StrutLength, hi = 0.5f * StrutLength;
                        if (i > 0)
                        {
                            var prev = StrutAt(from, to, (i - 0.5f) / n, s.Radius);
                            Bound(i, (prev - pos).normalized, 0.5f * (prev + pos), ref lo, ref hi);
                        }
                        if (i < n - 1)
                        {
                            var next = StrutAt(from, to, (i + 1.5f) / n, s.Radius);
                            Bound(i, (next - pos).normalized, 0.5f * (next + pos), ref lo, ref hi);
                        }

                        ClearBlocks();
                        float ribHalf = 0.5f * BarSection;
                        BlockSlab(i, _rayEast[rayA], 0f, ribHalf);
                        BlockSlab(i, _rayEast[rayB], 0f, ribHalf);
                        if (odd) BlockSlab(i, _rayEast[rayA + 1], 0f, ribHalf);
                        BlockStrutAtNode(i, rayA * s.Lats.Length + (lean ? band : band + 1));
                        BlockStrutAtNode(i, rayB * s.Lats.Length + (lean ? band + 1 : band));
                        Plan(i, lo, hi);
                        _rail[i] = i;
                    }
                    RehouseStuck(n, false, along.magnitude / n);
                    for (int i = 0; i < n; i++) Lay(i, Domains.Blue, PrismKind.Plain);
                }
            }
        }

        static Vector3 StrutAt(Vector3 from, Vector3 to, float u, float radius) =>
            Vector3.Lerp(from, to, u).normalized * radius;

        /// <summary>Chunky knuckles where a rib crosses a hoop - the cage's visual anchors, and the
        /// one prism at a node that yields to nothing: the rib, the hoop and the struts all stop at
        /// it, at its largest jitter.</summary>
        void BuildJoints(ShellSpec s)
        {
            for (int rib = 0; rib < s.Ribs; rib++)
            {
                float lon = s.Longitude(rib);
                var dom = BoneDoms[rib % BoneDoms.Length];
                foreach (float latDeg in s.Lats)
                {
                    var pos = s.ToCell(Shell(lon, latDeg * Mathf.Deg2Rad, s.Radius));
                    Emit(pos, SpawnPoint.LookRotation(pos.normalized, Vector3.up),
                        Jit(new Vector3(JointSize, JointSize, JointSize)), dom, PrismKind.Plain);
                }
            }
        }

        /// <summary>
        /// Polar crowns: a tight ring just short of each pole, closing the cap where the ribs
        /// converge and would otherwise read as a spike of bone. A chord ring like a hoop; the ribs
        /// stop at it.
        /// </summary>
        void BuildCrowns(ShellSpec s)
        {
            float dLon = 2f * Mathf.PI / CrownCount;
            for (int pole = 0; pole < 2; pole++)
            {
                float lat = (pole == 0 ? CrownLat : -CrownLat) * Mathf.Deg2Rad;
                EnsureMembers(CrownCount);
                for (int i = 0; i < CrownCount; i++)
                {
                    float lon = i * dLon + s.LonOffset;
                    var pos = s.ToCell(Shell(lon, lat, s.Radius));
                    Member(i, pos, s.ToCell(East(lon)), pos.normalized, 0.5f * CrownSection,
                           0.5f * CrownSection * JitFactor());
                    float lo = -0.5f * CrownLength, hi = 0.5f * CrownLength;
                    Bound(i, s.ToCell(East(lon + 0.5f * dLon)), Vector3.zero, ref lo, ref hi);
                    Bound(i, -s.ToCell(East(lon - 0.5f * dLon)), Vector3.zero, ref lo, ref hi);
                    ClearBlocks();
                    Plan(i, lo, hi);
                    _rail[i] = i;
                }
                for (int i = 0; i < CrownCount; i++)
                    Lay(i, BoneDoms[i % BoneDoms.Length], PrismKind.Plain);
            }
        }

        // ── What a rib or strut stops at ─────────────────────────────────────
        // Every node of a rind - a ray (a rib's meridian, either side of the pole) crossing a
        // hoop - and every point where a ray crosses a crown ring is measured ONCE per rind
        // (MeasureNodes), so a bar asks a table, not the geometry.

        /// <summary>How near a bar has to be to a node for the node to be worth asking about -
        /// well past the longest bar's reach plus the deepest knuckle.</summary>
        const float NodeReach = 60f;

        int _rays, _nodeHoopsPerRay;
        float _rayGap;
        Vector3[] _rayEast, _wedgeAhead, _wedgeBehind;
        Vector3[] _nodePos, _nodeNorth, _crownPos, _crownNorth;
        float[] _nodeRib, _nodeHoop, _nodeJointN, _nodeJointE, _crownRib;

        /// <summary>
        /// Measure this rind's rays and nodes. A ray is half a great circle: an even rib count
        /// lays each great circle twice, so its rays are the rib longitudes; an odd count's far
        /// halves fall half-way between, so there are twice as many (and only the rib longitudes
        /// carry joints). Per node: the hoop - a slab square to the rib there, as deep as the hoop
        /// bars either side actually reach along it - and, where there is one, the joint, both at
        /// their largest; per crown crossing, the crown ring the same way.
        /// </summary>
        void MeasureNodes(ShellSpec s)
        {
            bool paired = (s.Ribs & 1) == 0;
            _rays = paired ? s.Ribs : 2 * s.Ribs;
            _rayGap = 2f * Mathf.PI / _rays;
            int hoops = s.Lats.Length;
            _nodeHoopsPerRay = hoops;
            if (_rayEast == null || _rayEast.Length < _rays)
            {
                _rayEast = new Vector3[_rays]; _wedgeAhead = new Vector3[_rays]; _wedgeBehind = new Vector3[_rays];
                _crownPos = new Vector3[2 * _rays]; _crownNorth = new Vector3[2 * _rays]; _crownRib = new float[2 * _rays];
            }
            if (_nodePos == null || _nodePos.Length < _rays * hoops)
            {
                int n = _rays * hoops;
                _nodePos = new Vector3[n]; _nodeNorth = new Vector3[n];
                _nodeRib = new float[n]; _nodeHoop = new float[n]; _nodeJointN = new float[n]; _nodeJointE = new float[n];
            }
            for (int k = 0; k < _rays; k++)
            {
                float lon = s.LonOffset + k * _rayGap;
                bool joint = paired || (k & 1) == 0;
                var east = s.ToCell(East(lon));
                _rayEast[k] = east;
                _wedgeAhead[k] = s.ToCell(East(lon + 0.5f * _rayGap));
                _wedgeBehind[k] = -s.ToCell(East(lon - 0.5f * _rayGap));
                for (int h = 0; h < hoops; h++)
                {
                    float lat = s.Lats[h] * Mathf.Deg2Rad;
                    int at = k * hoops + h;
                    var node = s.ToCell(Shell(lon, lat, s.Radius));
                    var north = s.ToCell(North(lon, lat));
                    _nodePos[at] = node; _nodeNorth[at] = north;
                    _nodeHoop[at] = RingSupport(s, lat, lon, north, node, BarSection, BarLength,
                        Mathf.RoundToInt(2f * Mathf.PI * s.Radius * Mathf.Cos(lat) / BarStep));
                    _nodeJointN[at] = joint ? JointSupport(node, north) : 0f;
                    _nodeJointE[at] = joint ? JointSupport(node, east) : 0f;
                    _nodeRib[at] = Mathf.Max(_nodeHoop[at], _nodeJointN[at]);
                }
                for (int pole = 0; pole < 2; pole++)
                {
                    float lat = (pole == 0 ? CrownLat : -CrownLat) * Mathf.Deg2Rad;
                    var node = s.ToCell(Shell(lon, lat, s.Radius));
                    var north = s.ToCell(North(lon, lat));
                    _crownPos[2 * k + pole] = node; _crownNorth[2 * k + pole] = north;
                    _crownRib[2 * k + pole] = RingSupport(s, lat, lon, north, node, CrownSection, CrownLength, CrownCount);
                }
            }
        }

        /// <summary>The ray of rib <paramref name="rib"/>'s own (near) half.</summary>
        int RayOfRib(ShellSpec s, int rib)
        {
            int k = (s.Ribs & 1) == 0 ? rib : 2 * rib;
            return ((k % _rays) + _rays) % _rays;
        }

        /// <summary>A rib bar stops at the node's hoop, and at its joint where it has one - one
        /// slab square to the rib, the deeper of the two.</summary>
        void BlockRibAtNode(int g, int node)
        {
            if ((_mPos[g] - _nodePos[node]).sqrMagnitude > NodeReach * NodeReach) return;
            BlockSlab(g, _nodeNorth[node], Vector3.Dot(_nodeNorth[node], _nodePos[node]), _nodeRib[node]);
        }

        /// <summary>A rib bar stops at the crown ring where its ray crosses it.</summary>
        void BlockRibAtCrown(int g, int crossing)
        {
            if ((_mPos[g] - _crownPos[crossing]).sqrMagnitude > NodeReach * NodeReach) return;
            BlockSlab(g, _crownNorth[crossing], Vector3.Dot(_crownNorth[crossing], _crownPos[crossing]),
                      _crownRib[crossing]);
        }

        /// <summary>A strut stops at the hoop through its end node and at that node's joint - a
        /// box, bounded across the node too, so a strut leaving it diagonally only has to clear
        /// its corner.</summary>
        void BlockStrutAtNode(int g, int node)
        {
            if ((_mPos[g] - _nodePos[node]).sqrMagnitude > NodeReach * NodeReach) return;
            var m = _nodeNorth[node];
            float alongM = Vector3.Dot(m, _nodePos[node]);
            BlockSlab(g, m, alongM, _nodeHoop[node]);
            var e = _rayEast[node / _nodeHoopsPerRay];
            BlockBox(g, m, alongM, _nodeJointN[node], e, Vector3.Dot(e, _nodePos[node]), _nodeJointE[node]);
        }

        /// <summary>
        /// How far the bars of a chord ring (a hoop or a crown: <paramref name="count"/> bars at
        /// latitude <paramref name="lat"/>, section <paramref name="section"/>, length ceiling
        /// <paramref name="length"/>) reach from <paramref name="node"/> along
        /// <paramref name="m"/>, over the two bars either side of longitude <paramref name="lon"/>
        /// - each at its largest: its rail-fitted length, its height at the top of its jitter.
        /// </summary>
        static float RingSupport(ShellSpec s, float lat, float lon, Vector3 m, Vector3 node,
                                 float section, float length, int count)
        {
            float dLon = 2f * Mathf.PI / count;
            float hx = 0.5f * section, hy = 0.5f * section * JitMost;
            float rho = s.Radius * Mathf.Cos(lat);
            float sinLat = Mathf.Abs(Mathf.Sin(lat)), cosLat = Mathf.Abs(Mathf.Cos(lat));
            float hz = Mathf.Min(0.5f * length,
                (rho - hx * sinLat - hy * cosLat) * Mathf.Sin(0.5f * dLon) / Mathf.Cos(0.5f * dLon));
            int i0 = Mathf.FloorToInt((lon - s.LonOffset) / dLon);
            float most = 0f;
            for (int i = i0; i <= i0 + 1; i++)
            {
                float bl = i * dLon + s.LonOffset;
                var c = s.ToCell(Shell(bl, lat, s.Radius));
                var z = s.ToCell(East(bl));
                var y = c.normalized;
                var x = Vector3.Cross(y, z).normalized;
                float reach = Mathf.Abs(Vector3.Dot(c - node, m)) + hx * Mathf.Abs(Vector3.Dot(x, m))
                            + hy * Mathf.Abs(Vector3.Dot(y, m)) + hz * Mathf.Abs(Vector3.Dot(z, m));
                most = Mathf.Max(most, reach);
            }
            return most;
        }

        /// <summary>How far a joint at <paramref name="node"/> reaches along <paramref name="n"/>,
        /// at the top of its jitter.</summary>
        static float JointSupport(Vector3 node, Vector3 n)
        {
            var z = node.normalized;
            var x = Vector3.Cross(Vector3.up, z).normalized;
            var y = Vector3.Cross(z, x);
            return 0.5f * JointSize * JitMost * (Mathf.Abs(Vector3.Dot(x, n)) + Mathf.Abs(Vector3.Dot(y, n))
                                     + Mathf.Abs(Vector3.Dot(z, n)));
        }

        // ── Rails ────────────────────────────────────────────────────────────
        // A member is planned before it is laid: its box, the slot its rail gives it, and what it
        // stops at. Planning walks the members in the order they are laid and spends each one's
        // jitter draw as it goes, so the stream is unchanged; laying draws nothing.

        Vector3[] _mPos, _mZ, _mX, _mY;
        float[] _mHx, _mHy, _mLo, _mHi;
        bool[] _mClear;
        int[] _mHost, _mGuestOf, _mGuestsLo, _mGuestsHi, _rail;
        float[] _railKey;
        readonly float[] _blockLo = new float[16], _blockHi = new float[16];
        int _blocks;

        void EnsureMembers(int count)
        {
            if (_mPos != null && _mPos.Length >= count) return;
            int n = Mathf.Max(count, 64);
            _mPos = new Vector3[n]; _mZ = new Vector3[n]; _mX = new Vector3[n]; _mY = new Vector3[n];
            _mHx = new float[n]; _mHy = new float[n]; _mLo = new float[n]; _mHi = new float[n];
            _mClear = new bool[n]; _mHost = new int[n]; _mGuestOf = new int[n];
            _mGuestsLo = new int[n]; _mGuestsHi = new int[n]; _rail = new int[n]; _railKey = new float[n];
        }

        /// <summary>Member <paramref name="g"/>'s box, framed exactly as LookRotation(z, up) frames
        /// it: x = up x z, y = z x x.</summary>
        void Member(int g, Vector3 pos, Vector3 z, Vector3 up, float hx, float hy)
        {
            z = z.normalized;
            var x = Vector3.Cross(up, z).normalized;
            _mPos[g] = pos; _mZ[g] = z; _mX[g] = x; _mY[g] = Vector3.Cross(z, x);
            _mHx[g] = hx; _mHy[g] = hy; _mHost[g] = g;
        }

        /// <summary>Tighten member <paramref name="g"/>'s slot [lo, hi] (offsets along its z) so it
        /// stays on the near side of the plane through <paramref name="at"/> with outward normal
        /// <paramref name="n"/>. An infeasible plane leaves lo > hi.</summary>
        void Bound(int g, Vector3 n, Vector3 at, ref float lo, ref float hi)
        {
            float room = Vector3.Dot(n, at - _mPos[g]) - _mHx[g] * Mathf.Abs(Vector3.Dot(_mX[g], n))
                       - _mHy[g] * Mathf.Abs(Vector3.Dot(_mY[g], n));
            float gz = Vector3.Dot(n, _mZ[g]);
            if (Mathf.Abs(gz) < 1e-5f) { if (room < 0f) { lo = 1f; hi = 0f; } return; }
            if (gz > 0f) hi = Mathf.Min(hi, room / gz); else lo = Mathf.Max(lo, room / gz);
        }

        /// <summary>
        /// Keep a rib bar inside the half-plane through the pole axis with outward normal
        /// <paramref name="n"/>, giving up WIDTH and length in equal shares of reach (the
        /// volume-optimal cut) rather than length alone - near a pole, where ribs converge, the
        /// bar's own width is what reaches the next rib.
        /// </summary>
        void Wedge(int g, Vector3 n, ref float lo, ref float hi)
        {
            float room = -Vector3.Dot(n, _mPos[g]) - _mHy[g] * Mathf.Abs(Vector3.Dot(_mY[g], n));
            float ax = Mathf.Abs(Vector3.Dot(_mX[g], n)), az = Mathf.Abs(Vector3.Dot(_mZ[g], n));
            float cap = Mathf.Max(-lo, hi);
            if (room <= 0f || cap <= 0f) { lo = 1f; hi = 0f; return; }
            if (ax * _mHx[g] + az * cap <= room) return;
            float a = 0.5f * room / Mathf.Max(ax, 1e-5f), z = 0.5f * room / Mathf.Max(az, 1e-5f);
            if (a >= _mHx[g]) { a = _mHx[g]; z = (room - ax * a) / Mathf.Max(az, 1e-5f); }
            else if (z >= cap) { z = cap; a = (room - az * z) / Mathf.Max(ax, 1e-5f); }
            _mHx[g] = Mathf.Max(0.01f, a);
            lo = Mathf.Max(lo, -z); hi = Mathf.Min(hi, z);
        }

        void ClearBlocks() => _blocks = 0;

        /// <summary>The offsets along member <paramref name="g"/>'s z at which its cross-section
        /// would enter the slab |n.p - centre| &lt; half, as an open interval.</summary>
        bool SlabInterval(int g, Vector3 n, float centre, float half, out float s0, out float s1)
        {
            float d = Vector3.Dot(n, _mPos[g]) - centre;
            float perp = _mHx[g] * Mathf.Abs(Vector3.Dot(_mX[g], n)) + _mHy[g] * Mathf.Abs(Vector3.Dot(_mY[g], n));
            float reach = half + perp + Clearance;
            float gz = Vector3.Dot(n, _mZ[g]);
            if (Mathf.Abs(gz) < 1e-5f)
            {
                s0 = float.NegativeInfinity; s1 = float.PositiveInfinity;
                return Mathf.Abs(d) < reach;
            }
            float a = (-reach - d) / gz, b = (reach - d) / gz;
            s0 = Mathf.Min(a, b); s1 = Mathf.Max(a, b);
            return true;
        }

        void BlockSlab(int g, Vector3 n, float centre, float half)
        {
            if (_blocks == _blockLo.Length || !SlabInterval(g, n, centre, half, out float s0, out float s1)) return;
            _blockLo[_blocks] = s0; _blockHi[_blocks] = s1; _blocks++;
        }

        /// <summary>A box bounded by two slabs: its sweep is blocked where BOTH are.</summary>
        void BlockBox(int g, Vector3 n1, float c1, float h1, Vector3 n2, float c2, float h2)
        {
            if (_blocks == _blockLo.Length) return;
            if (!SlabInterval(g, n1, c1, h1, out float a0, out float a1)) return;
            if (!SlabInterval(g, n2, c2, h2, out float b0, out float b1)) return;
            float s0 = Mathf.Max(a0, b0), s1 = Mathf.Min(a1, b1);
            if (s1 <= s0) return;
            _blockLo[_blocks] = s0; _blockHi[_blocks] = s1; _blocks++;
        }

        /// <summary>
        /// Plan member <paramref name="g"/>'s piece of its slot [lo, hi], clear of every block. The
        /// slot is filled; where something blocks it, the bar shrinks ABOUT the slot's centre until
        /// it stops at it, unless the centre is blocked (a placement no shrink can fix) or
        /// shrinking in place would keep under a quarter of the larger clear piece - then it
        /// becomes that piece. No clear piece at all leaves it to <see cref="RehouseStuck"/>.
        /// </summary>
        void Plan(int g, float lo, float hi)
        {
            _mClear[g] = false; _mLo[g] = _mHi[g] = 0f;
            if (hi <= lo) return;
            float mid = 0.5f * (lo + hi);
            bool centreClear = true;
            float room = 0.5f * (hi - lo);
            for (int i = 0; i < _blocks; i++)
            {
                if (_blockLo[i] < mid && _blockHi[i] > mid) { centreClear = false; break; }
                if (_blockHi[i] <= mid) room = Mathf.Min(room, mid - Mathf.Max(lo, _blockHi[i]));
                else room = Mathf.Min(room, Mathf.Min(hi, _blockLo[i]) - mid);
            }
            float inPlace = centreClear ? Mathf.Max(0f, room) : 0f;

            float best = 0f, bestMid = mid;
            for (int k = -1; k < _blocks; k++)
            {
                float start = k < 0 ? lo : Mathf.Max(lo, _blockHi[k]);
                if (start >= hi) continue;
                bool inside = false;
                for (int i = 0; i < _blocks; i++)
                    if (_blockLo[i] < start && _blockHi[i] > start) { inside = true; break; }
                if (inside) continue;
                float stop = hi;
                for (int i = 0; i < _blocks; i++)
                    if (_blockLo[i] >= start) stop = Mathf.Min(stop, _blockLo[i]);
                if (stop - start > best) { best = stop - start; bestMid = 0.5f * (start + stop); }
            }

            if (inPlace > 0f && 4f * (2f * inPlace) >= best) { _mLo[g] = mid - inPlace; _mHi[g] = mid + inPlace; }
            else if (best > 0f) { _mLo[g] = bestMid - 0.5f * best; _mHi[g] = bestMid + 0.5f * best; }
            else return;
            _mClear[g] = true;
        }

        /// <summary>
        /// A member with NO clear piece of its slot sits inside what it crosses - a placement no
        /// shrink can fix - so it moves, the least distance it can, along its own rail
        /// (<c>_rail</c>, in order): it becomes a GUEST in the nearest clear piece with room for one
        /// more, which is then shared evenly with its owner, no share under a quarter of a
        /// <paramref name="slot"/>. The owner keeps the share nearest where it was laid, guests
        /// take the shares on their own side, and every share is a sub-box of a box already clear
        /// of everything, laid on that member's own line - so a move reaches nothing new.
        /// </summary>
        void RehouseStuck(int count, bool cyclic, float slot)
        {
            float least = 0.25f * slot;
            for (int k = 0; k < count; k++) { _mGuestsLo[_rail[k]] = 0; _mGuestsHi[_rail[k]] = 0; }
            for (int j = 0; j < count; j++)
            {
                int gj = _rail[j];
                if (_mClear[gj]) continue;
                int host = -1;
                for (int d = 1; d < count && host < 0; d++)
                for (int dir = -1; dir <= 1 && host < 0; dir += 2)
                {
                    int r = j + dir * d;
                    if (cyclic) r = (r % count + count) % count;
                    else if (r < 0 || r >= count) continue;
                    int gr = _rail[r];
                    if (r == j || !_mClear[gr] || _mHost[gr] != gr) continue;
                    int members = 1 + _mGuestsLo[gr] + _mGuestsHi[gr] + 1;
                    if ((_mHi[gr] - _mLo[gr]) / members < least) continue;
                    host = gr;
                    bool fromLow = cyclic ? ((j - r + count) % count) > count / 2 : j < r;
                    if (fromLow) _mGuestsLo[gr]++; else _mGuestsHi[gr]++;
                    _mGuestOf[gj] = fromLow ? -_mGuestsLo[gr] : _mGuestsHi[gr];
                }
                _mHost[gj] = host < 0 ? gj : host;
                if (host < 0)
                {
                    _mLo[gj] = _mHi[gj] = 0f;
                    CSDebug.LogWarning($"[Ribcage] A bar at {_mPos[gj]} found no clear room on its rail; laid as a sliver.");
                }
            }
            for (int r = 0; r < count; r++)
            {
                int gr = _rail[r];
                int lowN = _mGuestsLo[gr], highN = _mGuestsHi[gr];
                if (lowN + highN == 0) continue;
                float width = (_mHi[gr] - _mLo[gr]) / (lowN + 1 + highN);
                float baseLo = _mLo[gr];
                for (int j = 0; j < count; j++)
                {
                    int gj = _rail[j];
                    if (_mHost[gj] != gr || gj == gr) continue;
                    int index = _mGuestOf[gj] < 0 ? lowN + _mGuestOf[gj] : lowN + _mGuestOf[gj];
                    _mLo[gj] = baseLo + width * index;
                    _mHi[gj] = _mLo[gj] + width;
                }
                _mLo[gr] = baseLo + width * lowN;
                _mHi[gr] = _mLo[gr] + width;
            }
        }

        /// <summary>Lay member <paramref name="g"/> as planned, on its host's line, with its own
        /// section no wider than the host's (the section its piece was planned clear at).</summary>
        void Lay(int g, Domains dom, PrismKind kind)
        {
            int host = _mHost[g];
            float centre = 0.5f * (_mLo[g] + _mHi[g]);
            float half = Mathf.Max(0.01f, 0.5f * (_mHi[g] - _mLo[g]) * Flush);
            float hx = Mathf.Min(_mHx[g], _mHx[host]), hy = Mathf.Min(_mHy[g], _mHy[host]);
            Emit(_mPos[host] + _mZ[host] * centre, SpawnPoint.LookRotation(_mZ[host], _mY[host]),
                new Vector3(2f * hx, 2f * hy, 2f * half), dom, kind);
        }

        /// <summary>One jitter factor in [0.8, 1.2] from the shared stream - exactly the one draw
        /// <c>Jit</c> makes, so spending it on a single axis moves no other prism.</summary>
        float JitFactor() => Jit(Vector3.one).x;
    }
}
