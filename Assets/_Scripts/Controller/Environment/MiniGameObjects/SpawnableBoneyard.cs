using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Boneyard" - the arena of <see cref="GameModes.DogFight"/>: an apocalyptic aftermath
    /// built out of the same vocabulary as <see cref="SpawnableAtlantis"/> (the intensity-4
    /// Scurry world) and aimed at the opposite feeling. Atlantis is a drowned garden-city that
    /// GREW; this is what is left after one fell.
    ///
    /// <b>Every family here exists to answer one question: where can a pilot hide, and how does
    /// the other pilot find them?</b> A dogfight in open space is a jousting match - two vessels
    /// see each other from 800 units out and converge head-on forever. So the arena is built as
    /// broken sightlines at three scales:
    ///
    ///   1. FALLEN HULKS - colossal ship-hull sections lying across the crust, ribbed and
    ///      HOLLOW, plated over roughly half their circumference so the torn-open side is a way
    ///      IN. These are the hiding places: you can sit inside one, and someone hunting you has
    ///      to commit to entering. The one place in the arena where a pilot can be genuinely
    ///      invisible.
    ///   2. SHATTERED SPIRES + SKELETON FRAMES - snapped towers and the open girder cages of a
    ///      collapsed superstructure. Cover you fly AROUND rather than into, at the scale of a
    ///      single turn. Frames are see-through, which is the point: they break a missile's
    ///      flight without breaking your view, so a pilot can watch a rocket eat a girder.
    ///   3. RUBBLE + ASH - the floor litter and the suspended fallout. Neither will stop anyone,
    ///      but both fill the volume with parallax, which is what makes a fast pass read as fast.
    ///
    /// <b>SCATTER is the whole read.</b> A boneyard is patchy and it goes all the way to the
    /// horizon: wreckage clumps into DEBRIS FIELDS with open lanes between them, spread
    /// EQUAL-AREA so the rim carries as much structure as the middle, and the exact centre is
    /// held CLEAR. Roughly <see cref="driftFraction"/> of every family never landed at all - it
    /// hangs in the volume above the crust at any attitude - so there is no single ground plane
    /// and no pile in the middle. See <see cref="ScatterPlanar"/> for the three rules and what
    /// each of them fixes.
    ///
    /// <b>Altitude is still a trade.</b> The crust buckles into shelves and most of the heavy
    /// cover rests on them, so low is a warren and high is comparatively open - but the drifters
    /// mean high is not EMPTY, and a fight can climb through wreckage instead of leaving it.
    /// Nothing enforces this; it falls out of the geometry.
    ///
    /// <b>The wreckage is COVER, not the objective.</b> Unlike Ribcage (whose bone IS the score)
    /// or the Wildlife Liberation cages (which ARE the walls of the rooms), shooting the Boneyard
    /// is worth exactly nothing in Dog Fight - the only thing that scores is landing a shot on
    /// another pilot. That is deliberate: a pilot who spends the match demolishing scenery should
    /// lose to one who spends it shooting people.
    ///
    /// INTENSITY ramps the DENSITY OF COVER and nothing else - more wrecks, tighter warrens,
    /// shorter sightlines - via the serialized structure counts plus the base
    /// <c>density</c> knob on the four prefab variants. The arena RADIUS is deliberately fixed
    /// at every intensity, for the same reason Ribcage and the wildlife cages fix theirs: it is
    /// what the player spawn shell, the AI's fallback aim point and the arena silhouette are all
    /// defined against.
    ///
    /// DETERMINISM: multiplayer clients each build the environment locally with no seed sync
    /// (like every scene-placed spawnable), so generation is fully deterministic - all
    /// randomness flows from the serialized seed through one System.Random plus the seeded noise
    /// in <see cref="PaintingStrokeToolkit"/>, and a seed of 0 falls back to
    /// <see cref="DefaultSeed"/> rather than time-seeding.
    ///
    /// COLLIDER BUDGET: plain and danger prisms ride the LOD-cullable BoxCollider, so the active
    /// count is bounded by <c>PrismColliderLodManager</c>'s radius rather than by population.
    /// Shielded / super-shielded prisms carry always-on convex MeshColliders and are therefore
    /// rationed hard - the reactor core ring and one beacon per spire, well under 1% of the
    /// structure. Do NOT armour the wreckage: a shielded hulk would be both un-shootable cover
    /// and a few thousand permanent mesh colliders.
    /// </summary>
    public class SpawnableBoneyard : CellEnvironmentSpawnableBase
    {
        [Header("Arena")]
        [Tooltip("Outer radius of the wreck field. Ash and the outermost rubble reach a little " +
                 "past it. FIXED across intensities on purpose - the spawn shell and the arena " +
                 "silhouette are defined against it. Keep well inside the r=1200 membrane.")]
        [SerializeField] float arenaRadius = 520f;

        [Tooltip("Depth of the crust bowl at its centre. The bowl rises toward the rim, so the " +
                 "middle of the arena is also its deepest, most enclosed part.")]
        [SerializeField] float crustDepth = -200f;

        [Header("Cover (the intensity dial)")]
        [Tooltip("Fallen hull sections - the hiding places. The single most expensive family " +
                 "and the one that most changes how the mode plays.")]
        [SerializeField, Min(0)] int hulkCount = 8;

        [Tooltip("Snapped towers still standing. Vertical cover for climbing and diving fights.")]
        [SerializeField, Min(0)] int spireCount = 12;

        [Tooltip("Open girder cages of the collapsed superstructure. See-through cover: stops a " +
                 "rocket, not a sightline.")]
        [SerializeField, Min(0)] int frameCount = 5;

        [Tooltip("Broken elevated roadways arcing between structures. Fly under, over, or " +
                 "through the gap where the span fell.")]
        [SerializeField, Min(0)] int overpassCount = 4;

        [Header("Scatter")]
        [Tooltip("How many DEBRIS FIELDS the wreckage clumps into. A boneyard is patchy - knots " +
                 "of wreckage with open lanes between them - not an even sprinkle, and not a pile " +
                 "in the middle. Every structure belongs to one field.")]
        [SerializeField, Min(1)] int debrisFields = 7;

        [Tooltip("Radius of one debris field, as a fraction of the arena radius. Bigger = the " +
                 "fields blur into each other; smaller = tight islands with wide empty lanes.")]
        [SerializeField, Range(0.05f, 0.45f)] float fieldRadiusFraction = 0.2f;

        [Tooltip("No structure is placed within this radius of the arena centre. The middle of " +
                 "the arena is deliberately OPEN AIR - it is where the opening merge happens, " +
                 "and a clear centre is what stops the whole field reading as a heap.")]
        [SerializeField, Min(0f)] float coreClearRadius = 120f;

        [Tooltip("Fraction of structures that float CLEAR of the crust instead of resting on it. " +
                 "A wreck field in open water has no single ground plane; the drifters are what " +
                 "fill the upper volume and let a fight climb.")]
        [SerializeField, Range(0f, 1f)] float driftFraction = 0.4f;

        [Tooltip("How far above the crust a drifting wreck can hang.")]
        [SerializeField, Min(0f)] float driftHeight = 300f;

        // ── Family constants ─────────────────────────────────────────────────
        // Per-unit prism counts are FIXED (never randomised) so the analytic budget in
        // Tools/Build/boneyard_budget.py can mirror this file exactly. Randomness moves things
        // around; it never changes how many there are.

        const int CrustPlates = 3000;      // before density
        const int RubbleChunks = 2400;     // before density
        const int AshMotes = 1700;         // before density

        const int HulkRibs = 10;           // ribs per hull section
        const int HulkRibSegments = 22;    // prisms per rib ring
        const int HulkStations = 34;       // plating stations along the axis
        const int HulkPlateArc = 11;       // plating prisms per station (half the rib ring)

        const int SpireSegments = 26;
        const int SpireRing = 8;

        const int FrameEdges = 12;         // a box frame's twelve edges
        const int FrameEdgePrisms = 20;
        const int FrameBraces = 6;
        const int FrameBracePrisms = 16;

        const int OverpassDeck = 64;
        const int OverpassRails = 2;

        const int ReactorRing = 24;        // the super-shielded core ring
        const int ReactorShellRibs = 10;
        const int ReactorShellPrisms = 26;

        protected override int DefaultSeed => 41;
        protected override int LayCapacity => 34000;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                System.HashCode.Combine(arenaRadius, crustDepth, hulkCount, spireCount,
                    frameCount, overpassCount),
                System.HashCode.Combine(debrisFields, fieldRadiusFraction, coreClearRadius,
                    driftFraction, driftHeight),
                nameof(SpawnableBoneyard), 2);

        // Wreck domains. Structures are painted across the full triad rather than one colour,
        // because in a dogfight the cover is also the map: "the ruby hulk" is how a pilot says
        // where they are, and a monochrome arena is one a pilot gets lost in.
        static readonly Domains[] WreckDoms = { Domains.Jade, Domains.Ruby, Domains.Gold };

        protected override void BuildEnvironment()
        {
            BuildCrust();
            BuildHulks();
            BuildSpires();
            BuildFrames();
            BuildOverpasses();
            BuildRubble();
            BuildReactor();
            BuildAsh();
        }

        /// <summary>Paraboloid crust: deepest at the centre, rising toward the rim.</summary>
        float CrustY(float r)
        {
            float t = Mathf.Clamp01(r / arenaRadius);
            return crustDepth + t * t * 130f;
        }

        /// <summary>
        /// Crust height. TWO octaves of buckling, and the coarse one is deliberately violent
        /// (±110 against a bowl that only rises 130 across its whole radius): it breaks the
        /// paraboloid into SHELVES at different heights instead of one smooth dish. A smooth
        /// dish is a funnel - it points every sightline and every drifting pilot at the middle,
        /// which is most of what made the first pass read as centred.
        /// </summary>
        float CrustSurface(Vector3 planar)
        {
            float coarse = (N01(planar.x * 0.0032f, 0f, planar.z * 0.0032f, 3) - 0.5f) * 220f;
            float fine = (N01(planar.x * 0.011f, 17f, planar.z * 0.011f, 8) - 0.5f) * 46f;
            float r = new Vector2(planar.x, planar.z).magnitude;
            return CrustY(r) + coarse + fine;
        }

        // ── SCATTER ──────────────────────────────────────────────────────────

        /// <summary>
        /// Where one structure goes. Every structural family routes through this, and it is the
        /// difference between "a boneyard" and "a heap with a clear edge".
        ///
        /// Three rules, each fixing a specific way the first pass read as CENTRED:
        ///
        ///   1. <b>Equal-area radii, everywhere.</b> Drawing a radius uniformly puts far more
        ///      wreckage per unit of AREA near the middle (density falls off as 1/r), which is
        ///      exactly the "it's all in the centre" look. Every radius here is
        ///      <c>R·sqrt(u)</c>, so a ring at the rim gets as much wreckage as a ring near the
        ///      core.
        ///   2. <b>Debris FIELDS, not a sprinkle.</b> A wrecked world is patchy: knots of
        ///      structure with open lanes between them. Structures are clustered onto
        ///      <see cref="debrisFields"/> anchors spread equal-area over the disc, and the
        ///      families INTERLEAVE across those anchors (the +index·3 stride) so a field is a
        ///      mixed tangle of hulk and spire and girder rather than one family's private
        ///      island.
        ///   3. <b>The centre is empty.</b> Nothing is placed inside
        ///      <see cref="coreClearRadius"/>; a placement that lands there is pushed out rather
        ///      than dropped, so the clearing costs no cover. The open middle is where the
        ///      opening merge happens, and it is what makes the surrounding scatter legible.
        ///
        /// Returns the horizontal position; <see cref="ScatterHeight"/> resolves the vertical.
        /// </summary>
        Vector3 ScatterPlanar(int index, int salt)
        {
            int fields = Mathf.Max(1, debrisFields);
            int field = (index * 3 + salt) % fields;

            // Field anchor: equal-area radius on a golden-angle spiral, with per-field angular
            // jitter so the anchors do not read as a spiral.
            //
            // The equal-area draw runs over the PLAYABLE ANNULUS - coreClearRadius out to
            // 0.92R - not over the whole disc. Spreading equal-area across the full disc sounds
            // right and is not: the innermost anchor lands inside the clearing, its whole field
            // gets shoved back out by the clamp, and the result is a ring of wreckage piled
            // against the core with the rim left bare. Measured over the shipped counts, the
            // full-disc version put ~51% of all structure in the inner third of the AREA and
            // only ~11% in the outer third; over the annulus it is close to even.
            float u = (field + 0.5f) / fields;
            float rIn = Mathf.Min(coreClearRadius, arenaRadius * 0.5f);
            float rOut = arenaRadius * 0.92f;
            float anchorR = Mathf.Sqrt(rIn * rIn + u * (rOut * rOut - rIn * rIn));
            float anchorA = field * GoldenAngle + Hash01(field * 191 + _noiseSeed) * 1.4f;
            var anchor = new Vector3(anchorR * Mathf.Cos(anchorA), 0f, anchorR * Mathf.Sin(anchorA));

            // Offset within the field - equal-area again, so a field is evenly filled rather
            // than dense at its own middle.
            float fieldR = arenaRadius * fieldRadiusFraction;
            float offR = fieldR * Mathf.Sqrt(Hash01(index * 53 + salt * 7 + 1));
            float offA = Hash01(index * 67 + salt * 11 + 2) * Mathf.PI * 2f;
            var planar = anchor + new Vector3(offR * Mathf.Cos(offA), 0f, offR * Mathf.Sin(offA));

            // Keep the core clear, and keep everything inside the arena.
            float d = new Vector2(planar.x, planar.z).magnitude;
            if (d < 0.001f) planar = new Vector3(coreClearRadius, 0f, 0f);
            else
            {
                float clamped = Mathf.Clamp(d, coreClearRadius, arenaRadius * 0.97f);
                planar *= clamped / d;
            }
            return planar;
        }

        /// <summary>
        /// The vertical half of a scatter placement. A fraction of structures DRIFT clear of the
        /// crust instead of resting on it - without them every wreck sits on one surface and the
        /// arena reads as a floor with junk on it rather than a volume full of debris, which
        /// also flattens the altitude trade the mode is built around.
        /// </summary>
        float ScatterHeight(Vector3 planar, int index, int salt, float restOffset)
        {
            float ground = CrustSurface(planar) + restOffset;
            float roll = Hash01(index * 89 + salt * 13 + 5);
            if (roll >= driftFraction) return ground;

            // Drifters hang somewhere in the volume above their patch of crust.
            float t = Hash01(index * 97 + salt * 17 + 6);
            return ground + 60f + driftHeight * t;
        }

        /// <summary>True when this structure is one of the drifters (see above).</summary>
        bool IsDrifting(int index, int salt) => Hash01(index * 89 + salt * 13 + 5) < driftFraction;

        // ── 1. CRUST - the shattered ground ──────────────────────────────────

        /// <summary>
        /// A phyllotaxis-spread field of tilted slabs over the bowl. Deliberately POROUS
        /// (~14 units between plates against ~9-unit plates), so it reads as ground that has
        /// been broken up rather than a floor: a pilot can drop through it into the gaps, which
        /// is the lowest and most claustrophobic layer of the arena.
        /// </summary>
        void BuildCrust()
        {
            int n = Scaled(CrustPlates);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float r = arenaRadius * Mathf.Sqrt(t);          // equal-area spread
                float a = i * GoldenAngle;
                var planar = new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a));

                // WARP the even spread with low-frequency noise. An equal-area phyllotaxis disc
                // is perfectly uniform, which reads as a manufactured floor; pushing each plate
                // along a noise field pulls them into shelves and opens ragged HOLES between
                // them, so the ground looks broken up rather than laid down. The plate COUNT is
                // untouched - this only moves them, which is what keeps boneyard_budget.py an
                // exact mirror.
                float wx = (N01(planar.x * 0.0026f, 5f, planar.z * 0.0026f, 21) - 0.5f) * 190f;
                float wz = (N01(planar.x * 0.0026f, 91f, planar.z * 0.0026f, 22) - 0.5f) * 190f;
                planar += new Vector3(wx, 0f, wz);

                float y = CrustSurface(planar);
                var pos = new Vector3(planar.x, y, planar.z);

                // Slab normal follows the local buckling, so plates lie ALONG the ground rather
                // than all facing straight up - the difference between rubble and floor tiles.
                float e = 9f;
                float dx = CrustSurface(planar + new Vector3(e, 0f, 0f)) - y;
                float dz = CrustSurface(planar + new Vector3(0f, 0f, e)) - y;
                var normal = new Vector3(-dx, e, -dz).normalized;
                var along = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));

                Emit(pos, SpawnPoint.LookRotation(normal, along),
                    Jit(new Vector3(9f, 8.4f, 1.4f), 0.35f), Domains.Blue);
            }
        }

        // ── 2. FALLEN HULKS - the hiding places ──────────────────────────────

        /// <summary>
        /// Each hulk is a broken hull section: ribs at intervals along an axis, plated over
        /// roughly HALF its circumference. Both halves of that description are load-bearing.
        ///
        /// The gaps BETWEEN ribs are what let a pilot slip in from the side; the missing half of
        /// the plating is what makes the interior reachable at all and gives the hunter a
        /// sightline they have to work for. A fully plated tube would be a wall with two ends,
        /// which is a corridor, not a hiding place - you would always know where your quarry had
        /// to come out.
        ///
        /// They lie at shallow tilts ON the crust rather than floating, so each one also makes a
        /// sheltered pocket underneath where it lifts off the ground.
        /// </summary>
        void BuildHulks()
        {
            for (int k = 0; k < hulkCount; k++)
            {
                var centrePlanar = ScatterPlanar(k, 0);

                float length = 190f + 150f * Hash01(k * 17 + 5);
                float radius = 30f + 18f * Hash01(k * 23 + 11);

                // A hulk that came to rest on the crust lies roughly flat, as if it slid to a
                // stop. A DRIFTER never landed, so it tumbles to any attitude - which is what
                // stops the hulks reading as a row of logs all pointing the same way up.
                bool drifting = IsDrifting(k, 0);
                float heading = Hash01(k * 41 + 3) * Mathf.PI * 2f;
                float pitch = (Hash01(k * 53 + 7) - 0.5f) * (drifting ? 2.6f : 0.55f);
                var axis = new Vector3(
                    Mathf.Cos(heading) * Mathf.Cos(pitch),
                    Mathf.Sin(pitch),
                    Mathf.Sin(heading) * Mathf.Cos(pitch)).normalized;

                float baseY = ScatterHeight(centrePlanar, k, 0, radius * 0.75f);
                var centre = new Vector3(centrePlanar.x, baseY, centrePlanar.z);

                var up = Vector3.Cross(axis, Vector3.up).sqrMagnitude > 1e-3f
                    ? Vector3.Cross(axis, Vector3.up).normalized
                    : Vector3.forward;
                var side = Vector3.Cross(up, axis).normalized;

                var dom = WreckDoms[k % 3];
                // Which way the hull is torn open. Random per hulk so a pilot cannot learn one
                // rule and read every wreck the same way.
                float openPhase = Hash01(k * 61 + 13) * Mathf.PI * 2f;

                BuildHulkRibs(centre, axis, up, side, length, radius, dom, k);
                BuildHulkPlating(centre, axis, up, side, length, radius, dom, k, openPhase);
            }
        }

        void BuildHulkRibs(Vector3 centre, Vector3 axis, Vector3 up, Vector3 side,
            float length, float radius, Domains dom, int k)
        {
            for (int rib = 0; rib < HulkRibs; rib++)
            {
                float t = HulkRibs == 1 ? 0.5f : rib / (float)(HulkRibs - 1);
                Vector3 ringCentre = centre + axis * ((t - 0.5f) * length);

                // The hull tapers toward its torn ends, so the section reads as a piece of
                // something bigger rather than a barrel.
                float taper = 0.72f + 0.28f * Mathf.Sin(t * Mathf.PI);
                float rr = radius * taper;

                for (int s = 0; s < HulkRibSegments; s++)
                {
                    float ang = 2f * Mathf.PI * s / HulkRibSegments;
                    var radial = (up * Mathf.Cos(ang) + side * Mathf.Sin(ang)).normalized;
                    // Circumferential direction = d(radial)/d(ang). The rib's LONG axis runs
                    // around the hoop, so consecutive prisms chain into a continuous ring
                    // rather than reading as a ring of separate posts.
                    var tangent = (up * -Mathf.Sin(ang) + side * Mathf.Cos(ang)).normalized;
                    var pos = ringCentre + radial * rr;

                    // Torn rib ends bite. Sparse and only on the two end ribs, so the danger is
                    // exactly where the wreck looks most jagged - telegraphed by the geometry
                    // rather than hidden in it.
                    bool endRib = rib == 0 || rib == HulkRibs - 1;
                    var kind = endRib && Hash01(k * 97 + rib * 31 + s) < 0.22f
                        ? PrismKind.Danger
                        : PrismKind.Plain;

                    // forward = radial (2.2 thick), up = tangent (5.6 around the hoop),
                    // x = 3.4 along the hull axis.
                    Emit(pos, SpawnPoint.LookRotation(radial, tangent),
                        Jit(new Vector3(3.4f, 5.6f, 2.2f), 0.2f), dom, kind);
                }
            }
        }

        void BuildHulkPlating(Vector3 centre, Vector3 axis, Vector3 up, Vector3 side,
            float length, float radius, Domains dom, int k, float openPhase)
        {
            for (int st = 0; st < HulkStations; st++)
            {
                float t = st / (float)(HulkStations - 1);
                Vector3 ringCentre = centre + axis * ((t - 0.5f) * length);
                float taper = 0.72f + 0.28f * Mathf.Sin(t * Mathf.PI);
                float rr = radius * taper;

                for (int s = 0; s < HulkPlateArc; s++)
                {
                    // Plating covers one contiguous ~half of the circumference starting at
                    // openPhase; the other half is the tear. Contiguous rather than scattered so
                    // the opening is a DOOR a pilot can find and use, not a sieve.
                    float ang = openPhase + Mathf.PI * (s / (float)HulkPlateArc);
                    var radial = (up * Mathf.Cos(ang) + side * Mathf.Sin(ang)).normalized;

                    // Buckled skin: plates ride slightly in and out of true.
                    float dent = 1f + 0.06f * (N01(st * 0.4f, s * 0.7f, k * 3.1f, 11) - 0.5f) * 2f;
                    var pos = ringCentre + radial * (rr * dent);

                    // Plates are sized to nearly TOUCH at the loose end of the size range and
                    // to overlap at the tight end, which is what makes the plated half read as
                    // a wall rather than a lattice. That opacity is the whole point of a hiding
                    // place: at 11 plates over a half-circumference of ~107u the spacing is
                    // ~9.7u circumferentially and 5.8-10.3u along the axis, so anything much
                    // narrower than this leaves a hull you can see straight through.
                    Emit(pos, SpawnPoint.LookRotation(radial, axis),
                        Jit(new Vector3(8.6f, 8.0f, 1.5f), 0.25f), dom);
                }
            }
        }

        // ── 3. SHATTERED SPIRES - vertical cover ─────────────────────────────

        void BuildSpires()
        {
            for (int k = 0; k < spireCount; k++)
            {
                var planar = ScatterPlanar(k, 1);
                float baseY = ScatterHeight(planar, k, 1, 0f);

                float height = 140f + 210f * Hash01(k * 29 + 9);
                float baseRadius = 11f + 9f * Hash01(k * 37 + 4);

                // A snapped tower leans. The lean is what stops the spire field reading as a
                // row of chess pieces, and it produces the overhangs a pilot can duck under.
                // A spire that never landed - a torn-off section still turning - can point any
                // which way, so the drifters take a much wider lean.
                bool drifting = IsDrifting(k, 1);
                float leanAngle = (Hash01(k * 43 + 6) - 0.5f) * (drifting ? 3.0f : 0.5f);
                float leanHeading = Hash01(k * 47 + 2) * Mathf.PI * 2f;
                var lean = new Vector3(
                    Mathf.Cos(leanHeading) * Mathf.Sin(leanAngle),
                    Mathf.Cos(leanAngle),
                    Mathf.Sin(leanHeading) * Mathf.Sin(leanAngle)).normalized;

                var dom = WreckDoms[(k + 1) % 3];
                var side = Vector3.Cross(lean, Vector3.right).sqrMagnitude > 1e-3f
                    ? Vector3.Cross(lean, Vector3.right).normalized
                    : Vector3.forward;
                var other = Vector3.Cross(lean, side).normalized;

                for (int seg = 0; seg < SpireSegments; seg++)
                {
                    float t = seg / (float)(SpireSegments - 1);
                    // Taper toward the snap, and stop short of a point - a snapped tower ends in
                    // a ragged stump, not a needle.
                    float rr = baseRadius * (1f - 0.62f * t);
                    var ringCentre = new Vector3(planar.x, baseY, planar.z) + lean * (t * height);

                    for (int s = 0; s < SpireRing; s++)
                    {
                        float ang = 2f * Mathf.PI * s / SpireRing + t * 1.1f; // slight twist
                        var radial = (side * Mathf.Cos(ang) + other * Mathf.Sin(ang)).normalized;
                        Emit(ringCentre + radial * rr, SpawnPoint.LookRotation(radial, lean),
                            Jit(new Vector3(3.6f, 5.4f, 2f), 0.25f), dom);
                    }
                }

                // One shielded beacon per spire: the arena's navigation landmarks, and the whole
                // shielded ration outside the reactor. Shielded rather than plain so they SURVIVE
                // a match - a landmark a stray rocket can delete is not a landmark.
                var tip = new Vector3(planar.x, baseY, planar.z) + lean * height;
                Emit(tip, SpawnPoint.LookRotation(lean, side), new Vector3(4f, 4f, 4f),
                    dom, PrismKind.Shielded);
            }
        }

        // ── 4. SKELETON FRAMES - see-through cover ───────────────────────────

        /// <summary>
        /// Open girder cages: the bones of a collapsed superstructure. These are the arena's
        /// most interesting cover precisely because you can SEE through them - a pilot watching
        /// their quarry through a frame still has to shoot around it, and a missile that clips a
        /// girder detonates early. They also roof over a patch of the warren, so a fight can go
        /// on underneath one.
        /// </summary>
        void BuildFrames()
        {
            for (int k = 0; k < frameCount; k++)
            {
                var planar = ScatterPlanar(k, 2);
                float baseY = ScatterHeight(planar, k, 2, 20f);

                var half = new Vector3(
                    52f + 40f * Hash01(k * 13 + 1),
                    38f + 46f * Hash01(k * 17 + 2),
                    52f + 40f * Hash01(k * 19 + 3));

                float yaw = Hash01(k * 23 + 5) * Mathf.PI * 2f;
                float roll = (Hash01(k * 29 + 8) - 0.5f) * 0.7f;   // it fell; it did not settle level
                var frameRot = Quaternion.Euler(roll * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * 0.6f * Mathf.Rad2Deg);
                var origin = new Vector3(planar.x, baseY, planar.z);
                var dom = WreckDoms[(k + 2) % 3];

                // The twelve edges of a box: for each axis, the four edges parallel to it sit at
                // the four sign combinations of the OTHER two axes. Walked this way rather than
                // as a hand-written corner table so the enumeration is obviously complete.
                for (int e = 0; e < FrameEdges; e++)
                {
                    int axis = e / 4;                       // 0=x, 1=y, 2=z
                    int combo = e % 4;                      // sign pair for the other two axes
                    int b = (axis + 1) % 3;
                    int c = (axis + 2) % 3;

                    var start = Vector3.zero;
                    start[axis] = -half[axis];
                    start[b] = (combo & 1) == 0 ? -half[b] : half[b];
                    start[c] = (combo & 2) == 0 ? -half[c] : half[c];

                    var dir = Vector3.zero;
                    dir[axis] = 1f;

                    EmitGirder(origin, frameRot, start, dir, half[axis] * 2f, FrameEdgePrisms, dom);
                }

                // Cross-braces through the volume: the difference between a wireframe box and
                // something that reads as structure.
                for (int b = 0; b < FrameBraces; b++)
                {
                    float u = (b + 0.5f) / FrameBraces;
                    var start = new Vector3(-half.x, Mathf.Lerp(-half.y, half.y, u), -half.z);
                    var end = new Vector3(half.x, Mathf.Lerp(half.y, -half.y, u),
                        (b % 2 == 0) ? half.z : -half.z);
                    var delta = end - start;
                    EmitGirder(origin, frameRot, start, delta.normalized, delta.magnitude,
                        FrameBracePrisms, dom);
                }
            }
        }

        void EmitGirder(Vector3 origin, Quaternion rot, Vector3 localStart, Vector3 localDir,
            float length, int count, Domains dom)
        {
            var worldDir = rot * localDir;
            var worldStart = origin + rot * localStart;
            var girderRot = SpawnPoint.LookRotation(worldDir, Vector3.up);
            float step = length / Mathf.Max(1, count - 1);

            for (int i = 0; i < count; i++)
                Emit(worldStart + worldDir * (i * step), girderRot,
                    new Vector3(2.6f, 2.6f, 4.4f), dom);
        }

        // ── 5. OVERPASSES - broken elevated roadways ─────────────────────────

        /// <summary>
        /// Arcs of deck rising off the crust and falling back to it, with a MISSING span in the
        /// middle. The gap is the feature: it is a hole a pilot can shoot through, dive through
        /// at speed, or misjudge.
        /// </summary>
        void BuildOverpasses()
        {
            for (int k = 0; k < overpassCount; k++)
            {
                float span = 250f + 190f * Hash01(k * 83 + 2);
                var mid = ScatterPlanar(k, 3);
                float heading = Hash01(k * 59 + 4) * Mathf.PI * 2f;
                var along = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
                var across = Vector3.Cross(Vector3.up, along).normalized;

                float rise = 90f + 90f * Hash01(k * 67 + 6);
                var dom = WreckDoms[k % 3];

                // The collapsed span, as a fraction of the deck either side of centre.
                float gapHalf = 0.10f + 0.09f * Hash01(k * 73 + 8);

                for (int i = 0; i < OverpassDeck; i++)
                {
                    float t = i / (float)(OverpassDeck - 1);
                    float centred = t - 0.5f;
                    if (Mathf.Abs(centred) < gapHalf) continue;   // the fallen span

                    var planar = mid + along * (centred * span);
                    float ground = CrustSurface(planar);
                    // Parabolic arc: piers on the crust at both ends, apex at the gap.
                    float y = ground + rise * (1f - 4f * centred * centred) + 30f;

                    // Deck tangent, so the roadway chains continuously through the arc.
                    float dy = -8f * centred * rise / span;
                    var tangent = (along + Vector3.up * dy).normalized;
                    var deckRot = SpawnPoint.LookRotation(Vector3.Cross(tangent, across).normalized, tangent);

                    var pos = new Vector3(planar.x, y, planar.z);
                    Emit(pos, deckRot, new Vector3(11f, 7f, 1.8f), dom);

                    for (int rail = 0; rail < OverpassRails; rail++)
                    {
                        float sgn = rail == 0 ? 1f : -1f;
                        Emit(pos + across * (sgn * 9f) + Vector3.up * 4f,
                            SpawnPoint.LookRotation(tangent, Vector3.up),
                            new Vector3(1.6f, 3.2f, 3.6f), dom);
                    }
                }
            }
        }

        // ── 6. RUBBLE - the floor litter ─────────────────────────────────────

        void BuildRubble()
        {
            int n = Scaled(RubbleChunks);
            for (int i = 0; i < n; i++)
            {
                float a = i * GoldenAngle * 1.19f;
                float r = arenaRadius * 1.04f * Mathf.Sqrt(Hash01(i * 7 + 1));
                var planar = new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a));

                // Rubble piles up ON the crust and just above it, so the lowest layer of the
                // arena is genuinely cluttered rather than a clean floor with debris hovering.
                float y = CrustSurface(planar) + 4f + 34f * Hash01(i * 11 + 3);

                Emit(new Vector3(planar.x, y, planar.z),
                    Quaternion.Euler(Hash01(i * 3) * 360f, Hash01(i * 5) * 360f, Hash01(i * 13) * 360f),
                    Jit(new Vector3(3.6f, 3f, 5.2f), 0.45f), WreckDoms[i % 3]);
            }
        }

        // ── 7. THE REACTOR - the centre landmark ─────────────────────────────

        /// <summary>
        /// One unmissable structure, and the only super-shielded mass in the Boneyard. It exists
        /// for orientation - "meet me at the reactor" - and it is deliberately a bad place to
        /// loiter: the core is ringed with danger, so the most visible point in the arena is
        /// also the most punishing to sit still in.
        ///
        /// <b>It sits OFF-CENTRE, and that is the point.</b> A landmark in the middle of a
        /// radially symmetric arena tells a pilot nothing - every bearing off it looks the same,
        /// so it orients nobody while planting a monolith exactly where the wreck field most
        /// needs to read as open. Off to one side it becomes a real reference ("north of the
        /// reactor") and the centre stays clear.
        /// </summary>
        void BuildReactor()
        {
            float ra = Hash01(_noiseSeed * 7 + 3) * Mathf.PI * 2f;
            float rr = arenaRadius * 0.52f;
            var reactorPlanar = new Vector3(rr * Mathf.Cos(ra), 0f, rr * Mathf.Sin(ra));
            float baseY = CrustSurface(reactorPlanar);

            // The core ring: super-shielded, indestructible, permanent. 24 prisms is the whole
            // always-on mesh-collider budget for this family.
            for (int i = 0; i < ReactorRing; i++)
            {
                float ang = 2f * Mathf.PI * i / ReactorRing;
                var radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var tangent = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                Emit(new Vector3(reactorPlanar.x + radial.x * 46f, baseY + 60f,
                        reactorPlanar.z + radial.z * 46f),
                    SpawnPoint.LookRotation(radial, tangent),
                    new Vector3(5f, 8f, 4f), Domains.Blue, PrismKind.SuperShielded);
            }

            // The torn containment shell around it: open ribs, so the core is visible from
            // outside and the shell is something to weave through.
            for (int rib = 0; rib < ReactorShellRibs; rib++)
            {
                float phi = Mathf.PI * (rib + 0.5f) / ReactorShellRibs;
                float ringR = 96f * Mathf.Sin(phi);
                float ringY = baseY + 60f + 96f * Mathf.Cos(phi);
                var dom = WreckDoms[rib % 3];

                for (int s = 0; s < ReactorShellPrisms; s++)
                {
                    float ang = 2f * Mathf.PI * s / ReactorShellPrisms;
                    var radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    var outward = (radial * Mathf.Sin(phi) + Vector3.up * Mathf.Cos(phi)).normalized;
                    var tangent = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));

                    // A quarter of the shell is missing - the blow-out. Contiguous, so it reads
                    // as damage and gives a clean way in to the core.
                    if (ang > 0.35f && ang < 1.92f) continue;

                    // The innermost ribs are the hot ones.
                    var kind = (rib == ReactorShellRibs / 2 || rib == ReactorShellRibs / 2 - 1)
                               && Hash01(rib * 51 + s * 7) < 0.3f
                        ? PrismKind.Danger
                        : PrismKind.Plain;

                    Emit(new Vector3(reactorPlanar.x + radial.x * ringR, ringY,
                            reactorPlanar.z + radial.z * ringR),
                        SpawnPoint.LookRotation(outward, tangent),
                        Jit(new Vector3(4.4f, 5.8f, 1.8f), 0.2f), dom, kind);
                }
            }
        }

        // ── 8. ASH - the suspended fallout ───────────────────────────────────

        /// <summary>
        /// Sparse motes filling the whole volume, thickest low and thinning with altitude. They
        /// stop nothing; they are here so that a fast pass through the open upper half still has
        /// something to measure speed against. Skimmable, so a pilot cutting through the drift
        /// is also feeding.
        /// </summary>
        void BuildAsh()
        {
            int n = Scaled(AshMotes);
            for (int i = 0; i < n; i++)
            {
                float a = i * GoldenAngle * 2.7f;
                float r = arenaRadius * 1.1f * Mathf.Sqrt(Hash01(i * 17 + 5));
                // Biased low: squaring the height sample piles the drift onto the crust and
                // leaves the sky comparatively clean.
                float h = Hash01(i * 23 + 7);
                float y = crustDepth + 40f + (h * h) * 520f;

                Emit(new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)),
                    Quaternion.Euler(Hash01(i * 29) * 360f, Hash01(i * 31) * 360f, 0f),
                    new Vector3(1.1f, 1.1f, 2.2f), WreckDoms[(i / 3) % 3]);
            }
        }
    }
}
