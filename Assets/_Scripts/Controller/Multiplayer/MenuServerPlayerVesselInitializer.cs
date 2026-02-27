using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main vessel initializer. Forces Squirrel, spawns the host vessel,
    /// initializes it, then activates autopilot.
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
        void Start()
        {
            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
            gameData.InitializeGame();
        }

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

            gameData.InvokeMiniGameRoundStarted();
            gameData.InvokeTurnStarted();
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
