using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Environment.FlowField;
using CosmicShore;

public enum BoidCollisionEffects
{
    Attach = 0,
    Explode = 1,
}

public class Boid : MonoBehaviour
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
    public Transform Goal;
    public Transform DefaultGoal;
    public float normalizedIndex;

    [Header("Mound Settings")]
    public Transform Mound;

    private Vector3 currentVelocity;
    private Vector3 desiredDirection;
    Quaternion desiredRotation;

    public bool isKilled = false;
    public Teams Team = Teams.Blue;

    bool isTraveling = false;

    [SerializeField] List<BoidCollisionEffects> collisionEffects;

    public BoidManager boidManager;
    private TrailBlock trailBlock;
    private BoxCollider BlockCollider;
    private Crystal crystal;
    [SerializeField] Material activeCrystalMaterial;

    private List<Collider> separatedBoids = new List<Collider>();

    //Collider[] boidsInVicinity = new Collider[100];

    bool attached = false;
    [SerializeField] bool hasCrystal = true;

    private void Start()
    {
        crystal = GetComponentInChildren<Crystal>();
        if (!boidManager) boidManager = GetComponentInParent<BoidManager>();
        trailBlock = GetComponentInChildren<TrailBlock>();
        BlockCollider = GetComponentInChildren<BoxCollider>();
        currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
        float initialDelay = normalizedIndex * behaviorUpdateRate;
        StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
        trailBlock.Team = Team;
    }

    IEnumerator CalculateBehaviorCoroutine(float initialDelay)
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            CalculateBehavior();
            yield return new WaitForSeconds(behaviorUpdateRate);
        }
    }

    void CalculateBehavior()
    {
        if (attached)
        {
            desiredDirection = (Goal.position - transform.position).normalized;
            currentVelocity = desiredDirection * Mathf.Clamp(currentVelocity.magnitude, minSpeed, maxSpeed);
            desiredRotation = currentVelocity != Vector3.zero ? Quaternion.LookRotation(currentVelocity.normalized) : transform.rotation;
            return;
        }
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 goalDirection = Goal ? (Goal.position - transform.position) : Vector3.zero;
        Vector3 blockAttraction = Vector3.zero;

        float averageSpeed = 0.0f;
        separatedBoids.Clear();

        //LayerMask
        var boidsInVicinity = Physics.OverlapSphere(transform.position, cohesionRadius);
        var ColliderCount = boidsInVicinity.Length;
        //var ColliderCount = Physics.OverlapSphereNonAlloc(transform.position, cohesionRadius, boidsInVicinity, LayerMask.NameToLayer("TrailBlocks"));
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
                Debug.Log($"TrailBlock.Team {otherTrailBlock.Team}");
                float blockWeight = boidManager.Weights[Mathf.Abs((int)otherTrailBlock.Team-1)]; // TODO: this is a hack to get the team weight, need to make this more robust
                blockAttraction += -diff.normalized * blockWeight / distance;

                if (distance < trailBlockInteractionRadius && otherTrailBlock.Team != trailBlock.Team)
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
                                        attached = true;
                                        Goal = otherTrailBlock.transform;
                                        otherTrailBlock.Grow(-1);
                                        trailBlock.Grow(1);
                                        if (trailBlock.IsLargest) StartCoroutine(AddToMoundCoroutine());
                                    }
                                    else Goal = DefaultGoal;
                                }
                                break;
                            case BoidCollisionEffects.Explode:
                                otherTrailBlock.Explode(currentVelocity, trailBlock.Team, trailBlock.PlayerName + " boid", true);
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

    IEnumerator AddToMoundCoroutine()
    {
        attached = false;
        isTraveling = true;

        Goal = Mound;
        float scanRadius = 30f;
        //while ((transform.position - Mound.position).sqrMagnitude > scanRadius)
        //{
        //    yield return null;
        //}

        Collider[] colliders = new Collider[0];
        while (colliders.Length == 0)
        {
            int layerIndex = LayerMask.NameToLayer("Mound");
            int layerMask = 1 << layerIndex;
            colliders = Physics.OverlapSphere(transform.position, scanRadius, layerMask);

            //colliders = Physics.OverlapSphere(transform.position, 1f, LayerMask.NameToLayer("Mound"));
            GyroidAssembler nakedEdge = null;
            Debug.Log($"colliders: {colliders.Length}");
            foreach (var collider in colliders)
            {
                nakedEdge = collider.GetComponent<GyroidAssembler>();
                if (nakedEdge && !nakedEdge.FullyBonded && nakedEdge.preferedBlocks.Count == 0 && (nakedEdge.IsBonded() || nakedEdge.isSeed))
                {
                    (var newBlock1, var gyroidBlock1) = NewBlock();
                    nakedEdge.preferedBlocks.Enqueue(gyroidBlock1);
                    gyroidBlock1.GyroidBlock = newBlock1;

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
        trailBlock.IsLargest = false;
        trailBlock.DeactivateShields();
        trailBlock.Grow(-3);
    }

    (TrailBlock, GyroidAssembler) NewBlock()
    {
        var newBlock = Instantiate(trailBlock, transform.position, transform.rotation, boidManager.transform);
        newBlock.Team = trailBlock.Team;
        newBlock.gameObject.layer = LayerMask.NameToLayer("Mound");
        //var ID = GetInstanceID().ToString();                    
        //Debug.Log($"ID of created : {ID}");
        //newBlock.ID = ID;
        var gyroidBlock = newBlock.gameObject.AddComponent<GyroidAssembler>();
        return (newBlock,gyroidBlock);
    }

    void Poop()
    {
        attached = false;
        var newblock = Instantiate(trailBlock, transform.position, transform.rotation, boidManager.transform);
        newblock.Team = trailBlock.Team;
        trailBlock.Grow(-3);
    }

    void Update()
    {

        if ((trailBlock.destroyed || isKilled) && hasCrystal && !crystal.enabled) // TODO: still need the crystal check?
        {
            crystal.transform.parent = boidManager.transform;
            crystal.gameObject.GetComponent<SphereCollider>().enabled = true;
            crystal.enabled = true; 

            crystal.GetComponentInChildren<SkinnedMeshRenderer>().material = activeCrystalMaterial; // TODO: make a crytal material set that this pulls from using the element
            StopAllCoroutines();
            Destroy(gameObject);
            return;
        }

        //if (trailBlock.Team != Teams.Blue)
        //{
        //    goal = trailBlock.Player.Ship.transform; // TODO: unccomment and make event driven and commander friendly
        //}

        transform.position += currentVelocity * Time.deltaTime;
        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime);
    }
}
