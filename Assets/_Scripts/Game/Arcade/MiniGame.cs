using CosmicShore.Core;
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
using System.Linq;


namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// DEPRECATED - Use R_MiniGameBase instead
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
        [SerializeField] ShipClassType DefaultPlayerShipType = ShipClassType.Dolphin;
        [SerializeField] SO_Captain DefaultPlayerCaptain;

        protected Button ReadyButton;

        // protected GameObject EndGameScreen;
        protected MiniGameHUD HUD;
        protected List<IPlayer> Players = new();
        protected CountdownTimer countdownTimer;

        List<Teams> PlayerTeams = new() { Teams.Jade, Teams.Ruby, Teams.Gold };
        List<string> PlayerNames = new() { "PlayerOne", "PlayerTwo", "PlayerThree" };

        // Configuration set by player
        public static int NumberOfPlayers = 1; // TODO: P1 - support excluding single player games (e.g for elimination)
        public static int IntensityLevel = 1;
        public static bool IsDailyChallenge = false;
        public static bool IsMission = false;
        public static bool IsTraining = false;
        static ShipClassType playerShipType = ShipClassType.Dolphin;
        static bool playerShipTypeInitialized;

        public static ShipClassType PlayerShipType
        {
            get => playerShipType;
            set
            {
                playerShipType = value;
                playerShipTypeInitialized = true;
            }
        }

        public static SO_Captain PlayerCaptain;
        public static ResourceCollection ResourceCollection = new(.5f, .5f, .5f, .5f);

        // Game State Tracking
        protected int TurnsTakenThisRound;
        int RoundsPlayedThisGame;

        // PlayerId Tracking
        int activePlayerId;
        int RemainingPlayersActivePlayerIndex = -1;
        protected List<int> RemainingPlayers = new();
        protected bool gameRunning;

        public IPlayer ActivePlayer { get; protected set; }

        // Firebase analytics events
        public delegate void MiniGameStart(GameModes mode, ShipClassType ship, int playerCount, int intensity);

        public static event MiniGameStart OnMiniGameStart;

        public delegate void MiniGameEnd(GameModes mode, ShipClassType ship, int playerCount, int intensity,
            int highScore);

        public static event MiniGameEnd OnMiniGameEnd;

        protected virtual void Awake()
        {
            // EndGameScreen = GameCanvas.EndGameScreen;
            HUD = GameCanvas.MiniGameHUD;
            ReadyButton = HUD.View.ReadyButton;
            countdownTimer = HUD.View.CountdownTimer;
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

            // TODO - Replaced in R_MiniGameBase
            // OnMiniGameStart += FirebaseAnalyticsController.LogEventMiniGameStart;
            // OnMiniGameEnd += FirebaseAnalyticsController.LogEventMiniGameEnd;
            PauseSystem.OnGamePaused += HandleGamePaused;
            PauseSystem.OnGameResumed += HandleGameResumed;
        }

        protected virtual void OnDisable()
        {
            // GameManager.OnPlayGame -= InitializeGame;

            // TODO - Replaced in R_MiniGameBase
            // OnMiniGameStart -= FirebaseAnalyticsController.LogEventMiniGameStart;
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

            countdownTimer.BeginCountdown(() => { StartTurn(); });
        }

        protected virtual void StartNewGame()
        {
            //Debug.Log($"Playing as {PlayerCaptain.Name} - \"{PlayerCaptain.Description}\"");
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
                    Debug.LogError($"Wrong prefab instantiated!");
                    return null;
                }

                Players.Add(player);
                IPlayer.InitializeData data = new()
                {
                    ShipClass = playerShipTypeInitialized ? PlayerShipType : DefaultPlayerShipType,
                    Team = PlayerTeams[i],
                    PlayerName = i == 0 ? PlayerDataController.PlayerProfile.DisplayName : PlayerNames[i],
                    PlayerUUID = PlayerNames[i]
                };

                // TODO - Player spawning and initializations are done using PlayerSpawner now!
                // Players[i].Initialize(data);
                Players[i].ToggleGameObject(true);
            }

            Players[0].ToggleActive(true);
            ActivePlayer = Players[0];
            return ActivePlayer;
        }

        void StartGame()
        {
            gameRunning = true;
            Debug.Log($"MiniGame.StartGame, ... {Time.time}");
            // EndGameScreen.SetActive(false);
            RoundsPlayedThisGame = 0;
            OnMiniGameStart?.Invoke(gameMode, PlayerShipType, NumberOfPlayers, IntensityLevel);
            StartRound();
        }

        void StartRound()
        {
            Debug.Log($"MiniGame.StartRound - Round {RoundsPlayedThisGame + 1} Start, ... {Time.time}");
            TurnsTakenThisRound = 0;
            SetupTurn();
        }

        protected void StartTurn()
        {
            /*foreach (var turnMonitor in TurnMonitors)
                turnMonitor.ResumeTurn();*/

            // ScoreTracker.StartTracking(Players[activePlayerId].PlayerName, Players[activePlayerId].Team);

            Debug.Log($"Player {activePlayerId + 1} Get Ready! {Time.time}");

            ActivePlayer.InputController.InputStatus.Paused = false;

            if (EnableTrails)
            {
                ActivePlayer.Ship.ShipStatus.TrailSpawner.ForceStartSpawningTrail();
                ActivePlayer.Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
            }
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
            ActivePlayer.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();

            yield return new WaitForSeconds(EndOfTurnDelay);

            TurnsTakenThisRound++;

            // ScoreTracker.EndTurn();
            Debug.Log($"MiniGame.EndTurn - Turns Taken: {TurnsTakenThisRound}, ... {Time.time}");

            if (TurnsTakenThisRound >= RemainingPlayers.Count)
                EndRound();
            else
                SetupTurn();
        }

        protected void EndRound()
        {
            RoundsPlayedThisGame++;

            ResolveEliminations();

            Debug.Log($"MiniGame.EndRound - Rounds Played: {RoundsPlayedThisGame}, ... {Time.time}");

            if (RoundsPlayedThisGame >= NumberOfRounds || RemainingPlayers.Count <= 0)
                EndGame();
            else
                StartRound();
        }

        void EndGame()
        {
            Debug.Log($"MiniGame.EndGame - Rounds Played: {RoundsPlayedThisGame}, ... {Time.time}");
            // Debug.Log($"MiniGame.EndGame - Winner: {ScoreTracker.GetWinnerScoreData().Name} ");


            // TODO - In MiniGameBase, use MiniGameData to get scores
            /*foreach (var player in Players)
                Debug.Log($"MiniGame.EndGame - Player Score: {ScoreTracker.GetScore(player.Name)} ");*/

            if (IsDailyChallenge)
            {
                // LeaderboardManager.Instance.ReportDailyChallengeStatistic(0/*(int)ScoreTracker.GetWinnerScoreData().Score*/, ScoreTracker.GolfRules);
                DailyChallengeSystem.Instance.ReportScore(0 /*(int)ScoreTracker.GetWinnerScoreData().Score*/);

                // TODO: P1 Hide play again button, or map it to use another ticket
            }
            else if (IsMission)
            {
                GameCanvas.AwardsContainer.SetActive(true);
                // Award Crystals
                Debug.Log(
                    $"Mission EndGame - Award Mission Crystals -  score:{0 /*(int)ScoreTracker.GetWinnerScoreData().Score*/}");
                Debug.Log($"Mission EndGame - Award Mission Crystals -  element:{PlayerCaptain.PrimaryElement}");
                int crystalsEarned = 0;
                switch (PlayerCaptain.PrimaryElement)
                {
                    case Element.Charge:
                        crystalsEarned =
                            0; // (int)(StatsManager.Instance.LastRoundPlayerStats[PlayerDataController.PlayerProfile.DisplayName].ChargeCrystalValue * 100);
                        CatalogManager.Instance.GrantElementalCrystals(crystalsEarned, Element.Charge);
                        break;
                    case Element.Mass:
                        crystalsEarned =
                            0; // (int)(StatsManager.Instance.LastRoundPlayerStats[PlayerDataController.PlayerProfile.DisplayName].MassCrystalValue * 100);
                        CatalogManager.Instance.GrantElementalCrystals(crystalsEarned, Element.Mass);
                        break;
                    case Element.Space:
                        crystalsEarned =
                            0; // (int)(StatsManager.Instance.LastRoundPlayerStats[PlayerDataController.PlayerProfile.DisplayName].SpaceCrystalValue * 100);
                        CatalogManager.Instance.GrantElementalCrystals(crystalsEarned, Element.Space);
                        break;
                    case Element.Time:
                        crystalsEarned =
                            0; // (int)(StatsManager.Instance.LastRoundPlayerStats[PlayerDataController.PlayerProfile.DisplayName].TimeCrystalValue * 100);
                        CatalogManager.Instance.GrantElementalCrystals(crystalsEarned, Element.Time);
                        break;
                }

                Debug.Log($"Mission EndGame - Award Mission Crystals - Player has earned {crystalsEarned} crystals");
                GameCanvas.CrystalsEarnedImage.sprite = CaptainManager.Instance.GetCaptainByName(PlayerCaptain.Name)
                    .SO_Element.GetFullIcon(true);
                GameCanvas.CrystalsEarnedText.text = crystalsEarned.ToString();

                // Award XP
                Debug.Log(
                    $"Mission EndGame - Award Mission XP -  score:{0 /*(int)ScoreTracker.GetWinnerScoreData().Score*/}, element:{PlayerCaptain.PrimaryElement}");
                XpHandler.IssueXP(CaptainManager.Instance.GetCaptainByName(PlayerCaptain.Name), 10);
                GameCanvas.XPEarnedText.text = "10";

                // Report any encountered captains
                Debug.Log($"Mission EndGame - Unlock Mission Captains");

                // TODO - Get Captains from Data Containers, not Hanger
                // if (Hangar.Instance.HostileAI1Captain != null && !CaptainManager.Instance.IsCaptainEncountered(Hangar.Instance.HostileAI1Captain.Name))
                /*{
                    Debug.Log($"Encountering Captain!!! - {Hangar.Instance.HostileAI1Captain}");
                    CaptainManager.Instance.EncounterCaptain(Hangar.Instance.HostileAI1Captain.Name);


                    //GameCanvas.EncounteredCaptainImage.sprite = Hangar.Instance.HostileAI1Captain.Image;
                }*/

                // TODO - Get Captains from Data Containers, not Hanger

                /*{
                    Debug.Log($"Encountering Captain!!! - {Hangar.Instance.HostileAI2Captain}");
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
                LeaderboardManager.Instance.ReportGameplayStatistic(gameMode, PlayerShipType, IntensityLevel,
                    0 /*(int)ScoreTracker.GetWinnerScoreData().Score*/, false); // ScoreTracker.GolfRules);

            UserActionSystem.Instance.CompleteAction(new UserAction(
                UserActionType.PlayGame,
                0, // (int)ScoreTracker.GetWinnerScoreData().Score,
                UserAction.GetGameplayUserActionLabel(gameMode, PlayerShipType, IntensityLevel)));

            CameraManager.Instance.SetEndCameraActive();
            PauseSystem.TogglePauseGame(true);
            gameRunning = false;
            // EndGameScreen.SetActive(true);

            // TODO - Scoreboard uses MiniGameData's events.
            /*if (NumberOfPlayers > 1)
                GameCanvas.scoreboard.ShowMultiplayerView();
            else
                GameCanvas.scoreboard.ShowSinglePlayerView();*/

            OnMiniGameEnd?.Invoke(gameMode, PlayerShipType, NumberOfPlayers, IntensityLevel,
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
            ReadyNextPlayer();

            // Wait for player ready before activating turn monitor (only really relevant for time based monitor)
            /*foreach (var turnMonitor in TurnMonitors)
            {
                turnMonitor.NewTurn(Players[activePlayerId].PlayerName);
                turnMonitor.PauseTurn();
            }*/

            ActivePlayer.Transform.SetPositionAndRotation(PlayerOrigin.transform.position,
                PlayerOrigin.transform.rotation);
            ActivePlayer.InputController.InputStatus.Paused = true;
            ActivePlayer.Ship.Teleport(PlayerOrigin.transform);
            ActivePlayer.Ship.ShipStatus.ShipTransformer.ResetShipTransformer();
            ActivePlayer.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
            ActivePlayer.Ship.ShipStatus.ResourceSystem.Reset();
            ActivePlayer.Ship.SetResourceLevels(ResourceCollection);

            // CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.ShipStatus.FollowTarget);

            // For single player games, don't require the extra button press
            if (Players.Count > 1)
                ReadyButton.gameObject.SetActive(true);
            else
                StartCoroutine(StartCountdownTimerCoroutine());
        }

        protected void ReadyNextPlayer()
        {
            RemainingPlayersActivePlayerIndex++;
            RemainingPlayersActivePlayerIndex %= RemainingPlayers.Count;
            activePlayerId = RemainingPlayers[RemainingPlayersActivePlayerIndex];
            ActivePlayer = Players[activePlayerId];

            foreach (var player in Players)
            {
                Debug.Log($"PlayerUUID: {player.PlayerUUID}");
                player.ToggleGameObject(player.PlayerUUID == ActivePlayer.PlayerUUID);
            }
        }

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
            R_Player activePlayer = ((R_Player)ActivePlayer);

            // TODO -  Should not directly call StartAutoPilot, use event
            // activePlayer.StartAutoPilot();
        }

        private void HandleGameResumed()
        {
            if (!gameRunning || ActivePlayer == null) return;

            // give control back to the player
            R_Player activePlayer = ((R_Player)ActivePlayer);

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