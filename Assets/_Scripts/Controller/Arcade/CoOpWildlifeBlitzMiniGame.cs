using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    public class CoOpWildlifeBlitzMiniGame : MultiplayerMiniGameControllerBase
    {
        int readyClientCount;

        protected override void OnReadyClicked_()
        {
            RaiseToggleReadyButtonEvent(false);
            OnReadyClicked_ServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        void OnReadyClicked_ServerRpc(ServerRpcParams rpcParams = default)
        {
            readyClientCount++;

            // Use connected clients count (humans only - excludes AI)
            int humanCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
            if (readyClientCount < humanCount)
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
            EnsureLocalHumanCanMove();
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
    }
}