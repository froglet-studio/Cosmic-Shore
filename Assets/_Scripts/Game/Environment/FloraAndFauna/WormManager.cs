using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Core;

public class WormManager : Population
{
    [SerializeField] Worm wormPrefab;
    [SerializeField] Worm emptyWormPrefab;
    [SerializeField] int initialWormCount = 3;
    [SerializeField] float spawnRadius = 50f;
    [SerializeField] float growthInterval = 10f;
    [SerializeField] int maxWormsAllowed = 10;
    [SerializeField] float targetUpdateInterval = 5f;

    Vector3 headSpacing;
    Vector3 tailSpacing;
    Vector3 middleSpacing;

    List<Worm> activeWorms = new List<Worm>();
    float growthTimer;
    float targetUpdateTimer;

    protected override void Start()
    {
        base.Start();
        headSpacing = wormPrefab.initialSegments[0].transform.position - wormPrefab.initialSegments[1].transform.position;
        tailSpacing = wormPrefab.initialSegments[wormPrefab.initialSegments.Count - 1].transform.position - wormPrefab.initialSegments[wormPrefab.initialSegments.Count - 2].transform.position;
        middleSpacing = wormPrefab.initialSegments[2].transform.position - wormPrefab.initialSegments[1].transform.position;
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
            CreateWorm(spawnPosition);
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
                worm.AddSegment();
            }
        }
    }

    private void UpdateWormTargets()
    {
        targetUpdateTimer += Time.deltaTime;
        if (targetUpdateTimer >= targetUpdateInterval)
        {
            targetUpdateTimer = 0f;
            Vector3 highDensityPosition = node.GetExplosionTarget(Team);
            foreach (Worm worm in activeWorms)
            {
                worm.SetTarget(highDensityPosition);
            }
        }
    }

    public Worm CreateWorm(Vector3 position , Worm newWormPrefab = null)
    {
        Worm newWorm;
        if (!newWormPrefab) newWorm = Instantiate(wormPrefab, position, Quaternion.identity);
        else newWorm = Instantiate(newWormPrefab, position, Quaternion.identity);

        newWorm.Manager = this;
        newWorm.Team = Team;
        newWorm.transform.parent = transform;
        newWorm.headSpacing = headSpacing;
        newWorm.tailSpacing = tailSpacing;
        newWorm.middleSpacing = middleSpacing;
        activeWorms.Add(newWorm);
        return newWorm;
    }

    public Worm CreateWorm(List<BodySegmentFauna> segments)
    {
        if (segments.Count == 0) return null;

        Worm newWorm = CreateWorm(segments[0].transform.position, emptyWormPrefab);

        newWorm.initialSegments = segments;
        newWorm.InitializeWorm();

        return newWorm;
    }

    public void RemoveWorm(Worm worm)
    {
        activeWorms.Remove(worm);
        Destroy(worm.gameObject);

        //// Spawn a new worm if we're below the initial count
        //if (activeWorms.Count < initialWormCount)
        //{
        //    Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
        //    Worm newWorm = CreateWorm(spawnPosition);
        //    activeWorms.Add(newWorm);
        //}
    }

}