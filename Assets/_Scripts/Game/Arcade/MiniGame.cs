using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.PlayStream;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CosmicShore.App.Systems.UserActions;
using CosmicShore.Game.UI;
using CosmicShore.Models.Enums;
using CosmicShore.App.Systems;
using CosmicShore.Integrations.PlayFab.PlayerData;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.App.Systems.Xp;
using UnityEngine.Serialization;
using CosmicShore.Utility;


namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// DEPRECATED - Use MiniGameControllerBase instead
    /// </summary>
    public abstract class MiniGame : MonoBehaviour
    {
        [SerializeField] protected GameModes gameMode;
        [SerializeField] protected int NumberOfRounds = int.MaxValue;
        // [SerializeField] protected List<TurnMonitor> TurnMonitors;
        [SerializeField] protected ScoreTracker ScoreTracker;
        [SerializeField] GameCanvas GameCanvas;
        [SerializeField] GameObject playerPrefab;
        [SerializeField] GameObject PlayerOrigin;
        [SerializeField] float EndOfTurnDelay = 0f;
        [SerializeField] bool EnableTrails = true;
        [FormerlySerializedAs("DefaultPlayerShipType")] [SerializeField] VesselClassType defaultPlayerVesselType = VesselClassType.Dolphin;
        [FormerlySerializedAs("DefaultPlayerCaptain")]
        [SerializeField] SO_Vessel DefaultPlayerShip;

        protected Button ReadyButton;
        // protected GameObject EndGameScreen;
        protected MiniGameHUD HUD;
        protected List<IPlayer> Players = new();
        protected CountdownTimer countdownTimer;

        List<Domains> PlayerTeams = new() { Domains.Jade, Domains.Ruby, Domains.Gold };
        List<string> PlayerNames = new() { "PlayerOne", "PlayerTwo", "PlayerThree" };

        // Configuration set by player
        public static int NumberOfPlayers = 1;  // TODO: P1 - support excluding single player games (e.g for elimination)
        public static int IntensityLevel = 1;
        public static bool IsDailyChallenge = false;
        public static bool IsMission = false;
        public static bool IsTraining = false;
        static VesselClassType _playerVesselType = VesselClassType.Dolphin;
        static bool playerShipTypeInitialized;

        public static VesselClassType PlayerVesselType
        {
            get => _playerVesselType;
            set
            {
                _playerVesselType = value;
                playerShipTypeInitialized = true;
            }
        }
        public static ResourceCollection ResourceCollection = new(.5f, .5f, .5f, .5f);

        // Game State Tracking
        protected int TurnsTakenThisRound;
        int RoundsPlayedThisGame;

        // PlayerId Tracking
        int activePlayerId;
        protected List<int> RemainingPlayers = new();
        protected bool gameRunning;

        public IPlayer ActivePlayer { get; protected set; }

        // Firebase analytics events
        public delegate void MiniGameStart(GameModes mode, VesselClassType vessel, int playerCount, int intensity);
        public static event MiniGameStart OnMiniGameStart;
        public delegate void MiniGameEnd(GameModes mode, VesselClassType vessel, int playerCount, int intensity, int highScore);
        public static event MiniGameEnd OnMiniGameEnd;

        protected virtual void Awake()
        {
            // EndGameScreen = GameCanvas.EndGameScreen;
            HUD = GameCanvas.MiniGameHUD;
            // ReadyButton = HUD.View.ReadyButton;
            // countdownTimer = HUD.View.CountdownTimer;
            // ScoreTracker.GameCanvas = GameCanvas;

            /*foreach (var turnMonitor in TurnMonitors)
                if (turnMonitor is TimeBasedTurnMonitor tbtMonitor)
                    tbtMonitor.Display = HUD.View.RoundTimeDisplay;
                else if (turnMonitor is VolumeCreatedTurnMonitor hvtMonitor) // TODO: consolidate with above
                    hvtMonitor.Display = HUD.View.RoundTimeDisplay;
                else if (turnMonitor is ShipCollisionTurnMonitor scMonitor) // TODO: consolidate with above
                    scMonitor.Display = HUD.View.RoundTimeDisplay;
                else if (turnMonitor is DistanceTurnMonitor dtMonitor) // TODO: consolidate with above
                    dtMonitor.Display = HUD.View.RoundTimeDisplay;*/

            PauseSystem.TogglePauseGame(false);
        }

        protected virtual void Start()
        {
            InstantiateAndInitializePlayer();
        }

        protected virtual void OnEnable()
        {
            // GameManager.OnPlayGame += InitializeGame;
            
            // TODO - Replaced in MiniGameControllerBase
            // OnMiniGameTurnStarted += FirebaseAnalyticsController.LogEventMiniGameStart;
            // OnMiniGameEnd += FirebaseAnalyticsController.LogEventMiniGameEnd;
            PauseSystem.OnGamePaused += HandleGamePaused;
            PauseSystem.OnGameResumed += HandleGameResumed;
        }

        protected virtual void OnDisable()
        {
            // GameManager.OnPlayGame -= InitializeGame;
            
            // TODO - Replaced in MiniGameControllerBase
            // OnMiniGameTurnStarted -= FirebaseAnalyticsController.LogEventMiniGameStart;
            // OnMiniGameEnd -= FirebaseAnalyticsController.LogEventMiniGameEnd;
            PauseSystem.OnGamePaused -= HandleGamePaused;
            PauseSystem.OnGameResumed -= HandleGameResumed;

        }

        void InitializeGame()
        {
            ReadyButton.onClick.AddListener(OnReadyClicked);
            ReadyButton.gameObject.SetActive(false);

            // Give other objects a few moments to start
            StartCoroutine(StartNewGameCoroutine());
        }

        IEnumerator StartNewGameCoroutine()
        {
            yield return new WaitForSeconds(.2f);

            StartNewGame();
        }

        // TODO: use the scene navigator instead?
        public void ResetAndReplay()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void OnReadyClicked()
        {
            ReadyButton.gameObject.SetActive(false);

            countdownTimer.BeginCountdown(() =>
            {
                StartTurn();
            });
        }

        protected virtual void StartNewGame()
        {
            //CSDebug.Log($"Playing as {PlayerCaptain.Name} - \"{PlayerCaptain.Description}\"");
            PauseSystem.TogglePauseGame(false);

            RemainingPlayers = new();
            for (var i = 0; i < Players.Count; i++) RemainingPlayers.Add(i);

            StartGame();
        }

        protected virtual void Update()
        {
            if (!gameRunning)
                return;

            /*foreach (var turnMonitor in TurnMonitors)
            {
                if (turnMonitor.CheckForEndOfTurn())
                {
                    EndTurn();
                    return;
                }
            }*/
        }

        public IPlayer InstantiateAndInitializePlayer()
        {
            if (ActivePlayer != null)
                return ActivePlayer;

            for (var i = 0; i < NumberOfPlayers; i++)
            {
                Instantiate(playerPrefab).TryGetComponent(out IPlayer player);
                if (player == null)
                {
                    CSDebug.LogError($"Wrong prefab instantiated!");
                    return null;
                }

                Players.Add(player);
                IPlayer.InitializeData data = new()
                {
                    vesselClass = playerShipTypeInitialized ? PlayerVesselType : defaultPlayerVesselType,
                    domain = PlayerTeams[i],
                    PlayerName = i == 0 ? PlayerDataController.PlayerProfile.DisplayName : PlayerNames[i],
                    // PlayerUUID = PlayerNames[i]
                };
                
                // TODO - Player spawning and initializations are done using PlayerSpawner now!
                // Players[i].Initialize(data);
                Players[i].ToggleGameObject(true);
            }

            // Players[0].ToggleActive(true);
            ActivePlayer = Players[0];
            return ActivePlayer;
        }

        void StartGame()
        {
            gameRunning = true;
            CSDebug.Log($"MiniGame.StartGame, ... {Time.time}");
            // EndGameScreen.SetActive(false);
            RoundsPlayedThisGame = 0;
            OnMiniGameStart?.Invoke(gameMode, PlayerVesselType, NumberOfPlayers, IntensityLevel);
            StartRound();
        }

        void StartRound()
        {
            CSDebug.Log($"MiniGame.StartRound - Round {RoundsPlayedThisGame + 1} Start, ... {Time.time}");
            TurnsTakenThisRound = 0;
            SetupTurn();
        }

        protected void StartTurn()
        {
            /*foreach (var turnMonitor in TurnMonitors)
                turnMonitor.ResumeTurn();*/

            // ScoreTracker.StartTracking(Players[activePlayerId].PlayerName, Players[activePlayerId].Team);

            CSDebug.Log($"Player {activePlayerId + 1} Get Ready! {Time.time}");
            
            ActivePlayer.InputController.InputStatus.Paused = false;

            /*if (EnableTrails)
            {
                LocalPlayer.Vessel.VesselStatus.TrailSpawner.ForceStartSpawningTrail();
                LocalPlayer.Vessel.VesselStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
            }*/
        }

        protected virtual void EndTurn()
        {
            StartCoroutine(EndTurnCoroutine());
        }

        IEnumerator EndTurnCoroutine()
        {
            /*foreach (var turnMonitor in TurnMonitors)
                turnMonitor.PauseTurn();*/
            ActivePlayer.InputController.InputStatus.Paused = true;
            // ActivePlayer.Vessel.VesselStatus.VesselPrismController.StopSpawn();

            yield return new WaitForSeconds(EndOfTurnDelay);

            TurnsTakenThisRound++;

            // ScoreTracker.EndTurn();
            CSDebug.Log($"MiniGame.EndTurn - Turns Taken: {TurnsTakenThisRound}, ... {Time.time}");

            if (TurnsTakenThisRound >= RemainingPlayers.Count)
                EndRound();
            else
                SetupTurn();
        }

        protected void EndRound()
        {
            RoundsPlayedThisGame++;

            ResolveEliminations();

            CSDebug.Log($"MiniGame.EndRound - Rounds Played: {RoundsPlayedThisGame}, ... {Time.time}");

            if (RoundsPlayedThisGame >= NumberOfRounds || RemainingPlayers.Count <= 0)
                EndGame();
            else
                StartRound();
        }

        void EndGame()
        {
            CSDebug.Log($"MiniGame.EndGame - Rounds Played: {RoundsPlayedThisGame}, ... {Time.time}");
            // CSDebug.Log($"MiniGame.EndGame - Winner: {ScoreTracker.GetWinnerScoreData().Name} ");

            
            // TODO - In MiniGameBase, use MiniGameData to get scores
            /*foreach (var player in Players)
                CSDebug.Log($"MiniGame.EndGame - Player Score: {ScoreTracker.GetScore(player.Name)} ");*/

            if (IsDailyChallenge)
            {
                // LeaderboardManager.Instance.ReportDailyChallengeStatistic(0/*(int)ScoreTracker.GetWinnerScoreData().Score*/, ScoreTracker.GolfRules);
                DailyChallengeSystem.Instance.ReportScore(0/*(int)ScoreTracker.GetWinnerScoreData().Score*/);

                // TODO: P1 Hide play again button, or map it to use another ticket

            }
            else if (IsMission)
            {
                GameCanvas.AwardsContainer.SetActive(true);
                // Mission rewards — captain system removed, awards disabled until refactored
                int crystalsEarned = 0;
                GameCanvas.CrystalsEarnedText.text = crystalsEarned.ToString();
                GameCanvas.XPEarnedText.text = "0";
                
                // TODO - Get Captains from Data Containers, not Hanger
                // if (Hangar.Instance.HostileAI1Captain != null && !CaptainManager.Instance.IsCaptainEncountered(Hangar.Instance.HostileAI1Captain.Name))
                /*{
                    CSDebug.Log($"Encountering Captain!!! - {Hangar.Instance.HostileAI1Captain}");
                    CaptainManager.Instance.EncounterCaptain(Hangar.Instance.HostileAI1Captain.Name);
                    
                    
                    //GameCanvas.EncounteredCaptainImage.sprite = Hangar.Instance.HostileAI1Captain.Image;
                }*/
                
                // TODO - Get Captains from Data Containers, not Hanger

                /*{
                    CSDebug.Log($"Encountering Captain!!! - {Hangar.Instance.HostileAI2Captain}");
                    CaptainManager.Instance.EncounterCaptain(Hangar.Instance.HostileAI2Captain.Name);
                }*/
            }
            else if (IsTraining)
            {
                TrainingGameProgressSystem.GetGameProgress(gameMode);
                /*TrainingGameProgressSystem.ReportProgress(
                    Core.Arcade.Instance.TrainingGames.Games.First(x => x.Game.Mode == gameMode), IntensityLevel,
                    0 (int)ScoreTracker.GetWinnerScoreData().Score);*/
            }
            else
            {
                // [PLAYFAB DISABLED] Was: LeaderboardManager.Instance.ReportGameplayStatistic(...)
                // Leaderboard reporting now handled by UGS via UGSStatsManager.
            }

            UserActionSystem.Instance.CompleteAction(new UserAction(
                    UserActionType.PlayGame,
                    0,// (int)ScoreTracker.GetWinnerScoreData().Score,
                    UserAction.GetGameplayUserActionLabel(gameMode, PlayerVesselType, IntensityLevel)));

            CameraManager.Instance.SetEndCameraActive();
            PauseSystem.TogglePauseGame(true);
            gameRunning = false;
            // EndGameScreen.SetActive(true);

            // TODO - Scoreboard uses MiniGameData's events.
            /*if (NumberOfPlayers > 1)
                GameCanvas.scoreboard.ShowMultiplayerView();
            else
                GameCanvas.scoreboard.ShowSinglePlayerView();*/

            OnMiniGameEnd?.Invoke(gameMode, PlayerVesselType, NumberOfPlayers, IntensityLevel,
                0); //(int)ScoreTracker.GetWinnerScoreData().Score);
        }

        List<int> EliminatedPlayers = new();

        protected void EliminateActivePlayer()
        {
            // TODO Add to queue and resolve when round ends
            EliminatedPlayers.Add(activePlayerId);
        }

        protected void ResolveEliminations()
        {
            EliminatedPlayers.Reverse();
            foreach (var playerId in EliminatedPlayers)
                RemainingPlayers.Remove(playerId);

            EliminatedPlayers = new();

            if (RemainingPlayers.Count <= 0)
                EndGame();
        }

        protected virtual void SetupTurn()
        {
            /*ReadyNextPlayer();

            // Wait for player ready before activating turn monitor (only really relevant for time based monitor)
            foreach (var turnMonitor in TurnMonitors)
            {
                turnMonitor.NewTurn(Players[activePlayerId].PlayerName);
                turnMonitor.PauseTurn();
            }

            LocalPlayer.Transform.SetPositionAndRotation(PlayerOrigin.transform.position, PlayerOrigin.transform.rotation);
            LocalPlayer.InputController.InputStatus.Paused = true;
            LocalPlayer.Vessel.Teleport(PlayerOrigin.transform);
            LocalPlayer.Vessel.VesselStatus.VesselTransformer.ResetTransformer();
            LocalPlayer.Vessel.VesselStatus.VesselPrismController.PauseTrailSpawner();
            LocalPlayer.Vessel.VesselStatus.ResourceSystem.Reset();
            LocalPlayer.Vessel.SetResourceLevels(ResourceCollection);

            CameraManager.Instance.SetupGamePlayCameras(LocalPlayer.Vessel.VesselStatus.CameraFollowTarget);

            // For single player games, don't require the extra button press
            if (Players.Count > 1)
                ReadyButton.gameObject.SetActive(true);
            else
                StartCoroutine(StartCountdownTimerCoroutine());*/
        }

        /*protected void ReadyNextPlayer()
        {
            RemainingPlayersActivePlayerIndex++;
            RemainingPlayersActivePlayerIndex %= RemainingPlayers.Count;
            activePlayerId = RemainingPlayers[RemainingPlayersActivePlayerIndex];
            LocalPlayer = Players[activePlayerId];

            foreach (var player in Players)
            {
                CSDebug.Log($"PlayerUUID: {player.PlayerUUID}");
                player.ToggleGameObject(player.PlayerUUID == LocalPlayer.PlayerUUID);
            }
        }*/

        IEnumerator StartCountdownTimerCoroutine()
        {
            yield return new WaitForSecondsRealtime(2f);
            OnReadyClicked();
        }

        /// <summary>
        /// TODO: WIP - Allow for timed events to happen during game play
        /// </summary>
        static List<TimedCallback> TimedCallbacks = new();

        public static void ClearTimedCallbacks()
        {
            TimedCallbacks.Clear();
        }

        public static void AddTimedCallback(float invokeAfterSeconds, Action callback)
        {
            TimedCallbacks.Add(new(invokeAfterSeconds, callback));
        }

        IEnumerator TimedCallbackCoroutine(float invokeAfterSeconds, Action callback)
        {
            yield return new WaitForSeconds(invokeAfterSeconds);

            callback?.Invoke();
        }

        private void HandleGamePaused()
        {
            if (!gameRunning || ActivePlayer == null) return;

            // hand control to the AI
            Player activePlayer = ((Player)ActivePlayer);

            // TODO -  Should not directly call StartAutoPilot, use event
            // activePlayer.StartAutoPilot();
        }

        private void HandleGameResumed()
        {
            if (!gameRunning || ActivePlayer == null) return;

            // give control back to the player
            Player activePlayer = ((Player)ActivePlayer);

            // TODO -  Should not directly call StartAutoPilot, use event
            // activePlayer.StopAutoPilot();
        }

        struct TimedCallback
        {
            public float invokeAfterSeconds;
            public Action callback;

            public TimedCallback(float invokeAfterSeconds, Action callback)
            {
                this.invokeAfterSeconds = invokeAfterSeconds;
                this.callback = callback;
            }
        }
    }

    // public 
}