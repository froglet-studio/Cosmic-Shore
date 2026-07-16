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
        public Vector3 Goal;

        // Stable per-instance offset so each fauna orbits its resolved goal at a
        // different point. Seeded once at Start so the spread is deterministic per
        // spawn but varied across the pack.
        Vector3 _goalOrbitOffset;

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
            TryReproduce();
        }

        // -------------------------------------------------------------------
        //  Reproduction - the population driver (retires the fixed-period
        //  spawner as the source of population; the spawner is now only a
        //  seeder). All tuning lives on the species' FaunaConfigurationSO; a
        //  fauna with no lineage (manager-spawned, drones) never reproduces.
        // -------------------------------------------------------------------

        Cell hostCell;
        FaunaConfigurationSO sourceConfig;
        bool lineageRegistered;
        int _feedsSinceBirth;
        float _lastBirthTime = float.NegativeInfinity;

        // Offspring appear within this radius of the parent - far enough not to
        // stack, close enough to join the parent's swarm/feeding ground.
        const float OffspringSpawnJitter = 25f;

        /// <summary>The species config this fauna was spawned from (null for manager-spawned/drone fauna).</summary>
        public FaunaConfigurationSO SourceConfig => sourceConfig;

        /// <summary>
        /// Binds this fauna to its species lineage: the cell whose population it
        /// belongs to and the FaunaConfigurationSO that defines the species.
        /// Registers it in the cell's per-species live count (unregistered in
        /// OnDestroy). Called by the spawner after Initialize, and by a parent
        /// for its offspring - heredity is what lets reproduction recurse.
        /// </summary>
        public void AssignLineage(Cell host, FaunaConfigurationSO config)
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
                if (config.Element != Element.None)
                {
                    crystal = LifeFormCrystal.EnsureElementalCrystal(this, config.Element);
                    if (crystal) crystal.SetEmbeddedIn(this);
                }
                if (config.Variant is { Enabled: true })
                    ApplyVariantTuning(config.Variant);
                SetLevel(config.InitialLevel, animate: false);
            }
        }

        void TryReproduce()
        {
            var cfg = sourceConfig;
            var host = hostCell;
            if (!cfg || !host || cfg.FeedsPerOffspring <= 0 || !cfg.FaunaPrefab) return;

            _feedsSinceBirth++;
            if (!FaunaReproductionRules.ShouldBirth(
                    _feedsSinceBirth, cfg.FeedsPerOffspring,
                    Time.time - _lastBirthTime, cfg.ReproductionCooldownSeconds,
                    host.GetLiveFaunaCount(cfg), cfg.MaxLivePopulation))
                return;

            _lastBirthTime = Time.time;
            _feedsSinceBirth = 0;

            int offspring = Mathf.Max(1, cfg.OffspringPerBirth);
            for (int i = 0; i < offspring; i++)
            {
                // Re-check the cap per birth so a multi-offspring birth can't
                // overshoot the performance backstop.
                if (cfg.MaxLivePopulation > 0 && host.GetLiveFaunaCount(cfg) >= cfg.MaxLivePopulation)
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
            child.AssignLineage(host, cfg);
            host.RegisterSpawnedObject(child.gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (lineageRegistered && hostCell)
                hostCell.UnregisterLiveFauna(this);
            lineageRegistered = false;
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

        Vector3 _levelBaseScale = Vector3.one;   // root scale at level 1 (captured on first level apply)
        float _crystalBaseScale = 1f;            // crystal local scale at level 1
        bool _levelBaseCaptured;
        Coroutine _levelGrowRoutine;

        float BodyScalePerLevel => sourceConfig ? sourceConfig.BodyScalePerLevel : 1.15f;
        float CrystalScalePerLevel => sourceConfig ? sourceConfig.CrystalScalePerLevel : 1.2f;
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
                if (crystal) _crystalBaseScale = crystal.transform.localScale.x;
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
            // (crystal value reads lossyScale live at collect time - mass rewarded). The crystal
            // is a child of the root, so divide the body growth back out of its LOCAL target.
            if (crystal)
            {
                // The body grows by pow(BodyScalePerLevel, L-1), so the crystal's LOCAL target is
                // its world target divided by the body growth (it lands at the world size wanted).
                float worldTarget = _crystalBaseScale * Mathf.Pow(CrystalScalePerLevel, Level - 1);
                float localTarget = worldTarget / Mathf.Pow(BodyScalePerLevel, Level - 1);
                if (animate && crystal.gameObject.activeInHierarchy)
                    crystal.GrowCrystal(LevelGrowSeconds, localTarget);
                else
                    crystal.transform.localScale = Vector3.one * localTarget;
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

        IEnumerator GrowToScale(Vector3 target, float seconds)
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, seconds);
                transform.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(t));
                NotifyBodyPrismsMoved(); // keep the spatial index honest while the body grows
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
        /// <see cref="LifeFormCrystal.EnsureElementalCrystal"/>; null for manager /
        /// composite-segment fauna that are not standalone lifeforms (their crystal is
        /// owned at the whole-creature level).
        /// </summary>
        protected Crystal crystal;

        /// <summary>
        /// Death chokepoint - SEALED so no fauna can die without conserving its mass.
        /// Every death path (starvation, <see cref="Predated"/>) routes here; it drops
        /// the elemental crystal (the locked "every lifeform drops one elemental crystal
        /// on death, mass is conserved" invariant - the creature does not just vanish)
        /// and then runs subclass removal via <see cref="OnDeath"/>. ActivateCrystal
        /// reparents the crystal to the cell, so it survives this object's destruction
        /// as a collectible powerup.
        /// </summary>
        protected void Die(string killerName = "")
        {
            if (crystal && crystal.gameObject && crystal.gameObject.activeInHierarchy)
                crystal.ActivateCrystal();
            OnDeath(killerName);
        }

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
        /// A predator has caught this fauna. Routes through the normal <see cref="Die"/> path
        /// (manager removal / destroy), is idempotent, and respects the post-spawn predation
        /// immunity window. Returns true only if the prey was actually eaten this call - the
        /// predator should reset its starvation clock (NotifyFed) only on a true result.
        /// </summary>
        public virtual bool Predated(string predatorName = "predator")
        {
            if (_consumedAsPrey || IsPredationImmune) return false;
            _consumedAsPrey = true;
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
