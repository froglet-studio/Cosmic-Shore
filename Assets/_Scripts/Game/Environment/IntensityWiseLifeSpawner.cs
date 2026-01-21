using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public  class IntensityWiseLifeSpawner : ICellLifeSpawner
    {
        Coroutine floraRoutine;
        Coroutine faunaRoutine;

        public void Start(Cell host, SO_CellType cellType, CellDataSO cellData, GameDataSO gameData)
        {
            Stop(host);
            if (!host || !cellType) return;

            var cfg = cellType.IntensityLifeFormConfiguration;

            floraRoutine = host.StartCoroutine(SpawnInitialFlora(host, cellType, cellData, cfg));
            faunaRoutine = host.StartCoroutine(SpawnInitialPopulations(host, cellData, gameData, cellType, cfg));
        }

        public void Stop(Cell host)
        {
            if (!host) return;

            if (floraRoutine != null) { host.StopCoroutine(floraRoutine); floraRoutine = null; }

            if (faunaRoutine == null) return;
            host.StopCoroutine(faunaRoutine); faunaRoutine = null;
        }

        IEnumerator SpawnInitialFlora(Cell host, SO_CellType cellType, CellDataSO cellData, SO_CellType.LifeFormConfiguration lifeFormConfiguration)
        {
            if (lifeFormConfiguration.FloraInitialDelaySeconds > 0f)
                yield return new WaitForSeconds(lifeFormConfiguration.FloraInitialDelaySeconds);

            if (cellType.SupportedFlora == null || cellType.SupportedFlora.Count == 0)
                yield break;

            var floraConfig = PickWeighted(cellType.SupportedFlora, f => f.SpawnProbability);
            if (floraConfig == null || !floraConfig.Flora)
                yield break;

            int count = Mathf.Max(0, floraConfig.initialSpawnCount);

            var allowedMask = lifeFormConfiguration.AllowedFloraDomains;
            if (!lifeFormConfiguration.SpawnJade)
                allowedMask &= ~SO_CellType.DomainMask.Jade;

            for (int i = 0; i < count; i++)
            {
                var newFlora = Object.Instantiate(floraConfig.Flora, host.transform.position, Quaternion.identity);
                newFlora.domain = DomainPicker.PickRandom(allowedMask);
                newFlora.Initialize(host);

                if (lifeFormConfiguration.FloraSpawnIntervalSeconds > 0f && i < count - 1)
                    yield return new WaitForSeconds(lifeFormConfiguration.FloraSpawnIntervalSeconds);
            }
        }

        IEnumerator SpawnInitialPopulations(Cell host, CellDataSO cellData, GameDataSO gameData, SO_CellType cellType, SO_CellType.LifeFormConfiguration cfg)
        {
            if (cfg.FaunaInitialDelaySeconds > 0f)
                yield return new WaitForSeconds(cfg.FaunaInitialDelaySeconds);

            if (cellType.SupportedFauna == null || cellType.SupportedFauna.Count == 0)
                yield break;

            var local = gameData.LocalRoundStats?.Domain ?? Domains.Jade;
            Domains? excluded = cfg.FaunaExcludeLocalDomain ? local : (Domains?)null;

            foreach (var populationConfiguration in cellType.SupportedFauna)
            {
                if (populationConfiguration == null || !populationConfiguration.Population) continue;

                int populationCount = Mathf.Max(0, populationConfiguration.InitialFloraSpawnCount);

                for (int i = 0; i < populationCount; i++)
                {
                    var pop = Object.Instantiate(populationConfiguration.Population, host.transform.position, Quaternion.identity);

                    pop.domain = DomainPicker.PickRandom(cfg.AllowedFaunaDomains, excluded);
                    pop.Goal = cellData.CrystalTransform.position;

                    if (cfg.FaunaSpawnIntervalSeconds > 0f && i < populationCount - 1)
                        yield return new WaitForSeconds(cfg.FaunaSpawnIntervalSeconds);
                }
            }
        }

        static T PickWeighted<T>(IReadOnlyList<T> items, System.Func<T, float> weightSelector)
        {
            if (items == null || items.Count == 0) return default;

            float total = items.Sum(t => Mathf.Max(0f, weightSelector(t)));

            if (total <= 0f) return items[0];

            float roll = Random.value * total;
            float cumulative = 0f;

            foreach (var t in items)
            {
                cumulative += Mathf.Max(0f, weightSelector(t));
                if (roll <= cumulative) return t;
            }

            return items[^1];
        }
    }
}
