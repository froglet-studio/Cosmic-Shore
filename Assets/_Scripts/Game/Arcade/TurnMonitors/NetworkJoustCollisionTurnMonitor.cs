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
            if (IsServer)
            {
                foreach (var stat in gameData.RoundStatsList)
                    stat.OnJoustCollisionChanged += ServerSideCollisionSync;
            }
        }

        public override void StopMonitor()
        {
            base.StopMonitor();
            if (IsServer)
            {
                foreach (var stat in gameData.RoundStatsList)
                    stat.OnJoustCollisionChanged -= ServerSideCollisionSync;
            }
        }
        
        void ServerSideCollisionSync(IRoundStats stats)
        {
            if (controller)
                controller.NotifyCollision(stats.Name, stats.JoustCollisions);
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;
            return gameData.RoundStatsList.Any(stats => stats.JoustCollisions >= CollisionsNeeded);
        }
        
        protected override void OnTurnEnded()
        {
            base.OnTurnEnded();
            var winner = gameData.RoundStatsList.FirstOrDefault(stats => stats.JoustCollisions >= CollisionsNeeded);
            if (controller) controller.OnTurnEndedByMonitor(winner?.Name);
        }
    }
}