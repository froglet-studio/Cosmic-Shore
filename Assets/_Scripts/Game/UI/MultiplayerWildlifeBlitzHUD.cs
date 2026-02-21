namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Multiplayer HUD for co-op Wildlife Blitz.
    /// Shows per-player kill count cards during gameplay.
    /// Uses BlocksDestroyed as the kill counter (co-op reuse of this stat field).
    /// </summary>
    public class MultiplayerWildlifeBlitzHUD : MultiplayerHUD
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
