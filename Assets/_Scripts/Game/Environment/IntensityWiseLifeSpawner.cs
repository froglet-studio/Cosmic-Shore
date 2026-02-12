using System.Collections;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public sealed class IntensityWiseLifeSpawner : CellLifeSpawnerBase
    {
        protected override void OnStart(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            Track(host, SpawnInitialFlora(host, config, runtime, gameData));
            Track(host, SpawnInitialFauna(host, config, runtime, gameData));
        }

        IEnumerator SpawnInitialFlora(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            var spawnProfile = config.SpawnProfile;
            if (!spawnProfile) yield break;
            if (spawnProfile.SupportedFloras is not { Count: > 0 })
                yield break;

            var excluded = GetExcludedDomain(spawnProfile.FloraExcludeLocalDomain, gameData, fallbackLocal: Domains.Jade);

            // NEW: iterate every flora type, roll probability, start a per-type spawning coroutine
            foreach (var floraCfg in spawnProfile.SupportedFloras)
            {
                if (!floraCfg || !floraCfg.FloraPrefab) continue;

                if (!AllowSpawn(floraCfg.SpawnProbability))
                    continue;

                Track(host, SpawnFloraTypeLoop(host, spawnProfile, floraCfg, excluded));
            }
        }

        IEnumerator SpawnInitialFauna(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            var spawnProfile = config.SpawnProfile;
            if (!spawnProfile) yield break;
            if (spawnProfile.SupportedFaunas is not { Count: > 0 })
                yield break;

            var excluded = GetExcludedDomain(spawnProfile.FaunaExcludeLocalDomain, gameData, fallbackLocal: Domains.Jade);

            // NEW: iterate every fauna type, roll probability, start a per-type spawning coroutine
            foreach (var faunaCfg in spawnProfile.SupportedFaunas)
            {
                if (!faunaCfg || !faunaCfg.FaunaPrefab) continue;

                if (!AllowSpawn(faunaCfg.SpawnProbability))
                    continue;

                Track(host, SpawnFaunaTypeLoop(host, runtime, spawnProfile, faunaCfg, excluded));
            }
        }

        IEnumerator SpawnFloraTypeLoop(
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
                SpawnFlora(host, floraCfg.FloraPrefab, excluded);

                if (initialInterval > 0f && i < initialCount - 1)
                    yield return new WaitForSeconds(initialInterval);
            }

            // Continuous spawn for this flora type (uses its own period rule)
            while (true)
            {
                // same behavior as your old random loop period logic
                float waitPeriod = floraCfg.OverrideDefaultPlantPeriod
                    ? Mathf.Max(0f, (float)floraCfg.NewPlantPeriod)
                    : floraCfg.FloraPrefab.PlantPeriod;

                if (waitPeriod > 0f)
                    yield return new WaitForSeconds(waitPeriod);
                else
                    yield return null;

                SpawnFlora(host, floraCfg.FloraPrefab, excluded);
            }
        }

        IEnumerator SpawnFaunaTypeLoop(
            Cell host,
            CellRuntimeDataSO runtime,
            SpawnProfileSO spawnProfile,
            FaunaConfigurationSO faunaCfg,
            Domains? excluded)
        {
            // Initial batch
            int initialCount = Mathf.Max(0, faunaCfg.InitialSpawnCount);
            float initialInterval = Mathf.Max(0f, spawnProfile.FaunaSpawnIntervalSeconds);

            for (int i = 0; i < initialCount; i++)
            {
                if (TryGetCrystalGoal(runtime, out var goal))
                    SpawnFauna(host, faunaCfg.FaunaPrefab, goal, excluded);

                if (initialInterval > 0f && i < initialCount - 1)
                    yield return new WaitForSeconds(initialInterval);
            }

            // Continuous spawn for this fauna type
            while (true)
            {
                // If you later add per-type period fields, change this to faunaCfg.SpawnPeriodSeconds etc.
                float wait = Mathf.Max(0f, spawnProfile.BaseFaunaSpawnTime);
                if (wait > 0f) yield return new WaitForSeconds(wait);
                else yield return null;

                if (TryGetCrystalGoal(runtime, out var goal))
                    SpawnFauna(host, faunaCfg.FaunaPrefab, goal, excluded);
            }
        }
    }
}