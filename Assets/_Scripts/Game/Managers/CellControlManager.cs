using CosmicShore.Core;
using CosmicShore.Utilities;
using Obvious.Soap;
using System.Collections.Generic;
using UnityEngine;


// TODO: Rename to 'CellManager'
public class CellControlManager : Singleton<CellControlManager>
{
    [SerializeField] List<Cell> Cells;

    public void AddBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var cell in Cells)
        {
            if (cell.ContainsPosition(blockProperties.position))
            {
                cell.ChangeVolume(team, blockProperties.volume);
                cell.AddBlock(blockProperties.trailBlock);
                break;
            }
        }
    }

    public void RemoveBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var cell in Cells)
        {
            if (cell.ContainsPosition(blockProperties.position))
            {
                cell.ChangeVolume(team, -blockProperties.volume);
                cell.RemoveBlock(blockProperties.trailBlock);
                break;
            }
        }
    }

    public Cell GetCellByPosition(Vector3 position)
    {
        foreach (var cell in Cells)
            if (cell.ContainsPosition(position))
                return cell;

        return null;
    }

    public Cell GetNearestCell(Vector3 position)
    {
        if (Cells.Count == 0) return null;

        var minPosition = Mathf.Infinity;
        var result = Cells[0];

        foreach (var cell in Cells)
            if (Vector3.SqrMagnitude(position - cell.transform.position) < minPosition)
                result = cell;

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

    public void AddItem(CellItem item)
    {
        foreach (var cell in Cells)
        {
            if (cell.ContainsPosition(item.transform.position))
            {
                cell.AddItem(item);
                break;
            }
        }
    }

    public void RemoveItem(CellItem item)
    {
        foreach (var cell in Cells)
        {
            if (cell.ContainsPosition(item.transform.position))
            {
                cell.RemoveItem(item);
                break;
            }
        }
    }

    public void UpdateItem(CellItem item)
    {
        foreach (var cell in Cells)
        {
            if (cell.ContainsPosition(item.transform.position))
            {
                // cell.NotifyPilotsOfUpdates();
                break;
            }
        }
    }

    public void StealBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var cell in Cells)
        {
            if (cell.ContainsPosition(blockProperties.position))
            {
                cell.ChangeVolume(team, blockProperties.volume);
                cell.ChangeVolume(blockProperties.trailBlock.Team, -blockProperties.volume);
                break;
            }
        }
    }

    public void RestoreBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var cell in Cells)
        {
            if (cell.ContainsPosition(blockProperties.position))
            {
                cell.ChangeVolume(team, blockProperties.volume);
                break;
            }
        }
    }

    public void OutputNodeControl()
    {
        foreach (var cell in Cells)
        {
            if (cell.enabled)
                Debug.LogWarning($"Node Control - Node ID: {cell.ID}, Controlling Team: {cell.ControllingTeam}, Green Volume: {cell.GetTeamVolume(Teams.Jade)}, Red Volume: {cell.GetTeamVolume(Teams.Ruby)}");
        }
    }
}