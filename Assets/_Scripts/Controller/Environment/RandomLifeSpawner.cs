using System.Collections;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
            // Authority gate (Docs/ECOSYSTEM_NETWORK_SYNC.md, flora): planting decisions
            // are server-only — clients reconstruct plants from FloraNetworkSync slots.
            // (Authority only flips with a scene reload, so a loop-start gate suffices;
            // the per-plant sites below re-check for belt and braces.)
            if (!FaunaNetworkSync.IsSimAuthority) yield break;

            var spawnProfile = config.SpawnProfile;
            if (!spawnProfile) yield break;
            if (spawnProfile.SupportedFloras is not { Count: > 0 })
                yield break;

            // No excluded-domain roll - the locked no-domain-asymmetry invariant: all three
            // domains seed flora uniformly (the exclusion kept the local player's color from
            // ever growing canopy). Passing null keeps legacy SpawnProfile assets with the old
            // serialized FloraExcludeLocalDomain=true inert. Fauna were already controlling-
            // color only (see StartFaunaLoops below).
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
            // there is no excluded-domain roll here - that exclusion is exactly what kept
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
                // Frenzy gate: plant at a steady rate until the cell hits Frenzy
                // (Phase < Frenzy). No early planting cap - flora keep planting + growing
                // and the food web (fauna grazing) is the only down-force. Replaces the
                // old scored-volume ceiling (~0 in Menu_Main, so it never bounded planting).
                if (host && host.FloraPlantingEnabled)
                    SpawnFlora(host, floraCfg.FloraPrefab, excluded);

                // Spread instantiation across frames. WaitForSeconds when an interval
                // is configured; otherwise yield a single frame so a large InitialSpawnCount
                // doesn't instantiate every (prism-bodied) life form in one frame - that
                // showed up as a ~48% frame spike in Cell.SpawnFaunaTypeLoop_Random.
                if (i < initialCount - 1)
                {
                    if (initialInterval > 0f) yield return new WaitForSeconds(initialInterval);
                    else yield return null;
                }
            }

            // Continuous - keeps ticking so planting resumes if the cell falls back
            // across the Frenzy hysteresis floor (Phase drops below Frenzy again).
            while (true)
            {
                float waitPeriod = floraCfg.OverrideDefaultPlantPeriod
                    ? Mathf.Max(0f, (float)floraCfg.NewPlantPeriod)
                    : floraCfg.FloraPrefab.PlantPeriod;

                if (waitPeriod > 0f) yield return new WaitForSeconds(waitPeriod);
                else yield return null;

                if (!host) yield break;
                if (!FaunaNetworkSync.IsSimAuthority || SpawnsSuppressed) continue;
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

            // SEEDER, not population driver: each fixed period the loop only tops the
            // species back up to its seed floor (PopulationSize) - bootstrap on scene
            // start, and recovery after a starvation/predation crash so extinction is
            // never permanent. Above the floor the population is driven by REPRODUCTION
            // (well-fed fauna birth offspring, Fauna.TryReproduce) and bounded by
            // starvation - the food web, not this timer. Production still pauses when
            // opposing prism mass is below FaunaFoodFloor (no prey ⇒ no seeding either).
            // (Docs/ECOSYSTEM.md §6: prey-linked production + starvation + reproduction.)
            float period = Mathf.Max(0.05f, spawnProfile.BaseFaunaSpawnTime);

            // Prey signal by diet: herbivore species seed on prism prey - opposing
            // ENVIRONMENT volume, per "volume is the spine" (fauna bodies excluded:
            // not edible, so they must not read as food) - predator species on the
            // LIVE HERBIVORE count (creatures are eaten per-individual, so count is
            // the honest unit there). FaunaFoodFloor doubles as both floors: it is
            // authored in nominal prisms, converted ×NominalPrismVolume for the
            // herbivore volume check, and read directly as N herbivores for a
            // predator. (Docs/ECOSYSTEM.md §6-§7.)
            bool isPredator = faunaCfg.FaunaPrefab && faunaCfg.FaunaPrefab.Diet == FaunaDiet.Predator;

            while (true)
            {
                if (!host) yield break;

                Domains color = host.ControllingDomain;

                // Seeder (default): top the species back up to its seed floor.
                // Full-wave (SeedFullWaveEveryTick): every tick births a fresh wave of
                // PopulationSize in the controlling color, clamped by the hard cap -
                // wave-scored modes (Brood Rush) ride this so each 30s cycle visibly
                // hatches a brood. Population stays starvation-bounded either way.
                int toSpawn = spawnProfile.SeedFullWaveEveryTick
                    ? FaunaReproductionRules.WaveSpawnCount(
                        host.GetLiveFaunaCount(faunaCfg),
                        Mathf.Max(1, faunaCfg.PopulationSize),
                        faunaCfg.MaxLivePopulation)
                    : FaunaReproductionRules.SeedSpawnCount(
                        host.GetLiveFaunaCount(faunaCfg),
                        Mathf.Max(1, faunaCfg.PopulationSize),
                        faunaCfg.MaxLivePopulation);

                // Authority gate (Docs/ECOSYSTEM_NETWORK_SYNC.md): only the server
                // originates fauna - clients receive replicated spawns instead. toSpawn
                // is zeroed rather than breaking the loop so the fixed-period wave tick
                // + spawn-ring telemetry below keep running on every peer. Launch
                // teardown (SpawnsSuppressed) likewise stops late births so a spawn
                // message can't batch into the scene-load tick.
                if (!FaunaNetworkSync.IsSimAuthority || SpawnsSuppressed) toSpawn = 0;

                bool preyAvailable = FaunaReproductionRules.PreyAvailable(
                    isPredator, host.GetLiveHerbivoreCount(), host.OpposingVolume(color), spawnProfile.FaunaFoodFloor);

                int spawned = 0;
                if (toSpawn > 0 && preyAvailable)
                {
                    SpawnFaunaPopulation(host, runtime, faunaCfg, color, toSpawn);
                    spawned = toSpawn;
                }

                // Reset the spawn-cycle ring each period whether or not seeding happened -
                // the ring reflects the fixed timer cadence, not the food/deficit gates.
                host.RecordFaunaSpawn();

                // Publish the wave tick: domain + whether that domain is a genuine
                // nucleus claim (node control). Scoring systems (Brood Rush) listen on
                // the runtime SO's channel; one event per species loop per period, so
                // wave-scored modes author exactly ONE fauna species in their profile.
                bool nucleusControlled = host.TryGetNucleusClaim(out var claimant) && claimant == color;
                runtime.OnFaunaWaveSpawned.Raise(
                    new FaunaWaveData(host.ID, color, spawned, nucleusControlled));

                yield return new WaitForSeconds(period);
            }
        }

        /// <summary>
        /// Spawns <paramref name="count"/> fauna in <paramref name="color"/> - the cell's
        /// controlling domain. Spawning in the controller's color fixes "no Jade fauna
        /// when Jade controls" and lets the dominant color's fauna hunt the minority.
        /// Each spawn is lineage-bound to its species config so it counts toward the
        /// per-cell population and can reproduce. Seeks the densest mass concentration
        /// when present, the crystal/cell anchor otherwise.
        /// </summary>
        // Jitter radius around the mass concentration when spawning a population, so the
        // swarm spreads over the buildup instead of stacking on one point.
        const float FaunaSpawnJitter = 150f;

        void SpawnFaunaPopulation(Cell host, CellRuntimeDataSO runtime, FaunaConfigurationSO faunaCfg, Domains color, int count)
        {
            // Spawn new fauna right ON the prioritized mass concentration (the densest region
            // the cell senses) so they appear on the buildup they'll forage, not at the
            // distant cell centre - they start clearing immediately. GetDensestRegionAnyDomain
            // falls back to the crystal/cell anchor when there's no mass yet.
            Vector3 goal = host.GetDensestRegionAnyDomain();

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = goal + UnityEngine.Random.insideUnitSphere * FaunaSpawnJitter;
                var fauna = SpawnFaunaWithDomain(host, faunaCfg.FaunaPrefab, goal, color, spawnPos);
                if (fauna) fauna.AssignLineage(host, faunaCfg);
            }
        }
    }
}