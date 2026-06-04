using System.Collections;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public sealed class RandomLifeSpawner : CellLifeSpawnerBase
    {
        protected override void OnStart(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            Track(host, StartFloraLoops(host, config, runtime, gameData));
            Track(host, StartFaunaLoops(host, config, runtime, gameData));
        }

        IEnumerator StartFloraLoops(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            var spawnProfile = config.SpawnProfile;
            if (!spawnProfile) yield break;
            if (spawnProfile.SupportedFloras is not { Count: > 0 })
                yield break;

            var excluded = GetExcludedDomain(spawnProfile.FloraExcludeLocalDomain, gameData, fallbackLocal: Domains.Blue);

            foreach (var floraCfg in spawnProfile.SupportedFloras)
            {
                if (!floraCfg || !floraCfg.FloraPrefab) continue;

                if (!AllowSpawn(floraCfg.SpawnProbability))
                    continue;

                Track(host, SpawnFloraTypeLoop_Random(host, spawnProfile, floraCfg, excluded));
            }
        }

        IEnumerator StartFaunaLoops(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            var spawnProfile = config.SpawnProfile;
            if (!spawnProfile) yield break;
            if (spawnProfile.SupportedFaunas is not { Count: > 0 })
                yield break;

            // Fauna spawn in the cell's controlling color (see SpawnFaunaPopulation), so
            // there is no excluded-domain roll here — that exclusion is exactly what kept
            // the controller's own color (e.g. Jade) from ever appearing.
            foreach (var faunaCfg in spawnProfile.SupportedFaunas)
            {
                if (!faunaCfg || !faunaCfg.FaunaPrefab) continue;

                if (!AllowSpawn(faunaCfg.SpawnProbability))
                    continue;

                Track(host, SpawnFaunaTypeLoop_Random(host, runtime, spawnProfile, faunaCfg));
            }
        }

        IEnumerator SpawnFloraTypeLoop_Random(
            Cell host,
            SpawnProfileSO spawnProfile,
            FloraConfigurationSO floraCfg,
            Domains? excluded)
        {
            // Initial batch
            int initialCount = Mathf.Max(0, floraCfg.InitialSpawnCount);
            float initialInterval = Mathf.Max(0f, spawnProfile.FloraSpawnIntervalSeconds);

            for (int i = 0; i < initialCount; i++)
            {
                // Phase gate: plant only while the cell still allows new flora
                // (Phase < Settled). Replaces the old scored-volume ceiling, which
                // reads ~0 in Menu_Main and so never bounded planting there — tying
                // planting to the cell's own live prism mass is what lets the
                // grow/consume cycle close.
                if (host && host.FloraPlantingEnabled)
                    SpawnFlora(host, floraCfg.FloraPrefab, excluded);

                // Spread instantiation across frames. WaitForSeconds when an interval
                // is configured; otherwise yield a single frame so a large InitialSpawnCount
                // doesn't instantiate every (prism-bodied) life form in one frame — that
                // showed up as a ~48% frame spike in Cell.SpawnFaunaTypeLoop_Random.
                if (i < initialCount - 1)
                {
                    if (initialInterval > 0f) yield return new WaitForSeconds(initialInterval);
                    else yield return null;
                }
            }

            // Continuous — keeps ticking so planting resumes if the cell falls back
            // across the planting hysteresis floor (Phase drops below Settled again).
            while (true)
            {
                float waitPeriod = floraCfg.OverrideDefaultPlantPeriod
                    ? Mathf.Max(0f, (float)floraCfg.NewPlantPeriod)
                    : floraCfg.FloraPrefab.PlantPeriod;

                if (waitPeriod > 0f) yield return new WaitForSeconds(waitPeriod);
                else yield return null;

                if (!host) yield break;
                if (host.FloraPlantingEnabled)
                    SpawnFlora(host, floraCfg.FloraPrefab, excluded);
            }
        }

        IEnumerator SpawnFaunaTypeLoop_Random(
            Cell host,
            CellRuntimeDataSO runtime,
            SpawnProfileSO spawnProfile,
            FaunaConfigurationSO faunaCfg)
        {
            // Optional initial wait before the first population appears.
            if (spawnProfile.InitialFaunaSpawnWaitTime > 0f)
                yield return new WaitForSeconds(spawnProfile.InitialFaunaSpawnWaitTime);

            // Timer-driven spawning at a FIXED period — no phase gate, no aggression
            // scaling. Prism count drives fauna *aggression/behavior* (see Fauna /
            // LightFauna); the timer drives *when* they spawn. Each tick emits a
            // fixed-size population in a prey-weighted domain (across all playable domains),
            // but only while there is prey to eat: production pauses when no domain holds
            // opposing prism mass above FaunaFoodFloor, and starving fauna despawn — so the
            // population self-bounds to prey. (Docs/ECOSYSTEM.md §6, option C: prey-linked.)
            float period = Mathf.Max(0.05f, spawnProfile.BaseFaunaSpawnTime);

            while (true)
            {
                if (!host) yield break;

                // Cross-domain, prey-weighted spawn: the population's domain emerges from
                // the cell's current mass distribution, so consumers appear where there is
                // food and the DOMINANT canopy (the biggest prey pool for the OTHER colors)
                // gets grazed back down by other-domain fauna, which then starve as it thins.
                // Consumption + starvation regulate the cell — no imposed prism lifespan, no
                // regrowth pulse. (Docs/ECOSYSTEM.md §5–6.)
                if (TryPickPreyWeightedDomain(host, spawnProfile.FaunaFoodFloor, out var color))
                    SpawnFaunaPopulation(host, runtime, faunaCfg, color);

                // Reset the spawn-cycle ring each period whether or not prey allowed a
                // burst — the ring reflects the fixed timer cadence, not the food gate.
                host.RecordFaunaSpawn();

                yield return new WaitForSeconds(period);
            }
        }

        /// <summary>
        /// Spawns one fixed-size population in <paramref name="color"/> — the prey-weighted
        /// domain chosen by <see cref="CellLifeSpawnerBase.TryPickPreyWeightedDomain"/>. The
        /// school hunts opposing mass, so weighting the domain by available prey points fauna
        /// at whichever color currently dominates the cell. Seeks the crystal when present,
        /// the cell centre otherwise; each member adds its own orbit offset (Fauna) so the
        /// swarm spreads instead of stacking.
        /// </summary>
        void SpawnFaunaPopulation(Cell host, CellRuntimeDataSO runtime, FaunaConfigurationSO faunaCfg, Domains color)
        {
            int count = Mathf.Max(1, faunaCfg.PopulationSize);
            Vector3 goal = TryGetCrystalGoal(runtime, out var crystalGoal)
                ? crystalGoal
                : host.transform.position;

            for (int i = 0; i < count; i++)
                SpawnFaunaWithDomain(host, faunaCfg.FaunaPrefab, goal, color);
        }
    }
}