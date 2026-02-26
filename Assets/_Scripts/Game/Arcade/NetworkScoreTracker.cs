using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class NetworkScoreTracker : BaseScoreTracker
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            SubscribeScoreEvents();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            UnsubscribeScoreEvents();
        }

        /// <summary>
        /// Re-subscribe when the environment is reactivated (party mode SetActive
        /// toggling). Prevents inactive environments' score trackers from
        /// responding to events and firing conflicting RPCs.
        /// </summary>
        private void OnEnable()
        {
            if (IsSpawned && IsServer)
                SubscribeScoreEvents();
        }

        private void OnDisable()
        {
            if (!IsServer) return;
            UnsubscribeScoreEvents();
        }

        void SubscribeScoreEvents()
        {
            // Unsubscribe first to prevent double-subscription when both
            // OnNetworkSpawn and OnEnable fire (party mode SetActive toggling).
            UnsubscribeScoreEvents();

            gameData.OnInitializeGame += InitializeScoringMode;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd += CalculateWinnerOnServer;
            if (OnClickToMainMenu) OnClickToMainMenu.OnRaised += OnTurnEnded;
        }

        void UnsubscribeScoreEvents()
        {
            gameData.OnInitializeGame -= InitializeScoringMode;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnMiniGameEnd -= CalculateWinnerOnServer;
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