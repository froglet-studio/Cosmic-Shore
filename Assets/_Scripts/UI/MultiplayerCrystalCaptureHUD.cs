using CosmicShore.Data;

namespace CosmicShore.UI
{
    public class MultiplayerCrystalCaptureHUD : MultiplayerHUD
    {
        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.CrystalsCollected;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats == null) return;
            stats.OnCrystalsCollectedChanged += HandleCrystalChanged;
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats == null) return;
            stats.OnCrystalsCollectedChanged -= HandleCrystalChanged;
        }

        void HandleCrystalChanged(IRoundStats stats)
        {
            HandlePlayerStatChanged(stats);
        }
    }
}
