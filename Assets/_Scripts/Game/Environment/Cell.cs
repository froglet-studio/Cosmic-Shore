using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.SOAP;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;


namespace CosmicShore.Game
{
    public class Cell : MonoBehaviour
    {
        enum CellTypeChoiceOptions
        {
            Random,
            IntensityWise
        }
        
        [SerializeField] public int ID;

        [SerializeField] 
        List<SO_CellType> CellTypes;
        
        [SerializeField]
        CellTypeChoiceOptions cellTypeChoiceOptions = CellTypeChoiceOptions.Random;
        
        [SerializeField]
        CellDataSO cellData;
        SO_CellType cellType => cellData.CellType;
        
        GameObject membrane;

        [SerializeField] int FloraTypeCount = 2;
        [SerializeField] bool spawnJade = true;
        [SerializeField] int FaunaTypeCount = 2;

        [SerializeField] float floraSpawnVolumeCeiling = 12000f;

        [SerializeField] float initialFaunaSpawnWaitTime = 10f;
        [SerializeField] float faunaSpawnVolumeThreshold = 1f;
        [SerializeField] float baseFaunaSpawnTime = 60f;

        [SerializeField] bool hasRandomFloraAndFauna;
        
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;

        public Dictionary<Domains, BlockCountDensityGrid> countGrids = new Dictionary<Domains, BlockCountDensityGrid>();
        public Dictionary<Domains, BlockVolumeDensityGrid> volumeGrids = new Dictionary<Domains, BlockVolumeDensityGrid>();

        Dictionary<Domains, float> teamVolumes = new Dictionary<Domains, float>();

        private void OnEnable()
        {
            gameData.OnInitializeGame += Initialize;
            cellData.OnCrystalSpawned.OnRaised += OnCrystalSpawnedInCell;
        }

        private void OnDisable()
        {
            gameData.OnInitializeGame -= Initialize;
            cellData.OnCrystalSpawned.OnRaised -= OnCrystalSpawnedInCell;
            cellData.ResetRuntimeData();
        }

        void Initialize()
        {
            cellData.Cell = this;
            AssignCellType();

            // TODO: handle Blue?
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };  // TODO: Store this as a constant somewhere (where?).
            foreach (Domains t in teams)
            {
                countGrids.Add(t, new BlockCountDensityGrid(t));
            }
            
            normalizeWeights();

            var cellType = cellData.CellType;
            membrane = Instantiate(cellType.MembranePrefab, transform.position, Quaternion.identity);
            Instantiate(cellType.NucleusPrefab, transform.position, Quaternion.identity);
            teamVolumes.Add(Domains.Jade, 0);
            teamVolumes.Add(Domains.Ruby, 0);
            teamVolumes.Add(Domains.Gold, 0);
            teamVolumes.Add(Domains.Blue, 0);
        }
        
        void OnCrystalSpawnedInCell()
        {
            foreach (var modifier in cellType.CellModifiers)
            {
                modifier.Apply(this);
            }
            SpawnLife();
        }

        internal Transform GetCrystalTransform()
        {
            return cellData.CrystalTransform;
        }

        void AssignCellType() 
        {
            if (CellTypes.Count == 0)
            {
                Debug.LogError("No cell types found to assign to cell!");
                return;
            }

            int index = cellTypeChoiceOptions switch
            {
                CellTypeChoiceOptions.Random => Random.Range(0, CellTypes.Count),
                CellTypeChoiceOptions.IntensityWise => Mathf.Clamp(gameData.SelectedIntensity.Value - 1, 0, CellTypes.Count - 1),
                _ => throw new ArgumentOutOfRangeException()
            };
            cellData.CellType = CellTypes[index];

            if (!cellData.CellType)
            {
                Debug.LogError("Cell type is not assigned. Please assign a valid cell type.");
            }
        }

        void SpawnLife()
        {
            var cellType = cellData.CellType;
            
            if (cellType.SupportedFlora.Count > 0)
            {
                for (int i = 0; i < FloraTypeCount; i++)
                {
                    var floraConfiguration = ConfigureFlora();
                    StartCoroutine(SpawnFlora(floraConfiguration, spawnJade));
                }
            }

            if (cellType.SupportedFauna.Count > 0)
            {
                for (int i = 0; i < FaunaTypeCount; i++)
                {
                    var population = ConfigurePopulation();
                    StartCoroutine(SpawnPopulation(population));
                }
            }
        }

        Population ConfigurePopulation()
        {
            var spawnWeight = Random.value;
            var spawnIndex = 0;
            var totalWeight = 0f;
            for (int i = 0; i < cellType.SupportedFauna.Count && totalWeight < spawnWeight; i++)
            {
                spawnIndex = i;
                totalWeight += cellType.SupportedFauna[i].SpawnProbability;
            }

            return cellType.SupportedFauna[spawnIndex].Population;
        }

        FloraConfiguration ConfigureFlora()
        {
            var spawnWeight = Random.value;
            var spawnIndex = 0;
            var totalWeight = 0f;
            for (int i = 0; i < cellType.SupportedFlora.Count && totalWeight < spawnWeight; i++)
            {
                spawnIndex = i;
                totalWeight += cellType.SupportedFlora[i].SpawnProbability;
            }

            return cellType.SupportedFlora[spawnIndex];
        }

        void normalizeWeights()
        {
            float totalWeight = 0;
            foreach (var fauna in cellType.SupportedFauna)
            {
                totalWeight += fauna.SpawnProbability;
            }

            for (int i = 0; i < cellType.SupportedFauna.Count; i++)
                cellType.SupportedFauna[i].SpawnProbability = cellType.SupportedFauna[i].SpawnProbability * (1 / totalWeight);

            totalWeight = 0;
            foreach (var flora in cellType.SupportedFlora)
            {
                totalWeight += flora.SpawnProbability;
            }

            for (int i = 0; i < cellType.SupportedFlora.Count; i++)
                cellType.SupportedFlora[i].SpawnProbability = cellType.SupportedFlora[i].SpawnProbability * (1 / totalWeight);
        }

        public void AddBlock(Prism block)
        {
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (Domains t in teams)
            {
                if (t != block.Domain) countGrids[t].AddBlock(block);
            }
        }

        public void RemoveBlock(Prism block)
        {
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (Domains t in teams)
            {
                if (t != block.Domain) countGrids[t].RemoveBlock(block);
            }
        }

        public Vector3 GetExplosionTarget(Domains domain)
        {
            return countGrids[domain].FindDensestRegion();
        }

        public bool ContainsPosition(Vector3 position)
        {
            if (membrane is null)
                return false;

            return Vector3.Distance(position, transform.position) < membrane.transform.localScale.x; // only works if nodes remain spherical
        }

        public void ChangeVolume(Domains domain, float volume)
        {
            if (!teamVolumes.ContainsKey(domain))
                teamVolumes.Add(domain, 0);

            teamVolumes[domain] += volume;
        }

        public float GetTeamVolume(Domains domain)
        {
            if (!teamVolumes.ContainsKey(domain))
                return 0;

            return teamVolumes[domain];
        }
        
        IEnumerator SpawnFlora(FloraConfiguration floraConfiguration, bool spawnJade = true)
        {
            for (int i = 0; i < floraConfiguration.initialSpawnCount - 1; i++)
            {
                var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                newFlora.domain = spawnJade ? (Domains)Random.Range(1, 5): (Domains)Random.Range(2, 5);
                newFlora.Initialize(this);
            }
            while (true)
            {
                var controllingVolume = gameData.GetControllingTeamStatsBasedOnVolumeRemaining().Item2; // GetTeamVolume(ControllingTeam);
                if (controllingVolume < floraSpawnVolumeCeiling)
                {
                    var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                    newFlora.domain = spawnJade ? (Domains)Random.Range(1, 5) : (Domains)Random.Range(2, 5);
                    newFlora.Initialize(this);
                }

                float waitPeriod;
                if (floraConfiguration.OverrideDefaultPlantPeriod)
                    waitPeriod = floraConfiguration.NewPlantPeriod;
                else
                    waitPeriod = floraConfiguration.Flora.PlantPeriod;
                    
                yield return new WaitForSeconds(waitPeriod);
            }
        }
        
        IEnumerator SpawnPopulation(Population population)
        {
            yield return new WaitForSeconds(initialFaunaSpawnWaitTime);
            while (true)
            {
                var controllingTeamStat = gameData.GetControllingTeamStatsBasedOnVolumeRemaining();
                var controllingVolume = controllingTeamStat.Item2; // GetTeamVolume(ControllingTeam);
                var period = baseFaunaSpawnTime * faunaSpawnVolumeThreshold / controllingVolume; //TODO: use this to adjust spawn rate
                if (controllingVolume > faunaSpawnVolumeThreshold)
                {
                    var newPopulation = Instantiate(population, transform.position, Quaternion.identity);
                    newPopulation.domain = controllingTeamStat.Item1; // ControllingTeam;
                    newPopulation.Goal = cellData.CrystalTransform.position;
                    yield return new WaitForSeconds(baseFaunaSpawnTime);
                }
                else
                {
                    yield return new WaitForSeconds(2);
                }
            } 
        }
    }
}