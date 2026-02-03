using CosmicShore.App.Systems;
using CosmicShore.Soap;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Stateless top-level game-flow controller.
    /// Template Method Pattern: Defines skeleton of game flow with hooks for customization.
    /// </summary>
    public abstract class MiniGameControllerBase : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] protected int numberOfRounds = int.MaxValue;
        [SerializeField] protected int numberOfTurnsPerRound = 1;
        
        [Header("Scene References")]
        [SerializeField] protected CountdownTimer countdownTimer;
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] protected ScriptableEventBool _onToggleReadyButton;

        protected virtual bool HasEndGame => true;
        protected virtual bool ShouldResetPlayersOnTurnEnd => false;
        protected virtual bool ShowEndGameSequence => true;
        protected virtual bool UseGolfRules => false;
        
        public void OnReadyClicked()
        {
            OnReadyClicked_();
        }
        
        protected virtual void OnReadyClicked_()
        {
            RaiseToggleReadyButtonEvent(false);
            StartCountdownTimer();
        }

        protected void StartCountdownTimer()
        {
            if (countdownTimer != null)
                countdownTimer.BeginCountdown(OnCountdownTimerEnded);
        }
        
        protected void RaiseToggleReadyButtonEvent(bool enable)
        {
            _onToggleReadyButton?.Raise(enable);
        }
        
        /// <summary>Called when countdown reaches zero - starts the actual gameplay</summary>
        protected abstract void OnCountdownTimerEnded();
        
        /// <summary>
        /// Setup a new turn. Called before turn starts.
        /// Override to add game-specific turn setup (environment reset, etc.)
        /// </summary>
        protected virtual void SetupNewTurn()
        {
        }

        /// <summary>
        /// Called when turn ends. Handles progression to next turn or round end.
        /// Final method - use OnTurnEndedCustom for game-specific logic.
        /// </summary>
        protected void EndTurn()
        {
            OnTurnEndedCustom();
            
            if (ShouldResetPlayersOnTurnEnd)
                gameData.ResetPlayers();
            
            gameData.TurnsTakenThisRound++;

            if (gameData.TurnsTakenThisRound >= numberOfTurnsPerRound)
                EndRound();
            else 
                SetupNewTurn();
        }
        
        /// <summary>
        /// Hook for game-specific turn end logic.
        /// Called BEFORE checking if round should end.
        /// </summary>
        protected virtual void OnTurnEndedCustom()
        {
        }
        
        /// <summary>
        /// Setup a new round. Called at game start and between rounds.
        /// Override to add game-specific round setup.
        /// </summary>
        protected virtual void SetupNewRound()
        {
            gameData.TurnsTakenThisRound = 0;
            gameData.InvokeMiniGameRoundStarted();
            SetupNewTurn();
        }
        
        /// <summary>
        /// Called when round ends. Handles progression to next round or game end.
        /// Final method - use OnRoundEndedCustom for game-specific logic.
        /// </summary>
        protected void EndRound()
        {
            OnRoundEndedCustom();
            
            gameData.RoundsPlayed++;
            gameData.InvokeMiniGameRoundEnd();
            
            if (HasEndGame && gameData.RoundsPlayed >= numberOfRounds)
                EndGame();
            else
                SetupNewRound();
        }
        
        /// <summary>
        /// Hook for game-specific round end logic.
        /// Called BEFORE checking if game should end.
        /// </summary>
        protected virtual void OnRoundEndedCustom()
        {
        }

        /// <summary>
        /// Ends the game and triggers end game sequence.
        /// Override to customize what happens when game ends.
        /// </summary>
        protected virtual void EndGame()
        {
            if (!ShowEndGameSequence) return;
            gameData.SortRoundStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }
        
        /// <summary>
        /// Called when user clicks "Play Again" from scoreboard.
        /// Subclasses should override ResetEnvironmentForReplay() to clean up their environment.
        /// </summary>
        protected virtual void OnResetForReplay()
        {
            SetupNewRound();
        }
        
        protected virtual void ResetEnvironmentForReplay()
        {
            Debug.Log("[MiniGameControllerBase] ResetEnvironmentForReplay - Override in subclass");
        }
    }
}