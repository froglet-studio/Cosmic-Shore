using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Garland" - the FAR-CAMERA cell: a flowering bough wound twice around the seed of the
    /// world, cut to ~4,250 prisms so it loads in a breath and reads as one composition from the
    /// menu camera's orbit rather than as a wall of detail nobody is close enough to see.
    ///
    /// <para>It is the Ourobor/Yggdra hybrid at a fifth of their weight, and the budget is the
    /// design. The freestyle seven each spend 34-41k prisms building places you FLY THROUGH; this
    /// one is composed for the one shot the home screen actually shows - <c>MenuCam_LavaLamp1</c>
    /// orbiting the cell centre at <see cref="CamR"/> with the autopilot vessel drifting through
    /// it. At that distance a nominal 2.5-unit prism is a pixel, so the whole file follows one
    /// rule: <b>spend prisms on LENGTH and SILHOUETTE, never on surface</b>. Every family is a
    /// curve laid ONE prism per step, which buys a continuous, readable line for a twentieth of
    /// what filling the same shape as a sheet costs.</para>
    ///
    /// <para><b>Nothing here clips anything else</b>, and that is a property of the generator
    /// rather than a coincidence of its constants. Two mechanisms hold it, and they are different
    /// because the families are. Most of the cell clears itself by a clearance it can STATE: a
    /// chain fills <see cref="ChainFill"/> of its own step (not 1, which welds the chain shut and
    /// makes every consecutive pair interpenetrate - what this cell used to do at 4,372 clipping
    /// pairs); a blossom is concentric RINGS whose pitch is checked against the petal's length and
    /// whose count is checked against its width; every family attaches to the bough at its own
    /// PHASE, because two structures sharing a knot sample share a point in space. The SHORE
    /// cannot: the falls cross the whole cell, the patches land wherever a root came down, and the
    /// bands ring the seed through those landfalls. So the shore is laid LAST and it YIELDS - a
    /// prism that would land inside something already laid is simply not laid. Measured over the
    /// whole cloud with a 15-axis separating-axis test, it costs FIVE prisms.</para>
    ///
    /// <para><b>Three depth layers</b>, because depth is what a distant static camera has instead
    /// of detail: the SHORE (bands + landfall patches hugging the nucleus, so the biggest object
    /// in frame reads as a world instead of a bare marker), the BOUGH and its VINE (the two
    /// counter-wound torus knots that are the subject, in the band the camera sits inside), and
    /// the CROWN thinning outward to the membrane, so the composition has a far edge.
    /// The FALLS spiral from the bough down onto the shore and are what ties the layers into one
    /// object rather than three shells.</para>
    ///
    /// <para><b>Zero danger, deliberately.</b> Yggdra's thorns are the environment being real for
    /// a pilot who chose to fly it; this cell's normal state is an AI flying the player's vessel
    /// behind a menu, where a danger prism reads as the ship being jerked about for no reason the
    /// player can see. Geode and Ourobor already hold that pole (Docs/ECOSYSTEM.md §18).</para>
    ///
    /// <para><b>Determinism without the shared stream.</b> Nothing here draws from
    /// <c>_r</c>/<c>RangeF</c>/<c>Jit</c> or from value noise - every wobble is
    /// <see cref="CellEnvironmentSpawnableBase.Hash01"/> of the emitting index. That is not
    /// fastidiousness: it makes the whole world a closed form, so the offline model in
    /// <c>Tools/Build/author_garland_cell.py</c> reproduces the count, the volume and the minimum
    /// radius EXACTLY rather than estimating them, and the PhaseThresholds this cell ships with
    /// are measured rather than guessed (the /ecology skill §7).</para>
    ///
    /// <para>Nothing is laid inside <see cref="NucleusR"/> - Docs/ECOSYSTEM.md §13/§18.1: the
    /// nucleus interior IS the territorial claim, so an authored environment sitting in it hands
    /// node control to whatever colour it happens to favour before anyone has flown a metre. The
    /// assertion is on each prism's NEAREST CORNER, not its lay point.</para>
    /// </summary>
    public class SpawnableGarland : CellEnvironmentSpawnableBase
    {
        // ── The cell this world is composed against (all distances from the cell centre) ──
        //
        // Load-bearing constants, not comments: every family is derived from them, so moving the
        // nucleus or the membrane moves the composition instead of silently breaking its clearance.
        /// <summary>Nucleus.prefab localScale 400 x the Node mesh's ~0.98u radius - the node-control
        /// radius Cell.RefreshNucleusControlRadius derives from the renderer bounds.</summary>
        public const float NucleusR = 392f;
        /// <summary>CapsuleMembrane.prefab radius - the playfield boundary.</summary>
        public const float MembraneR = 1200f;
        /// <summary>MenuCam_LavaLamp1's orbit radius. The camera sits INSIDE the bough band, which
        /// is why the bough is the subject and the crown is the backdrop.</summary>
        public const float CamR = 686f;

        /// <summary>Clearance every family keeps between its nearest prism corner and the
        /// node-control radius. Not a safety fudge - it is the room a grazing herbivore and a
        /// drifting vessel need to pass between the world and the seed without clipping either.</summary>
        const float NucleusMargin = 16f;

        // ── Chains ──
        //
        // Every long family is one prism per step, and the LENGTH of that prism is derived from
        // the gap it has to fill rather than authored: the falls' step is a third of the bough's
        // and the crown's grows as the branch climbs, so one authored length cannot serve them.
        // ChainFill is that fraction, and it is under 1 by enough to absorb ChainJit AND the
        // corner a bend puts on the inside of a joint.
        const float ChainFill = 0.82f;
        const float ChainJit = 0.08f;

        // ── Yielding ──
        //
        // YieldCell must exceed twice the largest prism's bounding radius, or the 27-cell
        // neighbourhood below stops covering every pair that could touch (asserted offline - the
        // measured worst is 14.25). YieldGap is the clearance a yielding prism has to leave, and
        // it is deliberately not zero: a fit that clears by a hair re-reads as clipping the moment
        // anything moves, and a decision taken ON the boundary is one this model (float64) and the
        // engine (float32) can disagree about.
        const float YieldCell = 56f;
        const float YieldGap = 0.75f;

        // ── The seed's crust ──
        const float ShoreR = 436f;
        const float ShoreBandStep = 14f;
        const int ShoreBands = 2;
        const float ShoreBandLift = 15f;
        static readonly Vector2 ShoreBandSection = new(22f, 3f);
        const int ShorePatchPlates = 14;
        const float ShorePatchR = 42f;
        static readonly Vector3 ShorePatchPlate = new(10f, 3f, 10f);

        // ── The bough: a (2,3) torus knot. Radius runs Major-Minor .. Major+Minor = 530 .. 870,
        // centred on the camera orbit so the near pass looms and the far pass is the backdrop. ──
        //
        // THE INNER RADIUS IS SET BY THE BLOSSOMS, NOT BY THE WOOD. A blossom sitting at the
        // knot's closest approach reaches its outer ring further in, and its petals are this
        // cell's nearest prisms to the nucleus - so the clearance is an INEQUALITY on the knot's
        // parameters rather than a property of where the five blossoms happened to land
        // (Docs/ECOSYSTEM.md §18.1: the nucleus radius is load-bearing, not a comment). Asserted
        // over the emitted point cloud by Tools/Build/author_garland_cell.py, on each prism's
        // NEAREST CORNER:
        //     Major - Minor  >=  NucleusR + outer ring + petal half-diagonal + NucleusMargin
        const int BoughP = 2, BoughQ = 3;
        const float BoughMajor = 700f, BoughMinor = 170f;
        const float BoughStep = 22f;
        static readonly Vector2 BoughSection = new(16f, 7f);

        // ── The vine: a (3,2) knot counter-wound INSIDE the bough (480 .. 650), thinner. Its
        // INNER radius is set by its own blossoms by the same inequality. ──
        const int VineP = 3, VineQ = 2;
        const float VineMajor = 565f, VineMinor = 85f;
        const float VineStep = 26f;
        static readonly Vector2 VineSection = new(8f, 4f);

        // ── Blossoms: CONCENTRIC RINGS, not a golden-angle head ──
        //
        // A sunflower head packs its florets at a constant AREAL density, which is exactly what a
        // head of non-overlapping petals cannot be: at this cell's petal size the head's own area
        // runs out long before the count does. Rings state the two clearances separately - the
        // ring pitch against the petal's LENGTH, the ring count against its WIDTH - so each one is
        // a bound that can be written down and checked.
        const int Blossoms = 5, VineBlossoms = 3;
        static readonly int[] BlossomRings = { 11, 21, 30, 41, 48 };
        static readonly float[] BlossomRingRadii = { 30f, 47f, 64f, 81f, 98f };
        static readonly Vector3 BlossomPetal = new(8f, 2.6f, 13f);
        const int BlossomBosses = 9;
        const float BlossomBossRadius = 16f, BlossomBossSize = 6f;
        static readonly int[] VineBlossomRings = { 11, 17, 23, 27 };
        static readonly float[] VineBlossomRingRadii = { 18f, 30f, 42f, 54f };
        static readonly Vector3 VinePetal = new(6f, 2.2f, 9f);
        const int VineBlossomBosses = 7;
        const float VineBossRadius = 9f, VineBossSize = 5f;

        // ── Skirts: ROWS along the bough, not one wide fan ──
        const int Skirts = 17, SkirtRows = 6, SkirtLeaves = 3;
        static readonly Vector3 SkirtLeaf = new(6f, 2f, 15f);
        const float SkirtStandoff = 11f, SkirtRowPitch = 5f, SkirtFan = 0.78f;

        // ── Falls, crown, terraces ──
        const int Falls = 8, FallSteps = 82, FallSkip = 2;
        static readonly Vector2 FallSection = new(4.5f, 4.5f);
        const float FallStandoff = 10f;
        // A chain that starts at its parent's own lay point starts INSIDE it, and no length makes
        // that pair clear - so both start displaced. They are displaced DIFFERENTLY, and that
        // asymmetry is a measurement rather than a preference. A crown climbs OUT of the bough's
        // band and never returns, so lifting it radially puts the wood behind it for the whole run
        // (a sideways lift does not: the drift term swings the chain back across the bough within
        // two steps - 7.3 units apart at step 2, from a start 31 units clear). A fall does the
        // opposite: it spends three quarters of its length inside the band the bough wanders
        // through, so dropping it radially lays it directly under a curve that comes back down to
        // meet it (44 clipping pairs, against 7 for the same fall pushed out the bough's SIDE,
        // where it leaves the knot's own osculating plane at once).
        const float FallRootOffset = 18f, CrownRootOffset = 22f;
        const float FallEase = 1.4f;
        const int Crowns = 8, CrownSteps = 41, CrownSkip = 1;
        static readonly Vector2 CrownSection = new(7f, 7f);
        const int CrownTuftLeaves = 28;
        static readonly Vector3 CrownTuftLeaf = new(5f, 2.2f, 12f);
        const float CrownTuftRadius = 24f, CrownTuftCone = 1.309f;
        const int Terraces = 3;
        // Terraces ride the MIDPOINT of a blossom gap rather than their own stride: 3 and 5 beat
        // against each other on a closed loop, and a terrace 14 samples from a blossom is a deck
        // inside a flower.
        static readonly int[] TerraceGaps = { 0, 2, 4 };
        static readonly int[] TerraceRings = { 10, 20, 30, 40 };
        static readonly float[] TerraceRingRadii = { 22f, 39f, 56f, 73f };
        static readonly Vector3 TerracePlate = new(9f, 3f, 9f);
        const float TerraceLift = 20f, TerraceKeyLift = 8f, TerraceKey = 11f;
        const int TerraceMasts = 3, TerraceMastSegments = 15;
        static readonly Vector2 TerraceMast = new(5.5f, 5.5f);
        const float TerraceMastRadius = 88f, TerraceMastPitch = 8f;

        // Where each family attaches to the bough. Distinct offsets, because two families sharing
        // a knot sample means two structures sharing a point in space, and no per-family clearance
        // can see that.
        const int BlossomPhase = 0, SkirtPhase = 7, FallPhase = 28, CrownPhase = 11;
        const int VineBlossomPhase = 16;

        /// <summary>The knot axis - a general direction, so neither knot is ever edge-on or
        /// face-on to the whole of the camera's orbit (which sweeps about (1,1,0)).</summary>
        static readonly Vector3 KnotAxis = new(0.36f, 0.88f, 0.31f);

        protected override int DefaultSeed => 73;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableGarland), 2);
        protected override int LayCapacity => 6000;

        // Every knot sample the two knots produced, kept so the families that RIDE a knot
        // (blossoms, falls, crowns, terraces, skirts) index the same points the bough was laid
        // from - a second evaluation would drift and hang the furniture off the wood.
        List<KnotSample> _bough, _vine;

        protected override void BuildEnvironment()
        {
            _placed.Clear();
            _yieldGrid.Clear();
            _bough = SampleKnot(BoughP, BoughQ, BoughMajor, BoughMinor, BoughStep);
            _vine = SampleKnot(VineP, VineQ, VineMajor, VineMinor, VineStep);

            BuildBough();
            BuildVine();
            BuildBlossoms();
            BuildSkirts();
            BuildCrown();
            BuildTerraces();
            // The shore is laid LAST, and it is the part of the cell that yields. Everything above
            // clears by a clearance it states itself; these three meet whatever the rest of the
            // world put in their way, so they give ground instead of being tuned around it.
            BuildFalls();
            BuildShoreBands();

            // The scratch dies with the build. It is ~350 KB on this cell and this is the world
            // the home screen BOOTS into, so it would otherwise be held for as long as the player
            // sits in the menu - which is most of the time. Clearing at the top of the next build
            // is not the same thing: there may not be a next build.
            ReleaseBuildScratch();
        }

        /// <summary>Drop the yield grid and the knot samples. Build-time only - nothing reads
        /// them after <see cref="BuildEnvironment"/> returns, and a re-entry rebuilds both.</summary>
        void ReleaseBuildScratch()
        {
            _placed.Clear();
            _placed.TrimExcess();
            _yieldGrid.Clear();
            _bough = null;
            _vine = null;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Knot sampling
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>One point on a knot: where it is, which way it runs, and which way is "out"
        /// (away from the torus's own centre circle - the surface normal a rider stands on).</summary>
        public readonly struct KnotSample
        {
            public readonly Vector3 Pos, Tangent, Outward;
            public KnotSample(Vector3 pos, Vector3 tangent, Vector3 outward)
            { Pos = pos; Tangent = tangent; Outward = outward; }
        }

        /// <summary>
        /// Sample a (p,q) torus knot at EVEN ARC SPACING. The count is
        /// <c>floor(totalLength / step + 0.5)</c> from a fixed 4096-substep integration rather
        /// than an accumulate-and-emit walk: a walk's count depends on where float error lands
        /// relative to the final step, which is exactly the kind of thing that makes an offline
        /// model disagree with the engine by one prism and no obvious reason.
        /// (<c>Mathf.RoundToInt</c> is banker's rounding - the /ecology skill's own warning.)
        /// </summary>
        List<KnotSample> SampleKnot(int p, int q, float major, float minor, float step)
        {
            const int Sub = 4096;
            var arc = new float[Sub + 1];
            arc[0] = 0f;
            Vector3 prev = KnotPoint(0f, p, q, major, minor);
            for (int i = 1; i <= Sub; i++)
            {
                Vector3 cur = KnotPoint(2f * Mathf.PI * i / Sub, p, q, major, minor);
                arc[i] = arc[i - 1] + (cur - prev).magnitude;
                prev = cur;
            }

            float total = arc[Sub];
            int n = Mathf.Max(8, Mathf.FloorToInt(total / step + 0.5f));

            var samples = new List<KnotSample>(n);
            int cursor = 0;
            for (int k = 0; k < n; k++)
            {
                float want = total * k / n;
                while (cursor < Sub && arc[cursor + 1] < want) cursor++;
                float span = arc[cursor + 1] - arc[cursor];
                float f = span > 1e-6f ? (want - arc[cursor]) / span : 0f;
                float t = 2f * Mathf.PI * (cursor + f) / Sub;

                Vector3 pos = KnotPoint(t, p, q, major, minor);
                // Central difference on the parameter: a closed form derivative buys nothing here
                // and would have to be re-derived per (p,q), where this is one expression.
                const float H = 1e-3f;
                Vector3 tangent = (KnotPoint(t + H, p, q, major, minor) - KnotPoint(t - H, p, q, major, minor)).normalized;
                // "Out" is away from the torus's CENTRE CIRCLE - the point on the major circle
                // directly under this sample - so it is the surface normal, not the radial from
                // the cell centre (which is what a rider on the far side would call down).
                Vector3 hub = HubPoint(t, p, major);
                Vector3 outward = (pos - hub).normalized;
                samples.Add(new KnotSample(pos, tangent, outward));
            }
            return samples;
        }

        static Vector3 KnotPoint(float t, int p, int q, float major, float minor)
        {
            float r = major + minor * Mathf.Cos(q * t);
            return ToWorld(r * Mathf.Cos(p * t), r * Mathf.Sin(p * t), minor * Mathf.Sin(q * t));
        }

        /// <summary>The point on the torus's centre circle beneath parameter t.</summary>
        static Vector3 HubPoint(float t, int p, float major) =>
            ToWorld(major * Mathf.Cos(p * t), major * Mathf.Sin(p * t), 0f);

        /// <summary>Canonical knot space (torus axis = +z) into the cell, through one fixed basis
        /// derived from <see cref="KnotAxis"/> - literal vectors so the offline model reproduces it
        /// without depending on Unity's Euler ordering.</summary>
        static Vector3 ToWorld(float x, float y, float z)
        {
            Vector3 w = KnotAxis.normalized;
            Vector3 u = Vector3.Cross(w, Vector3.forward).normalized;
            Vector3 v = Vector3.Cross(w, u);
            return u * x + v * y + w * z;
        }

        /// <summary>The chord from sample i to the next one - what a chain prism has to span.</summary>
        static float Chord(List<KnotSample> knot, int i) =>
            (knot[(i + 1) % knot.Count].Pos - knot[i].Pos).magnitude;

        // ──────────────────────────────────────────────────────────────────────────────
        // Families
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The bough: one chunky prism per arc step, long axis down the tangent and wide axis
        /// across the surface. Every ninth prism is a gold growth ring - the only decoration the
        /// wood gets, because a second colour is legible at 700 units where a second SHAPE is not.
        /// </summary>
        void BuildBough()
        {
            for (int i = 0; i < _bough.Count; i++)
            {
                var s = _bough[i];
                Lay(s.Pos, SpawnPoint.LookRotation(s.Tangent, s.Outward),
                    ChainScale(BoughSection, Chord(_bough, i), i * 13 + 5, ChainJit),
                    i % 9 == 0 ? Domains.Gold : Domains.Jade);
            }
        }

        /// <summary>
        /// The vine: the counter-wound (3,2) knot inside the bough. Half the girth and a longer
        /// step, so it reads as a younger runner rather than a second trunk - and because it sits
        /// at 480..650 it is the one structure the camera looks ACROSS at the nucleus through.
        /// </summary>
        void BuildVine()
        {
            for (int i = 0; i < _vine.Count; i++)
            {
                var s = _vine[i];
                Lay(s.Pos, SpawnPoint.LookRotation(s.Tangent, s.Outward),
                    ChainScale(VineSection, Chord(_vine, i), i * 7 + 19, ChainJit),
                    i % 11 == 0 ? Domains.Gold : Domains.Jade);
            }
        }

        /// <summary>
        /// Blossoms: ringed discs facing along the bough's own tangent, so the camera sees them as
        /// full faces on one side of the knot and as edges on the other - the single cheapest way
        /// to get a distant composition to change while the camera crawls.
        /// The boss ring is the cell's landmark tier: super-shielded prisms at the hub, set far
        /// enough out to clear the wood the blossom is sitting on.
        /// </summary>
        void BuildBlossoms()
        {
            LayBlossoms(_bough, Blossoms, BlossomRings, BlossomRingRadii, BlossomPetal,
                BlossomBosses, BlossomBossRadius, BlossomBossSize, 1000, BlossomPhase);
            LayBlossoms(_vine, VineBlossoms, VineBlossomRings, VineBlossomRingRadii, VinePetal,
                VineBlossomBosses, VineBossRadius, VineBossSize, 2000, VineBlossomPhase);
        }

        void LayBlossoms(List<KnotSample> knot, int count, int[] rings, float[] radii,
            Vector3 petal, int bosses, float bossR, float bossS, int saltBase, int phase)
        {
            float outer = radii[radii.Length - 1];
            for (int b = 0; b < count; b++)
            {
                var s = knot[(b * knot.Count / count + phase) % knot.Count];
                Vector3 axis = s.Tangent;
                Vector3 uAxis = s.Outward;
                Vector3 vAxis = Vector3.Cross(axis, uAxis);

                int salt = saltBase;
                for (int r = 0; r < rings.Length; r++)
                {
                    for (int i = 0; i < rings[r]; i++)
                    {
                        salt++;
                        float a = 2f * Mathf.PI * i / rings[r] + r * 0.37f;
                        Vector3 outward = uAxis * Mathf.Cos(a) + vAxis * Mathf.Sin(a);
                        Vector3 scale = HJit(petal, salt + b * 131, 0.14f);
                        // Cupped: petals lift toward the blossom's face as they go out.
                        Vector3 dir = (outward + axis * (0.34f * radii[r] / outer)).normalized;
                        Lay(s.Pos + dir * radii[r], SpawnPoint.LookRotation(dir, axis), scale,
                            Hash01((salt + b * 131) * 3) < 0.16f ? Domains.Ruby : Domains.Gold);
                    }
                }

                for (int c = 0; c < bosses; c++)
                {
                    float a = 2f * Mathf.PI * c / bosses;
                    Vector3 outward = uAxis * Mathf.Cos(a) + vAxis * Mathf.Sin(a);
                    Lay(s.Pos + outward * bossR, SpawnPoint.LookRotation(outward, axis),
                        new Vector3(bossS, bossS, bossS), Domains.Gold, PrismKind.SuperShielded);
                }
            }
        }

        /// <summary>
        /// Skirts: leaf tufts hanging off the bough between the blossoms, in ROWS along the wood
        /// rather than as one wide fan - a fan cannot hold this many leaves at its own base radius
        /// without them growing through each other, and rows also read as a longer skirt. They
        /// hang on the side AWAY from the knot's surface normal so they break the bough's
        /// silhouette rather than thickening it - the cheapest way to stop a long clean curve
        /// reading as a pipe.
        /// </summary>
        void BuildSkirts()
        {
            for (int k = 0; k < Skirts; k++)
            {
                var s = _bough[(k * _bough.Count / Skirts + SkirtPhase) % _bough.Count];
                Vector3 hang = -s.Outward;
                Vector3 side = Vector3.Cross(s.Tangent, hang);
                for (int row = 0; row < SkirtRows; row++)
                {
                    Vector3 along = s.Tangent * ((row - (SkirtRows - 1) * 0.5f) * SkirtRowPitch);
                    for (int i = 0; i < SkirtLeaves; i++)
                    {
                        int salt = 3000 + k * 97 + row * 11 + i;
                        float a = (i - (SkirtLeaves - 1) * 0.5f) * SkirtFan;
                        Vector3 dir = (hang * Mathf.Cos(a) + side * Mathf.Sin(a)).normalized;
                        Vector3 scale = HJit(SkirtLeaf, salt, 0.18f);
                        Lay(s.Pos + along + dir * (SkirtStandoff + scale.z * 0.5f),
                            SpawnPoint.LookRotation(dir, s.Tangent), scale, Domains.Jade);
                    }
                }
            }
        }

        /// <summary>
        /// The crown: boughlets leaving the bough OUTWARD and thinning toward the membrane, each
        /// ending in a leaf tuft. This is the far edge of the composition - without it the cell
        /// stops at the bough and the outer two thirds of the frame is empty black.
        /// </summary>
        void BuildCrown()
        {
            for (int c = 0; c < Crowns; c++)
            {
                int idx = (c * _bough.Count / Crowns + CrownPhase) % _bough.Count;
                var s = _bough[idx];
                Vector3 start = s.Pos.normalized * (s.Pos.magnitude + CrownRootOffset);
                float r0 = start.magnitude;
                Vector3 n0 = start / r0;
                Vector3 drift = (s.Tangent - n0 * Vector3.Dot(s.Tangent, n0)).normalized;
                float rEnd = 900f + 150f * Hash01(8000 + c * 29);
                float turn = 0.10f + 0.10f * Hash01(8100 + c * 31);

                Vector3 prev = start;
                Vector3 tipDir = n0;
                for (int i = 1; i <= CrownSteps; i++)
                {
                    float t = i / (float)CrownSteps;
                    float r = Mathf.Lerp(r0, rEnd, t);
                    float ang = turn * 2f * Mathf.PI * t;
                    Vector3 dir = (n0 * Mathf.Cos(ang) + drift * Mathf.Sin(ang)).normalized;
                    Vector3 p = dir * r;
                    Vector3 step = p - prev;
                    int salt = 8200 + c * 173 + i;
                    // Taper: a branch that keeps its girth to the tip reads as scaffolding.
                    float taper = Mathf.Lerp(1f, 0.45f, t);
                    if (i > CrownSkip)
                        Lay(prev + step * 0.5f,
                            SpawnPoint.LookRotation(step, ChainUp(step, n0, drift)),
                            ChainScale(new Vector2(CrownSection.x * taper, CrownSection.y * taper),
                                step.magnitude, salt, ChainJit), Domains.Jade);
                    prev = p;
                    tipDir = dir;
                }

                // Leaf tuft: a CAP of leaves on the tip, not a ring. The old fan put every leaf at
                // one polar angle, so a tuft was a circle of leaves whose spacing fell as the count
                // rose - 28 of them on one ring cannot clear each other at any size worth drawing.
                Vector3 tu = Vector3.Cross(tipDir, Vector3.up).sqrMagnitude < 1e-4f
                    ? Vector3.Cross(tipDir, Vector3.right).normalized
                    : Vector3.Cross(tipDir, Vector3.up).normalized;
                Vector3 tv = Vector3.Cross(tipDir, tu);
                float cosMax = Mathf.Cos(CrownTuftCone);
                for (int i = 0; i < CrownTuftLeaves; i++)
                {
                    int salt = 8400 + c * 191 + i;
                    float cz = 1f - (1f - cosMax) * (i + 0.5f) / CrownTuftLeaves;
                    float rho = Mathf.Sqrt(Mathf.Max(0f, 1f - cz * cz));
                    float a = i * GoldenAngle;
                    Vector3 outward = (tipDir * cz + tu * (rho * Mathf.Cos(a)) + tv * (rho * Mathf.Sin(a))).normalized;
                    Lay(prev + outward * CrownTuftRadius,
                        SpawnPoint.LookRotation(outward, tipDir), HJit(CrownTuftLeaf, salt, 0.18f),
                        Hash01(salt * 7) < 0.22f ? Domains.Gold : Domains.Jade);
                }
            }
        }

        /// <summary>
        /// Terraces: small inhabited platforms riding the bough - Ourobor's countryside idea at a
        /// fiftieth of its prism cost. A lens deck of concentric plate rings, three slender masts,
        /// and one super-shielded keystone each. They exist to give the cell a human scale the eye
        /// can measure the rest of it against.
        /// </summary>
        void BuildTerraces()
        {
            for (int k = 0; k < Terraces; k++)
            {
                int idx = (TerraceGaps[k] * _bough.Count / Blossoms
                    + _bough.Count / (2 * Blossoms) + BlossomPhase) % _bough.Count;
                var s = _bough[idx];
                // A terrace stands on the bough's own surface normal, FLIPPED to the side the seed
                // is not on. The raw normal points AT the seed on the knot's inner equator, so a
                // deck built on it grows INTO the nucleus (a mast ended up 90 units inside the
                // control radius that way); the cell radial fixes that and breaks something else,
                // because the deck plane then no longer contains the tangent and the bough runs out
                // through the floor. The flip keeps both: up is never toward the seed, and the wood
                // still lies in the deck's own plane where the lift can clear it.
                Vector3 n = Vector3.Dot(s.Outward, s.Pos.normalized) >= 0f ? s.Outward : -s.Outward;
                Vector3 u = s.Tangent;
                Vector3 v = Vector3.Cross(n, u);
                Vector3 c = s.Pos + n * TerraceLift;

                for (int r = 0; r < TerraceRings.Length; r++)
                {
                    for (int i = 0; i < TerraceRings[r]; i++)
                    {
                        int salt = 9000 + k * 331 + r * 37 + i;
                        float a = 2f * Mathf.PI * i / TerraceRings[r] + r * 0.4f;
                        Vector3 outward = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                        Lay(c + outward * TerraceRingRadii[r], SpawnPoint.LookRotation(outward, n),
                            HJit(TerracePlate, salt, 0.16f),
                            Hash01(salt * 3) < 0.3f ? Domains.Gold : Domains.Blue);
                    }
                }

                for (int m = 0; m < TerraceMasts; m++)
                {
                    float a = m * 2f * Mathf.PI / TerraceMasts + 0.6f;
                    Vector3 outward = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    Vector3 basePos = c + outward * TerraceMastRadius;
                    for (int i = 0; i < TerraceMastSegments; i++)
                    {
                        int salt = 9500 + k * 419 + m * 53 + i;
                        float h = 9f + i * TerraceMastPitch;
                        float taper = Mathf.Lerp(1f, 0.5f, i / (float)(TerraceMastSegments - 1));
                        Lay(basePos + n * h, SpawnPoint.LookRotation(n, outward),
                            ChainScale(new Vector2(TerraceMast.x * taper, TerraceMast.y * taper),
                                TerraceMastPitch, salt, ChainJit), Domains.Blue);
                    }
                }

                Lay(c + n * TerraceKeyLift, SpawnPoint.LookRotation(n, u),
                    new Vector3(TerraceKey, TerraceKey, TerraceKey), Domains.Gold,
                    PrismKind.SuperShielded);
            }
        }

        /// <summary>
        /// Falls: root strands that leave the bough, spiral a third of a turn about the cell
        /// centre and land on the shore. They are the composition's depth cue - the only family
        /// that crosses the whole gap between the two things the camera can always see (the
        /// nucleus and the bough), so the eye reads the cell as ONE object with a near and a far
        /// side instead of as two concentric shells. Being the family that crosses everything,
        /// it is also the family that yields.
        /// The landfall gets a rosette of shore plates, which is why the shore needs almost no
        /// scatter of its own.
        /// </summary>
        void BuildFalls()
        {
            for (int f = 0; f < Falls; f++)
            {
                var s = _bough[(f * _bough.Count / Falls + FallPhase) % _bough.Count];
                Vector3 start = s.Pos + Vector3.Cross(s.Tangent, s.Outward).normalized * FallRootOffset;
                float r0 = start.magnitude;
                Vector3 n0 = start / r0;
                // Spin axis: the component of the bough's tangent perpendicular to the radial, so
                // the strand peels off in the direction the wood was already running.
                Vector3 drift = (s.Tangent - n0 * Vector3.Dot(s.Tangent, n0)).normalized;
                float turn = 0.30f + 0.16f * Hash01(4000 + f * 17);
                float rEnd = ShoreR + FallStandoff;

                Vector3 prev = start;
                Vector3 land = start;
                for (int i = 1; i <= FallSteps; i++)
                {
                    float t = i / (float)FallSteps;
                    // Radius eases in (t^1.4) so the strand leaves the bough softly and dives at
                    // the end - a linear fall reads as a wire.
                    float r = Mathf.Lerp(r0, rEnd, Mathf.Pow(t, FallEase));
                    float ang = turn * 2f * Mathf.PI * t;
                    Vector3 dirOnSphere = (n0 * Mathf.Cos(ang) + drift * Mathf.Sin(ang)).normalized;
                    Vector3 p = dirOnSphere * r;
                    Vector3 step = p - prev;
                    // The first steps are SKIPPED, not shortened: a chain that starts at its
                    // parent's own lay point starts inside it, and no length makes that pair clear.
                    if (i > FallSkip && step.sqrMagnitude > 1e-4f)
                    {
                        int salt = 5000 + f * 211 + i;
                        TryLay(prev + step * 0.5f,
                            SpawnPoint.LookRotation(step, ChainUp(step, n0, drift)),
                            ChainScale(FallSection, step.magnitude, salt, ChainJit),
                            i > FallSteps - 10 ? Domains.Gold : Domains.Jade);
                    }
                    prev = p;
                    land = p;
                }

                LayShorePatch(land, 6000 + f * 53);
            }
        }

        /// <summary>A rosette of shore plates where a fall touches down - the world's only
        /// "ground", and it exists exactly where the eye is already being led.</summary>
        void LayShorePatch(Vector3 land, int salt)
        {
            Vector3 n = land.normalized;
            Vector3 u = Vector3.Cross(n, Vector3.up).sqrMagnitude < 1e-4f
                ? Vector3.Cross(n, Vector3.right).normalized
                : Vector3.Cross(n, Vector3.up).normalized;
            Vector3 v = Vector3.Cross(n, u);
            for (int i = 0; i < ShorePatchPlates; i++)
            {
                float a = i * GoldenAngle;
                // sqrt keeps the areal density even, so the rosette does not clot at its centre.
                float rr = ShorePatchR * Mathf.Sqrt((i + 0.5f) / ShorePatchPlates);
                Vector3 p = n * ShoreR + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rr;
                TryLay(p, SpawnPoint.LookRotation(u, n), HJit(ShorePatchPlate, salt + i, 0.16f),
                    Hash01(salt + i * 5) < 0.25f ? Domains.Gold : Domains.Jade);
            }
        }

        /// <summary>
        /// Shore bands: great circles on the seed at mutually tilted inclinations - the
        /// coastlines. A band is a chain of flat plates, so it costs one prism per 14 units of
        /// coastline and still reads as a continuous line at 700 units, where the same area filled
        /// as a surface would cost forty times as much and read as the same line. Each band rides
        /// its own shell so the two can cross without meeting, and both yield where a root came
        /// down - which is why a coastline here breaks at an estuary.
        /// </summary>
        void BuildShoreBands()
        {
            for (int b = 0; b < ShoreBands; b++)
            {
                // Tilt each band by the golden angle about a fixed axis so they never share a
                // pole - the bands cross, which is what makes the seed read as mapped.
                float tilt = b * GoldenAngle;
                Vector3 axis = new Vector3(Mathf.Cos(tilt), 0.42f, Mathf.Sin(tilt)).normalized;
                Vector3 u = Vector3.Cross(axis, Vector3.forward).normalized;
                Vector3 v = Vector3.Cross(axis, u);
                float bandR = ShoreR + (b + 1) * ShoreBandLift;

                int n = Mathf.Max(8, Mathf.FloorToInt(2f * Mathf.PI * bandR / ShoreBandStep + 0.5f));
                for (int i = 0; i < n; i++)
                {
                    int salt = 7000 + b * 977 + i;
                    // A coastline is not a hoop: drop a fifth of the plates so the band breaks up.
                    if (Hash01(salt * 11) < 0.20f) continue;
                    float a = 2f * Mathf.PI * i / n;
                    Vector3 dir = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    Vector3 tangent = (v * Mathf.Cos(a) - u * Mathf.Sin(a)).normalized;
                    TryLay(dir * bandR, SpawnPoint.LookRotation(tangent, dir),
                        ChainScale(ShoreBandSection, 2f * Mathf.PI * bandR / n, salt, 0.14f),
                        b == 1 ? Domains.Blue : Domains.Jade);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A chain prism's size: the authored cross-section, with its LENGTH derived from the gap
        /// it has to fill rather than authored. <see cref="ChainFill"/> is that fraction.
        /// </summary>
        static Vector3 ChainScale(Vector2 section, float span, int n, float jit) =>
            HJit(new Vector3(section.x, section.y, ChainFill * span), n, jit);

        /// <summary>
        /// A chain prism's UP - the roll about its own length - made safe. LookRotation is
        /// undefined when up is parallel to forward, and a fall's last steps are very nearly
        /// radial while its authored up IS the radial. Unity does not report that; it invents a
        /// pose. So the up is projected off the step, and a second, orthogonal candidate takes
        /// over when that projection collapses.
        /// </summary>
        static Vector3 ChainUp(Vector3 step, Vector3 primary, Vector3 fallback)
        {
            Vector3 d = step.normalized;
            Vector3 perp = primary - d * Vector3.Dot(primary, d);
            if (perp.sqrMagnitude < 0.02f) perp = fallback - d * Vector3.Dot(fallback, d);
            return perp;
        }

        /// <summary>
        /// <see cref="CellEnvironmentSpawnableBase.Jit"/>'s hash-keyed twin: one uniform jitter
        /// factor per prism, but keyed on the emitting INDEX instead of drawn from the shared
        /// System.Random stream. That is what lets the offline model reproduce this world's volume
        /// exactly (see the class doc), and it also means inserting a family no longer re-rolls
        /// every prism after it.
        /// Floored at 0.5 for the same reason Jit is: PrismScaleAnimator clamps per axis inside
        /// its setter with no log, so an axis under the prefab's minScale is silently a different
        /// prism (Docs/ECOSYSTEM.md §34.9).
        /// </summary>
        static Vector3 HJit(Vector3 s, int n, float amt)
        {
            float k = 1f + (Hash01(n) * 2f - 1f) * amt;
            return new Vector3(Mathf.Max(0.5f, s.x * k), Mathf.Max(0.5f, s.y * k), Mathf.Max(0.5f, s.z * k));
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // The yield grid
        // ──────────────────────────────────────────────────────────────────────────────
        //
        // Every prism this cell lays is recorded here; the three shore families ask before they
        // lay. It is a build-time structure and dies with the build - nothing queries it at run
        // time, and it adds no colliders, no components and no per-frame work.

        struct Placed
        {
            public Vector3 C, Ax, Ay, Az, H;
            public float R;
        }

        readonly List<Placed> _placed = new();
        readonly Dictionary<(int, int, int), List<int>> _yieldGrid = new();
        readonly Vector3[] _satAxes = new Vector3[15];

        static (int, int, int) YieldKey(Vector3 p) => (
            Mathf.FloorToInt(p.x / YieldCell),
            Mathf.FloorToInt(p.y / YieldCell),
            Mathf.FloorToInt(p.z / YieldCell));

        void Lay(Vector3 pos, Quaternion rot, Vector3 scale, Domains dom,
            PrismKind kind = PrismKind.Plain)
        {
            var p = new Placed
            {
                C = pos,
                Ax = rot * Vector3.right,
                Ay = rot * Vector3.up,
                Az = rot * Vector3.forward,
                H = scale * 0.5f,
                R = 0.5f * scale.magnitude,
            };
            var key = YieldKey(pos);
            if (!_yieldGrid.TryGetValue(key, out var bucket)) _yieldGrid[key] = bucket = new List<int>();
            bucket.Add(_placed.Count);
            _placed.Add(p);
            Emit(pos, rot, scale, dom, kind);
        }

        /// <summary>Lay this prism unless it would land inside one already laid.</summary>
        void TryLay(Vector3 pos, Quaternion rot, Vector3 scale, Domains dom,
            PrismKind kind = PrismKind.Plain)
        {
            if (Obstructed(pos, rot, scale)) return;
            Lay(pos, rot, scale, dom, kind);
        }

        bool Obstructed(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            Vector3 ax = rot * Vector3.right, ay = rot * Vector3.up, az = rot * Vector3.forward;
            Vector3 h = scale * 0.5f;
            float r = 0.5f * scale.magnitude;
            var (kx, ky, kz) = YieldKey(pos);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!_yieldGrid.TryGetValue((kx + dx, ky + dy, kz + dz), out var bucket))
                            continue;
                        for (int b = 0; b < bucket.Count; b++)
                        {
                            Placed o = _placed[bucket[b]];
                            Vector3 d = o.C - pos;
                            float rr = r + o.R;
                            // Sphere reject first - most candidates die here for two dot products.
                            if (d.sqrMagnitude > rr * rr) continue;
                            if (Separation(pos, ax, ay, az, h, o) < YieldGap) return true;
                        }
                    }
            return false;
        }

        /// <summary>
        /// Signed separation of two oriented boxes: the largest gap found over the 15 separating
        /// axes, so POSITIVE means clear by that much and NEGATIVE is how deep they interpenetrate.
        /// Signed rather than boolean because it is compared against <see cref="YieldGap"/> - a
        /// prism that clears by a hair has not cleared.
        /// </summary>
        float Separation(Vector3 c, Vector3 ax, Vector3 ay, Vector3 az, Vector3 h, in Placed o)
        {
            Vector3 d = o.C - c;
            _satAxes[0] = ax; _satAxes[1] = ay; _satAxes[2] = az;
            _satAxes[3] = o.Ax; _satAxes[4] = o.Ay; _satAxes[5] = o.Az;
            int n = 6;
            for (int i = 0; i < 3; i++)
                for (int j = 3; j < 6; j++)
                    _satAxes[n++] = Vector3.Cross(_satAxes[i], _satAxes[j]);

            float best = float.NegativeInfinity;
            for (int i = 0; i < 15; i++)
            {
                Vector3 L = _satAxes[i];
                float len = L.magnitude;
                // Parallel axes: the cross product is void and carries no information.
                if (len < 1e-9f) continue;
                float ra = Mathf.Abs(Vector3.Dot(ax, L)) * h.x
                         + Mathf.Abs(Vector3.Dot(ay, L)) * h.y
                         + Mathf.Abs(Vector3.Dot(az, L)) * h.z;
                float rb = Mathf.Abs(Vector3.Dot(o.Ax, L)) * o.H.x
                         + Mathf.Abs(Vector3.Dot(o.Ay, L)) * o.H.y
                         + Mathf.Abs(Vector3.Dot(o.Az, L)) * o.H.z;
                float sep = (Mathf.Abs(Vector3.Dot(d, L)) - ra - rb) / len;
                if (sep > best) best = sep;
            }
            return best;
        }
    }
}
