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
            string message = (CrystalCollisions - roundStats.OmniCrystalsCollected).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}