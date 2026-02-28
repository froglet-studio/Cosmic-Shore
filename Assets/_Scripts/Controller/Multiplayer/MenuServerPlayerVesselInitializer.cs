using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;

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
    /// Listens to <see cref="GameDataSO.OnPlayerNetworkSpawnedUlong"/> via the base class,
    /// which waits for NetworkVariables to sync before spawning.
    ///
    /// Signals completion via <see cref="GameDataSO.OnMenuReady"/> SOAP event
    /// so any system can react to the menu being fully interactive.
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
        /// <summary>
        /// Menu override: after the base spawns + initializes the vessel, activate autopilot.
        /// </summary>
        protected override async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            await base.OnPlayerReadyToSpawnAsync(player, ct);
            ActivateAutopilot(player);
        }

        void ActivateAutopilot(Player player)
        {
            if (player?.Vessel == null)
            {
                CSDebug.LogError("[MenuServerVesselInit] LocalPlayer or Vessel not available after initialization.");
                return;
            }

            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            // Camera setup is handled by MainMenuController.HandleMenuReady()
            // which activates the CM Main Menu Cinemachine camera for menu state.
        }
    }
}
