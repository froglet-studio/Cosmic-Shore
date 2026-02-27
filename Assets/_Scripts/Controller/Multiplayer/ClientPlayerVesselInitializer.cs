using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [Inject] protected GameDataSO gameData;

        /// <summary>
        /// Initializes a player and its vessel directly.
        /// Called by ServerPlayerVesselInitializer after spawning the vessel.
        /// </summary>
        public void InitializePlayerAndVessel(Player player, NetworkObject vesselNO)
        {
            if (!vesselNO.TryGetComponent(out IVessel vessel))
            {
                CSDebug.LogError("[ClientPlayerVesselInitializer] Spawned vessel missing IVessel component.");
                return;
            }

            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, vessel);
            gameData.AddPlayer(player);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
    }
}
