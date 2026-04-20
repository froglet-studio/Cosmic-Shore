using System.Collections;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public sealed class IntensityWiseLifeSpawner : CellLifeSpawnerBase
    {
        // Fauna spawn-interval multipliers by CellAggressionLevel (3 levels).
        // Lower = faster cadence under stress so reinforcements arrive.
        static readonly float[] FaunaSpawnIntervalByAggression = { 1f, 0.55f, 0.25f };

        static float ScaleFaunaInterval(Cell host, float baseSeconds)
        {
            if (!host) return baseSeconds;
            int idx = Mathf.Clamp((int)host.AggressionLevel, 0, FaunaSpawnIntervalByAggression.Length - 1);
            return Mathf.Max(0.05f, baseSeconds * FaunaSpawnIntervalByAggression[idx]);
        }

        protected override void OnStart(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            Track(host, SpawnInitialFlora(host, config, runtime, gameData));
            Track(host, SpawnInitialFauna(host, config, runtime, gameData));
        }

        // ------------------------------- FLORA -------------------------------

        IEnumerator SpawnInitialFlora(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            var spawnProfile = config.SpawnProfile;
            if (!spawnProfile) yield break;
            if (spawnProfile.SupportedFloras is not { Count: > 0 })
                yield break;

            var excluded = GetExcludedDomain(spawnProfile.FloraExcludeLocalDomain, gameData, fallbackLocal: Domains.Jade);

            // Per user spec: flora begin spawning with random domains at count=0. The
            // per-flora SpawnProbability is applied as a per-attempt roll inside the loop
            // (not as a once-at-startup gate), so all configured flora types get a fair
            // shot every cycle instead of being permanently disabled by a bad initial roll.
            foreach (var floraCfg in spawnProfile.SupportedFloras)
            {
                if (!floraCfg || !floraCfg.FloraPrefab) continue;
                Track(host, SpawnFloraTypeLoop(host, spawnProfile, floraCfg, excluded));
            }
        }

        IEnumerator SpawnFloraTypeLoop(
            Cell host,
            SpawnProfileSO spawnProfile,
            FloraConfigurationSO floraCfg,
            Domains? excluded)
        {
            // Initial batch (only if gate allows).
            int initialCount = Mathf.Max(0, floraCfg.InitialSpawnCount);
            float initialInterval = Mathf.Max(0f, spawnProfile.FloraSpawnIntervalSeconds);

            for (int i = 0; i < initialCount; i++)
            {
                if (host && host.FloraPlantingEnabled && AllowSpawn(floraCfg.SpawnProbability))
                    SpawnFlora(host, floraCfg.FloraPrefab, excluded);

                if (initialInterval > 0f && i < initialCount - 1)
                    yield return new WaitForSeconds(initialInterval);
            }

            // Continuous spawn for this flora type, gated on FloraPlantingEnabled.
            while (true)
            {
                float waitPeriod = floraCfg.OverrideDefaultPlantPeriod
                    ? Mathf.Max(0f, (float)floraCfg.NewPlantPeriod)
                    : floraCfg.FloraPrefab.PlantPeriod;

                if (waitPeriod > 0f)
                    yield return new WaitForSeconds(waitPeriod);
                else
                    yield return null;

                if (!host) yield break;

                // Gate: stop planting new flora when the cell has hit its plant-end threshold.
                if (!host.FloraPlantingEnabled) continue;

                // Per-attempt probability, not once-at-startup.
                if (!AllowSpawn(floraCfg.SpawnProbability)) continue;

                SpawnFlora(host, floraCfg.FloraPrefab, excluded);
            }
        }

        // ------------------------------- FAUNA -------------------------------

        IEnumerator SpawnInitialFauna(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            var spawnProfile = config.SpawnProfile;
            if (!spawnProfile) yield break;
            if (spawnProfile.SupportedFaunas is not { Count: > 0 })
            {
                // Fail loud: empty SupportedFaunas is almost always a data-wiring mistake.
                // Without this warning fauna silently never spawn (as happened in Menu_Main).
                CSDebug.LogWarning($"[IntensityWiseLifeSpawner] '{config.name}' SpawnProfile has no SupportedFaunas wired; fauna will never spawn in this cell.");
                yield break;
            }

            foreach (var faunaCfg in spawnProfile.SupportedFaunas)
            {
                if (!faunaCfg)
                {
                    CSDebug.LogWarning($"[IntensityWiseLifeSpawner] '{config.name}' has a null FaunaConfigurationSO entry in SupportedFaunas.");
                    continue;
                }
                if (!faunaCfg.FaunaPrefab)
                {
                    CSDebug.LogWarning($"[IntensityWiseLifeSpawner] FaunaConfiguration '{faunaCfg.name}' has no FaunaPrefab; skipping.");
                    continue;
                }
                if (faunaCfg.SpawnProbability <= 0f)
                {
                    CSDebug.LogWarning($"[IntensityWiseLifeSpawner] FaunaConfiguration '{faunaCfg.name}' has SpawnProbability <= 0; fauna of this type will never roll true. Set to 1 to always spawn.");
                    continue;
                }
                Track(host, SpawnFaunaTypeLoop(host, runtime, spawnProfile, faunaCfg));
            }
        }

        IEnumerator SpawnFaunaTypeLoop(
            Cell host,
            CellRuntimeDataSO runtime,
            SpawnProfileSO spawnProfile,
            FaunaConfigurationSO faunaCfg)
        {
            int initialCount = Mathf.Max(0, faunaCfg.InitialSpawnCount);
            float initialInterval = Mathf.Max(0f, spawnProfile.FaunaSpawnIntervalSeconds);

            // Initial batch — only fires while the fauna-spawn gate is open.
            for (int i = 0; i < initialCount; i++)
            {
                if (host && host.FaunaSpawningEnabled && AllowSpawn(faunaCfg.SpawnProbability))
                    TrySpawnFauna(host, runtime, faunaCfg);

                if (initialInterval > 0f && i < initialCount - 1)
                    yield return new WaitForSeconds(initialInterval);
            }

            // Continuous spawn loop, gated on FaunaSpawningEnabled.
            while (true)
            {
                float wait = Mathf.Max(0.05f, spawnProfile.BaseFaunaSpawnTime);
                wait = ScaleFaunaInterval(host, wait);
                yield return new WaitForSeconds(wait);

                if (!host) yield break;

                // Gate: no new fauna until the cell has crossed the spawn-start threshold.
                if (!host.FaunaSpawningEnabled) continue;

                if (!AllowSpawn(faunaCfg.SpawnProbability)) continue;

                TrySpawnFauna(host, runtime, faunaCfg);
            }
        }

        void TrySpawnFauna(Cell host, CellRuntimeDataSO runtime, FaunaConfigurationSO faunaCfg)
        {
            // Prefer the crystal as the initial goal, but fall back to the cell's own
            // position. The previous implementation silently skipped spawning when no
            // crystal existed, which is exactly why Menu_Main never saw fauna appear.
            Vector3 goal = TryGetCrystalGoal(runtime, out var crystalGoal)
                ? crystalGoal
                : host.transform.position;

            // Per user spec: fauna spawn in the cell's controlling color, not a random one.
            Domains color = host.ControllingDomain;

            SpawnFaunaWithDomain(host, faunaCfg.FaunaPrefab, goal, color);
        }
    }
}
