using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Base controller for single-player game modes.
    /// Handles event subscriptions and initial setup.
    /// </summary>
    public abstract class SinglePlayerMiniGameControllerBase : MiniGameControllerBase
    {
        protected virtual void OnEnable() => SubscribeToEvents();

        protected virtual void Start()
        {
            SubscribeToEvents();

            if (gameData == null)
            {
                CSDebug.LogError("GameDataSO is not assigned!", this);
                return;
            }

            gameData.InitializeGame();
            gameData.InvokeClientReady();
            SetupNewRound();
        }

        protected virtual void OnDisable() => UnsubscribeFromEvents();

        private void SubscribeToEvents()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnEnd.OnRaised -= EndTurn;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
            gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
        }

        private void UnsubscribeFromEvents()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnEnd.OnRaised -= EndTurn;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
        }
        
        protected override void OnCountdownTimerEnded()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }
    }
}