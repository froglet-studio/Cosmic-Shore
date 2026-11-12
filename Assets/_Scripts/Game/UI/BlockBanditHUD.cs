using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Game.UI
{
    public class BlockBanditHUD : MultiplayerHUD
    {
        private readonly Dictionary<IRoundStats, Action<IRoundStats>> _stolenChangeHandlers = new();

        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.PrismStolen;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats == null) return;

            Action<IRoundStats> handler = s => UpdatePlayerCard(s.Name, s.PrismStolen);
            _stolenChangeHandlers[stats] = handler;

            stats.OnPrismsStolenChanged += handler;
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats == null || !_stolenChangeHandlers.TryGetValue(stats, out var handler)) return;
            stats.OnPrismsStolenChanged -= handler;
            _stolenChangeHandlers.Remove(stats);
        }

        protected override void SubscribeToGameSpecificEvents()
        {
            if (gameData != null)
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllPlayerCards;
        }

        protected override void UnsubscribeFromGameSpecificEvents()
        {
            if (gameData != null)
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllPlayerCards;
        }

        void RefreshAllPlayerCards()
        {
            if (gameData?.RoundStatsList == null) return;

            foreach (var stats in gameData.RoundStatsList.Where(stats => stats != null))
            {
                UpdatePlayerCard(stats.Name, stats.PrismStolen);
            }
        }
    }
}
