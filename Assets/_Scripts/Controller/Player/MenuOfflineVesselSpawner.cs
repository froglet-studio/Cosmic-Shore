using CosmicShore.Core;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Offline (non-networked) lava-lamp vessel spawner for the WebGL main-menu shell
    /// (<c>Menu_Main_WebGL</c>). Replaces the Netcode <see cref="MenuServerPlayerVesselInitializer"/>:
    /// WebGL cannot be a Netcode host, so the autopilot vessel cannot spawn through the
    /// server/Relay pipeline. Instead it spawns through the single-player <see cref="PlayerSpawner"/>
    /// path (the same one the arcade single-player adapter uses), activates autopilot exactly like the
    /// networked menu initializer, and raises <see cref="GameDataSO.OnClientReady"/> so the existing
    /// splash-release wiring reveals the menu.
    ///
    /// Lives only on the WebGL branch. Conserved-mass rules still apply to the trail it lays — no
    /// caps/TTL/decay; bound prism growth via the tuned Blob cell or by throttling the spawner.
    /// </summary>
    public class MenuOfflineVesselSpawner : PlayerSpawnerAdapterBase
    {
        [Inject] SceneTransitionManager _sceneTransitionManager;

        // Subscribe in Start (not OnEnable): [Inject] fields are populated after Awake but before
        // Start (matches MiniGamePlayerSpawnerAdapter). If MainMenuController raises OnInitializeGame
        // before this Start runs, give this component an earlier Script Execution Order than
        // MainMenuController (verify in the Editor).
        void Start()
        {
            AddSpawnPosesToGameData();
            _gameData.OnInitializeGame.OnRaised += SpawnMenuVessel;
        }

        void OnDisable()
        {
            if (_gameData != null)
                _gameData.OnInitializeGame.OnRaised -= SpawnMenuVessel;
        }

        void SpawnMenuVessel()
        {
            var data = new IPlayer.InitializeData
            {
                // Squirrel by default — set by AppManager.ConfigureGameData / MainMenuController.
                vesselClass   = _gameData.selectedVesselClass.Value,
                PlayerName    = "Pilot",
                AvatarId      = 0,
                IsAI          = false,
                AllowSpawning = true,
            };

            var player = _playerSpawner.SpawnPlayerAndShip(data);
            if (player == null)
            {
                CSDebug.LogError("[MenuOfflineVesselSpawner] Failed to spawn the offline menu vessel.");
                return;
            }

            _gameData.AddPlayer(player);

            // Autopilot — mirrors MenuServerPlayerVesselInitializer.ActivateAutopilot.
            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            // Reveal the menu. InvokeClientReady reuses the existing OnClientReady splash-release
            // (SceneLoader.FadeFromSplashOnReady) and drives MainMenuController.HandleMenuReady.
            // The explicit FadeFromBlack is a safety net: SceneLoader only auto-arms the fade for a
            // scene literally named "Menu_Main", and this scene is "Menu_Main_WebGL".
            _gameData.InvokeClientReady();
            _sceneTransitionManager?.FadeFromBlack().Forget();
        }
    }
}
