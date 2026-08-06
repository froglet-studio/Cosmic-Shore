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

            // Seed into a world that EXISTS. A config with an authored environment claims its
            // build immediately but defers it past scene boot (Cell.DeferredEnvironmentBuild),
            // so without this the whole initial planting batch disperses over empty space
            // seconds before the world - and its prepared beds - arrive underneath it. Bounded,
            // so a build that never lands can't mute the cell's flora forever.
            float deadline = Time.time + 25f;
            while (host && host.IsEnvironmentBuildPending && Time.time < deadline)
                yield return null;

            if (spawnProfile.FloraInitialDelaySeconds > 0f)
                yield return new WaitForSeconds(spawnProfile.FloraInitialDelaySeconds);

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
                    PlantOne(host, floraCfg, excluded);

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
                if (host.FloraPlantingEnabled)
                    PlantOne(host, floraCfg, excluded);
            }
        }

        /// <summary>
        /// Plant one flora of this species. When the cell's authored environment prepared ground
        /// (a garden's beds, trellis feet and hanging baskets - <see cref="FloraPlantingSite"/>),
        /// the plant roots THERE, oriented to the bed; otherwise it disperses itself across the
        /// membrane shell exactly as before. Same spawn path either way - a garden gets no
        /// privileged spawner, only better-chosen ground.
        /// </summary>
        static void PlantOne(Cell host, FloraConfigurationSO floraCfg, Domains? excluded)
        {
            if (host.TryTakePlantingSite(floraCfg.PreferredSites, out var pos, out var up))
                SpawnFlora(host, floraCfg.FloraPrefab, excluded, floraCfg, pos, up);
            else
                SpawnFlora(host, floraCfg.FloraPrefab, excluded, floraCfg);
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

            // Wave number on the fixed spawn clock - the herbivore spawn-point ring
            // rotates on THIS, not on how many waves happened to hatch (see
            // HerbivoreSpawnPoint). Every species loop is started in the same frame by
            // StartFaunaLoops and shares InitialFaunaSpawnWaitTime + period, so all
            // herbivore species agree on the wave number and land on the same point,
            // and the point steps once per period.
            int wave = 0;

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

                // Solitary predators: while the predator spawn ring is active, at most
                // ONE predator hatches per spawn interval (successive spawns alternate
                // ring points, e.g. the two poles).
                if (isPredator && spawnProfile.PredatorSpawnPointCount > 0 && spawnProfile.PredatorSpawnRadius > 0f)
                    toSpawn = Mathf.Min(toSpawn, 1);

                bool preyAvailable = FaunaReproductionRules.PreyAvailable(
                    isPredator, host.GetLiveHerbivoreCount(), host.OpposingVolume(color), spawnProfile.FaunaFoodFloor);

                // Staged release: a mode may hold a species closed until its own scored
                // signal opens it (Ribcage releases the brood at 25% of the cage, the
                // predator at 50%). Default tiers - config 0, cell int.MaxValue - leave
                // every shipped biome released from the first tick.
                bool released = faunaCfg.ReleaseTier <= host.FaunaReleaseTier;

                int spawned = 0;
                if (toSpawn > 0 && preyAvailable && released)
                {
                    // Spread across frames - a densely-stocked biome seeds tens of prism-bodied
                    // creatures on one tick (Ribcage hatches 85 across four species loops that all
                    // start in the same frame), and instantiating them together is the same frame
                    // spike the flora batch above already yields to avoid.
                    yield return SpawnFaunaPopulation(host, runtime, spawnProfile, faunaCfg, color, toSpawn, wave);
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
                wave++;
            }
        }

        /// <summary>
        /// Spawns <paramref name="count"/> fauna in <paramref name="color"/> - the cell's
        /// controlling domain. Spawning in the controller's color fixes "no Jade fauna
        /// when Jade controls" and lets the dominant color's fauna hunt the minority.
        /// Each spawn is lineage-bound to its species config so it counts toward the
        /// per-cell population and can reproduce. Predators spawn on the densest mass
        /// concentration (crystal/cell anchor when empty); herbivores land on the point
        /// this <paramref name="wave"/> of the spawn clock owns on the profile's ring
        /// when one is configured (see <see cref="HerbivoreSpawnPoint"/>).
        /// </summary>
        // Jitter radius around the mass concentration when spawning a population, so the
        // swarm spreads over the buildup instead of stacking on one point.
        const float FaunaSpawnJitter = 150f;

        // Rotates predator waves around the predator ring. Instance state — one spawner
        // per cell — so successive predators alternate poles. The HERBIVORE ring is not
        // an index: it rides the wave clock (see HerbivoreSpawnPoint).
        int _predatorSpawnPointIndex;

        /// <summary>Creatures instantiated per frame while seeding a population (see the caller).</summary>
        const int FaunaSpawnBatchPerFrame = 6;

        IEnumerator SpawnFaunaPopulation(Cell host, CellRuntimeDataSO runtime, SpawnProfileSO spawnProfile,
            FaunaConfigurationSO faunaCfg, Domains color, int count, int wave)
        {
            bool isPredator = faunaCfg.FaunaPrefab && faunaCfg.FaunaPrefab.Diet == FaunaDiet.Predator;
            bool useHerbivoreRing = !isPredator &&
                spawnProfile.HerbivoreSpawnPointCount > 1 && spawnProfile.HerbivoreSpawnRadius > 0f;
            bool usePredatorRing = isPredator &&
                spawnProfile.PredatorSpawnPointCount > 0 && spawnProfile.PredatorSpawnRadius > 0f;

            // Ring mode: each wave takes the next point on its diet's ring — herbivores
            // get their own feeding ground away from where the last group (and any
            // predator drawn to it) already is; predators enter from the poles,
            // orthogonal to the herbivore ring. Legacy mode: spawn right ON the densest
            // mass concentration so they start clearing immediately
            // (GetDensestRegionAnyDomain falls back to the crystal/cell anchor when
            // there's no mass yet).
            Vector3 goal = useHerbivoreRing ? HerbivoreSpawnPoint(host, spawnProfile, wave)
                : usePredatorRing ? NextPredatorSpawnPoint(host, spawnProfile)
                : host.GetDensestRegionAnyDomain();

            // A BANDED species (FaunaConfigurationSO.BandInner/OuterRadius) hatches inside its
            // own band. Without this the creature is born on the cell's ordinary spawn ring and
            // only its GOAL is banded, so a whole wave spends its first seconds swimming home
            // through mass it is not allowed to eat - and a species banded to the core would
            // hatch outside the cage it is supposed to be locked in, which is the one thing the
            // pen exists to prevent. Unbanded species (every shipped biome) are untouched.
            goal = BandGoal(host, faunaCfg, goal);

            for (int i = 0; i < count; i++)
            {
                if (!host) yield break;   // cell torn down mid-seed (scene change)

                Vector3 spawnPos = goal + UnityEngine.Random.insideUnitSphere * FaunaSpawnJitter;
                spawnPos = BandGoal(host, faunaCfg, spawnPos);   // jitter must not escape the band
                var fauna = SpawnFaunaWithDomain(host, faunaCfg.FaunaPrefab, goal, color, spawnPos);
                if (fauna) fauna.AssignLineage(host, faunaCfg);

                if (i + 1 < count && (i + 1) % FaunaSpawnBatchPerFrame == 0)
                    yield return null;
            }
        }

        /// <summary>
        /// Projects a point into a species' band, and picks a fresh point on the band's mid-shell
        /// when the input sits at the cell centre (which is what <c>GetDensestRegionAnyDomain</c>
        /// returns for an empty grid - projecting THAT would stack a whole wave on one radial).
        /// Returns the point unchanged for an unbanded species, so the common path is one compare.
        /// </summary>
        static Vector3 BandGoal(Cell host, FaunaConfigurationSO cfg, Vector3 point)
        {
            if (!host || !cfg || cfg.BandOuterRadius <= 0f) return point;

            float inner = Mathf.Min(cfg.BandInnerRadius, cfg.BandOuterRadius);
            float outer = Mathf.Max(cfg.BandInnerRadius, cfg.BandOuterRadius);

            Vector3 centre = host.transform.position;
            Vector3 offset = point - centre;
            float d = offset.magnitude;

            // The cell CENTRE is the "no mass sensed yet" fallback from
            // GetDensestRegionAnyDomain, not a real destination. Treat it as needing a fresh
            // point even when it is technically inside the band (which it always is for the
            // core room, whose inner radius is 0) - otherwise a whole core wave stacks on one
            // point at the origin.
            if (d < 1f)
                return centre + UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(inner, outer);

            Vector3 dir = offset / d;
            if (d >= inner && d <= outer) return point;

            // Land anywhere in the shell rather than pinned to whichever wall was nearest -
            // a wave that all lands on the outer wall reads as a ring, not a population.
            return centre + dir * UnityEngine.Random.Range(inner, outer);
        }

        /// <summary>
        /// The herbivore feeding ground for a given WAVE of the fixed spawn clock: point
        /// <c>wave % HerbivoreSpawnPointCount</c> on the equatorial ring, so successive
        /// waves walk the ring in succession — N points × BaseFaunaSpawnTime covers the
        /// whole ring, then repeats (Blob: 3 points at 15s ⇒ 45s a lap).
        ///
        /// Keying on the wave rather than on a spawn COUNTER is the fix for "every
        /// herbivore hatches at the same point": the old index only advanced when a wave
        /// actually hatched, and the seeder only hatches while a species is under its
        /// floor. Reproduction holds a fed population at its cap, so after the bootstrap
        /// wave the counter could sit on one point for the whole session — pinning every
        /// later seed, and the crystals its creatures eventually drop, to one patch of
        /// the cell. The wave clock advances whether or not the food web called for a
        /// seed, so the next hatch is always somewhere new.
        /// </summary>
        static Vector3 HerbivoreSpawnPoint(Cell host, SpawnProfileSO spawnProfile, int wave)
        {
            int pointCount = spawnProfile.HerbivoreSpawnPointCount;
            float angle = (wave % pointCount) * (Mathf.PI * 2f / pointCount);
            return host.transform.position
                   + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnProfile.HerbivoreSpawnRadius;
        }

        // Predator ring: a VERTICAL circle (X-Y plane) starting at +Y, orthogonal to the
        // equatorial (XZ) herbivore ring — with 2 points the spawns sit exactly on the
        // poles, alternating each wave.
        Vector3 NextPredatorSpawnPoint(Cell host, SpawnProfileSO spawnProfile)
        {
            int pointCount = spawnProfile.PredatorSpawnPointCount;
            float angle = (_predatorSpawnPointIndex++ % pointCount) * (Mathf.PI * 2f / pointCount);
            return host.transform.position
                   + new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * spawnProfile.PredatorSpawnRadius;
        }
    }
}