using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game.Fauna
{
    /// <summary>
    /// Manages a swarm of Locust fauna that consume open-ended trail prisms.
    /// Spawning is driven by a CellAggressionManager which monitors total prism
    /// volume in the Cell and relays an aggression level (0-1).
    /// </summary>
    public class LocustSwarmManager : CosmicShore.Fauna
    {
        [Header("Swarm Settings")]
        [SerializeField] Locust locustPrefab;
        [SerializeField] LocustConfigSO config;

        [Header("Variants")]
        [Tooltip("Optional variant species. If populated, spawns are weighted across these variants.")]
        [SerializeField] List<LocustVariantSO> variants = new();

        [Header("Spawning")]
        [Tooltip("Maximum swarm population for this manager.")]
        [SerializeField] int maxSwarmSize = 40;
        [Tooltip("How many locusts to spawn per spawn wave.")]
        [SerializeField] int spawnBatchSize = 3;
        [Tooltip("Seconds between spawn waves (scaled by aggression).")]
        [SerializeField] float baseSpawnInterval = 5f;
        [Tooltip("Spawn radius around the swarm manager's position.")]
        [SerializeField] float spawnRadius = 15f;

        [Header("Aggression")]
        [Tooltip("Reference to the Cell's aggression manager. If null, defaults to 0.5 aggression.")]
        [SerializeField] CellAggressionManager aggressionManager;

        readonly List<Locust> activeLocusts = new();
        public IReadOnlyList<Locust> ActiveLocusts => activeLocusts;

        /// <summary>
        /// Current aggression from the Cell (0 = dormant, 1 = maximum).
        /// </summary>
        public float CurrentAggression => aggressionManager ? aggressionManager.Aggression : 0.5f;

        Coroutine spawnRoutine;
        Coroutine purgeRoutine;

        protected override void Start()
        {
            base.Start();
        }

        public override void Initialize(Cell cell)
        {
            if (aggressionManager == null)
                aggressionManager = GetComponentInParent<CellAggressionManager>();

            if (aggressionManager == null)
                aggressionManager = FindAnyObjectByType<CellAggressionManager>();

            spawnRoutine = StartCoroutine(SpawnLoop());
            purgeRoutine = StartCoroutine(PurgeLoop());
        }

        #region Spawning

        IEnumerator SpawnLoop()
        {
            // Wait until aggression manager signals there are enough prisms
            while (aggressionManager && aggressionManager.Aggression <= 0f)
                yield return new WaitForSeconds(2f);

            while (true)
            {
                float aggression = CurrentAggression;

                // Spawn rate scales with aggression: high aggression = faster spawning
                float interval = baseSpawnInterval / Mathf.Max(aggression, 0.1f);

                if (activeLocusts.Count < maxSwarmSize && aggression > 0f)
                {
                    int toSpawn = Mathf.Min(spawnBatchSize, maxSwarmSize - activeLocusts.Count);

                    // Scale batch size with aggression
                    toSpawn = Mathf.Max(1, Mathf.RoundToInt(toSpawn * aggression));

                    for (int i = 0; i < toSpawn; i++)
                        SpawnLocust();
                }

                yield return new WaitForSeconds(interval);
            }
        }

        void SpawnLocust()
        {
            // Resolve which prefab and config to use (variant or default)
            Locust prefab = locustPrefab;
            LocustConfigSO cfg = config;
            LocustVariantSO chosenVariant = null;

            if (variants is { Count: > 0 })
            {
                chosenVariant = PickWeightedVariant();
                if (chosenVariant)
                {
                    if (chosenVariant.LocustPrefab) prefab = chosenVariant.LocustPrefab;
                    if (chosenVariant.Config) cfg = chosenVariant.Config;
                }
            }

            if (!prefab || !cfg) return;

            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            Vector3 spawnPos = transform.position + offset;
            Quaternion spawnRot = Random.rotation;

            var locust = Instantiate(prefab, spawnPos, spawnRot, transform);
            locust.SetSwarmManager(this);
            locust.SetConfig(cfg);
            locust.domain = domain;
            locust.NormalizedIndex = activeLocusts.Count > 0
                ? (float)activeLocusts.Count / maxSwarmSize
                : 0f;

            // Apply variant size multiplier
            if (chosenVariant && Mathf.Abs(chosenVariant.SizeMultiplier - 1f) > 0.01f)
                locust.transform.localScale *= chosenVariant.SizeMultiplier;

            locust.Initialize(cell);
            activeLocusts.Add(locust);
        }

        LocustVariantSO PickWeightedVariant()
        {
            float totalWeight = 0f;
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i]) totalWeight += variants[i].SpawnWeight;
            }

            if (totalWeight <= 0f) return variants[0];

            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < variants.Count; i++)
            {
                if (!variants[i]) continue;
                cumulative += variants[i].SpawnWeight;
                if (roll <= cumulative) return variants[i];
            }

            return variants[^1];
        }

        #endregion

        #region Swarm Management

        public void OnLocustDied(Locust locust)
        {
            activeLocusts.Remove(locust);
        }

        /// <summary>
        /// Periodically purge stale attachment tracking entries.
        /// </summary>
        IEnumerator PurgeLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(10f);
                Locust.PurgeStaleAttachments();
                CleanupDeadLocusts();
            }
        }

        void CleanupDeadLocusts()
        {
            for (int i = activeLocusts.Count - 1; i >= 0; i--)
            {
                if (!activeLocusts[i])
                    activeLocusts.RemoveAt(i);
            }
        }

        #endregion

        #region Lifecycle

        protected override void Spawn() { }

        protected override void Die(string killerName = "")
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            if (purgeRoutine != null) StopCoroutine(purgeRoutine);

            for (int i = activeLocusts.Count - 1; i >= 0; i--)
            {
                if (activeLocusts[i])
                    Destroy(activeLocusts[i].gameObject);
            }
            activeLocusts.Clear();

            Destroy(gameObject);
        }

        void OnDestroy()
        {
            StopAllCoroutines();
        }

        #endregion
    }
}
