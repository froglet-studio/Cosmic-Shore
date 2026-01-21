using System.Collections;
using System.Collections.Generic;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class RandomLifeSpawner : ICellLifeSpawner
    {
        readonly List<Coroutine> running = new();

        public void Start(Cell host, SO_CellType cellType, CellDataSO cellData, GameDataSO gameData)
        {
            Stop(host);
            if (!host || !cellType) return;

            var profile = host.RandomSpawnProfile;
            if (!profile)
            {
                Debug.LogWarning($"{nameof(Cell)} {host.name}: Random mode selected but no RandomSpawnProfile assigned.");
                return;
            }

            if (cellType.SupportedFlora is { Count: > 0 })
            {
                for (int i = 0; i < profile.FloraTypeCount; i++)
                {
                    var floraConfig = PickWeighted(cellType.SupportedFlora, f => f.SpawnProbability);
                    if (!floraConfig?.Flora) continue;
                    running.Add(host.StartCoroutine(SpawnFloraLoop(host, floraConfig, profile, gameData)));
                }
            }

            if (cellType.SupportedFauna != null && cellType.SupportedFauna.Count > 0)
            {
                for (int i = 0; i < profile.FaunaTypeCount; i++)
                {
                    var picked = PickWeighted(cellType.SupportedFauna, f => f.SpawnProbability);
                    if (picked?.Population == null) continue;
                    running.Add(host.StartCoroutine(SpawnPopulationLoop(host, picked.Population, profile, cellData, gameData)));
                }
            }
        }

        public void Stop(Cell host)
        {
            if (!host) return;

            foreach (var t in running)
            {
                if (t != null)
                    host.StopCoroutine(t);
            }
            running.Clear();
        }

        IEnumerator SpawnFloraLoop(Cell host, FloraConfiguration floraConfiguration, CellRandomSpawnProfileSO profile, GameDataSO gameData)
        {
            // initial batch
            for (int i = 0; i < floraConfiguration.initialSpawnCount - 1; i++)
            {
                var newFlora = Object.Instantiate(floraConfiguration.Flora, host.transform.position, Quaternion.identity);
                newFlora.domain = profile.SpawnJade ? (Domains)Random.Range(1, 5) : (Domains)Random.Range(2, 5);
                newFlora.Initialize(host);
            }

            while (true)
            {
                var controllingVolume = gameData.GetControllingTeamStatsBasedOnVolumeRemaining().Item2;

                if (controllingVolume < profile.FloraSpawnVolumeCeiling)
                {
                    var newFlora = Object.Instantiate(floraConfiguration.Flora, host.transform.position, Quaternion.identity);
                    newFlora.domain = profile.SpawnJade ? (Domains)Random.Range(1, 5) : (Domains)Random.Range(2, 5);
                    newFlora.Initialize(host);
                }

                float waitPeriod = floraConfiguration.OverrideDefaultPlantPeriod
                    ? floraConfiguration.NewPlantPeriod
                    : floraConfiguration.Flora.PlantPeriod;

                yield return new WaitForSeconds(waitPeriod);
            }
        }

        IEnumerator SpawnPopulationLoop(Cell host, Population population, CellRandomSpawnProfileSO profile, CellDataSO cellData, GameDataSO gameData)
        {
            yield return new WaitForSeconds(profile.InitialFaunaSpawnWaitTime);

            while (true)
            {
                var controllingVolume = gameData.GetControllingTeamStatsBasedOnVolumeRemaining().Item2;

                if (controllingVolume > profile.FaunaSpawnVolumeThreshold)
                {
                    var newPopulation = Object.Instantiate(population, host.transform.position, Quaternion.identity);
                    newPopulation.domain = host.GetHostileDomainToLocalLegacy();
                    newPopulation.Goal = cellData.CrystalTransform.position;
                    yield return new WaitForSeconds(profile.BaseFaunaSpawnTime);
                }
                else
                {
                    yield return new WaitForSeconds(2f);
                }
            }
        }

        static T PickWeighted<T>(IReadOnlyList<T> items, System.Func<T, float> weightSelector)
        {
            if (items == null || items.Count == 0) return default;

            float total = 0f;
            for (int i = 0; i < items.Count; i++)
                total += Mathf.Max(0f, weightSelector(items[i]));

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