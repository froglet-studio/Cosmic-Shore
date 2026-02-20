using CosmicShore.Game;
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Arcade.Tournament;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models.Enums;
using CosmicShore.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;


namespace CosmicShore.Core
{
    /// <summary>
    /// Singleton class responsible for interacting with games
    /// </summary>
    public class Arcade : SingletonPersistent<Arcade>
    {
        [field: FormerlySerializedAs("FactionMissionGames")]
        [field: SerializeField] public SO_MissionList MissionGames { get; private set; }
        [field: SerializeField] public SO_GameList ArcadeGames { get; private set; }
        [field: SerializeField] public SO_TrainingGameList TrainingGames { get; private set; }

        [FormerlySerializedAs("miniGameData")] [SerializeField] private GameDataSO gameData;
        
        // [SerializeField] ScriptableEventArcadeData OnArcadeMultiplayerModeSelected;
        
        /*[SerializeField]
        ScriptableEventNoParam _onStartSceneTransition;*/

        Dictionary<GameModes, SO_ArcadeGame> ArcadeGameLookup = new();
        Dictionary<GameModes, SO_TrainingGame> TrainingGameLookup = new();
        Dictionary<GameModes, SO_Mission> MissionLookup = new();
        Animator SceneTransitionAnimator;

        public override void Awake()
        {
            base.Awake();

            foreach (var game in ArcadeGames.Games)
                ArcadeGameLookup.Add(game.Mode, game);

            foreach (var trainingGame in TrainingGames.Games)
                TrainingGameLookup.Add(trainingGame.Game.Mode, trainingGame);

            foreach (var game in MissionGames.Games)
                MissionLookup.Add(game.Mode, game);
        }

        public void LaunchMission(GameModes gameMode, SO_Captain captain, int intensity)
        {
            gameData.PlayerCaptain = captain;
            gameData.ResourceCollection = CaptainManager.Instance.GetCaptainByName(captain.Name).ResourceLevels;
            gameData.IsDailyChallenge = false;
            gameData.IsTraining = false;
            gameData.IsMission = true;
            gameData.IsMultiplayerMode = false;
            gameData.GameMode = gameMode;
            gameData.SelectedPlayerCount.Value = 1;
            gameData.SelectedIntensity.Value = intensity;
            gameData.SceneName = MissionLookup[gameMode].SceneName;
            gameData.InvokeGameLaunch();
            
            /*MiniGame.PlayerCaptain = captain;
            MiniGame.PlayerShipType = captain.Vessel.Class;


            // TODO - Not in hanger, but do this in MiniGameData, or PlayerSpawner
            Hangar.Instance.SetAiIntensityLevel(intensity);


            var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.PORT);
            screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.FACTION_MISSION);
            

            StartCoroutine(LaunchGameCoroutine(MissionLookup[gameMode].SceneName));
            */
        }

        public void LaunchArcadeGame(GameModes gameMode, VesselClassType vessel, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isMultiplayer, bool isDailyChallenge = false)
        {
            gameData.ResourceCollection = shipResources;
            gameData.IsDailyChallenge = isDailyChallenge;
            gameData.IsTraining = false;
            gameData.IsMission = false;
            gameData.GameMode = gameMode;
            
            // For multiplayer-capable games with only 1 human player, run locally with AI
            // instead of doing online matchmaking. Use gameData.SelectedPlayerCount (set by
            // the config modal) rather than the legacy numberOfPlayers parameter.
            gameData.IsMultiplayerMode = isMultiplayer && gameData.SelectedPlayerCount.Value > 1;
            gameData.SceneName = ArcadeGameLookup[gameMode].SceneName;
            gameData.InvokeGameLaunch();

            /*MiniGame.PlayerShipType = vessel;
            MiniGame.ResourceCollection = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            MiniGame.IsTraining = false;
            MiniGame.IsMission = false;

            // TODO - Not in hanger, but do this in MiniGameData, or PlayerSpawner
            Hangar.Instance.SetAiIntensityLevel(intensity);


            var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.ARCADE);
            if (isDailyChallenge)
                screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.DAILY_CHALLENGE);

            // TODO: Refactor later to support multiple multiplayer game modes. We can add a bool isMultiplayer paramter to SO_Game later if needed.
            if (SO_Game.IsMultiplayerModes(gameMode))
            {
                // TODO: Remove MultiplayerSetup dependency

                // _multiplayerSetup.ExecuteMultiplayerSetup(ArcadeGameLookup[gameMode].SceneName, GetArcadeGameByMode(gameMode).MaxPlayersForMultiplayer);
                OnArcadeMultiplayerModeSelected.Raise(new ArcadeData()
                {
                    SceneName = ArcadeGameLookup[gameMode].SceneName,
                    MaxPlayers = GetArcadeGameByMode(gameMode).MaxPlayersForMultiplayer
                });
                return;
            }

            StartCoroutine(LaunchGameCoroutine(ArcadeGameLookup[gameMode].SceneName));*/
        }

        /*SO_Game GetArcadeGameByMode(GameModes gameMode)
        {
            return ArcadeGames.Games.Where(x => x.Mode == gameMode).FirstOrDefault();
        }*/

        public void LaunchTrainingGame(GameModes gameMode, VesselClassType vessel, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            gameData.ResourceCollection = shipResources;
            gameData.IsDailyChallenge = isDailyChallenge;
            gameData.IsTraining = !isDailyChallenge;
            gameData.IsMission = false;
            gameData.IsMultiplayerMode = false;
            gameData.SceneName = TrainingGameLookup[gameMode].Game.SceneName;
            gameData.InvokeGameLaunch();
            
            /*MiniGame.PlayerShipType = vessel;
            MiniGame.ResourceCollection = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            MiniGame.IsTraining = !isDailyChallenge;
            MiniGame.IsMission = false;
            Hangar.Instance.SetAiIntensityLevel(intensity);


            var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            if (isDailyChallenge)
            {
                screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.ARCADE);
                screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.DAILY_CHALLENGE);
            }
            else
                screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.HANGAR);
            

            StartCoroutine(LaunchGameCoroutine(TrainingGameLookup[gameMode].Game.SceneName));*/
        }

        public SO_TrainingGame GetTrainingGameByMode(GameModes gameMode)
        {
            return TrainingGames.Games.Where(x => x.Game.Mode == gameMode).FirstOrDefault();
        }

        public SO_ArcadeGame GetArcadeGameSOByName(string displayName)
        {
            return ArcadeGames.Games.Where(x => x.DisplayName == displayName).FirstOrDefault();
        }

        /// <summary>
        /// Start a tournament of 5 random games (Crystal Capture, Hex Race, Joust).
        /// Each game's intensity is randomly chosen at or below maxIntensity.
        /// The scoreboard tracks game wins; the domain with the most wins after
        /// 5 games is the tournament winner.
        /// </summary>
        public void LaunchTournament(int maxIntensity)
        {
            if (TournamentManager.Instance == null)
            {
                Debug.LogError("[Arcade] TournamentManager not found! " +
                               "Add a TournamentManager to the persistent managers scene.");
                return;
            }

            gameData.IsDailyChallenge = false;
            gameData.IsTraining = false;
            gameData.IsMission = false;
            gameData.GameMode = GameModes.Tournament;

            TournamentManager.Instance.StartTournament(maxIntensity, gameData);
        }
    }
}