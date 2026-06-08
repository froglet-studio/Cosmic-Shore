using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Utility;
using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages a tadpole-style <see cref="Boid"/> murmuration: a large, flocking forager
    /// swarm. Boids forage opposing-domain prism mass (their Explode collision effect) and
    /// are prey for predator fauna (sharks). This manager keeps the swarm alive:
    ///
    /// - <see cref="numberOfBoids"/> is a TARGET the manager maintains — boids lost to
    ///   predators / attrition are respawned, so the murmuration persists indefinitely
    ///   instead of depleting (it "looks great as a murmuration" only when it stays dense).
    /// - <see cref="spawnAtCrystals"/> distributes the swarm across the cell's crystal
    ///   positions instead of around this transform, so foragers appear where trail-prism
    ///   obstacles accumulate (e.g. HexRace, where AI orbit crystals).
    ///
    /// Drone abilities use <see cref="Boid"/> directly (via BoidController) for mound
    /// building; that path does NOT go through this manager, so the forager behavior here
    /// never touches drones.
    /// </summary>
    public class BoidManager : Fauna
    {
        [Header("Boid Settings")]
        public Boid boidPrefab;
        [Tooltip("Target swarm size. The manager keeps the LIVE count at this value — " +
                 "respawning boids eaten by predators or otherwise lost — so the murmuration " +
                 "stays dense indefinitely. Larger = denser swarm (watch per-scene perf).")]
        public int numberOfBoids = 100;
        public float spawnRadius = 50.0f;

        [Header("Crystal-anchored spawning")]
        [Tooltip("When ON, boids spawn distributed across the cell's crystal positions " +
                 "(cellData.Crystals) instead of around this transform — so the forager swarm " +
                 "appears where obstacles accumulate (HexRace: AI orbit crystals leaving trail " +
                 "prisms). The swarm splits roughly evenly across all crystals. Requires " +
                 "cellData wired to the same CellRuntimeDataSO the crystal manager writes to.")]
        [SerializeField] bool spawnAtCrystals = false;
        [Tooltip("Random jitter radius around each crystal when spawnAtCrystals is ON.")]
        [SerializeField] float crystalSpawnJitter = 60f;

        [Header("Maintenance")]
        [Tooltip("Seconds between population top-ups (drop dead refs + respawn toward the target).")]
        [SerializeField] float maintainInterval = 1.5f;
        [Tooltip("Max boids respawned per maintenance tick, to avoid instantiation spikes.")]
        [SerializeField] int maxRespawnPerTick = 8;

        [Header("Global Boid Settings")]
        public Transform Mound;

        public List<Boid> Boids = new();
        public Trail boidTrail = new();

        float _nextMaintainAt;
        // Ever-increasing spawn index — drives normalizedIndex and the crystal round-robin
        // so respawned boids keep spreading rather than stacking on one crystal.
        int _spawnCounter;

        protected override void Start()
        {
            base.Start();

            // Transform-anchored swarms spawn their full batch immediately (the original
            // behavior). Crystal-anchored swarms wait — HexRace spawns crystals after track
            // generation, so the maintenance loop fills the swarm in once they exist.
            if (!spawnAtCrystals)
                FillToTarget(int.MaxValue);
        }

        void Update()
        {
            if (Time.time < _nextMaintainAt) return;
            _nextMaintainAt = Time.time + Mathf.Max(0.1f, maintainInterval);

            // Drop boids lost to predators / death so the live count is accurate, then
            // respawn toward the target. This is what makes the murmuration self-sustaining.
            Boids.RemoveAll(b => !b);
            FillToTarget(Mathf.Max(1, maxRespawnPerTick));
        }

        /// <summary>Respawn up to <paramref name="cap"/> boids toward <see cref="numberOfBoids"/>.</summary>
        void FillToTarget(int cap)
        {
            int deficit = numberOfBoids - Boids.Count;
            if (deficit <= 0) return;
            // Crystal mode can't place a boid until crystals exist — wait without erroring.
            if (spawnAtCrystals && !HasCrystals()) return;

            int toSpawn = Mathf.Min(deficit, cap);
            for (int i = 0; i < toSpawn; i++)
                SpawnOneBoid();
        }

        bool HasCrystals() =>
            cellData != null && cellData.Crystals != null && cellData.Crystals.Count > 0;

        void SpawnOneBoid()
        {
            Vector3 spawnPosition = ResolveSpawnPosition(_spawnCounter);
            SafeLookRotation.TryGet(Vector3.Cross(spawnPosition, Vector3.forward), out var initialRotation, boidPrefab);

            Boid newBoid = Instantiate(boidPrefab, spawnPosition, initialRotation, transform);
            newBoid.BoidManager = this;
            newBoid.domain = domain;
            newBoid.normalizedIndex = numberOfBoids > 0 ? (float)(_spawnCounter % numberOfBoids) / numberOfBoids : 0f;
            newBoid.Initialize(cell);

            Boids.Add(newBoid);
            _spawnCounter++;

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

        /// <summary>
        /// Where to spawn boid <paramref name="index"/>. In crystal mode, round-robins across
        /// the cell's crystals (jittered) so the swarm splits evenly and foragers sit on the
        /// obstacle hotspots; otherwise the legacy ring around this transform.
        /// </summary>
        Vector3 ResolveSpawnPosition(int index)
        {
            if (spawnAtCrystals && HasCrystals())
            {
                var crystals = cellData.Crystals;
                for (int k = 0; k < crystals.Count; k++)
                {
                    var c = crystals[(index + k) % crystals.Count];
                    if (c) return c.transform.position + Random.insideUnitSphere * crystalSpawnJitter;
                }
            }
            return transform.position + (spawnRadius * (Quaternion.AngleAxis(Random.Range(0, 360), Vector3.forward) * Vector3.right));
        }
    }
}
