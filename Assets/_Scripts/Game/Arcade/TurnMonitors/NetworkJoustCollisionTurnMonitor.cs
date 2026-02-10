using Unity.Netcode;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class NetworkJoustCollisionTurnMonitor : JoustCollisionTurnMonitor
    {
        [SerializeField] private MultiplayerJoustController controller;
        public override bool CheckForEndOfTurn()
        {
            // Only check on server
            if (!IsServer)
                return false;
                
            var winner = gameData.RoundStatsList.FirstOrDefault(stats => stats.JoustCollisions >= CollisionsNeeded);

            if (winner == null) return false;
            Debug.Log($"[NetworkJoustCollisionTurnMonitor] Winner detected: {winner.Name} with {winner.JoustCollisions} collisions");
            return true;

        }
        
        protected override void OnTurnEnded()
        {
            base.OnTurnEnded();
    
            var winner = gameData.RoundStatsList.FirstOrDefault(stats => stats.JoustCollisions >= CollisionsNeeded);
            if (!controller) return;
            controller.OnTurnEndedByMonitor(winner?.Name);
        }
    }
}