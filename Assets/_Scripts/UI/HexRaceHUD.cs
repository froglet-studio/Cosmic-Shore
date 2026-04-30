using CosmicShore.Data;

namespace CosmicShore.UI
{
    public class HexRaceHUD : MultiplayerHUD
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
            if (updatedStats == null) return;
            // Use the stats-reference-keyed update path so live score updates still
            // land on the right card even if NetName replicates after the card was
            // created (which would have made the legacy name-keyed lookup miss).
            UpdatePlayerCard(updatedStats, updatedStats.OmniCrystalsCollected);
        }
    }
}
