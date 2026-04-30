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

                yield return new WaitForSeconds(Mathf.Max(0f, data.behaviorUpdateRate + Phase));
                UpdateBehavior();
            }
        }

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
            float consumeRadius = Mathf.Max(0f, data.consumeRadius);

            // Aggression 2 drops friendly avoidance (other same-domain fauna and any
            // same-domain HealthPrisms stop contributing to separation). Cross-domain
            // entities still push us away so we don't clip through enemy mass.
            bool dropFriendlyAvoidance = phase >= CellPhase.Rabid;

            var nearbyColliders = Physics.OverlapSphere(transform.position, detectionRadius);

            foreach (var collider in nearbyColliders)
            {
                if (!collider || collider.gameObject == gameObject) continue;

                Vector3 diff = transform.position - collider.transform.position;
                float distance = diff.magnitude;
                if (distance <= 0f) continue;

                // Handle Ships
                if (collider.TryGetComponent(out IVesselStatus _))
                {
                    neighborCount++;
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

            float minSpeed = Mathf.Max(0f, data.minSpeed);
            float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed);

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
