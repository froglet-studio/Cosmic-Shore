// NetworkJoustCollisionTurnMonitor.cs
using Unity.Netcode;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class NetworkJoustCollisionTurnMonitor : JoustCollisionTurnMonitor
    {
        [SerializeField] private MultiplayerJoustController controller;

        public override void StartMonitor()
        {
            base.StartMonitor();

            // ALL machines subscribe — client needs to report its own collisions up to server
            foreach (var stat in gameData.RoundStatsList)
                stat.OnJoustCollisionChanged += OnCollisionChanged;
        }

        public override void StopMonitor()
        {
            base.StopMonitor();

            foreach (var stat in gameData.RoundStatsList)
                stat.OnJoustCollisionChanged -= OnCollisionChanged;
        }

        void OnCollisionChanged(IRoundStats stats)
        {
            if (IsServer)
            {
                // Server detects it directly — notify controller to sync down to clients
                controller?.NotifyCollision(stats.Name, stats.JoustCollisions);
            }
            else
            {
                // Client detected a collision the server missed (high-speed physics) 
                // — report it up so the server can authoritative sync everyone
                controller?.ReportCollisionToServer(stats.Name, stats.JoustCollisions);
            }
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