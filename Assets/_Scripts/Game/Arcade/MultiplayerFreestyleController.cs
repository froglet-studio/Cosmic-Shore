using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerFreestyleController : MiniGameControllerBase
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;
            miniGameData.OnMiniGameTurnEnd += EndTurn;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;
            miniGameData.OnMiniGameTurnEnd += EndTurn;
        }

        protected override void OnReadyClicked_()
        {
            if (!IsServer)
                return;
            base.OnReadyClicked_();
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
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            miniGameData.SetPlayersActiveForMultiplayer(active: true);
            miniGameData.StartNewGame();
        }
    }
}