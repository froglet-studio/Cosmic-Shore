using System.Collections.Generic;
using System.Linq;
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
        [SerializeField]
        GameDataSO gameData;
        
        [SerializeField] public ScriptableEventNoParam OnCrystalSpawned;
        [SerializeField] public ScriptableEventNoParam OnCellItemsUpdated;

        public Dictionary<int, CellStats> CellStatsList = new();

        public SO_CellType CellType;
        public Cell Cell;
        public Transform CellTransform => Cell.transform;

        public List<CellItem> CellItems = new();
        public List<Crystal> Crystals = new();
        
        public void AddCrystalToList(Crystal crystal)
        {
            CellItems.Add(crystal);
            Crystals.Add(crystal);
            OnCellItemsUpdated.Raise();
        }

        public Transform CrystalTransform => 
            !TryGetLocalCrystal(out Crystal crystal) ? null : crystal.transform;

        public bool TryGetLocalCrystal(out Crystal crystal)
        {
            crystal = null;
            var ownDomain = gameData.LocalPlayer?.Domain ?? Domains.None;
            return TryGetCrystalByDomain(ownDomain, out crystal) ||
                   (TryGetCrystalByDomain(Domains.None, out crystal));
        }
        
        bool TryGetCrystalByDomain(Domains domain, out Crystal crystal)
        {
            crystal = null;
            foreach (var c in Crystals.Where(crystal => crystal.ownDomain == domain))
            {
                crystal = c;
                return true;
            }
            
            return false;
        }
        
        public bool TryGetCrystalById(int crystalId, out Crystal crystal)
        {
            crystal = null;
            
            foreach (var c in Crystals.Where(c => c.Id == crystalId))
            {
                crystal = c;
                return true;
            }

            return false;
        }
        
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
            CellItems.Clear();
            Crystals.Clear();
        }
    }
}