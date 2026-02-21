namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Co-op HUD for Wildlife Blitz.
    /// Shows per-player kill count cards during gameplay.
    /// Used when multiple players are present; the existing WildlifeBlitzHUD
    /// handles the single-player score/lifeform display.
    /// </summary>
    public class WildlifeBlitzCoOpHUD : MultiplayerHUD
    {
        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.BlocksDestroyed;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnBlocksDestroyedChanged += HandleKillStatChanged;
            }
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnBlocksDestroyedChanged -= HandleKillStatChanged;
            }
        }

        private void HandleKillStatChanged(IRoundStats updatedStats)
        {
            if (updatedStats != null)
            {
                UpdatePlayerCard(updatedStats.Name, updatedStats.BlocksDestroyed);
            }
        }
    }
}
