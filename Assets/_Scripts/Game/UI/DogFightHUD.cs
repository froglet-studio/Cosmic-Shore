namespace CosmicShore.Game.UI
{
    public class DogFightHUD : MultiplayerHUD
    {
        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.JoustCollisions;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnJoustCollisionChanged += HandleHitStatChanged;
            }
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnJoustCollisionChanged -= HandleHitStatChanged;
            }
        }

        private void HandleHitStatChanged(IRoundStats updatedStats)
        {
            if (updatedStats != null)
            {
                UpdatePlayerCard(updatedStats.Name, updatedStats.JoustCollisions);
            }
        }
    }
}
