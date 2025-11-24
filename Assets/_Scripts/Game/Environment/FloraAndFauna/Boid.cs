using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore;
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
    [SerializeField ] float trailBlockInteractionRadius = 10f;

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

    BoxCollider BlockCollider;

    List<Collider> separatedBoids = new List<Collider>();


    public override void Initialize(Cell cell)
    {
        AddSpindle(spindle);
        BlockCollider = healthPrism.GetComponent<BoxCollider>();
        currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
        float initialDelay = normalizedIndex * behaviorUpdateRate;
        StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
        healthPrism.Domain = domain;
    }

    IEnumerator CalculateBehaviorCoroutine(float initialDelay)
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (!isAttached && Population.Goal != null) target = Population.Goal;
            // TODO add visual effect here leaving behind particles. and zoom ahead but decay in speed
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

        //LayerMask
        var boidsInVicinity = Physics.OverlapSphere(transform.position, cohesionRadius);
        var ColliderCount = boidsInVicinity.Length;
        for (int i = 0; i < ColliderCount; i++)
        {
            Collider collider = boidsInVicinity[i];

            if (collider.gameObject == BlockCollider.gameObject) continue;

            Boid otherBoid = collider.GetComponentInParent<Boid>();
            Prism otherPrism = collider.GetComponent<Prism>();

            Vector3 diff = transform.position - collider.transform.position;
            float distance = diff.magnitude;
            if (distance == 0) continue;

            if (otherBoid)
            {
                float weight = 1; // Placeholder for potential weight logic
                cohesion += -diff.normalized * weight / distance;
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
                //float blockWeight = Population.Weights[Mathf.Abs((int)otherTrailBlock.Team-1)]; // TODO: this is a hack to get the team weight, need to make this more robust
                blockAttraction += -diff.normalized / distance;

                if (distance < trailBlockInteractionRadius && otherPrism.Domain != healthPrism.Domain)
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
                                        healthPrism.Grow(1);
                                        if (healthPrism.IsLargest) StartCoroutine(AddToMoundCoroutine());
                                    }
                                    else target = DefaultGoal.position;
                                }
                                break;
                            case BoidCollisionEffects.Explode:
                                if ((currentVelocity * healthPrism.Volume).x == Mathf.Infinity || (currentVelocity * healthPrism.Volume).x == Mathf.NegativeInfinity)
                                {
                                    Debug.LogError($"Infinite velocity on block collision detected! velocity:({currentVelocity.x},{currentVelocity.y},{currentVelocity.z})");
                                    break;
                                }
                                otherPrism.Damage(currentVelocity * healthPrism.Volume, healthPrism.Domain, healthPrism.PlayerName + " boid", true);
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

        desiredDirection = ((separation * separationWeight) + (alignment * alignmentWeight) + (cohesion * cohesionWeight) + (goalDirection * goalWeight) + blockAttraction).normalized;
        currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);
        if (SafeLookRotation.TryGet(currentVelocity, out var desiredRot, this))
            desiredRotation = desiredRot;
        else
            desiredRotation = transform.rotation;
    }

    protected override void Spawn()
    {
        throw new System.NotImplementedException();
    }

    IEnumerator AddToMoundCoroutine()
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
            colliders = Physics.OverlapSphere(transform.position, scanRadius, layerMask);

            //colliders = Physics.OverlapSphere(transform.position, 1f, LayerMask.NameToLayer("Mound"));
            GyroidAssembler nakedEdge = null;
            foreach (var collider in colliders)
            {
                nakedEdge = collider.GetComponent<GyroidAssembler>();
                if (nakedEdge && !nakedEdge.IsFullyBonded() && nakedEdge.preferedBlocks.Count == 0 && (nakedEdge.IsBonded() || nakedEdge.isSeed))
                {
                    (var newBlock1, var gyroidBlock1) = NewBlock();
                    nakedEdge.preferedBlocks.Enqueue(gyroidBlock1);
                    gyroidBlock1.Prism = newBlock1;

                    //(var newBlock2, var gyroidBlock2) = NewBlock();
                    //nakedEdge.preferedBlocks.Enqueue(gyroidBlock2);
                    //gyroidBlock2.GyroidBlock = newBlock2;

                    nakedEdge.Depth = 1;
                    nakedEdge.StartBonding();
                    break;
                }
            }
            if (!nakedEdge) colliders = new Collider[0];
            yield return null;
        }

        isTraveling = false;
        healthPrism.IsLargest = false;
        healthPrism.DeactivateShields();
        healthPrism.Grow(-3);
    }

    (Prism, GyroidAssembler) NewBlock()
    {
        var newBlock = Instantiate(healthPrism, transform.position, transform.rotation, Population.transform);
        newBlock.ChangeTeam(healthPrism.Domain);
        newBlock.gameObject.layer = LayerMask.NameToLayer("Mound");
        newBlock.prismProperties = new()
        {
            prism = newBlock
        };
        var gyroidBlock = newBlock.gameObject.AddComponent<GyroidAssembler>();
        return (newBlock,gyroidBlock);
    }

    void Update()
    {
        transform.position += currentVelocity * Time.deltaTime;
        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime);
    }
}