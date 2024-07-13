using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Core;

public class WormManager : Population
{
    [SerializeField] private BodySegmentFauna headSegmentPrefab;
    [SerializeField] private BodySegmentFauna middleSegmentPrefab;
    [SerializeField] private BodySegmentFauna tailSegmentPrefab;
    [SerializeField] private Worm wormPrefab;

    [SerializeField] private int initialWormCount = 3;
    [SerializeField] private int initialSegmentsPerWorm = 5;
    [SerializeField] private float spawnRadius = 50f;
    [SerializeField] private float growthInterval = 10f;
    [SerializeField] private int maxWormsAllowed = 10;
    [SerializeField] private float targetUpdateInterval = 5f;

    private List<Worm> activeWorms = new List<Worm>();
    private float growthTimer;
    private float targetUpdateTimer;

    protected override void Start()
    {
        base.Start();
        SpawnInitialWorms();
    }

    private void Update()
    {
        ManageWormGrowth();
        UpdateWormTargets();
    }

    private void SpawnInitialWorms()
    {
        for (int i = 0; i < initialWormCount; i++)
        {
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
            Worm newWorm = CreateWorm(spawnPosition, initialSegmentsPerWorm);
            activeWorms.Add(newWorm);
        }
    }

    private void ManageWormGrowth()
    {
        growthTimer += Time.deltaTime;
        if (growthTimer >= growthInterval)
        {
            growthTimer = 0f;
            foreach (Worm worm in activeWorms)
            {
                if (worm.Segments.Count > 2) // Only grow if there's at least one middle segment
                {
                    BodySegmentFauna newSegment;
                    if (!worm.hasHead) newSegment = CreateSegment(worm.Segments[0].transform.position, 1);
                    else if (!worm.hasTail) newSegment = CreateSegment(worm.Segments[-1].transform.position, 1);
                    else newSegment = CreateSegment(worm.Segments[1].transform.position, 0.8f);
                    worm.AddSegment(newSegment, 3); // Insert after the head
                }
            }
        }
    }

    private void UpdateWormTargets()
    {
        targetUpdateTimer += Time.deltaTime;
        if (targetUpdateTimer >= targetUpdateInterval)
        {
            targetUpdateTimer = 0f;
            Vector3 highDensityPosition = node.GetExplosionTargets(1, Team)[0];
            foreach (Worm worm in activeWorms)
            {
                worm.SetTarget(highDensityPosition);
            }
        }
    }

    public Worm CreateWorm(Vector3 position, int segmentCount)
    {
        Worm newWorm = Instantiate(wormPrefab, position, Quaternion.identity);
        newWorm.Manager = this;
        newWorm.Team = Team;

        for (int i = 0; i < segmentCount; i++)
        {
            BodySegmentFauna newSegment;
            if (i == 0)
                newSegment = CreateSegment(position, 1f, true, false);
            else if (i == segmentCount - 1)
                newSegment = CreateSegment(position, 0.6f, false, true);
            else
                newSegment = CreateSegment(position, Mathf.Max(0.6f, 1f - (i * 0.1f)), false, false);

            newWorm.AddSegment(newSegment);
            position += newWorm.transform.forward * newSegment.transform.localScale.z; // TODO: use cylinder or other correction, add parenting possibly
        }

        return newWorm;
    }

    public Worm CreateWorm(List<BodySegmentFauna> segments)
    {
        if (segments.Count == 0) return null;

        Worm newWorm = Instantiate(wormPrefab, segments[0].transform.position, Quaternion.identity);
        newWorm.Manager = this;
        newWorm.Team = Team;

        foreach (var segment in segments)
        {
            newWorm.AddSegment(segment);
        }

        activeWorms.Add(newWorm);
        return newWorm;
    }

    public BodySegmentFauna CreateSegment(Vector3 position, float scale, bool isHead = false, bool isTail = false)
    {
        BodySegmentFauna prefab = isHead ? headSegmentPrefab : (isTail ? tailSegmentPrefab : middleSegmentPrefab);
        BodySegmentFauna newSegment = Instantiate(prefab, position, Quaternion.identity);
        newSegment.SetScale(scale);
        newSegment.IsHead = isHead;
        newSegment.IsTail = isTail;
        newSegment.Team = Team;
        return newSegment;
    }

    public void RemoveWorm(Worm worm)
    {
        activeWorms.Remove(worm);
        Destroy(worm.gameObject);

        // Spawn a new worm if we're below the initial count
        if (activeWorms.Count < initialWormCount)
        {
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
            Worm newWorm = CreateWorm(spawnPosition, initialSegmentsPerWorm);
            activeWorms.Add(newWorm);
        }
    }

}