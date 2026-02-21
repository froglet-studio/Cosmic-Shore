using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Game;

public class WormManager : Fauna
{
    [SerializeField] Worm wormPrefab;
    [SerializeField] Worm emptyWormPrefab;
    [SerializeField] int initialWormCount = 3;
    [SerializeField] float spawnRadius = 50f;
    [SerializeField] float growthInterval = 10f;
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

    public override void Initialize(Cell cell) { }

    protected override void Spawn()
    {
        SpawnInitialWorms();
    }

    protected override void Die(string killername = "")
    {
        foreach (var worm in activeWorms.ToArray())
        {
            if (worm) Destroy(worm.gameObject);
        }
        activeWorms.Clear();
        Destroy(gameObject);
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
            Vector3 highDensityPosition = cell.GetExplosionTarget(domain);
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
        newWorm.Domain = domain;
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