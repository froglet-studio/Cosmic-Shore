using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class NetworkScoreTracker : BaseScoreTracker
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            gameData.OnInitializeGame.OnRaised += InitializeScoringMode;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd.OnRaised += CalculateWinnerOnServer;
            OnClickToMainMenu.OnRaised += OnTurnEnded;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;

            gameData.OnInitializeGame.OnRaised -= InitializeScoringMode;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnMiniGameEnd.OnRaised -= CalculateWinnerOnServer;
            OnClickToMainMenu.OnRaised -= OnTurnEnded;
        }

        private void CalculateWinnerOnServer()
        {
            DelayAndSendResults().Forget(); // fire and forget async call
        }

        private async UniTaskVoid DelayAndSendResults()
        {
            await UniTask.Delay(500); // waits for 0.5 seconds (500ms)
            SendRoundStats_ClientRpc();
        }

        [ClientRpc]
        private void SendRoundStats_ClientRpc()
        {
            SortAndInvokeResults();
        }
    }
}