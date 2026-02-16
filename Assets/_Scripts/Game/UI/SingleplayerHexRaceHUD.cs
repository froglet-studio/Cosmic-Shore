using System.Collections.Generic;
using UnityEngine.SocialPlatforms.Impl;

namespace CosmicShore.Game.UI
{
    public class SingleplayerHexRaceHUD : MiniGameHUD
    {
        private Dictionary<string, PlayerScoreCard> _playerCards = new();

        protected override void OnMiniGameTurnStarted()
        {
            base.OnMiniGameTurnStarted();
            
            if (isAIAvailable)
                InitializeScoreCards();
        }

        protected override void OnMiniGameTurnEnd()
        {
            base.OnMiniGameTurnEnd();
            CleanupScoreCards();
        }

        private void InitializeScoreCards()
        {
            view.ClearPlayerList();
            _playerCards.Clear();

            foreach (var stats in gameData.RoundStatsList)
            {
                var card = Instantiate(view.PlayerScoreCardPrefab, view.PlayerScoreContainer);
                var isLocal = stats == localRoundStats;
                var teamColor = view.GetColorForDomain(stats.Domain);
                
                card.Setup(stats.Name, stats.OmniCrystalsCollected, teamColor, isLocal);
                _playerCards[stats.Name] = card;

                stats.OnOmniCrystalsCollectedChanged += HandleStatChanged;
            }
        }

        private void HandleStatChanged(IRoundStats stats)
        {
            if (_playerCards.TryGetValue(stats.Name, out var card))
            {
                card.UpdateScore(stats.OmniCrystalsCollected);
            }
        }

        private void CleanupScoreCards()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                stats.OnOmniCrystalsCollectedChanged -= HandleStatChanged;
            }
            _playerCards.Clear();
            view.ClearPlayerList();
        }
    }
}