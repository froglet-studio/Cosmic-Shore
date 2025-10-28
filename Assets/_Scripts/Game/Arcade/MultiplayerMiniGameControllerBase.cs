using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

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
        [SerializeField]
        protected MultiplayerSetup multiplayerSetup;
        
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
                gameData.OnResetForReplay.OnRaised += OnResetForReplay;
            }
            
            InitializeAfterDelay().Forget();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;

            gameData.OnMiniGameTurnEnd.OnRaised -= EndTurn;
            gameData.OnSessionStarted -= SubscribeToSessionEvents;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
        }

        void SubscribeToSessionEvents()
        {
            gameData.ActiveSession.Deleted += UnsubscribeFromSessionEvents;
            gameData.ActiveSession.PlayerLeaving += OnPlayerLeavingFromSession;
        }

        void UnsubscribeFromSessionEvents()
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
            if (!IsServer)
                return;

            OnCountdownTimerEnded_ClientRpc();
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn(); 
        }

        protected override void EndTurn()
        {
            EndTurn_ClientRpc();
            base.EndTurn();
        }

        [ClientRpc]
        void EndTurn_ClientRpc()
        {
            // Server already invoked this on TurnMonitorController
            if (!IsServer)
            gameData.InvokeGameTurnConditionsMet();
            
            gameData.ResetPlayers();
        }
        
        protected override void EndGame()
        {
            EndGame_ClientRpc();
        }
        
        [ClientRpc]
        void EndGame_ClientRpc()
        {
            gameData.InvokeMiniGameEnd();
        }
    }
}