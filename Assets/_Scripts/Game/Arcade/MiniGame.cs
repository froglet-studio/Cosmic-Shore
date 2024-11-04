using CosmicShore.Core;
using CosmicShore.Game.IO;
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
    public class MiniGame : MonoBehaviour
    {
        [SerializeField] protected GameModes gameMode;
        [SerializeField] protected int NumberOfRounds = int.MaxValue;
        [SerializeField] protected List<TurnMonitor> TurnMonitors;
        [SerializeField] protected ScoreTracker ScoreTracker;
        [SerializeField] protected GameCanvas GameCanvas;
        [SerializeField] protected Player playerPrefab;
        [SerializeField] protected GameObject PlayerOrigin;
        [SerializeField] float EndOfTurnDelay = 0f;
        [SerializeField] bool EnableTrails = true;
        [SerializeField] protected ShipTypes DefaultPlayerShipType = ShipTypes.Dolphin;
        [SerializeField] SO_Captain DefaultPlayerCaptain;

        [SerializeField] private float readyCountdownDelay = 2f;  // Replaces hardcoded 2f
        [SerializeField] private float newGameStartDelay = 0.2f;  // Replaces hardcoded 0.2f in StartNewGameCoroutine

        protected Button ReadyButton;
        protected GameObject EndGameScreen;
        protected MiniGameHUD HUD;
        protected List<Player> Players;
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
        public static ResourceCollection ShipResources = new(.5f, .5f, .5f, .5f);

        // Game State Tracking
        protected int TurnsTakenThisRound;
        int RoundsPlayedThisGame;

        // PlayerId Tracking
        int activePlayerId;
        int RemainingPlayersActivePlayerIndex = -1;
        protected List<int> RemainingPlayers = new();
        [HideInInspector] public Player ActivePlayer;
        protected bool gameRunning;

        // Firebase analytics events
        public delegate void MiniGameStart(GameModes mode, ShipTypes ship, int playerCount, int intensity);
        public static event MiniGameStart OnMiniGameStart;
        public delegate void MiniGameEnd(GameModes mode, ShipTypes ship, int playerCount, int intensity, int highScore);
        public static event MiniGameEnd OnMiniGameEnd;

        /// <summary>
        /// Initialize essential game components and setup TurnMonitors.
        /// TurnMonitors are assigned a display, which will be shown in the UI to represent various game states.
        /// </summary>
        protected virtual void Awake()
        {
            EndGameScreen = GameCanvas.EndGameScreen;
            HUD = GameCanvas.MiniGameHUD;
            ReadyButton = HUD.ReadyButton;
            countdownTimer = HUD.CountdownTimer;
            ScoreTracker.GameCanvas = GameCanvas;

            foreach (var turnMonitor in TurnMonitors)
                turnMonitor.Display = HUD.RoundTimeDisplay; // Assign display to all, even if not used

            GameManager.UnPauseGame();
        }

        /// <summary>
        /// Start the game, initialize players, and prepare the game for player interaction.
        /// </summary>
        protected virtual void Start()
        {
            InitializePlayers();

            ReadyButton.onClick.AddListener(OnReadyClicked);
            ReadyButton.gameObject.SetActive(false);

            // Start the game after a small delay to ensure all components are ready
            StartCoroutine(StartNewGameCoroutine());
        }

        protected virtual void OnEnable()
        {
            OnMiniGameStart += FirebaseAnalyticsController.LogEventMiniGameStart;
            OnMiniGameEnd += FirebaseAnalyticsController.LogEventMiniGameEnd;
        }

        protected virtual void OnDisable()
        {
            OnMiniGameStart -= FirebaseAnalyticsController.LogEventMiniGameStart;
            OnMiniGameEnd -= FirebaseAnalyticsController.LogEventMiniGameEnd;
        }

        /// <summary>
        /// Initialize all players, setting their teams, names, and ships.
        /// </summary>
        protected virtual void InitializePlayers()
        {
            Players = new List<Player>();
            for (var i = 0; i < NumberOfPlayers; i++)
            {
                var player = Instantiate(playerPrefab);
                player.defaultShip = playerShipTypeInitialized ? PlayerShipType : DefaultPlayerShipType;
                player.Team = PlayerTeams[i];
                player.PlayerName = i == 0 ? PlayerDataController.PlayerProfile.DisplayName : PlayerNames[i];
                player.PlayerUUID = PlayerNames[i];
                player.name = "Player" + (i + 1);
                player.gameObject.SetActive(true);
                Players.Add(player);
            }
        }

        /// <summary>
        /// Coroutine to start a new game after a delay to allow other components to initialize.
        /// </summary>
        IEnumerator StartNewGameCoroutine()
        {
            yield return new WaitForSeconds(newGameStartDelay);
            StartNewGame();
        }

        /// <summary>
        /// Starts a countdown for the player to be ready. Once the countdown ends, the player's turn begins.
        /// </summary>
        public void OnReadyClicked()
        {
            ReadyButton.gameObject.SetActive(false);

            countdownTimer.BeginCountdown(() =>
            {
                StartTurn();

                ActivePlayer.GetComponent<InputController>().SetPaused(false);

                if (EnableTrails)
                {
                    ActivePlayer.Ship.TrailSpawner.ForceStartSpawningTrail();
                    ActivePlayer.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(readyCountdownDelay);
                }
            });
        }


        public virtual void StartNewGame()
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

        void StartGame()
        {
            Debug.Log($"MiniGame.StartGame, ... {Time.time}");
            gameRunning = true;
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
            ActivePlayer.GetComponent<InputController>().SetPaused(true);
            ActivePlayer.Ship.TrailSpawner.PauseTrailSpawner();

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

                // TODO: Hide play again button, or map it to use another ticket

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
                    GameCanvas.EncounteredCaptainImage.sprite = Hangar.Instance.HostileAI1Captain.Image;
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
            if (!EliminatedPlayers.Contains(activePlayerId))
                EliminatedPlayers.Add(activePlayerId);
        }

        protected void ResolveEliminations()
        {
            foreach (var playerId in EliminatedPlayers)
                RemainingPlayers.Remove(playerId);

            EliminatedPlayers.Clear();

            if (RemainingPlayers.Count <= 0)
                EndGame();
        }

        protected void ReadyNextPlayer()
        {
            LoopActivePlayerIndex();
            activePlayerId = RemainingPlayers[RemainingPlayersActivePlayerIndex];
            ActivePlayer = Players[activePlayerId];

            foreach (var player in Players)
            {
                Debug.Log($"PlayerUUID: {player.PlayerUUID}");
                player.gameObject.SetActive(player.PlayerUUID == ActivePlayer.PlayerUUID);
            }

            Player.ActivePlayer = ActivePlayer;
        }

        /// <summary>
        /// Setup turn for the active player, ensuring their position, rotation, and ship settings are correct.
        /// </summary>
        protected virtual void SetupTurn()
        {
            ReadyNextPlayer();

            // Initialize turn monitors and setup player state
            foreach (var turnMonitor in TurnMonitors)
            {
                turnMonitor.NewTurn(Players[activePlayerId].PlayerName);
                turnMonitor.PauseTurn(); // Paused until player is ready
            }

            ActivePlayer.transform.SetPositionAndRotation(PlayerOrigin.transform.position, PlayerOrigin.transform.rotation);
            ActivePlayer.GetComponent<InputController>().SetPaused(true);
            ActivePlayer.Ship.Teleport(PlayerOrigin.transform);
            ActivePlayer.Ship.GetComponent<ShipTransformer>().Reset();
            ActivePlayer.Ship.TrailSpawner.PauseTrailSpawner();
            ActivePlayer.Ship.ResourceSystem.Reset();
            ActivePlayer.Ship.SetResourceLevels(ShipResources);

            CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.FollowTarget);

            // Automatically start the countdown for single-player games
            if (Players.Count > 1)
                ReadyButton.gameObject.SetActive(true);
            else
                StartCoroutine(StartCountdownTimerCoroutine());
        }

        IEnumerator StartCountdownTimerCoroutine()
        {
            yield return new WaitForSecondsRealtime(readyCountdownDelay);
            OnReadyClicked();
        }
    }
}
