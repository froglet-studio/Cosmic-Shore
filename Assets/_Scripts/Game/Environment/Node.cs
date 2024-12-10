using System.Collections;
using System.Collections.Generic;
using CosmicShore.Environment.FlowField;
using CosmicShore.Game.AI;
using CosmicShore;
using UnityEngine;
using CosmicShore.Core;

public class Node : MonoBehaviour
{
    [SerializeField] public string ID;
    [SerializeField] float volumeControlThreshold = 100f;

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

    public Dictionary<Teams, BlockCountDensityGrid> countGrids = new Dictionary<Teams, BlockCountDensityGrid>();
    public Dictionary<Teams, BlockVolumeDensityGrid> volumeGrids = new Dictionary<Teams, BlockVolumeDensityGrid>();

    Dictionary<Teams, float> teamVolumes = new Dictionary<Teams, float>();

    Dictionary<int, NodeItem> NodeItems = new Dictionary<int, NodeItem>();
    List<AIPilot> AIPilots = new List<AIPilot>();
    int itemsAdded;

    void Awake()
    {
        if (cellType == null)
        {
            AssignCellType();
        }

        // TODO: handle Blue?
        Teams[] teams = { Teams.Jade, Teams.Ruby, Teams.Gold };  // TODO: Store this as a constant somewhere (where?).
        foreach (Teams t in teams)
        {
            countGrids.Add(t, new BlockCountDensityGrid(t));
            //volumeGrids.Add(t, new BlockVolumeDensityGrid(t));
        }
    }

    void AssignCellType() 
    {
        if (CellTypes != null && CellTypes.Count > 0)
        {
            cellType = CellTypes[Random.Range(0, CellTypes.Count)];
        }
        if (cellType != null) 
        {
            normalizeWeights();

            membrane = Instantiate(cellType.MembranePrefab, transform.position, Quaternion.identity);
            nucleus = Instantiate(cellType.NucleusPrefab, transform.position, Quaternion.identity);
            SnowChanger = Instantiate(cellType.CytoplasmPrefab, transform.position, Quaternion.identity);
            SnowChanger.Crystal = Crystal.gameObject;
            SnowChanger.SetOrigin(transform.position);

            foreach (var modifier in cellType.CellModifiers)
            {
                modifier.Apply(this);
            }
            SpawnLife();
            Crystal.gameObject.SetActive(true);
        }   
    }

    void Start()
    {
        teamVolumes.Add(Teams.Jade, 0);
        teamVolumes.Add(Teams.Ruby, 0);
        teamVolumes.Add(Teams.Gold, 0);

        Crystal.SetOrigin(transform.position);

        if (cellType != null)
        {
            foreach (var modifier in cellType.CellModifiers)
            {
                modifier.Apply(this);
            }
            SpawnLife();
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

    public void AddItem(NodeItem item)
    {
        if (item.GetID() == 0)
        {
            item.SetID(++itemsAdded);
            NodeItems.Add(item.GetID(), item);
            NotifyPilotsOfUpdates();
        }
    }

    public void RemoveItem(NodeItem item)
    {
        NodeItems.Remove(item.GetID());
        NotifyPilotsOfUpdates();
    }

    public void UpdateItem(NodeItem item)
    {
        NotifyPilotsOfUpdates();
    }

    public void RegisterForUpdates(AIPilot pilot)
    {
        AIPilots.Add(pilot);
    }

    void NotifyPilotsOfUpdates()
    {
        foreach (var pilot in AIPilots)
            pilot.NodeContentUpdated();
    }

    public Dictionary<int, NodeItem> GetItems()
    {
        return NodeItems;
    }

    public NodeItem GetClosestItem(Vector3 position)
    {
        float MinDistance = Mathf.Infinity;
        NodeItem closestItem = null;

        foreach (var item in NodeItems.Values)
        {
            var distance = Vector3.Distance(item.transform.position, position);
            if (distance < MinDistance)
            {
                closestItem = item;
                MinDistance = distance;
            }
        }

        return closestItem;
    }

    public NodeItem GetCrystal()
    {
        return Crystal;
    }

    public bool ContainsPosition(Vector3 position)
    {
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
        get
        {
            if (!enabled)
                return Teams.None;

            if (!teamVolumes.ContainsKey(Teams.Jade)  && !teamVolumes.ContainsKey(Teams.Ruby) &&!teamVolumes.ContainsKey(Teams.Gold))
                return Teams.None;

            if ((!teamVolumes.ContainsKey(Teams.Ruby) || (!teamVolumes.ContainsKey(Teams.Gold))) && teamVolumes[Teams.Jade] > volumeControlThreshold)
                return Teams.Jade;

            if ((!teamVolumes.ContainsKey(Teams.Jade) || (!teamVolumes.ContainsKey(Teams.Gold))) && teamVolumes[Teams.Ruby] > volumeControlThreshold)
                return Teams.Ruby;

            if ((!teamVolumes.ContainsKey(Teams.Jade) || (!teamVolumes.ContainsKey(Teams.Ruby))) && teamVolumes[Teams.Gold] > volumeControlThreshold)
                return Teams.Gold;

            if (teamVolumes[Teams.Jade] < volumeControlThreshold && teamVolumes[Teams.Ruby] < volumeControlThreshold && teamVolumes[Teams.Gold] < volumeControlThreshold)
                return Teams.None;

            if (teamVolumes[Teams.Jade] == teamVolumes[Teams.Gold] && teamVolumes[Teams.Jade] == teamVolumes[Teams.Ruby])
                return Teams.None;

            if (teamVolumes[Teams.Jade] > teamVolumes[Teams.Ruby] && teamVolumes[Teams.Jade] > teamVolumes[Teams.Gold])
                return Teams.Jade;
            else if (teamVolumes[Teams.Ruby] > teamVolumes[Teams.Jade] && teamVolumes[Teams.Ruby] > teamVolumes[Teams.Gold])
                return Teams.Ruby;
            else
                return Teams.Gold;
        }
    }

    IEnumerator SpawnFlora(FloraConfiguration floraConfiguration, bool spawnJade = true)
    {
        for (int i = 0; i < floraConfiguration.initialSpawnCount - 1; i++)
        {
            var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
            newFlora.Team = spawnJade ? (Teams)Random.Range(1, 5): (Teams)Random.Range(2, 5);
        }
        while (true)
        {
            var controllingVolume = GetTeamVolume(ControllingTeam);
            if (controllingVolume < floraSpawnVolumeCeiling)
            {
                var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                newFlora.Team = spawnJade ? (Teams)Random.Range(1, 5) : (Teams)Random.Range(2, 5);
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
                newPopulation.Goal = GetCrystal().gameObject.transform.position;
                yield return new WaitForSeconds(baseFaunaSpawnTime);
            }
            else
            {
                yield return new WaitForSeconds(2);
            }
        } 
    }
}