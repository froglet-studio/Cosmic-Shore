using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;

/// <summary>
/// Manages a group of <see cref="Boid"/> creatures.
/// Handles spawning, trail registration, and mound target assignment.
/// Extends Fauna for domain/goal propagation from the spawning system (LSP-compliant:
/// lifecycle methods use base defaults instead of throwing NotImplementedException).
/// </summary>
public class BoidManager : Fauna
{
    [Header("Boid Settings")]
    public Boid boidPrefab;
    public int numberOfBoids = 100;
    public float spawnRadius = 50.0f;

    [Header("Global Boid Settings")]
    public Transform Mound;

    public List<Boid> Boids;
    public Trail boidTrail = new();

    protected override void Start()
    {
        base.Start();
        SpawnBoids();
    }

    void SpawnBoids()
    {
        for (int i = 0; i < numberOfBoids; i++)
        {
            Vector3 spawnPosition = transform.position + (spawnRadius * (Quaternion.AngleAxis(Random.Range(0, 360), Vector3.forward) * Vector3.right));
            SafeLookRotation.TryGet(Vector3.Cross(spawnPosition, Vector3.forward), out var initialRotation, boidPrefab);

            Boid newBoid = Instantiate(boidPrefab, spawnPosition, initialRotation, transform);
            newBoid.BoidManager = this;
            newBoid.domain = domain;
            newBoid.normalizedIndex = (float)i / numberOfBoids;
            newBoid.Initialize(cell);

            Boids.Add(newBoid);

            var block = newBoid.GetComponentInChildren<Prism>(true);
            if (block)
            {
                boidTrail.Add(block);
                block.ChangeTeam(domain);
                block.Trail = boidTrail;
            }

            if (Mound)
                newBoid.Mound = Mound;
        }
    }
}
