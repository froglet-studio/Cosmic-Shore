using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerDomainGamesController : MultiplayerMiniGameControllerBase
    {
        private int readyClientCount;
        
        public void OnClickReturnToMainMenu()
        {
            CloseSession_ServerRpc();
        }
        
        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer)
                return;

            OnCountdownTimerEnded_ClientRpc();
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn(); 
        }

        [ServerRpc(RequireOwnership = false)]
        void CloseSession_ServerRpc()
        {
            multiplayerSetup.LeaveSession().Forget();
        }
        
        protected override void OnReadyClicked_()
        {
            RaiseToggleReadyButtonEvent(false);
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
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewRound();
        }
    }
}