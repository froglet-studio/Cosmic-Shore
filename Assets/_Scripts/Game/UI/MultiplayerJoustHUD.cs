using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class MultiplayerJoustHUD : MultiplayerHUD
    {
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
    }
}