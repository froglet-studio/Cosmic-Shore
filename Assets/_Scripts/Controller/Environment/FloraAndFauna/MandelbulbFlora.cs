using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A plant that grows as a cage of CURVES traced over the surface of a <b>Mandelbulb</b> —
    /// the escape-time fractal of <c>v -> v^n + c</c> in triplex coordinates. It is the fourth
    /// flora growth family, and the whole of its rule lives in <see cref="MandelbulbSurface"/>,
    /// which is a pure file so the offline verifier can COMPILE AND RUN it rather than read it.
    ///
    /// <para><b>Why a fourth family.</b> <see cref="AssembledFlora"/> crystallises a lattice and
    /// needs an exact tile and a measured bond table, which a fractal boundary does not have and
    /// cannot be given. <see cref="BranchingFlora"/> and <see cref="PhyllotacticFlora"/> advance
    /// headings through open space, so the shape they make is a property of their own walk; here
    /// the shape is a property of a FUNCTION and the plant's job is only to discover it.</para>
    ///
    /// <para><b>It is not a skin, and that is still the whole design.</b> The first cut of this
    /// species plated every surface cell and read as a lumpy sphere at any resolution, because a
    /// closed crust of a solid form IS that solid (Docs/ECOSYSTEM.md §50.2 lists the four
    /// closed-surface candidates built and rejected). What replaced it is anisotropic by
    /// construction: prisms are laid DENSELY along a curve and SPARSELY across it, so a plant is
    /// a lattice of open ribbons you see the fractal through. A curve the rule cannot follow
    /// cleanly — too much turn, out of the radius band — is ABANDONED rather than plated, which
    /// is what leaves the dust and the singular zones unsampled.</para>
    ///
    /// <para><b>The fractal is never evaluated here.</b> It is ray-marched offline into a
    /// spherical height field and fitted to spherical harmonics
    /// (<c>Tools/Build/bake_mandelbulb_surface.py</c>); the shipped table is 289 coefficients per
    /// vector, four vectors per element. A plant reconstructs that once and walks it. The three
    /// modes are <c>dR/dc</c>, so a plant is the shared basis plus THREE WEIGHTS and every plant
    /// in a cell is a genuinely different member of the same fractal family rather than a
    /// rotation of one bake.</para>
    ///
    /// <para><b>The element is the fractal ORDER</b> (Docs/ECOSYSTEM.md §40: a lifeform is its
    /// species and its element and nothing else). Charge grows the classic 8th-order bulb, Mass
    /// the 5th, Space the 3rd, Time the 12th — and each also carries its own curve family, so the
    /// four read as four plants rather than four sizes of one. Resolved in
    /// <see cref="Initialize"/> AFTER <c>base.Initialize</c>, the one point where the prefab, the
    /// rolled variant, the cell overrides and the crystal carrying the element have all landed —
    /// the same choke point <c>Flora.ResolveShieldPeriod</c> uses, and for the same reason.</para>
    ///
    /// <para>Everything else is inherited and unchanged: prisms are conserved mass laid through
    /// the ordinary health-prism path, growth is gated on <c>Cell.FloraGrowingEnabled</c>, the
    /// live-prism budget frees as fauna graze so a cropped plant regrows, sites are claimed in
    /// <c>PrismSpatialIndex</c> before the spawn, death withers spindle-by-spindle and drops the
    /// elemental crystal. No clock removes anything.</para>
    /// </summary>
    public class MandelbulbFlora : Flora
    {
        /// <summary>
        /// One element's FORM: which curve family it grows and how thick the ribbon is. Authored
        /// on the PREFAB rather than on the four element configs, because a config's element is
        /// ROLLED per plant so no per-element asset field can reach a config that rolls
        /// (Docs/ECOSYSTEM.md §38's argument, applied to shape instead of tempo).
        ///
        /// <para>The cross-section is per-element for one geometric reason: <b>a CHARGE plant's
        /// leaves are shielded by law</b> (<c>Flora.ResolveShieldPeriod</c>), and a shield
        /// replaces the box with the octahedron CIRCUMSCRIBING it — 3x the half-extents, reaching
        /// 1.5 x leafSize (Docs/ECOSYSTEM.md §35). Charge is therefore fitted against its ARMOUR
        /// and comes out thinner: its ribbons read as a sparse skeleton and its octahedra fill the
        /// cage in, exactly as the gyroid and Schwarz P Charge variants shipped.</para>
        /// </summary>
        [Serializable]
        public struct ElementalForm
        {
            public Element Element;

            [Tooltip("Ribbon cross-section in SURFACE units (the unit sphere), x = width across " +
                     "the curve, y = thickness through it. Length comes from the curve itself.")]
            public Vector2 CrossSection;

            public MandelbulbSurface.GrowthRules Rules;
        }

        [Header("Form")]
        [Tooltip("Per-element curve family. An element not listed here falls back to the first " +
                 "entry, which is also what a plant with no element grows.")]
        [SerializeField] ElementalForm[] formByElement = Array.Empty<ElementalForm>();

        [Tooltip("World radius of the surface's unit sphere — how big one plant is. Every prism " +
                 "dimension is a multiple of it, so this is a k^3 volume dial and lands on the " +
                 "cell's Frenzy ladder (Docs/ECOSYSTEM.md §34.8).")]
        [SerializeField, Min(1f)] float shellRadius = 75f;

        [Tooltip("Reconstruction lattice for the height field. Coefficients are grid-independent, " +
                 "so this trades memory against how finely the curve walk can read the surface; " +
                 "it is NOT the offline fit's grid.")]
        [SerializeField, Min(32)] int fieldWidth = 192;

        [Header("Form — which member of the family")]
        [Tooltip("Half-width of the per-plant weight draw. 0 makes every plant of an element the " +
                 "SAME bulb; 1 spans the basin the three modes were measured over. Past ~1.5 the " +
                 "first-order expansion stops describing a real fractal surface.")]
        [SerializeField, Range(0f, 1.5f)] float weightSpread = 1f;

        [Tooltip("Weights are quantised to this many steps per axis so plants SHARE reconstructed " +
                 "surfaces. 1 collapses every plant onto the mean.")]
        [SerializeField, Range(1, 9)] int weightSteps = 3;

        [Header("Growth")]
        [Tooltip("Maximum LIVE prisms this plant can hold. Consumption frees budget — a grazed " +
                 "plant regrows toward this cap instead of staying a permanent fragment.")]
        [SerializeField, Min(1)] int maxTotalSpawnedObjects = 2800;

        /// <summary>The live-prism budget this individual resolved to — the base reads it for the
        /// reproduction maturity gate (see <see cref="Flora.PrismBudget"/>).</summary>
        protected override int PrismBudget => maxTotalSpawnedObjects;

        [Tooltip("Prisms decided per grow tick.")]
        [SerializeField, Min(1)] int growthsPerTick = 8;

        [Tooltip("Instantiations executed per frame. The tick DECIDES (and claims sites); the " +
                 "drain spreads the prefab instantiation over frames — the same pacing contract " +
                 "AssembledFlora and PhyllotacticFlora use, throughput preserved.")]
        [SerializeField, Min(1)] int maxSpawnsPerFrame = 3;

        [SerializeField, Min(0f)] float plantRadius = 150f;

        /// <summary>
        /// This species' prism size is a measurement of its own curve, so no per-individual or
        /// per-cell scale curve may touch it — <c>Flora.ApplyCellPrismScale</c> reads this and
        /// stands down. <see cref="FloraVariantTuning.LatticeScale"/> is the sanctioned dial and
        /// scales <see cref="shellRadius"/>, which moves the surface and every prism on it
        /// together; there is nothing here written as an absolute distance for it to tear apart
        /// (Docs/ECOSYSTEM.md §34.8).
        /// </summary>
        protected override bool PrismSizeFixedByGrowthRule => true;

        // ── State ─────────────────────────────────────────────────────────────

        MandelbulbSurface.Surface _surface;
        MandelbulbSurface.Growth _growth;
        MandelbulbSurface.Frame _frame;
        ElementalForm _form;
        Vector3 _axis = Vector3.up;

        /// <summary>Every address the rule has produced so far, in growth order. The generator is
        /// LAZY — it traces one curve at a time so a plant reaching its budget never costs a
        /// visible hitch — and this list is what lets a grazed prism be regrown without
        /// re-tracing: an index into it IS the claim, so a duplicate is structurally impossible.
        /// </summary>
        readonly List<MandelbulbSurface.PrismAddress> _addresses = new();

        /// <summary>
        /// For each address, the index of the address it GROWS OUT OF — <c>-1</c> for the one
        /// prism that grows out of the heart. Parallel to <see cref="_addresses"/> because a
        /// parent is a property of the growth ORDER rather than of a prism's pose, and folding it
        /// into <see cref="MandelbulbSurface.PrismAddress"/> would make
        /// <c>MandelbulbSurface.Pose</c> stop being a pure function of one address.
        ///
        /// <para>The generator guarantees <c>_parent[i] &lt; i</c>, so laying in list order is a
        /// growth order by construction: nothing is ever hung off something that does not exist
        /// yet. What it cannot guarantee is that the parent was actually LAID — the claim refuses
        /// roughly a third of candidates — which is what <see cref="ResolveAnchorWorld"/> is for.
        /// </para>
        /// </summary>
        readonly List<int> _parent = new();

        /// <summary>Addresses whose prism was grazed away and which are open to regrow.</summary>
        readonly Queue<int> _regrow = new();

        /// <summary>What was actually laid where, so a grazed prism can be freed and regrown.</summary>
        readonly Dictionary<int, HealthPrism> _laid = new();

        /// <summary>
        /// The LIMB carrying each laid address — the spindle spanning the bond from its anchor to
        /// it. Kept across grazing for the reason <see cref="BorromeanFlora"/> records: a branch
        /// whose leaf was eaten is still a branch, and re-using it is what stops a regrowth
        /// minting a second spindle on one bond. It is RE-POSED on re-use rather than assumed
        /// still correct, because a regrown prism's nearest standing ancestor may have changed.
        /// </summary>
        readonly Dictionary<int, Spindle> _limb = new();

        /// <summary>
        /// How far the spindle prefab's own branch geometry reaches along its local +z, and the
        /// prefab's per-child z scales that reach was measured at. Measured once per PREFAB
        /// rather than authored — the number is a property of a mesh and a transform chain, and a
        /// constant copied out of an asset is true only on the day it is copied. This mirrors
        /// <see cref="BorromeanFlora"/>'s measurement deliberately: the two species share the
        /// contract (a spindle is a bond) and not a base class, and a shared helper would have to
        /// live on <c>Flora</c>, which every family that does NOT pose limbs this way also
        /// inherits.
        /// </summary>
        static readonly Dictionary<int, BranchGeometry> BranchByPrefab = new();
        static bool _warnedNoBranch;

        readonly struct BranchGeometry
        {
            public readonly float Reach;
            public readonly float[] ChildScaleZ;
            public BranchGeometry(float reach, float[] childScaleZ)
            {
                Reach = reach;
                ChildScaleZ = childScaleZ;
            }
        }

        /// <summary>Reconstructed height fields, shared across plants of the same element and
        /// quantised weights. Bounded rather than unbounded: a field is ~74 KB at the shipped
        /// grid, and a cell can hold more distinct (element, weights) pairs than it is worth
        /// caching.</summary>
        static readonly Dictionary<long, MandelbulbSurface.Surface> SurfaceCache = new();
        const int SurfaceCacheLimit = 8;

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
        // overlap another grower. Dropped at drain — the address is freed and simply re-decided.
        const float MaxOrderAgeSeconds = PrismSpatialIndex.ReservationTtlSeconds - 1f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void Initialize(Cell cell)
        {
            // Base plants us (honoring an authored site), seats the crystal and starts the grow
            // cadence. The first tick runs before the surface exists and no-ops — the same
            // ordering BranchingFlora and PhyllotacticFlora rely on.
            base.Initialize(cell);

            _axis = GrowthUp;
            SafeLookRotation.TrySet(transform, _axis, transform);

            // AFTER base.Initialize: this is the first point at which Element is final (prefab ->
            // rolled variant -> cell overrides -> the crystal that carries it).
            _form = ResolveForm();

            int seed = PlantSeed();
            ResolveWeights(seed, out float w0, out float w1, out float w2);
            _surface = ResolveSurface(Element, w0, w1, w2);
            _growth = new MandelbulbSurface.Growth(_surface, _form.Rules, seed);
            _addresses.Clear();
            _parent.Clear();
            _regrow.Clear();
            _laid.Clear();
            _limb.Clear();
            _pending.Clear();
        }

        ElementalForm ResolveForm()
        {
            if (formByElement != null && formByElement.Length > 0)
            {
                foreach (var entry in formByElement)
                    if (entry.Element == Element)
                        return entry;
                return formByElement[0];
            }
            // A prefab with no authored form still has to grow something rather than throw.
            return new ElementalForm
            {
                Element = Element,
                CrossSection = new Vector2(0.055f, 0.020f),
                Rules = new MandelbulbSurface.GrowthRules
                {
                    Field = MandelbulbSurface.SteeringField.Contour,
                    SwirlDegrees = 25f, FieldMix = 0.9f, Momentum = 0.35f,
                    StepSize = 0.04f, MaxSteps = 130, LanesPerSeed = 60, LaneGap = 0.30f,
                    HopSeek = 0.3f, HopJitter = 0.3f, SeedCount = 70, SeedSpreadDegrees = 10f,
                    MaxTurnDegrees = 30f, RadiusMin = 0.3f, RadiusMax = 2f, MinRun = 8,
                    LengthFactor = 1f, GirthTaper = 0.4f,
                },
            };
        }

        /// <summary>
        /// A plant's identity, derived from its own PLANTED POSITION rather than from
        /// <c>UnityEngine.Random</c> or an instance id. Flora are simulated per peer, and a
        /// NetworkSynced species replicates the planting DECISION (species, root pose, domain,
        /// element) and nothing else — so the position is the one thing every peer already agrees
        /// on, and hashing it is what makes two peers grow the same plant.
        /// </summary>
        int PlantSeed()
        {
            var p = transform.position;
            unchecked
            {
                int h = 17;
                h = h * 486187739 + Mathf.RoundToInt(p.x * 4f);
                h = h * 486187739 + Mathf.RoundToInt(p.y * 4f);
                h = h * 486187739 + Mathf.RoundToInt(p.z * 4f);
                return h == 0 ? 1 : h;
            }
        }

        void ResolveWeights(int seed, out float w0, out float w1, out float w2)
        {
            int steps = Mathf.Max(1, weightSteps);
            uint s = (uint)seed;
            float Next()
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                if (steps <= 1) return 0f;
                int q = (int)(s % (uint)steps);
                return weightSpread * (2f * q / (steps - 1) - 1f);
            }
            w0 = Next(); w1 = Next(); w2 = Next();
        }

        MandelbulbSurface.Surface ResolveSurface(Element element, float w0, float w1, float w2)
        {
            int w = Mathf.Max(32, fieldWidth);
            int h = Mathf.Max(16, w / 2);
            long key = ((long)(int)element << 40)
                     ^ ((long)Mathf.RoundToInt(w0 * 1000f) << 27)
                     ^ ((long)Mathf.RoundToInt(w1 * 1000f) << 14)
                     ^ (long)Mathf.RoundToInt(w2 * 1000f)
                     ^ ((long)w << 52);
            if (SurfaceCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var basis = MandelbulbSurfaceTables.For(element);
            var coeffs = MandelbulbSurface.Compose(basis, w0, w1, w2);
            var field = MandelbulbSurface.Reconstruct(MandelbulbSurfaceTables.Degree, coeffs, w, h);
            var surface = new MandelbulbSurface.Surface(field, w, h);
            // Clearing outright rather than evicting one: the cache exists so plants standing up
            // together share a field, and a cell that has moved past those plants has no use for
            // any of them.
            if (SurfaceCache.Count >= SurfaceCacheLimit) SurfaceCache.Clear();
            SurfaceCache[key] = surface;
            return surface;
        }

        /// <summary>
        /// Mandelbulb layer of the variant expression: the live-prism budget (the field every
        /// flora family reads) and <see cref="FloraVariantTuning.LatticeScale"/>, which here
        /// scales the whole plant — <see cref="shellRadius"/>, and therefore every prism with it —
        /// while leaving the surface, the curve walk and the prism COUNT exactly unchanged. That
        /// is safe here in a way it was not for the gyroid (Docs/ECOSYSTEM.md §34.8) because this
        /// species has no absolute-distance coherence tolerance to drag out from under: the whole
        /// growth rule is stated in units of the unit sphere.
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
            // Live-prism budget (frees as fauna graze — a cropped plant regrows).
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects) return;

            // Frenzy gate: steady growth until the cell tops out, then freeze and resume when an
            // active force brings the mass back down. Cell.FloraGrowingEnabled is the only gate.
            if (cell && !cell.FloraGrowingEnabled) return;

            if (_growth == null || _surface == null) return;

            for (int i = 0; i < growthsPerTick; i++)
            {
                if (_regrow.Count > 0) { Decide(_regrow.Dequeue()); continue; }

                // NOTE the cap is on CANDIDATES, not on the budget. The claim refuses roughly
                // two candidates in three (that is its job — see Claim), so capping the address
                // list at the budget would top a plant out at about a third of its prisms with
                // nothing reporting it. The list is bounded all the same: the generator can
                // produce seeds x lanes x steps addresses, which is half a million for one
                // element, and holding them all is 20 MB per plant.
                if (_addresses.Count >= maxTotalSpawnedObjects * AddressCandidateFactor
                    || !_growth.TryNext(out var next, out int parent))
                {
                    // Covered everything the rule could reach, or its front was grazed off. Free
                    // the addresses whose prisms are gone and re-open them — that is how a cropped
                    // plant heals back over itself instead of sitting inert.
                    ReopenGrazed();
                    return;
                }
                _addresses.Add(next);
                _parent.Add(parent);
                Decide(_addresses.Count - 1);
            }
        }

        void Decide(int index)
        {
            if (_laid.ContainsKey(index)) return;
            if (index < 0 || index >= _addresses.Count) return;

            var address = _addresses[index];
            MandelbulbSurface.Pose(_surface, address, ref _frame,
                                   out var local, out var forward, out var up);
            local *= shellRadius;
            Vector3 world = transform.TransformPoint(local);

            // Cross-PLANT occupancy. Within one plant a duplicate is already impossible (the
            // address list is the claim); this is the only thing that can refuse a prism, and a
            // refusal costs that prism and nothing else.
            if (!Claim(world, address)) return;

            _pending.Enqueue(new SpawnOrder
            {
                Index = index,
                LocalPosition = local,
                LocalRotation = PrismRotation(forward, up),
                Size = PrismSize(address),
                DecidedAt = Time.time,
            });
        }

        /// <summary>
        /// A prism lies ALONG its curve: local +z on the direction of travel (so the prism's long
        /// axis is the curve), local +y on the surface normal. Composed from the frame the pose
        /// measured rather than stored as a rotation, because half the frames on a surface are
        /// reflections and a baked quaternion carried through one is silently wrong
        /// (Docs/ECOSYSTEM.md §34).
        /// </summary>
        static Quaternion PrismRotation(Vector3 forward, Vector3 up)
        {
            if (forward.sqrMagnitude < 1e-8f || up.sqrMagnitude < 1e-8f) return Quaternion.identity;
            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>
        /// World size: the ribbon's authored cross-section scaled by this lane's GIRTH, and a
        /// length the curve measured. The girth taper is what gives one plant several scales of
        /// texture — the first lanes are its structure and the later ones its detail — which is
        /// the thing a species whose prisms all share one size and aspect cannot have.
        /// </summary>
        Vector3 PrismSize(in MandelbulbSurface.PrismAddress address)
        {
            float girth = Mathf.Max(0.05f, address.Girth) * shellRadius;
            return new Vector3(
                Mathf.Max(0.01f, _form.CrossSection.x * girth),
                Mathf.Max(0.01f, _form.CrossSection.y * girth),
                Mathf.Max(0.01f, address.Length * shellRadius));
        }

        /// <summary>
        /// Cross-plant occupancy AND this species' own thinning rule, which are the same
        /// operation. Curves CROSS — that is what a cage is — so two ribbons meeting at an angle
        /// legitimately overlap, and the thing worth refusing is a prism laid essentially inside
        /// one already there.
        ///
        /// <para>The radius is a fraction of the prism's OWN length, and the fraction is under 1
        /// for a structural reason rather than a tuned one: consecutive prisms of a curve sit
        /// exactly one length apart, so any factor below 1 clears the chain BY CONSTRUCTION and
        /// can never punch a hole along a ribbon. MEASURED over 6,000 candidates per element,
        /// 0.70 takes deeply-interleaved pairs (separating scale under 0.5) from 17–31% of
        /// touching pairs to zero, and the plant still reaches its whole budget because the
        /// claim thins CANDIDATES rather than the budget.</para>
        /// </summary>
        bool Claim(Vector3 world, in MandelbulbSurface.PrismAddress address)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return true;
            float radius = Mathf.Max(0.25f, ClaimFactor * address.Length * shellRadius);
            return index.TryReserve(world, radius);
        }

        /// <summary>See <see cref="Claim"/>. Under 1 so a curve's own chain always clears.</summary>
        internal const float ClaimFactor = 0.70f;

        /// <summary>How many addresses the rule may produce per prism of budget. Measured, the
        /// claim keeps between a third and two thirds of what the walk offers.</summary>
        internal const int AddressCandidateFactor = 4;

        void ReopenGrazed()
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
            // Orders decided just before Frenzy WAIT here and execute when growing re-enables —
            // the same freeze-and-resume the tick gate gives.
            if (cell && !cell.FloraGrowingEnabled) return;

            int spawned = 0;
            while (spawned < maxSpawnsPerFrame && _pending.Count > 0)
            {
                var order = _pending.Dequeue();
                if (Time.time - order.DecidedAt > MaxOrderAgeSeconds)
                {
                    // The spatial reservation lapsed. Re-open the address so a later tick
                    // re-decides it rather than leaving a permanent gap in the ribbon.
                    _regrow.Enqueue(order.Index);
                    continue;
                }
                Execute(order);
                spawned++;
            }
        }

        void Execute(SpawnOrder order)
        {
            // THE LIMB IS THE BOND. It is rooted at whatever this prism grows OUT of — the
            // nearest standing ancestor, or the heart at the plant's own origin — aimed at the
            // prism, and stretched to span the gap, so a plant reads as one connected object
            // growing outward from its crystal rather than as a field of prisms each wearing a
            // stub. That is the platform's growth law (the /flora skill §2, Docs/ECOSYSTEM.md
            // §49): first the crystal, then spindles out of the crystal, then prisms out of
            // spindles, and spindles out of prisms; it loops.
            Vector3 childWorld = transform.TransformPoint(order.LocalPosition);
            Quaternion childRot = transform.rotation * order.LocalRotation;
            Vector3 root = ResolveAnchorWorld(order.Index);
            Vector3 bond = childWorld - root;

            // Re-used when this address is regrowing into its own vacancy, so grazing can never
            // mint a second spindle on one bond — and RE-POSED either way, because the nearest
            // standing ancestor may have changed while the prism was gone.
            if (!_limb.TryGetValue(order.Index, out var limb) || !limb)
            {
                limb = AddSpindle();
                if (!limb)
                {
                    _regrow.Enqueue(order.Index);
                    return;
                }
                _limb[order.Index] = limb;
            }
            limb.LifeForm = this;
            limb.transform.position = root;
            // The branch geometry runs along the spindle's local +z, so LookRotation puts it on
            // the bond. Up is the prism's own surface NORMAL — PrismRotation seats the normal on
            // local +y — which keeps the limb's cross-section lying in the surface rather than
            // rolling arbitrarily about the bond.
            if (!SafeLookRotation.TrySet(limb.transform, bond, childRot * Vector3.up, this))
                limb.transform.rotation = childRot;
            StretchToBond(limb, bond.magnitude);

            var leaf = EnvironmentPrismPool.Get(healthPrism, childWorld, childRot);
            if (!leaf)
            {
                _regrow.Enqueue(order.Index);
                return;
            }

            // Parented to the SPINDLE and never to another prism: a prism carries its measured
            // size as its localScale, and a non-uniform scale above a rotated child is a shear
            // (Docs/ECOSYSTEM.md §37.9). worldPositionStays keeps the prism exactly on the pose
            // the surface measured — the spindle ROOT is never scaled, only its branch children
            // are, so no stretch can leak into the leaf.
            leaf.transform.SetParent(limb.transform, true);
            leaf.LifeForm = this;
            leaf.ChangeTeam(domain);

            _pendingPrismScale = order.Size;
            AddHealthBlock(leaf);
            leaf.Initialize("flora");

            _laid[order.Index] = leaf;

            // Growth is this plant's feeding — see Flora.NotifyGrew.
            NotifyGrew();
        }

        /// <summary>
        /// Where the prism at <paramref name="index"/> grows OUT of, in world space.
        ///
        /// <para>The generator hands every address the index it continues from, and guarantees a
        /// parent is always earlier in the list — so laying in list order is a growth order by
        /// construction. What it cannot guarantee is that the parent was LAID: the cross-plant
        /// claim refuses roughly a third of candidates, and a refused prism would otherwise leave
        /// its whole downstream curve hanging off nothing. So the anchor walks UP the parent chain
        /// to the nearest prism actually standing, and falls back to the plant's own origin —
        /// where the heart sits — when the chain runs out. A skipped prism therefore lengthens a
        /// bond rather than severing a branch, which is what keeps the plant ONE object under
        /// refusal and under grazing alike.</para>
        /// </summary>
        Vector3 ResolveAnchorWorld(int index)
        {
            int p = index >= 0 && index < _parent.Count ? _parent[index] : -1;
            // Bounded by the chain length, and the chain is strictly decreasing, so it terminates.
            while (p >= 0)
            {
                if (_laid.TryGetValue(p, out var prism) && prism) return prism.transform.position;
                p = p < _parent.Count ? _parent[p] : -1;
            }
            return transform.position;
        }

        /// <summary>
        /// Stretches a limb's branch geometry to span its own bond.
        ///
        /// <para>It scales the spindle's CHILDREN and never the root, for the reason
        /// <c>AssembledFlora.ScaleSpindleToLattice</c> records: the prism parents to the root, so
        /// a scaled root would multiply the measured prism size and this species' fitted leaf
        /// would stop describing the prism. Only the child's LOCAL Z is scaled — on every spindle
        /// prefab in the project that is the branch's length axis.</para>
        ///
        /// <para>It writes an ABSOLUTE scale rather than multiplying the current one, because a
        /// regrown prism re-poses its existing limb onto a possibly different bond and a
        /// multiplying stretch would compound every time a plant was grazed back.</para>
        /// </summary>
        void StretchToBond(Spindle limb, float bond)
        {
            if (!limb || bond <= 0f) return;
            var geometry = ResolveBranchGeometry(limb);
            if (geometry.Reach <= 0f || geometry.ChildScaleZ == null) return;

            float f = bond / geometry.Reach;
            Transform root = limb.transform;
            // Counted over the BRANCH children only. A re-posed limb can be carrying a prism by
            // now, and a prism wears its measured size as localScale — writing a branch's z onto
            // it would shear the leaf, which is the one thing parenting-to-the-spindle exists to
            // prevent.
            int seen = 0;
            for (int i = 0; i < root.childCount && seen < geometry.ChildScaleZ.Length; i++)
            {
                Transform child = root.GetChild(i);
                if (child.GetComponent<Prism>()) continue;
                Vector3 scale = child.localScale;
                scale.z = geometry.ChildScaleZ[seen++] * f;
                child.localScale = scale;
            }
        }

        /// <summary>
        /// How far the spindle prefab's branch reaches along the spindle's local +z, composed
        /// through the transform chain from the mesh bounds — not authored, because a number
        /// copied out of an asset is true only on the day it is copied. The per-child z scales it
        /// was measured at travel with it, so <see cref="StretchToBond"/> can state an absolute
        /// scale instead of a multiplier.
        ///
        /// <para>A prefab whose branch does NOT run along +z measures zero and is reported once by
        /// name: the limb then stands unstretched rather than silently inverted, which is a thing
        /// a human can see and act on.</para>
        /// </summary>
        BranchGeometry ResolveBranchGeometry(Spindle limb)
        {
            int key = spindle ? spindle.GetInstanceID() : 0;
            if (BranchByPrefab.TryGetValue(key, out var cached)) return cached;

            Transform root = limb.transform;
            float reach = 0f;
            foreach (var filter in limb.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter || !filter.sharedMesh || filter.transform == root) continue;
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 m = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = m.MultiplyPoint3x4(new Vector3(
                        (c & 1) == 0 ? b.min.x : b.max.x,
                        (c & 2) == 0 ? b.min.y : b.max.y,
                        (c & 4) == 0 ? b.min.z : b.max.z));
                    if (corner.z > reach) reach = corner.z;
                }
            }

            // Read off the live limb rather than the prefab asset: it has just been instantiated
            // and nothing has stretched it yet, and reading a prefab's transform chain in a
            // player is a different question from reading an instance's.
            var baseZ = new List<float>(root.childCount);
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.GetComponent<Prism>()) continue;
                baseZ.Add(child.localScale.z);
            }

            if (reach <= 0f && !_warnedNoBranch)
            {
                _warnedNoBranch = true;
                CSDebug.LogWarning(
                    $"{name}: spindle prefab '{(spindle ? spindle.name : "none")}' has no branch " +
                    "geometry along its local +z, so MandelbulbFlora cannot stretch its limbs to " +
                    "their bonds. This family expects the branch BranchingFlora and BorromeanFlora " +
                    "use, whose geometry runs forward from the spindle's origin.", this);
            }

            var geometry = new BranchGeometry(reach, baseZ.ToArray());
            BranchByPrefab[key] = geometry;
            return geometry;
        }

        /// <summary>
        /// Forgets a limb the tracker retired, so the address it carried grows a new one instead
        /// of re-posing a destroyed object. Named <c>limb</c> rather than the base's
        /// <c>spindle</c> on purpose: <see cref="LifeForm.spindle"/> is the PREFAB field, and a
        /// parameter shadowing it here would make the two look interchangeable.
        /// </summary>
        public override void RemoveSpindle(Spindle limb)
        {
            if (limb && _limb.Count > 0)
            {
                int key = int.MinValue;
                foreach (var pair in _limb)
                    if (pair.Value == limb) { key = pair.Key; break; }
                if (key != int.MinValue) _limb.Remove(key);
            }
            base.RemoveSpindle(limb);
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
                // AdmitTargetScale first: the size is STATED (measured from the curve), not grown
                // into, so it has to survive PrismScaleAnimator's silent per-axis [0.5, 10] clamp
                // — this species' ribbons run from well under a unit to several dozen, i.e. over
                // the ceiling at one end and under the floor at the other, and the clamp has no
                // log and no return value (Docs/ECOSYSTEM.md §34.9).
                healthPrism.AdmitTargetScale(_pendingPrismScale.Value);
                healthPrism.TargetScale = _pendingPrismScale.Value;
            }
            _pendingPrismScale = null;
        }

        // ── Preview ───────────────────────────────────────────────────────────

        /// <summary>
        /// Pure preview of the curve walk — see <see cref="Flora.TryPreviewGrowth"/>. Mirrors
        /// <see cref="Grow"/> exactly, and unlike the other families it is EXACT rather than
        /// merely representative: the growth rule's only randomness is its own deterministic
        /// generator, so the icon a player sees is the plant they will meet, prism for prism.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0) return false;

            var form = ResolveForm();
            ResolveWeights(seed == 0 ? 1 : seed, out float w0, out float w1, out float w2);
            var surface = ResolveSurface(Element, w0, w1, w2);
            var growth = new MandelbulbSurface.Growth(surface, form.Rules, seed == 0 ? 1 : seed);
            var frame = new MandelbulbSurface.Frame();

            int laid = 0;
            while (laid < budget && growth.TryNext(out var address))
            {
                MandelbulbSurface.Pose(surface, address, ref frame,
                                       out var local, out var forward, out var up);
                float girth = Mathf.Max(0.05f, address.Girth) * shellRadius;
                into.Add(new SpawnPoint(
                    local * shellRadius,
                    PrismRotation(forward, up),
                    new Vector3(Mathf.Max(0.01f, form.CrossSection.x * girth),
                                Mathf.Max(0.01f, form.CrossSection.y * girth),
                                Mathf.Max(0.01f, address.Length * shellRadius))));
                laid++;
            }
            return into.Count > 0;
        }
    }
}
