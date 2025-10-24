using Unity.Netcode;


namespace CosmicShore.Game.Arcade
{
    public class NetworkScoreTracker : BaseScoreTracker
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            gameData.OnInitializeGame += InitializeScoringMode;
            gameData.OnMiniGmaeTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd += CalculateWinnerOnServer;
            OnClickToMainMenu.OnRaised += OnTurnEnded;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;

            gameData.OnInitializeGame -= InitializeScoringMode;
            gameData.OnMiniGmaeTurnStarted.OnRaised -= OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnMiniGameEnd -= CalculateWinnerOnServer;
            OnClickToMainMenu.OnRaised -= OnTurnEnded;
        }

        private void CalculateWinnerOnServer()
        {
            SendRoundStats_ClientRpc();
        }

        [ClientRpc]
        private void SendRoundStats_ClientRpc()
        {
            SortAndInvokeResults();
        }
    }
}