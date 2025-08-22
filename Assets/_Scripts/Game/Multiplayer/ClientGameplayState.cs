using CosmicShore.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;


namespace CosmicShore.Game
{
    public class ClientGameplayState : NetworkBehaviour
    {
        [ClientRpc]
        internal void InitializeAndSetupPlayer_ClientRpc(ClientRpcParams clientRpcParams = default)
        {
            foreach (R_Player networkPlayer in R_Player.NppList)
            {
                R_ShipController networkShip = NetworkShipClientCache.GetInstanceByClientId(networkPlayer.OwnerClientId);
                Assert.IsTrue(networkShip, $"Network ship not found for client {networkPlayer.OwnerClientId}!");

                networkPlayer.InitializeShip(networkShip);
            }

            // TODO - Should not access GameManager directly, use events
            // GameManager.UnPauseGame();
            // GameManager.Instance.WaitOnPlayerLoading();
        }
    }
}
