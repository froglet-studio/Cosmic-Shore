using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main adapter. Configures game data (vessel class, spawn positions)
    /// before <see cref="MenuServerPlayerVesselInitializer"/> spawns the host
    /// vessel. Does NOT spawn players or vessels directly.
    ///
    /// Responsibilities:
    ///   1. Set the vessel class to Squirrel before the host player spawns.
    ///   2. Initialize game data and spawn positions.
    ///
    /// Post-initialization (autopilot activation, lifecycle events) is handled
    /// by <see cref="MenuServerPlayerVesselInitializer"/>.
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
        }
    }
}
