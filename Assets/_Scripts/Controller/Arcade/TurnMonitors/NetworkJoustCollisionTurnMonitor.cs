// NetworkJoustCollisionTurnMonitor.cs
using System.Collections.Generic;
using CosmicShore.Data;
using Unity.Netcode;
using System.Linq;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Network-aware joust collision turn monitor. Owns the collision sync RPCs
    /// so that no direct reference to any game controller is needed.
    /// The monitor detects collisions locally, syncs them across the network,
    /// and ends the turn when any player reaches <see cref="CollisionsNeeded"/>.
    /// </summary>
    public class NetworkJoustCollisionTurnMonitor : JoustCollisionTurnMonitor
    {
        // Stats this monitor actually subscribed to. Unsubscription must run off THIS
        // list, never gameData.RoundStatsList: on a mid-turn scene exit, SceneLoader's
        // ResetRuntimeData clears the roster BEFORE the old scene's objects are
        // destroyed, so a list-based unsubscribe loop detaches nothing and the handlers
        // leak onto the persistent human RoundStats (Docs/ScoringSystem/BUGS.md B15).
        readonly List<IRoundStats> _subscribedStats = new();

        public override void StartMonitor()
        {
            base.StartMonitor();

            CSDebug.Log($"[NetworkJoustMonitor] StartMonitor - IsServer={IsServer}, " +
                $"CollisionsNeeded={CollisionsNeeded}, " +
                $"Players={gameData.RoundStatsList.Count}, " +
                $"Names=[{string.Join(", ", gameData.RoundStatsList.Select(s => s.Name))}]");

            // ALL machines subscribe - client needs to report its own collisions up to server,
            // and the HUD's "jousts remaining" readout needs to reflect the local player's
            // DOMAIN aggregate, which changes whenever ANY teammate jousts.
            foreach (var stat in gameData.RoundStatsList)
            {
                if (stat == null || _subscribedStats.Contains(stat)) continue;
                stat.OnJoustCollisionChanged += OnCollisionChanged;
                stat.OnJoustCollisionChanged += OnAnyJoustChangedUI;
                _subscribedStats.Add(stat);
            }

            UpdateDomainRemainingUI();
        }

        public override void StopMonitor()
        {
            foreach (var stat in _subscribedStats)
            {
                if (stat == null) continue;
                stat.OnJoustCollisionChanged -= OnCollisionChanged;
                stat.OnJoustCollisionChanged -= OnAnyJoustChangedUI;
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

        void OnAnyJoustChangedUI(IRoundStats _) => UpdateDomainRemainingUI();

        void UpdateDomainRemainingUI()
        {
            if (gameData.LocalPlayer == null) return;
            // Remaining = local player's DOMAIN joust deficit (the rule owns target + sum).
            int remaining = gameData.ScoringRule.Remaining(gameData, gameData.LocalPlayer.Domain);
            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }

        void OnCollisionChanged(IRoundStats stats)
        {
            if (IsServer)
            {
                // Server already has the correct local value from the setter -
                // just broadcast to clients. Do NOT re-assign JoustCollisions here
                // or it will re-trigger this handler and cause infinite recursion.
                SyncCollision_ClientRpc(stats.Name, stats.JoustCollisions);
            }
            else
            {
                // Client detected a collision the server missed (high-speed physics)
                // - report it up so the server can authoritatively sync everyone
                ReportCollision_ServerRpc(stats.Name, stats.JoustCollisions);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportCollision_ServerRpc(string playerName, int collisionCount)
        {
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats == null)
            {
                CSDebug.LogError($"[NetworkJoustMonitor] ServerRpc: no stats for '{playerName}'");
                return;
            }

            // Only accept if the client reports a higher count (prevent stale/duplicate reports)
            if (collisionCount <= stats.JoustCollisions) return;

            stats.JoustCollisions = collisionCount;
            SyncCollision_ClientRpc(playerName, collisionCount);
        }

        [ClientRpc]
        void SyncCollision_ClientRpc(string playerName, int collisionCount)
        {
            // Server already has the correct value - only clients need the update.
            if (IsServer) return;
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats)) return;
            stats.JoustCollisions = collisionCount;
        }

        public override bool CheckForEndOfTurn()
        {
            // Only server ends the turn authoritatively
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: an active domain's summed
            // jousts reaching the target. Domain teammates finish the objective together.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }
    }
}
