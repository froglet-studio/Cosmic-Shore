using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Ribcage" - the cage cell environment and the arena of <see cref="GameModes.Ribcage"/>
    /// (player-facing name: "Peel the Cage"). A LAYERED ORANGE: one or more concentric hollow
    /// shells of prism bone, added INWARD from a fixed outer radius. Each shell is twenty-six
    /// meridian ribs running pole to pole, thirteen latitude hoops binding them, short diagonal
    /// struts weaving the bands into lattice, chunky joints at every rib x hoop crossing, and a
    /// crown closing each pole where the ribs converge.
    ///
    /// Three properties of this structure are load-bearing for the mode, not decoration:
    ///
    ///   • <see cref="shellCount"/> is the INTENSITY dial. The Cell picks one
    ///     <c>CellConfigDataSO</c> per intensity (<c>CellTypeChoiceOptions.IntensityWise</c>),
    ///     and each config points at a prefab variant of this component with a different shell
    ///     count - so intensity 1 is one rind to peel and intensity 4 is four. Shells are added
    ///     INWARD (<see cref="ShellRadius"/> never moves) so the AI's aim point, the player spawn
    ///     ring and the arena's outer silhouette are identical at every intensity.
    ///   • The weave is deliberately OPEN - a ~87u x ~82u grille opening at the outer shell. This
    ///     is a ribcage, not a prison wall: you fly between the bones freely, and the gaps are
    ///     what let you see the next rind waiting behind this one. Successive shells are rotated
    ///     by a fraction of a rib spacing (<see cref="ShellLonOffsets"/>) so the gaps never line
    ///     up radially - there is no free corridor straight through to the core.
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
    /// Baselines): 5,471 / 9,902 / 13,316 / 15,690 prisms at one through four shells. See
    /// RIBCAGE.md for the per-shell table and the collider-budget statement, and
    /// Tools/Build/ribcage_budget.py for the model.
    /// </summary>
    public class SpawnableRibcage : CellEnvironmentSpawnableBase
    {
        // Outermost shell. Density is where the prism budget goes, not radius: a bigger sphere
        // would just move the arena out. Deliberately NOT a dense weave - see the class summary.
        const float CageR = 360f;

        /// <summary>Radial spacing between rinds - shells land at 360 / 280 / 200 / 120.</summary>
        const float ShellGap = 80f;

        /// <summary>Ceiling on <see cref="shellCount"/>; one shell per intensity.</summary>
        public const int MaxShells = 4;

        /// <summary>
        /// The cage's OUTER shell radius, exposed so <c>RibcageController</c> can aim its AI
        /// cage-breakers at the bone without hard-coding a second copy of the number. Shells are
        /// added inward, so this is intensity-independent and the AI needs no per-intensity case.
        /// </summary>
        public const float ShellRadius = CageR;

        [Header("Ribcage")]
        [Tooltip("How many concentric rinds to build, from the outer shell inward. THE INTENSITY " +
                 "DIAL: author one prefab variant per shell count and point each intensity's " +
                 "CellConfigDataSO at the matching variant (Cell picks by IntensityWise). Each " +
                 "config's PhaseThresholds must ride ITS OWN baseline - see ribcage_budget.py.")]
        [SerializeField, Range(1, MaxShells)] int shellCount = 1;

        const int RibCount = 26;       // meridian great circles (pole to pole), per shell
        const float BarStep = 17f;     // arc-length spacing along every rib and hoop
        const int LatticeBands = 6;    // diagonal strut bands between adjacent ribs
        const int StrutPrisms = 3;
        const float CrownLat = 84f;
        const int CrownCount = 18;

        /// <summary>
        /// Longitude offset of each shell, as a FRACTION of one rib spacing. No two of the four
        /// shells share a phase, so the open grilles never line up radially and there is no free
        /// run from the outside to the core - you always have bone in front of you.
        /// </summary>
        static readonly float[] ShellLonOffsets = { 0f, 0.5f, 0.25f, 0.75f };

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

        // Latitude hoops, GENERATED rather than hand-listed so the count is one number to
        // turn and the Python budget model can mirror it exactly. Equator first, then
        // symmetric pairs out to +/-HoopSpanDeg.
        const int HoopCount = 13;
        const float HoopSpanDeg = 78f;

        static readonly float[] HoopLats = BuildHoopLats();

        static float[] BuildHoopLats()
        {
            int half = (HoopCount - 1) / 2;
            var lats = new float[1 + half * 2];
            lats[0] = 0f;
            for (int i = 1; i <= half; i++)
            {
                float lat = HoopSpanDeg * i / half;
                lats[i * 2 - 1] = lat;
                lats[i * 2] = -lat;
            }
            return lats;
        }

        // Structural triad - all three domains present, per the no-domain-asymmetry spirit.
        static readonly Domains[] BoneDoms = { Domains.Jade, Domains.Ruby, Domains.Gold };

        int Shells => Mathf.Clamp(shellCount, 1, MaxShells);

        static float ShellRadiusAt(int shell) => CageR - shell * ShellGap;

        protected override int DefaultSeed => 39;

        // Hashes the real generation parameters, not a bump-me constant: change the weave OR the
        // shell count and the SpawnableBase cache invalidates itself rather than serving a stale
        // point cloud. shellCount is in here because the four prefab variants share this script.
        protected override int BuildParameterHash() => System.HashCode.Combine(
            nameof(SpawnableRibcage), CageR, ShellGap, Shells,
            System.HashCode.Combine(RibCount, BarStep, HoopCount, HoopSpanDeg),
            System.HashCode.Combine(LatticeBands, StrutPrisms, CrownLat, CrownCount,
                                    DangerEveryNthRibPrism));

        // Pre-size for the worst case this variant can build. Shells shrink inward, so the outer
        // shell dominates; 6000 per shell clears the measured 5,471 with headroom and never
        // over-allocates by more than a shell's worth.
        protected override int LayCapacity => 6000 * Shells;

        protected override void BuildEnvironment()
        {
            for (int shell = 0; shell < Shells; shell++)
            {
                float r = ShellRadiusAt(shell);
                float lonOffset = ShellLonOffsets[shell % ShellLonOffsets.Length]
                                  * Mathf.PI * 2f / RibCount;

                BuildRibs(shell, r, lonOffset);
                BuildHoops(r, lonOffset);
                BuildLattice(r, lonOffset);
                BuildJoints(r, lonOffset);
                BuildCrowns(r, lonOffset);
            }
        }

        /// <summary>Point on a shell at longitude/latitude, in radians.</summary>
        static Vector3 Shell(float lonRad, float latRad, float radius) => new(
            radius * Mathf.Cos(latRad) * Mathf.Cos(lonRad),
            radius * Mathf.Sin(latRad),
            radius * Mathf.Cos(latRad) * Mathf.Sin(lonRad));

        static float RibLongitude(int rib, float lonOffset) =>
            rib * Mathf.PI * 2f / RibCount + lonOffset;

        /// <summary>
        /// Meridian ribs: full great circles through the poles. The bar's LONG axis (+z) runs
        /// along the rib's own tangent so the bones read as continuous bone, not as beads.
        /// </summary>
        void BuildRibs(int shell, float radius, float lonOffset)
        {
            int perRib = Mathf.RoundToInt(2f * Mathf.PI * radius / BarStep);

            for (int rib = 0; rib < RibCount; rib++)
            {
                float lon = RibLongitude(rib, lonOffset);
                var dom = BoneDoms[rib % BoneDoms.Length];

                for (int i = 0; i < perRib; i++)
                {
                    // Deterministic, evenly-spread trap bars (see DangerEveryNthRibPrism). Indexed
                    // on the GLOBAL rib prism counter so the pattern walks around the cage instead
                    // of stacking the traps at the same latitude on every rib, plus a per-shell
                    // phase so it doesn't stack radially either.
                    bool danger = (shell * DangerShellPhase + rib * perRib + i)
                                  % DangerEveryNthRibPrism == 0;

                    // Sweep the full great circle: theta is the angle around it, so the same
                    // loop covers both hemispheres and both poles.
                    float theta = i * Mathf.PI * 2f / perRib;
                    var pos = Shell(lon, theta, radius);

                    // Tangent along the great circle = d/dtheta of Shell.
                    var tangent = new Vector3(
                        -Mathf.Sin(theta) * Mathf.Cos(lon),
                        Mathf.Cos(theta),
                        -Mathf.Sin(theta) * Mathf.Sin(lon));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.6f, 3.6f, 16f)), dom,
                        danger ? PrismKind.Danger : PrismKind.Plain);
                }
            }
        }

        /// <summary>Latitude hoops - the bands that bind the ribs. Blue: neutral binding bone.</summary>
        void BuildHoops(float radius, float lonOffset)
        {
            foreach (float latDeg in HoopLats)
            {
                float lat = latDeg * Mathf.Deg2Rad;
                float ringR = radius * Mathf.Cos(lat);
                int n = Mathf.RoundToInt(2f * Mathf.PI * ringR / BarStep);

                for (int i = 0; i < n; i++)
                {
                    float lon = i * Mathf.PI * 2f / n + lonOffset;
                    var pos = Shell(lon, lat, radius);
                    var tangent = new Vector3(-Mathf.Sin(lon), 0f, Mathf.Cos(lon));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.6f, 3.6f, 16f)), Domains.Blue, PrismKind.Plain);
                }
            }
        }

        /// <summary>
        /// Diagonal struts woven between adjacent ribs across the equatorial bands - what turns
        /// hoops and ribs into something that reads as a woven cage rather than an armillary
        /// sphere. Alternating lean per band gives the weave.
        /// </summary>
        void BuildLattice(float radius, float lonOffset)
        {
            for (int rib = 0; rib < RibCount; rib++)
            {
                float lonA = RibLongitude(rib, lonOffset);
                float lonB = RibLongitude(rib + 1, lonOffset);

                for (int band = 0; band < LatticeBands; band++)
                {
                    // Bands walk the belt between the +52 and -52 hoops; the lean alternates so
                    // adjacent bands cross.
                    float t0 = (band + 0.15f) / LatticeBands;
                    float t1 = (band + 0.85f) / LatticeBands;
                    bool lean = ((rib + band) & 1) == 0;

                    float latStart = Mathf.Lerp(-52f, 52f, lean ? t0 : t1) * Mathf.Deg2Rad;
                    float latEnd = Mathf.Lerp(-52f, 52f, lean ? t1 : t0) * Mathf.Deg2Rad;

                    var from = Shell(lonA, latStart, radius);
                    var to = Shell(lonB, latEnd, radius);
                    var along = to - from;

                    for (int s = 0; s < StrutPrisms; s++)
                    {
                        float u = (s + 0.5f) / StrutPrisms;
                        // Push the strut back onto the shell - a straight chord would sag inside it.
                        var pos = Vector3.Lerp(from, to, u).normalized * radius;
                        Emit(pos, SpawnPoint.LookRotation(along, pos.normalized),
                            Jit(new Vector3(2.4f, 2.4f, 11f)), Domains.Blue, PrismKind.Plain);
                    }
                }
            }
        }

        /// <summary>Chunky knuckles where a rib crosses a hoop - the cage's visual anchors.</summary>
        void BuildJoints(float radius, float lonOffset)
        {
            for (int rib = 0; rib < RibCount; rib++)
            {
                float lon = RibLongitude(rib, lonOffset);
                var dom = BoneDoms[rib % BoneDoms.Length];

                foreach (float latDeg in HoopLats)
                {
                    var pos = Shell(lon, latDeg * Mathf.Deg2Rad, radius);
                    Emit(pos, SpawnPoint.LookRotation(pos.normalized, Vector3.up),
                        Jit(new Vector3(5.4f, 5.4f, 5.4f)), dom, PrismKind.Plain);
                }
            }
        }

        /// <summary>
        /// Polar crowns: a tight ring just short of each pole, closing the cap where the ribs
        /// converge and would otherwise read as a spike of overlapping bone.
        /// </summary>
        void BuildCrowns(float radius, float lonOffset)
        {
            for (int pole = 0; pole < 2; pole++)
            {
                float lat = (pole == 0 ? CrownLat : -CrownLat) * Mathf.Deg2Rad;

                for (int i = 0; i < CrownCount; i++)
                {
                    float lon = i * Mathf.PI * 2f / CrownCount + lonOffset;
                    var pos = Shell(lon, lat, radius);
                    var tangent = new Vector3(-Mathf.Sin(lon), 0f, Mathf.Cos(lon));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.2f, 3.2f, 12f)), BoneDoms[i % BoneDoms.Length],
                        PrismKind.Plain);
                }
            }
        }
    }
}
