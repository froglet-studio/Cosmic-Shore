using System;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Common multiplayer controller base:
    /// - Server-only hooks for NetworkSpawn/Despawn
    /// - Subscribes/Unsubscribes to miniGameData.OnMiniGameTurnEnd
    /// - Delayed Initialize() pattern
    /// - Small helpers for common actions
    /// </summary>
    public abstract class MultipalyerMiniGameControllerBase : MiniGameControllerBase
    {
        /// <summary>
        /// Delay (ms) before Initialize() is called after NetworkSpawn (server only).
        /// </summary>
        protected virtual int InitDelayMs => 1000;
        
        public override void OnNetworkSpawn()
        {
            TeamAssigner.ClearCache();
            
            if (!IsServer)
                return;

            miniGameData.OnMiniGameTurnEnd += EndTurn;
            miniGameData.OnSessionStarted += SubscribeToSessionEvents;
            
            
            InitializeAfterDelay().Forget();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;

            miniGameData.OnMiniGameTurnEnd -= EndTurn;
            miniGameData.OnSessionStarted -= SubscribeToSessionEvents;
        }

        public virtual void OnClickReturnToMainMenu()
        {
            
        }

        private void SubscribeToSessionEvents()
        {
            miniGameData.ActiveSession.Deleted += UnsubscribeFromSessionEvents;
            miniGameData.ActiveSession.PlayerLeaving += OnPlayerLeavingFromSession;
        }

        private void UnsubscribeFromSessionEvents()
        {
            miniGameData.ActiveSession.Deleted -= UnsubscribeFromSessionEvents;
            miniGameData.ActiveSession.PlayerLeaving -= OnPlayerLeavingFromSession;
        }

        void OnPlayerLeavingFromSession(string clientId) {}

        /// <summary>
        /// Runs Initialize() after a small, unscaled delay (server only).
        /// </summary>
        async UniTaskVoid InitializeAfterDelay()
        {
            try
            {
                await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime);
                Initialize();
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        // ---------- Small helpers for subclasses ----------

        void SetPlayersActiveForMultiplayer(bool active) =>
            miniGameData.SetPlayersActiveForMultiplayer(active);

        void ResetRoundCountersServerOnly()
        {
            if (!IsServer)
                return;
            
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
        }

        /// <summary>
        /// Starts a new game on server only (safe for server-gated game flow).
        /// </summary>
        void StartNewGameServerOnly()
        {
            if (IsServer)
                miniGameData.StartNewGameServerOnly();
        }

        /// <summary>
        /// Starts a new game on any side (use only if your StartNewGame is replicated/authority-safe).
        /// </summary>
        void StartNewGameAllSides() => miniGameData.StartNewGame();
        
        protected override void OnCountdownTimerEnded()
        {
            SetPlayersActiveForMultiplayer(true);
            StartNewGameAllSides();
            
            // Matches your original Duel behavior: only server starts the game and resets counters.
            if (!IsServer)
                return;

            ResetRoundCountersServerOnly();
            StartNewGameServerOnly();
        }
    }
}