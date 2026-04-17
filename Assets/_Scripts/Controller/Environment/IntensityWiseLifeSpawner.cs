using System.Collections;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public sealed class IntensityWiseLifeSpawner : CellLifeSpawnerBase
    {
        // Fauna spawn-interval multipliers by CellAggressionLevel. Lower = faster
        // spawn cadence under stress, so new fauna arrive before the cell chokes.
        static readonly float[] FaunaSpawnIntervalByAggression = { 1f, 0.6f, 0.35f, 0.2f };
        // Flora spawn-interval multipliers. Higher = slower spawn cadence under stress;
        // this keeps new gyroid seeds from compounding growth while cleanup is catching up.
        static readonly float[] FloraSpawnIntervalByAggression = { 1f, 1.4f, 2.0f, 4.0f };

        static float ScaleByAggression(Cell host, float baseSeconds, float[] table)
        {
            if (!host || table == null || table.Length == 0) return baseSeconds;
            int idx = Mathf.Clamp((int)host.AggressionLevel, 0, table.Length - 1);
            return Mathf.Max(0.05f, baseSeconds * table[idx]);
        }

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

                // Stretch flora spawn intervals under stress so growth doesn't compound.
                waitPeriod = ScaleByAggression(host, waitPeriod, FloraSpawnIntervalByAggression);

                if (waitPeriod > 0f)
                    yield return new WaitForSeconds(waitPeriod);
                else
                    yield return null;

                // Hard gate at Critical: stop seeding new flora entirely until the cell recovers.
                if (host && host.AggressionLevel == CellAggressionLevel.Critical)
                    continue;

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
                // Tighten fauna spawn cadence under stress so reinforcements arrive before the prism load overwhelms.
                wait = ScaleByAggression(host, wait, FaunaSpawnIntervalByAggression);
                if (wait > 0f) yield return new WaitForSeconds(wait);
                else yield return null;

                if (TryGetCrystalGoal(runtime, out var goal))
                    SpawnFauna(host, faunaCfg.FaunaPrefab, goal, excluded);
            }
        }
    }
}