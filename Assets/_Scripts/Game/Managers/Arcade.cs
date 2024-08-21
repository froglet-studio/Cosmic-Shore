using CosmicShore.Game.Arcade;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Singleton;
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
        Dictionary<MiniGames, SO_ArcadeGame> FactionMissionLookup = new();

        public override void Awake()
        {
            base.Awake();

            foreach (var game in ArcadeGames.GameList)
                ArcadeGameLookup.Add(game.Mode, game);

            foreach (var trainingGame in TrainingGames.GameList)
                TrainingGameLookup.Add(trainingGame.Game.Mode, trainingGame);

            foreach (var game in FactionMissionGames.GameList)
                FactionMissionLookup.Add(game.Mode, game);
        }

        public void LaunchFactionMission(MiniGames gameMode, ShipTypes ship, ResourceCollection shipResources, int intensity)
        {
            MiniGame.PlayerShipType = ship;
            MiniGame.ShipResources = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = 1;
            MiniGame.IsDailyChallenge = false;
            Hangar.Instance.SetAiDifficultyLevel(intensity);
            SceneManager.LoadScene(FactionMissionLookup[gameMode].SceneName);
        }

        public void LaunchArcadeGame(MiniGames gameMode, ShipTypes ship, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            MiniGame.PlayerShipType = ship;
            MiniGame.ShipResources = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            Hangar.Instance.SetAiDifficultyLevel(intensity);
            SceneManager.LoadScene(ArcadeGameLookup[gameMode].SceneName);
        }

        public void LaunchTrainingGame(MiniGames gameMode, ShipTypes ship, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            MiniGame.PlayerShipType = ship;
            MiniGame.ShipResources = shipResources;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = numberOfPlayers;
            MiniGame.IsDailyChallenge = isDailyChallenge;
            Hangar.Instance.SetAiDifficultyLevel(intensity);
            SceneManager.LoadScene(TrainingGameLookup[gameMode].Game.SceneName);
        }

        public SO_TrainingGame GetTrainingGameByMode(MiniGames gameMode)
        {
            return TrainingGames.GameList.Where(x => x.Game.Mode == gameMode).FirstOrDefault();
        }

        public SO_ArcadeGame GetArcadeGameSOByName(string displayName)
        {
            return ArcadeGames.GameList.Where(x => x.DisplayName == displayName).FirstOrDefault();
        }
    }
}