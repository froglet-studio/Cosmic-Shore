using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCellularDuelController : MultipalyerMiniGameControllerBase
    {
        private int readyClientCount;
        
        protected override void OnReadyClicked_()
        {
            ToggleReadyButton(false);
            // Debug.Log($"{NetworkManager.Singleton.LocalClientId} is ready!");
            OnReadyClicked_ServerRpc();
        }
        
        protected override void EndGame()
        {
            readyClientCount = 0;
            miniGameData.InvokeMiniGameEnd();
            EndGame_ClientRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnReadyClicked_ServerRpc()
        {
            readyClientCount++;
            if (!readyClientCount.Equals(miniGameData.SelectedPlayerCount))
                return;

            OnReadyClicked_ClientRpc();
        }

        [ClientRpc]
        private void OnReadyClicked_ClientRpc()
        {
            StartCountdownTimer();
        }

        [ClientRpc]
        private void EndGame_ClientRpc()
        {
            miniGameData.SetPlayersActiveForMultiplayer(false);
        }
    }
}