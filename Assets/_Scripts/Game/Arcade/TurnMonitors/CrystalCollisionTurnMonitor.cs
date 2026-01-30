using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] protected int CrystalCollisions;
        [SerializeField] bool hostileCollection;

        IRoundStats ownStats;
        
        public override bool CheckForEndOfTurn() =>
            ownStats.CrystalsCollected >= CrystalCollisions;

        protected override void RestrictedUpdate()
        {
            base.RestrictedUpdate();
            UpdateCrystalsRemainingUI();
        }

        protected virtual void UpdateCrystalsRemainingUI() =>
            InvokeUpdateTurnMonitorDisplay(GetRemainingCrystalsCountToCollect());
        
        protected void InvokeUpdateTurnMonitorDisplay(string message) =>
            onUpdateTurnMonitorDisplay?.Raise(message);

        protected string GetRemainingCrystalsCountToCollect()
        {
            if (ownStats == null)
            {
                if (!gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats))
                {
                    Debug.LogError("No round stats found for local player");
                    return string.Empty;
                }
            }
            
            return (CrystalCollisions - ownStats.CrystalsCollected).ToString();
        }
    }
}