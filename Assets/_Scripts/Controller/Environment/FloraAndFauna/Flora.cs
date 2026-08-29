using System.Collections;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Abstract base for plant-like lifeforms.
    /// Provides periodic growth cycles and planting behavior on top of LifeForm's
    /// health/spindle infrastructure.
    /// </summary>
    public abstract class Flora : LifeForm
    {
        [Header("Flora Settings")]
        [SerializeField] Vector3 leafSize = new Vector3(4f, 4f, 1f);
        [SerializeField] protected float growPeriod = 3f;
        [SerializeField] public float PlantPeriod = 15f;
        [SerializeField] float stunDuration = 1f;

        [Header("Planting")]
        [Tooltip("OUTER edge of this species' planting band, as a fraction of the cell's membrane " +
                 "radius. Flora disperse inside the band so domain clusters form in distinct " +
                 "locations - fauna schools of different domains then get directed at different " +
                 "clusters instead of comingling around the centre. 0 = use the flora's legacy " +
                 "fixed planting radius.")]
        [Range(0f, 1f)] [SerializeField] protected float plantRadiusCellFraction = 0.6f;

        [Tooltip("INNER edge of the planting band, as a fraction of the membrane radius. 0 (the " +
                 "default) collapses the band to a single SHELL at the outer fraction - the legacy " +
                 "behaviour every cell had before bands existed. Set it below the outer fraction " +
                 "and this species fills the whole shell of space between the two, which is what a " +
                 "cell needs when its plants are meant to read as a forest with depth rather than " +
                 "a soap bubble at one radius.\n\n" +
                 "The inner edge is always clamped OUTSIDE the cell's nucleus: nucleus-interior " +
                 "mass is the territorial claim, is excluded from the fauna targeting grids, and " +
                 "is where a standard crystal respawns - so a plant rooted in there is mass the " +
                 "food web can never reach and clutter in the one volume that has to stay legible.")]
        [Range(0f, 1f)] [SerializeField] protected float plantRadiusCellFractionMin = 0f;

        protected bool isGrowing = true;

        /// <summary>
        /// The prism size this species' leaves grow to, and the unit its lattice bonds at: the
        /// per-element leaf PRISM size (authored on the prefab, overridden by
        /// <c>FloraVariantTuning.LeafSize</c>, scaled by level). Exposed so a flora that shapes
        /// its prisms per ROLE - a stem segment is not a leaf - can derive those shapes from the
        /// element's identity instead of re-authoring it. See <see cref="PhyllotacticFlora"/>.
        /// </summary>
        protected Vector3 LeafSize => leafSize;

        public abstract void Grow();
        public abstract void Plant();

        /// <summary>
        /// A <b>pure preview of this species' growth pattern</b>: run the growth rule in local
        /// space and report where prisms WOULD land, spawning nothing at all.
        ///
        /// Flora have no art model - a species IS its growth rule - so this is the only honest way
        /// to draw one as an icon (the Lifeform bench's stations and its toy emblem). It is not a
        /// second implementation of growth for gameplay: no prism, no spindle, no GameObject, no
        /// cell, no spatial-index reservation, no config mutation, and it must never touch
        /// <c>UnityEngine.Random</c> (that would perturb a shared deterministic sequence) - the
        /// caller passes a seed and gets the same shape every time.
        ///
        /// Default is false = "this species has no preview", and the caller falls back. Overrides
        /// mirror their own <see cref="Grow"/> and are expected to drift only in ways a thumbnail
        /// cannot show.
        /// </summary>
        /// <param name="budget">Maximum prisms to report.</param>
        /// <param name="seed">Deterministic seed for any branching choices.</param>
        /// <param name="into">Receives local-space prism poses.</param>
        public virtual bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into) => false;

        // Optional pinned planting spot. Plant() implementations normally disperse the flora
        // across the cell; a caller that needs it to root at a KNOWN spot (the Lifeform Matrix
        // toy's spawn-here stations) sets this before Initialize and Plant() honors it.
        Vector3? _plantPositionOverride;

        /// <summary>
        /// The surface normal at the pinned spot, when one was supplied - an authored garden bed
        /// has an UP and a plant rooted in it should grow away from the bed, not toward the cell
        /// crystal. Vector3.zero when the caller pinned a position only.
        /// </summary>
        Vector3 _plantUpOverride;

        /// <summary>Pin where this flora plants itself. Call before Initialize.</summary>
        public void SetPlantPositionOverride(Vector3 position) => _plantPositionOverride = position;

        /// <summary>
        /// Pin where this flora plants itself AND which way is up there (an authored planting
        /// site - <see cref="FloraPlantingSite"/>). Call before Initialize.
        /// </summary>
        public void SetPlantPositionOverride(Vector3 position, Vector3 up)
        {
            _plantPositionOverride = position;
            _plantUpOverride = up;
        }

        /// <summary>True (with the spot) when a caller pinned the planting position.</summary>
        protected bool TryGetPlantPositionOverride(out Vector3 position)
        {
            position = _plantPositionOverride ?? default;
            return _plantPositionOverride.HasValue;
        }

        /// <summary>
        /// The pinned growth axis: the site's normal when one was supplied, else the direction
        /// away from the cell centre (a plant on an unstructured cell still grows outward, which
        /// is what the legacy shell dispersal implies). Only meaningful after Plant().
        /// </summary>
        protected Vector3 GrowthUp
        {
            get
            {
                if (_plantUpOverride.sqrMagnitude > 0.0001f) return _plantUpOverride.normalized;
                if (cell)
                {
                    var radial = transform.position - cell.transform.position;
                    if (radial.sqrMagnitude > 0.0001f) return radial.normalized;
                }
                return Vector3.up;
            }
        }

        /// <summary>
        /// This species' planting band as fractions of the membrane radius, (inner, outer).
        ///
        /// <para>Read-only, and readable WITHOUT a cell - which is the point: an arcade card has to
        /// say where a species plants before anything has been planted and before any Cell exists
        /// to ask. <see cref="ResolvePlantRadius"/> stays the only thing that decides where an
        /// actual plant goes.</para>
        /// </summary>
        public Vector2 PlantingBandFractions =>
            new(Mathf.Min(plantRadiusCellFractionMin, plantRadiusCellFraction), plantRadiusCellFraction);

        /// <summary>
        /// Planting radius for <see cref="Plant"/>: a fraction of the owning cell's membrane
        /// radius when configured (disperses flora across the whole cell), falling back to the
        /// flora's legacy fixed radius when the outer fraction is 0 or the cell/membrane is
        /// unavailable.
        ///
        /// <para>With <see cref="plantRadiusCellFractionMin"/> set, this draws a radius from the
        /// BAND between the two fractions instead of returning the single outer shell. The draw is
        /// uniform by VOLUME (<c>r = cbrt(lerp(min³, max³, u))</c>), not by radius: a shell's
        /// available space grows as r², so a uniform-in-radius draw crowds plants onto the inner
        /// edge and leaves the outer band — most of the cell — looking empty. Volume-uniform gives
        /// an even spatial density through the whole band, which naturally puts most plants in the
        /// outer reaches (that is where the space is) while still landing some in close.</para>
        ///
        /// <para>The inner edge is clamped outside the nucleus — see the field tooltip.</para>
        /// </summary>
        protected float ResolvePlantRadius(float legacyRadius)
        {
            if (plantRadiusCellFraction <= 0f || !cell || cell.MembraneRadius <= 0f)
                return legacyRadius;

            float membrane = cell.MembraneRadius;
            float outer = membrane * plantRadiusCellFraction;

            // Never inside the nucleus: that mass is the territorial claim, is kept out of the
            // fauna targeting grids, and shares its volume with the standard crystal respawn.
            // ExpectedNucleusWorldRadius (not NucleusWorldRadius) so the clamp is correct even if
            // a caller plants before the cell has instantiated its nucleus.
            float inner = Mathf.Max(membrane * plantRadiusCellFractionMin,
                                    cell.ExpectedNucleusWorldRadius);
            if (inner >= outer) return outer;

            float t = Random.value;
            float innerCubed = inner * inner * inner;
            float outerCubed = outer * outer * outer;
            return Mathf.Pow(Mathf.Lerp(innerCubed, outerCubed, t), 1f / 3f);
        }

        /// <summary>
        /// The point <see cref="ResolvePlantRadius"/>'s shell is measured FROM: the owning
        /// cell's centre, which is what "a fraction of the cell's membrane radius" has always
        /// meant. Every <see cref="Plant"/> implementation used to measure from
        /// <c>cellData.CrystalTransform.position</c> instead - harmless while a mode's crystals
        /// sit in the cell core, and wrong the moment a mode lets its crystal roam: the whole
        /// planting shell then rides the crystal and a plant lands at
        /// <c>crystalRadius + fraction x membraneRadius</c>, i.e. OUTSIDE the membrane, where
        /// <c>Cell.ContainsPosition</c> rejects its prisms and neither the volume ladder nor
        /// the fauna density grids can see them. Rampage's contested crystal is the mode that
        /// exposed it.
        ///
        /// Falls back to the crystal (legacy) and then to this flora's own position, so a cell
        /// that never resolved is still a planting anchor rather than a
        /// <see cref="System.NullReferenceException"/> (<c>CrystalTransform</c> returns null
        /// when a cell holds no crystal at all).
        /// </summary>
        protected Vector3 ResolvePlantCenter()
        {
            if (cell) return cell.transform.position;

            var crystalTransform = cellData ? cellData.CrystalTransform : null;
            return crystalTransform ? crystalTransform.position : transform.position;
        }

        /// <summary>
        /// Flora layer of the variant expression: the per-element leaf PRISM shape, growth
        /// tempo, and planting radius (the fields that differ between the four GyroidFlora
        /// prefabs). Runs before Initialize, so every leaf grows to the variant's size.
        /// </summary>
        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;

            if (tuning.LeafSize != Vector3.zero) leafSize = tuning.LeafSize;
            if (tuning.GrowPeriod >= 0f) growPeriod = tuning.GrowPeriod;
            if (tuning.PlantRadiusCellFraction >= 0f)
                plantRadiusCellFraction = Mathf.Clamp01(tuning.PlantRadiusCellFraction);
            if (tuning.PlantRadiusCellFractionMin >= 0f)
                plantRadiusCellFractionMin = Mathf.Clamp01(tuning.PlantRadiusCellFractionMin);
        }

        /// <summary>
        /// Seconds between shield refreshes on a CHARGE plant's leaves - the cadence the nine
        /// already-authored Charge flora assets carry, hoisted here so the law and the data
        /// state the same number.
        /// </summary>
        public const float ChargeShieldPeriod = 1f;

        /// <summary>
        /// THE CHARGE LAW: a Charge plant armours its leaves. Charge is the element whose
        /// identity is that its mass is SHIELDED - a shielded prism sheds its shield instead
        /// of being eaten (<c>Prism.Consume</c>), so a herbivore must strip a Charge plant
        /// before it can graze it, and the plant re-armours one leaf every
        /// <see cref="ChargeShieldPeriod"/> seconds. Nothing is culled and nothing is immune:
        /// grazing simply takes two passes, which is the cost the element buys.
        ///
        /// <para>It is a rule here rather than a number on every asset because the cadence is
        /// authored per CONFIG while the element is ROLLED per plant
        /// (<c>FloraConfigurationSO.SpreadElements</c>). A config with no element palette rolls
        /// an element and then applies its OWN variant block to it, so the two Hesperides
        /// topiary configs would hand a Charge plant a cadence of 0 and no amount of asset
        /// authoring could reach them without re-shaping the other three elements too. The
        /// canonical per-element assets still author 1 - this only floors what they say.</para>
        ///
        /// <para>An authored cadence still wins: a Charge species may be armoured FASTER or
        /// SLOWER than the fleet, it just may not be unarmoured. Fauna are deliberately
        /// untouched (this override is on <see cref="Flora"/>, not <c>LifeForm</c>): a
        /// creature's body prisms are not the food web's mass, and shielding them would
        /// change what it takes to kill a creature.</para>
        /// </summary>
        protected override float ResolveShieldPeriod(float authored)
            => Element == CosmicShore.Data.Element.Charge && authored <= 0f
                ? ChargeShieldPeriod
                : authored;

        /// <summary>
        /// THE TIME LAW: a Time plant breeds faster, every other element a little slower
        /// (<see cref="FloraReproductionRules.ReproductionRateFor"/>). Reproduction is the one
        /// clock a plant owns, and the clock is Time's identity - the same kind of elemental
        /// expression as <see cref="ResolveShieldPeriod"/>, which is why it lives here rather
        /// than in the assets.
        ///
        /// <para>It CANNOT be authored per config: the quota is authored per CONFIG (by
        /// <c>Tools/Build/author_flora_populations.py</c>) while the element is ROLLED per
        /// plant (<c>FloraConfigurationSO.SpreadElements</c>) - and every species that actually
        /// spends this quota rolls its element, so there is no asset field that could express
        /// it. The authored number stays the SPECIES baseline and the element scales it at
        /// spawn, so the authoring script needs no per-element fork and cannot drift from
        /// this rule.</para>
        ///
        /// <para>An authored 0 stays 0: that is the species saying it does not reproduce at
        /// all, and no element may scale a species into breeding.</para>
        /// </summary>
        protected int ResolveGrowthPerOffspring(int authored)
            => FloraReproductionRules.ScaleGrowthQuota(
                authored, FloraReproductionRules.ReproductionRateFor(Element));

        /// <summary>
        /// True when this species' prism SIZE is dictated by its growth rule rather than being
        /// free, so <b>no per-individual scale curve may ever touch it</b>. A LATTICE species is
        /// the case (<see cref="AssembledFlora"/>): its neighbour offsets are a measured bond
        /// table in absolute local units (<see cref="OctagonNeighbor"/>,
        /// <c>SeparationDistance</c>), and <c>GyroidAssembler.Start</c> captures the prism's
        /// target scale once — so growing the leaf mid-life lays prisms the table no longer
        /// describes. It cannot be fixed by making the offsets scale-aware either: a plant's
        /// EARLIER prisms were laid at the old size, and two prism sizes cannot tile one lattice.
        ///
        /// <para><b>Nothing reads this today, deliberately.</b> Its one reader was the LEVEL
        /// curve, which is retired (Docs/ECOSYSTEM.md §40) — a plant's leaf is now stated once
        /// by its element and never changes in life, so there is no curve left to exempt.
        /// It is kept because the RULE outlived the mechanism: <b>before adding any
        /// per-individual scale curve, ask which species' geometry is authored in absolute
        /// units</b>, and gate it on this. Deleting it deletes the guard, not the hazard.</para>
        /// </summary>
        protected virtual bool PrismSizeFixedByGrowthRule => false;

        public override void AddHealthBlock(HealthPrism healthPrism)
        {
            base.AddHealthBlock(healthPrism);
            // leafSize is AUTHORED, not grown, so it must survive PrismScaleAnimator's per-axis
            // [minScale, maxScale] clamp - default [0.5, 10], which no flora prefab overrides.
            // Without this a config asking for 60 x 1 x 1 silently becomes 10 x 1 x 1.
            healthPrism.AdmitTargetScale(leafSize);
            healthPrism.TargetScale = leafSize;
        }

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell);

            // Android stripped-performance branch: flora never plant or grow — a creation-side
            // pause (sanctioned: "not creating mass is allowed; aging it out is not"). Existing
            // mass is untouched; scene-placed flora sit as inert roots. Covers the menu cell's
            // 6 scene-placed BranchingFlora, whose otherwise-unbounded growth (12,000-volume
            // ceiling, 0s intervals) dominates CPU + collider load on a mid phone.
            if (CosmicShore.Utility.PerfStrip.Enabled)
            {
                isGrowing = false;
                return;
            }

            Plant();
            StartCoroutine(GrowCoroutine());
        }

        // -------------------------------------------------------------------
        //  Lineage + reproduction - the population driver (Docs/ECOSYSTEM.md §32).
        //  The plant-side mirror of Fauna.AssignLineage / TryReproduce: the periodic
        //  spawner is demoted to a SEEDER (it only tops a species back up to its floor)
        //  and the standing forest is grown by the plants themselves and grazed back
        //  down by the food web. A plant with no lineage (a toy clone, a microscene
        //  release) never reproduces - same rule a manager-spawned fauna follows.
        // -------------------------------------------------------------------

        Cell hostCell;
        FloraConfigurationSO sourceConfig;

        // This individual's rolled variant (element + the block expressing it + level).
        // Passed to offspring so a lineage breeds true instead of re-rolling per birth.
        LifeformVariantPick<FloraVariantTuning>? _variantPick;
        bool lineageRegistered;
        int _growthSinceBirth;
        float _lastBirthTime = float.NegativeInfinity;

        /// <summary>The species config this plant was spawned from (null for toy/microscene plants).</summary>
        public FloraConfigurationSO SourceConfig => sourceConfig;

        /// <summary>
        /// This plant's live-prism budget - what <see cref="FloraVariantTuning.MaxTotalSpawnedObjects"/>
        /// resolved to on this individual. Each flora family owns its own field (they ship very
        /// different budgets), so the base only needs to be able to READ it, for the maturity
        /// gate. 0 = "this family has no budget", which skips the gate.
        /// </summary>
        protected virtual int PrismBudget => 0;

        /// <summary>
        /// Binds this plant to its species lineage: the cell whose population it belongs to and
        /// the config that defines the species. Registers it in the cell's per-species live
        /// count (unregistered in OnDestroy). Called by <c>CellLifeSpawnerBase.SpawnFlora</c> -
        /// the one canonical spawn path every producer already routes through - and by a parent
        /// for its offspring, since heredity is what lets reproduction recurse.
        /// </summary>
        public void AssignLineage(Cell host, FloraConfigurationSO config,
            LifeformVariantPick<FloraVariantTuning>? inherit = null)
        {
            hostCell = host;
            sourceConfig = config;
            _variantPick = inherit;

            if (host && config && !lineageRegistered)
            {
                host.RegisterLiveFlora(this);
                lineageRegistered = true;
            }
        }

        /// <summary>
        /// A subclass calls this each time it actually grows prisms. <b>Growth is a plant's
        /// feeding</b> (Docs/ECOSYSTEM.md §32): it is what converts space into population, the
        /// way prey converts into population for a creature - so every grown prism advances the
        /// birth counter. The decision itself is taken once per grow tick (see
        /// <see cref="GrowCoroutine"/>), not per prism.
        ///
        /// <para><b>A new flora family must call this</b> at the point it actually lays a prism,
        /// or its species can never reproduce. The consequence of forgetting is benign (that
        /// family is simply seeder-driven, as every flora was before) - but it is silent.</para>
        ///
        /// <para>This is also what keeps the population bounded with NO imposed death: a plant
        /// at its budget has stopped growing, so it has stopped funding children, and it only
        /// funds another one after the food web grazes it and it regrows.</para>
        /// </summary>
        protected void NotifyGrew(int prisms = 1)
        {
            if (prisms > 0) _growthSinceBirth += prisms;
        }

        void TryReproduce()
        {
            var cfg = sourceConfig;
            var host = hostCell;
            if (!cfg || !host || cfg.GrowthPerOffspring <= 0 || !cfg.FloraPrefab) return;

            // Reproduction is PRODUCTION, so it freezes with planting at Frenzy and resumes on
            // its own when an active force (grazing, a vessel ability) brings the cell back
            // under the hysteresis floor. Cell.FloraPlantingEnabled is the single source of
            // truth - a new plant is a planting, whichever producer asked for it.
            if (cell && !cell.FloraPlantingEnabled) return;

            // The cap is the CELL's, not the config's: a biome that scales its forest
            // (SpawnProfileSO.FloraPopulationScale) has to scale what reproduction may fill to,
            // or the seeder and the plants would be working to two different ceilings.
            if (!FloraReproductionRules.ShouldSeed(
                    _growthSinceBirth, ResolveGrowthPerOffspring(cfg.GrowthPerOffspring),
                    Time.time - _lastBirthTime, cfg.ReproductionCooldownSeconds,
                    healthTracker != null ? healthTracker.Count : 0, PrismBudget, cfg.MaturityFraction,
                    host.GetLiveFloraCount(cfg), host.ResolveFloraCap(cfg)))
                return;

            int offspring = Mathf.Max(1, cfg.OffspringPerBirth);
            int born = 0;
            for (int i = 0; i < offspring; i++)
            {
                // Re-check the cap per birth so a multi-offspring birth can't overshoot the
                // performance backstop.
                if (host.IsFloraAtCap(cfg)) break;
                if (!SpawnOffspring(host, cfg)) break;
                born++;
            }

            // The quota is spent only by a birth that actually happened. A plant blocked by the
            // cap - or by having nowhere to put a child - stays ARMED, which is the whole point:
            // a full plant has stopped growing, so it will never be re-armed by another growth
            // tick, and without this it could never fill the gap left when a neighbour is grazed
            // out. Staying armed makes that gap-filling automatic and costs nothing (the check
            // only re-runs when something grows).
            if (born <= 0) return;

            _lastBirthTime = Time.time;
            _growthSinceBirth = 0;
        }

        /// <summary>
        /// NOURISH: an own-domain pilot shepherded this plant (the Squirrel's Space-5 joust).
        /// A plant has no mouth, so the nourishment lands where a plant's own effort lands —
        /// its GROWTH QUOTA, the currency it funds children from — and then immediately tests
        /// for a seeding, so a shepherded plant pays out as another plant rather than as a
        /// bigger one (Docs/ECOSYSTEM.md §40.4).
        ///
        /// <para>The credit is one whole offspring's worth of growth, so shepherding is a
        /// meaningful act rather than a tap. It is bounded by every gate an ordinary birth
        /// passes: the Frenzy planting freeze, the cell's per-species cap, the reproduction
        /// cooldown and the maturity fraction — so it can accelerate a population, never
        /// exceed the ceiling the cell authored for it. A species that does not reproduce
        /// (GrowthPerOffspring 0) declines, because there is nothing to nourish.</para>
        ///
        /// <para>Unlike the LEVEL-UP it replaced, this has no ceiling of its OWN — levelling
        /// stopped at 5 and nourishing does not stop. What bounds the RATE is upstream and
        /// downstream of here rather than in it, so it is worth naming: the jouster must
        /// re-enter the heart's trigger and can only land one strike per crystal per 0.5 s
        /// (<c>VesselImpactor</c>'s per-crystal latch), and a plant still cannot birth faster
        /// than its own <c>ReproductionCooldownSeconds</c>. Banked credit above that is not
        /// lost and not compounded — a birth spends the quota back to zero, so a shepherd
        /// who parks on one plant gets that plant's cooldown, not a burst.</para>
        /// </summary>
        public override bool Nourish()
        {
            var cfg = sourceConfig;
            if (!cfg || cfg.GrowthPerOffspring <= 0) return false;
            NotifyGrew(ResolveGrowthPerOffspring(cfg.GrowthPerOffspring));
            TryReproduce();
            return true;
        }

        /// <summary>
        /// Spawns exactly ONE offspring right now, subject only to the universal production
        /// gates (the Frenzy planting freeze and the cell cap). This is the entry point for a
        /// POPULATION-coordinated reproduction model - the gyroid octagon colony's
        /// one-birth-per-cycle (Docs/ECOSYSTEM.md §32.7) - where WHEN and WHERE to reproduce
        /// is decided at the population level rather than by this plant's own growth quota.
        /// The per-plant quota path (<see cref="TryReproduce"/>) is untouched for species
        /// that reproduce individually.
        /// </summary>
        protected bool TrySpawnOneOffspring()
        {
            var cfg = sourceConfig;
            var host = hostCell;
            if (!cfg || !host || !cfg.FloraPrefab) return false;
            if (cell && !cell.FloraPlantingEnabled) return false;
            if (host.IsFloraAtCap(cfg)) return false;
            if (!SpawnOffspring(host, cfg)) return false;

            return true;
        }

        bool SpawnOffspring(Cell host, FloraConfigurationSO cfg)
        {
            if (!TryResolveOffspringPlacement(out var position, out var rotation, out var up))
                return false;

            // Same lifecycle as a spawner planting: the ONE canonical spawn path applies the
            // variant, the cell overrides and the level, then Initialize plants and starts
            // growth - so an offspring is an ordinary plant of its species in every respect,
            // including the bloom-in its prisms already animate (continuity of existence).
            //
            // The pinned position is what stops Plant() dispersing the child randomly across
            // the membrane: a daughter belongs NEXT TO ITS PARENT, and for a lattice species it
            // belongs at one exact bond site (see AssembledFlora.TryResolveOffspringPlacement).
            //
            // Heredity: the child inherits this plant's variant pick - its element and the level
            // it seeded at - rather than rolling a fresh identity. In-world level-ups (the
            // Shepherd joust) are NOT inherited: acquired growth is not heritable.
            // Offspring inherit the parent's domain rather than re-rolling, and it is passed IN
            // rather than applied after the fact: Initialize files the child's first prisms into
            // the cell's per-domain grids on its way through, so a SetTeam afterwards would have
            // binned them under a rolled colour first. Uniform seeding across the three domains
            // stays the SPAWNER's job (the no-domain-asymmetry invariant); within a lineage a
            // plant's children are its own colour, exactly as a creature's are.
            var child = CellLifeSpawnerBase.SpawnFlora(
                host, cfg.FloraPrefab, excludedDomain: null, config: cfg,
                spawnPosition: position, spawnUp: up, spawnRotation: rotation,
                inherit: _variantPick, preInitialize: ConfigureOffspring, domainOverride: domain);

            if (!child) return false;
            return true;
        }

        /// <summary>
        /// Where this plant's next offspring goes. Default: a point at
        /// <see cref="FloraConfigurationSO.OffspringSpread"/> from the parent, kept inside this
        /// species' planting band (so a child can never be seeded into the nucleus, whose mass
        /// the food web is deliberately never steered to). Rotation is unconstrained.
        ///
        /// <para>A species whose growth rule DOES have an opinion about where the next plant
        /// belongs overrides this - <see cref="AssembledFlora"/> hands the daughter the exact
        /// bond site its own lattice would have grown into next, which is what makes many small
        /// plants add up to one continuous minimal surface.</para>
        /// </summary>
        /// <returns>False to skip this birth entirely (no site available).</returns>
        protected virtual bool TryResolveOffspringPlacement(
            out Vector3 position, out Quaternion rotation, out Vector3? up)
        {
            float spread = sourceConfig ? Mathf.Max(1f, sourceConfig.OffspringSpread) : 60f;
            position = ClampToPlantingBand(transform.position + Random.onUnitSphere * spread);
            rotation = transform.rotation;
            up = null;
            return true;
        }

        /// <summary>
        /// Optional per-family setup applied to an offspring AFTER its variant/level are applied
        /// and BEFORE <see cref="Initialize"/> - the window in which a plant's growth rule can be
        /// seeded (see <see cref="AssembledFlora"/>, which programs the daughter's first
        /// assembler so its lattice continues the parent's).
        /// </summary>
        protected virtual void ConfigureOffspring(Flora child) { }

        /// <summary>
        /// Pulls a point back into this species' planting band around the cell centre: never
        /// inside the nucleus (that mass is the territorial claim, is excluded from the fauna
        /// targeting grids, and shares its volume with the crystal respawn) and never outside
        /// the band's outer edge, where <c>Cell.ContainsPosition</c> would reject the prisms.
        /// Returns the point untouched when the species has no band authored.
        /// </summary>
        protected Vector3 ClampToPlantingBand(Vector3 point)
        {
            if (!cell || cell.MembraneRadius <= 0f) return point;

            Vector3 centre = cell.transform.position;
            float outer = plantRadiusCellFraction > 0f
                ? cell.MembraneRadius * plantRadiusCellFraction
                : cell.MembraneRadius;
            float inner = Mathf.Min(cell.ExpectedNucleusWorldRadius, outer);

            Vector3 offset = point - centre;
            float d = offset.magnitude;
            if (d > inner && d < outer) return point;
            if (d < 0.001f) offset = Random.onUnitSphere * Mathf.Max(inner, 1f);
            else offset = offset / d * Mathf.Clamp(d, inner, outer);
            return centre + offset;
        }

        protected virtual void OnDestroy()
        {
            if (lineageRegistered && hostCell)
                hostCell.UnregisterLiveFlora(this);
            lineageRegistered = false;
        }

        public override void RemoveHealthBlock(HealthPrism healthPrism, string killername = "")
        {
            base.RemoveHealthBlock(healthPrism);
            isGrowing = false;
        }

        IEnumerator GrowCoroutine()
        {
            while (true)
            {
                if (isGrowing)
                {
                    Grow();

                    // Reproduction is decided once per grow tick rather than once per grown
                    // prism: the quota itself is EARNED by growth (NotifyGrew), so this only
                    // chooses when the decision is re-evaluated - and re-evaluating on the tick
                    // is what lets a plant that banked its quota while the species was at its
                    // cap fill the gap the moment a neighbour is grazed out. A plant with no
                    // banked growth fails on the first compare, so an unauthored species pays
                    // two int reads per tick and nothing else.
                    TryReproduce();

                    yield return new WaitForSeconds(growPeriod);
                }
                else
                {
                    isGrowing = true;
                    yield return new WaitForSeconds(stunDuration);
                }
            }
        }
    }
}
