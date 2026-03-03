using System.Linq;
using CosmicShore.UI;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
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

            Debug.Log($"<color=#00CED1>[FLOW-9] [DomainGamesCtrl] OnCountdownTimerEnded (server) — activating players. Players={gameData.Players.Count}, RoundStats={gameData.RoundStatsList.Count}</color>");
            OnCountdownTimerEnded_ClientRpc();
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc()
        {
            Debug.Log("<color=#00CED1>[FLOW-9] [DomainGamesCtrl] OnCountdownTimerEnded_ClientRpc — SetPlayersActive + StartTurn</color>");
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
            OnReadyClicked_ServerRpc(gameData.LocalPlayer.Name);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnReadyClicked_ServerRpc(string playerName)
        {
            readyClientCount++;

            // Debug log to help track this state if issues persist
            Debug.Log($"<color=#00CED1>[FLOW-9] [DomainGamesCtrl] OnReadyClicked_ServerRpc — {playerName} ready. Count: {readyClientCount}/{gameData.SelectedPlayerCount}</color>");
            CSDebug.Log($"[Server] Player Ready. Count: {readyClientCount}/{gameData.SelectedPlayerCount}");

            // Broadcast which player is ready to all clients
            NotifyPlayerReady_ClientRpc(playerName);

            if (!readyClientCount.Equals(gameData.SelectedPlayerCount))
            {
                Debug.Log($"<color=#FFA500>[FLOW-9] [DomainGamesCtrl] Waiting for more players ({readyClientCount}/{gameData.SelectedPlayerCount})</color>");
                return;
            }

            Debug.Log("<color=#00CED1>[FLOW-9] [DomainGamesCtrl] All players ready! Starting countdown...</color>");
            readyClientCount = 0;
            OnReadyClicked_ClientRpc();
        }

        [ClientRpc]
        void NotifyPlayerReady_ClientRpc(string playerName)
        {
            var domain = gameData.RoundStatsList
                .FirstOrDefault(s => s.Name == playerName)?.Domain ?? Domains.Unassigned;
            GameFeedAPI.Post($"<b>{playerName}</b> Ready", domain, GameFeedType.PlayerReady);
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

        protected override void OnPlayerLeavingFromSession(string clientId)
        {
            if (ulong.TryParse(clientId, out var id) &&
                gameData.TryGetPlayerByOwnerClientId(id, out var player))
            {
                var domain = player.RoundStats?.Domain ?? Domains.Unassigned;
                GameFeedAPI.Post($"<b>{player.Name}</b> disconnected", domain, GameFeedType.PlayerDisconnected);
                gameData.RemovePlayerData(player.Name);
            }
        }
    }
}