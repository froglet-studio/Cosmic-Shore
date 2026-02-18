// =======================================================
// NetworkCrystalCollisionTurnMonitor.cs  (FINAL / FIXED)
// - Still updates local UI via base (ownStats)
// - On SERVER: subscribes to ALL players' OnCrystalsCollectedChanged and pushes updates into controller
// - On SERVER: sets networked crystals-to-finish target into controller (authoritative)
// - CheckForEndOfTurn is SERVER-only and ends when ANY player reaches target
// =======================================================

using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        [SerializeField] private MultiplayerHexRaceController controller;

        private readonly NetworkVariable<int> _netCrystalCollisions = new NetworkVariable<int>(0);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            int target = GetCrystalCollisionCount();
            _netCrystalCollisions.Value = target;

            // Ensure controller uses the same target as the monitor
            controller?.SetCrystalsToFinishServer(target);
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (!IsServer) return;

            foreach (var stat in gameData.RoundStatsList)
                stat.OnCrystalsCollectedChanged += ServerSideCrystalSync;

            // Push initial values
            foreach (var stat in gameData.RoundStatsList)
                ServerSideCrystalSync(stat);
        }

        public override void StopMonitor()
        {
            if (IsServer)
            {
                foreach (var stat in gameData.RoundStatsList)
                    stat.OnCrystalsCollectedChanged -= ServerSideCrystalSync;
            }

            base.StopMonitor();
        }

        void ServerSideCrystalSync(IRoundStats stats)
        {
            if (!IsServer) return;
            controller?.NotifyCrystalsCollected(stats.Name, stats.CrystalsCollected);
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            int target = _netCrystalCollisions.Value > 0 ? _netCrystalCollisions.Value : CrystalCollisions;
            return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target);
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
