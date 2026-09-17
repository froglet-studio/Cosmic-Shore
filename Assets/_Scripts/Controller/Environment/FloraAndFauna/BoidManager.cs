using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Utility;
using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
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

                // The replication seam. A boid has no config of its own, so this resolves to
                // NeutralizeStray - and here that is NOT bookkeeping: both shipped population
                // prefabs wire `boidPrefab` to TadPoleFauna.prefab, which CARRIES a
                // NetworkObject, and they spawn 100 and 150 of them. Un-spawned, that is 100
                // identical GlobalObjectIdHash entries in one scene, which is exactly the
                // scene-object index collision that breaks synchronization for every later
                // joiner (Docs/PartySystem/BUGS.md B16, B5).
                FaunaNetworkSync.ServerSpawn(newBoid);

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
}
