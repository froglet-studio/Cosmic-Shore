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
    SO_CellType CellType;

    SnowChanger SnowChanger;
    GameObject membrane;
    GameObject nucleus; // TODO: Use radius to spawn/move crystal

    Flora flora1;
    Flora flora2;

    Population fauna1;
    Population fauna2;

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
        CellType = CellTypes[Random.Range(0, CellTypes.Count)];
        membrane = Instantiate(CellType.MembranePrefab, transform.position, Quaternion.identity);
        nucleus = Instantiate(CellType.NucleusPrefab, transform.position, Quaternion.identity);
        SnowChanger = Instantiate(CellType.CytoplasmPrefab, transform.position, Quaternion.identity);
        SnowChanger.Crystal = Crystal.gameObject;

        // TODO: handle Blue?
        Teams[] teams = { Teams.Jade, Teams.Ruby, Teams.Gold };  // TODO: Store this as a constant somewhere (where?).
        foreach (Teams t in teams)
        {
            countGrids.Add(t, new BlockCountDensityGrid(t));
            //volumeGrids.Add(t, new BlockVolumeDensityGrid(t));
        }
    }

    void Start()
    {

        foreach (var modifier in CellType.CellModifiers)
        {
            modifier.Apply(this);
        }

        if (CellType.SupportedFlora.Count > 0)
        {
            flora1 = CellType.SupportedFlora[Random.Range(0, CellType.SupportedFlora.Count)];
            flora2 = CellType.SupportedFlora[Random.Range(0, CellType.SupportedFlora.Count)];
        }
        if (CellType.SupportedFauna.Count > 0)
        {
            fauna1 = CellType.SupportedFauna[Random.Range(0, CellType.SupportedFauna.Count)];
            fauna2 = CellType.SupportedFauna[Random.Range(0, CellType.SupportedFauna.Count)];
        }

        teamVolumes.Add(Teams.Jade, 0);
        teamVolumes.Add(Teams.Ruby, 0);
        teamVolumes.Add(Teams.Gold, 0);

        SnowChanger.SetOrigin(transform.position);
        Crystal.SetOrigin(transform.position);
        if (fauna1) StartCoroutine(SpawnFauna(fauna1));
        if (fauna2) StartCoroutine(SpawnFauna(fauna2));
        if (flora1) StartCoroutine(SpawnFlora(flora1));
        if (flora2) StartCoroutine(SpawnFlora(flora2));
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

    IEnumerator SpawnFlora(Flora flora)
    {
        while (true)
        {
            var controllingVolume = GetTeamVolume(ControllingTeam);
            if (controllingVolume < floraSpawnVolumeCeiling)
            {
                var newFlora = Instantiate(flora, transform.position, Quaternion.identity);
                newFlora.Team = (Teams)Random.Range(1,5);
            }
            yield return new WaitForSeconds(flora.PlantPeriod);
        }
    }
    
    IEnumerator SpawnFauna(Population population)
    {
        yield return new WaitForSeconds(initialFaunaSpawnWaitTime);
        while (true)
        {
            var controllingVolume = GetTeamVolume(ControllingTeam);
            var period = baseFaunaSpawnTime * faunaSpawnVolumeThreshold / controllingVolume;
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