using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "PeelTheCage" - the cage cell environment and the arena of <see cref="GameModes.PeelTheCage"/>
    /// (player-facing name: "Peel the Cage"). A LAYERED ORANGE: one or more concentric hollow
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
    ///     INWARD (<see cref="ShellRadius"/> never moves) so the AI's aim point, the player spawn
    ///     ring and the arena's outer silhouette are identical at every intensity.
    ///   • The OUTER shell's weave is open - a ~94u x ~98u cell. This is a ribcage, not a prison
    ///     wall: you fly between the bones freely, and the gaps are what let you see the next rind
    ///     waiting behind this one.
    ///   • Each shell inward is <see cref="DensityStep"/> times denser than the one outside it, ON
    ///     TOP of the tightening that shrinking radius already gives - so the pith at the core is
    ///     a far harder skin than the rind at the surface (~94u cells outside, ~22u at the core
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
    /// Budget (analytic, confirm with FrogletTools > Ecology > Measure Cell Environment
    /// Baselines): 10,620 / 14,731 / 17,992 / 20,153 prisms at intensity 1..4 (2..5 shells). See
    /// PEEL_THE_CAGE.md for the per-shell table and the collider-budget statement, and
    /// Tools/Build/ribcage_budget.py for the model.
    /// </summary>
    public class SpawnableRibcage : CellEnvironmentSpawnableBase
    {
        // Outermost shell. Density is where the prism budget goes, not radius: a bigger sphere
        // would just move the arena out. The OUTER weave is deliberately open - see the summary.
        const float CageR = 360f;

        /// <summary>Radial spacing between rinds - shells land at 360 / 295 / 230 / 165 / 100.</summary>
        const float ShellGap = 65f;

        /// <summary>
        /// Ceiling on <see cref="shellCount"/>. Intensity does NOT map 1:1 onto shells: the ramp
        /// is 2/3/4/5 rinds for intensity 1..4, because a single shell cannot reach the ~10k
        /// prism budget without closing the outer weave the design depends on. So even the
        /// easiest arena is already a layered orange.
        /// </summary>
        public const int MaxShells = 5;

        /// <summary>
        /// The cage's OUTER shell radius, exposed so <c>PeelTheCageController</c> can aim its AI
        /// cage-breakers at the bone without hard-coding a second copy of the number. Shells are
        /// added inward, so this is intensity-independent and the AI needs no per-intensity case.
        /// </summary>
        public const float ShellRadius = CageR;

        [Header("PeelTheCage")]
        [Tooltip("How many concentric rinds to build, from the outer shell inward. THE INTENSITY " +
                 "DIAL: author one prefab variant per shell count and point each intensity's " +
                 "CellConfigDataSO at the matching variant (Cell picks by IntensityWise). Each " +
                 "config's PhaseThresholds must ride ITS OWN baseline - see ribcage_budget.py.")]
        [SerializeField, Range(1, MaxShells)] int shellCount = 1;

        // Rib/hoop counts of the OUTERMOST shell. Every shell inward multiplies both by
        // DensityStep, so these two numbers set the surface weave and DensityStep sets the ramp.
        const int BaseRibCount = 24;    // meridian great circles (pole to pole)
        const int BaseHoopCount = 11;   // latitude hoops (kept ODD - equator plus symmetric pairs)

        /// <summary>
        /// Per-shell density multiplier, applied to BOTH rib and hoop counts: shell k has
        /// round(Base * DensityStep^k). This is what makes the core the hard part - it compounds
        /// with the tightening that shrinking radius already provides, so the four shells go from
        /// ~94u cells at the surface to ~22u at the core. Turn this DOWN before turning shells
        /// down if the collider budget bites; it is the second-cheapest dial after shellCount.
        /// </summary>
        const float DensityStep = 1.05f;

        const float BarStep = 17f;      // arc-length spacing along every rib and hoop
        const float StrutStep = 26f;    // arc-length spacing along a triangulating diagonal
        const float StrutLength = 24f;  // long axis of a diagonal prism (chunkier than a bar)
        const float CrownLat = 84f;
        const int CrownCount = 18;
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
                                    DangerEveryNthRibPrism, TiltHash()));

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

        protected override void BuildEnvironment()
        {
            for (int shell = 0; shell < Shells; shell++)
            {
                var spec = new ShellSpec(shell);
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

        /// <summary>
        /// Meridian ribs: full great circles through the poles. The bar's LONG axis (+z) runs
        /// along the rib's own tangent so the bones read as continuous bone, not as beads.
        /// </summary>
        void BuildRibs(ShellSpec s)
        {
            int perRib = Mathf.RoundToInt(2f * Mathf.PI * s.Radius / BarStep);

            for (int rib = 0; rib < s.Ribs; rib++)
            {
                float lon = s.Longitude(rib);
                var dom = BoneDoms[rib % BoneDoms.Length];

                for (int i = 0; i < perRib; i++)
                {
                    // Deterministic, evenly-spread trap bars (see DangerEveryNthRibPrism). Indexed
                    // on the GLOBAL rib prism counter so the pattern walks around the cage instead
                    // of stacking the traps at the same latitude on every rib, plus a per-shell
                    // phase so it doesn't stack radially either.
                    bool danger = (s.Index * DangerShellPhase + rib * perRib + i)
                                  % DangerEveryNthRibPrism == 0;

                    // Sweep the full great circle: theta is the angle around it, so the same
                    // loop covers both hemispheres and both poles.
                    float theta = i * Mathf.PI * 2f / perRib;
                    var pos = s.ToCell(Shell(lon, theta, s.Radius));

                    // Tangent along the great circle = d/dtheta of Shell, in the shell's own frame.
                    var tangent = s.ToCell(new Vector3(
                        -Mathf.Sin(theta) * Mathf.Cos(lon),
                        Mathf.Cos(theta),
                        -Mathf.Sin(theta) * Mathf.Sin(lon)));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.6f, 3.6f, 16f)), dom,
                        danger ? PrismKind.Danger : PrismKind.Plain);
                }
            }
        }

        /// <summary>Latitude hoops - the bands that bind the ribs. Blue: neutral binding bone.</summary>
        void BuildHoops(ShellSpec s)
        {
            foreach (float latDeg in s.Lats)
            {
                float lat = latDeg * Mathf.Deg2Rad;
                float ringR = s.Radius * Mathf.Cos(lat);
                int n = Mathf.RoundToInt(2f * Mathf.PI * ringR / BarStep);

                for (int i = 0; i < n; i++)
                {
                    float lon = i * Mathf.PI * 2f / n + s.LonOffset;
                    var pos = s.ToCell(Shell(lon, lat, s.Radius));
                    var tangent = s.ToCell(new Vector3(-Mathf.Sin(lon), 0f, Mathf.Cos(lon)));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.6f, 3.6f, 16f)), Domains.Blue, PrismKind.Plain);
                }
            }
        }

        /// <summary>
        /// One diagonal through EVERY rib x hoop cell, which is what turns the weave's openings
        /// from quadrilaterals ("bubbles") into TRIANGLES - each quad is split into two. The
        /// diagonal's direction alternates on a checkerboard so the result zigzags like a truss
        /// instead of shearing uniformly one way.
        /// </summary>
        void BuildTriangulation(ShellSpec s)
        {
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
                    var from = s.ToCell(Shell(lonA, lean ? latLo : latHi, s.Radius));
                    var to = s.ToCell(Shell(lonB, lean ? latHi : latLo, s.Radius));
                    var along = to - from;

                    int n = Mathf.Max(1, Mathf.RoundToInt(along.magnitude / StrutStep));
                    for (int i = 0; i < n; i++)
                    {
                        float u = (i + 0.5f) / n;
                        // Push the strut back onto the shell - a straight chord would sag inside it.
                        var pos = Vector3.Lerp(from, to, u).normalized * s.Radius;
                        Emit(pos, SpawnPoint.LookRotation(along, pos.normalized),
                            Jit(new Vector3(2.4f, 2.4f, StrutLength)), Domains.Blue, PrismKind.Plain);
                    }
                }
            }
        }

        /// <summary>Chunky knuckles where a rib crosses a hoop - the cage's visual anchors.</summary>
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
                        Jit(new Vector3(5.4f, 5.4f, 5.4f)), dom, PrismKind.Plain);
                }
            }
        }

        /// <summary>
        /// Polar crowns: a tight ring just short of each pole, closing the cap where the ribs
        /// converge and would otherwise read as a spike of overlapping bone.
        /// </summary>
        void BuildCrowns(ShellSpec s)
        {
            for (int pole = 0; pole < 2; pole++)
            {
                float lat = (pole == 0 ? CrownLat : -CrownLat) * Mathf.Deg2Rad;

                for (int i = 0; i < CrownCount; i++)
                {
                    float lon = i * Mathf.PI * 2f / CrownCount + s.LonOffset;
                    var pos = s.ToCell(Shell(lon, lat, s.Radius));
                    var tangent = s.ToCell(new Vector3(-Mathf.Sin(lon), 0f, Mathf.Cos(lon)));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.2f, 3.2f, 12f)), BoneDoms[i % BoneDoms.Length],
                        PrismKind.Plain);
                }
            }
        }
    }
}
