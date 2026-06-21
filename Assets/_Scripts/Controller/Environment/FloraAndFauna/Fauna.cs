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
        [Tooltip("What this fauna eats — the predator/herbivore selector. Herbivore: " +
                 "opposing-domain prism MASS (flora canopy + vessel trails); the default, " +
                 "original behavior. Predator: herbivore FAUNA (ignores prism mass for " +
                 "feeding). Both starve via the clock below, so a Predator layered on " +
                 "Herbivores yields a two-tier Lotka-Volterra food web. Docs/ECOSYSTEM.md §7/§10.")]
        [SerializeField] protected FaunaDiet diet = FaunaDiet.Herbivore;

        /// <summary>What this fauna eats — the predator/herbivore selector. See <see cref="FaunaDiet"/>.</summary>
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
                 "Feeding (consuming any prism, or — for predators — eating a herbivore) resets " +
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
        /// Reset the starvation clock — a subclass calls this whenever it consumes prey.
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
        //  Reproduction — the population driver (retires the fixed-period
        //  spawner as the source of population; the spawner is now only a
        //  seeder). All tuning lives on the species' FaunaConfigurationSO; a
        //  fauna with no lineage (manager-spawned, drones) never reproduces.
        // -------------------------------------------------------------------

        Cell hostCell;
        FaunaConfigurationSO sourceConfig;
        bool lineageRegistered;
        int _feedsSinceBirth;
        float _lastBirthTime = float.NegativeInfinity;

        // Offspring appear within this radius of the parent — far enough not to
        // stack, close enough to join the parent's swarm/feeding ground.
        const float OffspringSpawnJitter = 25f;

        /// <summary>The species config this fauna was spawned from (null for manager-spawned/drone fauna).</summary>
        public FaunaConfigurationSO SourceConfig => sourceConfig;

        /// <summary>
        /// Binds this fauna to its species lineage: the cell whose population it
        /// belongs to and the FaunaConfigurationSO that defines the species.
        /// Registers it in the cell's per-species live count (unregistered in
        /// OnDestroy). Called by the spawner after Initialize, and by a parent
        /// for its offspring — heredity is what lets reproduction recurse.
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
        /// creature — eliminating the per-tick Collider[] allocation that made
        /// large swarms GC-churn. NonAlloc silently truncates beyond capacity,
        /// which for steering/grazing just means considering a subset of an
        /// extremely dense neighborhood that tick.
        /// </summary>
        protected static readonly Collider[] OverlapScratch = new Collider[256];

        /// <summary>
        /// Shared scratch list for PrismSpatialIndex.QuerySphere in fauna behavior
        /// ticks — the prism half of the neighborhood scan (the physics half above
        /// now only carries non-prism populations like vessels). Same single-buffer
        /// rationale as <see cref="OverlapScratch"/>; reproduction's deferred first
        /// behavior tick (see Boid.CalculateBehaviorCoroutine) keeps a parent from
        /// having its snapshot clobbered mid-iteration.
        /// </summary>
        protected static readonly List<Prism> PrismScratch = new(256);

        /// <summary>
        /// Overlap mask for the physics half of fauna scans: everything EXCEPT prism
        /// layers (TrailBlocks + Mound). Prisms — including other fauna's body
        /// HealthPrisms — are served by PrismSpatialIndex.QuerySphere instead, so the
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
        // Fauna bodies are HealthPrisms — registered prism mass that MOVES every
        // frame. The index stores positions, so the mover must keep them honest
        // (Docs/SPATIAL_INDEX.md): otherwise batch AOE hits the creature at its
        // spawn point and index-served fauna senses look for it where it used
        // to be.

        HealthPrism[] _bodyPrisms;

        /// <summary>
        /// Caches this fauna's body HealthPrisms for the per-frame movement
        /// notification. Call from Initialize, after the body hierarchy exists.
        /// Returns the cached array so subclasses can reuse it for body setup.
        /// </summary>
        protected HealthPrism[] CacheBodyPrisms() =>
            _bodyPrisms = GetComponentsInChildren<HealthPrism>(true);

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
        /// Initialize this fauna with its parent cell. Overrides must call base —
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
        /// Death chokepoint — SEALED so no fauna can die without conserving its mass.
        /// Every death path (starvation, <see cref="Predated"/>) routes here; it drops
        /// the elemental crystal (the locked "every lifeform drops one elemental crystal
        /// on death, mass is conserved" invariant — the creature does not just vanish)
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
        /// THIS, not <see cref="Die"/> — the crystal drop is sealed into Die so the mass-
        /// conservation invariant cannot be bypassed by a subclass. Default is empty so
        /// managers and stubs don't need to throw NotImplementedException.
        /// </summary>
        protected virtual void OnDeath(string killerName = "") { }

        // Idempotency for predation: two predators can reach the same herbivore on the
        // same frame (each iterating its own OverlapSphere snapshot). Without this guard
        // the second Predated() re-enters Die() and double-removes / double-destroys.
        bool _consumedAsPrey;

        /// <summary>False once a predator has eaten this fauna — predators skip already-eaten prey.</summary>
        public bool IsAlivePrey => !_consumedAsPrey;

        /// <summary>
        /// A predator has caught this fauna. Routes through the normal <see cref="Die"/> path
        /// (manager removal / destroy), is idempotent, and respects the post-spawn predation
        /// immunity window. Returns true only if the prey was actually eaten this call — the
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
        ///   Level0 — head toward the cell's crystal
        ///   Level1 — head toward the nearest opposing-color centroid
        ///   Level2 — head toward the nearest centroid of ANY color
        ///
        /// A per-instance orbit offset is added at Levels 0 and 1 so the pack spreads
        /// around the target. Level 2 skips the offset — at berserk aggression we
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
