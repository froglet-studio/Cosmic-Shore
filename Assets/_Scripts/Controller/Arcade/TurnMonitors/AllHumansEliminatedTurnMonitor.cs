// AllHumansEliminatedTurnMonitor.cs
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative Friction turn-ender: fires when every human player's
    /// RoundStats.IsEliminated is true (hunters chased down the whole party before
    /// anyone reached the crystal target or time ran out). AI hunters are excluded
    /// via IPlayer.IsInitializedAsAI, cross-referenced by RoundStats.Name.
    /// </summary>
    public class AllHumansEliminatedTurnMonitor : TurnMonitor
    {
        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            var humanStats = gameData.RoundStatsList
                .Where(stats => gameData.Players.Any(p => p.Name == stats.Name && !p.IsInitializedAsAI))
                .ToList();

            if (humanStats.Count == 0) return false;

            return humanStats.All(stats => stats.IsEliminated);
        }
    }
}
