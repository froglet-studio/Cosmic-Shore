using CosmicShore.Core;
using CosmicShore.Utilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace CosmicShore.Game
{
    // TODO: Rename to 'CellManager'
    public class CellControlManager : Singleton<CellControlManager>
    {
        [SerializeField] List<Cell> cells;

        void OnEnable()
        {
            GameManager.OnGameOver += OutputCodeControl;
        }

        void OnDisable()
        {
            GameManager.OnGameOver -= OutputCodeControl;
        }
        
        public void AddBlock(Teams team, TrailBlockProperties blockProperties)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(blockProperties.position)))
            {
                cell.ChangeVolume(team, blockProperties.volume);
                cell.AddBlock(blockProperties.trailBlock);
                break;
            }
        }

        public void RemoveBlock(Teams team, TrailBlockProperties blockProperties)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(blockProperties.position)))
            {
                cell.ChangeVolume(team, -blockProperties.volume);
                cell.RemoveBlock(blockProperties.trailBlock);
                break;
            }
        }

        public Cell GetCellByPosition(Vector3 position) =>
            cells.FirstOrDefault(cell => cell.ContainsPosition(position));

        public Cell GetNearestCell(Vector3 position)
        {
            if (cells.Count == 0) return null;

            var minPosition = Mathf.Infinity;
            var result = cells[0];

            foreach (var cell in cells.Where(IsCloseToCell))
                result = cell;

            return result;
            
            bool IsCloseToCell(Cell cell) => Vector3.SqrMagnitude(position - cell.transform.position) < minPosition;
        }


        public void AddItem(CellItem item)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(item.transform.position)))
            {
                cell.TryInitializeAndAdd(item);
                break;
            }
        }

        public void RemoveItem(CellItem item)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(item.transform.position)))
            {
                cell.TryRemoveItem(item);
                break;
            }
        }

        public void UpdateItem(CellItem item)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(item.transform.position)))
            {
                cell.UpdateItem();
                break;
            }
        }

        public void StealBlock(Teams team, TrailBlockProperties blockProperties)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(blockProperties.position)))
            {
                cell.ChangeVolume(team, blockProperties.volume);
                cell.ChangeVolume(blockProperties.trailBlock.Team, -blockProperties.volume);
                break;
            }
        }

        public void RestoreBlock(Teams team, TrailBlockProperties blockProperties)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(blockProperties.position)))
            {
                cell.ChangeVolume(team, blockProperties.volume);
                break;
            }
        }

        void OutputCodeControl()
        {
            foreach (var cell in cells.Where(cell => cell.enabled))
            {
                Debug.LogWarning(
                    $"Node Control - Node ID: {cell.ID}, Controlling Team: {cell.ControllingTeam}, Green Volume: {cell.GetTeamVolume(Teams.Jade)}, Red Volume: {cell.GetTeamVolume(Teams.Ruby)}");
            }
        }
    }
}

