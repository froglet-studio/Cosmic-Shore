using StarWriter.Core;
using StarWriter.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;

public class NodeControlManager : Singleton<NodeControlManager>
{
    [SerializeField] List<Node> Nodes;

    void OnEnable()
    {
        GameManager.onDeath += OutputNodeControl;
        GameManager.onGameOver += OutputNodeControl;
    }

    void OnDisable()
    {
        GameManager.onDeath -= OutputNodeControl;
        GameManager.onGameOver -= OutputNodeControl;
    }

    public void AddBlock(Teams team, string playerName, TrailBlockProperties blockProperties)
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

    public void RemoveBlock(Teams team, string playerName, TrailBlockProperties blockProperties)
    {
        foreach (var node in Nodes)
        {
            if (node.ContainsPosition(blockProperties.position))
            {
                node.ChangeVolume(team, -blockProperties.volume);
                break;
            }
        }
    }

    public void OutputNodeControl()
    {
        foreach (var node in Nodes)
        {
            //Debug.LogWarning($"Node Control - Node ID: {node.ID}, Controlling Team: {node.ControllingTeam}, Green Volume: {node.GetTeamVolume(Teams.Green)}, Red Volume: {node.GetTeamVolume(Teams.Red)}");
        }
    }
}