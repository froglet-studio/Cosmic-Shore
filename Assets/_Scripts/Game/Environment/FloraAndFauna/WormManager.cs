using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Game;

/// <summary>
/// Manages a group of <see cref="Worm"/> creatures.
/// Handles spawning, periodic growth, and target updates.
/// Extends Fauna for domain/goal propagation from the spawning system (LSP-compliant:
/// lifecycle methods use base defaults instead of throwing NotImplementedException).
/// </summary>
public class WormManager : Fauna
{
    [Header("Worm Prefabs")]
    [SerializeField] Worm wormPrefab;
    [SerializeField] Worm emptyWormPrefab;

    [Header("Spawn Settings")]
    [SerializeField] int initialWormCount = 3;
    [SerializeField] float spawnRadius = 50f;

    [Header("Behavior Settings")]
    [SerializeField] float growthInterval = 10f;
    [SerializeField] float targetUpdateInterval = 5f;

    Vector3 headSpacing;
    Vector3 tailSpacing;
    Vector3 middleSpacing;

    readonly List<Worm> activeWorms = new();
    float growthTimer;
    float targetUpdateTimer;

    protected override void Start()
    {
        base.Start();
        CacheSegmentSpacing();
        SpawnInitialWorms();
    }

    void CacheSegmentSpacing()
    {
        var segments = wormPrefab.initialSegments;
        headSpacing = segments[0].transform.position - segments[1].transform.position;
        tailSpacing = segments[segments.Count - 1].transform.position - segments[segments.Count - 2].transform.position;
        middleSpacing = segments[2].transform.position - segments[1].transform.position;
    }

    void Update()
    {
        ManageWormGrowth();
        UpdateWormTargets();
    }

    void SpawnInitialWorms()
    {
        for (int i = 0; i < initialWormCount; i++)
        {
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
            CreateWorm(spawnPosition);
        }
    }

    void ManageWormGrowth()
    {
        growthTimer += Time.deltaTime;
        if (growthTimer >= growthInterval)
        {
            growthTimer = 0f;
            foreach (Worm worm in activeWorms)
                worm.AddSegment();
        }
    }

    void UpdateWormTargets()
    {
        targetUpdateTimer += Time.deltaTime;
        if (targetUpdateTimer >= targetUpdateInterval)
        {
            targetUpdateTimer = 0f;
            Vector3 highDensityPosition = cell.GetExplosionTarget(domain);
            foreach (Worm worm in activeWorms)
                worm.SetTarget(highDensityPosition);
        }
    }

    public Worm CreateWorm(Vector3 position, Worm newWormPrefab = null)
    {
        Worm newWorm = Instantiate(newWormPrefab ? newWormPrefab : wormPrefab, position, Quaternion.identity);
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
    }
}
