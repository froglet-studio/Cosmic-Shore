using System.Linq;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    public class HexRaceHUD : MultiplayerHUD
    {
        /// <summary>
        /// Each card shows the player's TEAM-pooled OmniCrystalsCollected so co-op
        /// teammates see shared progress. Solo / independent-team players are their
        /// own team, so the pooled count equals their individual count.
        /// </summary>
        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return GetTeamCrystalTotal(stats.Domain);
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            stats.OnOmniCrystalsCollectedChanged += HandleCrystalStatChanged;
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            stats.OnOmniCrystalsCollectedChanged -= HandleCrystalStatChanged;
        }

        /// <summary>
        /// When any teammate's crystal count changes, refresh ALL teammates on the same
        /// Domain so their cards display the new team total. The win condition is
        /// team-pooled (see <see cref="Gameplay.NetworkCrystalCollisionTurnMonitor"/>),
        /// so the cards mirror what the win condition is actually counting.
        /// </summary>
        private void HandleCrystalStatChanged(IRoundStats updatedStats)
        {
            if (gameData?.RoundStatsList == null || updatedStats == null) return;

            int teamTotal = GetTeamCrystalTotal(updatedStats.Domain);

            foreach (var teammate in gameData.RoundStatsList)
            {
                if (teammate == null) continue;
                if (teammate.Domain != updatedStats.Domain) continue;
                UpdatePlayerCard(teammate.Name, teamTotal);
            }
        }

        int GetTeamCrystalTotal(Domains domain)
        {
            if (gameData?.RoundStatsList == null) return 0;

            return gameData.RoundStatsList
                .Where(s => s != null && s.Domain == domain)
                .Sum(s => s.OmniCrystalsCollected);
        }
    }
}
