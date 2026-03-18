using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class NetworkDogFightCollisionTurnMonitor : DogFightCollisionTurnMonitor
    {
        [SerializeField] private DogFightController controller;

        public override void StartMonitor()
        {
            base.StartMonitor();

            foreach (var stat in gameData.RoundStatsList)
                stat.OnJoustCollisionChanged += OnHitChanged;
        }

        public override void StopMonitor()
        {
            base.StopMonitor();

            foreach (var stat in gameData.RoundStatsList)
                stat.OnJoustCollisionChanged -= OnHitChanged;
        }

        void OnHitChanged(IRoundStats stats)
        {
            if (IsServer)
            {
                controller?.NotifyHit(stats.Name, stats.JoustCollisions);
            }
            else
            {
                controller?.ReportHitToServer(stats.Name, stats.JoustCollisions);
            }
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            return gameData.RoundStatsList
                .Any(stats => stats.JoustCollisions >= HitsNeeded);
        }
    }
}
