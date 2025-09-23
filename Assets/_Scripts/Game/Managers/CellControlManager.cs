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
        
        public void AddBlock(Domains domain, PrismProperties blockProperties)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(blockProperties.position)))
            {
                cell.ChangeVolume(domain, blockProperties.volume);
                cell.AddBlock(blockProperties.prism);
                break;
            }
        }

        public void RemoveBlock(Domains domain, PrismProperties blockProperties)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(blockProperties.position)))
            {
                cell.ChangeVolume(domain, -blockProperties.volume);
                cell.RemoveBlock(blockProperties.prism);
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

        public void StealBlock(Domains domain, PrismProperties blockProperties)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(blockProperties.position)))
            {
                cell.ChangeVolume(domain, blockProperties.volume);
                cell.ChangeVolume(blockProperties.prism.Domain, -blockProperties.volume);
                break;
            }
        }

        public void RestoreBlock(Domains domain, PrismProperties blockProperties)
        {
            foreach (var cell in cells.Where(cell => cell.ContainsPosition(blockProperties.position)))
            {
                cell.ChangeVolume(domain, blockProperties.volume);
                break;
            }
        }
    }
}

