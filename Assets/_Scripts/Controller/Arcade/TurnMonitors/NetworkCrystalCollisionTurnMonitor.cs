// NetworkCrystalCollisionTurnMonitor.cs
using System.Collections.Generic;
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

        // Stats this monitor actually subscribed to. Unsubscription must run off THIS
        // list, never gameData.RoundStatsList: on a mid-turn scene exit, SceneLoader's
        // ResetRuntimeData clears the roster BEFORE the old scene's objects are
        // destroyed, so a list-based unsubscribe loop detaches nothing and the handler
        // leaks onto the persistent human RoundStats (Docs/ScoringSystem/BUGS.md B15).
        readonly List<IRoundStats> _subscribedStats = new();

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

            // Subscribe to every player's crystal-changed event so the HUD displays the
            // up-to-date DOMAIN sum (not just the local player's count). Base only
            // wires the local player; teammates' collections would otherwise stay invisible.
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null || _subscribedStats.Contains(stats)) continue;
                stats.OnCrystalsCollectedChanged += OnAnyCrystalChanged;
                _subscribedStats.Add(stats);
            }

            if (!IsServer) return;

            _netCrystalCollisions.Value = CrystalCollisions;
            gameData.CrystalTargetCount = CrystalCollisions;

            CSDebug.Log($"[NetworkCrystalMonitor] Server set crystal target: {CrystalCollisions} " +
                      $"(intensity={gameData.SelectedIntensity.Value})");
        }

        public override void StopMonitor()
        {
            foreach (var stats in _subscribedStats)
            {
                if (stats == null) continue;
                stats.OnCrystalsCollectedChanged -= OnAnyCrystalChanged;
            }
            _subscribedStats.Clear();
            base.StopMonitor();
        }

        public override void OnDestroy()
        {
            // Safety net for destruction paths that bypass StopMonitor - detaching
            // from the persistent RoundStats must never depend on the turn ending.
            StopMonitor();
            base.OnDestroy();
        }

        void OnAnyCrystalChanged(IRoundStats _) => UpdateCrystalsRemainingUI();

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule. HexRace and Crystal Capture
            // both end when an active domain's summed metric reaches the target; the trigger is
            // per-team so AI and human teammates finish the objective together.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void UpdateCrystalsRemainingUI()
        {
            // Remaining is the LOCAL PLAYER'S DOMAIN deficit (the rule owns target + sum), so
            // the HUD reflects the team objective rather than a single ship. A null local
            // player resolves to Domains.Blue (sum 0 → full target remaining), matching the
            // previous behavior.
            int remaining = gameData.ScoringRule.Remaining(
                gameData, gameData.LocalPlayer?.Domain ?? Domains.Blue);

            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}
