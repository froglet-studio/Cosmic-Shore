using System.Collections.Generic;
using System.Linq;
using CosmicShore.Game.Arcade.Party;
using UnityEngine;

namespace CosmicShore.Game.UI.Party
{
    /// <summary>
    /// Provides party-specific stats for the final scoreboard:
    /// games won per player, total rounds played.
    /// </summary>
    public class PartyStatsProvider : ScoreboardStatsProvider
    {
        [SerializeField] PartyGameController partyController;

        public override List<StatData> GetStats()
        {
            var stats = new List<StatData>();

            if (partyController == null)
                return stats;

            // Total rounds played
            stats.Add(new StatData
            {
                Label = "Rounds Played",
                Value = partyController.TotalRounds.ToString(),
            });

            // Per-player win counts (sorted by wins descending)
            var players = partyController.PlayerStates
                .OrderByDescending(p => p.GamesWon)
                .ToList();

            foreach (var player in players)
            {
                stats.Add(new StatData
                {
                    Label = player.PlayerName,
                    Value = $"{player.GamesWon} wins",
                });
            }

            // List which games were played each round
            foreach (var result in partyController.RoundResults)
            {
                if (!result.IsCompleted) continue;
                string modeName = PartyGameController.GetMiniGameDisplayName(result.MiniGameMode);
                stats.Add(new StatData
                {
                    Label = $"Round {result.RoundIndex + 1}",
                    Value = $"{modeName} - {result.WinnerName}",
                });
            }

            return stats;
        }
    }
}
