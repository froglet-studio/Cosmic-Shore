using CosmicShore.App.Systems;
using CosmicShore.SOAP;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;


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
        [SerializeField] protected GameModes gameMode;
        [SerializeField] protected int numberOfRounds = int.MaxValue;
        [SerializeField] protected int numberOfTurnsPerRound = 1;
        
        [Header("Scene References")]
        [SerializeField] CountdownTimer countdownTimer;
        
        [SerializeField] 
        protected MiniGameDataSO miniGameData;
        
        [SerializeField] 
        protected ScriptableEventBool _onToggleReadyButton;
        
        [SerializeField]
        Transform[] _playerOrigins;

        // Gameplay state
        protected int turnsTakenThisRound;
        protected int roundsPlayed;

        private void Start()
        {
            miniGameData.InitializeMiniGame();
            miniGameData.PlayerOrigins =  _playerOrigins;
            miniGameData.GameMode = gameMode;
        }

        public void OnReadyClicked() =>
            OnReadyClicked_();

        protected virtual void OnReadyClicked_()
        {
            DisableReadyButton();
            StartCountdownTimer();
        }

        protected void StartCountdownTimer() =>
            countdownTimer.BeginCountdown(OnCountdownTimerEnded);
        
        protected void DisableReadyButton() => _onToggleReadyButton.Raise(false);
        
        protected virtual void OnCountdownTimerEnded()
        {
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            miniGameData.SetPlayersActive(active: true);
            miniGameData.StartNewGame();
        }
        
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
        
        protected void EndTurn()
        {
            // miniGameData.InvokeMiniGameTurnEnd();   
            turnsTakenThisRound++;

            if(turnsTakenThisRound >= numberOfTurnsPerRound)
                EndRound();
            else 
                SetupNewTurn();
        }

        void EndRound()
        {
            roundsPlayed++;
            if (roundsPlayed >= numberOfRounds) 
                EndGame();
            else
            {
                SetupNewTurn();
            }
        }

        void EndGame()
        {
            miniGameData.InvokeMiniGameEnd();
        }
        
        // These should go to events.
        /*void OnGamePaused()
        {
            if(!gameRunning || _miniGameData.Value.LocalPlayer is null) 
                return; 
            
            _miniGameData.Value.LocalPlayer?.ToggleAutoPilotMode(true);
        }

        void OnGameResumed()
        {
            if(!gameRunning || _miniGameData.Value.LocalPlayer is null) 
                return; 
            
            _miniGameData.Value.LocalPlayer?.ToggleAutoPilotMode(false);
        }*/
    }
}