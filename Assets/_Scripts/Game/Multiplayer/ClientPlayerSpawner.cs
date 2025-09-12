using CosmicShore.App.Systems;
using CosmicShore.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;


namespace CosmicShore.Game
{
    public class ClientPlayerSpawner : NetworkBehaviour
    {
        [SerializeField] 
        ThemeManagerDataContainerSO themeManagerData;
        
        [ClientRpc]
        internal void InitializeAndSetupPlayer_ClientRpc(ClientRpcParams clientRpcParams = default)
        {
            foreach (R_Player networkPlayer in R_Player.NppList)
            {
                var networkShip = NetworkShipClientCache.GetInstanceByClientId(networkPlayer.OwnerClientId);
                Assert.IsTrue(networkShip, $"Network ship not found for client {networkPlayer.OwnerClientId}!");

                networkPlayer.InitializeForClient(networkShip);
                networkShip.Initialize(networkPlayer, false);
                PlayerVesselInitializeHelper.SetShipProperties(themeManagerData, networkShip);
                
                networkPlayer.Ship.ShipStatus.ResourceSystem.Reset();
                networkPlayer.Ship.ShipStatus.ShipTransformer.ResetShipTransformer();

                bool toggle = !networkPlayer.IsOwner;
                networkPlayer.ToggleStationaryMode(toggle);
                networkPlayer.ToggleInputStatus(toggle);
            }

            // TODO - Should not access GameManager directly, use events
            // GameManager.UnPauseGame();
            // GameManager.Instance.WaitOnPlayerLoading();

            PauseSystem.TogglePauseGame(false);
        }
    }
}
