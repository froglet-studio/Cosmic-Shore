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
        [SerializeField] GameDataSO gameData;
        [SerializeField] public ScriptableEventNoParam OnResetForReplay;
        
        [SerializeField] public ScriptableEventNoParam OnCrystalSpawned;
        [SerializeField] public ScriptableEventNoParam OnCellItemsUpdated;

        public Dictionary<int, CellStats> CellStatsList = new();

        public SO_CellType CellType;
        public Cell Cell;
        public Transform CellTransform => Cell ? Cell.transform : null;

        public List<CellItem> CellItems = new();
        public List<Crystal> Crystals = new();
        
        public void AddCrystalToList(Crystal crystal)
        {
            if (!crystal) return;
            
            CellItems.Add(crystal);
            Crystals.Add(crystal);
            OnCellItemsUpdated.Raise();
        }

        /// <summary>
        /// CRITICAL: Get crystal transform for local player
        /// Returns null if no crystal exists
        /// </summary>
        public Transform CrystalTransform
        {
            get
            {
                if (!TryGetLocalCrystal(out Crystal crystal))
                {
                    Debug.LogWarning("[CellDataSO] No local crystal found!");
                    return null;
                }
                return crystal.transform;
            }
        }

        /// <summary>
        /// CRITICAL: Get crystal for local player
        /// First tries to get by player's domain, falls back to Domains.None
        /// </summary>
        public bool TryGetLocalCrystal(out Crystal crystal)
        {
            crystal = null;
            var ownDomain = gameData?.LocalPlayer?.Domain ?? Domains.None;
            
            // Try to get crystal matching player's domain
            if (TryGetCrystalByDomain(ownDomain, out crystal))
                return true;
            
            // Fall back to neutral crystal
            if (TryGetCrystalByDomain(Domains.None, out crystal))
                return true;
            
            // If still nothing, just return the first crystal
            if (Crystals != null && Crystals.Count > 0 && Crystals[0])
            {
                crystal = Crystals[0];
                return true;
            }
            
            return false;
        }
        
        bool TryGetCrystalByDomain(Domains domain, out Crystal crystal)
        {
            crystal = null;
            
            if (Crystals == null || Crystals.Count == 0)
                return false;
            
            foreach (var c in Crystals.Where(c => c && c.ownDomain == domain))
            {
                crystal = c;
                return true;
            }
            
            return false;
        }
        
        public bool TryGetCrystalById(int crystalId, out Crystal crystal)
        {
            crystal = null;
            
            if (Crystals == null || Crystals.Count == 0)
                return false;
            
            foreach (var c in Crystals.Where(c => c && c.Id == crystalId))
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

        /// <summary>
        /// CRITICAL FIX: Properly destroy all crystals before clearing lists
        /// </summary>
        public void ResetRuntimeData()
        {
            Debug.Log("<color=yellow>[CellDataSO] Resetting runtime data</color>");
            
            CellType = null;
            Cell = null;
            
            // CRITICAL: Destroy crystal GameObjects before clearing list
            if (Crystals != null)
            {
                for (int i = Crystals.Count - 1; i >= 0; i--)
                {
                    if (Crystals[i] && Crystals[i].gameObject)
                    {
                        Debug.Log($"<color=yellow>[CellDataSO] Destroying crystal {Crystals[i].Id}</color>");
                        Object.Destroy(Crystals[i].gameObject);
                    }
                }
                Crystals.Clear();
            }
            
            // Clear cell items (they should already be destroyed)
            CellItems?.Clear();
            
            Debug.Log("<color=green>[CellDataSO] Runtime data reset complete</color>");
        }
    }
}