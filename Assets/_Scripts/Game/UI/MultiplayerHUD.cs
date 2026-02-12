using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CosmicShore.Game.UI
{
    public abstract class MultiplayerHUD : MiniGameHUD
    {
        [Header("Multiplayer View")]
        [SerializeField] protected MultiplayerHUDView multiplayerView;

        protected Dictionary<string, PlayerScoreCard> _playerCards = new();

        protected override void OnEnable()
        {
            base.OnEnable();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllPlayerCards;
                gameData.OnResetForReplay.OnRaised += ResetAllCards;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllPlayerCards;
                gameData.OnResetForReplay.OnRaised -= ResetAllCards;
            }
        }

        void ResetAllCards()
        {
            if (gameData?.RoundStatsList == null) return;

            foreach (var stats in gameData.RoundStatsList.Where(s => s != null))
            {
                UpdatePlayerCard(stats.Name, 0);
            }
        }

        void RefreshAllPlayerCards()
        {
            if (gameData?.RoundStatsList == null) return;

            foreach (var stats in gameData.RoundStatsList.Where(s => s != null))
            {
                UpdatePlayerCard(stats.Name, GetInitialCardValue(stats));
            }
        }

        private void OnValidate()
        {
            if (multiplayerView == null) multiplayerView = GetComponent<MultiplayerHUDView>();
            if (view == null) view = multiplayerView;
        }

        protected override void OnMiniGameTurnStarted()
        {
            base.OnMiniGameTurnStarted();
            InitializePlayerCards();
            SubscribeToGameSpecificEvents();
        }

        protected override void OnMiniGameTurnEnd()
        {
            base.OnMiniGameTurnEnd();
            UnsubscribeFromAllStats();
            UnsubscribeFromGameSpecificEvents();
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
            
            card.Setup(stats.Name, GetInitialCardValue(stats), teamColor, isLocal);
            _playerCards[stats.Name] = card;

            SubscribeToPlayerStats(stats);
        }

        private void UnsubscribeFromAllStats()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                UnsubscribeFromPlayerStats(stats);
            }
        }

        protected abstract int GetInitialCardValue(IRoundStats stats);
        protected abstract void SubscribeToPlayerStats(IRoundStats stats);
        protected abstract void UnsubscribeFromPlayerStats(IRoundStats stats);
        protected virtual void SubscribeToGameSpecificEvents() { }
        protected virtual void UnsubscribeFromGameSpecificEvents() { }

        protected void UpdatePlayerCard(string playerName, int newValue)
        {
            if (_playerCards.TryGetValue(playerName, out var card))
            {
                card.UpdateScore(newValue);
            }
        }
    }
}