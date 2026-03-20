namespace CosmicShore.Game.UI
{
    public class NeedleThreadHUD : MultiplayerHUD
    {
        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return (int)stats.HostileVolumeDestroyed;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnHostileVolumeDestroyedChanged += HandleVolumeStatChanged;
            }
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats != null)
            {
                stats.OnHostileVolumeDestroyedChanged -= HandleVolumeStatChanged;
            }
        }

        private void HandleVolumeStatChanged(IRoundStats updatedStats)
        {
            if (updatedStats != null)
            {
                UpdatePlayerCard(updatedStats.Name, (int)updatedStats.HostileVolumeDestroyed);
            }
        }
    }
}
