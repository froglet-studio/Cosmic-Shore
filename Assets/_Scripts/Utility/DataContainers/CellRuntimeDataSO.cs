using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(
        fileName = "scriptable_variable_" + nameof(CellRuntimeDataSO),
        menuName = "ScriptableObjects/DataContainers/" + nameof(CellRuntimeDataSO))]
    public class CellRuntimeDataSO : ScriptableObject
    {
        // ---------------------------------------------------------------------
        // References (runtime)
        // ---------------------------------------------------------------------

        [Header("Design Time References")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] public ScriptableEventNoParam OnResetForReplay;
        [SerializeField] public ScriptableEventNoParam OnCrystalSpawned;
        [SerializeField] public ScriptableEventNoParam OnCellItemsUpdated;
        
        [Header("Run Time References")]
        public CellConfigDataSO Config; // <- your "CellConfigData"

        // ---------------------------------------------------------------------
        // Runtime State
        // ---------------------------------------------------------------------

        public Dictionary<int, CellStats> CellStatsList = new();

        public Cell Cell;
        public Transform CellTransform => Cell ? Cell.transform : null;

        public List<CellItem> CellItems = new();
        public List<Crystal> Crystals = new();

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public void AddCrystalToList(Crystal crystal)
        {
            if (!crystal) return;

            CellItems.Add(crystal);
            Crystals.Add(crystal);

            OnCellItemsUpdated?.Raise();
        }
        
        public bool TryRemoveItem(CellItem item)
        {
            if (!CellItems.Contains(item))
                return false;

            CellItems.Remove(item);
            if (item is Crystal crystal)
                Crystals.Remove(crystal);
            OnCellItemsUpdated.Raise();
            return true;
        }

        /// <summary>
        /// Get crystal transform for local player (falls back to neutral, then first crystal).
        /// Returns null if no crystal exists.
        /// </summary>
        public Transform CrystalTransform
        {
            get
            {
                if (!TryGetLocalCrystal(out Crystal crystal))
                {
                    Debug.LogWarning("[CellRuntimeDataSO] No local crystal found!");
                    return null;
                }
                return crystal.transform;
            }
        }

        /// <summary>
        /// Get crystal for local player.
        /// Tries local domain, then Domains.None, then first crystal.
        /// </summary>
        public bool TryGetLocalCrystal(out Crystal crystal)
        {
            crystal = null;

            var ownDomain = gameData?.LocalPlayer?.Domain ?? Domains.None;

            if (TryGetCrystalByDomain(ownDomain, out crystal))
                return true;

            if (TryGetCrystalByDomain(Domains.None, out crystal))
                return true;

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
            if (Crystals == null || Crystals.Count == 0) return false;

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
            if (Crystals == null || Crystals.Count == 0) return false;

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
        /// Runtime-only reset. Destroys crystals, clears lists, clears runtime refs.
        /// Config is NOT cleared.
        /// </summary>
        public void ResetRuntimeData()
        {
            Debug.Log("<color=yellow>[CellRuntimeDataSO] Resetting runtime data</color>");

            Cell = null;

            if (Crystals != null)
            {
                for (int i = Crystals.Count - 1; i >= 0; i--)
                {
                    if (Crystals[i] && Crystals[i].gameObject)
                    {
                        Debug.Log($"<color=yellow>[CellRuntimeDataSO] Destroying crystal {Crystals[i].Id}</color>");
                        Object.Destroy(Crystals[i].gameObject);
                    }
                }
                Crystals.Clear();
            }

            CellItems?.Clear();

            Debug.Log("<color=green>[CellRuntimeDataSO] Runtime data reset complete</color>");
        }
    }
}