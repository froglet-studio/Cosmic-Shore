using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A plant that grows over the surface of a <b>Mandelbulb</b> - the escape-time fractal of
    /// <c>v -> v^n + c</c> in triplex coordinates - one prism per surface site, spreading from a
    /// single seed at its footing until it has covered the whole form or run out of budget.
    ///
    /// <b>Why a fourth flora growth family.</b> The three we had each answer a different question
    /// about shape, and none of them can answer this one. <see cref="AssembledFlora"/> crystallises
    /// a lattice - it needs an exact tile and a bond table, which a fractal boundary does not have
    /// and cannot be given (see <see cref="MandelbulbLattice"/>). <see cref="BranchingFlora"/> and
    /// <see cref="PhyllotacticFlora"/> grow by advancing headings through open space, so the shape
    /// they make is a property of their own walk; here the shape is a property of a FUNCTION, and
    /// the plant's job is only to discover it. That is a genuinely different growth rule, and it
    /// is the one that makes self-similarity available to the ecology: a Mandelbulb has structure
    /// at every scale, so the same species reads as a different object as its prisms get smaller.
    ///
    /// <para><b>What emerges and what is written down.</b> Nothing in this file describes a bulb.
    /// A site is laid iff it is inside the set and has an exposed face; a plant expands to its
    /// neighbours. The lobes, the polar cup and the terracing are what that rule leaves behind -
    /// the same claim, and the same kind of claim, the gyroid octagon colony makes
    /// (Docs/ECOSYSTEM.md §32.7).</para>
    ///
    /// <para><b>The element is the fractal ORDER.</b> Docs/ECOSYSTEM.md §40: a lifeform is its
    /// species and its element and nothing else, and everything an element states about itself it
    /// states exactly once. Here an element states its <i>power</i> - Charge grows the classic
    /// 8th-order bulb, the others grow genuinely different solids - which is a far stronger
    /// expression of identity than a leaf aspect would be, and it is why this species does NOT
    /// vary its prism shape per element: the leaf has to tile the lattice, and the element has
    /// already changed the whole object. Resolved in <see cref="Initialize"/> AFTER
    /// <c>base.Initialize</c>, the one point where the prefab, the rolled variant, the cell
    /// overrides and the crystal carrying the element have all landed - the same choke point
    /// <c>Flora.ResolveShieldPeriod</c> uses, and for the same reason: the cadence is authored per
    /// CONFIG while the element is ROLLED per plant.</para>
    ///
    /// <para>Everything else is inherited and unchanged: prisms are conserved mass laid through
    /// the ordinary health-prism path, growth is gated on <c>Cell.FloraGrowingEnabled</c>, the
    /// live-prism budget frees as fauna graze so a cropped plant regrows, sites are claimed in
    /// <c>PrismSpatialIndex</c> before the spawn, death withers spindle-by-spindle and drops the
    /// elemental crystal. No clock removes anything.</para>
    /// </summary>
    public class MandelbulbFlora : Flora
    {
        /// <summary>One element's FORM: the fractal order it grows, and the plate it grows it
        /// out of. Authored on the PREFAB rather than on the four element configs because the
        /// config's element is rolled per plant, so no per-element asset field can reach a config
        /// that rolls (Docs/ECOSYSTEM.md §38's argument, applied to shape instead of tempo).
        ///
        /// <para>The plate is per-element for exactly one reason and it is a geometric one:
        /// <b>a CHARGE plant's leaves are shielded by law</b> (<c>Flora.ResolveShieldPeriod</c>),
        /// and a shield replaces the box with the octahedron CIRCUMSCRIBING it - 3x the
        /// half-extents, reaching 1.5 x leafSize (Docs/ECOSYSTEM.md §35). Measured on this
        /// species' own sites, the plate the other three elements clear at fuses 1639 of Charge's
        /// 2810 neighbour pairs into one solid. So Charge is fitted against its ARMOUR and comes
        /// out ~2.2x smaller: its plates read as a sparse skeleton and its octahedra fill the
        /// lattice in, which is exactly the outcome the gyroid and Schwarz P Charge variants
        /// shipped with. A zero plate falls back to <see cref="plateScale"/>.</para></summary>
        [Serializable]
        public struct ElementalForm
        {
            public Element Element;
            [Range(2, 16)] public int Power;
            [Tooltip("Prism size as a multiple of the WORLD pitch; leave zero to use plateScale.")]
            public Vector3 Plate;
        }

        [Header("Form - the set")]
        [Tooltip("Per-element FORM - the exponent in v -> v^n + c, and the plate. 8 is the " +
                 "classic Mandelbulb. An element not listed here falls back to the values below.")]
        [SerializeField] ElementalForm[] formByElement =
        {
            new() { Element = Element.Charge, Power = 8,  Plate = new(0.326f, 0.326f, 0.114f) },
            new() { Element = Element.Mass,   Power = 5,  Plate = new(0f, 0f, 0f) },
            new() { Element = Element.Space,  Power = 3,  Plate = new(0f, 0f, 0f) },
            new() { Element = Element.Time,   Power = 12, Plate = new(0f, 0f, 0f) },
        };

        [Tooltip("Fallback order for a plant with no element (a toy clone, a microscene release).")]
        [SerializeField, Range(2, 16)] int power = 8;

        [Tooltip("Escape-test iterations. Raising it sharpens the boundary and costs a little " +
                 "more per site; it does NOT change the object at prism scale past about 8.")]
        [SerializeField, Range(4, 24)] int escapeIterations = 10;

        [Tooltip("Escape radius. 2 is the standard bailout and the value the offline model proves " +
                 "this species against - changing it changes every measured count.")]
        [SerializeField, Min(1.5f)] float bailout = 2f;

        [Header("Form - the lattice")]
        [Tooltip("Voxel pitch in the set's OWN unit space. This is the species' resolution dial: " +
                 "prism count grows as 1/pitch^2, because the plant is a SURFACE. Measured at " +
                 "power 8 - 0.20 -> 204 prisms, 0.16 -> 362, 0.13 -> 589, 0.115 -> 793.")]
        [SerializeField, Range(0.06f, 0.30f)] float latticePitch = 0.13f;

        [Tooltip("World radius of the set's unit sphere - how big one plant is. World prism " +
                 "spacing is latticePitch x this.")]
        [SerializeField, Min(1f)] float shellRadius = 34f;

        [Tooltip("Prism size as a multiple of the WORLD pitch, for an element that authors no " +
                 "plate of its own. x,y span the surface (1 = plates meeting edge to edge before " +
                 "rotation), z is the thin axis into the surface. FITTED, not eyeballed: an exact " +
                 "separating-axis test over this species' own measured sites and its own " +
                 "normals - Tools/Build/measure_mandelbulb_flora.py --fit, which fails the build " +
                 "if any neighbour pair interpenetrates.")]
        [SerializeField] Vector3 plateScale = new(0.71f, 0.71f, 0.248f);

        [Header("Growth")]
        [Tooltip("Maximum LIVE prisms this plant can hold. Consumption frees budget - a grazed " +
                 "plant regrows toward this cap instead of staying a permanent fragment. At the " +
                 "shipped pitch the largest element's bulb is 628 sites, so the default covers a " +
                 "complete form of every element with headroom.")]
        [SerializeField, Min(1)] int maxTotalSpawnedObjects = 660;

        /// <summary>The live-prism budget this individual resolved to - the base reads it for
        /// the reproduction maturity gate (see <see cref="Flora.PrismBudget"/>).</summary>
        protected override int PrismBudget => maxTotalSpawnedObjects;

        [Tooltip("Surface sites decided per grow tick.")]
        [SerializeField, Min(1)] int growthsPerTick = 6;

        [Tooltip("Instantiations executed per frame. The tick DECIDES (and claims sites); the " +
                 "drain spreads the prefab instantiation over frames - the same pacing contract " +
                 "AssembledFlora and PhyllotacticFlora use, throughput preserved.")]
        [SerializeField, Min(1)] int maxSpawnsPerFrame = 2;

        [SerializeField, Min(0f)] float plantRadius = 150f;

        /// <summary>
        /// This species' prism size is its LATTICE PITCH, so no per-individual or per-cell scale
        /// curve may touch it - <c>Flora.ApplyCellPrismScale</c> reads this and stands down.
        /// Scaling the leaf without scaling the lattice would tear the surface open; scaling both
        /// is what <see cref="FloraVariantTuning.LatticeScale"/> does, below.
        /// </summary>
        protected override bool PrismSizeFixedByGrowthRule => true;

        // ── State ─────────────────────────────────────────────────────────────

        MandelbulbLattice _lattice;
        Vector3 _axis = Vector3.up;

        /// <summary>Sites already laid, queued to be laid, or refused - the integer occupancy that
        /// makes a duplicate structurally impossible. No tolerance, no spatial hash, no float key.</summary>
        readonly HashSet<Vector3Int> _claimed = new();

        /// <summary>Shell sites discovered but not yet decided - the growth front.</summary>
        readonly Queue<Vector3Int> _frontier = new();

        /// <summary>What was actually laid where, so a grazed site can be freed and regrown.</summary>
        readonly Dictionary<Vector3Int, HealthPrism> _laid = new();

        struct SpawnOrder
        {
            public Vector3Int Site;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public float DecidedAt;
        }

        readonly Queue<SpawnOrder> _pending = new();

        // A Frenzy hold can outlive a spatial-index reservation; an order whose claim lapsed could
        // overlap another grower. Dropped at drain - the site is freed and simply re-decided.
        const float MaxOrderAgeSeconds = PrismSpatialIndex.ReservationTtlSeconds - 1f;

        /// <summary>World units between adjacent sites.</summary>
        float WorldPitch => latticePitch * shellRadius;

        /// <summary>Site index at which the march in <see cref="MandelbulbLattice.SeedSite"/> and
        /// the growth walk are bounded. The set is contained in radius ~1.33 at every power we
        /// author; the margin is insurance, not tuning.</summary>
        int MaxSiteRadius => Mathf.CeilToInt(1.45f / Mathf.Max(0.01f, latticePitch));

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void Initialize(Cell cell)
        {
            // Base plants us (honoring an authored site), seats the crystal and starts the grow
            // cadence. The first tick finds an empty frontier and no prisms, so it no-ops - the
            // same ordering BranchingFlora and PhyllotacticFlora rely on.
            base.Initialize(cell);

            _axis = GrowthUp;
            SafeLookRotation.TrySet(transform, _axis, transform);

            // AFTER base.Initialize: this is the first point at which Element is final (prefab ->
            // rolled variant -> cell overrides -> the crystal that carries it).
            _lattice = MandelbulbLattice.For(ResolvePower(), latticePitch, escapeIterations, bailout);

            var seed = _lattice.SeedSite(MaxSiteRadius);
            _claimed.Add(seed);
            _frontier.Enqueue(seed);
        }

        /// <summary>The fractal order this plant's element grows, falling back to the prefab's own
        /// <see cref="power"/> for a plant with no element.</summary>
        int ResolvePower()
        {
            if (formByElement != null)
                foreach (var entry in formByElement)
                    if (entry.Element == Element && entry.Power >= 2)
                        return entry.Power;
            return power;
        }

        /// <summary>
        /// Mandelbulb layer of the variant expression: the live-prism budget (the field every
        /// flora family reads) and <see cref="FloraVariantTuning.LatticeScale"/>, which here scales
        /// the whole plant - <see cref="shellRadius"/>, and therefore the world pitch and the
        /// prism with it - while leaving the integer lattice, the topology and the prism COUNT
        /// exactly unchanged. That is the field's documented meaning and it is safe here in a way
        /// it was not for the gyroid (Docs/ECOSYSTEM.md §34.8): this species has no
        /// absolute-distance coherence tolerances to drag out from under - occupancy is an integer
        /// test and the spatial claim radius is derived from the pitch, so both scale with it by
        /// construction. Note the cubic-volume consequence §34.8 records still applies: a uniform
        /// k-times scale is a k^3 volume change and lands on the cell's Frenzy ladder.
        /// </summary>
        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;

            if (tuning.MaxTotalSpawnedObjects >= 0) maxTotalSpawnedObjects = tuning.MaxTotalSpawnedObjects;
            // Cell density scalar, applied AFTER the absolute so it scales whatever budget won.
            // Round half UP explicitly: Mathf.RoundToInt is banker's rounding.
            if (tuning.MaxTotalSpawnedObjectsScale > 0f)
                maxTotalSpawnedObjects = Mathf.Max(1, Mathf.FloorToInt(
                    maxTotalSpawnedObjects * tuning.MaxTotalSpawnedObjectsScale + 0.5f));

            if (tuning.LatticeScale > 0f) shellRadius *= tuning.LatticeScale;
        }

        public override void Plant()
        {
            // A pinned site (a garden bed, or the Lifeform Matrix toy's spawn-here station) wins;
            // otherwise disperse across the cell like every other flora. Measured from the CELL
            // CENTRE (Flora.ResolvePlantCenter), never the crystal.
            if (TryGetPlantPositionOverride(out var pinned))
            {
                transform.position = pinned;
                return;
            }
            float radius = ResolvePlantRadius(legacyRadius: plantRadius);
            transform.position = ResolvePlantCenter() + radius * UnityEngine.Random.onUnitSphere;
        }

        // ── Growth ────────────────────────────────────────────────────────────

        public override void Grow()
        {
            // Live-prism budget (frees as fauna graze - a cropped plant regrows).
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects) return;

            // Frenzy gate: steady growth until the cell tops out, then freeze and resume when an
            // active force brings the mass back down. Cell.FloraGrowingEnabled is the only gate.
            if (cell && !cell.FloraGrowingEnabled) return;

            if (_lattice == null) return;

            if (_frontier.Count == 0)
            {
                // Reawakening: the plant has covered everything it could reach, or its front was
                // grazed off. Free the sites whose prisms are gone and re-open them - that is how
                // a cropped Mandelbulb heals back over itself instead of sitting inert.
                ReopenGrazedSites();
                return;
            }

            int decisions = Mathf.Min(growthsPerTick, _frontier.Count);
            for (int i = 0; i < decisions; i++)
                DecideSite(_frontier.Dequeue());
        }

        void DecideSite(Vector3Int site)
        {
            // Expand FIRST and unconditionally. Expansion is pure integer bookkeeping, so the
            // sweep must not be able to stall on a site whose prism could not be placed - a
            // region blocked by a neighbouring plant would otherwise cut this plant's surface in
            // two and leave the far side permanently unreachable.
            foreach (var d in MandelbulbLattice.Neighbour26)
            {
                var next = site + d;
                if (Mathf.Abs(next.x) > MaxSiteRadius || Mathf.Abs(next.y) > MaxSiteRadius ||
                    Mathf.Abs(next.z) > MaxSiteRadius) continue;
                if (_claimed.Contains(next)) continue;
                if (!_lattice.IsShellSite(next)) continue;
                _claimed.Add(next);
                _frontier.Enqueue(next);
            }

            if (_laid.ContainsKey(site)) return;

            Vector3 local = (Vector3)site * WorldPitch;
            Vector3 world = transform.TransformPoint(local);

            // Cross-PLANT occupancy. Within one plant a duplicate is already impossible (the
            // integer claim above); this is the only thing that can refuse a site, and a refusal
            // costs the site and nothing else.
            if (!Claim(world)) return;

            Vector3 normal = _lattice.Normal(site);
            _pending.Enqueue(new SpawnOrder
            {
                Site = site,
                LocalPosition = local,
                LocalRotation = PlateRotation(normal),
                DecidedAt = Time.time,
            });
        }

        /// <summary>
        /// A plate lies FLAT on the surface: its thin axis (local +z, the flora convention every
        /// lattice species uses) along the normal, its long axes spanning the surface. The tangent
        /// is derived from the site's own normal against the plant axis, with a fallback basis for
        /// the poles, so it is a pure function of the address - never a stored rotation
        /// (Docs/ECOSYSTEM.md §34: half the transforms on a lattice are reflections and a baked
        /// quaternion carried through one is silently wrong).
        /// </summary>
        static Quaternion PlateRotation(Vector3 normal)
        {
            Vector3 reference = Mathf.Abs(normal.z) > 0.95f ? Vector3.right : Vector3.forward;
            Vector3 tangent = Vector3.Cross(normal, reference);
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 1e-6f) return Quaternion.identity;
            return Quaternion.LookRotation(normal, tangent.normalized);
        }

        /// <summary>The plate's world size. Uniform across a plant and across its element - see
        /// the class remarks on why the element is expressed as the ORDER, not the leaf.</summary>
        Vector3 PlateSize()
        {
            Vector3 mult = plateScale;
            if (formByElement != null)
                foreach (var entry in formByElement)
                    if (entry.Element == Element && entry.Plate != Vector3.zero)
                    {
                        mult = entry.Plate;
                        break;
                    }

            float p = WorldPitch;
            return new Vector3(
                Mathf.Max(0.01f, p * mult.x),
                Mathf.Max(0.01f, p * mult.y),
                Mathf.Max(0.01f, p * mult.z));
        }

        bool Claim(Vector3 world)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return true;
            // Half the pitch: a legitimate NEIGHBOUR site is a full pitch away and must never be
            // blocked, a genuine duplicate is at zero distance and always must be.
            return index.TryReserve(world, Mathf.Max(1.5f, 0.45f * WorldPitch));
        }

        void ReopenGrazedSites()
        {
            if (_laid.Count == 0) return;

            List<Vector3Int> freed = null;
            foreach (var pair in _laid)
                if (!pair.Value)
                    (freed ??= new List<Vector3Int>()).Add(pair.Key);

            if (freed == null) return;
            // Still in _claimed (it was claimed when first laid), so only the _laid record has to
            // go: DecideSite re-lays a site it does not find there, and re-expanding its already
            // claimed neighbours is a no-op.
            foreach (var site in freed)
            {
                _laid.Remove(site);
                _frontier.Enqueue(site);
            }
        }

        // ── Drain ─────────────────────────────────────────────────────────────

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
                if (Time.time - order.DecidedAt > MaxOrderAgeSeconds)
                {
                    // The spatial reservation lapsed. Free the site so a later tick re-decides it
                    // rather than leaving a permanent hole in the surface.
                    _claimed.Remove(order.Site);
                    continue;
                }
                Execute(order);
                spawned++;
            }
        }

        void Execute(SpawnOrder order)
        {
            // A spindle per site, parented to the plant root - NOT to a prism. A prism carries its
            // authored leaf as its localScale, and a non-uniform scale above a rotated child is a
            // shear (Docs/ECOSYSTEM.md §37.9). Flat rather than chained because this plant has no
            // chain: sites are addresses on a shell, not a descent, so there is no parent spindle
            // for a site to belong to and a chained hierarchy would invent a lineage the growth
            // rule does not have.
            var newSpindle = Instantiate(spindle, transform);
            newSpindle.LifeForm = this;
            newSpindle.transform.localPosition = order.LocalPosition;
            newSpindle.transform.localRotation = order.LocalRotation;
            AddSpindle(newSpindle);

            var leaf = EnvironmentPrismPool.Get(healthPrism,
                newSpindle.transform.position, newSpindle.transform.rotation);
            if (!leaf)
            {
                _claimed.Remove(order.Site);
                return;
            }

            leaf.transform.SetParent(newSpindle.transform, false);
            leaf.transform.localPosition = Vector3.zero;
            leaf.transform.localRotation = Quaternion.identity;
            leaf.LifeForm = this;
            leaf.ChangeTeam(domain);

            _pendingPrismScale = PlateSize();
            AddHealthBlock(leaf);
            leaf.Initialize("flora");

            _laid[order.Site] = leaf;

            // Growth is this plant's feeding - see Flora.NotifyGrew.
            NotifyGrew();
        }

        // The scale the next AddHealthBlock should apply. Flora.AddHealthBlock stamps every prism
        // with the one authored leafSize; this species' prism size IS its world pitch, so it
        // overrides that stamp for the prism it is currently placing.
        Vector3? _pendingPrismScale;

        public override void AddHealthBlock(HealthPrism healthPrism)
        {
            base.AddHealthBlock(healthPrism);
            if (healthPrism && _pendingPrismScale.HasValue)
            {
                // AdmitTargetScale first: the plate is STATED (derived from the pitch), not grown
                // into, so it has to survive PrismScaleAnimator's silent per-axis [0.5, 10] clamp
                // - a thin plate's z axis sits under that floor at every pitch we author, and the
                // clamp has no log and no return value (Docs/ECOSYSTEM.md §34.9).
                healthPrism.AdmitTargetScale(_pendingPrismScale.Value);
                healthPrism.TargetScale = _pendingPrismScale.Value;
            }
            _pendingPrismScale = null;
        }

        // ── Preview ───────────────────────────────────────────────────────────

        /// <summary>
        /// Pure preview of the surface walk - see <see cref="Flora.TryPreviewGrowth"/>. Mirrors
        /// <see cref="Grow"/> exactly, and unlike the other families it is EXACT rather than
        /// merely representative: this growth rule contains no randomness at all, so the icon a
        /// player sees is the plant they will meet, site for site. The seed is consumed only to
        /// satisfy the contract.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0) return false;

            var lattice = MandelbulbLattice.For(ResolvePower(), latticePitch, escapeIterations, bailout);
            var size = PlateSize();
            float pitch = WorldPitch;
            int bound = MaxSiteRadius;

            var claimed = new HashSet<Vector3Int>();
            var frontier = new Queue<Vector3Int>();
            var start = lattice.SeedSite(bound);
            claimed.Add(start);
            frontier.Enqueue(start);

            while (frontier.Count > 0 && into.Count < budget)
            {
                var site = frontier.Dequeue();
                into.Add(new SpawnPoint((Vector3)site * pitch, PlateRotation(lattice.Normal(site)), size));

                foreach (var d in MandelbulbLattice.Neighbour26)
                {
                    var next = site + d;
                    if (Mathf.Abs(next.x) > bound || Mathf.Abs(next.y) > bound ||
                        Mathf.Abs(next.z) > bound) continue;
                    if (!claimed.Add(next)) continue;
                    if (!lattice.IsShellSite(next)) continue;
                    frontier.Enqueue(next);
                }
            }

            return into.Count > 0;
        }
    }
}
