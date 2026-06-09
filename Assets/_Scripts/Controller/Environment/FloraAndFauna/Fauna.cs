using System.Collections;
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

        /// <summary>Reset the starvation clock — a subclass calls this whenever it consumes prey.</summary>
        protected void NotifyFed() => _lastFedTime = Time.time;

        // --- ILifeFormEntity ---
        public Domains Domain => domain;
        public GameObject GetGameObject() => gameObject;

        // `cellData ? cellData.Cell : null` (not `cellData.Cell`) so callers don't
        // NRE when cellData was never wired on the prefab — they just get null
        // and skip the goal/avoidance branches that need it.
        protected Cell cell => cellData ? cellData.Cell : null;

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
        /// Initialize this fauna with its parent cell. Override in subclasses that need
        /// setup beyond the default. Default implementation is intentionally empty -
        /// this satisfies LSP so managers and stubs don't need to throw NotImplementedException.
        /// </summary>
        public virtual void Initialize(Cell cell) { }

        /// <summary>
        /// Handle this fauna's death. Default is empty - override in subclasses
        /// that have meaningful death behavior.
        /// </summary>
        protected virtual void Die(string killerName = "") { }

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
