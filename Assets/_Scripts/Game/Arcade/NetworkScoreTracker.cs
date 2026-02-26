using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class NetworkScoreTracker : BaseScoreTracker
    {
        /// <summary>
        /// True when this tracker should act with server authority.
        /// In party mode, IsServer may be false after env deactivation/reactivation
        /// (IsSpawned unreliable), but the host is always both server and client.
        /// </summary>
        bool IsEffectiveServer => IsServer || (gameData != null && gameData.IsPartyMode);

        public override void OnNetworkSpawn()
        {
            if (!IsEffectiveServer) return;
            SubscribeScoreEvents();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeScoreEvents();
        }

        /// <summary>
        /// Re-subscribe when the environment is reactivated (party mode SetActive
        /// toggling). Prevents inactive environments' score trackers from
        /// responding to events and firing conflicting RPCs.
        /// In party mode, subscribe even when IsSpawned is false — the environment
        /// was deactivated during network spawn so IsSpawned may be unreliable.
        /// </summary>
        private void OnEnable()
        {
            if (IsSpawned || (gameData != null && gameData.IsPartyMode))
                SubscribeScoreEvents();
        }

        private void OnDisable()
        {
            UnsubscribeScoreEvents();
        }

        void SubscribeScoreEvents()
        {
            // Only server (or party mode host) drives scoring
            if (!IsEffectiveServer) return;

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
            // In party mode, sort + invoke directly — RPCs may not work
            // after environment deactivation/reactivation.
            if (gameData != null && gameData.IsPartyMode)
            {
                SortAndInvokeResults();
                return;
            }

            DelayAndSendResults().Forget();
        }

        private async UniTaskVoid DelayAndSendResults()
        {
            await UniTask.Delay(500);
            SendRoundStats_ClientRpc();
        }

        [ClientRpc]
        private void SendRoundStats_ClientRpc()
        {
            SortAndInvokeResults();
        }
    }
}