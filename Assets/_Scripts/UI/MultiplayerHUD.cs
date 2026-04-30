using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
namespace CosmicShore.UI
{
    public abstract class MultiplayerHUD : MiniGameHUD
    {
        [Header("Multiplayer View")]
        [SerializeField] protected MultiplayerHUDView multiplayerView;

        // Cards are keyed by the IRoundStats reference (stable across name changes)
        // and a parallel name-keyed map exists for legacy callers (UpdatePlayerCard).
        protected Dictionary<IRoundStats, PlayerScoreEntry> _cardsByStats = new();
        protected Dictionary<string, PlayerScoreEntry> _playerCards = new();

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

        /// <summary>
        /// Defensive refresh on turn start: re-applies team color AND score to every
        /// card from the current authoritative <see cref="IRoundStats"/>, in case the
        /// card was built with stale values during the replication race window
        /// (Domain or score landed after CreateCardForPlayer ran).
        /// </summary>
        void RefreshAllPlayerCards()
        {
            if (gameData?.RoundStatsList == null) return;

            foreach (var stats in gameData.RoundStatsList.Where(s => s != null))
            {
                if (!_cardsByStats.TryGetValue(stats, out var card) || card == null) continue;
                card.SetDomainColor(view.GetColorForDomain(stats.Domain));
                card.UpdateScore(GetInitialCardValue(stats));
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
            _cardsByStats.Clear();
        }

        private void InitializePlayerCards()
        {
            view.ClearPlayerList();
            _playerCards.Clear();
            _cardsByStats.Clear();
            AssignAIProfiles();

            for (int i = 0; i < gameData.RoundStatsList.Count; i++)
            {
                CreateCardForPlayer(gameData.RoundStatsList[i], i);
            }
        }

        private void CreateCardForPlayer(IRoundStats stats, int staggerIndex)
        {
            var card = Instantiate(view.PlayerScoreEntryPrefab, view.PlayerScoreContainer);
            var isLocal = gameData.LocalPlayer != null && stats.Name == gameData.LocalPlayer.Name;
            var teamColor = view.GetColorForDomain(stats.Domain);

            card.Setup(stats.Name, GetInitialCardValue(stats), teamColor, isLocal, staggerIndex);

            // Resolve avatar: for non-local players, try AI profile first
            Sprite avatarSprite = null;
            if (!isLocal)
                avatarSprite = ResolveAIAvatarSprite(stats.Name);

            if (avatarSprite == null)
            {
                var player = gameData.Players.FirstOrDefault(p => p.Name == stats.Name);
                if (player != null)
                    avatarSprite = ResolveAvatarSprite(player.AvatarId);
            }
            card.SetAvatar(avatarSprite);

            _playerCards[stats.Name] = card;
            _cardsByStats[stats] = card;

            // Refresh team-color when stats.Domain replicates after the card was created.
            // RoundStats.n_Domain replication can land after the turn-start UI build,
            // which would otherwise leave non-owner cards showing the default Color.white.
            stats.OnDomainChanged += HandleDomainChanged;

            SubscribeToPlayerStats(stats);
        }

        private void UnsubscribeFromAllStats()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats != null)
                    stats.OnDomainChanged -= HandleDomainChanged;
                UnsubscribeFromPlayerStats(stats);
            }
        }

        private void HandleDomainChanged(IRoundStats updatedStats)
        {
            if (updatedStats == null) return;
            // Look up by stats reference first (stable), fall back to name for backward compat.
            if (_cardsByStats.TryGetValue(updatedStats, out var card) && card != null)
            {
                card.SetDomainColor(view.GetColorForDomain(updatedStats.Domain));
                return;
            }
            if (_playerCards.TryGetValue(updatedStats.Name, out card) && card != null)
                card.SetDomainColor(view.GetColorForDomain(updatedStats.Domain));
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

        /// <summary>
        /// Stable update path that doesn't depend on the player's Name matching the
        /// dictionary key — useful when NetName replicates after the card was created.
        /// </summary>
        protected void UpdatePlayerCard(IRoundStats stats, int newValue)
        {
            if (stats == null) return;
            if (_cardsByStats.TryGetValue(stats, out var card) && card != null)
                card.UpdateScore(newValue);
        }
    }
}
