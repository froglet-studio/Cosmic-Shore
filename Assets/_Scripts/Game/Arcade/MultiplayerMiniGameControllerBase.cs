using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Common multiplayer controller base:
    /// - Server-only hooks for NetworkSpawn/Despawn
    /// - Subscribes/Unsubscribes to miniGameData.OnMiniGameTurnEnd
    /// - Delayed Initialize() pattern
    /// - Small helpers for common actions
    /// </summary>
    public abstract class MultiplayerMiniGameControllerBase : MiniGameControllerBase
    {
        /// <summary>
        /// Delay (ms) before Initialize() is called after NetworkSpawn (server only).
        /// </summary>
        int initDelayMs => 1000;
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
                gameData.OnSessionStarted += SubscribeToSessionEvents;    
            }
            
            InitializeAfterDelay().Forget();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;

            gameData.OnMiniGameTurnEnd.OnRaised -= EndTurn;
            gameData.OnSessionStarted -= SubscribeToSessionEvents;
        }

        private void SubscribeToSessionEvents()
        {
            gameData.ActiveSession.Deleted += UnsubscribeFromSessionEvents;
            gameData.ActiveSession.PlayerLeaving += OnPlayerLeavingFromSession;
        }

        private void UnsubscribeFromSessionEvents()
        {
            gameData.ActiveSession.Deleted -= UnsubscribeFromSessionEvents;
            gameData.ActiveSession.PlayerLeaving -= OnPlayerLeavingFromSession;
        }

        void OnPlayerLeavingFromSession(string clientId) {}

        /// <summary>
        /// Runs Initialize() after a small, unscaled delay (server only).
        /// </summary>
        async UniTaskVoid InitializeAfterDelay()
        {
            try
            {
                await UniTask.Delay(initDelayMs, DelayType.UnscaledDeltaTime);
                InitializeGame();
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }
        
        protected override void OnCountdownTimerEnded()
        {
            gameData.StartTurn(); // For this client only.
            OnCountdownTimerEnded_ServerRpc();
            
            // Matches your original Duel behavior: only server starts the game and resets counters.
            if (!IsServer)
                return;

            // reset this only in server one time
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
        }

        [ServerRpc(RequireOwnership = false)]
        void OnCountdownTimerEnded_ServerRpc()
        {
            OnCountdownTimerEnded_ClientRpc();
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc()
        {
            gameData.SetPlayersActiveForMultiplayer();
        }
        
        protected override void EndGame()
        {
            EndGame_ClientRpc();
        }
        
        [ClientRpc]
        private void EndGame_ClientRpc()
        {
            gameData.InvokeMiniGameEnd();
            gameData.ResetPlayers();
        }
    }
}