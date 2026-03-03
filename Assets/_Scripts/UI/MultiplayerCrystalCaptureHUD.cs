using System;
using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    public class MultiplayerCrystalCaptureHUD : MultiplayerHUD
    {
        private readonly Dictionary<IRoundStats, Action> _scoreChangeHandlers = new();

        protected override void OnDisable()
        {
            base.OnDisable();
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
            if (stats == null || !_scoreChangeHandlers.TryGetValue(stats, out var handler)) return;
            stats.OnScoreChanged -= handler;
            _scoreChangeHandlers.Remove(stats);
        }

    }
}