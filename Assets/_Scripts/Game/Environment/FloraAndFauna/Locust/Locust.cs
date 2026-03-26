using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game.Fauna
{
    public class Locust : CosmicShore.Fauna
    {
        [Header("Locust Config")]
        [SerializeField] LocustConfigSO config;

        LocustSwarmManager swarmManager;
        HealthPrism embeddedHealthPrism;
        BoxCollider blockCollider;

        // Flocking state
        Vector3 currentVelocity;
        Quaternion desiredRotation;

        // Targeting state
        Prism targetPrism;
        bool isAttached;
        bool isConsuming;
        float lingerTimer;
        bool isLingering;

        // Swarm tracking — set by manager for staggered updates
        public float NormalizedIndex { get; set; }

        // Attachment slot tracking (static registry so locusts coordinate)
        static readonly Dictionary<Prism, int> attachmentCounts = new();

        public override void Initialize(Cell cell)
        {
            embeddedHealthPrism = GetComponentInChildren<HealthPrism>(true);
            if (!embeddedHealthPrism)
            {
                CSDebug.LogError($"{nameof(Locust)} on {name} has no embedded HealthPrism in children.");
                return;
            }

            blockCollider = embeddedHealthPrism.GetComponent<BoxCollider>();
            embeddedHealthPrism.ChangeTeam(domain);

            currentVelocity = transform.forward * Random.Range(config.MinSpeed, config.MaxSpeed);
            desiredRotation = transform.rotation;

            float initialDelay = NormalizedIndex * config.BehaviorUpdateInterval;
            StartCoroutine(BehaviorLoop(initialDelay));
        }

        public void SetSwarmManager(LocustSwarmManager manager) => swarmManager = manager;
        public void SetConfig(LocustConfigSO cfg) => config = cfg;

        #region Core Loop

        IEnumerator BehaviorLoop(float initialDelay)
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                if (!isAttached)
                    SeekTarget();

                if (isAttached && targetPrism && !targetPrism.destroyed)
                    ConsumeTick();

                CalculateFlocking();
                yield return new WaitForSeconds(config.BehaviorUpdateInterval);
            }
        }

        void Update()
        {
            if (isAttached && targetPrism && !targetPrism.destroyed)
            {
                // Stick to the target prism position
                Vector3 toTarget = targetPrism.transform.position - transform.position;
                if (toTarget.sqrMagnitude > 0.01f)
                    transform.position = Vector3.Lerp(transform.position, targetPrism.transform.position, Time.deltaTime * config.SteeringSmoothing * 2f);
            }
            else
            {
                transform.position += currentVelocity * Time.deltaTime;
            }

            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime * config.SteeringSmoothing);
        }

        #endregion

        #region Targeting

        void SeekTarget()
        {
            // If we had a target that got destroyed, move to next in same trail
            if (targetPrism && targetPrism.destroyed)
            {
                Detach();
                Prism next = FindNextInTrail(targetPrism);
                if (next)
                {
                    TryAttach(next);
                    return;
                }
            }

            if (isAttached) return;

            // Scan for open-ended trail prisms
            Prism closest = FindClosestOpenEndPrism();
            if (closest)
                TryAttach(closest);
        }

        Prism FindClosestOpenEndPrism()
        {
            float detectionRadiusSq = config.PrismDetectionRadius * config.PrismDetectionRadius;
            Prism best = null;
            float bestDistSq = float.MaxValue;

            var colliders = Physics.OverlapSphere(transform.position, config.PrismDetectionRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (!col) continue;

                // Skip our own collider
                if (blockCollider && col.gameObject == blockCollider.gameObject) continue;

                var prism = col.GetComponent<Prism>();
                if (!prism || prism.destroyed) continue;

                // Skip prisms on our own domain
                if (embeddedHealthPrism && prism.Domain == embeddedHealthPrism.Domain) continue;

                // Skip shielded and dangerous prisms for now
                if (prism.prismProperties.IsShielded || prism.prismProperties.IsSuperShielded) continue;
                if (prism.prismProperties.IsDangerous) continue;

                // Must be an open end of a trail
                if (!IsOpenEnd(prism)) continue;

                // Check attachment capacity
                if (!HasAttachmentCapacity(prism)) continue;

                float distSq = (prism.transform.position - transform.position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = prism;
                }
            }

            return best;
        }

        /// <summary>
        /// A prism is an "open end" if it's missing at least one neighbor in its trail.
        /// That means it's at index 0, at the last index, or an adjacent prism has been destroyed.
        /// </summary>
        bool IsOpenEnd(Prism prism)
        {
            if (prism.Trail == null) return true; // standalone prism, fair game

            var trail = prism.Trail;
            int index = trail.GetBlockIndex(prism);
            if (index < 0) return true; // not in trail anymore

            int count = trail.TrailList.Count;
            if (count <= 1) return true;

            // First or last in trail = open end
            if (index == 0 || index == count - 1) return true;

            // Check if either neighbor is destroyed
            var prev = trail.TrailList[index - 1];
            var next = trail.TrailList[index + 1];

            if (!prev || prev.destroyed) return true;
            if (!next || next.destroyed) return true;

            return false;
        }

        bool HasAttachmentCapacity(Prism prism)
        {
            int current = GetAttachmentCount(prism);
            int maxAllowed = Mathf.Max(
                config.MinLocustsPerPrism,
                Mathf.FloorToInt(prism.Volume * config.MaxLocustsPerVolumeUnit)
            );
            return current < maxAllowed;
        }

        void TryAttach(Prism prism)
        {
            if (!prism || prism.destroyed) return;
            if (!HasAttachmentCapacity(prism)) return;

            targetPrism = prism;
            isAttached = true;
            isConsuming = false;
            isLingering = false;
            lingerTimer = 0f;
            IncrementAttachment(prism);
        }

        void Detach()
        {
            if (targetPrism)
                DecrementAttachment(targetPrism);

            isAttached = false;
            isConsuming = false;
            isLingering = false;
            lingerTimer = 0f;
            targetPrism = null;
        }

        /// <summary>
        /// When a consumed prism is destroyed, find the next open-end prism along the same trail.
        /// </summary>
        Prism FindNextInTrail(Prism consumed)
        {
            if (consumed.Trail == null) return null;

            var trail = consumed.Trail;
            int index = trail.GetBlockIndex(consumed);
            if (index < 0) return null;

            // Look in both directions for the nearest open-end, undestroyed prism
            Prism candidate = null;
            float bestDist = float.MaxValue;

            // Search forward
            for (int i = index + 1; i < trail.TrailList.Count; i++)
            {
                var p = trail.TrailList[i];
                if (!p || p.destroyed) continue;
                if (IsOpenEnd(p) && HasAttachmentCapacity(p))
                {
                    float dist = (p.transform.position - transform.position).sqrMagnitude;
                    if (dist < bestDist) { bestDist = dist; candidate = p; }
                }
                break; // Only check the first undestroyed neighbor
            }

            // Search backward
            for (int i = index - 1; i >= 0; i--)
            {
                var p = trail.TrailList[i];
                if (!p || p.destroyed) continue;
                if (IsOpenEnd(p) && HasAttachmentCapacity(p))
                {
                    float dist = (p.transform.position - transform.position).sqrMagnitude;
                    if (dist < bestDist) { bestDist = dist; candidate = p; }
                }
                break; // Only check the first undestroyed neighbor
            }

            return candidate;
        }

        #endregion

        #region Consumption

        void ConsumeTick()
        {
            if (!targetPrism || targetPrism.destroyed)
            {
                Detach();
                return;
            }

            float aggression = swarmManager ? swarmManager.CurrentAggression : 0.5f;
            float curveMultiplier = config.AggressionConsumptionCurve.Evaluate(aggression);
            float consumptionRate = config.BaseConsumptionRate * curveMultiplier;

            if (targetPrism.IsSmallest)
            {
                // Prism is at minimum size — linger then destroy
                if (!isLingering)
                {
                    isLingering = true;
                    // Linger time is a function of consumption rate and remaining "virtual" volume
                    // Faster consumption = shorter linger
                    lingerTimer = config.MinSizeLingerSeconds / Mathf.Max(consumptionRate, 0.01f);
                }

                lingerTimer -= config.BehaviorUpdateInterval;
                if (lingerTimer <= 0f)
                {
                    DestroyTargetPrism(aggression);
                }
                return;
            }

            // Shrink the prism
            float shrinkAmount = consumptionRate * config.BehaviorUpdateInterval;
            targetPrism.Grow(-shrinkAmount);

            isConsuming = true;
        }

        void DestroyTargetPrism(float aggression)
        {
            if (!targetPrism || targetPrism.destroyed) return;

            Prism consumed = targetPrism;
            Detach();

            // Consume the prism (implode toward the locust)
            consumed.Consume(transform, embeddedHealthPrism ? embeddedHealthPrism.Domain : Domains.None,
                embeddedHealthPrism ? embeddedHealthPrism.PlayerName + " locust" : "locust", true);

            // Try to move to next prism in the same trail
            Prism next = FindNextInTrail(consumed);
            if (next)
                TryAttach(next);
        }

        #endregion

        #region Flocking

        void CalculateFlocking()
        {
            if (isAttached) return;

            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            Vector3 toTarget = Vector3.zero;
            int neighborCount = 0;

            // Use the swarm manager's locust list for flocking instead of physics overlap
            if (swarmManager)
            {
                var swarm = swarmManager.ActiveLocusts;
                for (int i = 0; i < swarm.Count; i++)
                {
                    var other = swarm[i];
                    if (!other || other == this) continue;

                    Vector3 diff = transform.position - other.transform.position;
                    float dist = diff.magnitude;
                    if (dist <= 0f || dist > config.CohesionRadius) continue;

                    neighborCount++;
                    cohesion += -diff.normalized / dist;
                    alignment += other.transform.forward;

                    if (dist < config.SeparationRadius)
                        separation += diff.normalized / dist;
                }
            }

            if (targetPrism && !targetPrism.destroyed)
                toTarget = (targetPrism.transform.position - transform.position).normalized;
            else if (swarmManager)
                toTarget = (Goal - transform.position).normalized;

            Vector3 desired = (separation * config.SeparationWeight
                             + alignment * config.AlignmentWeight
                             + cohesion * config.CohesionWeight
                             + toTarget * config.TargetWeight).normalized;

            if (desired.sqrMagnitude < 0.001f)
                desired = transform.forward;

            float speed = Mathf.Clamp(currentVelocity.magnitude, config.MinSpeed, config.MaxSpeed);
            currentVelocity = desired * speed;

            if (SafeLookRotation.TryGet(currentVelocity, out var rot, this))
                desiredRotation = rot;
        }

        #endregion

        #region Attachment Registry

        static int GetAttachmentCount(Prism prism)
        {
            attachmentCounts.TryGetValue(prism, out int count);
            return count;
        }

        static void IncrementAttachment(Prism prism)
        {
            attachmentCounts.TryGetValue(prism, out int count);
            attachmentCounts[prism] = count + 1;
        }

        static void DecrementAttachment(Prism prism)
        {
            if (!attachmentCounts.TryGetValue(prism, out int count)) return;
            count--;
            if (count <= 0) attachmentCounts.Remove(prism);
            else attachmentCounts[prism] = count;
        }

        /// <summary>
        /// Clear stale entries. Called periodically by the swarm manager.
        /// </summary>
        public static void PurgeStaleAttachments()
        {
            var stale = new List<Prism>();
            foreach (var kvp in attachmentCounts)
            {
                if (!kvp.Key || kvp.Key.destroyed)
                    stale.Add(kvp.Key);
            }
            for (int i = 0; i < stale.Count; i++)
                attachmentCounts.Remove(stale[i]);
        }

        #endregion

        #region Lifecycle

        protected override void Spawn() { }

        protected override void Die(string killerName = "")
        {
            Detach();
            if (swarmManager)
                swarmManager.OnLocustDied(this);
            Destroy(gameObject);
        }

        void OnDisable()
        {
            Detach();
        }

        #endregion
    }
}
