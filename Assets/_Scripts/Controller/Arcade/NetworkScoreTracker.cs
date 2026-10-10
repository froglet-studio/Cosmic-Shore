using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Gameplay
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
            // Half a second for the last stats to land, bound to this tracker: a match torn down
            // inside the wait must not send an RPC from a dead object or keep it reachable.
            if (await UniTask.Delay(500, cancellationToken: destroyCancellationToken).SuppressCancellationThrow()) return;
            SendRoundStats_ClientRpc();
        }

        [ClientRpc]
        private void SendRoundStats_ClientRpc()
        {
            SortAndInvokeResults();
        }
    }
}