using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class JoustCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] protected int CollisionsNeeded;

        IRoundStats ownStats;

        public override void StartMonitor()
        {
            InitializeOwnStats();
            ownStats.OnJoustCollisionChanged += UpdateJoustCollisions;
            UpdateJoustCollisions(ownStats);
            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();
            ownStats.OnJoustCollisionChanged -= UpdateJoustCollisions;
        }
        
        public override bool CheckForEndOfTurn() =>
            ownStats.JoustCollisions >= CollisionsNeeded;
        
        protected void UpdateJoustCollisions(IRoundStats stats) =>
            UpdateJoustCollisionRemainingUI();

        protected void UpdateJoustCollisionRemainingUI()
        {
            string message = GetRemainingJoustCollisionCount();
            onUpdateTurnMonitorDisplay?.Raise(message);
        }
        
        string GetRemainingJoustCollisionCount()
        {
            InitializeOwnStats();
            return (CollisionsNeeded - ownStats.JoustCollisions).ToString();
        }

        void InitializeOwnStats()
        {
            if (ownStats != null) return;
            if (gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats)) return;
            Debug.LogError("No round stats found for local player");
        }
    }
}