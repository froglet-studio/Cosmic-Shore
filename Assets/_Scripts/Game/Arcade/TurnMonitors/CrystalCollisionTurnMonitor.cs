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

        public event Action OnTurnFinished; 

        public override void StartMonitor()
        {
            if (optionalEnvironment)
            {
                CrystalCollisions = optionalEnvironment.waypoints[optionalEnvironment.intenstyLevel - 1].positions.Count * optionalLaps;
            }
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
            bool isFinished = ownStats.CrystalsCollected >= CrystalCollisions;
            if (isFinished)
            {
                OnTurnFinished?.Invoke();
            }
            
            return isFinished;
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
    }
}