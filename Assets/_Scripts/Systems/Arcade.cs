using System;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models.Enums;
using CosmicShore.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Soap;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Systems
{
    /// <summary>
    /// --------------- DEPRECATED --------------- 
    /// 
    /// Singleton class responsible for interacting with games
    /// </summary>
    public class Arcade : SingletonPersistent<Arcade>
    {
        [field: FormerlySerializedAs("FactionMissionGames")]
        [field: SerializeField] public SO_MissionList MissionGames { get; private set; }
        [field: SerializeField] public SO_GameList ArcadeGames { get; private set; }
        [field: SerializeField] public SO_TrainingGameList TrainingGames { get; private set; }

        [Inject] GameDataSO gameData;

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

        void LaunchArcadeGame()
        {
            /*gameData.ResourceCollection = shipResources;
            gameData.IsDailyChallenge = isDailyChallenge;
            gameData.IsTraining = false;
            gameData.IsMission = false;
            gameData.GameMode = gameMode;
            
            gameData.IsMultiplayerMode = isMultiplayer;
            gameData.SceneName = ArcadeGameLookup[gameMode].SceneName;*/

            /*MiniGame.PlayerShipType = vessel;
            MiniGame.ResourceCollection = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            MiniGame.IsTraining = false;
            MiniGame.IsMission = false;

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
    }
}