using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A plant that grows the way real plants do: a small set of growing TIPS, each advancing
    /// along its own heading, pulled toward a growth axis, wandering a little, occasionally
    /// splitting - and, past a depth, opening <b>whorls</b> of leaves spaced at the golden angle
    /// (phyllotaxis, the same constant the canopies and shell patches in the authored
    /// environments use).
    ///
    /// <b>Why a third flora growth model.</b> The two we had are surfaces, not plants:
    /// <see cref="AssembledFlora"/> crystallises a triply-periodic minimal surface (gyroid,
    /// Schwarz P) out of bonded lattice sites, and <see cref="BranchingFlora"/> grows a
    /// crystaltropic scribble that only reads as structure in bulk. A GARDEN needs plants with a
    /// silhouette - a trunk that rises and opens a crown, a tendril that creeps along a trellis,
    /// a rosette that carpets a bed - and it needs them to be the SAME species varying by
    /// parameter, not three bespoke behaviours. So this is one growth model with a form dial:
    ///
    /// <list type="bullet">
    /// <item><b>Arbor</b> - one tip, strong axis tropism, branching, wide whorls: a garden tree.</item>
    /// <item><b>Tendril</b> - several tips, weak tropism, heavy wander, no whorls: a creeper.</item>
    /// <item><b>Rosette</b> - one tip, no rise, whorls from the first step: dense bed cover.</item>
    /// </list>
    ///
    /// Everything else is inherited and unchanged: prisms are conserved mass laid through the
    /// ordinary health-prism path, growth is gated on <see cref="Cell.FloraGrowingEnabled"/>
    /// (steady until Frenzy, no self-limit), the live-prism budget frees as fauna graze so a
    /// cropped plant regrows, sites are claimed in <see cref="PrismSpatialIndex"/> before the
    /// spawn (colliders are blind for the first 0.6s), death withers spindle-by-spindle from the
    /// extremities and drops the elemental crystal. No clock removes anything.
    /// </summary>
    public class PhyllotacticFlora : Flora
    {
        [Header("Form - stem")]
        [Tooltip("Growing tips seeded at the root. 1 = a single trunk; more = a clump/creeper.")]
        [SerializeField, Min(1)] int initialTips = 1;
        [Tooltip("Hard cap on simultaneously growing tips (bounds the per-tick decision cost).")]
        [SerializeField, Min(1)] int maxTips = 10;
        [SerializeField, Min(1)] int maxDepth = 26;
        [Tooltip("Maximum LIVE prisms this flora can hold. Consumption frees budget - a grazed " +
                 "plant regrows toward this cap instead of staying a permanent fragment.")]
        [SerializeField, Min(1)] int maxTotalSpawnedObjects = 400;

        /// <summary>The live-prism budget this individual resolved to - the base reads it for
        /// the reproduction maturity gate (see <see cref="Flora.PrismBudget"/>).</summary>
        protected override int PrismBudget => maxTotalSpawnedObjects;
        [Tooltip("Tips advanced per grow tick.")]
        [SerializeField, Min(1)] int growthsPerTick = 3;
        [Tooltip("Instantiations executed per frame. The tick DECIDES (and claims sites); the " +
                 "drain spreads the prefab instantiation over frames - same pacing contract as " +
                 "AssembledFlora, throughput preserved.")]
        [SerializeField, Min(1)] int maxSpawnsPerFrame = 1;

        [Header("Form - segment")]
        [SerializeField, Min(0.1f)] float segmentLength = 15f;
        [Tooltip("Length multiplier per depth - <1 tapers the plant toward its tips.")]
        [SerializeField, Range(0.6f, 1.2f)] float segmentTaper = 0.97f;

        // Prism shape note: LENGTHS here are structural - a stem prism spans its own segment, a
        // leaf prism spans its own reach - so this flora reads LeafSize.x/y (the element's
        // cross-section identity: a Space plant is wiry, a Mass plant is thick) and does NOT
        // read LeafSize.z. The assembled species keep using LeafSize.z as their thin axis.
        [Header("Form - prism shape")]
        [Tooltip("STEM prism: x,y are cross-section multiples of the flora's leafSize (so the " +
                 "per-element leaf identity - the Space needle, the Mass slab - carries into the " +
                 "stalk), and z is the fraction of the ACTUAL SEGMENT LENGTH the prism spans. " +
                 "z near 1 chains the segments into a continuous stalk; a fixed length would " +
                 "leave a long-segmented plant looking like a string of beads.")]
        [SerializeField] Vector3 stemScale = new(0.5f, 0.5f, 0.9f);
        [Tooltip("WHORL LEAF prism: x,y are cross-section multiples of leafSize, z is the " +
                 "fraction of the leaf's REACH it spans. The leaf is then placed at half its " +
                 "own length out from the node, so it runs from the stalk outward and is " +
                 "ATTACHED rather than floating at the end of an invisible stalk.")]
        [SerializeField] Vector3 leafScale = new(1f, 0.28f, 0.95f);
        [Tooltip("Scale multiplier per depth - <1 thins the plant toward its tips, so a mature " +
                 "trunk reads heavy at the base and fine at the crown.")]
        [SerializeField, Range(0.8f, 1.1f)] float depthTaper = 0.97f;
        [Tooltip("Per-prism uniform size jitter (±fraction). Nothing in a garden is machined; " +
                 "a little variation is most of what separates 'grown' from 'stamped'.")]
        [SerializeField, Range(0f, 0.5f)] float prismJitter = 0.18f;
        [Tooltip("Every other leaf in a whorl takes this fraction of full length - the long/short " +
                 "alternation real whorls have. 1 = every leaf the same.")]
        [SerializeField, Range(0.2f, 1f)] float whorlAlternateScale = 0.62f;

        [Header("Form - heading")]
        [Tooltip("Per-step pull toward the growth axis (the planting site's normal, or outward " +
                 "from the cell centre when unplanted ground). 0 = a creeper that ignores up.")]
        [SerializeField, Range(0f, 1f)] float tropism = 0.35f;
        [Tooltip("Random deviation added to the heading each step - the difference between a mast " +
                 "and a vine.")]
        [SerializeField, Range(0f, 1f)] float wander = 0.2f;
        [Tooltip("Half-angle of the cone the initial tips are seeded into, around the growth axis.")]
        [SerializeField, Range(0f, 90f)] float spreadDegrees = 20f;
        [Tooltip("Constant downward bias added to every heading step (world -Y). Turns a mast " +
                 "into an arching frond or a weeping form; 0 for anything that should stand up.")]
        [SerializeField, Range(0f, 1f)] float gravityDroop;
        [Tooltip("Extra roll about the heading per node, in degrees, on top of the golden angle - " +
                 "a stem whose whorls corkscrew rather than stacking in register.")]
        [SerializeField, Range(-40f, 40f)] float spiralTwist;

        [Header("Form - branching")]
        [SerializeField, Min(0)] int branchStartDepth = 3;
        [SerializeField, Range(0f, 1f)] float branchChance = 0.25f;
        [SerializeField, Range(0f, 90f)] float branchAngle = 34f;

        [Header("Form - whorl (the phyllotactic head)")]
        [Tooltip("Depth at which the plant starts opening leaf whorls. Above maxDepth = never " +
                 "(a bare tendril).")]
        [SerializeField, Min(0)] int whorlStartDepth = 6;
        [Tooltip("Open a whorl every N steps past the start depth.")]
        [SerializeField, Min(1)] int whorlEvery = 4;
        [SerializeField, Min(0)] int whorlLeaves = 5;
        [SerializeField, Min(0f)] float whorlRadius = 10f;
        [Tooltip("Extra whorl radius at full depth, as a fraction of the base radius - a crown " +
                 "that opens as it climbs.")]
        [SerializeField, Range(0f, 3f)] float whorlFlare = 0.8f;
        [Tooltip("Tilt of each whorl leaf toward the tip (+) or the root (-), in degrees. A flat " +
                 "wheel of leaves reads as a gear; a cupped one reads as a flower.")]
        [SerializeField, Range(-80f, 80f)] float leafPitchDegrees = 24f;
        [Tooltip("A tip that reaches maxDepth opens one final whorl at this size multiple - the " +
                 "bloom at the end of the stalk. 0 = no terminal head.")]
        [SerializeField, Range(0f, 4f)] float terminalWhorlScale = 1.7f;

        /// <summary>The phyllotaxis constant - the same golden angle the authored canopies use.</summary>
        const float GoldenAngle = 2.39996323f;

        // Site claim radius: below half a segment so a legitimate next site is never blocked,
        // above any per-step drift so a genuine duplicate always is (the GyroidAssembler rule).
        const float ClaimRadiusFraction = 0.4f;

        struct Tip
        {
            public GameObject gameObject;   // the spindle this tip grows from
            public Vector3 heading;
            public int depth;
            public float roll;              // golden-angle phase carried down the stem
        }

        struct SpawnOrder
        {
            public Tip parent;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 heading;
            public Vector3 scale;
            public bool becomesTip;
            public float decidedAt;
        }

        // A Frenzy hold can outlive a site claim; an order whose claim lapsed could overlap
        // another grower. Dropped at drain - the next tick simply re-decides it.
        const float MaxOrderAgeSeconds = PrismSpatialIndex.ReservationTtlSeconds - 1f;

        readonly List<Tip> _tips = new();
        readonly Queue<SpawnOrder> _pending = new();
        Vector3 _axis = Vector3.up;

        public override void Initialize(Cell cell)
        {
            // Base plants us (honoring an authored site) and starts the grow cadence. The first
            // tick finds no tips and no prisms, so it no-ops - exactly like BranchingFlora, whose
            // trunks are also seeded after the base call.
            base.Initialize(cell);

            _axis = GrowthUp;
            SafeLookRotation.TrySet(transform, _axis, transform);
            SeedTips();
        }

        /// <summary>
        /// Phyllotactic layer of the variant expression: the live-prism budget - the same field
        /// <see cref="AssembledFlora"/> and <see cref="BranchingFlora"/> read, so a cell that
        /// authors a per-plant budget gets it on every flora family rather than silently only on
        /// the assembled one.
        /// </summary>
        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;
            if (tuning.MaxTotalSpawnedObjects >= 0) maxTotalSpawnedObjects = tuning.MaxTotalSpawnedObjects;
            // Cell density scalar, applied AFTER the absolute so it scales whatever budget won.
            // Round half UP explicitly: Mathf.RoundToInt is banker's rounding, which would turn
            // an authored 150 x 0.9 into 134 on one species and 135 on the next.
            if (tuning.MaxTotalSpawnedObjectsScale > 0f)
                maxTotalSpawnedObjects = Mathf.Max(1, Mathf.FloorToInt(
                    maxTotalSpawnedObjects * tuning.MaxTotalSpawnedObjectsScale + 0.5f));
        }

        public override void Plant()
        {
            // A pinned site (a garden bed, or the Lifeform Matrix toy's spawn-here station) wins;
            // otherwise disperse across the cell like every other flora.
            if (TryGetPlantPositionOverride(out var pinned))
            {
                transform.position = pinned;
                return;
            }
            // Shell measured from the CELL CENTRE (Flora.ResolvePlantCenter), not the crystal -
            // a roaming crystal must not carry the planting shell outside the membrane.
            float radius = ResolvePlantRadius(legacyRadius: 150f);
            transform.position = ResolvePlantCenter() + radius * Random.onUnitSphere;
        }

        // ── Growth ────────────────────────────────────────────────────────────

        public override void Grow()
        {
            // Live-prism budget (frees as fauna graze - a cropped plant regrows).
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects) return;

            // Frenzy gate: steady growth until the cell tops out, then freeze and resume when an
            // active force brings the mass back down. Cell.FloraGrowingEnabled is the only gate.
            if (cell && !cell.FloraGrowingEnabled) return;

            if (_tips.Count == 0)
            {
                // Reawakening: every tip was grazed off or grew out. Re-sprout from survivors so
                // the plant keeps producing instead of sitting inert. Guarded on having survivors
                // so a freshly-planted flora doesn't double-seed before Initialize finishes.
                if (healthTracker != null && healthTracker.Count > 0) ReseedTips();
                return;
            }

            int decisions = Mathf.Min(growthsPerTick, _tips.Count);
            for (int i = 0; i < decisions; i++)
            {
                // Round-robin from the front so every tip advances, not just the first few.
                var tip = _tips[0];
                _tips.RemoveAt(0);
                if (!tip.gameObject || tip.depth >= maxDepth) continue;
                DecideStep(tip);
            }
        }

        void DecideStep(Tip tip)
        {
            float len = segmentLength * Mathf.Pow(segmentTaper, tip.depth);

            Vector3 heading = Vector3.Slerp(tip.heading, _axis, tropism * 0.3f);
            if (wander > 0f) heading += Random.onUnitSphere * wander;
            // Gravity is not a tropism: it does not compete with the growth axis, it bends the
            // result. A frond climbs and arches over; a mast with droop 0 is unaffected.
            if (gravityDroop > 0f) heading += Vector3.down * gravityDroop;
            if (heading.sqrMagnitude < 0.0001f) heading = _axis;
            heading.Normalize();

            Vector3 pos = tip.gameObject.transform.position + heading * len;
            if (!Claim(pos, len))
            {
                // Site taken (a neighbour plant, or a sibling tip this same tick). Keep the tip
                // alive with a nudged heading - next tick it tries elsewhere. Nothing is lost.
                tip.heading = (heading + Random.onUnitSphere * 0.6f).normalized;
                Keep(tip);
                return;
            }

            int nodeDepth = tip.depth + 1;

            _pending.Enqueue(new SpawnOrder
            {
                parent = tip,
                position = pos,
                rotation = SpawnPoint.LookRotation(heading, _axis),
                heading = heading,
                // The stem prism runs ALONG the segment (long axis +z == heading), so successive
                // segments overlap into one stalk instead of reading as beads.
                scale = StemPrismScale(nodeDepth, len, 1f),
                becomesTip = true,
                decidedAt = Time.time,
            });

            // A whorl at this node - the leaf head. Terminal leaves (they never become tips),
            // which is what bounds a mature plant's cost.
            bool atTip = nodeDepth >= maxDepth;
            if (whorlLeaves > 0 && nodeDepth >= whorlStartDepth &&
                (nodeDepth - whorlStartDepth) % whorlEvery == 0)
                DecideWhorl(tip, pos, heading, nodeDepth, 1f);
            // ...and the bloom at the end of the stalk, whatever the whorl cadence says.
            else if (whorlLeaves > 0 && atTip && terminalWhorlScale > 0f)
                DecideWhorl(tip, pos, heading, nodeDepth, terminalWhorlScale);
        }

        /// <summary>
        /// A whorl of leaves spaced at the golden angle around the heading, cupped toward the tip
        /// by <see cref="leafPitchDegrees"/> and alternating long/short. The flat wheel this
        /// replaces read as a gear; the cup and the alternation are what make it read as a head.
        /// </summary>
        void DecideWhorl(Tip tip, Vector3 node, Vector3 heading, int depth, float sizeScale)
        {
            float t = maxDepth > 0 ? Mathf.Clamp01(depth / (float)maxDepth) : 0f;
            float radius = whorlRadius * (1f + whorlFlare * t) * sizeScale;
            Vector3 basis = Vector3.Cross(heading, Mathf.Abs(Vector3.Dot(heading, _axis)) > 0.95f
                ? Vector3.right : _axis);
            if (basis.sqrMagnitude < 0.0001f) basis = Vector3.Cross(heading, Vector3.forward);
            basis.Normalize();

            for (int i = 0; i < whorlLeaves; i++)
            {
                float angle = tip.roll + i * GoldenAngle;
                Vector3 outward = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, heading) * basis;

                // Cup: tilt the leaf toward (or away from) the growing tip.
                Vector3 hinge = Vector3.Cross(outward, heading);
                if (hinge.sqrMagnitude > 0.0001f)
                    outward = Quaternion.AngleAxis(-leafPitchDegrees, hinge.normalized) * outward;
                outward.Normalize();

                // Long/short alternation: a head with an inner and an outer rank, not one flat rim.
                float reach = radius * ((i % 2 == 0) ? 1f : whorlAlternateScale);
                var scale = LeafPrismScale(depth, reach);

                // Placed at HALF its own length out from the node, so the leaf runs from the
                // stalk outward and is attached to the plant. Placing it AT the reach left every
                // leaf floating at the end of an invisible stem - the single biggest reason the
                // whorls read as a wheel of chips rather than a head of leaves.
                // Claim on the leaf's CROSS-SECTION, not its length. A whorl is meant to be dense
                // around one node - claiming a length-sized volume would have each leaf reject
                // its own siblings and the head would come out with holes in it.
                Vector3 pos = node + outward * (scale.z * 0.5f);
                if (!Claim(pos, scale.x)) continue;

                _pending.Enqueue(new SpawnOrder
                {
                    parent = tip,
                    position = pos,
                    rotation = SpawnPoint.LookRotation(outward, heading),
                    heading = outward,
                    scale = scale,
                    becomesTip = false,
                    decidedAt = Time.time,
                });
            }
        }

        /// <summary>
        /// The stem prism for a node: cross-section from the element's leaf identity
        /// (<c>FloraVariantTuning.LeafSize</c>, already level-scaled), long axis spanning
        /// <paramref name="segment"/> so successive segments meet.
        /// </summary>
        Vector3 StemPrismScale(int depth, float segment, float lengthMul)
        {
            float taper = Mathf.Pow(depthTaper, depth);
            float j = Jitter();
            return Floor(new Vector3(
                LeafSize.x * stemScale.x * taper * j,
                LeafSize.y * stemScale.y * taper * j,
                segment * stemScale.z * lengthMul * j));
        }

        /// <summary>
        /// The leaf prism for a whorl position: cross-section from the element's leaf identity,
        /// long axis spanning <paramref name="reach"/> outward from the stalk.
        /// </summary>
        Vector3 LeafPrismScale(int depth, float reach)
        {
            float taper = Mathf.Pow(depthTaper, depth);
            float j = Jitter();
            return Floor(new Vector3(
                LeafSize.x * leafScale.x * taper * j,
                LeafSize.y * leafScale.y * taper * j,
                reach * leafScale.z * j));
        }

        /// <summary>
        /// Per-prism size jitter. Deliberately ONE implementation shared with
        /// <see cref="TryPreviewGrowth"/>: the preview draws the prism shapes this plant really
        /// grows, and a second copy of the taper/jitter maths is a second thing to keep in step.
        /// The preview swaps the SOURCE of randomness (it must never touch
        /// <c>UnityEngine.Random</c> - that would perturb a shared deterministic sequence), never
        /// the shape.
        /// </summary>
        float Jitter()
        {
            if (prismJitter <= 0f) return 1f;
            return _previewRng != null
                ? 1f + (float)(_previewRng.NextDouble() * 2.0 - 1.0) * prismJitter
                : 1f + Random.Range(-prismJitter, prismJitter);
        }

        /// <summary>Non-null ONLY for the duration of <see cref="TryPreviewGrowth"/>.</summary>
        System.Random _previewRng;

        /// <summary>Floored at the prism scale animator's 0.5 minimum so nothing silently clamps.</summary>
        static Vector3 Floor(Vector3 s) =>
            new(Mathf.Max(0.5f, s.x), Mathf.Max(0.5f, s.y), Mathf.Max(0.5f, s.z));

        static bool Claim(Vector3 position, float spacing)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return true;
            return index.TryReserve(position, Mathf.Max(1.5f, ClaimRadiusFraction * spacing));
        }

        void Update()
        {
            if (_pending.Count == 0) return;
            // Parity with the WaitForSeconds grow loop: frozen at timeScale 0 (menu pause).
            if (Time.timeScale <= 0f) return;
            // Orders decided just before Frenzy WAIT here (sites stay claimed) and execute when
            // growing re-enables - the same freeze-and-resume the tick gate gives.
            if (cell && !cell.FloraGrowingEnabled) return;

            int spawned = 0;
            while (spawned < maxSpawnsPerFrame && _pending.Count > 0)
            {
                var order = _pending.Dequeue();
                if (Time.time - order.decidedAt > MaxOrderAgeSeconds) continue;
                Execute(order);
                spawned++;
            }
        }

        // The scale the next AddHealthBlock should apply. Flora.AddHealthBlock stamps every prism
        // with the one leafSize; this flora shapes its prisms per ROLE (stem vs leaf) and per
        // depth, so it overrides that stamp for the prism it is currently placing.
        Vector3? _pendingPrismScale;

        public override void AddHealthBlock(HealthPrism healthPrism)
        {
            base.AddHealthBlock(healthPrism);
            if (healthPrism && _pendingPrismScale.HasValue)
                healthPrism.TargetScale = _pendingPrismScale.Value;
            _pendingPrismScale = null;

            // NOT calling AdmitTargetScale here, deliberately - see Docs/ECOSYSTEM.md §34.9.
            // This flora's STEM spans its whole segment, so 5 of the 8 authored species ask for
            // a long axis above PrismScaleAnimator's 10 ceiling and are silently trimmed to it:
            // Arbor 15.3, Reed 13.6, Spire 12.4, Frond 11.4, Tendril 10.5. Admitting the size
            // would be the same correct fix Flora.AddHealthBlock got - and it would change the
            // look of the Hesperides garden and Rampage on a branch about the Schwarz P lattice,
            // with no way to play-test it here. Filed rather than smuggled in.
        }

        void Execute(SpawnOrder order)
        {
            // The parent spindle may have been eaten between decision and drain; the claimed site
            // simply lapses with its TTL.
            if (!order.parent.gameObject) return;

            var newSpindle = Instantiate(spindle, order.parent.gameObject.transform);
            newSpindle.LifeForm = this;
            newSpindle.transform.position = order.position;
            newSpindle.transform.rotation = order.rotation;
            AddSpindle(newSpindle);

            var leaf = EnvironmentPrismPool.Get(healthPrism, order.position, order.rotation);
            leaf.transform.SetParent(newSpindle.transform, false);
            leaf.transform.localPosition = Vector3.zero;
            leaf.transform.localRotation = Quaternion.identity;
            leaf.LifeForm = this;
            leaf.ChangeTeam(domain);
            _pendingPrismScale = order.scale;
            AddHealthBlock(leaf);
            leaf.Initialize("flora");
            // Growth is this plant's feeding - see Flora.NotifyGrew.
            NotifyGrew();

            if (!order.becomesTip) return;

            float nextRoll = order.parent.roll + GoldenAngle + spiralTwist * Mathf.Deg2Rad;

            Keep(new Tip
            {
                gameObject = newSpindle.gameObject,
                heading = order.heading,
                depth = order.parent.depth + 1,
                roll = nextRoll,
            });

            // Split: past the branch depth a node may fork, so the plant fills volume instead of
            // growing one wire. Bounded by maxTips.
            if (order.parent.depth + 1 >= branchStartDepth && _tips.Count < maxTips &&
                Random.value < branchChance)
            {
                Vector3 axis = Vector3.Cross(order.heading, _axis);
                if (axis.sqrMagnitude < 0.0001f) axis = Vector3.Cross(order.heading, Vector3.right);
                Vector3 forked = Quaternion.AngleAxis(branchAngle, axis.normalized) *
                                 (Quaternion.AngleAxis(Random.Range(0f, 360f), order.heading) * order.heading);
                Keep(new Tip
                {
                    gameObject = newSpindle.gameObject,
                    heading = forked.normalized,
                    depth = order.parent.depth + 1,
                    roll = order.parent.roll + GoldenAngle * 0.5f,
                });
            }
        }

        void Keep(Tip tip)
        {
            if (_tips.Count >= maxTips) return;
            _tips.Add(tip);
        }

        // ── Seeding / reawakening ─────────────────────────────────────────────

        void SeedTips()
        {
            Vector3 basis = Vector3.Cross(_axis, Mathf.Abs(_axis.y) > 0.95f ? Vector3.right : Vector3.up)
                .normalized;

            for (int i = 0; i < initialTips; i++)
            {
                // Fan the initial tips around the axis at the golden angle so a multi-tip clump
                // never sends two shoots the same way...
                float roll = i * GoldenAngle;
                Vector3 tilt = Quaternion.AngleAxis(roll * Mathf.Rad2Deg, _axis) * basis;
                Vector3 heading = Quaternion.AngleAxis(spreadDegrees, tilt) * _axis;

                // ...and stand each root apart on the ground. Seeding every root at the planting
                // point stacked five reed stalks in exactly one spot; a clump wants a footprint.
                Vector3 root0 = transform.position +
                                (initialTips > 1 ? tilt * (segmentLength * 0.28f) : Vector3.zero);

                var root = AddSpindle();
                root.transform.position = root0;

                var leaf = EnvironmentPrismPool.Get(healthPrism, root0, transform.rotation);
                leaf.transform.SetParent(root.transform, false);
                leaf.transform.localPosition = Vector3.zero;
                leaf.LifeForm = this;
                leaf.ChangeTeam(domain);
                // The root collar: shorter than a segment but half again as thick - a plant sits
                // on a base, it does not sprout from a twig.
                var collar = StemPrismScale(0, segmentLength * 0.55f, 1f);
                _pendingPrismScale = new Vector3(collar.x * 1.5f, collar.y * 1.5f, collar.z);
                AddHealthBlock(leaf);
                leaf.Initialize("flora");

                Keep(new Tip { gameObject = root.gameObject, heading = heading.normalized, depth = 0, roll = roll });
            }
        }

        /// <summary>
        /// Re-sprout tips from surviving spindles so a grazed plant recovers instead of sitting
        /// as a dead fragment. Survivors are sampled at random (not first-N) so repeated reseeds
        /// don't keep picking the same exhausted end of the plant.
        /// </summary>
        void ReseedTips()
        {
            var survivors = new List<Spindle>();
            foreach (var hp in healthTracker.All)
            {
                if (!hp) continue;
                var sp = hp.GetComponentInParent<Spindle>();
                if (sp) survivors.Add(sp);
            }
            if (survivors.Count == 0) return;

            int target = Mathf.Max(1, Mathf.Min(initialTips, maxTips));
            for (int i = 0; i < target && i < survivors.Count; i++)
            {
                int pick = Random.Range(i, survivors.Count);
                (survivors[i], survivors[pick]) = (survivors[pick], survivors[i]);
                Keep(new Tip
                {
                    gameObject = survivors[i].gameObject,
                    heading = (survivors[i].transform.forward + _axis).normalized,
                    // Regrowth restarts mid-plant, not from scratch: a re-sprouted shoot opens
                    // whorls again rather than climbing the whole stem a second time. CLAMPED
                    // below maxDepth - a species that never whorls authors whorlStartDepth above
                    // maxDepth, and an unclamped reseed there would hand back a tip that is
                    // instantly discarded as over-depth, reseeding forever and never growing.
                    depth = Mathf.Clamp(whorlStartDepth - 1, 0, Mathf.Max(0, maxDepth - 2)),
                    roll = Random.value * Mathf.PI * 2f,
                });
            }
        }

        // ── Preview ───────────────────────────────────────────────────────────

        /// <summary>
        /// Pure preview of the phyllotactic walk - see <see cref="Flora.TryPreviewGrowth"/>.
        /// Mirrors <see cref="SeedTips"/> then <see cref="DecideStep"/> / <see cref="DecideWhorl"/>
        /// / the tip half of <see cref="Execute"/>, in LOCAL space around the plant's own origin,
        /// and spawns nothing at all: no prism, no spindle, no GameObject, no cell, no spatial
        /// index reservation.
        ///
        /// <para>Three substitutions, and only three. Randomness comes from a caller-seeded
        /// <see cref="System.Random"/> so the same seed always draws the same plant (and so the
        /// shared <c>UnityEngine.Random</c> sequence is never perturbed). Site claims go to a
        /// local list instead of <see cref="PrismSpatialIndex"/>, since there is no world to
        /// contend with. And a decided node becomes a tip immediately rather than at the drain,
        /// because there are no frames here - which reorders growth slightly and cannot change the
        /// silhouette, the one thing a thumbnail shows.</para>
        ///
        /// <para>Prism SHAPES are not re-derived: <see cref="StemPrismScale"/> and
        /// <see cref="LeafPrismScale"/> are the live ones, so a preview cannot drift from the
        /// plant on taper, cross-section or jitter.</para>
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0) return false;

            // A plant is bounded by its own live-prism budget; showing more would draw a species
            // denser than the one the cell actually holds.
            int cap = Mathf.Min(budget, Mathf.Max(1, maxTotalSpawnedObjects));

            _previewRng = new System.Random(seed);
            try
            {
                // Local frame: the plant stands on its own origin and grows up its own axis. The
                // live _axis is the planting site's normal, which does not exist without a cell.
                Vector3 axis = Vector3.up;
                var claims = new List<Vector4>(cap);
                var tips = new List<PreviewTip>();

                SeedPreviewTips(axis, tips, claims, into, cap);

                while (into.Count < cap && tips.Count > 0)
                {
                    var tip = tips[0];
                    tips.RemoveAt(0);
                    if (tip.Depth >= maxDepth) continue;
                    StepPreview(tip, axis, tips, claims, into, cap);
                }

                return into.Count > 0;
            }
            finally
            {
                _previewRng = null;
            }
        }

        struct PreviewTip
        {
            public Vector3 Position;
            public Vector3 Heading;
            public int Depth;
            public float Roll;
        }

        void SeedPreviewTips(Vector3 axis, List<PreviewTip> tips, List<Vector4> claims,
            List<SpawnPoint> into, int cap)
        {
            Vector3 basis = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.95f ? Vector3.right : Vector3.up)
                .normalized;

            for (int i = 0; i < initialTips && into.Count < cap; i++)
            {
                float roll = i * GoldenAngle;
                Vector3 tilt = Quaternion.AngleAxis(roll * Mathf.Rad2Deg, axis) * basis;
                Vector3 heading = Quaternion.AngleAxis(spreadDegrees, tilt) * axis;

                Vector3 root = initialTips > 1 ? tilt * (segmentLength * 0.28f) : Vector3.zero;

                var collar = StemPrismScale(0, segmentLength * 0.55f, 1f);
                into.Add(new SpawnPoint(root, Quaternion.identity,
                    new Vector3(collar.x * 1.5f, collar.y * 1.5f, collar.z)));
                ClaimPreview(claims, root, segmentLength);

                if (tips.Count < maxTips)
                    tips.Add(new PreviewTip
                    {
                        Position = root,
                        Heading = heading.normalized,
                        Depth = 0,
                        Roll = roll,
                    });
            }
        }

        void StepPreview(PreviewTip tip, Vector3 axis, List<PreviewTip> tips, List<Vector4> claims,
            List<SpawnPoint> into, int cap)
        {
            float len = segmentLength * Mathf.Pow(segmentTaper, tip.Depth);

            Vector3 heading = Vector3.Slerp(tip.Heading, axis, tropism * 0.3f);
            if (wander > 0f) heading += PreviewOnUnitSphere() * wander;
            if (gravityDroop > 0f) heading += Vector3.down * gravityDroop;
            if (heading.sqrMagnitude < 0.0001f) heading = axis;
            heading.Normalize();

            Vector3 pos = tip.Position + heading * len;
            if (!ClaimPreview(claims, pos, len))
            {
                tip.Heading = (heading + PreviewOnUnitSphere() * 0.6f).normalized;
                if (tips.Count < maxTips) tips.Add(tip);
                return;
            }

            int nodeDepth = tip.Depth + 1;
            into.Add(new SpawnPoint(pos, SpawnPoint.LookRotation(heading, axis),
                StemPrismScale(nodeDepth, len, 1f)));

            bool atTip = nodeDepth >= maxDepth;
            if (whorlLeaves > 0 && nodeDepth >= whorlStartDepth &&
                (nodeDepth - whorlStartDepth) % whorlEvery == 0)
                WhorlPreview(tip.Roll, pos, heading, axis, nodeDepth, 1f, claims, into, cap);
            else if (whorlLeaves > 0 && atTip && terminalWhorlScale > 0f)
                WhorlPreview(tip.Roll, pos, heading, axis, nodeDepth, terminalWhorlScale, claims, into, cap);

            // The tip half of Execute. Immediate here - there is no drain without frames.
            if (tips.Count < maxTips)
                tips.Add(new PreviewTip
                {
                    Position = pos,
                    Heading = heading,
                    Depth = nodeDepth,
                    Roll = tip.Roll + GoldenAngle + spiralTwist * Mathf.Deg2Rad,
                });

            if (nodeDepth >= branchStartDepth && tips.Count < maxTips &&
                _previewRng.NextDouble() < branchChance)
            {
                Vector3 forkAxis = Vector3.Cross(heading, axis);
                if (forkAxis.sqrMagnitude < 0.0001f) forkAxis = Vector3.Cross(heading, Vector3.right);
                Vector3 forked = Quaternion.AngleAxis(branchAngle, forkAxis.normalized) *
                                 (Quaternion.AngleAxis((float)(_previewRng.NextDouble() * 360.0), heading) * heading);
                tips.Add(new PreviewTip
                {
                    Position = pos,
                    Heading = forked.normalized,
                    Depth = nodeDepth,
                    Roll = tip.Roll + GoldenAngle * 0.5f,
                });
            }
        }

        void WhorlPreview(float tipRoll, Vector3 node, Vector3 heading, Vector3 axis, int depth,
            float sizeScale, List<Vector4> claims, List<SpawnPoint> into, int cap)
        {
            float t = maxDepth > 0 ? Mathf.Clamp01(depth / (float)maxDepth) : 0f;
            float radius = whorlRadius * (1f + whorlFlare * t) * sizeScale;
            Vector3 basis = Vector3.Cross(heading, Mathf.Abs(Vector3.Dot(heading, axis)) > 0.95f
                ? Vector3.right : axis);
            if (basis.sqrMagnitude < 0.0001f) basis = Vector3.Cross(heading, Vector3.forward);
            basis.Normalize();

            for (int i = 0; i < whorlLeaves && into.Count < cap; i++)
            {
                float angle = tipRoll + i * GoldenAngle;
                Vector3 outward = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, heading) * basis;

                Vector3 hinge = Vector3.Cross(outward, heading);
                if (hinge.sqrMagnitude > 0.0001f)
                    outward = Quaternion.AngleAxis(-leafPitchDegrees, hinge.normalized) * outward;
                outward.Normalize();

                float reach = radius * ((i % 2 == 0) ? 1f : whorlAlternateScale);
                var scale = LeafPrismScale(depth, reach);

                Vector3 pos = node + outward * (scale.z * 0.5f);
                if (!ClaimPreview(claims, pos, scale.x)) continue;

                into.Add(new SpawnPoint(pos, SpawnPoint.LookRotation(outward, heading), scale));
            }
        }

        /// <summary>
        /// The local stand-in for <see cref="Claim"/>. Same radius rule; a plain list because a
        /// preview has no world to contend with and at most a few hundred points to test.
        /// </summary>
        static bool ClaimPreview(List<Vector4> claims, Vector3 position, float spacing)
        {
            float radius = Mathf.Max(1.5f, ClaimRadiusFraction * spacing);
            for (int i = 0; i < claims.Count; i++)
            {
                var c = claims[i];
                float reach = Mathf.Max(radius, c.w);
                if ((new Vector3(c.x, c.y, c.z) - position).sqrMagnitude < reach * reach) return false;
            }
            claims.Add(new Vector4(position.x, position.y, position.z, radius));
            return true;
        }

        Vector3 PreviewOnUnitSphere()
        {
            double y = _previewRng.NextDouble() * 2.0 - 1.0;
            double theta = _previewRng.NextDouble() * System.Math.PI * 2.0;
            double r = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - y * y));
            return new Vector3((float)(r * System.Math.Cos(theta)), (float)y,
                               (float)(r * System.Math.Sin(theta)));
        }

        protected override void Die(string killerName = "")
        {
            // The drain is not a coroutine, so Die's StopAllCoroutines does not stop it: without
            // this a dying plant keeps parenting fresh spindles onto an evaporating corpse
            // (stalls the wither wait, and hard-destroys the child with its spindle - a pop-out).
            _pending.Clear();
            _tips.Clear();
            base.Die(killerName);
        }

        public override void RemoveSpindle(Spindle spindle)
        {
            base.RemoveSpindle(spindle);
            if (!spindle) return;
            for (int i = _tips.Count - 1; i >= 0; i--)
                if (_tips[i].gameObject == spindle.gameObject) _tips.RemoveAt(i);
        }
    }
}
