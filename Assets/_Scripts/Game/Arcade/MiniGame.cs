using CosmicShore.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Integrations.Firebase.Controller;
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
    public abstract class MiniGame : MonoBehaviour
    {
        [SerializeField] protected GameModes gameMode;
        [SerializeField] protected int NumberOfRounds = int.MaxValue;
        [SerializeField] protected List<TurnMonitor> TurnMonitors;
        [SerializeField] protected ScoreTracker ScoreTracker;
        [SerializeField] GameCanvas GameCanvas;
        [SerializeField] GameObject playerPrefab;
        [SerializeField] GameObject PlayerOrigin;
        [SerializeField] float EndOfTurnDelay = 0f;
        [SerializeField] bool EnableTrails = true;
        [SerializeField] ShipTypes DefaultPlayerShipType = ShipTypes.Dolphin;
        [SerializeField] SO_Captain DefaultPlayerCaptain;

        protected Button ReadyButton;
        protected GameObject EndGameScreen;
        protected MiniGameHUD HUD;
        protected List<IPlayer> Players = new();
        protected CountdownTimer countdownTimer;

        List<Teams> PlayerTeams = new() { Teams.Jade, Teams.Ruby, Teams.Gold };
        List<string> PlayerNames = new() { "PlayerOne", "PlayerTwo", "PlayerThree" };

        // Configuration set by player
        public static int NumberOfPlayers = 1;  // TODO: P1 - support excluding single player games (e.g for elimination)
        public static int IntensityLevel = 1;
        public static bool IsDailyChallenge = false;
        public static bool IsMission = false;
        public static bool IsTraining = false;
        static ShipTypes playerShipType = ShipTypes.Dolphin;
        static bool playerShipTypeInitialized;

        public static ShipTypes PlayerShipType
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
        public delegate void MiniGameStart(GameModes mode, ShipTypes ship, int playerCount, int intensity);
        public static event MiniGameStart OnMiniGameStart;
        public delegate void MiniGameEnd(GameModes mode, ShipTypes ship, int playerCount, int intensity, int highScore);
        public static event MiniGameEnd OnMiniGameEnd;

        protected virtual void Awake()
        {
            EndGameScreen = GameCanvas.EndGameScreen;
            HUD = GameCanvas.MiniGameHUD;
            ReadyButton = HUD.ReadyButton;
            countdownTimer = HUD.CountdownTimer;
            ScoreTracker.GameCanvas = GameCanvas;

            foreach (var turnMonitor in TurnMonitors)
                if (turnMonitor is TimeBasedTurnMonitor tbtMonitor)
                    tbtMonitor.Display = HUD.RoundTimeDisplay;
                else if (turnMonitor is VolumeCreatedTurnMonitor hvtMonitor) // TODO: consolidate with above
                    hvtMonitor.Display = HUD.RoundTimeDisplay;
                else if (turnMonitor is ShipCollisionTurnMonitor scMonitor) // TODO: consolidate with above
                    scMonitor.Display = HUD.RoundTimeDisplay;
                else if (turnMonitor is DistanceTurnMonitor dtMonitor) // TODO: consolidate with above
                    dtMonitor.Display = HUD.RoundTimeDisplay;

            GameManager.UnPauseGame();
        }

        protected virtual void Start()
        {
            InstantiateAndInitializePlayer();
        }

        protected virtual void OnEnable()
        {
            GameManager.OnPlayGame += InitializeGame;
            OnMiniGameStart += FirebaseAnalyticsController.LogEventMiniGameStart;
            OnMiniGameEnd += FirebaseAnalyticsController.LogEventMiniGameEnd;
        }

        protected virtual void OnDisable()
        {
            GameManager.OnPlayGame -= InitializeGame;
            OnMiniGameStart -= FirebaseAnalyticsController.LogEventMiniGameStart;
            OnMiniGameEnd -= FirebaseAnalyticsController.LogEventMiniGameEnd;
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

        public void Exit()
        {
            GameManager.ReturnToLobby();
        }

        public void OnReadyClicked()
        {
            ReadyButton.gameObject.SetActive(false);

            countdownTimer.BeginCountdown(() =>
            {
                StartTurn();

                ActivePlayer.InputController.InputStatus.Paused = false;

                if (EnableTrails)
                {
                    ActivePlayer.Ship.ShipStatus.TrailSpawner.ForceStartSpawningTrail();
                    ActivePlayer.Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
                }
            });
        }

        protected virtual void StartNewGame()
        {
            //Debug.Log($"Playing as {PlayerCaptain.Name} - \"{PlayerCaptain.Description}\"");
            if (PauseSystem.Paused) PauseSystem.TogglePauseGame();

            RemainingPlayers = new();
            for (var i = 0; i < Players.Count; i++) RemainingPlayers.Add(i);

            StartGame();
        }

        protected virtual void Update()
        {
            if (!gameRunning)
                return;

            foreach (var turnMonitor in TurnMonitors)
            {
                if (turnMonitor.CheckForEndOfTurn())
                {
                    EndTurn();
                    return;
                }
            }
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
                    DefaultShipType = playerShipTypeInitialized ? PlayerShipType : DefaultPlayerShipType,
                    Team = PlayerTeams[i],
                    PlayerName = i == 0 ? PlayerDataController.PlayerProfile.DisplayName : PlayerNames[i],
                    PlayerUUID = PlayerNames[i],
                    Name = "Player" + (i + 1)
                };
                Players[i].Initialize(data);
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
            EndGameScreen.SetActive(false);
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
            foreach (var turnMonitor in TurnMonitors)
                turnMonitor.ResumeTurn();

            ScoreTracker.StartTurn(Players[activePlayerId].PlayerName, Players[activePlayerId].Team);

            Debug.Log($"Player {activePlayerId + 1} Get Ready! {Time.time}");
        }

        protected virtual void EndTurn()
        {
            StartCoroutine(EndTurnCoroutine());
        }

        IEnumerator EndTurnCoroutine()
        {
            foreach (var turnMonitor in TurnMonitors)
                turnMonitor.PauseTurn();
            ActivePlayer.InputController.InputStatus.Paused = true;
            ActivePlayer.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();

            yield return new WaitForSeconds(EndOfTurnDelay);

            TurnsTakenThisRound++;

            ScoreTracker.EndTurn();
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
            Debug.Log($"MiniGame.EndGame - Winner: {ScoreTracker.GetWinner()} ");

            foreach (var player in Players)
                Debug.Log($"MiniGame.EndGame - Player Score: {ScoreTracker.GetScore(player.PlayerName)} ");

            if (IsDailyChallenge)
            {
                LeaderboardManager.Instance.ReportDailyChallengeStatistic(ScoreTracker.GetHighScore(), ScoreTracker.GolfRules);
                DailyChallengeSystem.Instance.ReportScore(ScoreTracker.GetHighScore());

                // TODO: P1 Hide play again button, or map it to use another ticket

            }
            else if (IsMission)
            {
                GameCanvas.AwardsContainer.SetActive(true);
                // Award Crystals
                Debug.Log($"Mission EndGame - Award Mission Crystals -  score:{ScoreTracker.GetHighScore()}");
                Debug.Log($"Mission EndGame - Award Mission Crystals -  element:{PlayerCaptain.PrimaryElement}");
                int crystalsEarned = 0;
                switch (PlayerCaptain.PrimaryElement)
                {
                    case Element.Charge:
                        crystalsEarned = (int)(StatsManager.Instance.LastRoundPlayerStats[PlayerDataController.PlayerProfile.DisplayName].ChargeCrystalValue * 100);
                        CatalogManager.Instance.GrantElementalCrystals(crystalsEarned, Element.Charge);
                        break;
                    case Element.Mass:
                        crystalsEarned = (int)(StatsManager.Instance.LastRoundPlayerStats[PlayerDataController.PlayerProfile.DisplayName].MassCrystalValue * 100);
                        CatalogManager.Instance.GrantElementalCrystals(crystalsEarned, Element.Mass);
                        break;
                    case Element.Space:
                        crystalsEarned = (int)(StatsManager.Instance.LastRoundPlayerStats[PlayerDataController.PlayerProfile.DisplayName].SpaceCrystalValue * 100);
                        CatalogManager.Instance.GrantElementalCrystals(crystalsEarned, Element.Space);
                        break;
                    case Element.Time:
                        crystalsEarned = (int)(StatsManager.Instance.LastRoundPlayerStats[PlayerDataController.PlayerProfile.DisplayName].TimeCrystalValue * 100);
                        CatalogManager.Instance.GrantElementalCrystals(crystalsEarned, Element.Time);
                        break;
                }
                Debug.Log($"Mission EndGame - Award Mission Crystals - Player has earned {crystalsEarned} crystals");
                GameCanvas.CrystalsEarnedImage.sprite = CaptainManager.Instance.GetCaptainByName(PlayerCaptain.Name).SO_Element.GetFullIcon(true);
                GameCanvas.CrystalsEarnedText.text = crystalsEarned.ToString();

                // Award XP
                Debug.Log($"Mission EndGame - Award Mission XP -  score:{ScoreTracker.GetHighScore()}, element:{PlayerCaptain.PrimaryElement}");
                XpHandler.IssueXP(CaptainManager.Instance.GetCaptainByName(PlayerCaptain.Name), 10);
                GameCanvas.XPEarnedText.text = "10";

                // Report any encountered captains
                Debug.Log($"Mission EndGame - Unlock Mission Captains");
                if (Hangar.Instance.HostileAI1Captain != null && !CaptainManager.Instance.IsCaptainEncountered(Hangar.Instance.HostileAI1Captain.Name))
                {
                    Debug.Log($"Encountering Captain!!! - {Hangar.Instance.HostileAI1Captain}");
                    CaptainManager.Instance.EncounterCaptain(Hangar.Instance.HostileAI1Captain.Name);
                    
                    
                    //GameCanvas.EncounteredCaptainImage.sprite = Hangar.Instance.HostileAI1Captain.Image;
                }
                if (Hangar.Instance.HostileAI2Captain != null && !CaptainManager.Instance.IsCaptainEncountered(Hangar.Instance.HostileAI2Captain.Name))
                {
                    Debug.Log($"Encountering Captain!!! - {Hangar.Instance.HostileAI2Captain}");
                    CaptainManager.Instance.EncounterCaptain(Hangar.Instance.HostileAI2Captain.Name);
                }
            }
            else if (IsTraining)
            {
                TrainingGameProgressSystem.GetGameProgress(gameMode);
                TrainingGameProgressSystem.ReportProgress(Core.Arcade.Instance.TrainingGames.Games.First(x => x.Game.Mode == gameMode), IntensityLevel, ScoreTracker.GetHighScore());
            }
            else
                LeaderboardManager.Instance.ReportGameplayStatistic(gameMode, PlayerShipType, IntensityLevel, ScoreTracker.GetHighScore(), ScoreTracker.GolfRules);

            UserActionSystem.Instance.CompleteAction(new UserAction(
                    UserActionType.PlayGame,
                    ScoreTracker.GetHighScore(),
                    UserAction.GetGameplayUserActionLabel(gameMode, PlayerShipType, IntensityLevel)));

            CameraManager.Instance.SetEndCameraActive();
            PauseSystem.TogglePauseGame();
            gameRunning = false;
            EndGameScreen.SetActive(true);

            if (NumberOfPlayers > 1)
                GameCanvas.scoreboard.ShowMultiplayerView();
            else
                GameCanvas.scoreboard.ShowSinglePlayerView();

            OnMiniGameEnd?.Invoke(gameMode, PlayerShipType, NumberOfPlayers, IntensityLevel, ScoreTracker.GetHighScore());
        }

        void LoopActivePlayerIndex()
        {
            RemainingPlayersActivePlayerIndex++;
            RemainingPlayersActivePlayerIndex %= RemainingPlayers.Count;
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
            foreach (var turnMonitor in TurnMonitors)
            {
                turnMonitor.NewTurn(Players[activePlayerId].PlayerName);
                turnMonitor.PauseTurn();
            }

            ActivePlayer.Transform.SetPositionAndRotation(PlayerOrigin.transform.position, PlayerOrigin.transform.rotation);
            ActivePlayer.InputController.InputStatus.Paused = true;
            ActivePlayer.Ship.Teleport(PlayerOrigin.transform);
            ActivePlayer.Ship.ShipStatus.ShipTransformer.Reset();
            ActivePlayer.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
            ActivePlayer.Ship.ShipStatus.ResourceSystem.Reset();
            ActivePlayer.Ship.SetResourceLevels(ResourceCollection);

            CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.ShipStatus.FollowTarget);

            // For single player games, don't require the extra button press
            if (Players.Count > 1)
                ReadyButton.gameObject.SetActive(true);
            else
                StartCoroutine(StartCountdownTimerCoroutine());
        }

        protected void ReadyNextPlayer()
        {
            LoopActivePlayerIndex();
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
}