using UnityEngine;
using System.Collections;
using CosmicShore.Core;
using CosmicShore;
using System.Collections.Generic;

public class LightFauna : Fauna
{
    [Header("Detection Settings")]
    [SerializeField] float cohesionRadius = 8.0f;
    [SerializeField] float separationRadius = 4.0f;
    [SerializeField] float behaviorUpdateRate = 1.0f;

    [Header("Behavior Weights")]
    [SerializeField] float separationWeight = 2.0f;
    [SerializeField] float cohesionWeight = 1.0f;
    [SerializeField] float goalWeight = 1.5f;

    [Header("Movement")]
    [SerializeField] float minSpeed = 3.0f;
    [SerializeField] float maxSpeed = 6.0f;

    private Vector3 currentVelocity;
    private Vector3 desiredDirection;
    private Quaternion desiredRotation;

    protected override void Start()
    {
        base.Start();
        currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
        StartCoroutine(UpdateBehaviorCoroutine());
        //if (healthBlock) healthBlock.Team = Team;
    }

    IEnumerator UpdateBehaviorCoroutine()
    {
        while (true)
        {
            UpdateBehavior();
            yield return new WaitForSeconds(behaviorUpdateRate);
        }
    }

    void UpdateBehavior()
    {
        Vector3 separation = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 goalDirection = Population.Goal - transform.position;
        
        int neighborCount = 0;
        float averageSpeed = 0f;

        var nearbyColliders = Physics.OverlapSphere(transform.position, cohesionRadius);
        
        foreach (var collider in nearbyColliders)
        {
            if (collider.gameObject == gameObject) continue;

            Vector3 diff = transform.position - collider.transform.position;
            float distance = diff.magnitude;
            if (distance == 0) continue;

            // Handle other fauna
            LightFauna otherFauna = collider.GetComponentInParent<LightFauna>();
            if (otherFauna)
            {
                neighborCount++;
                cohesion += collider.transform.position;
                
                if (distance < separationRadius)
                {
                    separation += diff.normalized / distance;
                    averageSpeed += otherFauna.currentVelocity.magnitude;
                }
                continue;
            }

            // Handle blocks
            TrailBlock block = collider.GetComponent<TrailBlock>();
            if (block && block.Team != Team && distance < cohesionRadius)
            {
                goalDirection = (block.transform.position - transform.position).normalized;
                if (distance < 2f && !healthBlock)
                {
                    block.Damage(currentVelocity * healthBlock.Volume, Team, "light fauna", true);
                }
            }
        }

        if (neighborCount > 0)
        {
            cohesion = ((cohesion / neighborCount) - transform.position).normalized;
            averageSpeed = averageSpeed > 0 ? averageSpeed / neighborCount : currentVelocity.magnitude;
        }
        else
        {
            averageSpeed = currentVelocity.magnitude;
        }

        // Combine behaviors
        desiredDirection = ((separation * separationWeight) + 
                          (cohesion * cohesionWeight) + 
                          (goalDirection * goalWeight)).normalized;

        currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);
        desiredRotation = currentVelocity != Vector3.zero ? 
            Quaternion.LookRotation(currentVelocity) : transform.rotation;
    }

    void Update()
    {
        transform.position += currentVelocity * Time.deltaTime;
        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime * 5f);
    }

    protected override void Spawn()
    {
        // Implement spawn behavior if needed
    }
}
