// NetworkCrystalCollisionTurnMonitor.cs
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        [SerializeField] private HexRaceController controller;

        private readonly NetworkVariable<int> _netCrystalCollisions = new NetworkVariable<int>(0);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (!IsEffectiveServer) return;
            int target = GetCrystalCollisionCount();

            // In party mode, skip NetworkVariable write (IsSpawned unreliable after
            // env deactivation/reactivation). SetCrystalsToFinishServer stores locally.
            if (!(gameData != null && gameData.IsPartyMode))
                _netCrystalCollisions.Value = target;

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
            if (IsEffectiveServer)
            {
                foreach (var stat in gameData.RoundStatsList)
                    stat.OnCrystalsCollectedChanged -= ServerSideCrystalSync;
            }

            base.StopMonitor();
        }

        void ServerSideCrystalSync(IRoundStats stats)
        {
            if (!IsEffectiveServer) return;
            controller?.NotifyCrystalsCollected(stats.Name, stats.CrystalsCollected);
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsEffectiveServer) return false;

            int target = _netCrystalCollisions.Value > 0
                ? _netCrystalCollisions.Value
                : CrystalCollisions;

            return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target);
        }

        protected override void UpdateCrystalsRemainingUI()
        {
            int target = _netCrystalCollisions.Value > 0
                ? _netCrystalCollisions.Value
                : CrystalCollisions;

            int current = ownStats?.CrystalsCollected ?? 0;
            int remaining = Mathf.Max(0, target - current);

            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}