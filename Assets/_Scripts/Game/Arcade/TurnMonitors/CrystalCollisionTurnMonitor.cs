using System; // Required for Action
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] protected int CrystalCollisions;
        protected IRoundStats ownStats;

        [Header("Optional Configuration")]
        [SerializeField] SpawnableWaypointTrack optionalEnvironment;
        [SerializeField] int optionalLaps = 4;

        public override void StartMonitor()
        {
            CrystalCollisions = GetCrystalCollisionCount();
            
            InitializeOwnStats();
            if (ownStats != null) ownStats.OnCrystalsCollectedChanged += UpdateCrystals;
            UpdateCrystals(ownStats);
            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();
            if (ownStats != null) ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
        }
        
        public override bool CheckForEndOfTurn()
        {
            if (ownStats == null) return false;
            return ownStats.CrystalsCollected >= CrystalCollisions;
        }
        
        protected virtual void UpdateCrystals(IRoundStats stats) => UpdateCrystalsRemainingUI();

        protected virtual void UpdateCrystalsRemainingUI()
        {
            string message = GetRemainingCrystalsCountToCollect();
            if (onUpdateTurnMonitorDisplay) onUpdateTurnMonitorDisplay.Raise(message);
        }
        
        public string GetRemainingCrystalsCountToCollect()
        {
            InitializeOwnStats();
            if (ownStats == null) return CrystalCollisions.ToString();
            int remaining = CrystalCollisions - ownStats.CrystalsCollected;
            return Mathf.Max(0, remaining).ToString();
        }

        protected virtual void InitializeOwnStats()
        {
            if (ownStats != null) return;
            if (gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats)) return;
        }

        protected int GetCrystalCollisionCount()
        {
            if (optionalEnvironment)
            {
                return optionalEnvironment.waypoints[optionalEnvironment.intenstyLevel - 1].positions.Count * optionalLaps;
            }

            if (CrystalCollisions != 0) return CrystalCollisions;
            Debug.LogWarning($"[CrystalCollisionTurnMonitor] No crystal collision count set for {gameObject.name}. Defaulting to 39.");
            return 39;

        }
    }
}