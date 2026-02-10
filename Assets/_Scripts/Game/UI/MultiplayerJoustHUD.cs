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
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllPlayerCards;
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