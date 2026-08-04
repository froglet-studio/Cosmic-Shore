using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Ribcage" - the cage cell environment (~10.2k prisms) and the arena of
    /// <see cref="GameModes.Ribcage"/>: a hollow sphere of SHIELDED prism bone that pens the
    /// cell's brood. Forty-eight meridian ribs run pole to pole, twenty-one latitude hoops
    /// bind them, short diagonal struts weave the bands into lattice, chunky joints sit at every
    /// rib x hoop crossing, and a crown closes each pole where the ribs converge. The interior
    /// is deliberately empty of STRUCTURE - it is where the brood lives and where the
    /// fighting happens.
    ///
    /// Two properties of this structure are load-bearing for the mode, not decoration:
    ///
    ///   • Every bar is <see cref="PrismKind.Shielded"/> except the sparse
    ///     <see cref="PrismKind.Danger"/> traps. Shielded mass is not food for any
    ///     herbivore (Docs/ECOSYSTEM.md §16.2) and - since the matching grid change - not a
    ///     fauna steering target either, so the cage can only ever be broken by a VESSEL. The
    ///     race can neither be eaten out from under the players nor stall with a swarm parked
    ///     on bars it cannot chew. It also makes each bar a TWO-hit prism (the first hit sheds
    ///     the shield, the second shatters it) unless the hit devastates - which is the mode's
    ///     whole skill surface.
    ///   • NOTHING is <see cref="PrismKind.SuperShielded"/>. A super-shielded prism is fully
    ///     invulnerable (<c>Prism.Damage</c> returns early), so a single one in the cage would
    ///     be permanently unbreakable mass - and enough of them could put the destruction
    ///     target out of reach. Do not "upgrade" the bars.
    ///
    /// Painted across the full domain triad so the cage reads as contested neutral bone rather
    /// than any one team's property; the paint is cosmetic to scoring (StatsManager classifies
    /// every non-roster-owned prism hostile, so cage mass scores for whoever breaks it, in any
    /// colour). Deterministic per seed like every cell environment - clients build locally with
    /// no seed sync.
    ///
    /// Budget (analytic, confirm with FrogletTools > Ecology > Measure Cell Environment
    /// Baselines): 10,229 prisms / ~4,042,133 volume, of which 336 are DANGER bars. That is the
    /// same order as Rampage's deliberate 10,000-prism arena gate - see RIBCAGE.md for the
    /// per-structure table, the collider-budget statement, and
    /// Tools/Build/ribcage_budget.py for the model.
    /// </summary>
    public class SpawnableRibcage : CellEnvironmentSpawnableBase
    {
        // Cage radius. With 48 ribs and 21 hoops the grille opening is ~47u x ~49u - a near
        // SQUARE weave rather than the wide stripes 16 ribs gave (141u x 61u). Density is where
        // the prism budget goes, not radius: a bigger sphere would just move the arena out,
        // while a tighter weave is what makes the thing read as a cage. You can still fly
        // through an opening - the goal is to smash the structure, never to be locked inside.
        const float CageR = 360f;

        /// <summary>
        /// The cage's shell radius, exposed so <c>RibcageController</c> can aim its AI
        /// cage-breakers at the bone without hard-coding a second copy of the number. The AI
        /// cannot find the cage through the density grids the way Rampage's mass hunters do -
        /// shielded mass is deliberately absent from them (see Cell.AddBlock) - so it rams a
        /// point ON this shell instead.
        /// </summary>
        public const float ShellRadius = CageR;

        /// <summary>
        /// Radius the mode pens its brood inside - deliberately a little SMALLER than the shell
        /// (<see cref="ShellRadius"/>) so the cage's own bars sit OUTSIDE the pen. That is what
        /// keeps the danger bars below from being fauna food: shielded bars are already excluded
        /// from every herbivore diet, but a DANGER bar is unshielded and would otherwise be
        /// edible, letting the penned brood quietly eat the arena (and feed itself without an
        /// intruder ever showing up).
        /// </summary>
        public const float ContainmentRadius = CageR * 0.94f;

        const int RibCount = 48;       // meridian great circles (pole to pole)
        const float BarStep = 17f;     // arc-length spacing along every rib and hoop
        const int LatticeBands = 6;    // diagonal strut bands between adjacent ribs
        const int StrutPrisms = 3;
        const float CrownLat = 84f;
        const int CrownCount = 18;

        /// <summary>
        /// Every Nth rib prism is laid as a DANGER bar instead of a shielded one - the arena's
        /// trap. A danger prism is NOT a tougher bar: danger is mutually exclusive with both
        /// shield tiers (<c>PrismStateManager.MakeDangerous</c> clears them), so these are the
        /// SOFTEST bars in the cage - one hit and they shatter, no shield to shed. What they cost
        /// you is contact: the standard danger-prism punishment (volume-independent full-stop
        /// slow, a 4s all-element debuff, boost reset). So the bar that breaks fastest is the one
        /// that hurts, and "just ram everything" stops being the answer. They are also the only
        /// cage prisms fauna could eat, which is exactly why the pen radius above excludes the
        /// shell.
        /// </summary>
        const int DangerEveryNthRibPrism = 19;

        // Latitude hoops, GENERATED rather than hand-listed so the count is one number to
        // turn and the Python budget model can mirror it exactly. Equator first, then
        // symmetric pairs out to +/-HoopSpanDeg.
        const int HoopCount = 21;
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

        protected override int DefaultSeed => 39;
        // Hashes the real generation parameters, not a bump-me constant: change the weave and
        // the SpawnableBase cache invalidates itself rather than serving a stale point cloud.
        protected override int BuildParameterHash() => System.HashCode.Combine(
            nameof(SpawnableRibcage), CageR, RibCount, BarStep,
            System.HashCode.Combine(HoopCount, HoopSpanDeg, LatticeBands, StrutPrisms),
            System.HashCode.Combine(CrownLat, CrownCount, DangerEveryNthRibPrism));
        protected override int LayCapacity => 11000;

        protected override void BuildEnvironment()
        {
            BuildRibs();
            BuildHoops();
            BuildLattice();
            BuildJoints();
            BuildCrowns();
        }

        /// <summary>Point on the cage shell at longitude/latitude, in radians.</summary>
        static Vector3 Shell(float lonRad, float latRad, float radius) => new(
            radius * Mathf.Cos(latRad) * Mathf.Cos(lonRad),
            radius * Mathf.Sin(latRad),
            radius * Mathf.Cos(latRad) * Mathf.Sin(lonRad));

        static float RibLongitude(int rib) => rib * Mathf.PI * 2f / RibCount;

        /// <summary>
        /// Meridian ribs: full great circles through the poles. The bar's LONG axis (+z) runs
        /// along the rib's own tangent so the bones read as continuous bone, not as beads.
        /// </summary>
        void BuildRibs()
        {
            int perRib = Mathf.RoundToInt(2f * Mathf.PI * CageR / BarStep);

            for (int rib = 0; rib < RibCount; rib++)
            {
                float lon = RibLongitude(rib);
                var dom = BoneDoms[rib % BoneDoms.Length];

                for (int i = 0; i < perRib; i++)
                {
                    // Deterministic, evenly-spread trap bars (see DangerEveryNthRibPrism). Indexed
                    // on the GLOBAL rib prism counter so the pattern walks around the cage instead
                    // of stacking the traps at the same latitude on every rib.
                    bool danger = (rib * perRib + i) % DangerEveryNthRibPrism == 0;

                    // Sweep the full great circle: theta is the angle around it, so the same
                    // loop covers both hemispheres and both poles.
                    float theta = i * Mathf.PI * 2f / perRib;
                    var pos = Shell(lon, theta, CageR);

                    // Tangent along the great circle = d/dtheta of Shell.
                    var tangent = new Vector3(
                        -Mathf.Sin(theta) * Mathf.Cos(lon),
                        Mathf.Cos(theta),
                        -Mathf.Sin(theta) * Mathf.Sin(lon));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.6f, 3.6f, 16f)), dom,
                        danger ? PrismKind.Danger : PrismKind.Shielded);
                }
            }
        }

        /// <summary>Latitude hoops - the bands that bind the ribs. Blue: neutral binding bone.</summary>
        void BuildHoops()
        {
            foreach (float latDeg in HoopLats)
            {
                float lat = latDeg * Mathf.Deg2Rad;
                float ringR = CageR * Mathf.Cos(lat);
                int n = Mathf.RoundToInt(2f * Mathf.PI * ringR / BarStep);

                for (int i = 0; i < n; i++)
                {
                    float lon = i * Mathf.PI * 2f / n;
                    var pos = Shell(lon, lat, CageR);
                    var tangent = new Vector3(-Mathf.Sin(lon), 0f, Mathf.Cos(lon));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.6f, 3.6f, 16f)), Domains.Blue, PrismKind.Shielded);
                }
            }
        }

        /// <summary>
        /// Diagonal struts woven between adjacent ribs across the equatorial bands - what turns
        /// seven hoops and sixteen ribs into something that reads as a woven cage rather than an
        /// armillary sphere. Alternating lean per band gives the weave.
        /// </summary>
        void BuildLattice()
        {
            for (int rib = 0; rib < RibCount; rib++)
            {
                float lonA = RibLongitude(rib);
                float lonB = RibLongitude(rib + 1);

                for (int band = 0; band < LatticeBands; band++)
                {
                    // Bands walk the belt between the +52 and -52 hoops; the lean alternates so
                    // adjacent bands cross.
                    float t0 = (band + 0.15f) / LatticeBands;
                    float t1 = (band + 0.85f) / LatticeBands;
                    bool lean = ((rib + band) & 1) == 0;

                    float latStart = Mathf.Lerp(-52f, 52f, lean ? t0 : t1) * Mathf.Deg2Rad;
                    float latEnd = Mathf.Lerp(-52f, 52f, lean ? t1 : t0) * Mathf.Deg2Rad;

                    var from = Shell(lonA, latStart, CageR);
                    var to = Shell(lonB, latEnd, CageR);
                    var along = to - from;

                    for (int s = 0; s < StrutPrisms; s++)
                    {
                        float u = (s + 0.5f) / StrutPrisms;
                        // Push the strut back onto the shell - a straight chord would sag inside it.
                        var pos = Vector3.Lerp(from, to, u).normalized * CageR;
                        Emit(pos, SpawnPoint.LookRotation(along, pos.normalized),
                            Jit(new Vector3(2.4f, 2.4f, 11f)), Domains.Blue, PrismKind.Shielded);
                    }
                }
            }
        }

        /// <summary>Chunky knuckles where a rib crosses a hoop - the cage's visual anchors.</summary>
        void BuildJoints()
        {
            for (int rib = 0; rib < RibCount; rib++)
            {
                float lon = RibLongitude(rib);
                var dom = BoneDoms[rib % BoneDoms.Length];

                foreach (float latDeg in HoopLats)
                {
                    var pos = Shell(lon, latDeg * Mathf.Deg2Rad, CageR);
                    Emit(pos, SpawnPoint.LookRotation(pos.normalized, Vector3.up),
                        Jit(new Vector3(5.4f, 5.4f, 5.4f)), dom, PrismKind.Shielded);
                }
            }
        }

        /// <summary>
        /// Polar crowns: a tight ring just short of each pole, closing the cap where the sixteen
        /// ribs converge and would otherwise read as a spike of overlapping bone.
        /// </summary>
        void BuildCrowns()
        {
            for (int pole = 0; pole < 2; pole++)
            {
                float lat = (pole == 0 ? CrownLat : -CrownLat) * Mathf.Deg2Rad;

                for (int i = 0; i < CrownCount; i++)
                {
                    float lon = i * Mathf.PI * 2f / CrownCount;
                    var pos = Shell(lon, lat, CageR);
                    var tangent = new Vector3(-Mathf.Sin(lon), 0f, Mathf.Cos(lon));

                    Emit(pos, SpawnPoint.LookRotation(tangent, pos.normalized),
                        Jit(new Vector3(3.2f, 3.2f, 12f)), BoneDoms[i % BoneDoms.Length],
                        PrismKind.Shielded);
                }
            }
        }
    }
}
