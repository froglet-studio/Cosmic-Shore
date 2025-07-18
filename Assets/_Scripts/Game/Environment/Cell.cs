using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using Obvious.Soap;
using Random = UnityEngine.Random;


namespace CosmicShore.Game
{
    public class Cell : MonoBehaviour
    {
        [SerializeField] public string ID;

        [SerializeField] Crystal Crystal;

        [SerializeField] List<SO_CellType> CellTypes;
        SO_CellType cellType;
        public SO_CellType CellType 
        { 
            get => cellType;
            set 
            {
                cellType = value;
                AssignCellType();
            } 
        }

        SnowChanger SnowChanger;
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

        [SerializeField]
        ScriptableEventNoParam OnCellItemsUpdated;

        public Dictionary<Teams, BlockCountDensityGrid> countGrids = new Dictionary<Teams, BlockCountDensityGrid>();
        public Dictionary<Teams, BlockVolumeDensityGrid> volumeGrids = new Dictionary<Teams, BlockVolumeDensityGrid>();
        public List<CellItem> CellItems { get; private set; }

        Dictionary<Teams, float> teamVolumes = new Dictionary<Teams, float>();

        int _itemsAdded;

        void Awake()
        {
            CellItems = new List<CellItem>();

            if (cellType == null)
            {
                AssignCellType();
            }

            // TODO: handle Blue?
            Teams[] teams = { Teams.Jade, Teams.Ruby, Teams.Gold, Teams.Blue };  // TODO: Store this as a constant somewhere (where?).
            foreach (Teams t in teams)
            {
                countGrids.Add(t, new BlockCountDensityGrid(t));
                //volumeGrids.Add(t, new BlockVolumeDensityGrid(t));
            }
        }
        
        void Start()
        {
            normalizeWeights();

            membrane = Instantiate(cellType.MembranePrefab, transform.position, Quaternion.identity);
            nucleus = Instantiate(cellType.NucleusPrefab, transform.position, Quaternion.identity);
            SnowChanger = Instantiate(cellType.CytoplasmPrefab, transform.position, Quaternion.identity);
            SnowChanger.Crystal = Crystal.gameObject;
            SnowChanger.SetOrigin(transform.position);

            teamVolumes.Add(Teams.Jade, 0);
            teamVolumes.Add(Teams.Ruby, 0);
            teamVolumes.Add(Teams.Gold, 0);
            teamVolumes.Add(Teams.Blue, 0);

            Crystal.SetOrigin(transform.position);

            if (cellType != null)
            {
                foreach (var modifier in cellType.CellModifiers)
                {
                    modifier.Apply(this);
                }
                SpawnLife();
            }
            TryAddItem(Crystal);
            Crystal.gameObject.SetActive(true);
        }


        void AssignCellType() 
        {
            if (CellTypes != null && CellTypes.Count > 0)
            {
                cellType = CellTypes[Random.Range(0, CellTypes.Count)];
            }

            if (cellType == null)
            {
                Debug.LogError("Cell type is not assigned. Please assign a valid cell type.");
            }
        }

        void SpawnLife()
        {
            if (cellType.SupportedFlora.Count > 0)
            {
                for (int i = 0; i < FloraTypeCount; i++)
                {
                    var floraConfiguration = SpawnRandomFlora();
                    StartCoroutine(SpawnFlora(floraConfiguration, spawnJade));
                }
            }

            if (cellType.SupportedFauna.Count > 0)
            {
                for (int i = 0; i < FaunaTypeCount; i++)
                {
                    var fauna = SpawnRandomPopulation();
                    StartCoroutine(SpawnFauna(fauna));
                }
            }
        }

        Population SpawnRandomPopulation()
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

        FloraConfiguration SpawnRandomFlora()
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

        public void AddBlock(TrailBlock block)
        {
            Teams[] teams = { Teams.Jade, Teams.Ruby, Teams.Gold };
            foreach (Teams t in teams)
            {
                if (t != block.Team) countGrids[t].AddBlock(block);
            }
        }

        public void RemoveBlock(TrailBlock block)
        {
            Teams[] teams = { Teams.Jade, Teams.Ruby, Teams.Gold };
            foreach (Teams t in teams)
            {
                if (t != block.Team) countGrids[t].RemoveBlock(block);
            }
        }

        public Vector3 GetExplosionTarget(Teams team)
        {
            return countGrids[team].FindDensestRegion();
        }

        public bool TryAddItem(CellItem item)
        {
            if (item.GetID() != 0)
                return false;
            
            item.SetID(++_itemsAdded);
            CellItems.Add(item);
            OnCellItemsUpdated.Raise();

            return true;
        }

        public bool TryRemoveItem(CellItem item)
        {
            if (!CellItems.Contains(item))
                return false;
            
            CellItems.Remove(item);
            OnCellItemsUpdated.Raise();

            return true;
        }

        public void UpdateItem() => OnCellItemsUpdated.Raise();

        public CellItem GetClosestItem(Vector3 position)
        {
            float minDistance = Mathf.Infinity;
            CellItem closestItem = null;

            foreach (var item in CellItems)
            {
                var distance = Vector3.Distance(item.transform.position, position);
                if (distance < minDistance)
                {
                    closestItem = item;
                    minDistance = distance;
                }
            }

            return closestItem;
        }

        public CellItem GetCrystal()
        {
            return Crystal;
        }

        public bool ContainsPosition(Vector3 position)
        {
            if (membrane is null)
                return false;

            return Vector3.Distance(position, transform.position) < membrane.transform.localScale.x; // only works if nodes remain spherical
        }

        public void ChangeVolume(Teams team, float volume)
        {
            if (!teamVolumes.ContainsKey(team))
                teamVolumes.Add(team, 0);

            teamVolumes[team] += volume;
        }

        public float GetTeamVolume(Teams team)
        {
            if (!teamVolumes.ContainsKey(team))
                return 0;

            return teamVolumes[team];
        }

        public Teams ControllingTeam
        {
            /// TODO: replace this with below. This is a temporary fix to the issue of a single node not being able to accurately determine team volume

            get
            {
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
        }

        IEnumerator SpawnFlora(FloraConfiguration floraConfiguration, bool spawnJade = true)
        {
            for (int i = 0; i < floraConfiguration.initialSpawnCount - 1; i++)
            {
                var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                newFlora.Team = spawnJade ? (Teams)Random.Range(1, 5): (Teams)Random.Range(2, 5);
                newFlora.Initialize(this);
            }
            while (true)
            {
                var controllingVolume = GetTeamVolume(ControllingTeam);
                if (controllingVolume < floraSpawnVolumeCeiling)
                {
                    var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                    newFlora.Team = spawnJade ? (Teams)Random.Range(1, 5) : (Teams)Random.Range(2, 5);
                    newFlora.Initialize(this);
                }
                if (floraConfiguration.OverrideDefaultPlantPeriod) yield return new WaitForSeconds(floraConfiguration.NewPlantPeriod);
                else yield return new WaitForSeconds(floraConfiguration.Flora.PlantPeriod);
            }
        }
        
        IEnumerator SpawnFauna(Population population)
        {
            yield return new WaitForSeconds(initialFaunaSpawnWaitTime);
            while (true)
            {
                var controllingVolume = GetTeamVolume(ControllingTeam);
                var period = baseFaunaSpawnTime * faunaSpawnVolumeThreshold / controllingVolume; //TODO: use this to adjust spawn rate
                if (controllingVolume > faunaSpawnVolumeThreshold)
                {
                    var newPopulation = Instantiate(population, transform.position, Quaternion.identity);
                    newPopulation.Team = ControllingTeam;
                    newPopulation.Goal = Crystal.transform.position;
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