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
        
        protected abstract void OnCountdownTimerEnded();
        
        protected virtual void SetupNewTurn()
        {
        }

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
        
        protected virtual void OnTurnEndedCustom()
        {
        }
        
        protected virtual void SetupNewRound()
        {
            gameData.TurnsTakenThisRound = 0;
            gameData.InvokeMiniGameRoundStarted();
            SetupNewTurn();
        }
        
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
        
        protected virtual void OnRoundEndedCustom()
        {
        }

        protected virtual void EndGame()
        {
            if (!ShowEndGameSequence) return;
            gameData.SortRoundStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }
        
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