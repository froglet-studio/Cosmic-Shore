using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Base class for all multiplayer game HUDs.
    /// Handles player card creation and common multiplayer UI.
    /// Subclasses override UpdatePlayerCard to track game-specific stats.
    /// </summary>
    public abstract class MultiplayerHUD : MiniGameHUD
    {
        [Header("Multiplayer View")]
        [SerializeField] protected MultiplayerHUDView multiplayerView;

        protected Dictionary<string, PlayerScoreCard> _playerCards = new();

        private void OnValidate()
        {
            if (multiplayerView == null)
                multiplayerView = GetComponent<MultiplayerHUDView>();
            
            if (view == null)
                view = multiplayerView;
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

        #region Player Card Management

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
            
            // Initialize with starting value (subclass decides what to show)
            int initialValue = GetInitialCardValue(stats);
            card.Setup(stats.Name, initialValue, teamColor, isLocal);
            _playerCards[stats.Name] = card;

            // Subscribe to stat changes
            SubscribeToPlayerStats(stats);
        }

        private void UnsubscribeFromAllStats()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                UnsubscribeFromPlayerStats(stats);
            }
            _playerCards.Clear();
        }

        #endregion

        #region Abstract Methods - Override in Subclasses

        /// <summary>
        /// Get the initial value to display on the player card.
        /// Example: HexRace = OmniCrystalsCollected, Joust = JoustsWon
        /// </summary>
        protected abstract int GetInitialCardValue(IRoundStats stats);

        /// <summary>
        /// Subscribe to game-specific stat events.
        /// Example: HexRace = OnOmniCrystalsCollectedChanged, Joust = OnJoustsWonChanged
        /// </summary>
        protected abstract void SubscribeToPlayerStats(IRoundStats stats);

        /// <summary>
        /// Unsubscribe from game-specific stat events.
        /// </summary>
        protected abstract void UnsubscribeFromPlayerStats(IRoundStats stats);

        /// <summary>
        /// Subscribe to game-specific events (e.g., OnJoustPerformed).
        /// Called once during turn start.
        /// </summary>
        protected virtual void SubscribeToGameSpecificEvents() { }

        /// <summary>
        /// Unsubscribe from game-specific events.
        /// Called during turn end.
        /// </summary>
        protected virtual void UnsubscribeFromGameSpecificEvents() { }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Update a specific player's card with a new value.
        /// </summary>
        protected void UpdatePlayerCard(string playerName, int newValue)
        {
            if (_playerCards.TryGetValue(playerName, out var card))
            {
                card.UpdateScore(newValue);
            }
        }

        #endregion
    }
}