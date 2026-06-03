using System.Collections;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public sealed class RandomLifeSpawner : CellLifeSpawnerBase
    {
        // Fauna spawn-interval multipliers by CellAggressionLevel — mirrors
        // IntensityWiseLifeSpawner so Cell.CurrentFaunaSpawnPeriod (which the HUD
        // spawn-cycle ring reads) stays in lock-step with this spawner's real cadence.
        static readonly float[] FaunaSpawnIntervalByAggression = { 1f, 0.55f, 0.25f };

        static float ScaleFaunaInterval(Cell host, float baseSeconds)
        {
            if (!host) return baseSeconds;
            int idx = Mathf.Clamp((int)host.AggressionLevel, 0, FaunaSpawnIntervalByAggression.Length - 1);
            return Mathf.Max(0.05f, baseSeconds * FaunaSpawnIntervalByAggression[idx]);
        }

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

            var excluded = GetExcludedDomain(spawnProfile.FaunaExcludeLocalDomain, gameData, fallbackLocal: Domains.Jade);

            foreach (var faunaCfg in spawnProfile.SupportedFaunas)
            {
                if (!faunaCfg || !faunaCfg.FaunaPrefab) continue;

                if (!AllowSpawn(faunaCfg.SpawnProbability))
                    continue;

                Track(host, SpawnFaunaTypeLoop_Random(host, runtime, spawnProfile, faunaCfg, excluded));
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
            FaunaConfigurationSO faunaCfg,
            Domains? excluded)
        {
            // Optional initial wait (old behavior used InitialFaunaSpawnWaitTime too)
            if (spawnProfile.InitialFaunaSpawnWaitTime > 0f)
                yield return new WaitForSeconds(spawnProfile.InitialFaunaSpawnWaitTime);

            // Initial batch — gated on FaunaSpawningEnabled (cell has crossed Quiet),
            // matching IntensityWiseLifeSpawner. The old scored-volume gate
            // (GetControllingVolume) reads ~0 in Menu_Main where no game is scored, so
            // fauna never spawned there; the phase gate ties spawning to the cell's
            // own live prism mass instead.
            int initialCount = Mathf.Max(0, faunaCfg.InitialSpawnCount);
            float initialInterval = Mathf.Max(0f, spawnProfile.FaunaSpawnIntervalSeconds);

            for (int i = 0; i < initialCount; i++)
            {
                if (host && host.FaunaSpawningEnabled)
                    TrySpawnFaunaRandom(host, runtime, faunaCfg, excluded);

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

            // Continuous spawn — phase-gated, interval scales with aggression so the
            // cycle accelerates under stress. Seed the spawn-cycle telemetry before the
            // first wait so the HUD ring starts at 0% and advances, instead of sitting
            // at the "never spawned" sentinel (the dead-ring symptom in Menu_Main).
            host.RecordFaunaSpawn();
            while (true)
            {
                float wait = ScaleFaunaInterval(host, Mathf.Max(0.05f, spawnProfile.BaseFaunaSpawnTime));
                yield return new WaitForSeconds(wait);

                if (!host) yield break;
                if (!host.FaunaSpawningEnabled) continue;

                TrySpawnFaunaRandom(host, runtime, faunaCfg, excluded);
                host.RecordFaunaSpawn();
            }
        }

        /// <summary>
        /// Spawns one fauna in a random (excluded-aware) domain — RandomLifeSpawner's
        /// distinguishing flavor vs IntensityWiseLifeSpawner's controlling-color spawns.
        /// Seeks the crystal when present, the cell centre otherwise, so fauna still
        /// appear in cells whose crystal hasn't spawned yet (e.g. Menu_Main on entry).
        /// </summary>
        void TrySpawnFaunaRandom(Cell host, CellRuntimeDataSO runtime, FaunaConfigurationSO faunaCfg, Domains? excluded)
        {
            Vector3 goal = TryGetCrystalGoal(runtime, out var crystalGoal)
                ? crystalGoal
                : host.transform.position;

            SpawnFauna(host, faunaCfg.FaunaPrefab, goal, excluded);
        }
    }
}