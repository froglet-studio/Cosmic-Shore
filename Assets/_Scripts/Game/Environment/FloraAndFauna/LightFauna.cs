using UnityEngine;
using System.Collections;
using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;

public class LightFauna : Fauna
{
    const string PLAYER_NAME = "light FaunaPrefab";

    [Header("Data")]
    [SerializeField] private LightFaunaDataSO data;

    private Vector3 currentVelocity;
    private Vector3 desiredDirection;
    private Quaternion desiredRotation;

    [HideInInspector] public float Phase;

    public LightFaunaManager LightFaunaManager { get; set; }
    
    public override void Initialize(Cell cell)
    {
        if (!data)
        {
            Debug.LogError($"{nameof(LightFauna)} on {name} is missing {nameof(CosmicShore.LightFaunaDataSO)}.");
            return;
        }

        float minSpeed = Mathf.Max(0f, data.minSpeed);
        float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed);

        currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
        StartCoroutine(UpdateBehaviorCoroutine());
    }

    IEnumerator UpdateBehaviorCoroutine()
    {
        while (true)
        {
            if (!data)
                yield break;

            yield return new WaitForSeconds(Mathf.Max(0f, data.behaviorUpdateRate + Phase));
            UpdateBehavior();
        }
    }

    void UpdateBehavior()
    {
        if (!data)
            return;

        Vector3 separation = Vector3.zero;

        if (!IsFinite(Goal) || Goal.sqrMagnitude < 0.001f)
        {
            Goal = cellData && cellData.CrystalTransform ? cellData.CrystalTransform.position : cell.transform.position;
        }

        Vector3 goalDirection = (Goal - transform.position).normalized;

        int neighborCount = 0;
        float averageSpeed = 0f;

        float detectionRadius = Mathf.Max(0f, data.detectionRadius);
        float separationRadius = Mathf.Max(0f, data.separationRadius);
        float consumeRadius = Mathf.Max(0f, data.consumeRadius);

        var nearbyColliders = Physics.OverlapSphere(transform.position, detectionRadius);

        foreach (var collider in nearbyColliders)
        {
            if (!collider || collider.gameObject == gameObject) continue;

            Vector3 diff = transform.position - collider.transform.position;
            float distance = diff.magnitude;
            if (distance <= 0f) continue;

            // Handle Ships
            if (collider.TryGetComponent(out IVesselStatus _))
            {
                neighborCount++;
                separation -= diff.normalized / distance;
                continue;
            }

            // Handle other FaunaPrefab/health prisms
            var otherHealthBlock = collider.GetComponent<HealthPrism>();
            if (otherHealthBlock)
            {
                if (otherHealthBlock.LifeForm == this) continue;

                neighborCount++;

                if (distance < separationRadius)
                    separation += diff.normalized / distance;

                if (distance < consumeRadius && otherHealthBlock.LifeForm && otherHealthBlock.LifeForm.domain != domain)
                    otherHealthBlock.Consume(transform, domain, PLAYER_NAME, true);

                continue;
            }

            // Handle blocks
            Prism block = collider.GetComponent<Prism>();
            if (block && block.Domain != domain && distance < consumeRadius)
                block.Consume(transform, domain, PLAYER_NAME, true);
        }

        averageSpeed = neighborCount > 0
            ? (averageSpeed > 0 ? averageSpeed / neighborCount : currentVelocity.magnitude)
            : currentVelocity.magnitude;

        float separationWeight = Mathf.Max(0f, data.separationWeight);
        float goalWeight = Mathf.Max(0f, data.goalWeight);

        desiredDirection = ((separation * separationWeight) + (goalDirection * goalWeight)).normalized;

        float minSpeed = Mathf.Max(0f, data.minSpeed);
        float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed);

        currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);

        if (currentVelocity != Vector3.zero && SafeLookRotation.TryGet(currentVelocity, out var rotation, this))
            desiredRotation = rotation;
        else
            desiredRotation = transform.rotation;
    }

    void Update()
    {
        transform.position += currentVelocity * Time.deltaTime;

        float lerpSpeed = data ? Mathf.Max(0f, data.rotationLerpSpeed) : 5f;
        var t = Mathf.Clamp(Time.deltaTime * lerpSpeed, 0f, 0.99f);

        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, t);
    }

    protected override void Spawn()
    {
        // Implement spawn behavior if needed
    }

    protected override void Die(string killername = "")
    {
        StopAllCoroutines();
        if (LightFaunaManager)
            LightFaunaManager.RemoveFauna(this);
        else
            Destroy(gameObject);
    }

    static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
}
