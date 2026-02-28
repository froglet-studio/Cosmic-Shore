using Unity.Netcode;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main vessel initializer. Handles both server and client paths so
    /// that all party members get a vessel in the shared menu scene.
    ///
    /// Server path (host):
    ///   OnNetworkSpawn → subscribes to OnInitializeGame (via base).
    ///   When each player is ready → spawns vessel → activates autopilot.
    ///
    /// Client path (party member):
    ///   OnNetworkSpawn → subscribes to OnClientReady.
    ///   When all player-vessel pairs are initialized → starts autopilot,
    ///   sets camera to follow the local player's vessel.
    ///
    /// Each player starts in autopilot mode. Players can independently toggle
    /// between autopilot (menu browsing) and gameplay via their own input.
    ///
    /// Signals <see cref="GameDataSO.OnMenuReady"/> once after the first
    /// player is fully initialized (host vessel) so menu UI can activate.
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
        bool _menuReadySignaled;

        /// <summary>
        /// Overrides the base to support both server and client paths.
        /// The base disables the component on clients — we instead subscribe
        /// to OnClientReady to handle client-side autopilot activation.
        /// </summary>
        protected override void OnNetworkSpawn()
        {
            _menuReadySignaled = false;

            if (NetworkManager.Singleton.IsServer)
            {
                // Server: subscribe to OnInitializeGame → spawn vessels
                base.OnNetworkSpawn();
            }
            else
            {
                // Client: wait for all player-vessel pairs to be initialized
                // via RPCs, then activate autopilot for every player.
                gameData.OnClientReady.OnRaised += HandleClientReady;
            }
        }

        protected override void OnNetworkDespawn()
        {
            gameData.OnClientReady.OnRaised -= HandleClientReady;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Server: after the base spawns + initializes a vessel, start that
        /// player in autopilot mode. Only configures the camera for the local
        /// (host) player's vessel.
        /// </summary>
        protected override void OnPlayerReadyToSpawn(Player player)
        {
            base.OnPlayerReadyToSpawn(player);
            ActivatePlayerAutopilot(player);
        }

        void ActivatePlayerAutopilot(Player player)
        {
            if (player?.Vessel == null)
            {
                CSDebug.LogError("[MenuServerVesselInit] Player or Vessel not available after initialization.");
                return;
            }

            // Start only this player (not all players) to avoid restarting
            // previously initialized players and disrupting their state.
            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            // Camera follows the local (host) player's vessel only
            if (player.IsLocalUser && CameraManager.Instance)
            {
                var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.SetupEndCameraFollow(followTarget);
            }

            if (!_menuReadySignaled)
            {
                _menuReadySignaled = true;
                gameData.InvokeMenuReady();
            }
        }

        /// <summary>
        /// Client: all player-vessel pairs have been initialized via RPCs.
        /// Start every player in autopilot mode and set the camera to follow
        /// the local player's vessel.
        /// </summary>
        void HandleClientReady()
        {
            gameData.OnClientReady.OnRaised -= HandleClientReady;

            foreach (var player in gameData.Players)
            {
                player.StartPlayer();
                player.Vessel?.ToggleAIPilot(true);
                player.InputController?.SetPause(true);
            }

            if (gameData.LocalPlayer?.Vessel != null && CameraManager.Instance)
            {
                var followTarget = gameData.LocalPlayer.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.SetupEndCameraFollow(followTarget);
            }
        }
    }
}
