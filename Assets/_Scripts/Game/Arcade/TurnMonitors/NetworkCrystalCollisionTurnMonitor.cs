// NetworkCrystalCollisionTurnMonitor.cs
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        [Tooltip("Assign the game controller (must implement ICrystalRaceController, e.g. HexRaceController or DragScoutingController).")]
        [SerializeField] private MonoBehaviour controllerBehaviour;

        private ICrystalRaceController _controller;
        private readonly NetworkVariable<int> _netCrystalCollisions = new NetworkVariable<int>(0);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (controllerBehaviour != null)
                _controller = controllerBehaviour as ICrystalRaceController;

            if (_controller == null && controllerBehaviour != null)
                CSDebug.LogError($"[NetworkCrystalMonitor] Assigned controller '{controllerBehaviour.name}' does not implement ICrystalRaceController.");
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (!IsServer) return;
            int target = GetCrystalCollisionCount();
            _netCrystalCollisions.Value = target;
            _controller?.SetCrystalsToFinishServer(target);

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
            _controller?.NotifyCrystalsCollected(stats.Name, stats.CrystalsCollected);
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

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