using System.Linq;

namespace CosmicShore.Game.Arcade
{
    public class NetworkJoustCollisionTurnMonitor : JoustCollisionTurnMonitor
    {
        public override bool CheckForEndOfTurn() =>
            gameData.RoundStatsList.Any(stats => stats.JoustCollisions >= CollisionsNeeded);
    }
}