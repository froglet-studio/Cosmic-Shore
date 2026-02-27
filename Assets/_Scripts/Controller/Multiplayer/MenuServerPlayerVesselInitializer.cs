using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main vessel initializer. Owns the full menu vessel lifecycle:
    ///
    /// Pre-spawn (Start):
    ///   1. Force vessel class to Squirrel.
    ///   2. Initialize game data.
    ///
    /// Spawn (OnClientConnected):
    ///   3. Spawn only the host's vessel — no AI opponents.
    ///
    /// Post-initialization (OnClientReady):
    ///   4. Activate the player (start vessel motion, enable subsystems).
    ///   5. Enable AI pilot on the host's vessel.
    ///   6. Pause player input (autopilot drives the vessel).
    ///   7. Switch Cinemachine menu camera to follow the vessel.
    ///   8. Fire menu lifecycle events (round started, turn started).
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
        void Start()
        {
            // Force Squirrel for the menu vessel. Player.OnNetworkSpawn reads
            // selectedVesselClass to set NetDefaultVesselType.
            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
            gameData.InitializeGame();
        }

        protected override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!NetworkManager.Singleton.IsServer)
                return;

            gameData.OnClientReady.OnRaised += OnClientReady;
        }

        protected override void OnNetworkDespawn()
        {
            gameData.OnClientReady.OnRaised -= OnClientReady;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Menu override: spawn only the host's vessel — no AI opponents.
        /// </summary>
        protected override void OnClientConnected(ulong clientId)
        {
            DelayedSpawnVesselForPlayer(clientId).Forget();
        }

        /// <summary>
        /// After the vessel is initialized on the client, activate the player
        /// and enable autopilot so the vessel flies autonomously in the menu.
        /// </summary>
        void OnClientReady()
        {
            var player = gameData.LocalPlayer;
            if (player?.Vessel == null)
            {
                CSDebug.LogWarning("[MenuServerVesselInit] LocalPlayer or Vessel not available on ClientReady.");
                return;
            }

            // Activate the player (starts vessel motion, enables subsystems).
            gameData.SetPlayersActive();

            // Enable AI pilot — vessel is the host's but AI-controlled in the menu.
            player.Vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            // Switch the Cinemachine menu camera to follow the autopilot vessel.
            if (CameraManager.Instance)
            {
                var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.FollowVesselInMainMenu(followTarget);
            }

            // Signal menu systems that rely on these events.
            gameData.InvokeMiniGameRoundStarted();
            gameData.InvokeTurnStarted();

            CSDebug.Log("[MenuServerVesselInit] Host vessel initialized and activated in autopilot mode.");
        }
    }
}
