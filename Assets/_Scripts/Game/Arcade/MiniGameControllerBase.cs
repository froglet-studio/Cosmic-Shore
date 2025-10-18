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
        
        
        // Gameplay state
        protected int turnsTakenThisRound;
        protected int roundsPlayed;
        
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
            // TODO - Need to rewrite the following method.
            
            /*if (!_miniGameData.Value.TryAdvanceActivePlayer(out IPlayer activePlayer))
                return;

            activePlayer.ToggleStationaryMode(true);
            monitorController.NewTurn(_miniGameData.Value.LocalPlayer.PlayerName);
            
            turnsTakenThisRound = 0;
            
            monitorController.StartTurns();
            monitorController.PauseTurns();
            
            if (_miniGameData.Value.Players.Count > 1)
                _onToggleReadyButton.Raise(true);
            else
                countdownTimer.BeginCountdown(OnCountdownTimerEnded);*/
        }

        protected virtual void SetupNewRound()
        {
            turnsTakenThisRound = 0;
            SetupNewTurn();
        }
        
        protected virtual void EndTurn()
        {
            // miniGameData.InvokeMiniGameTurnEnd();   
            turnsTakenThisRound++;

            if(turnsTakenThisRound >= numberOfTurnsPerRound)
                EndRound();
            else 
                SetupNewTurn();
        }

        protected abstract void EndGame();

        void EndRound()
        {
            roundsPlayed++;
            if (roundsPlayed >= numberOfRounds) 
                EndGame();
            else
            {
                SetupNewRound();
            }
        }
        
        protected void OnResetForReplay()
        {
            roundsPlayed = 0;
            SetupNewRound();
        }
    }
}