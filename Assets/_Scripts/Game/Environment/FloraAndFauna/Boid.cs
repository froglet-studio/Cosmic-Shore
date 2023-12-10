using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Environment.FlowField;
using CosmicShore.Core.HangerBuilder;

public class Boid : MonoBehaviour
{
    [Header("Detection Settings")]
    public float cohesionRadius = 10.0f;
    public float behaviorUpdateRate = 1.5f;
    public float separationRadius = 5f;

    [Header("Behavior Weights")]
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float goalWeight = 1.0f;

    [Header("Speed Settings")]
    public float minSpeed = 2.0f;
    public float maxSpeed = 5.0f;

    [Header("Goal Settings")]
    public Transform goal;
    public float normalizedIndex;

    private Vector3 currentVelocity;
    private Vector3 desiredDirection;

    private BoidManager boidManager;
    private TrailBlock trailBlock;
    private BoxCollider BlockCollider;
    private Crystal crystal;
    [SerializeField] Material activeCrystalMaterial;

    private List<Collider> separatedBoids = new List<Collider>();

    //Collider[] boidsInVicinity = new Collider[100];

    private void Start()
    {
        crystal = GetComponentInChildren<Crystal>();
        boidManager = GetComponentInParent<BoidManager>();
        trailBlock = GetComponentInChildren<TrailBlock>();
        BlockCollider = GetComponentInChildren<BoxCollider>();
        currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
        float initialDelay = normalizedIndex * behaviorUpdateRate;
        StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
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
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 goalDirection = goal ? (goal.position - transform.position) : Vector3.zero;
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
                float blockWeight = boidManager.Weights[(int)otherTrailBlock.Team - 1];
                blockAttraction += -diff.normalized * blockWeight / distance;

                if (distance < BlockCollider.size.magnitude * 3 && otherTrailBlock.Team != trailBlock.Team)
                {
                    otherTrailBlock.Explode(currentVelocity, trailBlock.Team, trailBlock.PlayerName + " boid", true);
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
    }

    void Update()
    {

        if (trailBlock.destroyed && !crystal.enabled)
        {
            crystal.transform.parent = boidManager.transform;
            crystal.gameObject.GetComponent<SphereCollider>().enabled = true;
            crystal.enabled = true;

            crystal.GetComponentInChildren<SkinnedMeshRenderer>().material = activeCrystalMaterial; // TODO: make a crytal material set that this pulls from using the element
            StopAllCoroutines();
            Destroy(gameObject);
            return;
        }

        if (trailBlock.Team != Teams.Blue)
        {
            goal = trailBlock.Player.Ship.transform; // make event driven
        }

        transform.position += currentVelocity * Time.deltaTime;

        Quaternion desiredRotation = currentVelocity != Vector3.zero ? Quaternion.LookRotation(currentVelocity.normalized) : transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime);
    }
}
