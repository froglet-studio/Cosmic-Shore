using CosmicShore.Game;
using CosmicShore.Game.Arcade;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models.Enums;
using CosmicShore.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.SOAP;
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

        [SerializeField] private MiniGameDataSO miniGameData;
        
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
            miniGameData.PlayerCaptain = captain;
            miniGameData.ResourceCollection = CaptainManager.Instance.GetCaptainByName(captain.Name).ResourceLevels;
            miniGameData.IsDailyChallenge = false;
            miniGameData.IsTraining = false;
            miniGameData.IsMission = true;
            miniGameData.IsMultiplayerMode = false;
            miniGameData.SceneName = MissionLookup[gameMode].SceneName;
            miniGameData.InvokeGameLaunch();
            
            /*MiniGame.PlayerCaptain = captain;
            MiniGame.PlayerShipType = captain.Vessel.Class;
            MiniGame.ResourceCollection = CaptainManager.Instance.GetCaptainByName(captain.Name).ResourceLevels;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = 1;
            MiniGame.IsDailyChallenge = false;
            MiniGame.IsTraining = false;
            MiniGame.IsMission = true;

            // TODO - Not in hanger, but do this in MiniGameData, or PlayerSpawner
            Hangar.Instance.SetAiIntensityLevel(intensity);


            var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.PORT);
            screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.FACTION_MISSION);
            

            StartCoroutine(LaunchGameCoroutine(MissionLookup[gameMode].SceneName));
            */
        }

        public void LaunchArcadeGame(GameModes gameMode, VesselClassType vessel, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            miniGameData.ResourceCollection = shipResources;
            miniGameData.IsDailyChallenge = isDailyChallenge;
            miniGameData.IsTraining = false;
            miniGameData.IsMission = false;
            miniGameData.IsMultiplayerMode = SO_Game.IsMultiplayerModes(gameMode);
            miniGameData.SceneName = ArcadeGameLookup[gameMode].SceneName;
            miniGameData.InvokeGameLaunch();

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
            miniGameData.ResourceCollection = shipResources;
            miniGameData.IsDailyChallenge = isDailyChallenge;
            miniGameData.IsTraining = !isDailyChallenge;
            miniGameData.IsMission = false;
            miniGameData.IsMultiplayerMode = false;
            miniGameData.SceneName = TrainingGameLookup[gameMode].Game.SceneName;
            miniGameData.InvokeGameLaunch();
            
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

        /*IEnumerator LaunchGameCoroutine(string sceneName)
        {
            _onStartSceneTransition.Raise();

            yield return new WaitForSecondsRealtime(.5f);

            SceneManager.LoadScene(sceneName);
        }*/
    }
}