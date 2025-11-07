using CosmicShore.App.Systems;
using CosmicShore.SOAP;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Stateless top‑level game‑flow controller.
    /// Keeps responsibility limited to: StartGame ➜ Rounds ➜ Turns ➜ EndGame.
    /// Delegates per‑frame checks to TurnMonitorController and player logic to PlayerManager.
    /// </summary>
    public abstract class MiniGameControllerBase : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] protected int numberOfRounds = int.MaxValue;
        [SerializeField] protected int numberOfTurnsPerRound = 1;
        
        [Header("Scene References")]
        [SerializeField] CountdownTimer countdownTimer;
        
        [SerializeField] 
        protected GameDataSO gameData;
        
        [SerializeField] 
        protected ScriptableEventBool _onToggleReadyButton;
        
        public void OnReadyClicked() =>
            OnReadyClicked_();
        
        protected void InitializeGame() => gameData.InitializeGame();

        protected virtual void OnReadyClicked_()
        {
            ToggleReadyButton(false);
            StartCountdownTimer();
        }

        protected void StartCountdownTimer() =>
            countdownTimer.BeginCountdown(OnCountdownTimerEnded);
        
        protected void ToggleReadyButton(bool enable) => _onToggleReadyButton.Raise(enable);
        
        protected abstract void OnCountdownTimerEnded();
        
        protected virtual void SetupNewTurn()
        {
        }

        protected virtual void SetupNewRound()
        {
            gameData.TurnsTakenThisRound = 0;
            SetupNewTurn();
        }
        
        protected virtual void EndTurn()
        {
            gameData.TurnsTakenThisRound++;

            if(gameData.TurnsTakenThisRound >= numberOfTurnsPerRound)
                EndRound();
            else 
                SetupNewTurn();
        }

        protected abstract void EndGame();

        protected virtual void EndRound()
        {
            if (gameData.RoundsPlayed >= numberOfRounds) 
                EndGame();
            else
                SetupNewRound();
        }
        
        protected virtual void OnResetForReplay()
        {
            SetupNewRound();
        }
    }
}