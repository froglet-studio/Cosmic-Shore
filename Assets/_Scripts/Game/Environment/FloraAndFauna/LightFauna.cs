using UnityEngine;
using System.Collections;
using CosmicShore.Core;
using CosmicShore;
using CosmicShore.Game;

public class LightFauna : Fauna
{
    const string PLAYER_NAME = "light fauna";
    
    [Header("Detection Settings")]
    [SerializeField] float detectionRadius = 100.0f;
    //[SerializeField] float cohesionRadius = 8.0f;
    [SerializeField] float separationRadius = 100.0f;
    [SerializeField] float consumeRadius = 40.0f;
    [SerializeField] float behaviorUpdateRate = 2.0f;

    [Header("Behavior Weights")]
    [SerializeField] float separationWeight = 100f;
    //[SerializeField] float cohesionWeight = 1.0f;
    [SerializeField] float goalWeight = 1.5f;

    [Header("Movement")]
    [SerializeField] float minSpeed = 3.0f;
    [SerializeField] float maxSpeed = 6.0f;

    private Vector3 currentVelocity;
    private Vector3 desiredDirection;
    private Quaternion desiredRotation;

    [HideInInspector] public float Phase;

    public override void Initialize(Cell cell)
    {
        base.Initialize(cell);
        currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
        StartCoroutine(UpdateBehaviorCoroutine());
    }

    IEnumerator UpdateBehaviorCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(behaviorUpdateRate + Phase);
            UpdateBehavior();          
        }
    }

    void UpdateBehavior()
    {
        Vector3 separation = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 goalDirection = (Population.Goal - transform.position).normalized;
        
        int neighborCount = 0;
        float averageSpeed = 0f;

        var nearbyColliders = Physics.OverlapSphere(transform.position, detectionRadius);
        
        foreach (var collider in nearbyColliders)
        {
            if (collider.gameObject == gameObject) continue;

            Vector3 diff = transform.position - collider.transform.position;
            float distance = diff.magnitude;
            if (distance == 0) continue;

            // Hande Ships
            if (collider.TryGetComponent(out IVesselStatus _))
            {
                neighborCount++;
                separation -= diff.normalized / distance;
                continue;
            }

            // Handle other fauna
            var otherHealthBlock = collider.GetComponent<HealthBlock>();
            if (otherHealthBlock)
            {
                if (otherHealthBlock.LifeForm == this) continue;
                neighborCount++;
                //cohesion += collider.transform.position;
                
                if (distance < separationRadius)
                {
                    separation += diff.normalized / distance;
                }
                if (distance < consumeRadius && otherHealthBlock.LifeForm.domain != domain) 
                    // otherHealthBlock.Damage(currentVelocity, Team, "light fauna", true);
                    otherHealthBlock.Consume(transform, domain, PLAYER_NAME, true);
                continue;
            }

            // Handle blocks
            Prism block = collider.GetComponent<Prism>();
            if (block && block.Domain != domain && distance < consumeRadius)
            {
                block.Consume(transform, domain, PLAYER_NAME, true);
            }
        }

        if (neighborCount > 0)
        {
            //cohesion = ((cohesion / neighborCount) - transform.position).normalized;
            averageSpeed = averageSpeed > 0 ? averageSpeed / neighborCount : currentVelocity.magnitude;
        }
        else
        {
            averageSpeed = currentVelocity.magnitude;
        }

        // Combine behaviors
        desiredDirection = ((separation * separationWeight) + 
                          //(cohesion * cohesionWeight) + 
                          (goalDirection * goalWeight)).normalized;

        currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);
        desiredRotation = currentVelocity != Vector3.zero ? 
            Quaternion.LookRotation(currentVelocity) : transform.rotation;
    }

    void Update()
    {
        transform.position += currentVelocity * Time.deltaTime;
        var t = Mathf.Clamp(Time.deltaTime * 5f, 0, .99f);
        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, t);
    }

    protected override void Spawn()
    {
        // Implement spawn behavior if needed
    }
}
