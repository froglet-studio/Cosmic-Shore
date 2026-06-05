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

            // No domain asymmetry: all three playable domains seed flora (excluded = null).
            // Which domain ends up controlling a cell emerges from consumption (controlling-
            // color fauna grazing the minorities) and from players laying their own domain's
            // prisms — not from biasing which colors are allowed to seed. (Docs/ECOSYSTEM.md §5.)
            Domains? excluded = null;

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
            // fixed-size population in the cell's controlling color, but only while there
            // is prey to eat: production pauses when opposing prism mass is below
            // FaunaFoodFloor, and starving fauna wither — so the population self-bounds
            // to prey. (Docs/ECOSYSTEM.md §6, option C: prey-linked.)
            float period = Mathf.Max(0.05f, spawnProfile.BaseFaunaSpawnTime);

            while (true)
            {
                if (!host) yield break;

                // No asymmetry between domains: fauna spawn in ONE color, the cell's
                // controlling color, and hunt opposing mass. Production pauses when prey is
                // scarce; starving fauna wither (see LightFauna) — the food web, not a per-
                // domain spawn heuristic, regulates the cell. (Docs/ECOSYSTEM.md §5–6.)
                Domains color = host.ControllingDomain;
                if (host.OpposingVolume(color) >= spawnProfile.FaunaFoodFloor)
                    SpawnFaunaPopulation(host, runtime, faunaCfg, color);

                // Reset the spawn-cycle ring each period whether or not prey allowed a
                // burst — the ring reflects the fixed timer cadence, not the food gate.
                host.RecordFaunaSpawn();

                yield return new WaitForSeconds(period);
            }
        }

        /// <summary>
        /// Spawns one fixed-size population in <paramref name="color"/> — the cell's
        /// controlling domain. Spawning in the controller's color fixes "no Jade fauna
        /// when Jade controls" and lets the dominant color's fauna hunt the minority.
        /// Seeks the crystal when present, the cell centre otherwise; each member adds
        /// its own orbit offset (Fauna) so the swarm spreads instead of stacking.
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