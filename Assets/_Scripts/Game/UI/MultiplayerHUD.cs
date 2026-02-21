using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CosmicShore.Game.UI
{
    public abstract class MultiplayerHUD : MiniGameHUD
    {
        [Header("Multiplayer View")]
        [SerializeField] protected MultiplayerHUDView multiplayerView;

        [Header("Avatar Icons")]
        [SerializeField] private SO_ProfileIconList profileIconList;

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
            // Note: We do NOT call base.OnMiniGameTurnStarted() here if we want to 
            // override the AI setup logic with full multiplayer card logic.
            
            localRoundStats = gameData.LocalRoundStats;
            if (localRoundStats != null)
                localRoundStats.OnScoreChanged += UpdateScoreUI;

            InitializePlayerCards();
            SubscribeToGameSpecificEvents();
        }

        protected override void OnMiniGameTurnEnd()
        {
            base.OnMiniGameTurnEnd();
            UnsubscribeFromAllStats();
            UnsubscribeFromGameSpecificEvents();
            _playerCards.Clear();
        }

        private void InitializePlayerCards()
        {
            view.ClearPlayerList();
            _playerCards.Clear();

            foreach (var stats in gameData.RoundStatsList)
            {
                CreateCardForPlayer(stats);
            }
        }

        private void CreateCardForPlayer(IRoundStats stats)
        {
            var card = Instantiate(view.PlayerScoreCardPrefab, view.PlayerScoreContainer);
            var isLocal = gameData.LocalPlayer != null && stats.Name == gameData.LocalPlayer.Name;
            var teamColor = view.GetColorForDomain(stats.Domain);

            card.Setup(stats.Name, GetInitialCardValue(stats), teamColor, isLocal);

            // Resolve avatar sprite from the player's AvatarId
            var player = gameData.Players.FirstOrDefault(p => p.Name == stats.Name);
            if (player != null && profileIconList != null)
            {
                var sprite = ResolveAvatarSprite(player.AvatarId);
                card.SetAvatar(sprite);
            }

            _playerCards[stats.Name] = card;

            SubscribeToPlayerStats(stats);
        }

        private Sprite ResolveAvatarSprite(int avatarId)
        {
            if (profileIconList == null || profileIconList.profileIcons == null)
                return null;

            foreach (var icon in profileIconList.profileIcons)
            {
                if (icon.Id == avatarId)
                    return icon.IconSprite;
            }

            return profileIconList.profileIcons.Count > 0
                ? profileIconList.profileIcons[0].IconSprite
                : null;
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
        protected override bool RequireClientReady => true;

        protected void UpdatePlayerCard(string playerName, int newValue)
        {
            if (_playerCards.TryGetValue(playerName, out var card))
            {
                card.UpdateScore(newValue);
            }
        }
    }
}