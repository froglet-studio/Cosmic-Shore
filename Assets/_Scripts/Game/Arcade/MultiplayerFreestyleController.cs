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

        protected override void OnCountdownTimerEnded()
        {
            miniGameData.SetPlayersActiveForMultiplayer(active: true);
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            miniGameData.StartNewGame();
        }
    }
}