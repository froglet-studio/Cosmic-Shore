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
    ///
    /// End-of-turn check is team-aware: a team's pooled <c>CrystalsCollected</c> hitting
    /// the target ends the race. In co-op (multiple humans on the same Domain) this means
    /// teammates progress together; in solo/independent-team play each player is their
    /// own team so the behavior matches the legacy "first individual to target" rule.
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

            // Team-aware: pool CrystalsCollected per Domain. The first team whose
            // pooled total reaches the target ends the race. Single-player teams
            // collapse to the original "first individual to target" rule.
            return gameData.RoundStatsList
                .GroupBy(s => s.Domain)
                .Any(g => g.Sum(s => s.CrystalsCollected) >= target);
        }

        protected override void UpdateCrystalsRemainingUI()
        {
            int target = _netCrystalCollisions.Value > 0
                ? _netCrystalCollisions.Value
                : CrystalCollisions;

            // Show remaining for the local player's TEAM, not just the local individual,
            // so co-op teammates see shared progress. Falls back to local individual
            // when team membership isn't resolvable.
            int current = ResolveLocalTeamCrystalTotal();
            int remaining = Mathf.Max(0, target - current);

            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }

        int ResolveLocalTeamCrystalTotal()
        {
            if (gameData?.RoundStatsList == null || ownStats == null)
                return ownStats?.CrystalsCollected ?? 0;

            var domain = ownStats.Domain;
            if (domain == Domains.Unassigned || domain == Domains.None)
                return ownStats.CrystalsCollected;

            return gameData.RoundStatsList
                .Where(s => s != null && s.Domain == domain)
                .Sum(s => s.CrystalsCollected);
        }
    }
}
