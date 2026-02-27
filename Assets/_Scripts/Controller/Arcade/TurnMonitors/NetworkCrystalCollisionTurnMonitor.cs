// NetworkCrystalCollisionTurnMonitor.cs
using System.Linq;
using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        [SerializeField] private HexRaceController controller;

        private readonly NetworkVariable<int> _netCrystalCollisions = new NetworkVariable<int>(0);
        private int _localCrystalTarget;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (!this.IsServerSafe()) return;
            int target = GetCrystalCollisionCount();
            _localCrystalTarget = target;
            if (IsSpawned) _netCrystalCollisions.Value = target;
            controller?.SetCrystalsToFinishServer(target);

            CSDebug.Log($"[NetworkCrystalMonitor] Server set crystal target: {target} " +
                      $"(intensity={gameData.SelectedIntensity.Value})");

            foreach (var stat in gameData.RoundStatsList)
                stat.OnCrystalsCollectedChanged += ServerSideCrystalSync;

            // Push initial values
            foreach (var stat in gameData.RoundStatsList)
                ServerSideCrystalSync(stat);
        }

        public override void StopMonitor()
        {
            if (this.IsServerSafe())
            {
                foreach (var stat in gameData.RoundStatsList)
                    stat.OnCrystalsCollectedChanged -= ServerSideCrystalSync;
            }

            base.StopMonitor();
        }

        void ServerSideCrystalSync(IRoundStats stats)
        {
            if (!this.IsServerSafe()) return;
            controller?.NotifyCrystalsCollected(stats.Name, stats.CrystalsCollected);
        }

        public override bool CheckForEndOfTurn()
        {
            if (!this.IsServerSafe()) return false;

            int target = _netCrystalCollisions.Value > 0
                ? _netCrystalCollisions.Value
                : (_localCrystalTarget > 0 ? _localCrystalTarget : CrystalCollisions);

            return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target);
        }

        protected override void UpdateCrystalsRemainingUI()
        {
            int target = _netCrystalCollisions.Value > 0
                ? _netCrystalCollisions.Value
                : (_localCrystalTarget > 0 ? _localCrystalTarget : CrystalCollisions);

            int current = ownStats?.CrystalsCollected ?? 0;
            int remaining = Mathf.Max(0, target - current);

            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}
