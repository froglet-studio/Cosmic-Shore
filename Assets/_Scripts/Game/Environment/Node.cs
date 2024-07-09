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
    [SerializeField] SnowChanger SnowChanger;
    [SerializeField] Crystal Crystal;

    [SerializeField] Flora flora1;
    [SerializeField] Flora flora2;

    [SerializeField] Population fauna1;
    [SerializeField] Population fauna2;

    [SerializeField] FloraCollection floraCollection;

    [SerializeField] float floraSpawnVolumeCeiling = 12000f;

    [SerializeField] float initialFaunaSpawnWaitTime = 10f;
    [SerializeField] float faunaSpawnVolumeThreshold = 1f;
    [SerializeField] float baseFaunaSpawnTime = 60f;

    [SerializeField] bool hasRandomFloraAndFauna;

    [SerializeField] private float minOctreeSize = 20f;
    public Dictionary<Teams, BlockOctree> blockOctrees = new Dictionary<Teams, BlockOctree>();

    Dictionary<Teams, float> teamVolumes = new Dictionary<Teams, float>();

    Dictionary<int, NodeItem> NodeItems = new Dictionary<int, NodeItem>();
    List<AIPilot> AIPilots = new List<AIPilot>();
    int itemsAdded;


    void Start()
    {
        if (hasRandomFloraAndFauna)
        {
            flora1 = (Flora)floraCollection.GetRandomPrefab();
            flora2 = (Flora)floraCollection.GetRandomPrefab();
        }

        teamVolumes.Add(Teams.Green, 0);
        teamVolumes.Add(Teams.Red, 0);
        teamVolumes.Add(Teams.Gold, 0);

        SnowChanger.SetOrigin(transform.position);
        Crystal.SetOrigin(transform.position);
        if (fauna1) StartCoroutine(SpawnFauna(fauna1));
        if (fauna2) StartCoroutine(SpawnFauna(fauna2));
        if (flora1) StartCoroutine(SpawnFlora(flora1));
        if (flora2) StartCoroutine(SpawnFlora(flora2));
    }

    void Awake()
    {
        Vector3 size = transform.localScale;
        float maxSize = Mathf.Max(size.x, size.y, size.z) * 10;  // Unclear why such a large multiplier is needed.
        Teams[] teams = { Teams.Green, Teams.Red, Teams.Gold };  // TODO: Store this as a constant somewhere (where?).
        foreach (Teams t in teams)
        {
            blockOctrees.Add(t, new BlockOctree(transform.position, maxSize, minOctreeSize, t));
        }
    }

    public void AddBlock(TrailBlock block)
    {
        Teams[] teams = { Teams.Green, Teams.Red, Teams.Gold };
        foreach (Teams t in teams)
        {
            if (t != block.Team) blockOctrees[t].AddBlock(block);
        }
    }

    public void RemoveBlock(TrailBlock block)
    {
        Teams[] teams = { Teams.Green, Teams.Red, Teams.Gold };
        foreach (Teams t in teams)
        {
            if (t != block.Team) blockOctrees[t].RemoveBlock(block);
        }
    }

    public List<Vector3> GetExplosionTargets(int count, Teams team)
    {
        return blockOctrees[team].FindDensestRegions(count);
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
        return Vector3.Distance(position, transform.position) < transform.localScale.x; // only works if nodes remain spherical
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

            if (!teamVolumes.ContainsKey(Teams.Green)  && !teamVolumes.ContainsKey(Teams.Red) &&!teamVolumes.ContainsKey(Teams.Gold))
                return Teams.None;

            if ((!teamVolumes.ContainsKey(Teams.Red) || (!teamVolumes.ContainsKey(Teams.Gold))) && teamVolumes[Teams.Green] > volumeControlThreshold)
                return Teams.Green;

            if ((!teamVolumes.ContainsKey(Teams.Green) || (!teamVolumes.ContainsKey(Teams.Gold))) && teamVolumes[Teams.Red] > volumeControlThreshold)
                return Teams.Red;

            if ((!teamVolumes.ContainsKey(Teams.Green) || (!teamVolumes.ContainsKey(Teams.Red))) && teamVolumes[Teams.Gold] > volumeControlThreshold)
                return Teams.Gold;

            if (teamVolumes[Teams.Green] < volumeControlThreshold && teamVolumes[Teams.Red] < volumeControlThreshold && teamVolumes[Teams.Gold] < volumeControlThreshold)
                return Teams.None;

            if (teamVolumes[Teams.Green] == teamVolumes[Teams.Gold] && teamVolumes[Teams.Green] == teamVolumes[Teams.Red])
                return Teams.None;

            if (teamVolumes[Teams.Green] > teamVolumes[Teams.Red] && teamVolumes[Teams.Green] > teamVolumes[Teams.Gold])
                return Teams.Green;
            else if (teamVolumes[Teams.Red] > teamVolumes[Teams.Green] && teamVolumes[Teams.Red] > teamVolumes[Teams.Gold])
                return Teams.Red;
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