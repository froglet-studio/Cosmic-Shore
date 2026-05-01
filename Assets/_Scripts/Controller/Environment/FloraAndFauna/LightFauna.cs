using UnityEngine;
using System.Collections;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lightweight boid-like creature with separation, cohesion, and goal-seeking behaviors.
    /// Consumes enemy health prisms within range.
    /// </summary>
    public class LightFauna : Fauna
    {
        const string PLAYER_NAME = "light FaunaPrefab";

        [Header("Data")]
        [SerializeField] private LightFaunaDataSO data;

        private Vector3 currentVelocity;
        private Vector3 desiredDirection;
        private Quaternion desiredRotation;

        [HideInInspector] public float Phase;

        public LightFaunaManager LightFaunaManager { get; set; }

        /// <summary>
        /// True when the host cell's phase is <see cref="CellPhase.Rabid"/>: aggression-2
        /// fauna ignore danger-prism damage. Read by impactor pipelines that would
        /// otherwise debuff/damage the fauna on dangerous-prism contact. Centralizing
        /// the rule here keeps the impact code path from re-deriving phase semantics.
        /// </summary>
        public bool IsDangerImmune => cell && cell.Phase >= CellPhase.Rabid;

        public override void Initialize(Cell cell)
        {
            if (!data)
            {
                CSDebug.LogError($"{nameof(LightFauna)} on {name} is missing {nameof(LightFaunaDataSO)}.");
                return;
            }

            // The fauna body is composed of nested HealthPrism prefab instances
            // (e.g. MassBrittlestarFauna embeds DynamicHealthBlock children). They
            // start at local scale 0 and only grow to their authored target scale
            // when Prism.Initialize fires the scale animator. LifeForm does this
            // automatically via BindEmbeddedParts, but LightFauna does NOT extend
            // LifeForm — without this step the brittlestar / shark renders as a
            // cluster of invisible prisms. Recolor to the fauna's domain first,
            // then kick off the growth animation. LifeForm is intentionally not
            // assigned so these body prisms don't register with Cell as
            // consumable targets.
            var bodyPrisms = GetComponentsInChildren<HealthPrism>(true);
            for (int i = 0; i < bodyPrisms.Length; i++)
            {
                var hp = bodyPrisms[i];
                if (!hp) continue;
                hp.ChangeTeam(domain);
                hp.Initialize("FaunaPrefab");
            }

            float minSpeed = Mathf.Max(0f, data.minSpeed);
            float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed);

            currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
            StartCoroutine(UpdateBehaviorCoroutine());
        }

        protected override void Die(string killerName = "")
        {
            if (LightFaunaManager)
                LightFaunaManager.RemoveFauna(this);
            else
                Destroy(gameObject);
        }

        IEnumerator UpdateBehaviorCoroutine()
        {
            while (true)
            {
                if (!data)
                    yield break;

                float cadence = Mathf.Max(0.05f, data.behaviorUpdateRate + Phase) * GetAggressionCadenceMultiplier();
                yield return new WaitForSeconds(cadence);
                UpdateBehavior();
            }
        }

        // Cleanup urgency multipliers indexed by CellAggressionLevel (3 levels).
        // Level0 = baseline (world feels alive), Level1 = tighter, Level2 = berserk.
        static readonly float[] CadenceByAggression       = { 1f,   0.55f, 0.25f };
        static readonly float[] ConsumeRadiusByAggression = { 1f,   1.4f,  1.8f  };
        static readonly float[] SpeedByAggression         = { 1f,   1.25f, 1.6f  };

        float GetAggressionCadenceMultiplier()
        {
            if (cell == null) return 1f;
            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, CadenceByAggression.Length - 1);
            return CadenceByAggression[idx];
        }

        float GetAggressionConsumeRadiusMultiplier()
        {
            if (cell == null) return 1f;
            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, ConsumeRadiusByAggression.Length - 1);
            return ConsumeRadiusByAggression[idx];
        }

        float GetAggressionSpeedMultiplier()
        {
            if (cell == null) return 1f;
            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, SpeedByAggression.Length - 1);
            return SpeedByAggression[idx];
        }

        bool IsBerserk => cell != null && cell.AggressionLevel == CellAggressionLevel.Level2;

        void UpdateBehavior()
        {
            if (!data)
                return;

            Vector3 separation = Vector3.zero;

            // Phase-driven goal. Each phase swaps the goal source rather than killing/spawning
            // systems, so the same fauna instance can transition through aggression levels
            // as the cell's phase changes around it.
            //   Quiet/Settled: aggression 0 — head toward crystal
            //   Restless/Frozen: aggression 1 — head toward nearest opposing-color centroid
            //   Rabid: aggression 2 — head toward nearest centroid (any domain)
            var phase = cell ? cell.Phase : CellPhase.Sprout;
            Goal = phase switch
            {
                CellPhase.Restless => cell.GetExplosionTarget(domain),
                CellPhase.Frozen => cell.GetExplosionTarget(domain),
                CellPhase.Rabid => cell.GetDensestRegionAnyDomain(),
                _ => (cellData && cellData.CrystalTransform)
                       ? cellData.CrystalTransform.position
                       : (cell ? cell.transform.position : transform.position),
            };

            if (!IsFinite(Goal) || Goal.sqrMagnitude < 0.001f)
            {
                Goal = cellData && cellData.CrystalTransform ? cellData.CrystalTransform.position : cell.transform.position;
            }

            Vector3 goalDirection = (Goal - transform.position).normalized;

            int neighborCount = 0;
            float averageSpeed = 0f;

            float detectionRadius = Mathf.Max(0f, data.detectionRadius);
            float separationRadius = Mathf.Max(0f, data.separationRadius);
            float consumeRadius = Mathf.Max(0f, data.consumeRadius) * GetAggressionConsumeRadiusMultiplier();

            // Aggression 2 drops friendly avoidance (same-domain ships, fauna, and
            // health prisms stop contributing to separation). Cross-domain entities
            // still push us away so we don't clip through enemy mass.
            bool dropFriendlyAvoidance = phase >= CellPhase.Rabid;

            var nearbyColliders = Physics.OverlapSphere(transform.position, detectionRadius);

            foreach (var collider in nearbyColliders)
            {
                if (!collider || collider.gameObject == gameObject) continue;

                Vector3 diff = transform.position - collider.transform.position;
                float distance = diff.magnitude;
                if (distance <= 0f) continue;

                // Handle Ships
                if (collider.TryGetComponent(out IVesselStatus vessel))
                {
                    neighborCount++;
                    // Level 2: skip separation from same-domain ships.
                    if (!(dropFriendlyAvoidance && vessel.Domain == domain))
                        separation -= diff.normalized / distance;
                    continue;
                }

                // Handle other fauna/health prisms
                var otherHealthBlock = collider.GetComponent<HealthPrism>();
                if (otherHealthBlock)
                {
                    if (otherHealthBlock.LifeForm == this) continue;

                    neighborCount++;

                    bool sameDomain = otherHealthBlock.LifeForm && otherHealthBlock.LifeForm.domain == domain;

                    if (distance < separationRadius && !(dropFriendlyAvoidance && sameDomain))
                        separation += diff.normalized / distance;

                    if (distance < consumeRadius && otherHealthBlock.LifeForm && otherHealthBlock.LifeForm.domain != domain)
                        otherHealthBlock.Consume(transform, domain, PLAYER_NAME, true);

                    continue;
                }

                // Handle blocks
                Prism block = collider.GetComponent<Prism>();
                if (block && block.Domain != domain && distance < consumeRadius)
                    block.Consume(transform, domain, PLAYER_NAME, true);
            }

            averageSpeed = neighborCount > 0
                ? (averageSpeed > 0 ? averageSpeed / neighborCount : currentVelocity.magnitude)
                : currentVelocity.magnitude;

            float separationWeight = Mathf.Max(0f, data.separationWeight);
            float goalWeight = Mathf.Max(0f, data.goalWeight);

            desiredDirection = ((separation * separationWeight) + (goalDirection * goalWeight)).normalized;

            float speedMult = GetAggressionSpeedMultiplier();
            float minSpeed = Mathf.Max(0f, data.minSpeed) * speedMult;
            float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed * speedMult);

            currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);

            if (currentVelocity != Vector3.zero && SafeLookRotation.TryGet(currentVelocity, out var rotation, this))
                desiredRotation = rotation;
            else
                desiredRotation = transform.rotation;
        }

        void Update()
        {
            transform.position += currentVelocity * Time.deltaTime;

            float lerpSpeed = data ? Mathf.Max(0f, data.rotationLerpSpeed) : 5f;
            var t = Mathf.Clamp(Time.deltaTime * lerpSpeed, 0f, 0.99f);

            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, t);
        }

        static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
