using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine;

public class Boid : MonoBehaviour
{
    [Header("Detection Settings")]
    public float cohesionRadius = 10.0f; 
    public float behaviorUpdateRate = 0.2f;
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

    private void Start()
    {
        currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
        float initialDelay = normalizedIndex * behaviorUpdateRate;
        InvokeRepeating("CalculateBehavior", initialDelay, behaviorUpdateRate);
    }

    void CalculateBehavior()
    {
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 goalDirection = goal ? (goal.position - transform.position).normalized : Vector3.zero;

        float averageSpeed = 0.0f;
        List<Collider> separatedBoids = new();
        Collider[] boidsInVicinity = Physics.OverlapSphere(transform.position, cohesionRadius);
        foreach (Collider collider in boidsInVicinity)
        {
            

            Vector3 diff = transform.position - collider.transform.position;
            float distance = diff.magnitude;
            if (collider.gameObject != gameObject && collider.gameObject.GetComponent<TrailBlock>())
            {
                cohesion += collider.transform.position;
                alignment += collider.transform.forward;
            }

            if (distance < GetComponent<BoxCollider>().size.magnitude*2 && collider.gameObject.GetComponent<TrailBlock>() && !collider.CompareTag("Fauna"))
            {
                collider.gameObject.GetComponent<TrailBlock>().Explode(currentVelocity, Teams.None, "Boid", true);
            }
            else if (collider.gameObject != gameObject && collider.CompareTag("Fauna"))
            {
                separatedBoids.Add(collider);
                separation += diff.normalized / distance;
                Boid boidScript = collider.gameObject.GetComponent<Boid>();
                averageSpeed += boidScript.currentVelocity.magnitude;
            }
        }

        int totalBoids = boidsInVicinity.Length - 1; // Subtracting 1 to exclude the current boid

        if (totalBoids > 0)
        {
            cohesion /= totalBoids;
            cohesion = (cohesion - transform.position).normalized;
        }

        averageSpeed = separatedBoids.Count > 0 ? averageSpeed / separatedBoids.Count : currentVelocity.magnitude;

        desiredDirection = (separation * separationWeight + alignment * alignmentWeight + cohesion * cohesionWeight + goalDirection * goalWeight).normalized;
        currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);
    }

    void Update()
    {
        transform.position += currentVelocity * Time.deltaTime;
        Quaternion desiredRotation = Quaternion.LookRotation(currentVelocity.normalized);
        var angle = Vector3.Angle(currentVelocity.normalized, transform.forward);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, angle * Time.deltaTime);
    }

}