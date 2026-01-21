using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;

public enum BoidCollisionEffects
{
    Attach = 0,
    Explode = 1,
}

public class Boid : Fauna
{
    [Header("Detection Settings")]
    [SerializeField] float cohesionRadius = 10.0f;
    [SerializeField] float behaviorUpdateRate = 1.5f;
    [SerializeField] float separationRadius = 5f;
    [SerializeField] float trailBlockInteractionRadius = 10f;

    [Header("Behavior Weights")]
    [SerializeField] float separationWeight = 1.5f;
    [SerializeField] float alignmentWeight = 1.0f;
    [SerializeField] float cohesionWeight = 1.0f;
    [SerializeField] float goalWeight = 1.0f;

    [Header("Speed Settings")]
    [SerializeField] float minSpeed = 2.0f;
    [SerializeField] float maxSpeed = 5.0f;

    [Header("Goal Settings")]
    public Transform DefaultGoal;
    public Vector3 target = Vector3.zero;

    public float normalizedIndex;

    [Header("Mound Settings")]
    public Transform Mound;

    Vector3 currentVelocity;
    Vector3 desiredDirection;
    Quaternion desiredRotation;

    public bool isKilled = false;
    bool isTraveling = false;
    bool isAttached = false;

    [SerializeField] List<BoidCollisionEffects> collisionEffects;

    BoxCollider blockCollider;

    List<Collider> separatedBoids = new List<Collider>();
    HealthPrism embeddedHealthPrism;

    public override void Initialize(Cell cell)
    {
        base.Initialize(cell);

        embeddedHealthPrism = GetComponentInChildren<HealthPrism>(true);
        if (!embeddedHealthPrism)
        {
            Debug.LogError($"{nameof(Boid)} on {name} has no embedded HealthPrism in children. Scaling cannot work.");
            return;
        }

        blockCollider = embeddedHealthPrism.GetComponent<BoxCollider>();
        if (!blockCollider)
            Debug.LogWarning($"{nameof(Boid)} on {name}: embedded HealthPrism has no BoxCollider.");

        embeddedHealthPrism.ChangeTeam(domain);

        currentVelocity = transform.forward * Random.Range(minSpeed, Mathf.Max(minSpeed, maxSpeed));
        float initialDelay = normalizedIndex * behaviorUpdateRate;
        StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
    }

    IEnumerator CalculateBehaviorCoroutine(float initialDelay)
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (!isAttached)
            {
                target = Population ? Population.Goal : target;
            }

            CalculateBehavior();
            yield return new WaitForSeconds(behaviorUpdateRate);
        }
    }

    void CalculateBehavior()
    {
        if (isAttached)
        {
            desiredDirection = (target - transform.position).normalized;
            currentVelocity = desiredDirection * Mathf.Clamp(currentVelocity.magnitude, minSpeed, maxSpeed);

            if (SafeLookRotation.TryGet(currentVelocity, out var rotation, this))
                desiredRotation = rotation;
            else
                desiredRotation = transform.rotation;

            return;
        }

        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 goalDirection = target - transform.position;
        Vector3 blockAttraction = Vector3.zero;

        float averageSpeed = 0.0f;
        separatedBoids.Clear();

        var boidsInVicinity = Physics.OverlapSphere(transform.position, cohesionRadius);
        int colliderCount = boidsInVicinity.Length;

        for (int i = 0; i < colliderCount; i++)
        {
            Collider collider = boidsInVicinity[i];
            if (!collider) continue;

            // Ignore our own collider (if present)
            if (blockCollider && collider.gameObject == blockCollider.gameObject) continue;

            Boid otherBoid = collider.GetComponentInParent<Boid>();
            Prism otherPrism = collider.GetComponent<Prism>();

            Vector3 diff = transform.position - collider.transform.position;
            float distance = diff.magnitude;
            if (distance == 0) continue;

            if (otherBoid)
            {
                cohesion += -diff.normalized / distance;
                alignment += collider.transform.forward;

                if (distance < separationRadius)
                {
                    separatedBoids.Add(collider);
                    separation += diff.normalized / distance;
                    averageSpeed += currentVelocity.magnitude;
                }
            }
            else if (otherPrism)
            {
                blockAttraction += -diff.normalized / distance;

                if (distance < trailBlockInteractionRadius && embeddedHealthPrism && otherPrism.Domain != embeddedHealthPrism.Domain)
                {
                    foreach (var effect in collisionEffects)
                    {
                        switch (effect)
                        {
                            case BoidCollisionEffects.Attach:
                                if (!isTraveling)
                                {
                                    if (!otherPrism.IsSmallest)
                                    {
                                        isAttached = true;
                                        target = otherPrism.transform.position;
                                        otherPrism.Grow(-1);
                                        embeddedHealthPrism.Grow(1);
                                        if (embeddedHealthPrism.IsLargest) StartCoroutine(AddToMoundCoroutine());
                                    }
                                    else if (DefaultGoal) target = DefaultGoal.position;
                                }
                                break;

                            case BoidCollisionEffects.Explode:
                                if (embeddedHealthPrism)
                                    otherPrism.Damage(currentVelocity * embeddedHealthPrism.Volume, embeddedHealthPrism.Domain,
                                        embeddedHealthPrism.PlayerName + " boid", true);
                                break;
                        }
                    }
                }
            }
        }

        int totalBoids = boidsInVicinity.Length - 1;

        if (totalBoids > 0)
        {
            cohesion /= totalBoids;
            cohesion = (cohesion - transform.position).normalized;
        }

        averageSpeed = separatedBoids.Count > 0 ? averageSpeed / separatedBoids.Count : currentVelocity.magnitude;

        desiredDirection = ((separation * separationWeight)
                           + (alignment * alignmentWeight)
                           + (cohesion * cohesionWeight)
                           + (goalDirection * goalWeight)
                           + blockAttraction).normalized;

        currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);

        desiredRotation = SafeLookRotation.TryGet(currentVelocity, out var desiredRot, this) ? desiredRot : transform.rotation;
    }

    protected override void Spawn() { }

    IEnumerator AddToMoundCoroutine()
    {
        isAttached = false;
        isTraveling = true;

        if (Mound) target = Mound.position;

        float scanRadius = 30f;

        Collider[] colliders = new Collider[0];
        while (colliders.Length == 0)
        {
            int layerIndex = LayerMask.NameToLayer("Mound");
            int layerMask = 1 << layerIndex;
            colliders = Physics.OverlapSphere(transform.position, scanRadius, layerMask);

            GyroidAssembler nakedEdge = null;
            foreach (var collider in colliders)
            {
                nakedEdge = collider.GetComponent<GyroidAssembler>();
                if (nakedEdge && !nakedEdge.IsFullyBonded() && nakedEdge.preferedBlocks.Count == 0 && (nakedEdge.IsBonded() || nakedEdge.isSeed))
                {
                    (var newBlock1, var gyroidBlock1) = NewBlock();
                    nakedEdge.preferedBlocks.Enqueue(gyroidBlock1);
                    gyroidBlock1.Prism = newBlock1;

                    nakedEdge.Depth = 1;
                    nakedEdge.StartBonding();
                    break;
                }
            }

            if (!nakedEdge) colliders = new Collider[0];
            yield return null;
        }

        isTraveling = false;

        if (!embeddedHealthPrism) yield break;
        embeddedHealthPrism.IsLargest = false;
        embeddedHealthPrism.DeactivateShields();
        embeddedHealthPrism.Grow(-3);
    }

    private (Prism, GyroidAssembler) NewBlock()
    {
        var newBlock = Instantiate(healthPrism, transform.position, transform.rotation, Population.transform);
        newBlock.ChangeTeam(domain);
        newBlock.gameObject.layer = LayerMask.NameToLayer("Mound");
        newBlock.prismProperties = new() { prism = newBlock };
        var gyroidBlock = newBlock.gameObject.AddComponent<GyroidAssembler>();
        return (newBlock, gyroidBlock);
    }

    void Update()
    {
        transform.position += currentVelocity * Time.deltaTime;
        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime);
    }
}
