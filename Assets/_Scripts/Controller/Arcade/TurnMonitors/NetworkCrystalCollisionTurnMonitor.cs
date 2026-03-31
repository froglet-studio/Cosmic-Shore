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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (!IsServer) return;

            int overrideTarget = controller != null ? controller.GetTestCrystalOverride() : -1;
            int target = overrideTarget > 0 ? overrideTarget : GetCrystalCollisionCount();

            // When domain crystals are active, each player can only collect their
            // domain's share of the total crystals. Divide the target by the number
            // of distinct player domains so each player needs their fair portion.
            int domainCount = GetDistinctDomainCount();
            if (domainCount > 1)
                target = Mathf.Max(1, target / domainCount);

            _netCrystalCollisions.Value = target;
            controller?.SetCrystalsToFinishServer(target);

            CSDebug.Log($"[NetworkCrystalMonitor] Server set crystal target: {target} " +
                      $"(override={overrideTarget > 0}, intensity={gameData.SelectedIntensity.Value})");

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

            int target = _netCrystalCollisions.Value > 0
                ? _netCrystalCollisions.Value
                : CrystalCollisions;

            return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target);
        }

        int GetDistinctDomainCount()
        {
            if (gameData?.Players == null || gameData.Players.Count == 0)
                return 1;

            return gameData.Players
                .Select(p => p.Domain)
                .Where(d => d is not (Domains.None or Domains.Unassigned))
                .Distinct()
                .Count();
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