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

        protected override void SetupNewRound()
        {
            bool allowSwap = roundsPlayed > 0;
            if (allowSwap)
                ChangeOwnershipOfVessels();
            SetupNewRound_ClientRpc(allowSwap);
            
            base.SetupNewRound();
        }
        
        [ClientRpc]
        void SetupNewRound_ClientRpc(bool allowSwap)
        {
            if (allowSwap)
                gameData.SwapVessels();
            ToggleReadyButton(true);
        }
        
        protected override void OnResetForReplay()
        {
            ChangeOwnershipOfVessels();
            OnResetForReplay_ClientRpc();
            base.OnResetForReplay();
        }

        [ClientRpc]
        void OnResetForReplay_ClientRpc()
        {
            gameData.SwapVessels();
        }

        void ChangeOwnershipOfVessels()
        {
            var player0 = Player.NppList[0];
            var player1 = Player.NppList[1];
            
            // swap the vessel types from player.NetDefaultShipType.Value
            if (!player0.Vessel.Transform.TryGetComponent(out NetworkObject no0))
            {
                Debug.LogError("No network object found in vessel. This should not happen!");
                return;
            }
            
            if (!player1.Vessel.Transform.TryGetComponent(out NetworkObject no1))
            {
                Debug.LogError("No network object found in vessel. This should not happen!");
                return;
            }
            
            var no0_OwnerClientId = no0.OwnerClientId;
            no0.ChangeOwnership(no1.OwnerClientId);
            no1.ChangeOwnership(no0_OwnerClientId);
        }
    }
}