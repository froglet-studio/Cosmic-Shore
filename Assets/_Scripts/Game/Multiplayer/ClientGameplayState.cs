﻿using CosmicShore.Core;
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
            foreach (NetworkPlayer networkPlayer in NetworkPlayer.NppList)
            {
                NetworkShip networkShip = NetworkShipClientCache.GetInstanceByClientId(networkPlayer.OwnerClientId);
                Assert.IsTrue(networkShip, $"Network ship not found for client {networkPlayer.OwnerClientId}!");

                networkPlayer.Setup(networkShip);
            }

            GameManager.UnPauseGame();
            GameManager.Instance.WaitOnPlayerLoading();
        }
    }
}
