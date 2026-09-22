using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    public class CoOpWildlifeBlitzMiniGame : MultiplayerMiniGameControllerBase
    {
        protected override void OnReadyClicked_()
        {
            RaiseToggleReadyButtonEvent(false);
            OnReadyClicked_ServerRpc();
        }

        // The ready gate lives on the base (MultiplayerMiniGameControllerBase.EvaluateReadyGate).
        // This mode used to keep its own copy - a bare count evaluated only on a press - which had
        // both of the defects recorded there: a double-press could start the match without somebody,
        // and a player leaving mid-wait stranded everyone else at the ready screen forever.
        [ServerRpc(RequireOwnership = false)]
        void OnReadyClicked_ServerRpc(ServerRpcParams rpcParams = default)
        {
            MarkClientReady(rpcParams.Receive.SenderClientId);
            EvaluateReadyGate("Ready pressed");
        }

        /// <summary>Every human has pressed Ready - start the shared countdown.</summary>
        protected override void OnAllPlayersReady() => OnReadyClicked_ClientRpc();

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