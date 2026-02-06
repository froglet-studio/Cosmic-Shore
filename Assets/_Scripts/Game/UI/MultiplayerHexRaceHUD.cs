using System.Collections.Generic;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class MultiplayerHexRaceHUD : MiniGameHUD
    {
        [Header("Multiplayer View")]
        [SerializeField] private MultiplayerHexRaceHUDView multiplayerView;

        private Dictionary<string, PlayerScoreCard> _playerCards = new Dictionary<string, PlayerScoreCard>();

        private void OnValidate()
        {
            if (multiplayerView == null)
                multiplayerView = GetComponent<MultiplayerHexRaceHUDView>();
            
            if (view == null)
                view = multiplayerView;
        }

        protected override void OnMiniGameTurnStarted()
        {
            base.OnMiniGameTurnStarted();
            InitializePlayerCards();
        }

        protected override void OnMiniGameTurnEnd()
        {
            base.OnMiniGameTurnEnd();
            UnsubscribeFromAllStats();
        }

        private void InitializePlayerCards()
        {
            multiplayerView.ClearPlayerList();
            _playerCards.Clear();

            foreach (var stats in gameData.RoundStatsList)
            {
                CreateCardForPlayer(stats);
            }
        }

        private void CreateCardForPlayer(IRoundStats stats)
        {
            var card = Instantiate(multiplayerView.PlayerScoreCardPrefab, multiplayerView.PlayerScoreContainer);
            
            var isLocal = gameData.LocalPlayer != null && stats.Name == gameData.LocalPlayer.Name;
            var teamColor = multiplayerView.GetColorForDomain(stats.Domain);
            card.Setup(stats.Name, stats.OmniCrystalsCollected, teamColor, isLocal);
            _playerCards[stats.Name] = card;

            stats.OnOmniCrystalsCollectedChanged += OnCrystalStatChanged;
        }

        private void OnCrystalStatChanged(IRoundStats updatedStats)
        {
            if (_playerCards.TryGetValue(updatedStats.Name, out var card))
            {
                card.UpdateScore(updatedStats.OmniCrystalsCollected);
            }
        }

        private void UnsubscribeFromAllStats()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                stats.OnOmniCrystalsCollectedChanged -= OnCrystalStatChanged;
            }
            _playerCards.Clear();
        }
    }
}