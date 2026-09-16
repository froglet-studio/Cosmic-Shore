using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A plant that grows over the surface of a <b>Mandelbulb</b> - the escape-time fractal of
    /// <c>v -> v^n + c</c> in triplex coordinates - spreading from a single seed at its footing
    /// until it has covered the form or run out of budget.
    ///
    /// <b>Why a fourth flora growth family.</b> The three we had each answer a different question
    /// about shape, and none of them can answer this one. <see cref="AssembledFlora"/> crystallises
    /// a lattice - it needs an exact tile and a bond table, which a fractal boundary does not have
    /// and cannot be given (see <see cref="MandelbulbLattice"/>). <see cref="BranchingFlora"/> and
    /// <see cref="PhyllotacticFlora"/> grow by advancing headings through open space, so the shape
    /// they make is a property of their own walk; here the shape is a property of a FUNCTION, and
    /// the plant's job is only to discover it.
    ///
    /// <para><b>The plant is not a skin, and that is the whole design.</b> A voxel shell of a
    /// blobby solid IS a blobby solid: plating every surface cell with one prism size produced a
    /// closed crust that read as a lumpy sphere no matter how fine the lattice got, because a
    /// Mandelbulb's form is its TERRACING and a closed skin hides terracing by definition. So two
    /// things changed and both are measurements rather than authored shapes. The plant grows on
    /// the terrace RISERS only - a cell whose surface faces sideways relative to the radial - so
    /// the treads are open and you see into the object. And a plate is not a cell: adjacent
    /// riser cells with agreeing normals MERGE into one coplanar patch, and one prism stands for
    /// the whole patch, sized, elongated and oriented by fitting that patch. A long flat riser
    /// becomes one long plate; a twisting seam stays a scatter of chips. Prism size therefore
    /// spans about 16x within a single plant, its aspect runs from square to a 4:1 strut, and
    /// every prism carries its own frame - the layers of texture at different scales the form
    /// needs, none of which is a number anybody typed. The rule lives in
    /// <see cref="MandelbulbLattice.Plating"/>, which is pure and provable outside Unity.</para>
    ///
    /// <para><b>What emerges and what is written down.</b> Nothing in this file describes a bulb.
    /// A cell is inside iff the orbit stays bounded; a cell is plated iff its exposed faces point
    /// sideways; plates are patches of agreeing cells. The lobes, the polar cup and the terracing
    /// are what those rules leave behind - the same claim, and the same kind of claim, the gyroid
    /// octagon colony makes (Docs/ECOSYSTEM.md §32.7).</para>
    ///
    /// <para><b>The element is the fractal ORDER.</b> Docs/ECOSYSTEM.md §40: a lifeform is its
    /// species and its element and nothing else, and everything an element states about itself it
    /// states exactly once. Here an element states its <i>power</i> - Charge grows the classic
    /// 8th-order bulb, the others grow genuinely different solids. Resolved in
    /// <see cref="Initialize"/> AFTER <c>base.Initialize</c>, the one point where the prefab, the
    /// rolled variant, the cell overrides and the crystal carrying the element have all landed -
    /// the same choke point <c>Flora.ResolveShieldPeriod</c> uses, and for the same reason: the
    /// cadence is authored per CONFIG while the element is ROLLED per plant.</para>
    ///
    /// <para>Everything else is inherited and unchanged: prisms are conserved mass laid through
    /// the ordinary health-prism path, growth is gated on <c>Cell.FloraGrowingEnabled</c>, the
    /// live-prism budget frees as fauna graze so a cropped plant regrows, sites are claimed in
    /// <c>PrismSpatialIndex</c> before the spawn, death withers spindle-by-spindle and drops the
    /// elemental crystal. No clock removes anything.</para>
    /// </summary>
    public class MandelbulbFlora : Flora
    {
        /// <summary>One element's FORM: the fractal order it grows, and a scale on the prisms it
        /// grows it out of. Authored on the PREFAB rather than on the four element configs because
        /// the config's element is rolled per plant, so no per-element asset field can reach a
        /// config that rolls (Docs/ECOSYSTEM.md §38's argument, applied to shape instead of tempo).
        ///
        /// <para>The scale is per-element for exactly one reason and it is a geometric one:
        /// <b>a CHARGE plant's leaves are shielded by law</b> (<c>Flora.ResolveShieldPeriod</c>),
        /// and a shield replaces the box with the octahedron CIRCUMSCRIBING it - 3x the
        /// half-extents, reaching 1.5 x leafSize (Docs/ECOSYSTEM.md §35). So Charge is fitted
        /// against its ARMOUR and comes out smaller: its plates read as a sparse skeleton and its
        /// octahedra fill the form in, which is exactly the outcome the gyroid and Schwarz P
        /// Charge variants shipped with. Zero means 1.</para></summary>
        [Serializable]
        public struct ElementalForm
        {
            public Element Element;
            [Range(2, 16)] public int Power;
            [Tooltip("Uniform scale on every prism of this element. 0 = 1. Below 1 only for an " +
                     "element fitted against its ARMOUR - see the struct remarks.")]
            public float PlateScale;
        }

        [Header("Form - the set")]
        [Tooltip("Per-element FORM - the exponent in v -> v^n + c. 8 is the classic Mandelbulb. " +
                 "An element not listed here falls back to the values below.")]
        [SerializeField] ElementalForm[] formByElement =
        {
            new() { Element = Element.Charge, Power = 8,  PlateScale = 0.5f },
            new() { Element = Element.Mass,   Power = 5,  PlateScale = 0f },
            new() { Element = Element.Space,  Power = 3,  PlateScale = 0f },
            new() { Element = Element.Time,   Power = 12, PlateScale = 0f },
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
                 "surface cells grow as 1/pitch^2, because the plant is a SURFACE. It is NOT the " +
                 "prism size - prisms are fitted to merged patches of these cells, so a finer " +
                 "pitch buys DETAIL rather than more prisms one-for-one.")]
        [SerializeField, Range(0.012f, 0.30f)] float latticePitch = 0.027f;

        [Tooltip("World radius of the set's unit sphere - how big one plant is. World cell " +
                 "spacing is latticePitch x this, and every prism dimension is a multiple of it.")]
        [SerializeField, Min(1f)] float shellRadius = 75f;

        [Header("Form - the plating")]
        [Tooltip("A cell is plated iff 1 - |normal . radial| reaches this, i.e. its surface faces " +
                 "SIDEWAYS - a terrace riser or a crease wall. Lower closes the plant into a " +
                 "skin, which reads as a ball; higher opens it until the silhouette goes.")]
        [SerializeField, Range(0f, 0.9f)] float riserBias = 0.30f;

        [Tooltip("Merge admits a neighbouring cell whose normal is within this cosine of the " +
                 "patch's running mean. This is the dial that makes flat risers into long plates.")]
        [SerializeField, Range(0.3f, 0.999f)] float coplanarCos = 0.86f;

        [Tooltip("Largest RMS deviation of a patch from its own fitted plane, in CELLS. One plate " +
                 "cannot stand for a patch that is not flat, whatever its normals say.")]
        [SerializeField, Min(0.05f)] float planarTau = 0.62f;

        [Tooltip("The largest patch one plate may stand for, in cells - the only ceiling on prism " +
                 "size. Everything under it is decided by the surface.")]
        [SerializeField, Min(1)] int maxPatchCells = 22;

        [Tooltip("Plate size as a fraction of its patch's own measured span. Below 1 by design: " +
                 "the gaps it opens are what you see the inner structure through.")]
        [SerializeField, Range(0.2f, 1.2f)] float platePad = 0.74f;

        [Tooltip("Plate thickness in CELLS - absolute, not a fraction of the plate. A " +
                 "proportional thickness makes a long plate a slab, and a slab's volume lands on " +
                 "the cell's Frenzy ladder as the cube of its length.")]
        [SerializeField, Min(0.05f)] float plateThickness = 0.38f;

        [Tooltip("A candidate plate whose separating-axis scale against one already laid falls " +
                 "below this is not laid at all - hidden mass buys nothing, and this is also the " +
                 "bound on how deeply any two prisms in a plant interleave. 0 disables it.")]
        [SerializeField, Range(0f, 1f)] float containDrop = 0.62f;

        [Header("Growth")]
        [Tooltip("Maximum LIVE prisms this plant can hold. Consumption frees budget - a grazed " +
                 "plant regrows toward this cap instead of staying a permanent fragment. At the " +
                 "shipped constants the largest element's form is 2715 plates, so the default " +
                 "covers a complete form of every element with headroom.")]
        [SerializeField, Min(1)] int maxTotalSpawnedObjects = 2800;

        /// <summary>The live-prism budget this individual resolved to - the base reads it for
        /// the reproduction maturity gate (see <see cref="Flora.PrismBudget"/>).</summary>
        protected override int PrismBudget => maxTotalSpawnedObjects;

        [Tooltip("Plates decided per grow tick.")]
        [SerializeField, Min(1)] int growthsPerTick = 6;

        [Tooltip("Instantiations executed per frame. The tick DECIDES (and claims sites); the " +
                 "drain spreads the prefab instantiation over frames - the same pacing contract " +
                 "AssembledFlora and PhyllotacticFlora use, throughput preserved.")]
        [SerializeField, Min(1)] int maxSpawnsPerFrame = 2;

        [SerializeField, Min(0f)] float plantRadius = 150f;

        /// <summary>
        /// This species' prism size is a measurement of its own lattice, so no per-individual or
        /// per-cell scale curve may touch it - <c>Flora.ApplyCellPrismScale</c> reads this and
        /// stands down. Scaling the prisms without scaling the lattice would tear the surface
        /// open; scaling both is what <see cref="FloraVariantTuning.LatticeScale"/> does, below.
        /// </summary>
        protected override bool PrismSizeFixedByGrowthRule => true;

        // ── State ─────────────────────────────────────────────────────────────

        MandelbulbLattice _lattice;
        IReadOnlyList<MandelbulbLattice.Plate> _plates;
        Vector3 _axis = Vector3.up;
        float _plateScale = 1f;

        /// <summary>How far down <see cref="_plates"/> the plant has grown. The plating is one
        /// deterministic list in growth order, so this index IS the front - there is no frontier
        /// to keep and a duplicate is structurally impossible.</summary>
        int _next;

        /// <summary>Plates whose prism was grazed away and which are open to regrow.</summary>
        readonly Queue<int> _regrow = new();

        /// <summary>What was actually laid where, so a grazed plate can be freed and regrown.</summary>
        readonly Dictionary<int, HealthPrism> _laid = new();

        struct SpawnOrder
        {
            public int Index;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 Size;
            public float DecidedAt;
        }

        readonly Queue<SpawnOrder> _pending = new();

        // A Frenzy hold can outlive a spatial-index reservation; an order whose claim lapsed could
        // overlap another grower. Dropped at drain - the plate is freed and simply re-decided.
        const float MaxOrderAgeSeconds = PrismSpatialIndex.ReservationTtlSeconds - 1f;

        /// <summary>World units between adjacent lattice cells, and the unit every prism
        /// dimension is quoted in.</summary>
        float WorldPitch => latticePitch * shellRadius;

        /// <summary>Site index at which the march in <see cref="MandelbulbLattice.SeedSite"/> and
        /// the plating walk are bounded. The set is contained in radius ~1.33 at every power we
        /// author; the margin is insurance, not tuning.</summary>
        int MaxSiteRadius => Mathf.CeilToInt(1.45f / Mathf.Max(0.005f, latticePitch));

        MandelbulbLattice.PlatingRules Rules => new(
            riserBias, coplanarCos, planarTau, maxPatchCells,
            platePad, plateThickness, containDrop, MaxSiteRadius);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void Initialize(Cell cell)
        {
            // Base plants us (honoring an authored site), seats the crystal and starts the grow
            // cadence. The first tick runs before the plating exists and no-ops - the same
            // ordering BranchingFlora and PhyllotacticFlora rely on.
            base.Initialize(cell);

            _axis = GrowthUp;
            SafeLookRotation.TrySet(transform, _axis, transform);

            // AFTER base.Initialize: this is the first point at which Element is final (prefab ->
            // rolled variant -> cell overrides -> the crystal that carries it).
            _lattice = MandelbulbLattice.For(ResolvePower(), latticePitch, escapeIterations, bailout);
            _plateScale = ResolvePlateScale();
            _plates = _lattice.Plating(Rules);
            _next = 0;
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

        /// <summary>This element's uniform prism scale - 1 unless the element was fitted against
        /// something other than its own box (today, Charge against its shield octahedron).</summary>
        float ResolvePlateScale()
        {
            if (formByElement != null)
                foreach (var entry in formByElement)
                    if (entry.Element == Element && entry.PlateScale > 0f)
                        return entry.PlateScale;
            return 1f;
        }

        /// <summary>
        /// Mandelbulb layer of the variant expression: the live-prism budget (the field every
        /// flora family reads) and <see cref="FloraVariantTuning.LatticeScale"/>, which here scales
        /// the whole plant - <see cref="shellRadius"/>, and therefore the world pitch and every
        /// prism with it - while leaving the integer lattice, the plating and the prism COUNT
        /// exactly unchanged. That is the field's documented meaning and it is safe here in a way
        /// it was not for the gyroid (Docs/ECOSYSTEM.md §34.8): this species has no
        /// absolute-distance coherence tolerances to drag out from under - the plating rule is
        /// stated entirely in CELLS and the spatial claim radius is derived from the pitch, so
        /// both scale with it by construction. Note the cubic-volume consequence §34.8 records
        /// still applies: a uniform k-times scale is a k^3 volume change and lands on the cell's
        /// Frenzy ladder.
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

            if (_plates == null || _plates.Count == 0) return;

            for (int i = 0; i < growthsPerTick; i++)
            {
                if (_regrow.Count > 0) { Decide(_regrow.Dequeue()); continue; }
                if (_next >= _plates.Count)
                {
                    // Covered everything it could, or its front was grazed off. Free the plates
                    // whose prisms are gone and re-open them - that is how a cropped Mandelbulb
                    // heals back over itself instead of sitting inert.
                    ReopenGrazedPlates();
                    return;
                }
                Decide(_next++);
            }
        }

        void Decide(int index)
        {
            if (_laid.ContainsKey(index)) return;

            var plate = _plates[index];
            float pitch = WorldPitch;
            Vector3 local = plate.Centre * pitch;
            Vector3 world = transform.TransformPoint(local);

            // Cross-PLANT occupancy. Within one plant a duplicate is already impossible (the
            // plating is a list and the index is the claim); this is the only thing that can
            // refuse a plate, and a refusal costs the plate and nothing else.
            if (!Claim(world)) return;

            _pending.Enqueue(new SpawnOrder
            {
                Index = index,
                LocalPosition = local,
                LocalRotation = PlateRotation(plate),
                Size = PlateSize(plate, pitch),
                DecidedAt = Time.time,
            });
        }

        /// <summary>
        /// A plate lies FLAT on the surface: its thin axis (local +z, the flora convention every
        /// lattice species uses) along the patch's normal, its long axes along the patch's own
        /// principal directions. Composed from the basis the plating measured rather than stored
        /// as a rotation, because half the frames on a surface are reflections and a baked
        /// quaternion carried through one is silently wrong (Docs/ECOSYSTEM.md §34).
        /// </summary>
        static Quaternion PlateRotation(in MandelbulbLattice.Plate plate)
        {
            if (plate.Forward.sqrMagnitude < 1e-6f || plate.Up.sqrMagnitude < 1e-6f)
                return Quaternion.identity;
            // LookRotation(forward, up) puts local +z on Forward and local +y on Up, which leaves
            // local +x on Up x Forward - the plate's own Right, by construction of the basis.
            return Quaternion.LookRotation(plate.Forward, plate.Up);
        }

        /// <summary>The plate's world size: what the plating measured, in world units, scaled by
        /// whatever this element was fitted at.</summary>
        Vector3 PlateSize(in MandelbulbLattice.Plate plate, float pitch)
        {
            float k = pitch * _plateScale;
            return new Vector3(
                Mathf.Max(0.01f, plate.Size.x * k),
                Mathf.Max(0.01f, plate.Size.y * k),
                Mathf.Max(0.01f, plate.Size.z * k));
        }

        bool Claim(Vector3 world)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return true;
            // Deliberately well under a cell: two plates on opposite walls of a thin fin sit
            // barely more than one cell apart, and refusing one of those would punch a hole in
            // the plant's own surface. A genuine duplicate is at zero distance either way.
            return index.TryReserve(world, Mathf.Max(1.5f, 0.3f * WorldPitch));
        }

        void ReopenGrazedPlates()
        {
            if (_laid.Count == 0) return;

            List<int> freed = null;
            foreach (var pair in _laid)
                if (!pair.Value)
                    (freed ??= new List<int>()).Add(pair.Key);

            if (freed == null) return;
            foreach (int index in freed)
            {
                _laid.Remove(index);
                _regrow.Enqueue(index);
            }
        }

        // ── Drain ─────────────────────────────────────────────────────────────

        void Update()
        {
            if (_pending.Count == 0) return;
            // Parity with the WaitForSeconds grow loop: frozen at timeScale 0 (menu pause).
            if (Time.timeScale <= 0f) return;
            // Orders decided just before Frenzy WAIT here and execute when growing re-enables -
            // the same freeze-and-resume the tick gate gives.
            if (cell && !cell.FloraGrowingEnabled) return;

            int spawned = 0;
            while (spawned < maxSpawnsPerFrame && _pending.Count > 0)
            {
                var order = _pending.Dequeue();
                if (Time.time - order.DecidedAt > MaxOrderAgeSeconds)
                {
                    // The spatial reservation lapsed. Re-open the plate so a later tick re-decides
                    // it rather than leaving a permanent hole in the surface.
                    _regrow.Enqueue(order.Index);
                    continue;
                }
                Execute(order);
                spawned++;
            }
        }

        void Execute(SpawnOrder order)
        {
            // A spindle per plate, parented to the plant root - NOT to a prism. A prism carries
            // its measured plate as its localScale, and a non-uniform scale above a rotated child
            // is a shear (Docs/ECOSYSTEM.md §37.9). Flat rather than chained because this plant
            // has no chain: plates are patches on a shell, not a descent, so there is no parent
            // spindle for one to belong to and a chained hierarchy would invent a lineage the
            // growth rule does not have.
            var newSpindle = Instantiate(spindle, transform);
            newSpindle.LifeForm = this;
            newSpindle.transform.localPosition = order.LocalPosition;
            newSpindle.transform.localRotation = order.LocalRotation;
            AddSpindle(newSpindle);

            var leaf = EnvironmentPrismPool.Get(healthPrism,
                newSpindle.transform.position, newSpindle.transform.rotation);
            if (!leaf)
            {
                _regrow.Enqueue(order.Index);
                return;
            }

            leaf.transform.SetParent(newSpindle.transform, false);
            leaf.transform.localPosition = Vector3.zero;
            leaf.transform.localRotation = Quaternion.identity;
            leaf.LifeForm = this;
            leaf.ChangeTeam(domain);

            _pendingPrismScale = order.Size;
            AddHealthBlock(leaf);
            leaf.Initialize("flora");

            _laid[order.Index] = leaf;

            // Growth is this plant's feeding - see Flora.NotifyGrew.
            NotifyGrew();
        }

        // The scale the next AddHealthBlock should apply. Flora.AddHealthBlock stamps every prism
        // with the one authored leafSize; this species measures a size per prism, so it overrides
        // that stamp for the prism it is currently placing.
        Vector3? _pendingPrismScale;

        public override void AddHealthBlock(HealthPrism healthPrism)
        {
            base.AddHealthBlock(healthPrism);
            if (healthPrism && _pendingPrismScale.HasValue)
            {
                // AdmitTargetScale first: the plate is STATED (measured from the patch), not grown
                // into, so it has to survive PrismScaleAnimator's silent per-axis [0.5, 10] clamp
                // - this species' plates run from a couple of units to thirty, i.e. over the
                // ceiling at one end, and the clamp has no log and no return value
                // (Docs/ECOSYSTEM.md §34.9).
                healthPrism.AdmitTargetScale(_pendingPrismScale.Value);
                healthPrism.TargetScale = _pendingPrismScale.Value;
            }
            _pendingPrismScale = null;
        }

        // ── Preview ───────────────────────────────────────────────────────────

        /// <summary>
        /// Pure preview of the plating - see <see cref="Flora.TryPreviewGrowth"/>. Mirrors
        /// <see cref="Grow"/> exactly, and unlike the other families it is EXACT rather than
        /// merely representative: this growth rule contains no randomness at all, so the icon a
        /// player sees is the plant they will meet, prism for prism. The seed is consumed only to
        /// satisfy the contract.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0) return false;

            var lattice = MandelbulbLattice.For(ResolvePower(), latticePitch, escapeIterations, bailout);
            var plates = lattice.Plating(Rules);
            float pitch = WorldPitch;
            float scale = ResolvePlateScale();

            int count = Mathf.Min(budget, plates.Count);
            for (int i = 0; i < count; i++)
            {
                var plate = plates[i];
                into.Add(new SpawnPoint(plate.Centre * pitch, PlateRotation(plate),
                                        plate.Size * (pitch * scale)));
            }
            return into.Count > 0;
        }
    }
}
