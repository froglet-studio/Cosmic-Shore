using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        private readonly NetworkVariable<int> _netCrystalCollisions = new NetworkVariable<int>(value: 0);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                // Server sets the authoritative value from the Inspector
                _netCrystalCollisions.Value = CrystalCollisions;
            }
        }

        public override void StartMonitor()
        {
            if (IsSpawned)
            {
                base.StartMonitor();
            }
        }

        public override bool CheckForEndOfTurn()
        {
            if (ownStats == null) return false;
            return ownStats.CrystalsCollected >= _netCrystalCollisions.Value;
        }
        
        protected override void UpdateCrystalsRemainingUI()
        {
            int target = _netCrystalCollisions.Value > 0 ? _netCrystalCollisions.Value : CrystalCollisions;
            
            int current = ownStats?.CrystalsCollected ?? 0;
            int remaining = Mathf.Max(0, target - current);
            
            if (onUpdateTurnMonitorDisplay) 
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}