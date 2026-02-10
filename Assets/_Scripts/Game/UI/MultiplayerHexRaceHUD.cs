namespace CosmicShore.Game.UI
{
    public class MultiplayerHexRaceHUD : MultiplayerHUD
    {
        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.OmniCrystalsCollected;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            stats.OnOmniCrystalsCollectedChanged += HandleCrystalStatChanged;
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            stats.OnOmniCrystalsCollectedChanged -= HandleCrystalStatChanged;
        }

        private void HandleCrystalStatChanged(IRoundStats updatedStats)
        {
            UpdatePlayerCard(updatedStats.Name, updatedStats.OmniCrystalsCollected);
        }
    }
}