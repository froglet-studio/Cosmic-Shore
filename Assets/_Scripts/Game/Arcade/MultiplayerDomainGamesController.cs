using System.Collections;
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
            
            // Debug log to help track this state if issues persist
            Debug.Log($"[Server] Player Ready. Count: {readyClientCount}/{gameData.SelectedPlayerCount}");

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
            if (IsServer) 
            {
                readyClientCount = 0;
            }

            RaiseToggleReadyButtonEvent(true);
            base.SetupNewRound();
        }

        // Ensure players are physically reset (positions/state) when replaying
        protected override void OnResetForReplay()
        {
            gameData.ResetPlayers();
            base.OnResetForReplay();
        }
        
        protected override void EndGame()
        {
            if (!ShowEndGameSequence) return;
            gameData.SortRoundStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            if (IsServer)
            {
                StartCoroutine(EndGameSyncRoutine());
            }
        }

        private IEnumerator EndGameSyncRoutine()
        {
            yield return new WaitForSeconds(0.25f);
            gameData.InvokeMiniGameEnd();
        }
    }
}