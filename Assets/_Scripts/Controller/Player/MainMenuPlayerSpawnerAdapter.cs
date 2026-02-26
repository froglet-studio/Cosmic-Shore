using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main adapter. Does NOT spawn players or vessels — that is handled by
    /// MainMenuServerVesselInitializer + ClientPlayerVesselInitializer (same
    /// pattern as Multiplayer_Freestyle).
    ///
    /// Responsibilities:
    ///   1. Initialize game data and spawn positions.
    ///   2. Set the vessel class to Squirrel before the host player spawns.
    ///   3. On OnClientReady: enable AI pilot on the player's vessel, fire
    ///      menu lifecycle events.
    /// </summary>
    public class MainMenuPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        void Start()
        {
            // Force Squirrel for the menu vessel. Player.OnNetworkSpawn reads
            // selectedVesselClass to set NetDefaultVesselType.
            _gameData.selectedVesselClass.Value = VesselClassType.Squirrel;

            _gameData.InitializeGame();
            AddSpawnPosesToGameData();

            _gameData.OnClientReady.OnRaised += OnClientReady;
        }

        void OnDisable()
        {
            _gameData.OnClientReady.OnRaised -= OnClientReady;
        }

        void OnClientReady()
        {
            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null)
            {
                CSDebug.LogWarning("[MainMenuSpawner] LocalPlayer or Vessel not available on ClientReady.");
                return;
            }

            // Activate the player (starts vessel motion, enables subsystems).
            _gameData.SetPlayersActive();

            // Enable AI pilot — vessel is the player's but AI-controlled in the menu.
            player.Vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            // Signal menu systems that rely on these events.
            _gameData.InvokeMiniGameRoundStarted();
            _gameData.InvokeTurnStarted();

            CSDebug.Log("[MainMenuSpawner] Host player ready — Squirrel vessel with AI pilot.");
        }
    }
}
