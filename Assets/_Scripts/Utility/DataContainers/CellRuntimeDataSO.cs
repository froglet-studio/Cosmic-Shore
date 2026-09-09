using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
namespace CosmicShore.Utility
{
    [CreateAssetMenu(
        fileName = "DataContainer_" + nameof(CellRuntimeDataSO),
        menuName = "ScriptableObjects/Data Containers/" + nameof(CellRuntimeDataSO))]
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
        [SerializeField] public ScriptableEventCellPhase OnPhaseChanged;
        [Tooltip("Raised once per periodic fauna spawn-cycle tick (per species loop) with the " +
                 "wave's domain + nucleus-claim state. Scoring systems (Brood Rush) listen here.")]
        [SerializeField] public ScriptableEventFaunaWave OnFaunaWaveSpawned;
        [Tooltip("Raised when the set of living fauna hearts changes (a fauna gained its " +
                 "lineage heart, or died and dropped it). The domain fauna buff system listens " +
                 "here to re-sum domain elemental power without waiting for its reconcile sweep.")]
        [SerializeField] public ScriptableEventNoParam OnFaunaHeartsChanged;
        [Tooltip("Raised with the KILLER'S NAME when a fauna dies to an attributed force - a " +
                 "player shooting its body prisms out, or a crystal joust. Ecology-internal " +
                 "deaths (starvation, predation) are deliberately NOT published: a mode scored " +
                 "on wildlife kills must not have the wildlife scoring for itself. StatsManager " +
                 "(server only) turns it into IRoundStats.LifeformsKilled, the fauna twin of the " +
                 "flora stat LifeForm.OnLifeFormDeath already feeds.")]
        [SerializeField] public ScriptableEventString OnFaunaKilled;
        
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

            PruneDestroyed();

            CellItems.Add(crystal);
            Crystals.Add(crystal);

            OnCellItemsUpdated.Raise();
        }

        /// <summary>
        /// Drop entries whose object has been destroyed.
        ///
        /// <para>These lists live on a ScriptableObject ASSET, so they outlive every scene: one
        /// destroyed entry is a MissingReferenceException for the rest of the session, thrown not
        /// where the object died but in whoever iterates next. Every owner is supposed to remove
        /// itself and now does — this is the backstop that makes the failure self-healing rather
        /// than permanent, because "every future call site remembers" is not a property a shared
        /// mutable list can rely on.</para>
        ///
        /// <para>Cheap: it runs when the contents CHANGE, never per frame, and a cell holds a
        /// handful of items.</para>
        /// </summary>
        public void PruneDestroyed()
        {
            if (CellItems != null)
                for (int i = CellItems.Count - 1; i >= 0; i--)
                    if (!CellItems[i]) CellItems.RemoveAt(i);

            if (Crystals != null)
                for (int i = Crystals.Count - 1; i >= 0; i--)
                    if (!Crystals[i]) Crystals.RemoveAt(i);
        }
        
        public bool TryRemoveItem(CellItem item)
        {
            bool held = CellItems.Contains(item);
            if (held)
            {
                CellItems.Remove(item);
                if (item is Crystal crystal)
                    Crystals.Remove(crystal);
            }

            // Sweep regardless: this is the one call every owner makes on its way out, so it is
            // the cheapest place to notice that somebody ELSE died without saying so.
            PruneDestroyed();

            if (held) OnCellItemsUpdated.Raise();
            return held;
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
                    CSDebug.LogWarning("[CellRuntimeDataSO] No local crystal found!");
                    return null;
                }
                return crystal.transform;
            }
        }

        /// <summary>
        /// Get crystal for local player.
        /// Tries local domain, then Blue (the "no team" sentinel - uncommitted crystals),
        /// then first crystal.
        /// </summary>
        public bool TryGetLocalCrystal(out Crystal crystal)
        {
            crystal = null;

            var ownDomain = gameData?.LocalPlayer?.Domain ?? Domains.Blue;

            if (TryGetCrystalByDomain(ownDomain, out crystal))
                return true;

            if (TryGetCrystalByDomain(Domains.Blue, out crystal))
                return true;

            if (Crystals != null && Crystals.Count > 0 && Crystals[0])
            {
                crystal = Crystals[0];
                return true;
            }

            return false;
        }

        // Plain loops — the LINQ Where allocated a closure + enumerator per call,
        // and these run in per-frame paths (SnowChanger's reorientation slice calls
        // TryGetLocalCrystal every frame while a pass is active).
        bool TryGetCrystalByDomain(Domains domain, out Crystal crystal)
        {
            crystal = null;
            if (Crystals == null || Crystals.Count == 0) return false;

            for (int i = 0; i < Crystals.Count; i++)
            {
                var c = Crystals[i];
                if (c && c.ownDomain == domain)
                {
                    crystal = c;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetCrystalById(int crystalId, out Crystal crystal)
        {
            crystal = null;
            if (Crystals == null || Crystals.Count == 0) return false;

            for (int i = 0; i < Crystals.Count; i++)
            {
                var c = Crystals[i];
                if (c && c.Id == crystalId)
                {
                    crystal = c;
                    return true;
                }
            }

            return false;
        }

        public void EnsureCellStats(int cellId)
        {
            if (CellStatsList == null)
                CellStatsList = new Dictionary<int, CellStats>();

            if (!CellStatsList.ContainsKey(cellId))
                CellStatsList[cellId] = new CellStats
                {
                    LifeFormsInCell = 0,
                    LiveBlockCount = 0,
                    Phase = CellPhase.Calm,
                    DominantDomain = Domains.Blue,
                };
        }

        public int GetLifeFormsInCellSafe(int cellId)
        {
            EnsureCellStats(cellId);
            return CellStatsList[cellId].LifeFormsInCell;
        }

        /// <summary>
        /// Server-side write of phase + dominant domain + live block count for the
        /// addressed cell. Raises <see cref="OnPhaseChanged"/> when the phase actually
        /// transitions so consumers can react. The server calls this directly via
        /// <see cref="Cell"/>; clients route through <c>CellNetworkSync.OnValueChanged</c>
        /// so the same final state is observed everywhere.
        /// </summary>
        public void WriteCellRuntimeStats(int cellId, int liveBlockCount, CellPhase phase, Domains dominantDomain)
        {
            EnsureCellStats(cellId);

            var stats = CellStatsList[cellId];
            var previousPhase = stats.Phase;

            stats.LiveBlockCount = liveBlockCount;
            stats.Phase = phase;
            stats.DominantDomain = dominantDomain;
            CellStatsList[cellId] = stats;

            if (phase != previousPhase)
                OnPhaseChanged.Raise(phase);
        }

        /// <summary>
        /// Runtime-only reset. Destroys crystals, clears lists, clears runtime refs.
        /// Config is NOT cleared.
        /// </summary>
        public void ResetRuntimeData()
        {
            CSDebug.Log("<color=yellow>[CellRuntimeDataSO] Resetting runtime data</color>");

            Cell = null;

            if (Crystals != null)
            {
                for (int i = Crystals.Count - 1; i >= 0; i--)
                {
                    if (Crystals[i] && Crystals[i].gameObject)
                    {
                        CSDebug.Log($"<color=yellow>[CellRuntimeDataSO] Destroying crystal {Crystals[i].Id}</color>");
                        Object.Destroy(Crystals[i].gameObject);
                    }
                }
                Crystals.Clear();
            }

            CellItems?.Clear();
            CellStatsList?.Clear();

            CSDebug.Log("<color=green>[CellRuntimeDataSO] Runtime data reset complete</color>");
        }
    }
}