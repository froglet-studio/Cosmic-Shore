using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Base controller for single-player game modes.
    /// Handles event subscriptions and initial setup.
    /// </summary>
    public abstract class SinglePlayerMiniGameControllerBase : MiniGameControllerBase
    {
        protected virtual void OnEnable()
        {
            if (gameData == null) return;
            
            gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
        }
        
        protected virtual void Start()
        {
            if (gameData == null)
            {
                Debug.LogError("GameDataSO is not assigned!", this);
                return;
            }
            
            gameData.InitializeGame();
            gameData.InvokeClientReady();
            SetupNewRound();
        }
        
        protected virtual void OnDisable() 
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