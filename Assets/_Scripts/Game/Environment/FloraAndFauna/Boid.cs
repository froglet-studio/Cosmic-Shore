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

    [SerializeField]
    Prism healthPrism;

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

    // Pre-allocated buffer for OverlapSphereNonAlloc — zero allocations per behavior update
    static readonly Collider[] _overlapBuffer = new Collider[64];

    // Cache component lookups to avoid GetComponent in hot loop
    readonly Dictionary<Collider, Boid> _boidCache = new(32);
    readonly Dictionary<Collider, Prism> _prismCache = new(64);

    public BoidManager BoidManager { get; set; }
    public BoidController BoidController { get; set; }

    public override void Initialize(Cell cell)
    {
        embeddedHealthPrism = GetComponentInChildren<HealthPrism>(true);
        if (!embeddedHealthPrism)
        {
            CSDebug.LogError($"{nameof(Boid)} on {name} has no embedded HealthPrism in children. Scaling cannot work.");
            return;
        }

        blockCollider = embeddedHealthPrism.GetComponent<BoxCollider>();
        if (!blockCollider)
            CSDebug.LogWarning($"{nameof(Boid)} on {name}: embedded HealthPrism has no BoxCollider.");

        embeddedHealthPrism.ChangeTeam(domain);

        currentVelocity = transform.forward * Random.Range(minSpeed, Mathf.Max(minSpeed, maxSpeed));
        float initialDelay = normalizedIndex * behaviorUpdateRate;
        StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
    }

    WaitForSeconds _behaviorWait;

    IEnumerator CalculateBehaviorCoroutine(float initialDelay)
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        _behaviorWait = new WaitForSeconds(behaviorUpdateRate);

        while (true)
        {
            if (!isAttached)
                target = Goal;

            CalculateBehavior();
            yield return _behaviorWait;
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

        // NonAlloc: zero heap allocation per call
        int colliderCount = Physics.OverlapSphereNonAlloc(transform.position, cohesionRadius, _overlapBuffer);

        for (int i = 0; i < colliderCount; i++)
        {
            Collider collider = _overlapBuffer[i];
            if (!collider) continue;
            if (blockCollider && collider.gameObject == blockCollider.gameObject) continue;

            // Cached component lookups — avoid GetComponent every behavior tick
            if (!_boidCache.TryGetValue(collider, out var otherBoid))
            {
                otherBoid = collider.GetComponentInParent<Boid>();
                _boidCache[collider] = otherBoid;
            }

            if (!_prismCache.TryGetValue(collider, out var otherPrism))
            {
                collider.TryGetComponent(out otherPrism);
                _prismCache[collider] = otherPrism;
            }

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

        int totalBoids = colliderCount - 1;

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
    protected override void Die(string killername = "")
    {
        throw new System.NotImplementedException();
    }

    static readonly Collider[] _moundBuffer = new Collider[32];

    IEnumerator AddToMoundCoroutine()
    {
        isAttached = false;
        isTraveling = true;

        if (Mound) target = Mound.position;

        float scanRadius = 30f;

        int hitCount = 0;
        while (hitCount == 0)
        {
            int layerIndex = LayerMask.NameToLayer("Mound");
            int layerMask = 1 << layerIndex;
            hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _moundBuffer, layerMask);

            GyroidAssembler nakedEdge = null;
            for (int i = 0; i < hitCount; i++)
            {
                var collider = _moundBuffer[i];
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

            if (!nakedEdge) hitCount = 0;
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
        var newBlock = Instantiate(healthPrism, transform.position, transform.rotation, transform);
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
