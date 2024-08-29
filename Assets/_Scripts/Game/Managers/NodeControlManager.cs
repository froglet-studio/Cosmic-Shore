using CosmicShore.Core;
using CosmicShore.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;

// TODO: Rename to 'CellManager'
public class NodeControlManager : Singleton<NodeControlManager>
{
    [SerializeField] List<Node> Nodes;

    public void AddBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var node in Nodes)
        {
            if (node.ContainsPosition(blockProperties.position))
            {
                node.ChangeVolume(team, blockProperties.volume);
                //node.AddBlock(blockProperties.trailBlock);
                break;
            }
        }
    }

    public void RemoveBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var node in Nodes)
        {
            if (node.ContainsPosition(blockProperties.position))
            {
                node.ChangeVolume(team, -blockProperties.volume);
                node.RemoveBlock(blockProperties.trailBlock);
                break;
            }
        }
    }

    public Node GetNodeByPosition(Vector3 position)
    {
        foreach (var node in Nodes)
            if (node.ContainsPosition(position))
                return node;

        return null;
    }

    public Node GetNearestNode(Vector3 position)
    {
        var minPosition = Mathf.Infinity;
        var result = Nodes[0];

        foreach (var node in Nodes)
            if (Vector3.SqrMagnitude(position - node.transform.position) < minPosition)
                result = node;

        return result;
    }

    void OnEnable()
    {
        GameManager.OnGameOver += OutputNodeControl;
    }

    void OnDisable()
    {
        GameManager.OnGameOver -= OutputNodeControl;
    }

    public void AddItem(NodeItem item)
    {
        foreach (var node in Nodes)
        {
            if (node.ContainsPosition(item.transform.position))
            {
                node.AddItem(item);
                break;
            }
        }
    }

    public void RemoveItem(NodeItem item)
    {
        foreach (var node in Nodes)
        {
            if (node.ContainsPosition(item.transform.position))
            {
                node.RemoveItem(item);
                break;
            }
        }
    }

    public void UpdateItem(NodeItem item)
    {
        foreach (var node in Nodes)
        {
            if (node.ContainsPosition(item.transform.position))
            {
                node.UpdateItem(item);
                break;
            }
        }
    }

    public void StealBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var node in Nodes)
        {
            if (node.ContainsPosition(blockProperties.position))
            {
                node.ChangeVolume(team, blockProperties.volume);
                node.ChangeVolume(blockProperties.trailBlock.Team, -blockProperties.volume);
                break;
            }
        }
    }

    public void RestoreBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var node in Nodes)
        {
            if (node.ContainsPosition(blockProperties.position))
            {
                node.ChangeVolume(team, blockProperties.volume);
                break;
            }
        }
    }

    public void OutputNodeControl()
    {
        foreach (var node in Nodes)
        {
            if (node.enabled)
                Debug.LogWarning($"Node Control - Node ID: {node.ID}, Controlling Team: {node.ControllingTeam}, Green Volume: {node.GetTeamVolume(Teams.Jade)}, Red Volume: {node.GetTeamVolume(Teams.Ruby)}");
        }
    }
}