using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main vessel initializer. Spawns the host player's vessel via the
    /// base <see cref="ServerPlayerVesselInitializer"/> flow, initializes it,
    /// then activates it in autopilot mode. No AI opponents are spawned.
    ///
    /// Post-initialization responsibilities (on <see cref="GameDataSO.OnClientReady"/>):
    ///   1. Activate the player (start vessel motion, enable subsystems).
    ///   2. Enable AI pilot on the host's vessel.
    ///   3. Pause player input (autopilot drives the vessel).
    ///   4. Fire menu lifecycle events (round started, turn started).
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
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

            // Signal menu systems that rely on these events.
            gameData.InvokeMiniGameRoundStarted();
            gameData.InvokeTurnStarted();

            CSDebug.Log("[MenuServerVesselInit] Host vessel initialized and activated in autopilot mode.");
        }
    }
}
