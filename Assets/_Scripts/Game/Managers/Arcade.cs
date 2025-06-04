using CosmicShore.Game.Arcade;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models.Enums;
using CosmicShore.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] MultiplayerSetup _multiplayerSetup;


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
            MiniGame.PlayerCaptain = captain;
            MiniGame.PlayerShipType = captain.Ship.Class;
            MiniGame.ResourceCollection = CaptainManager.Instance.GetCaptainByName(captain.Name).ResourceLevels;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = 1;
            MiniGame.IsDailyChallenge = false;
            MiniGame.IsTraining = false;
            MiniGame.IsMission = true;
            Hangar.Instance.SetAiIntensityLevel(intensity);

            /*
            var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.PORT);
            screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.FACTION_MISSION);
            */

            StartCoroutine(LaunchGameCoroutine(MissionLookup[gameMode].SceneName));
        }

        public void LaunchArcadeGame(GameModes gameMode, ShipTypes ship, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            MiniGame.PlayerShipType = ship;
            MiniGame.ResourceCollection = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            MiniGame.IsTraining = false;
            MiniGame.IsMission = false;
            Hangar.Instance.SetAiIntensityLevel(intensity);

            /*
            var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.ARCADE);
            if (isDailyChallenge)
                screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.DAILY_CHALLENGE);
            */



            // TODO: Refactor later to support multiple multiplayer game modes. We can add a bool isMultiplayer paramter to SO_Game later if needed.
            if (SO_Game.IsMultiplayerModes(gameMode))
            {
                _multiplayerSetup.ExecuteMultiplayerSetup(ArcadeGameLookup[gameMode].SceneName, GetArcadeGameByMode(gameMode).MaxPlayersForMultiplayer);
                return;
            }

            StartCoroutine(LaunchGameCoroutine(ArcadeGameLookup[gameMode].SceneName));
        }

        SO_Game GetArcadeGameByMode(GameModes gameMode)
        {
            return ArcadeGames.Games.Where(x => x.Mode == gameMode).FirstOrDefault();
        }

        public void LaunchTrainingGame(GameModes gameMode, ShipTypes ship, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            MiniGame.PlayerShipType = ship;
            MiniGame.ResourceCollection = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            MiniGame.IsTraining = !isDailyChallenge;
            MiniGame.IsMission = false;
            Hangar.Instance.SetAiIntensityLevel(intensity);

            /*
            var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            if (isDailyChallenge)
            {
                screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.ARCADE);
                screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.DAILY_CHALLENGE);
            }
            else
                screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.HANGAR);
            */

            StartCoroutine(LaunchGameCoroutine(TrainingGameLookup[gameMode].Game.SceneName));
        }

        public SO_TrainingGame GetTrainingGameByMode(GameModes gameMode)
        {
            return TrainingGames.Games.Where(x => x.Game.Mode == gameMode).FirstOrDefault();
        }

        public SO_ArcadeGame GetArcadeGameSOByName(string displayName)
        {
            return ArcadeGames.Games.Where(x => x.DisplayName == displayName).FirstOrDefault();
        }

        public void RegisterSceneTransitionAnimator(Animator animator)
        {
            SceneTransitionAnimator = animator;
        }

        IEnumerator LaunchGameCoroutine(string sceneName)
        {
            SceneTransitionAnimator.SetTrigger("Start");

            yield return new WaitForSecondsRealtime(.5f);

            SceneManager.LoadScene(sceneName);
        }
    }
}