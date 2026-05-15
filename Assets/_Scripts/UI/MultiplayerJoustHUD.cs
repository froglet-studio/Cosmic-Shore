using CosmicShore.Data;

namespace CosmicShore.UI
{
    public class MultiplayerJoustHUD : MultiplayerHUD
    {
        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.JoustCollisions;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats == null) return;
            stats.OnJoustCollisionChanged += HandleJoustStatChanged;
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats == null) return;
            stats.OnJoustCollisionChanged -= HandleJoustStatChanged;
        }

        private void HandleJoustStatChanged(IRoundStats updatedStats)
        {
            HandlePlayerStatChanged(updatedStats);
        }
    }
}
