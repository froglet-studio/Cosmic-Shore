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
        [SerializeField] public int ID;

        [SerializeField] List<SO_CellType> CellTypes;
        
        [SerializeField]
        CellDataSO cellData;
        SO_CellType cellType => cellData.CellType;
        
        /*SO_CellType cellType;
        public SO_CellType CellType
        {
            get => cellType;
            set
            {
                cellType = value;
                AssignCellType();
            }
        }*/

        // SnowChanger SnowChanger;
        GameObject membrane;
        GameObject nucleus; // TODO: Use radius to spawn/move crystal

        [SerializeField] int FloraTypeCount = 2;
        [SerializeField] bool spawnJade = true;
        [SerializeField] int FaunaTypeCount = 2;

        [SerializeField] float floraSpawnVolumeCeiling = 12000f;

        [SerializeField] float initialFaunaSpawnWaitTime = 10f;
        [SerializeField] float faunaSpawnVolumeThreshold = 1f;
        [SerializeField] float baseFaunaSpawnTime = 60f;

        [SerializeField] bool hasRandomFloraAndFauna;

        /*[SerializeField]
        ScriptableEventNoParam OnCellItemsUpdated;*/
        
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;

        public Dictionary<Domains, BlockCountDensityGrid> countGrids = new Dictionary<Domains, BlockCountDensityGrid>();
        public Dictionary<Domains, BlockVolumeDensityGrid> volumeGrids = new Dictionary<Domains, BlockVolumeDensityGrid>();
        // public List<CellItem> CellItems { get; private set; }

        Dictionary<Domains, float> teamVolumes = new Dictionary<Domains, float>();

        // int _itemsAdded;

        void Awake()
        {
            /*if (cellType == null)
            {
                AssignCellType();
            }*/

            cellData.Cell = this;
            AssignCellType();

            // TODO: handle Blue?
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };  // TODO: Store this as a constant somewhere (where?).
            foreach (Domains t in teams)
            {
                countGrids.Add(t, new BlockCountDensityGrid(t));
                //volumeGrids.Add(t, new BlockVolumeDensityGrid(t));
            }
        }

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
            normalizeWeights();

            var cellType = cellData.CellType;
            membrane = Instantiate(cellType.MembranePrefab, transform.position, Quaternion.identity);
            nucleus = Instantiate(cellType.NucleusPrefab, transform.position, Quaternion.identity);
            
            // CrystalManager.Instance.Initialize();
            // SnowChanger.Initialize(crystalManager.GetCrystalTransform(), crystalManager.GetSphereRadius());
            
            // TODO - Remove execution dependency of initializationbetween CrystalManager and SnowChanger
            /*SnowChanger = Instantiate(cellType.CytoplasmPrefab, transform.position, Quaternion.identity);
            SnowChanger.Initialize();
            SnowChanger.SetOrigin(transform.position);*/

            teamVolumes.Add(Domains.Jade, 0);
            teamVolumes.Add(Domains.Ruby, 0);
            teamVolumes.Add(Domains.Gold, 0);
            teamVolumes.Add(Domains.Blue, 0);

            /*Crystal.SetOrigin(transform.position);

            if (cellType != null)
            {
                foreach (var modifier in cellType.CellModifiers)
                {
                    modifier.Apply(this);
                }
                SpawnLife();
            }
            TryInitializeAndAdd(crystalManager.Crystal);
            Crystal.gameObject.SetActive(true);
            crystalManager.ToggleCrstalActive(true);*/
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
            if (CellTypes is { Count: > 0 })
            {
                cellData.CellType = CellTypes[Random.Range(0, CellTypes.Count)];
            }

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

        /*public Teams ControllingTeam
        {
            /// TODO: replace this with below. This is a temporary fix to the issue of a single node not being able to accurately determine team volume

            get
            {
                if (!miniGameData.TryGetTeamRemainingVolume(Teams.Jade, out float greenVolume))
                    return

                var greenVolume =
                    StatsManager.Instance is not null && StatsManager.Instance.TeamStats.ContainsKey(Teams.Jade)
                        ? StatsManager.Instance.TeamStats[Teams.Jade].VolumeRemaining
                        : 0f;
                var redVolume = StatsManager.Instance is not null && StatsManager.Instance.TeamStats.ContainsKey(Teams.Ruby)
                    ? StatsManager.Instance.TeamStats[Teams.Ruby].VolumeRemaining
                    : 0f;
                var goldVolume =
                    StatsManager.Instance is not null && StatsManager.Instance.TeamStats.ContainsKey(Teams.Gold)
                        ? StatsManager.Instance.TeamStats[Teams.Gold].VolumeRemaining
                        : 0f;

                if (greenVolume > redVolume && greenVolume > goldVolume)
                    return Teams.Jade;
                else if (redVolume > greenVolume && redVolume > goldVolume)
                    return Teams.Ruby;
                else if (goldVolume > greenVolume && goldVolume > redVolume)
                    return Teams.Gold;
                else
                    return Teams.None;
            }
        }*/
        
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