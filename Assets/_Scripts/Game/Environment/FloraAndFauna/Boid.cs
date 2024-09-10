using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore;

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


    protected override void Start()
    {
        base.Start();
        AddSpindle(spindle);
        BlockCollider = healthBlock.GetComponent<BoxCollider>();
        currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
        float initialDelay = normalizedIndex * behaviorUpdateRate;
        StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
        healthBlock.Team = Team;
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
            desiredRotation = currentVelocity != Vector3.zero ? Quaternion.LookRotation(currentVelocity.normalized) : transform.rotation;
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
            TrailBlock otherTrailBlock = collider.GetComponent<TrailBlock>();

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
            else if (otherTrailBlock)
            {
                //float blockWeight = Population.Weights[Mathf.Abs((int)otherTrailBlock.Team-1)]; // TODO: this is a hack to get the team weight, need to make this more robust
                blockAttraction += -diff.normalized / distance;

                if (distance < trailBlockInteractionRadius && otherTrailBlock.Team != healthBlock.Team)
                {
                    foreach (var effect in collisionEffects)
                    {
                        switch (effect)
                        {
                            case BoidCollisionEffects.Attach:
                                if (!isTraveling)
                                {
                                    if (!otherTrailBlock.IsSmallest)
                                    {
                                        isAttached = true;
                                        target = otherTrailBlock.transform.position;
                                        otherTrailBlock.Grow(-1);
                                        healthBlock.Grow(1);
                                        if (healthBlock.IsLargest) StartCoroutine(AddToMoundCoroutine());
                                    }
                                    else target = DefaultGoal.position;
                                }
                                break;
                            case BoidCollisionEffects.Explode:
                                if ((currentVelocity * healthBlock.Volume).x == Mathf.Infinity || (currentVelocity * healthBlock.Volume).x == Mathf.NegativeInfinity)
                                {
                                    Debug.LogError($"Infinite velocity on block collision detected! velocity:({currentVelocity.x},{currentVelocity.y},{currentVelocity.z})");
                                    break;
                                }
                                otherTrailBlock.Damage(currentVelocity * healthBlock.Volume, healthBlock.Team, healthBlock.PlayerName + " boid", true);
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
        desiredRotation = currentVelocity != Vector3.zero ? Quaternion.LookRotation(currentVelocity.normalized) : transform.rotation;
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
                    gyroidBlock1.TrailBlock = newBlock1;

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
        healthBlock.IsLargest = false;
        healthBlock.DeactivateShields();
        healthBlock.Grow(-3);
    }

    (TrailBlock, GyroidAssembler) NewBlock()
    {
        var newBlock = Instantiate(healthBlock, transform.position, transform.rotation, Population.transform);
        newBlock.Team = healthBlock.Team;
        newBlock.gameObject.layer = LayerMask.NameToLayer("Mound");
        newBlock.TrailBlockProperties = new()
        {
            trailBlock = newBlock
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