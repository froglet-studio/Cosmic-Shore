using CosmicShore.App.Systems;
using CosmicShore.SOAP;
using Obvious.Soap;
using UnityEngine;


namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Stateless top‑level game‑flow controller.
    /// Keeps responsibility limited to: StartGame ➜ Rounds ➜ Turns ➜ EndGame.
    /// Delegates per‑frame checks to TurnMonitorController and player logic to PlayerManager.
    /// </summary>
    public abstract class R_MiniGameBase : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] protected GameModes gameMode;
        [SerializeField] protected int numberOfRounds = int.MaxValue;
        [SerializeField] protected int numberOfTurnsPerRound = 1;
        
        [Header("Scene References")]
        [SerializeField] protected TurnMonitorController monitorController;
        [SerializeField] protected ScoreTracker scoreTracker;
        [SerializeField] CountdownTimer countdownTimer;
        
        [SerializeField] 
        protected MiniGameDataSO _miniGameData;
        
        [SerializeField] 
        protected ScriptableEventBool _onToggleReadyButton;
        
        [SerializeField]
        Transform[] _playerOrigins;

        // Gameplay state
        protected int turnsTakenThisRound;
        protected int roundsPlayed;
        protected bool gameRunning;
        
        protected virtual void OnEnable() 
        {
            PauseSystem.TogglePauseGame(false);
            
            /*PauseSystem.OnGamePaused  += OnGamePaused;
            PauseSystem.OnGameResumed += OnGameResumed;*/
        }

        private void Start()
        {
            _miniGameData.PlayerOrigins =  _playerOrigins;
            _miniGameData.GameMode = gameMode;
            _miniGameData.InvokeInitialize();
            scoreTracker.InitializeScoringMode();
        }

        protected virtual void Update() {
            if(!gameRunning) 
                return;
            
            if (monitorController.CheckEndOfTurn()) 
                EndTurn();
        }

        protected virtual void OnDisable() 
        {
            /*PauseSystem.OnGamePaused  -= OnGamePaused;
            PauseSystem.OnGameResumed -= OnGameResumed;*/
        }


        public void OnReadyClicked()
        {
            _onToggleReadyButton.Raise(false);
            countdownTimer.BeginCountdown(StartNewGame);   
        }
        
        protected virtual void StartNewGame()
        {
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            gameRunning = true;
            
            monitorController.StartMonitors();
            _miniGameData.ActivateAllPlayers();
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
                countdownTimer.BeginCountdown(StartNewGame);*/
        }
        
        void EndTurn()
        {
            monitorController.PauseMonitors();
            
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
            gameRunning = false; 
            monitorController.StopMonitors();
            scoreTracker.CalculateWinner();
            PauseSystem.TogglePauseGame(true);
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