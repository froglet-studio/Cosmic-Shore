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

        // Gameplay state
        protected int turnsTakenThisRound;
        protected int roundsPlayed;

        protected virtual void Start()
        {
            Initialize();
        }

        public void OnReadyClicked() =>
            OnReadyClicked_();
        
        protected void Initialize()
        {
            miniGameData.InitializeMiniGame();
            miniGameData.GameMode = gameMode;
        }

        protected virtual void OnReadyClicked_()
        {
            PauseSystem.TogglePauseGame(false);
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

        protected virtual void EndGame()
        {
            PauseSystem.TogglePauseGame(true);
            miniGameData.SetPlayersActive(false);
            miniGameData.InvokeMiniGameEnd();
        }
    }
}