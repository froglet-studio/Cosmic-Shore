using System.Collections;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Abstract base for animal-like lifeforms and their managers.
    /// Uses virtual methods instead of abstract to satisfy LSP - subclasses only
    /// override what they need, and managers don't have to throw NotImplementedException.
    /// </summary>
    public abstract class Fauna : MonoBehaviour, ILifeFormEntity
    {
        [Header("Data References")]
        [Inject] GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Header("Team & Goals")]
        [FormerlySerializedAs("Team")]
        public Domains domain;
        [SerializeField] float goalUpdateInterval = 5f;
        [Tooltip("Goal-update cadence multipliers indexed by CellAggressionLevel " +
                 "(Level0/Level1/Level2). Lower = faster relocation under stress.")]
        [SerializeField] float[] goalUpdateIntervalByAggression = { 1f, 0.55f, 0.25f };
        [Tooltip("Each fauna picks a stable random offset on a sphere of this radius " +
                 "and adds it to its resolved goal. Prevents the whole pack from " +
                 "converging onto a single point (e.g. the crystal at origin), which " +
                 "otherwise creates a depletion zone where fauna repeatedly consume " +
                 "the same prism configuration.")]
        [SerializeField] float goalOrbitRadius = 60f;

        Vector3 _goal;

        /// <summary>
        /// Where this creature is currently heading. A PROPERTY rather than a field so the
        /// cell's fauna containment (<see cref="Cell.ClampToFaunaContainment"/>) applies at the
        /// single point every writer must pass through - and there are many: this class's
        /// <see cref="ResolveGoal"/>, Boid's override, LightFauna's half-dozen direct writes on
        /// its own behavior tick, the spawner's initial goal, and the inheritance in
        /// <see cref="TryReproduce"/>. Clamping in each of them would be a rule you could forget
        /// to apply in the next grazer; clamping here is a rule that cannot be bypassed.
        /// No-op (returns the value unchanged) for every cell that authors no containment,
        /// which is all of them except a mode that pens its brood.
        /// </summary>
        public Vector3 Goal
        {
            get => _goal;
            set
            {
                // Two pens compose here, outermost first: the CELL's pen (one radius, every
                // creature) and then this SPECIES' own band (an annulus, authored per config -
                // see BandInner/BandOuter). Both are no-ops when unauthored, which is every
                // biome that is not a mode's pen, so the common path is still two compares.
                var clamped = cell ? cell.ClampToFaunaContainment(value, transform.position) : value;
                _goal = ClampToBand(clamped);
            }
        }

        // ── Per-species band (the layered pen) ────────────────────────────────
        //
        // A mode that stacks CONCENTRIC pens needs more than the cell's single containment
        // radius: Wildlife Liberation stocks three nested cages with three different tiers of
        // creature, and a lone radius can only express "inside R". The band generalizes it to
        // an ANNULUS (inner..outer) authored per species on FaunaConfigurationSO, so the
        // outer swarm rides the outer shell, the mid tier the mid shell, and the kaiju the
        // core - each unable to reach the others' feeding ground.
        //
        // Same contract as the cell pen and for the same reason (Docs/ECOSYSTEM.md §22): it is
        // a spatial DIET + STEERING rule, never a wall. Nothing is teleported, no collider is
        // added, no creature is culled for leaving. A creature can still drift across a
        // boundary on its own momentum - it simply has no reason to and nothing to eat there.
        float _bandInner;
        float _bandOuter;

        /// <summary>True when this creature is penned to an annulus of its own (see the band notes).</summary>
        protected bool HasBand => _bandOuter > 0f;

        /// <summary>
        /// The centre both pens measure from. The host cell when there is one (which is every
        /// spawner-born creature); this creature's own spawn point otherwise, so a manager-spawned
        /// or drone fauna with no cell simply has no band rather than one centred on the origin.
        /// </summary>
        Vector3 BandCentre => cell ? cell.transform.position : transform.position;

        /// <summary>True when <paramref name="position"/> lies in this creature's band (always true with no band).</summary>
        public bool IsInsideBand(Vector3 position)
        {
            if (!HasBand) return true;
            float d = (position - BandCentre).magnitude;
            return d >= _bandInner && d <= _bandOuter;
        }

        /// <summary>
        /// Pulls a goal back into this creature's band. Returns the point unchanged when there is
        /// no band or the goal is already inside, so the common path costs one compare. A goal
        /// beyond the outer wall is pulled in to it; a goal inside the inner wall is pushed out to
        /// it (a goal exactly at the centre picks the creature's own outward radial, so the push
        /// is well-defined rather than arbitrary).
        /// </summary>
        public Vector3 ClampToBand(Vector3 goal)
        {
            if (!HasBand) return goal;

            Vector3 centre = BandCentre;
            Vector3 offset = goal - centre;
            float d = offset.magnitude;

            if (d >= _bandInner && d <= _bandOuter) return goal;

            // A goal AT THE CENTRE is the ecology's "nothing sensed" answer
            // (Cell.GetDensestRegionAnyDomain falls back to the cell anchor on an empty grid),
            // not a destination. Clamping it radially would send every creature in the room to
            // exactly _bandInner - the whole population collapsing onto the inner wall as a
            // shell, which is the same clumping the spawner's per-creature spread exists to
            // avoid. Project the creature's OWN position instead, so an unfed creature mills
            // about where it already is inside its room.
            if (d <= 0.0001f)
            {
                Vector3 own = transform.position - centre;
                float ownD = own.magnitude;
                if (ownD <= 0.0001f) return centre + Vector3.up * _bandInner;
                return centre + own / ownD * Mathf.Clamp(ownD, _bandInner, _bandOuter);
            }

            return centre + offset / d * Mathf.Clamp(d, _bandInner, _bandOuter);
        }

        /// <summary>
        /// The herbivore diet rule as THIS creature sees it: the cell's spatialized rule
        /// (<see cref="Cell.IsPreyForHerbivore"/>) AND the creature's own band. Every grazer
        /// routes its edibility test through here rather than calling the cell directly, for the
        /// same reason <see cref="IsShieldedMass"/> exists - "a creature must never be led to
        /// mass it cannot reach or eat" is ONE rule, and a per-subclass copy is a rule you can
        /// forget to apply in the next grazer.
        /// </summary>
        protected bool IsPreyForMe(Vector3 position, Domains preyDomain) =>
            cell != null && IsInsideBand(position) &&
            cell.IsPreyForHerbivore(position, domain, preyDomain);

        // Stable per-instance offset so each fauna orbits its resolved goal at a
        // different point. Seeded once at Start so the spread is deterministic per
        // spawn but varied across the pack.
        Vector3 _goalOrbitOffset;

        /// <summary>
        /// This creature's stable offset from whatever point it is currently seeking -
        /// the anti-convergence term. Subclasses that recompute <see cref="Goal"/> on
        /// their own cadence MUST add it wherever <see cref="ResolveGoal"/> does, or the
        /// pack collapses onto one point: every creature seeks the identical centroid,
        /// arrives, and its goal direction degenerates to zero. See
        /// <see cref="LightFauna"/>'s behavior tick.
        /// </summary>
        protected Vector3 GoalOrbitOffset => _goalOrbitOffset;

        [Header("Diet (predator / prey)")]
        [Tooltip("What this fauna eats - the predator/herbivore selector. Herbivore: " +
                 "opposing-domain prism MASS (flora canopy + vessel trails); the default, " +
                 "original behavior. Predator: herbivore FAUNA (ignores prism mass for " +
                 "feeding). Both starve via the clock below, so a Predator layered on " +
                 "Herbivores yields a two-tier Lotka-Volterra food web. Docs/ECOSYSTEM.md §7/§10.")]
        [SerializeField] protected FaunaDiet diet = FaunaDiet.Herbivore;

        /// <summary>What this fauna eats - the predator/herbivore selector. See <see cref="FaunaDiet"/>.</summary>
        public FaunaDiet Diet => diet;

        [Tooltip("Seconds after spawn during which this fauna CANNOT be eaten by a predator. " +
                 "All fauna spawn co-located at the cell centre, so without this a predator " +
                 "eats every herbivore the instant it spawns ('only sharks'). The grace window " +
                 "lets the swarm disperse first. See Docs/ECOSYSTEM.md §7.")]
        [SerializeField] protected float predationImmunitySeconds = 6f;

        // Set in Awake (runs during Instantiate, before any predator's behavior tick) so a
        // freshly-spawned creature is immune from frame zero, not only after its Start runs.
        float _spawnTime = -1f;

        /// <summary>True during the post-spawn grace window when this fauna can't be predated.</summary>
        public bool IsPredationImmune =>
            predationImmunitySeconds > 0f && _spawnTime >= 0f && (Time.time - _spawnTime) < predationImmunitySeconds;

        [Header("Population control (prey-linked)")]
        [Tooltip("Seconds this fauna can go without feeding before it starves and despawns. " +
                 "Feeding (consuming any prism, or - for predators - eating a herbivore) resets " +
                 "the clock; 0 = never starve. Concrete creature fauna (e.g. LightFauna) call " +
                 "NotifyFed() on consume and despawn when IsStarving; manager-type Fauna " +
                 "subclasses ignore it. See Docs/ECOSYSTEM.md §6.")]
        [SerializeField] protected float starvationSeconds = 30f;

        // -1 until the first Start tick so a fauna spawned when Time.time already exceeds
        // starvationSeconds isn't reported starving before its clock begins.
        float _lastFedTime = -1f;

        /// <summary>True once this fauna has gone longer than starvationSeconds without feeding.</summary>
        protected bool IsStarving =>
            starvationSeconds > 0f && _lastFedTime >= 0f && (Time.time - _lastFedTime) > starvationSeconds;

        /// <summary>
        /// Reset the starvation clock - a subclass calls this whenever it consumes prey.
        /// Feeding is also the reproduction trigger: prey converts to population
        /// (Docs/ECOSYSTEM.md §6), so every feed advances the birth counter and may
        /// birth offspring when the species' lineage config allows it.
        /// </summary>
        protected void NotifyFed()
        {
            _lastFedTime = Time.time;
            TryLevelUpFromFeeding();
            TryReproduce();
        }

        /// <summary>
        /// <b>A creature earns its level by eating</b> (Docs/ECOSYSTEM.md §33). Every creature
        /// hatches at level 1; a level costs <see cref="FaunaConfigurationSO.FeedsPerLevel"/>
        /// feeds — deliberately a multiple of what an offspring costs, so a big creature is a
        /// creature that has out-fed its siblings for a long time rather than one that won a
        /// spawn roll. 0 on the config disables levelling for that species (the worm colony,
        /// which funds its growth by segment instead).
        ///
        /// <para>Counted separately from the reproduction quota: a feed pays into both, and a
        /// birth must not reset progress toward a level (or vice versa).</para>
        /// </summary>
        void TryLevelUpFromFeeding()
        {
            var cfg = sourceConfig;
            if (!cfg || cfg.FeedsPerLevel <= 0 || Level >= MaxLifeformLevel) return;
            if (++_feedsSinceLevel < cfg.FeedsPerLevel) return;
            _feedsSinceLevel = 0;
            LevelUp();
        }

        // -------------------------------------------------------------------
        //  Reproduction - the population driver (retires the fixed-period
        //  spawner as the source of population; the spawner is now only a
        //  seeder). All tuning lives on the species' FaunaConfigurationSO; a
        //  fauna with no lineage (manager-spawned, drones) never reproduces.
        // -------------------------------------------------------------------

        Cell hostCell;
        FaunaConfigurationSO sourceConfig;

        // This individual's rolled variant (element + the block expressing it + hatch level).
        // Passed to offspring so a lineage breeds true instead of re-rolling per birth.
        LifeformVariantPick<FaunaVariantTuning>? _variantPick;
        bool lineageRegistered;
        int _feedsSinceBirth;
        int _feedsSinceLevel;
        float _lastBirthTime = float.NegativeInfinity;

        // Offspring appear within this radius of the parent - far enough not to
        // stack, close enough to join the parent's swarm/feeding ground.
        const float OffspringSpawnJitter = 25f;

        /// <summary>The species config this fauna was spawned from (null for manager-spawned/drone fauna).</summary>
        public FaunaConfigurationSO SourceConfig => sourceConfig;

        /// <summary>
        /// This individual's rolled variant - element, tuning, and the level it hatched at -
        /// or null before <see cref="AssignLineage"/> has run. Exposed so a subclass that
        /// produces a NEW individual outside the reproduction path can pass it on: the worm
        /// colony's split hands it to the severed half, because the two halves of a split
        /// worm are the same animal and must not re-roll their identity.
        /// </summary>
        protected LifeformVariantPick<FaunaVariantTuning>? VariantPick => _variantPick;

        /// <summary>
        /// Binds this fauna to its species lineage: the cell whose population it
        /// belongs to and the FaunaConfigurationSO that defines the species.
        /// Registers it in the cell's per-species live count (unregistered in
        /// OnDestroy). Called by the spawner after Initialize, and by a parent
        /// for its offspring - heredity is what lets reproduction recurse.
        /// </summary>
        public void AssignLineage(Cell host, FaunaConfigurationSO config) =>
            AssignLineage(host, config, null);

        /// <summary>
        /// Lineage bind with an optional INHERITED variant pick: a parent passes its own pick to
        /// its offspring so a lineage keeps its element (and the level it hatched at) instead of
        /// re-rolling a fresh identity every birth. Null rolls a new pick from the config - which,
        /// with spread off, is just the authored Element / Variant / InitialLevel.
        /// </summary>
        public void AssignLineage(Cell host, FaunaConfigurationSO config,
            LifeformVariantPick<FaunaVariantTuning>? inherit)
        {
            hostCell = host;
            sourceConfig = config;
            if (host && config && !lineageRegistered)
            {
                host.RegisterLiveFauna(this);
                lineageRegistered = true;
            }

            // Elemental contract: the species config may define the ELEMENT as data (one base
            // prefab, 20 data-defined variants) - re-provision the heart to that element if the
            // prefab-authored crystal disagrees - apply the variant's expression (behavior /
            // body / audio deltas that used to force a prefab variant per element), and seed the
            // spawn LEVEL (spawns AT size, nothing pops mid-life). Tuning runs BEFORE SetLevel so
            // the level curve grows from the variant's base scale.
            if (config)
            {
                // The species' own pen (see the band notes above). Authored on the config so a
                // mode pens a TIER rather than a cell, and so an offspring inherits its parent's
                // band for free - it binds the same config.
                _bandInner = Mathf.Max(0f, config.BandInnerRadius);
                _bandOuter = Mathf.Max(0f, config.BandOuterRadius);
                if (_bandOuter > 0f && _bandInner > _bandOuter)
                    (_bandInner, _bandOuter) = (_bandOuter, _bandInner);

                // The spawner sets Goal before Initialize (cell still null, so the setter's
                // clamp was a no-op) - re-run it now that the band exists, or the creature's
                // first few seconds are spent swimming to a point outside its own pen.
                if (HasBand) Goal = _goal;

                var pick = config.RollVariant(inherit);
                _variantPick = pick;

                if (pick.Element != Element.None)
                    ProvisionHeart(pick.Element);
                if (pick.Tuning is { Enabled: true })
                    ApplyVariantTuning(pick.Tuning);
                SetLevel(pick.Level, animate: false);
            }

            // A new living heart entered the world - let the domain fauna buff re-sum now
            // instead of on its next reconcile sweep.
            RaiseFaunaHeartsChanged();
        }

        /// <summary>
        /// Element-as-data landing point: gives this creature its heart of the config's
        /// picked element. Default = the standard embedded-crystal provisioning every
        /// simple fauna uses. A COLONY overrides to route the element to its MEMBERS —
        /// the worm colony forwards it to every segment, each of which is its own fauna
        /// carrying its own heart (the colony root is the population anchor and is
        /// deliberately heartless, Docs/ECOSYSTEM.md §23.3).
        /// </summary>
        protected virtual void ProvisionHeart(Element element)
        {
            crystal = LifeFormCrystal.EnsureElementalCrystal(this, element);
            if (crystal) crystal.SetEmbeddedIn(this);
        }

        /// <summary>
        /// Pokes <see cref="CellRuntimeDataSO.OnFaunaHeartsChanged"/> through the host cell's
        /// runtime SO (always wired on a live cell) rather than the per-prefab cellData wire —
        /// several fauna prefabs author cellData null or dangling, and every fauna that
        /// participates in the buff pool has a host cell by construction (AssignLineage sets
        /// it). cellData is the fallback for hostless deaths; the event field itself must fail
        /// loud if unwired on the asset.
        /// </summary>
        void RaiseFaunaHeartsChanged()
        {
            var runtimeData = hostCell ? hostCell.RuntimeData : cellData;
            if (runtimeData) runtimeData.OnFaunaHeartsChanged.Raise();
        }

        void TryReproduce()
        {
            var cfg = sourceConfig;
            var host = hostCell;
            if (!cfg || !host || cfg.FeedsPerOffspring <= 0 || !cfg.FaunaPrefab) return;

            _feedsSinceBirth++;
            // The cap is the CELL's, not the config's: a biome that scales its population
            // (SpawnProfileSO.FaunaPopulationScale) has to scale what reproduction may fill to,
            // or the seeder and the food web would be working to two different ceilings.
            if (!FaunaReproductionRules.ShouldBirth(
                    _feedsSinceBirth, cfg.FeedsPerOffspring,
                    Time.time - _lastBirthTime, cfg.ReproductionCooldownSeconds,
                    host.GetLiveFaunaCount(cfg), host.ResolveFaunaCap(cfg)))
                return;

            _lastBirthTime = Time.time;
            _feedsSinceBirth = 0;

            int offspring = Mathf.Max(1, cfg.OffspringPerBirth);
            for (int i = 0; i < offspring; i++)
            {
                // Re-check the cap per birth so a multi-offspring birth can't
                // overshoot the performance backstop.
                if (host.IsFaunaAtCap(cfg))
                    break;
                SpawnOffspring(host, cfg);
            }
        }

        void SpawnOffspring(Cell host, FaunaConfigurationSO cfg)
        {
            Vector3 pos = transform.position + Random.insideUnitSphere * OffspringSpawnJitter;
            var child = Instantiate(cfg.FaunaPrefab, pos, Quaternion.identity);
            child.domain = domain;
            child.Goal = Goal;
            // Same lifecycle as a spawner birth: Initialize grows the body prisms
            // and starts behavior; AssignLineage registers the species count and
            // passes heredity so the child can reproduce in turn. Predation
            // immunity (stamped in Awake) gives it time to disperse.
            child.Initialize(host);
            // Heredity: the child inherits this parent's variant pick - its element and the
            // level it hatched at - rather than rolling a new identity. In-world level-ups
            // (the Shepherd joust) are NOT inherited: acquired growth is not heritable.
            child.AssignLineage(host, cfg, _variantPick);
            host.RegisterSpawnedObject(child.gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (lineageRegistered && hostCell)
                hostCell.UnregisterLiveFauna(this);
            lineageRegistered = false;

            // Last line of defence for a DEFERRED heart (see DefersHeartRelease): an interrupted
            // wither - a cell drain, a manager pulling the husk, a turn ending - never reaches
            // the release inside the wither. This is a genuine recovery rather than a hopeful
            // one only because StashHeart already re-homed the crystal onto the cell at the top
            // of Die, so it is not a child of this husk and releasing it here still works.
            // Skipped during scene unload, where the cascade must not run at all (the same rule
            // Spindle.OnDisable follows) and where nothing survives to collect anyway.
            if (_diedThisLife && !_heartReleased && gameObject.scene.isLoaded)
                ReleaseHeart();
        }

        // --- ILifeFormEntity ---
        public Domains Domain => domain;
        public GameObject GetGameObject() => gameObject;

        // --- Elemental contract (element x level - one base prefab, 20 data-defined variants) ---

        /// <summary>Level cap for every lifeform (the 4 elements x 5 levels contract).</summary>
        public const int MaxLifeformLevel = 5;

        /// <summary>The element this creature carries - single source: its crystal (the heart).</summary>
        public Element Element => crystal ? crystal.crystalProperties.Element : Element.None;

        /// <summary>This creature's level, 1..MaxLifeformLevel. Scales body + crystal via the species config.</summary>
        public int Level { get; private set; } = 1;

        /// <summary>Current travel speed (world units/s). Mobile subclasses (Boid, LightFauna)
        /// override with their live velocity; manager/rooted fauna read as stationary.</summary>
        public virtual float CurrentSpeed => 0f;

        /// <summary>
        /// A faster vessel jousted this creature's heart - routes through Predated
        /// (idempotent, immunity-respecting) so it withers and drops its crystal.
        ///
        /// The style is stamped BEFORE the death runs because the death READS it: a joust
        /// frees the heart at once (the jouster reached in and took it) and the body then
        /// unravels from the hole outward, the mirror of a starvation wither. A joust that
        /// does not land - spawn immunity, or a creature already dying - leaves the style
        /// alone, so a later starvation still withers the ordinary way.
        /// </summary>
        public bool Jousted(string killerName)
        {
            var previousStyle = _deathStyle;
            _deathStyle = LifeformDeathStyle.Jousted;
            if (Predated(killerName)) return true;
            _deathStyle = previousStyle;
            return false;
        }

        Vector3 _levelBaseScale = Vector3.one;   // root scale at level 1 (captured on first level apply)
        bool _levelBaseCaptured;
        Coroutine _levelGrowRoutine;

        float BodyScalePerLevel => sourceConfig ? sourceConfig.BodyScalePerLevel : 1.15f;
        float LevelGrowSeconds => sourceConfig ? sourceConfig.LevelGrowSeconds : 1f;

        /// <summary>
        /// Raises this creature's level by one (clamped at <see cref="MaxLifeformLevel"/>),
        /// GROWING the body and its embedded crystal over LevelGrowSeconds - the continuity law:
        /// a level-up blooms, it never pops. Returns false at the cap (callers skip their juice).
        /// Raised in-world by active forces (e.g. an own-domain Crystal Joust).
        /// </summary>
        public bool LevelUp()
        {
            if (Level >= MaxLifeformLevel) return false;
            SetLevel(Level + 1, animate: true);
            return true;
        }

        /// <summary>Applies a level directly (spawn-time seeding animates nothing - it spawns AT size).</summary>
        protected void SetLevel(int level, bool animate)
        {
            level = Mathf.Clamp(level, 1, MaxLifeformLevel);
            if (!_levelBaseCaptured)
            {
                _levelBaseScale = transform.localScale;
                _levelBaseCaptured = true;
            }

            Level = level;
            Vector3 targetScale = _levelBaseScale * Mathf.Pow(BodyScalePerLevel, Level - 1);

            if (!animate || !isActiveAndEnabled)
            {
                transform.localScale = targetScale;
            }
            else
            {
                if (_levelGrowRoutine != null) StopCoroutine(_levelGrowRoutine);
                _levelGrowRoutine = StartCoroutine(GrowToScale(targetScale, LevelGrowSeconds));
            }

            // The heart grows with the level so the eventual death drop is a bigger powerup
            // (crystal value reads lossyScale live at collect time - mass rewarded). Its size is
            // the SHARED level curve, not this species' body scale: a shark's heart and a
            // tadpole's are the same size at the same level (Docs/ECOSYSTEM.md §33). The
            // animation therefore works in WORLD scale and divides out the parent chain every
            // frame, which is also what holds the heart's size steady while the body grows
            // underneath it.
            if (crystal)
            {
                float worldTarget = LifeFormCrystal.WorldScaleForLevel(Level);
                if (animate && isActiveAndEnabled && crystal.gameObject.activeInHierarchy)
                    StartCoroutine(GrowCrystalWithPop(worldTarget, LevelGrowSeconds));
                else
                    LifeFormCrystal.SetWorldScale(crystal, worldTarget);
            }
        }

        /// <summary>
        /// Applies the config's per-variant expression - the deltas that used to force a prefab
        /// variant per element (see FaunaVariantTuning). The base handles what every fauna has:
        /// body scale, spindle material, starvation, forager-agnostic survival; Boid layers the
        /// flocking numbers on top. Runs at AssignLineage, BEFORE the level curve seeds, and
        /// before the creature is visible-established (spawn-time - continuity is not violated).
        /// </summary>
        public virtual void ApplyVariantTuning(FaunaVariantTuning tuning)
        {
            if (tuning == null) return;

            if (tuning.BaseBodyScale > 0f)
                transform.localScale = Vector3.one * tuning.BaseBodyScale;

            if (tuning.StarvationSeconds >= 0f)
                starvationSeconds = tuning.StarvationSeconds;

            // Per-element body PRISM shape: retarget every body HealthPrism's TargetScale (the
            // bloom target - the clock growth stamp blooms toward it, so a post-Initialize
            // retarget still grows continuously, never pops).
            if (tuning.BodyPrismScale != Vector3.zero)
            {
                foreach (var hp in GetComponentsInChildren<HealthPrism>(true))
                    if (hp) hp.TargetScale = tuning.BodyPrismScale;
            }

            // Per-element body look: swap the spindle renderers' shared material (never
            // renderer.material - that clones). Crystal models keep their own materials.
            if (tuning.BodyMaterial)
            {
                foreach (var sp in GetComponentsInChildren<Spindle>(true))
                {
                    if (sp && sp.TryGetComponent<Renderer>(out var rend))
                        rend.sharedMaterial = tuning.BodyMaterial;
                }
            }

            // Per-element audio loop: retarget the FMOD emitter before its ObjectStart play
            // (AssignLineage runs in the spawn call, ahead of the emitter's Start). An empty
            // reference with OverrideAudio on silences the loop (the Space tadpole is silent).
            var emitter = tuning.OverrideAudio
                ? GetComponentInChildren<FMODUnity.StudioEventEmitter>(true)
                : null;
            if (emitter)
            {
                emitter.EventReference = tuning.AudioLoopEvent;
                if (tuning.AudioMinDistance >= 0f || tuning.AudioMaxDistance >= 0f)
                {
                    emitter.OverrideAttenuation = true;
                    if (tuning.AudioMinDistance >= 0f) emitter.OverrideMinDistance = tuning.AudioMinDistance;
                    if (tuning.AudioMaxDistance >= 0f) emitter.OverrideMaxDistance = tuning.AudioMaxDistance;
                }
            }
        }

        /// <summary>
        /// Level-up crystal growth with an OVERSHOOT pop: the heart flares past its new size in
        /// the first quarter of the animation, then settles - readable at flight speed, where a
        /// plain 20%-over-a-second ease is too subtle to notice. Still continuous (never pops in).
        /// </summary>
        IEnumerator GrowCrystalWithPop(float worldTarget, float seconds)
        {
            if (!crystal) yield break;
            var t = crystal.transform;
            float start = t.lossyScale.x;
            float flare = worldTarget * 1.6f;
            float flareTime = Mathf.Max(0.05f, seconds * 0.25f);
            float settleTime = Mathf.Max(0.05f, seconds - flareTime);

            // Stop the moment the heart stops riding this body: ANY death reparents the crystal
            // to the cell (ActivateCrystal, or StashHeart for a deferred release), and a write
            // that divides out THIS body's scale would land as the wrong WORLD scale there. The
            // drop keeps the world scale it had at death, which is exactly the value the domain
            // buff was granting at that moment. The test is `_diedThisLife` rather than the
            // embedded state, because a stashed heart is still embedded (that is what keeps it
            // uncollectable) and would sail past an EmbeddedIn check.
            for (float e = 0f; e < flareTime; e += Time.deltaTime)
            {
                if (!crystal || _diedThisLife || !ReferenceEquals(crystal.EmbeddedIn, this)) yield break;
                LifeFormCrystal.SetWorldScale(crystal, Mathf.Lerp(start, flare, e / flareTime));
                yield return null;
            }
            for (float e = 0f; e < settleTime; e += Time.deltaTime)
            {
                if (!crystal || _diedThisLife || !ReferenceEquals(crystal.EmbeddedIn, this)) yield break;
                float u = e / settleTime;
                LifeFormCrystal.SetWorldScale(
                    crystal, Mathf.Lerp(flare, worldTarget, u * u * (3f - 2f * u)));
                yield return null;
            }
            if (crystal && !_diedThisLife && ReferenceEquals(crystal.EmbeddedIn, this))
                LifeFormCrystal.SetWorldScale(crystal, worldTarget);
        }

        /// <summary>
        /// Creature-rig scale bloom over <paramref name="seconds"/>. Parent scale
        /// is mover-contract (C6 (b), 2026-08-25): a per-prism grow stamp cannot
        /// express a parent transform — the entity matrix is the composed world
        /// matrix. Locomotion already re-syncs every body prism from Update
        /// (<c>Boid</c> / <c>LightFauna</c> / <c>WormFauna</c>); do not call
        /// <see cref="NotifyBodyPrismsMoved"/> here or the grow doubles the
        /// per-frame prism write.
        /// </summary>
        IEnumerator GrowToScale(Vector3 target, float seconds)
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, seconds);
                transform.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(t));
                yield return null;
            }
            _levelGrowRoutine = null;
        }

        // Prefer the explicit host cell (set by Initialize/AssignLineage). The
        // cellData runtime SO is a SHARED asset holding only the LAST cell that
        // initialized it, so in multi-cell scenes the SO path can point a fauna at
        // the wrong cell; it remains the fallback for scene-placed managers that
        // are never Initialize(cell)-called. Unity-null guards on both so callers
        // just get null and skip the goal/avoidance branches that need a cell.
        protected Cell cell => hostCell ? hostCell : (cellData ? cellData.Cell : null);

        /// <summary>
        /// Squared length below which a steering vector counts as degenerate. A steering
        /// sum that cancels to ~zero normalizes to <see cref="Vector3.zero"/>, which zeroes
        /// the creature's velocity - and a motionless creature recomputes the identical
        /// zero from the identical position on every later tick, so the stall is PERMANENT
        /// (it hangs in place until it starves). Steering code must test against this and
        /// keep its last heading rather than publish a zero direction.
        /// </summary>
        protected const float DegenerateSteeringSqr = 1e-6f;

        /// <summary>
        /// Shielded mass is not food for ANY herbivore - the one canonical rule every
        /// species' edibility predicate routes through, so no new grazer can re-acquire
        /// the bug. A SUPER-shielded prism is fully invulnerable and a SHIELDED one only
        /// sheds its shield (see <see cref="Prism.Consume"/>), so a grazer that adopts one
        /// as its feed target approaches it, faces it, "eats" it, and finds it still there
        /// on the next mouthful-chaining query - grazing forever without ever removing
        /// mass. That is what parked brittlestars on Skim Race's super-shielded track
        /// prisms. Excluding shields from the predicate makes the creature skip straight
        /// to the next normal prism on the same behavior tick.
        /// </summary>
        protected static bool IsShieldedMass(Prism prism)
        {
            var properties = prism ? prism.prismProperties : null;
            return properties != null && (properties.IsShielded || properties.IsSuperShielded);
        }

        /// <summary>
        /// Shared scratch buffer for Physics.OverlapSphereNonAlloc in fauna
        /// behavior ticks. All fauna tick on the main thread and consume the
        /// buffer within a single call, so one static buffer serves every
        /// creature - eliminating the per-tick Collider[] allocation that made
        /// large swarms GC-churn. NonAlloc silently truncates beyond capacity,
        /// which for steering/grazing just means considering a subset of an
        /// extremely dense neighborhood that tick.
        /// </summary>
        protected static readonly Collider[] OverlapScratch = new Collider[256];

        /// <summary>
        /// Shared scratch list for PrismSpatialIndex.QuerySphere in fauna behavior
        /// ticks - the prism half of the neighborhood scan (the physics half above
        /// now only carries non-prism populations like vessels). Same single-buffer
        /// rationale as <see cref="OverlapScratch"/>; reproduction's deferred first
        /// behavior tick (see Boid.CalculateBehaviorCoroutine) keeps a parent from
        /// having its snapshot clobbered mid-iteration.
        /// </summary>
        protected static readonly List<Prism> PrismScratch = new(256);

        /// <summary>
        /// Separate scratch list for the small per-MOUTHFUL cluster query intentional
        /// feeding runs when a herbivore actually consumes (LightFauna.ConsumeMouthful /
        /// Boid feeding). Kept distinct from <see cref="PrismScratch"/> because mouthful
        /// consumption fires from per-frame Update code, which must never clobber a
        /// behavior tick's in-flight neighborhood snapshot.
        /// </summary>
        protected static readonly List<Prism> FeedScratch = new(64);

        /// <summary>
        /// Overlap mask for the physics half of fauna scans: everything EXCEPT prism
        /// layers (TrailBlocks + Mound). Prisms - including other fauna's body
        /// HealthPrisms - are served by PrismSpatialIndex.QuerySphere instead, so the
        /// physics query stops paying broadphase + GetComponent costs for thousands
        /// of prism colliders (and stops truncating ships out of the 256-slot scratch
        /// in dense fields). Lazy so LayerMask resolves after engine init.
        /// </summary>
        static int s_nonPrismOverlapMask;
        protected static int NonPrismOverlapMask =>
            s_nonPrismOverlapMask != 0
                ? s_nonPrismOverlapMask
                : s_nonPrismOverlapMask = ~LayerMask.GetMask("TrailBlocks", "Mound");

        // --- Body prisms (the movers contract with PrismSpatialIndex) -------
        // Fauna bodies are HealthPrisms - registered prism mass that MOVES every
        // frame. The index stores positions, so the mover must keep them honest
        // (Docs/SPATIAL_INDEX.md): otherwise batch AOE hits the creature at its
        // spawn point and index-served fauna senses look for it where it used
        // to be.

        HealthPrism[] _bodyPrisms;

        /// <summary>
        /// Caches this fauna's body HealthPrisms for the per-frame movement
        /// notification, and stamps each with its owner so fauna senses resolve
        /// "whose body is this prism" with a field read (HealthPrism.OwnerFauna)
        /// instead of a GetComponentInParent walk per neighbor per behavior tick.
        /// Call from Initialize, after the body hierarchy exists.
        /// Returns the cached array so subclasses can reuse it for body setup.
        /// </summary>
        protected HealthPrism[] CacheBodyPrisms()
        {
            _bodyPrisms = GetComponentsInChildren<HealthPrism>(true);
            for (int i = 0; i < _bodyPrisms.Length; i++)
            {
                if (_bodyPrisms[i])
                    _bodyPrisms[i].OwnerFauna = this;
            }
            return _bodyPrisms;
        }

        /// <summary>The cached body prisms (see <see cref="CacheBodyPrisms"/>). May contain destroyed entries.</summary>
        protected HealthPrism[] BodyPrisms => _bodyPrisms;

        /// <summary>True while any body prism is alive — the creature's health read.</summary>
        public bool HasLiveBodyPrisms
        {
            get
            {
                var prisms = _bodyPrisms;
                if (prisms == null) return false;
                for (int i = 0; i < prisms.Length; i++)
                {
                    var hp = prisms[i];
                    if (hp && !hp.destroyed) return true;
                }
                return false;
            }
        }

        // One-shot guard: a burst (a missile's AOE, a shotgun frame) can strip the last
        // several body prisms inside one frame, and every one of them calls back here.
        bool _diedFromBodyLoss;

        /// <summary>
        /// A prism of this creature's BODY was destroyed by an active force (vessel,
        /// projectile, AOE). Raised by <see cref="HealthPrism.Explode"/> through the
        /// stamped owner. <paramref name="killerName"/> is the attribution the destruction
        /// pipeline carried.
        ///
        /// THE PLAYER KILL PATH (platform-wide since Wildlife Liberation, 2026-08): when the
        /// LAST body prism is gone the creature dies through the sealed <see cref="Die"/> —
        /// it withers / is suctioned out (continuity of existence) and drops its elemental
        /// crystal (mass conserved), exactly like a starvation or predation death. Before
        /// this only <c>WormSegmentFauna</c> implemented it, so shooting any other creature
        /// stripped its body and left an immortal husk swimming; a whole class of vessel
        /// (the Sparrow, whose verbs ARE guns and missiles) could not kill wildlife at all.
        ///
        /// This is an ACTIVE force removing mass, so it is squarely inside the conserved-mass
        /// law — there is no timer, no lifespan and no cull here; a creature nobody shoots
        /// still only ever dies to starvation or predation. Subclasses may override to add
        /// topology bookkeeping (the worm colony re-links its chain first), but a creature
        /// that survives losing its whole body would violate the rule, so overrides must end
        /// in a death.
        /// </summary>
        public virtual void OnBodyPrismExploded(HealthPrism prism, string killerName)
        {
            if (_diedFromBodyLoss || HasLiveBodyPrisms) return;
            _diedFromBodyLoss = true;
            Die(killerName);
        }

        /// <summary>
        /// Pushes the body prisms' current positions into the spatial index. Call
        /// every frame after moving the creature. Cheap: the index only rebuckets
        /// when a body crosses an 8m occupancy-bucket boundary; unregistered
        /// bodies (inside Prism.waitTime) no-op.
        /// </summary>
        protected void NotifyBodyPrismsMoved()
        {
            var prisms = _bodyPrisms;
            if (prisms == null) return;
            for (int i = 0; i < prisms.Length; i++)
            {
                var hp = prisms[i];
                if (hp) hp.NotifyPositionChanged();
            }
        }

        protected virtual void Awake()
        {
            // Stamp spawn time as early as possible (Instantiate runs Awake synchronously,
            // before the spawner calls Initialize or any predator's first behavior tick), so
            // predation immunity is active from the moment the creature exists.
            _spawnTime = Time.time;
        }

        protected virtual void Start()
        {
            if (domain == Domains.Blue)
                CSDebug.LogWarning($"{name}: Population domain is Blue (sentinel). Assign a real domain before spawning, or set it on the prefab.");

            _goalOrbitOffset = Random.onUnitSphere * Mathf.Max(0f, goalOrbitRadius);
            _lastFedTime = Time.time; // start the starvation clock when the creature comes alive

            StartCoroutine(UpdateGoalCoroutine());
        }

        /// <summary>
        /// Initialize this fauna with its parent cell. Overrides must call base -
        /// the base remembers the spawning cell explicitly (the shared cellData SO
        /// only tracks the LAST cell that initialized it, which is wrong in
        /// multi-cell scenes). Otherwise intentionally minimal so managers and
        /// stubs don't need to throw NotImplementedException.
        /// </summary>
        public virtual void Initialize(Cell cell)
        {
            hostCell = cell;
        }

        /// <summary>
        /// The elemental crystal this fauna conserves its mass into on death. Set by
        /// concrete creature subclasses in Initialize via
        /// <see cref="LifeFormCrystal.EnsureElementalCrystal"/>; null only for MANAGER
        /// fauna and for a colony ROOT, which is a population anchor rather than an
        /// organism — every actual member of a colony carries its own heart
        /// (Docs/ECOSYSTEM.md §23.3).
        /// </summary>
        protected Crystal crystal;

        /// <summary>
        /// The living embedded heart: non-null only while this fauna is alive and its elemental
        /// crystal is still embedded in it. <see cref="ReleaseHeart"/> is what frees it, so this
        /// returns null from the exact moment the crystal becomes a collectible — the domain
        /// fauna buff keys off this so a fauna's domain-wide power ends precisely when its
        /// crystal (the same heart, at the same world scale, carrying the same value) hits the
        /// open water. On an outside-in wither that moment is the END of the wither, not the
        /// start of the death: the heart is the last thing standing, so a starving creature
        /// keeps powering its domain until the wither reaches its core.
        /// </summary>
        public Crystal LiveHeart =>
            crystal && crystal.gameObject.activeInHierarchy && ReferenceEquals(crystal.EmbeddedIn, this)
                ? crystal
                : null;

        // How this creature came apart - see LifeformDeathStyle. Written by the force that
        // killed it (Jousted / the devour overload of Predated); starvation and every other
        // death leave the default.
        LifeformDeathStyle _deathStyle = LifeformDeathStyle.Withered;

        /// <summary>How this creature is coming apart - read by subclass death animations.</summary>
        protected LifeformDeathStyle DeathStyle => _deathStyle;

        bool _heartReleased;
        bool _diedThisLife;   // Die ran - arms the OnDestroy heart backstop below

        /// <summary>
        /// True when this subclass runs a progressive wither that calls
        /// <see cref="ReleaseHeart"/> itself at the moment the wither reaches the core -
        /// the "outside-in until the crystal becomes collectable" death. Everything else
        /// drops the heart inside <see cref="Die"/> exactly as before, so the crystal
        /// invariant cannot be lost by forgetting to opt in. A subclass that DOES opt in
        /// must release, and <see cref="OnDestroy"/> screams if it didn't.
        /// </summary>
        protected virtual bool DefersHeartRelease => false;

        /// <summary>
        /// Death chokepoint - SEALED so no fauna can die without conserving its mass.
        /// Every death path (starvation, <see cref="Predated"/>, a jouster taking the
        /// heart, losing the last body prism) routes here; it frees the elemental crystal
        /// (the locked "every lifeform drops one elemental crystal on death, mass is
        /// conserved" invariant - the creature does not just vanish) and then runs
        /// subclass removal via <see cref="OnDeath"/>.
        ///
        /// WHEN the heart is freed is part of HOW the creature died (Docs/ECOSYSTEM.md §26):
        ///   • Jousted  - a vessel reached in and TOOK it, so it is free immediately and
        ///                the joust chain awards it to that pilot.
        ///   • Consumed - a predator is tearing the body apart around it; free immediately,
        ///                as it always has been.
        ///   • Withered - the body is spent from the outside in and the heart is the LAST
        ///                thing standing, so a subclass with a progressive wither
        ///                (<see cref="DefersHeartRelease"/>) releases it when the wither
        ///                reaches the core. Everything else still releases here.
        /// </summary>
        protected void Die(string killerName = "")
        {
            _diedThisLife = true;

            if (_deathStyle != LifeformDeathStyle.Withered || !DefersHeartRelease)
                ReleaseHeart();
            else
                StashHeart();

            ReportKill(killerName);
            OnDeath(killerName);
        }

        /// <summary>
        /// Re-homes the heart onto the cell but leaves it EMBEDDED - still uncollectable, still
        /// wearing the neutral heart tint - for a wither that will release it when it reaches the
        /// core. Doing this at the START of the death is what makes the deferral SAFE rather than
        /// merely late: a crystal still parented to the husk dies with it, so an interrupted
        /// wither (a cell drain, a manager removing the husk, a scene unload) would silently lose
        /// the crystal. With it re-homed, <see cref="ReleaseHeart"/> works from anywhere,
        /// including the <see cref="OnDestroy"/> backstop.
        /// </summary>
        void StashHeart()
        {
            if (crystal && crystal.gameObject && crystal.gameObject.activeInHierarchy)
                crystal.DetachHeartToCell();
        }

        /// <summary>
        /// Frees this creature's heart into the world as the collectible elemental crystal -
        /// the sealed half of "every lifeform drops one elemental crystal on death".
        /// ActivateCrystal reparents it to the cell, so it survives this object's
        /// destruction as a powerup. Idempotent: the wither, the husk removal and the
        /// backstop can all call it.
        /// </summary>
        protected void ReleaseHeart()
        {
            if (_heartReleased) return;
            _heartReleased = true;

            if (crystal && crystal.gameObject && crystal.gameObject.activeInHierarchy)
                crystal.ActivateCrystal();

            // The heart just left the living pool - poke the domain fauna buff so the
            // domain's power drops with the death, not on the next reconcile sweep.
            RaiseFaunaHeartsChanged();
        }

        /// <summary>
        /// Leaves this creature's body prisms in the world as its SKELETON - the frame stays
        /// where it died instead of being destroyed with the husk (Docs/ECOSYSTEM.md §26).
        /// Called by the wither styles (starvation, joust); a devoured creature does NOT
        /// call it, because there the mass genuinely transfers to the predator.
        ///
        /// Run this BEFORE withering any spindle: a body prism is parented to a spindle, so
        /// evaporating the spindle first would destroy the very mass the skeleton conserves.
        /// </summary>
        protected void LeaveSkeleton()
        {
            var host = cell;
            Transform skeletonParent = host ? host.transform : null;

            var prisms = GetComponentsInChildren<HealthPrism>(true);
            for (int i = 0; i < prisms.Length; i++)
            {
                var hp = prisms[i];
                if (hp && !hp.destroyed) hp.LeaveAsSkeleton(skeletonParent);
            }
        }

        /// <summary>
        /// Publishes an ATTRIBUTED death on the cell's SOAP channel - the fauna half of the
        /// stat <see cref="LifeForm.OnLifeFormDeath"/> already reports for flora, routed through
        /// a ScriptableEvent rather than a static event per the SOAP policy. StatsManager
        /// (server only) turns it into <see cref="IRoundStats.LifeformsKilled"/>.
        ///
        /// Only PLAYER-attributed deaths are published. The ecology's own deaths carry engine
        /// attribution ("starvation", a predator's name, a colony wither reason) and must not
        /// score for anyone - a mode whose objective is killing wildlife cannot have the wildlife
        /// killing itself onto the scoreboard. StatsManager resolves the name against the player
        /// roster anyway, so this is belt-and-braces on a hot-ish path.
        /// </summary>
        void ReportKill(string killerName)
        {
            if (string.IsNullOrEmpty(killerName)) return;
            if (killerName == StarvationKiller) return;

            var runtimeData = hostCell ? hostCell.RuntimeData : cellData;
            if (runtimeData) runtimeData.OnFaunaKilled.Raise(killerName);
        }

        /// <summary>Attribution string every starvation death uses - never a scoring kill.</summary>
        public const string StarvationKiller = "starvation";

        /// <summary>
        /// Subclass death behavior (manager removal / destroy / worm-splitting). Override
        /// THIS, not <see cref="Die"/> - the crystal drop is sealed into Die so the mass-
        /// conservation invariant cannot be bypassed by a subclass. Default is empty so
        /// managers and stubs don't need to throw NotImplementedException.
        /// </summary>
        protected virtual void OnDeath(string killerName = "") { }

        // Idempotency for predation: two predators can reach the same herbivore on the
        // same frame (each iterating its own OverlapSphere snapshot). Without this guard
        // the second Predated() re-enters Die() and double-removes / double-destroys.
        bool _consumedAsPrey;

        /// <summary>False once a predator has eaten this fauna - predators skip already-eaten prey.</summary>
        public bool IsAlivePrey => !_consumedAsPrey;

        /// <summary>
        /// Where a devouring predator wants this prey's body prisms suctioned to (the
        /// predator's mouth). Set by the devour overload of <see cref="Predated"/> before
        /// Die runs, so subclass OnDeath can choose the break-apart-and-suction exit over
        /// the default wither. Null for non-predation deaths (starvation).
        /// </summary>
        protected Transform DevourTarget { get; private set; }

        /// <summary>
        /// A predator has caught this fauna. Routes through the normal <see cref="Die"/> path
        /// (manager removal / destroy), is idempotent, and respects the post-spawn predation
        /// immunity window. Returns true only if the prey was actually eaten this call - the
        /// predator should reset its starvation clock (NotifyFed) only on a true result.
        /// </summary>
        public bool Predated(string predatorName = "predator") => Predated(predatorName, null);

        /// <summary>
        /// Devour variant: <paramref name="devourTarget"/> is the predator's mouth — the
        /// suction sink the prey's body prisms implode toward (continuity rule: the prey
        /// breaks apart and is pulled into the mouth, never popping out of existence). The
        /// sealed <see cref="Die"/> still drops the elemental crystal first (mass conserved).
        /// </summary>
        public virtual bool Predated(string predatorName, Transform devourTarget)
        {
            if (_consumedAsPrey || IsPredationImmune) return false;
            _consumedAsPrey = true;
            DevourTarget = devourTarget;
            // A mouth to suction into IS the consumed style: the body transfers to the
            // eater, so there is no skeleton and no wither ordering to pick.
            if (devourTarget) _deathStyle = LifeformDeathStyle.Consumed;
            Die(predatorName);
            return true;
        }

        public void SetTeam(Domains domain)
        {
            this.domain = domain;
        }

        IEnumerator UpdateGoalCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(GetAggressionScaledGoalInterval());
                if (cell == null) continue;
                Goal = ResolveGoal();
            }
        }

        /// <summary>
        /// Targeting strategy by aggression level (per user spec):
        ///   Level0 - head toward the cell's crystal
        ///   Level1 - head toward the nearest opposing-color centroid
        ///   Level2 - head toward the nearest centroid of ANY color
        ///
        /// A per-instance orbit offset is added at Levels 0 and 1 so the pack spreads
        /// around the target. Level 2 skips the offset - at berserk aggression we
        /// want tight convergence onto the densest cleanup target.
        /// </summary>
        protected virtual Vector3 ResolveGoal()
        {
            if (cell == null) return Goal;

            switch (cell.AggressionLevel)
            {
                case CellAggressionLevel.Level2:
                    return cell.GetDensestRegionAnyDomain();

                case CellAggressionLevel.Level1:
                    return cell.GetExplosionTarget(domain) + _goalOrbitOffset;

                case CellAggressionLevel.Level0:
                default:
                    // Voracious exterior: with a nucleus control zone, sensed mass
                    // outside the nucleus is prey at EVERY phase - hunt its densest
                    // region even at Calm instead of idling at the crystal. (The
                    // targeting grids only ever hold exterior mass in such cells.)
                    if (cell.HasNucleusControlZone && cell.HasSensedExteriorMass)
                        return cell.GetDensestRegionAnyDomain() + _goalOrbitOffset;

                    Vector3 anchor = cellData && cellData.CrystalTransform
                        ? cellData.CrystalTransform.position
                        : cell.transform.position;
                    return anchor + _goalOrbitOffset;
            }
        }

        float GetAggressionScaledGoalInterval()
        {
            float baseInterval = Mathf.Max(0.05f, goalUpdateInterval);
            if (cell == null || goalUpdateIntervalByAggression == null || goalUpdateIntervalByAggression.Length == 0)
                return baseInterval;

            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, goalUpdateIntervalByAggression.Length - 1);
            float mult = Mathf.Max(0.05f, goalUpdateIntervalByAggression[idx]);
            return baseInterval * mult;
        }
    }
}
