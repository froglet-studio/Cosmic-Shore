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

        protected override void OnNetworkSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                enabled = false;
                return;
            }

            gameData.SetSpawnPositions(_playerOrigins);
            DomainAssigner.Initialize();

            var hostClientId = NetworkManager.Singleton.LocalClientId;
            var player = FindPlayerByClientId(hostClientId);
            if (player == null)
            {
                CSDebug.LogError($"[MenuServerVesselInit] Host player not found for client {hostClientId}. " +
                                 $"Players registered: {gameData.Players.Count}");
                return;
            }

            SpawnVesselAndInitialize(hostClientId, player);
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
