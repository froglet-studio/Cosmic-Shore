using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using StarWriter.Core;

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
    private List<Collider> separatedBoids = new List<Collider>();

    private void Start()
    {
        boidManager = GetComponentInParent<BoidManager>();
        trailBlock = GetComponent<TrailBlock>();
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
        Collider[] boidsInVicinity = Physics.OverlapSphere(transform.position, cohesionRadius);

        foreach (Collider collider in boidsInVicinity)
        {
            if (collider.gameObject == gameObject) continue;

            Boid otherBoid = collider.GetComponent<Boid>();
            TrailBlock otherTrailBlock = collider.GetComponent<TrailBlock>();

            Vector3 diff = transform.position - collider.transform.position;
            float distance = diff.magnitude;

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

                if (distance < GetComponent<BoxCollider>().size.magnitude * 3)
                {
                    otherTrailBlock.Explode(currentVelocity, Teams.None, "Boid", true);
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


    bool destroyed;
    void Update()
    {
        if (destroyed) return;

        if (trailBlock.destroyed)
        {
            destroyed = true;
            StopAllCoroutines();
            return;
        }

        transform.position += currentVelocity * Time.deltaTime;
        Quaternion desiredRotation = Quaternion.LookRotation(currentVelocity.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime);
    }
}
