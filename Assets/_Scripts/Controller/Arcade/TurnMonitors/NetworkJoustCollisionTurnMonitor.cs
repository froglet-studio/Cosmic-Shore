// NetworkJoustCollisionTurnMonitor.cs
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
        public override void StartMonitor()
        {
            base.StartMonitor();

            CSDebug.Log($"[NetworkJoustMonitor] StartMonitor — IsServer={IsServer}, " +
                $"CollisionsNeeded={CollisionsNeeded}, " +
                $"Players={gameData.RoundStatsList.Count}, " +
                $"Names=[{string.Join(", ", gameData.RoundStatsList.Select(s => s.Name))}]");

            // ALL machines subscribe — client needs to report its own collisions up to server
            foreach (var stat in gameData.RoundStatsList)
                stat.OnJoustCollisionChanged += OnCollisionChanged;
        }

        public override void StopMonitor()
        {
            foreach (var stat in gameData.RoundStatsList)
                stat.OnJoustCollisionChanged -= OnCollisionChanged;

            base.StopMonitor();
        }

        void OnCollisionChanged(IRoundStats stats)
        {
            if (IsServer)
            {
                // Server already has the correct local value from the setter —
                // just broadcast to clients. Do NOT re-assign JoustCollisions here
                // or it will re-trigger this handler and cause infinite recursion.
                SyncCollision_ClientRpc(stats.Name, stats.JoustCollisions);
            }
            else
            {
                // Client detected a collision the server missed (high-speed physics)
                // — report it up so the server can authoritatively sync everyone
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
            // Server already has the correct value — only clients need the update.
            if (IsServer) return;
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats)) return;
            stats.JoustCollisions = collisionCount;
        }

        public override bool CheckForEndOfTurn()
        {
            // Only server ends the turn authoritatively
            if (!IsServer) return false;

            return gameData.RoundStatsList
                .Any(stats => stats.JoustCollisions >= CollisionsNeeded);
        }
    }
}
