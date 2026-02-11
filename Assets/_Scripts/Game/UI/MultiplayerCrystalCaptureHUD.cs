using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Game.UI
{
    public class MultiplayerCrystalCaptureHUD : MultiplayerHUD
    {
        private readonly Dictionary<IRoundStats, Action> _scoreChangeHandlers = new();

        protected override void OnEnable()
        {
            base.OnEnable();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllPlayerCards;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllPlayerCards;
            }
            
            _scoreChangeHandlers.Clear();
        }

        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return (int)stats.Score;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats == null) return;

            // Cache the action delegate so we can safely unsubscribe later
            Action handler = () => UpdatePlayerCard(stats.Name, (int)stats.Score);
            _scoreChangeHandlers[stats] = handler;
            
            stats.OnScoreChanged += handler;
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats != null && _scoreChangeHandlers.TryGetValue(stats, out var handler))
            {
                stats.OnScoreChanged -= handler;
                _scoreChangeHandlers.Remove(stats);
            }
        }

        void RefreshAllPlayerCards()
        {
            if (gameData?.RoundStatsList == null) return;

            // [Visual Note] Iterates through active players and snaps their scoreboard card crystal values to the server-verified count.
            foreach (var stats in gameData.RoundStatsList.Where(stats => stats != null))
            {
                UpdatePlayerCard(stats.Name, (int)stats.Score);
            }
        }
    }
}