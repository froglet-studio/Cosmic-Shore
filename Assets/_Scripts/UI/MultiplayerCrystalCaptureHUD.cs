using System.Linq;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    public class MultiplayerCrystalCaptureHUD : MultiplayerHUD
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            if (gameData != null)
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllPlayerCards;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (gameData != null)
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllPlayerCards;
        }

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
            UpdatePlayerCard(stats.Name, stats.CrystalsCollected);
        }

        void RefreshAllPlayerCards()
        {
            if (gameData?.RoundStatsList == null) return;

            foreach (var stats in gameData.RoundStatsList.Where(stats => stats != null))
            {
                UpdatePlayerCard(stats.Name, stats.CrystalsCollected);
            }
        }
    }
}
