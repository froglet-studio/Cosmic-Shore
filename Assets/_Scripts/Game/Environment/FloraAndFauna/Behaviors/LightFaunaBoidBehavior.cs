using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;

// Keep BoidCollisionEffects at namespace level like in original Boid.cs
namespace CosmicShore
{
    public enum BoidCollisionEffects
    {
        Attach = 0,
        Explode = 1,
    }

    /// <summary>
    /// Implements boid-like flocking behavior with additional features like
    /// block interaction, mound building, and team-based dynamics.
    /// </summary>
    public class LightFaunaBoidBehavior : FaunaBehavior
    {
        [Header("Detection Settings")]
        [SerializeField] float cohesionRadius = 10.0f;
        [SerializeField] float separationRadius = 5.0f;
        [SerializeField] float trailBlockInteractionRadius = 10.0f;
        [SerializeField] float behaviorUpdateRate = 1.5f;

        [Header("Behavior Weights")]
        [SerializeField] float separationWeight = 1.5f;
        [SerializeField] float alignmentWeight = 1.0f;
        [SerializeField] float cohesionWeight = 1.0f;
        [SerializeField] float goalWeight = 1.0f;

        [Header("Movement")]
        [SerializeField] float minSpeed = 2.0f;
        [SerializeField] float maxSpeed = 5.0f;

        [Header("Collision Effects")]
        [SerializeField] List<BoidCollisionEffects> collisionEffects;

        [Header("References")]
        public Transform DefaultGoal;
        public Transform Mound;

        // State
        private Vector3 currentVelocity;
        private Vector3 desiredDirection;
        private Quaternion desiredRotation;
        private Vector3 target = Vector3.zero;
        private bool isRunning;
        private bool isTraveling;
        private bool isAttached;
        [SerializeField] BoxCollider blockCollider;
        private readonly List<Collider> separatedBoids = new List<Collider>();

        public override bool CanPerform(Fauna fauna)
        {
            return !isRunning;
        }

        public override IEnumerator Perform(Fauna fauna)
        {
            isRunning = true;
            LightFauna lightFauna = fauna as LightFauna;
            if (lightFauna == null) yield break;

            // Initialize
            currentVelocity = lightFauna.transform.forward * Random.Range(minSpeed, maxSpeed);
            float initialDelay = lightFauna.Phase * behaviorUpdateRate;

            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                if (fauna == null) break;

                if (!isAttached && lightFauna.Population.Goal != null)
                {
                    target = lightFauna.Population.Goal;
                }

                CalculateBehavior(lightFauna);
                ApplyMovement(lightFauna);

                yield return new WaitForSeconds(behaviorUpdateRate);
            }
        }

        private void CalculateBehavior(LightFauna lightFauna)
        {
            if (isAttached)
            {
                HandleAttachedState(lightFauna);
                return;
            }

            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            Vector3 goalDirection = target - lightFauna.transform.position;
            Vector3 blockAttraction = Vector3.zero;

            float averageSpeed = 0.0f;
            separatedBoids.Clear();

            var boidsInVicinity = Physics.OverlapSphere(lightFauna.transform.position, cohesionRadius);
            foreach (var col in boidsInVicinity)
            {
                if (col.gameObject == blockCollider.gameObject) continue;

                Vector3 diff = lightFauna.transform.position - col.transform.position;
                float dist = diff.magnitude;
                if (dist == 0) continue;

                // Handle other fauna
                var otherHealthBlock = col.GetComponentInParent<HealthBlock>();
                if (otherHealthBlock && otherHealthBlock.LifeForm != lightFauna)
                {
                    float weight = 1.0f;
                    cohesion += -diff.normalized * weight / dist;
                    alignment += col.transform.forward;

                    if (dist < separationRadius)
                    {
                        separatedBoids.Add(col);
                        separation += diff.normalized / dist;
                        averageSpeed += currentVelocity.magnitude;
                    }
                }

                // Handle trail blocks
                var block = col.GetComponent<TrailBlock>();
                if (block && block.Team != lightFauna.Team)
                {
                    blockAttraction += -diff.normalized / dist;

                    if (dist < trailBlockInteractionRadius)
                    {
                        HandleBlockCollision(lightFauna, block);
                    }
                }
            }

            // Calculate final direction
            int totalBoids = boidsInVicinity.Length - 1;
            if (totalBoids > 0)
            {
                cohesion /= totalBoids;
                cohesion = (cohesion - lightFauna.transform.position).normalized;
            }

            averageSpeed = separatedBoids.Count > 0 ? 
                averageSpeed / separatedBoids.Count : 
                currentVelocity.magnitude;

            desiredDirection = (
                (separation * separationWeight) +
                (alignment * alignmentWeight) +
                (cohesion * cohesionWeight) +
                (goalDirection * goalWeight) +
                blockAttraction
            ).normalized;

            currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);
            desiredRotation = currentVelocity != Vector3.zero ? 
                Quaternion.LookRotation(currentVelocity.normalized) : 
                lightFauna.transform.rotation;
        }

        private void HandleAttachedState(LightFauna lightFauna)
        {
            desiredDirection = (target - lightFauna.transform.position).normalized;
            currentVelocity = desiredDirection * Mathf.Clamp(currentVelocity.magnitude, minSpeed, maxSpeed);
            desiredRotation = currentVelocity != Vector3.zero ? 
                Quaternion.LookRotation(currentVelocity.normalized) : 
                lightFauna.transform.rotation;
        }

        private void HandleBlockCollision(LightFauna lightFauna, TrailBlock block)
        {
            foreach (var effect in collisionEffects)
            {
                switch (effect)
                {
                    case BoidCollisionEffects.Attach:
                        if (!isTraveling && !block.IsSmallest)
                        {
                            isAttached = true;
                            target = block.transform.position;
                            block.Grow(-1);
                            lightFauna.HealthBlock.Grow(1);
                            if (lightFauna.HealthBlock.IsLargest)
                            {
                                StartCoroutine(AddToMoundCoroutine(lightFauna));
                            }
                        }
                        else if (block.IsSmallest)
                        {
                            target = DefaultGoal.position;
                        }
                        break;

                    case BoidCollisionEffects.Explode:
                        Vector3 impactVelocity = currentVelocity * lightFauna.HealthBlock.Volume;
                        if (!float.IsInfinity(impactVelocity.x) && !float.IsNegativeInfinity(impactVelocity.x))
                        {
                            block.Damage(
                                impactVelocity,
                                lightFauna.Team,
                                lightFauna.HealthBlock.PlayerName + " boid",
                                true
                            );
                        }
                        break;
                }
            }
        }

        private IEnumerator AddToMoundCoroutine(LightFauna lightFauna)
        {
            isAttached = false;
            isTraveling = true;

            target = Mound.position;
            float scanRadius = 30f;

            Collider[] colliders = new Collider[0];
            while (colliders.Length == 0)
            {
                int layerIndex = LayerMask.NameToLayer("Mound");
                int layerMask = 1 << layerIndex;
                colliders = Physics.OverlapSphere(lightFauna.transform.position, scanRadius, layerMask);

                GyroidAssembler nakedEdge = null;
                foreach (var collider in colliders)
                {
                    nakedEdge = collider.GetComponent<GyroidAssembler>();
                    if (nakedEdge && !nakedEdge.IsFullyBonded() && 
                        nakedEdge.preferedBlocks.Count == 0 && 
                        (nakedEdge.IsBonded() || nakedEdge.isSeed))
                    {
                        (var newBlock1, var gyroidBlock1) = NewBlock(lightFauna);
                        nakedEdge.preferedBlocks.Enqueue(gyroidBlock1);
                        gyroidBlock1.TrailBlock = newBlock1;

                        nakedEdge.Depth = 1;
                        nakedEdge.StartBonding();
                        break;
                    }
                }
                if (!nakedEdge) colliders = new Collider[0];
                yield return null;
            }

            isTraveling = false;
            lightFauna.HealthBlock.IsLargest = false;
            lightFauna.HealthBlock.DeactivateShields();
            lightFauna.HealthBlock.Grow(-3);
        }

        private (TrailBlock, GyroidAssembler) NewBlock(LightFauna lightFauna)
        {
            var newBlock = Object.Instantiate(
                lightFauna.HealthBlock, 
                lightFauna.transform.position,
                lightFauna.transform.rotation,
                lightFauna.Population.transform
            );

            newBlock.ChangeTeam(lightFauna.Team);
            newBlock.gameObject.layer = LayerMask.NameToLayer("Mound");
            newBlock.TrailBlockProperties = new() { trailBlock = newBlock };
            var gyroidBlock = newBlock.gameObject.AddComponent<GyroidAssembler>();
            return (newBlock, gyroidBlock);
        }

        private void ApplyMovement(LightFauna lightFauna)
        {
            lightFauna.transform.position += currentVelocity * Time.deltaTime;
            lightFauna.transform.rotation = Quaternion.Lerp(
                lightFauna.transform.rotation,
                desiredRotation,
                Time.deltaTime
            );
        }

        public override void OnBehaviorEnd(Fauna fauna)
        {
            isRunning = false;
        }
    }
}
