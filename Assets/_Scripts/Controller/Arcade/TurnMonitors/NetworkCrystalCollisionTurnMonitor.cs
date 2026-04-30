// NetworkCrystalCollisionTurnMonitor.cs
using System.Linq;
using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Network-aware crystal collection turn monitor. After <c>base.StartMonitor()</c>
    /// resolves the crystal target (from inspector override, waypoints, or default),
    /// this subclass syncs it to all clients via NetworkVariable and publishes it
    /// to <see cref="GameDataSO.CrystalTargetCount"/> so any system can read it.
    /// </summary>
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        private readonly NetworkVariable<int> _netCrystalCollisions = new NetworkVariable<int>(0);

        void OnEnable()
        {
            _netCrystalCollisions.OnValueChanged += OnCrystalTargetSynced;
        }

        void OnDisable()
        {
            _netCrystalCollisions.OnValueChanged -= OnCrystalTargetSynced;
        }

        /// <summary>
        /// Fires on all clients when the server writes to <c>_netCrystalCollisions</c>.
        /// Keeps <see cref="GameDataSO.CrystalTargetCount"/> in sync across all machines.
        /// </summary>
        void OnCrystalTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.CrystalTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            // Base resolves the target: CrystalCollisions (inspector) > waypoints > 39
            base.StartMonitor();

            if (!IsServer) return;

            _netCrystalCollisions.Value = CrystalCollisions;
            gameData.CrystalTargetCount = CrystalCollisions;

            CSDebug.Log($"[NetworkCrystalMonitor] Server set crystal target: {CrystalCollisions} " +
                      $"(intensity={gameData.SelectedIntensity.Value})");
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
