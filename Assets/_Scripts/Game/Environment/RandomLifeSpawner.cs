using System.Collections;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
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

            var excluded = GetExcludedDomain(spawnProfile.FloraExcludeLocalDomain, gameData, fallbackLocal: Domains.None);

            foreach (var floraCfg in spawnProfile.SupportedFloras)
            {
                if (!floraCfg || !floraCfg.FloraPrefab) continue;

                if (!AllowSpawn(floraCfg.SpawnProbability))
                    continue;

                Track(host, SpawnFloraTypeLoop_Random(host, gameData, spawnProfile, floraCfg, excluded));
            }
        }

        IEnumerator StartFaunaLoops(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            var spawnProfile = config.SpawnProfile;
            if (!spawnProfile) yield break;
            if (spawnProfile.SupportedFaunas is not { Count: > 0 })
                yield break;

            var excluded = GetExcludedDomain(spawnProfile.FaunaExcludeLocalDomain, gameData, fallbackLocal: Domains.Jade);

            foreach (var faunaCfg in spawnProfile.SupportedFaunas)
            {
                if (!faunaCfg || !faunaCfg.FaunaPrefab) continue;

                if (!AllowSpawn(faunaCfg.SpawnProbability))
                    continue;

                Track(host, SpawnFaunaTypeLoop_Random(host, runtime, gameData, spawnProfile, faunaCfg, excluded));
            }
        }

        IEnumerator SpawnFloraTypeLoop_Random(
            Cell host,
            GameDataSO gameData,
            SpawnProfileSO spawnProfile,
            FloraConfigurationSO floraCfg,
            Domains? excluded)
        {
            // Initial batch
            int initialCount = Mathf.Max(0, floraCfg.InitialSpawnCount);
            float initialInterval = Mathf.Max(0f, spawnProfile.FloraSpawnIntervalSeconds);

            for (int i = 0; i < initialCount; i++)
            {
                // Random mode volume gate
                if (GetControllingVolume(gameData) < spawnProfile.FloraSpawnVolumeCeiling)
                    SpawnFlora(host, floraCfg.FloraPrefab, excluded);

                if (initialInterval > 0f && i < initialCount - 1)
                    yield return new WaitForSeconds(initialInterval);
            }

            // Continuous
            while (true)
            {
                float waitPeriod = floraCfg.OverrideDefaultPlantPeriod
                    ? Mathf.Max(0f, (float)floraCfg.NewPlantPeriod)
                    : floraCfg.FloraPrefab.PlantPeriod;

                if (waitPeriod > 0f) yield return new WaitForSeconds(waitPeriod);
                else yield return null;

                if (GetControllingVolume(gameData) < spawnProfile.FloraSpawnVolumeCeiling)
                    SpawnFlora(host, floraCfg.FloraPrefab, excluded);
            }
        }

        IEnumerator SpawnFaunaTypeLoop_Random(
            Cell host,
            CellRuntimeDataSO runtime,
            GameDataSO gameData,
            SpawnProfileSO spawnProfile,
            FaunaConfigurationSO faunaCfg,
            Domains? excluded)
        {
            // Optional initial wait (old behavior used InitialFaunaSpawnWaitTime too)
            if (spawnProfile.InitialFaunaSpawnWaitTime > 0f)
                yield return new WaitForSeconds(spawnProfile.InitialFaunaSpawnWaitTime);

            // Initial batch
            int initialCount = Mathf.Max(0, faunaCfg.InitialSpawnCount);
            float initialInterval = Mathf.Max(0f, spawnProfile.FaunaSpawnIntervalSeconds);

            for (int i = 0; i < initialCount; i++)
            {
                if (GetControllingVolume(gameData) > spawnProfile.FaunaSpawnVolumeThreshold && TryGetCrystalGoal(runtime, out var goal))
                    SpawnFauna(host, faunaCfg.FaunaPrefab, goal, excluded);

                if (initialInterval > 0f && i < initialCount - 1)
                    yield return new WaitForSeconds(initialInterval);
            }

            // Continuous threshold loop (keeps your old random behaviour)
            yield return RunThresholdLoop(
                condition: () =>
                {
                    if (GetControllingVolume(gameData) <= spawnProfile.FaunaSpawnVolumeThreshold)
                        return false;

                    return runtime != null && runtime.CrystalTransform != null;
                },
                spawnOnce: () =>
                {
                    if (!TryGetCrystalGoal(runtime, out var goal)) return;
                    SpawnFauna(host, faunaCfg.FaunaPrefab, goal, excluded);
                },
                trueWait: () => spawnProfile.BaseFaunaSpawnTime,
                falseWait: () => 2f
            );
        }
    }
}