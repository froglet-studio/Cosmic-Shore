using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCellularDuelController : MultipalyerMiniGameControllerBase
    {
        private int readyClientCount;
        
        public void OnClickReturnToMainMenu()
        {
            CloseSession_ServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        void CloseSession_ServerRpc()
        {
            MultiplayerSetup.Instance.LeaveSession().Forget();
        }
        
        protected override void OnReadyClicked_()
        {
            ToggleReadyButton(false);
            // Debug.Log($"{NetworkManager.Singleton.LocalClientId} is ready!");
            OnReadyClicked_ServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnReadyClicked_ServerRpc()
        {
            readyClientCount++;
            if (!readyClientCount.Equals(miniGameData.SelectedPlayerCount))
                return;

            readyClientCount = 0;
            OnReadyClicked_ClientRpc();
        }

        [ClientRpc]
        private void OnReadyClicked_ClientRpc()
        {
            StartCountdownTimer();
        }
    }
}