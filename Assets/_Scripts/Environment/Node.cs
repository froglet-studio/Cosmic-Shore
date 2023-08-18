using StarWriter.Core.IO;
using System.Collections.Generic;
using UnityEngine;

public class Node : MonoBehaviour
{
    [SerializeField] public string ID;
    [SerializeField] float volumeControlThreshold = 100f;
    [SerializeField] SnowChanger SnowChanger;
    [SerializeField] Crystal Crystal;

    Dictionary<Teams, float> teamVolumes = new Dictionary<Teams, float>();

    Dictionary<int, NodeItem> NodeItems = new Dictionary<int, NodeItem>();
    List<AIPilot> AIPilots = new List<AIPilot>();
    int itemsAdded;

    void Start()
    {
        teamVolumes.Add(Teams.Green, 0);
        teamVolumes.Add(Teams.Red, 0);

        SnowChanger.SetOrigin(transform.position);
        Crystal.SetOrigin(transform.position);
    }

    public void AddItem(NodeItem item)
    {
        item.SetID(++itemsAdded);
        NodeItems.Add(item.GetID(), item);
        NotifyPilotsOfUpdates();
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
            if (!enabled || !teamVolumes.ContainsKey(Teams.Green) || !teamVolumes.ContainsKey(Teams.Red))
                return Teams.None;

            if (teamVolumes[Teams.Green] < volumeControlThreshold && teamVolumes[Teams.Red] < volumeControlThreshold)
                return Teams.None;

            if (teamVolumes[Teams.Green] == teamVolumes[Teams.Red])
                return Teams.None;

            if (teamVolumes[Teams.Green] > teamVolumes[Teams.Red])
                return Teams.Green;
            else
                return Teams.Red;
        }
    }
}