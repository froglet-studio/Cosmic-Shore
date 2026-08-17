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
        // Lower = faster cadence under stress so reinforcements arrive when needed.
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

            // No excluded-domain roll - the locked no-domain-asymmetry invariant: all three
            // domains seed flora uniformly. Passing null keeps legacy SpawnProfile assets with
            // the old serialized FloraExcludeLocalDomain=true inert (same as RandomLifeSpawner).
            Domains? excluded = null;

            // Per user spec: flora begin spawning with random domains at count=0. The
            // SpawnProbability is applied as a per-attempt roll inside SpawnFloraTypeLoop
            // (NOT as a once-at-startup gate that previously killed the loop on a single
            // failed roll). All configured flora types get a fair shot every cycle.
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
            // Cell density scalar: the SpawnProfile is the per-intensity asset, so scaling the
            // seed batch is how one cell config makes a bigger or smaller forest out of the same
            // species assets. Through the CELL, never the profile: every flora producer must resolve its
            // population numbers at one accessor or the density scalar ends up live in one
            // producer and dead in the next (Cell.ResolveFloraPopulation, Docs/ECOSYSTEM.md §32).
            // This used to be an inline copy of the scaling, duplicated verbatim in both spawners.
            int initialCount = host.ResolveFloraPopulation(Mathf.Max(0, floraCfg.InitialSpawnCount));
            float initialInterval = Mathf.Max(0f, spawnProfile.FloraSpawnIntervalSeconds);

            // Initial batch - gated on FloraPlantingEnabled and per-attempt probability.
            for (int i = 0; i < initialCount; i++)
            {
                if (host && host.FloraPlantingEnabled && !host.IsFloraAtCap(floraCfg)
                    && AllowSpawn(floraCfg.SpawnProbability))
                    SpawnFlora(host, floraCfg.FloraPrefab, excluded, floraCfg);

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

            // Continuous spawn - the loop keeps ticking so spawning resumes if the
            // cell falls back across the planting hysteresis floor.
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
                if (!host.FloraPlantingEnabled) continue;
                if (!AllowSpawn(floraCfg.SpawnProbability)) continue;

                // SEEDER, not population driver - the same rule RandomLifeSpawner follows, and
                // it lives on the shared base precisely so it cannot be live in one spawner and
                // dead in the other (CellLifeSpawnerBase.FloraSeedDeficit, Docs/ECOSYSTEM.md §32).
                int toPlant = FloraSeedDeficit(host, floraCfg);
                for (int i = 0; i < toPlant; i++)
                {
                    if (!host || !host.FloraPlantingEnabled) break;
                    SpawnFlora(host, floraCfg.FloraPrefab, excluded, floraCfg);
                    if (i + 1 < toPlant) yield return null;
                }
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
                // Without this warning fauna silently never spawn (as happened in Menu_Main
                // with the Blob Cell SpawnProfile).
                CSDebug.LogWarning($"[IntensityWiseLifeSpawner] '{config.name}' SpawnProfile has no SupportedFaunas wired; cell-spawned fauna will never appear in this biome (LightFaunaManager populations are unaffected).");
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
            // Cell density scalar, the fauna twin of the flora one above: the SpawnProfile is the
            // per-intensity asset, so scaling the seed batch here is how one cell config carries
            // more wildlife than another out of the same species assets. Asked of the CELL, which
            // owns the profile - see Cell.ResolveFaunaPopulation for why every producer must.
            int initialCount = Mathf.Max(0, host.ResolveFaunaPopulation(faunaCfg.InitialSpawnCount));
            float initialInterval = Mathf.Max(0f, spawnProfile.FaunaSpawnIntervalSeconds);

            // Initial batch - gated on FaunaSpawningEnabled (cell holds mass) +
            // per-attempt probability.
            for (int i = 0; i < initialCount; i++)
            {
                if (host && host.FaunaSpawningEnabled && AllowSpawn(faunaCfg.SpawnProbability))
                    TrySpawnFauna(host, runtime, faunaCfg);

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

            // Continuous spawn - interval scales with aggression so reinforcements
            // arrive faster when the cell is under stress. Seed the spawn-cycle
            // telemetry before the first wait so the indicator ring starts at 0%
            // instead of stuck-at-100% (the "never spawned" sentinel value).
            host.RecordFaunaSpawn();
            while (true)
            {
                float wait = Mathf.Max(0.05f, spawnProfile.BaseFaunaSpawnTime);
                wait = ScaleFaunaInterval(host, wait);
                yield return new WaitForSeconds(wait);

                if (!host) yield break;
                if (!host.FaunaSpawningEnabled) continue;
                if (!AllowSpawn(faunaCfg.SpawnProbability)) continue;

                TrySpawnFauna(host, runtime, faunaCfg);
                host.RecordFaunaSpawn();
            }
        }

        void TrySpawnFauna(Cell host, CellRuntimeDataSO runtime, FaunaConfigurationSO faunaCfg)
        {
            // Staged release, same rule the RandomLifeSpawner enforces: a species seeds only
            // while its ReleaseTier is at or below the cell's. Gated HERE, at the single spawn
            // funnel this class has, so both the initial batch and the continuous loop obey it.
            // The gate belongs to the CELL, so which spawner a biome happens to use must never
            // decide whether a mode's seal holds. Defaults (config tier 0, cell int.MaxValue via
            // SpawnProfileSO.InitialFaunaReleaseTier) leave every shipped biome unchanged.
            if (faunaCfg.ReleaseTier > host.FaunaReleaseTier) return;

            // Honour the performance backstop. This loop spawns ONE creature per tick forever,
            // so without a cap a long match walks a species past MaxLivePopulation - the number
            // the collider budget was sized against - even though reproduction
            // (Fauna.TryReproduce) respects it. The other spawner has always capped here
            // (FaunaReproductionRules.SeedSpawnCount); 0 still means uncapped. The CELL resolves
            // it so the cap moves with the profile's FaunaPopulationScale (Cell.IsFaunaAtCap).
            if (host.IsFaunaAtCap(faunaCfg)) return;

            // Prefer the crystal as the initial goal, but fall back to the cell's own
            // position. The previous implementation silently skipped spawning when no
            // crystal existed, which contributed to fauna never appearing in cells
            // whose crystal hadn't spawned yet.
            //
            // NOTE both of those fallbacks are AT OR NEAR THE CELL CENTRE, and this call used
            // to pass no spawn POSITION at all - so SpawnFaunaWithDomain defaulted to the cell
            // centre too. Every creature in the biome was therefore born at the centre and
            // immediately swam to the crystal, which is fine for an ordinary cell and disastrous
            // for a mode whose whole arena is concentric rooms. SpawnFaunaBanded gives a banded
            // species its own point in its own room instead; an unbanded species still gets
            // exactly this goal and the legacy centre spawn.
            Vector3 goal = TryGetCrystalGoal(runtime, out var crystalGoal)
                ? crystalGoal
                : host.transform.position;

            // Per user spec: fauna spawn in the cell's controlling color, not random.
            Domains color = host.ControllingDomain;

            // Lineage-bind (inside SpawnFaunaBanded) so the species counts toward the cell's
            // live population and can reproduce if its config authors FeedsPerOffspring > 0.
            SpawnFaunaBanded(host, faunaCfg, color, goal);
        }
    }
}
