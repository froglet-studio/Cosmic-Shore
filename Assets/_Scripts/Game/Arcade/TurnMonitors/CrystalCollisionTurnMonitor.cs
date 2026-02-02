using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] protected int CrystalCollisions;
        [SerializeField] bool hostileCollection;

        IRoundStats ownStats;

        public override void StartMonitor()
        {
            InitializeOwnStats();
            ownStats.OnCrystalsCollectedChanged += UpdateCrystals;
            UpdateCrystals(ownStats);
            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();
            ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
        }
        
        public override bool CheckForEndOfTurn() =>
            ownStats.CrystalsCollected >= CrystalCollisions;
        
        protected void UpdateCrystals(IRoundStats stats) =>
            UpdateCrystalsRemainingUI();

        protected void UpdateCrystalsRemainingUI()
        {
            string message = GetRemainingCrystalsCountToCollect();
            onUpdateTurnMonitorDisplay?.Raise(message);
        }
        
        string GetRemainingCrystalsCountToCollect()
        {
            InitializeOwnStats();
            return (CrystalCollisions - ownStats.CrystalsCollected).ToString();
        }

        void InitializeOwnStats()
        {
            if (ownStats != null) return;
            if (gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats)) return;
            Debug.LogError("No round stats found for local player");
        }
    }
}