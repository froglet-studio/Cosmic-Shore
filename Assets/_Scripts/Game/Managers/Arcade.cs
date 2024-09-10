using CosmicShore.App.UI;
using CosmicShore.Game.Arcade;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Singleton;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Singleton class responsible for interacting with games
    /// </summary>
    public class Arcade : SingletonPersistent<Arcade>
    {
        [field: SerializeField] public SO_GameList FactionMissionGames { get; private set; }
        [field: SerializeField] public SO_GameList ArcadeGames { get; private set; }
        [field: SerializeField] public SO_TrainingGameList TrainingGames { get; private set; }
        Dictionary<MiniGames, SO_ArcadeGame> ArcadeGameLookup = new();
        Dictionary<MiniGames, SO_TrainingGame> TrainingGameLookup = new();
        Dictionary<MiniGames, SO_ArcadeGame> MissionLookup = new();
        Animator SceneTransitionAnimator;

        public override void Awake()
        {
            base.Awake();

            foreach (var game in ArcadeGames.GameList)
                ArcadeGameLookup.Add(game.Mode, game);

            foreach (var trainingGame in TrainingGames.GameList)
                TrainingGameLookup.Add(trainingGame.Game.Mode, trainingGame);

            foreach (var game in FactionMissionGames.GameList)
                MissionLookup.Add(game.Mode, game);
        }

        public void LaunchMission(MiniGames gameMode, SO_Captain captain, int intensity)
        {
            MiniGame.PlayerCaptain = captain;
            MiniGame.PlayerShipType = captain.Ship.Class;
            MiniGame.ShipResources = CaptainManager.Instance.GetCaptainByName(captain.Name).ResourceLevels;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = 1;
            MiniGame.IsDailyChallenge = false;
            MiniGame.IsTraining = false;
            MiniGame.IsMission = true;
            Hangar.Instance.SetAiDifficultyLevel(intensity);

            var screenSwitcher = GameObject.FindAnyObjectByType<ScreenSwitcher>();
            screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.PORT);
            screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.FACTION_MISSION);

            StartCoroutine(LaunchGameCoroutine(MissionLookup[gameMode].SceneName));
        }

        public void LaunchArcadeGame(MiniGames gameMode, ShipTypes ship, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            MiniGame.PlayerShipType = ship;
            MiniGame.ShipResources = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            MiniGame.IsTraining = false;
            MiniGame.IsMission = false;
            Hangar.Instance.SetAiDifficultyLevel(intensity);

            var screenSwitcher = GameObject.FindAnyObjectByType<ScreenSwitcher>();
            screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.ARCADE);
            if (isDailyChallenge)
                screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.DAILY_CHALLENGE);

            StartCoroutine(LaunchGameCoroutine(ArcadeGameLookup[gameMode].SceneName));
        }

        public void LaunchTrainingGame(MiniGames gameMode, ShipTypes ship, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            MiniGame.PlayerShipType = ship;
            MiniGame.ShipResources = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            MiniGame.IsTraining = !isDailyChallenge;
            MiniGame.IsMission = false;
            Hangar.Instance.SetAiDifficultyLevel(intensity);

            var screenSwitcher = GameObject.FindAnyObjectByType<ScreenSwitcher>();
            if (isDailyChallenge)
            {
                screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.ARCADE);
                screenSwitcher.SetReturnToModal(ScreenSwitcher.ModalWindows.DAILY_CHALLENGE);
            }
            else
                screenSwitcher.SetReturnToScreen(ScreenSwitcher.MenuScreens.HANGAR);

            StartCoroutine(LaunchGameCoroutine(TrainingGameLookup[gameMode].Game.SceneName));
        }

        public SO_TrainingGame GetTrainingGameByMode(MiniGames gameMode)
        {
            return TrainingGames.GameList.Where(x => x.Game.Mode == gameMode).FirstOrDefault();
        }

        public SO_ArcadeGame GetArcadeGameSOByName(string displayName)
        {
            return ArcadeGames.GameList.Where(x => x.DisplayName == displayName).FirstOrDefault();
        }

        public void RegisterSceneTransitionAnimator(Animator animator)
        {
            SceneTransitionAnimator = animator;
        }


        IEnumerator LaunchGameCoroutine(string sceneName)
        {
            SceneTransitionAnimator?.SetTrigger("Start");

            yield return new WaitForSecondsRealtime(.5f);

            SceneManager.LoadScene(sceneName);
        }
    }
}