using System;
using CosmicShore.Data;
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
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
        /// <summary>
        /// Raised after the autopilot vessel is fully spawned, identity is set,
        /// AI pilot is active, and the camera follow target is configured.
        /// <see cref="Core.MainMenuController"/> subscribes to this to
        /// transition the menu state to <see cref="MainMenuState.Ready"/>.
        /// </summary>
        public event Action OnMenuVesselReady;

        /// <summary>
        /// Menu override: after the base spawns + initializes the vessel, activate autopilot.
        /// </summary>
        protected override void OnPlayerReadyToSpawn(Player player)
        {
            base.OnPlayerReadyToSpawn(player);
            ActivateAutopilot();
        }

        void ActivateAutopilot()
        {
            var player = gameData.LocalPlayer;
            if (player?.Vessel == null)
            {
                CSDebug.LogError("[MenuServerVesselInit] LocalPlayer or Vessel not available after initialization.");
                return;
            }

            InitializeMenuPlayerIdentity(player);
            gameData.SetPlayersActive();

            player.Vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            if (CameraManager.Instance)
            {
                var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                CameraManager.Instance.SetupEndCameraFollow(followTarget);
            }

            OnMenuVesselReady?.Invoke();
        }

        void InitializeMenuPlayerIdentity(IPlayer player)
        {
            if (player is not Player netPlayer)
                return;

            netPlayer.NetDomain.Value = Domains.Jade;

            if (string.IsNullOrEmpty(player.Name))
            {
                string displayName = !string.IsNullOrEmpty(gameData.LocalPlayerDisplayName)
                    ? gameData.LocalPlayerDisplayName
                    : "Pilot";
                netPlayer.NetName.Value = displayName;
            }

            player.RoundStats.Domain = player.Domain;
            player.RoundStats.Name = player.Name;
        }
    }
}
