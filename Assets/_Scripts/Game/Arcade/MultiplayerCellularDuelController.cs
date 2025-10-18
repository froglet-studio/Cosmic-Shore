using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCellularDuelController : MultiplayerMiniGameControllerBase
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

        protected override void SetupNewRound()
        {
            SetupNewRound_ClientRpc();
            base.SetupNewRound();
        }

        [ClientRpc]
        void SetupNewRound_ClientRpc()
        {
            ToggleReadyButton(true);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnReadyClicked_ServerRpc()
        {
            readyClientCount++;
            if (!readyClientCount.Equals(gameData.SelectedPlayerCount))
                return;

            readyClientCount = 0;
            OnReadyClicked_ClientRpc();
        }

        [ClientRpc]
        void OnReadyClicked_ClientRpc()
        {
            StartCountdownTimer();
        }
    }
}