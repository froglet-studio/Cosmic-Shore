using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main vessel initializer. Spawns the host vessel on the network,
    /// initializes it, then activates autopilot.
    ///
    /// Game data configuration (vessel class, player count, intensity) is handled
    /// by <see cref="Core.MainMenuController"/> — this class only handles the
    /// network spawn chain and autopilot activation.
    ///
    /// Defers spawn setup until <see cref="GameDataSO.OnInitializeGame"/> fires,
    /// because <c>OnNetworkSpawn</c> runs before <c>Start()</c> in Unity's
    /// execution order, and <see cref="Core.MainMenuController.Start"/> is what
    /// configures game data and raises <c>OnInitializeGame</c>.
    ///
    /// Signals completion via <see cref="GameDataSO.OnMenuReady"/> SOAP event
    /// so any system can react to the menu being fully interactive.
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
        /// <summary>
        /// Menu override: after the base spawns + initializes the vessel, activate autopilot.
        /// </summary>
        protected override void OnPlayerReadyToSpawn(Player player)
        {
            base.OnPlayerReadyToSpawn(player);
            ActivateAutopilot(player);
        }

        void ActivateAutopilot(Player player)
        {
            if (player?.Vessel == null)
            {
                CSDebug.LogError("[MenuServerVesselInit] LocalPlayer or Vessel not available after initialization.");
                return;
            }

            gameData.SetPlayersActive();

            player.Vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            if (CameraManager.Instance)
            {
                var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.SetupEndCameraFollow(followTarget);
            }

            gameData.InvokeMenuReady();
        }
    }
}
