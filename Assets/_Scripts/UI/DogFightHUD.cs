namespace CosmicShore.Game.UI
{
    public class DogFightHUD : MultiplayerHUD
    {
        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.DogFightHits;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnDogFightHitChanged += HandleHitStatChanged;
            }
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnDogFightHitChanged -= HandleHitStatChanged;
            }
        }

        private void HandleHitStatChanged(IRoundStats updatedStats)
        {
            if (updatedStats != null)
            {
                UpdatePlayerCard(updatedStats.Name, updatedStats.DogFightHits);
            }
        }
    }
}
