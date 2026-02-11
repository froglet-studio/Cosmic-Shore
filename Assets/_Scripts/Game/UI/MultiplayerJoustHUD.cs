using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class MultiplayerJoustHUD : MultiplayerHUD
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllPlayerCards;
                gameData.OnResetForReplay.OnRaised += OnReplayReset;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllPlayerCards;
                gameData.OnResetForReplay.OnRaised -= OnReplayReset;
            }
        }

        /// <summary>
        /// Called when replay reset happens - reset all collision counts to 0
        /// </summary>
        void OnReplayReset()
        {
            if (gameData?.RoundStatsList == null) return;

            // Reset all player cards to 0 collisions
            foreach (var stats in gameData.RoundStatsList.Where(stats => stats != null))
            {
                UpdatePlayerCard(stats.Name, 0);
            }
        }

        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.JoustCollisions;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnJoustCollisionChanged += HandleJoustStatChanged;
            }
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnJoustCollisionChanged -= HandleJoustStatChanged;
            }
        }

        private void HandleJoustStatChanged(IRoundStats updatedStats)
        {
            if (updatedStats != null)
            {
                UpdatePlayerCard(updatedStats.Name, updatedStats.JoustCollisions);
            }
        }

        /// <summary>
        /// Refresh all player cards - useful when game starts or resets
        /// </summary>
        void RefreshAllPlayerCards()
        {
            if (gameData?.RoundStatsList == null) return;

            foreach (var stats in gameData.RoundStatsList.Where(stats => stats != null))
            {
                UpdatePlayerCard(stats.Name, stats.JoustCollisions);
            }
        }
    }
}