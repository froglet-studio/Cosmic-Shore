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
        [Tooltip("Multiplier applied to goalUpdateInterval per CellAggressionLevel: [Level0, Level1, Level2]. " +
                 "Lower values = faster relocation to targeted region under stress.")]
        [SerializeField] float[] goalUpdateIntervalByAggression = { 1f, 0.55f, 0.25f };
        [Tooltip("Each fauna picks a stable random offset on a sphere of this radius and adds it " +
                 "to its resolved goal. Prevents the whole pack from converging onto a single point " +
                 "(e.g. the crystal at origin), which otherwise creates a visible depletion zone " +
                 "where fauna repeatedly consume spawn-on-impact prism configurations.")]
        [SerializeField] float goalOrbitRadius = 60f;
        public Vector3 Goal;

        // Stable per-instance offset so each fauna orbits its resolved goal at a different
        // point. Seeded once at Start from the fauna's domain + instance id so the spread
        // is deterministic per spawn but varied across the pack.
        Vector3 _goalOrbitOffset;

        // --- ILifeFormEntity ---
        public Domains Domain => domain;
        public GameObject GetGameObject() => gameObject;

        protected Cell cell => cellData ? cellData.Cell : null;

        protected virtual void Start()
        {
            if (domain == Domains.Unassigned)
                CSDebug.LogWarning($"{name}: Population domain is Unassigned. Assign it before spawning, or set it on the prefab.");

            _goalOrbitOffset = Random.onUnitSphere * Mathf.Max(0f, goalOrbitRadius);

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
        /// A per-instance orbit offset is added so the pack spreads around the target
        /// instead of piling onto a single point. Level 2 skips the offset — at berserk
        /// aggression we want tight convergence onto the densest cleanup target.
        /// </summary>
        protected virtual Vector3 ResolveGoal()
        {
            if (cell == null) return Goal;

            switch (cell.AggressionLevel)
            {
                case CellAggressionLevel.Level2:
                    return cell.GetPrimaryCentroid();

                case CellAggressionLevel.Level1:
                    // GetExplosionTarget(domain) finds the densest region of blocks
                    // that are NOT of this domain - the nearest opposing centroid.
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
