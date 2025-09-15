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
            foreach (Player networkPlayer in Player.NppList)
            {
                var networkShip = NetworkShipClientCache.GetInstanceByClientId(networkPlayer.OwnerClientId);
                Assert.IsTrue(networkShip, $"Network vessel not found for client {networkPlayer.OwnerClientId}!");

                networkPlayer.InitializeForMultiplayerMode(networkShip);
                networkShip.Initialize(networkPlayer, false);
                PlayerVesselInitializeHelper.SetShipProperties(themeManagerData, networkShip);

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
