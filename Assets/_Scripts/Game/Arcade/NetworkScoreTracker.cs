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

            if (gameData.OnInitializeGame) gameData.OnInitializeGame.OnRaised += InitializeScoringMode;
            if (gameData.OnMiniGameTurnStarted) gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            if (gameData.OnMiniGameTurnEnd) gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            if (gameData.OnMiniGameEnd) gameData.OnMiniGameEnd.OnRaised += CalculateWinnerOnServer;
            if (OnClickToMainMenu) OnClickToMainMenu.OnRaised += OnTurnEnded;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;

            if (gameData.OnInitializeGame) gameData.OnInitializeGame.OnRaised -= InitializeScoringMode;
            if (gameData.OnMiniGameTurnStarted) gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            if (gameData.OnMiniGameTurnEnd) gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            if (gameData.OnMiniGameEnd) gameData.OnMiniGameEnd.OnRaised -= CalculateWinnerOnServer;
            if (OnClickToMainMenu) OnClickToMainMenu.OnRaised -= OnTurnEnded;
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