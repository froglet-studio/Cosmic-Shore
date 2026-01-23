using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] int CrystalCollisions;
        [SerializeField] bool hostileCollection;

        IRoundStats roundStats;
        
        public override void StartMonitor()
        {
            if (!gameData.TryGetLocalPlayerStats(out IPlayer _, out roundStats))
                Debug.LogError("No round stats found for local player");
            
            base.StartMonitor();
        }
        
        public override bool CheckForEndOfTurn() =>
            roundStats.OmniCrystalsCollected >= CrystalCollisions;

        protected override void RestrictedUpdate()
        {
            base.RestrictedUpdate();
            UpdateCrystalsRemainingUI();
        }

        protected virtual void UpdateCrystalsRemainingUI() =>
            InvokeUpdateTurnMonitorDisplay(GetRemainingCrystalsCountToCollect());
        
        protected void InvokeUpdateTurnMonitorDisplay(string message) =>
            onUpdateTurnMonitorDisplay?.Raise(message);
        
        protected string GetRemainingCrystalsCountToCollect() =>
            (CrystalCollisions - roundStats.OmniCrystalsCollected).ToString();
    }
}