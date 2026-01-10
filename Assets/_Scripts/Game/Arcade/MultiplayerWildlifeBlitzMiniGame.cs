using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerWildlifeBlitzMiniGame : MultiplayerMiniGameControllerBase
    {
        int readyClientCount;

        public void OnClickReturnToMainMenu()
        {
            CloseSession_ServerRpc();
        }

        protected override void OnReadyClicked_()
        {
            RaiseToggleReadyButtonEvent(false);
            OnReadyClicked_ServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        void OnReadyClicked_ServerRpc(ServerRpcParams rpcParams = default)
        {
            readyClientCount++;

            // Keep the same pattern you used in MultiplayerCellularDuelController
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

        protected override void SetupNewRound()
        {
            SetupNewRound_ClientRpc();
        }

        [ClientRpc]
        void SetupNewRound_ClientRpc()
        {
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewRound();
        }

        [ServerRpc(RequireOwnership = false)]
        void CloseSession_ServerRpc()
        {
            multiplayerSetup.LeaveSession().Forget();
        }
    }
}