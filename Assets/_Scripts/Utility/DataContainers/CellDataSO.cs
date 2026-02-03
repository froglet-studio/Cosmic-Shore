using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(
        fileName = "scriptable_variable_" + nameof(CellDataSO),
        menuName = "ScriptableObjects/DataContainers/" + nameof(CellDataSO))]
    public class CellDataSO : ScriptableObject
    {
        [SerializeField] public ScriptableEventNoParam OnCrystalSpawned;
        [SerializeField] public ScriptableEventNoParam OnCellItemsUpdated;

        [SerializeField] public ScriptableEventNoParam OnResetForReplay;
        
        public Dictionary<int, CellStats> CellStatsList = new();

        public SO_CellType CellType;
        public Cell Cell;
        public Transform CellTransform => Cell.transform;

        public List<CellItem> CellItems;
        public Crystal Crystal;
        public Transform CrystalTransform => Crystal.transform;
        public float CrystalRadius => Crystal.SphereRadius;

        public void EnsureCellStats(int cellId)
        {
            if (CellStatsList == null)
                CellStatsList = new Dictionary<int, CellStats>();

            if (!CellStatsList.ContainsKey(cellId))
                CellStatsList[cellId] = new CellStats { LifeFormsInCell = 0 };
        }

        public int GetLifeFormsInCellSafe(int cellId)
        {
            EnsureCellStats(cellId);
            return CellStatsList[cellId].LifeFormsInCell;
        }

        public void ResetRuntimeData()
        {
            CellType = null;
            Cell = null;
            CellItems?.Clear();
            Crystal = null;
        }
        
    }
}