// Ported from Assets/_Scripts/Controller/Managers/Arcade.cs (Arc F 2b-iii) — verbatim;
// UnityEngine → CosmicShore.Engine, Obvious.Soap → CosmicShore.Engine.Soap,
// UnityEngine.SceneManagement → CosmicShore.Engine.SceneManagement. Deviation (marked
// inline): the Animator SceneTransitionAnimator field — the engine has no Animator; it
// is only consumed by commented-out legacy code upstream. All three launch paths write
// the same GameDataSO state and fire InvokeGameLaunch, the real SOAP launch seam.
using CosmicShore.Gameplay;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

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
        [field: SerializeField] SO_VesselList VesselList { get; set; }

        [FormerlySerializedAs("miniGameData")] [SerializeField] private GameDataSO gameData;

        // [SerializeField] ScriptableEventArcadeData OnArcadeMultiplayerModeSelected;

        /*[SerializeField]
        ScriptableEventNoParam _onStartSceneTransition;*/

        Dictionary<GameModes, SO_ArcadeGame> ArcadeGameLookup = new();
        Dictionary<GameModes, SO_TrainingGame> TrainingGameLookup = new();
        Dictionary<GameModes, SO_Mission> MissionLookup = new();
        // PORT Deviation (Arc F 2b-iii, Animator — only consumed by commented legacy code):
        // Animator SceneTransitionAnimator;

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

        public void LaunchMission(GameModes gameMode, SO_Vessel vessel, int intensity)
        {
            if (vessel != null && vessel.IsLocked)
            {
                CSDebug.LogWarning($"Arcade: Blocked launch with locked vessel {vessel.Name}");
                return;
            }

            gameData.ResourceCollection = vessel != null ? vessel.InitialResourceLevels : new ResourceCollection(.5f, .5f, .5f, .5f);
            gameData.IsDailyChallenge = false;
            gameData.IsTraining = false;
            gameData.IsMission = true;
            gameData.IsMultiplayerMode = false;
            gameData.GameMode = gameMode;
            gameData.SelectedPlayerCount.Value = 1;
            gameData.SelectedIntensity.Value = intensity;
            gameData.SceneName = MissionLookup[gameMode].SceneName;
            gameData.InvokeGameLaunch();
        }

        public void LaunchArcadeGame(GameModes gameMode, VesselClassType vessel, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isMultiplayer, bool isDailyChallenge = false)
        {
            if (VesselList && VesselList.TryGetVesselByClass(vessel, out var vesselSO) && vesselSO.IsLocked)
            {
                CSDebug.LogWarning($"Arcade: Blocked launch with locked vessel {vessel}");
                return;
            }

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
        }

        public void LaunchTrainingGame(GameModes gameMode, VesselClassType vessel, ResourceCollection shipResources, int intensity, int numberOfPlayers, bool isDailyChallenge = false)
        {
            if (VesselList && VesselList.TryGetVesselByClass(vessel, out var vesselSO) && vesselSO.IsLocked)
            {
                CSDebug.LogWarning($"Arcade: Blocked launch with locked vessel {vessel}");
                return;
            }

            gameData.ResourceCollection = shipResources;
            gameData.IsDailyChallenge = isDailyChallenge;
            gameData.IsTraining = !isDailyChallenge;
            gameData.IsMission = false;
            gameData.IsMultiplayerMode = false;
            gameData.SceneName = TrainingGameLookup[gameMode].Game.SceneName;
            gameData.InvokeGameLaunch();
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
