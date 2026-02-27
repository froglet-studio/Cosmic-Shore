using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    public class NetworkScoreTracker : BaseScoreTracker
    {
        public override void OnNetworkSpawn()
        {
            if (!this.IsServerSafe()) return;
            SubscribeScoreEvents();
        }

        public override void OnNetworkDespawn()
        {
            // Use IsServerSafe: after SetActive toggling IsServer may be stale
            if (!this.IsServerSafe()) return;
            UnsubscribeScoreEvents();
        }

        /// <summary>
        /// Re-subscribe when the environment is reactivated (party mode SetActive
        /// toggling). Requires IsSpawned (normal) or IsPartyMode (reactivation —
        /// IsSpawned may be stale after SetActive toggling) so we don't subscribe
        /// too early during initial scene load (before OnNetworkSpawn).
        /// </summary>
        private void OnEnable()
        {
            bool spawned = IsSpawned || (gameData != null && gameData.IsPartyMode);
            if (spawned && this.IsServerSafe())
                SubscribeScoreEvents();
        }

        private void OnDisable()
        {
            if (!this.IsServerSafe()) return;
            UnsubscribeScoreEvents();
        }

        void SubscribeScoreEvents()
        {
            // Unsubscribe first to prevent double-subscription when both
            // OnNetworkSpawn and OnEnable fire (party mode SetActive toggling).
            UnsubscribeScoreEvents();

            gameData.OnInitializeGame.OnRaised += InitializeScoringMode;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd.OnRaised += CalculateWinnerOnServer;
            if (OnClickToMainMenu) OnClickToMainMenu.OnRaised += OnTurnEnded;
        }

        void UnsubscribeScoreEvents()
        {
            gameData.OnInitializeGame.OnRaised -= InitializeScoringMode;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnMiniGameEnd.OnRaised -= CalculateWinnerOnServer;
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
