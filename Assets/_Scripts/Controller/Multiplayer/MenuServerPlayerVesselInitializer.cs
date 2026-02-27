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
    /// Spawn (HandleNewPlayer via OnPlayerNetworkSpawned SOAP event):
    ///   3. Spawn only the host's vessel — no AI opponents.
    ///
    /// Post-initialization (OnClientReady):
    ///   4. Initialize player identity (domain + playerName).
    ///   5. Activate the player (start vessel motion, enable subsystems).
    ///   6. Enable AI pilot on the host's vessel.
    ///   7. Pause player input (autopilot drives the vessel).
    ///   8. Switch end camera to follow the vessel.
    ///   9. Fire menu lifecycle events (round started, turn started).
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
        protected override void HandleNewPlayer(ulong clientId)
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

            // Ensure domain and player name are set before activating.
            // Mirrors the HexRace multiplayer pattern where domain and
            // playerName are explicitly assigned after player/vessel init.
            InitializeMenuPlayerIdentity(player);

            // Activate the player (starts vessel motion, enables subsystems).
            gameData.SetPlayersActive();

            // Enable AI pilot — vessel is the host's but AI-controlled in the menu.
            player.Vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            // Switch the end camera to follow the autopilot vessel.
            if (CameraManager.Instance)
            {
                var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.SetupEndCameraFollow(followTarget);
            }

            // Signal menu systems that rely on these events.
            gameData.InvokeMiniGameRoundStarted();
            gameData.InvokeTurnStarted();

            CSDebug.Log("[MenuServerVesselInit] Host vessel initialized and activated in autopilot mode.");
        }

        /// <summary>
        /// Sets domain to Jade and resolves the player display name for the
        /// menu background vessel.  Follows the HexRace multiplayer pattern
        /// where domain and playerName are explicitly written to the player's
        /// NetworkVariables after spawning.
        /// </summary>
        void InitializeMenuPlayerIdentity(IPlayer player)
        {
            if (player is not Player netPlayer)
                return;

            // Domain — always Jade for the menu background vessel.
            netPlayer.NetDomain.Value = Domains.Jade;

            // Display name — resolve from cached profile data if not already set.
            if (string.IsNullOrEmpty(player.Name))
            {
                string displayName = !string.IsNullOrEmpty(gameData.LocalPlayerDisplayName)
                    ? gameData.LocalPlayerDisplayName
                    : "Pilot";
                netPlayer.NetName.Value = displayName;
            }

            // Sync RoundStats with the resolved identity values.
            player.RoundStats.Domain = player.Domain;
            player.RoundStats.Name = player.Name;
        }
    }
}
