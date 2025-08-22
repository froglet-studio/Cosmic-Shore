using System.Collections;
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
        const float WAIT_FOR_SECONDS_BEFORE_INITIALIZE = .2f;
        const float WAIT_FOR_SECONDS_ON_SETUP_TURN = 2f;
        const float WAIT_FOR_SECONDS_BEFORE_END_TURN = 0.25f;
        
        [Header("Config")]
        [SerializeField] protected GameModes gameMode;
        [SerializeField] protected int numberOfRounds = int.MaxValue;
        
        [Header("Scene References")]
        [SerializeField] protected TurnMonitorController monitorController;
        [SerializeField] protected ScoreTracker scoreTracker;
        [SerializeField] CountdownTimer countdownTimer;
        
        [SerializeField] 
        protected MiniGameDataVariable _miniGameData;
        
        [SerializeField] 
        protected ScriptableEventBool _onToggleReadyButton;
        
        /*[SerializeField] 
        ScriptableEventNoParam _onPlayGame;*/
        
        [SerializeField]
        Transform[] _playerOrigins;

        // Gameplay state
        protected int turnsTakenThisRound;
        protected int roundsPlayed;
        protected bool gameRunning;
        
        protected virtual void OnEnable() 
        {
            // _onPlayGame.OnRaised += InitializeGame;
            PauseSystem.OnGamePaused  += OnGamePaused;
            PauseSystem.OnGameResumed += OnGameResumed;
        }

        void Start()
        {
            PauseSystem.TogglePauseGame(false);
            _miniGameData.Value.PlayerOrigins =  _playerOrigins;
            _miniGameData.Value.GameMode = gameMode;
            _miniGameData.InvokeInitialize();
        }
        
        protected virtual void Update() {
            if(!gameRunning) 
                return;
            
            if (monitorController.CheckEndOfTurn()) 
                EndTurn();
        }

        protected virtual void OnDisable() {
            // _onPlayGame.OnRaised -= InitializeGame;
            PauseSystem.OnGamePaused  -= OnGamePaused;
            PauseSystem.OnGameResumed -= OnGameResumed;
        }


        public void OnReadyClicked()
        {
            _onToggleReadyButton.Raise(false);
            countdownTimer.BeginCountdown(OnCountdownComplete);   
        } 

        protected abstract void OnStartNewGame();
        
        void OnCountdownComplete() => StartCoroutine(StartNewGameCoroutine());

        IEnumerator StartNewGameCoroutine()
        {
            yield return new WaitForSeconds(WAIT_FOR_SECONDS_BEFORE_INITIALIZE); 
            
            PauseSystem.TogglePauseGame(false);
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            gameRunning = true;
            OnStartNewGame();
            
            monitorController.ResumeAll();
            
            foreach (var player in _miniGameData.Value.Players)
                player.ToggleStationaryMode(false);

            var activePlayer = _miniGameData.Value.ActivePlayer;
            scoreTracker.StartTurn(activePlayer.PlayerName, activePlayer.Team);
            
            _miniGameData.InvokeStartMiniGame();
        }
        
        protected virtual void SetupNewTurn()
        {
            if (!_miniGameData.Value.TryAdvanceActivePlayer(out IPlayer activePlayer))
                return;

            activePlayer.ToggleStationaryMode(true);
            monitorController.NewTurn(_miniGameData.Value.ActivePlayer.PlayerName);
            monitorController.PauseAll();
            
            if (_miniGameData.Value.Players.Count > 1)
                _onToggleReadyButton.Raise(true);
            else
                StartCoroutine(StartCountdownTimerCoroutine());
        }
        
        
        IEnumerator StartCountdownTimerCoroutine()
        {
            yield return new WaitForSecondsRealtime(WAIT_FOR_SECONDS_ON_SETUP_TURN);
            OnReadyClicked();
        }
        
        void EndTurn() => StartCoroutine(EndTurnCoroutine());
        
        IEnumerator EndTurnCoroutine(){
            monitorController.PauseAll();
            
            IPlayer activePlayer = _miniGameData.Value.ActivePlayer;
            if (activePlayer is not null)
                activePlayer.ToggleStationaryMode(true);

            yield return new WaitForSeconds(WAIT_FOR_SECONDS_BEFORE_END_TURN);
            turnsTakenThisRound++;
            scoreTracker.EndTurn();
            
            if(turnsTakenThisRound >= _miniGameData.Value.RemainingPlayers.Count)
                EndRound();
            else 
                SetupNewTurn();
        }

        void EndRound()
        {
            roundsPlayed++;
            if (roundsPlayed >= numberOfRounds || _miniGameData.Value.RemainingPlayers.Count <= 0) 
                EndGame();
            else
            {
                turnsTakenThisRound = 0;
                SetupNewTurn();
            }
        }

        void EndGame()
        {
            gameRunning = false; 
            PauseSystem.TogglePauseGame(true);
            _miniGameData.Value.HighScore = scoreTracker.GetHighScore();
            _miniGameData.InvokeEndMiniGame();
        }
        
        // These should go to events.
        void OnGamePaused()
        {
            if(!gameRunning || _miniGameData.Value.ActivePlayer is null) 
                return; 
            
            _miniGameData.Value.ActivePlayer?.ToggleAutoPilotMode(true);
        }

        void OnGameResumed()
        {
            if(!gameRunning || _miniGameData.Value.ActivePlayer is null) 
                return; 
            
            _miniGameData.Value.ActivePlayer?.ToggleAutoPilotMode(false);
        }
    }
}