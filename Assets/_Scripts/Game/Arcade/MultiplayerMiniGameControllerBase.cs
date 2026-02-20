using System;
using CosmicShore.Systems;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Base controller for multiplayer game modes.
    /// Handles network synchronization, session management, and client-server communication.
    /// </summary>
    public abstract class MultiplayerMiniGameControllerBase : MiniGameControllerBase
    {
        [Header("Multiplayer")]
        [SerializeField] protected MultiplayerSetup multiplayerSetup;
        
        protected virtual int InitDelayMs => 1000;
        private bool _isResetting;
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
                gameData.OnSessionStarted.OnRaised += SubscribeToSessionEvents;
            }
            
            InitializeAfterDelay().Forget();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
                gameData.OnSessionStarted.OnRaised -= SubscribeToSessionEvents;
            }
            
            UnsubscribeFromSessionEvents();
            
            base.OnNetworkDespawn();
        }

        // ---------------- Session Management ----------------

        void SubscribeToSessionEvents()
        {
            if (gameData.ActiveSession == null)
                return;
                
            gameData.ActiveSession.Deleted += UnsubscribeFromSessionEvents;
            gameData.ActiveSession.PlayerLeaving += OnPlayerLeavingFromSession;
        }

        void UnsubscribeFromSessionEvents()
        {
            if (gameData.ActiveSession == null)
                return;
                
            gameData.ActiveSession.Deleted -= UnsubscribeFromSessionEvents;
            gameData.ActiveSession.PlayerLeaving -= OnPlayerLeavingFromSession;
        }

        /// <summary>
        /// Called when a player leaves the session.
        /// Override to handle player disconnection logic.
        /// </summary>
        protected virtual void OnPlayerLeavingFromSession(string clientId) 
        {
            // Base implementation does nothing
        }

        /// <summary>
        /// Runs Initialize() after a small delay (server only).
        /// </summary>
        async UniTaskVoid InitializeAfterDelay()
        {
            try
            {
                await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime);
                
                gameData.InitializeGame();
                
                if (!IsServer)
                    return;
                
                SetupNewRound();
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, ignore
            }
        }
        
        // ---------------- Turn & Round Flow ----------------

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer)
                return;
                
            // Server activates players and starts turn
            OnCountdownTimerEnded_ClientRpc();
        }
        
        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }
        
        /// <summary>
        /// Handles turn end event from server.
        /// </summary>
        void HandleTurnEnd()
        {
            if (!IsServer)
                return;

            SyncTurnEnd_ClientRpc();
            ExecuteServerTurnEnd();
        }
        
        [ClientRpc]
        void SyncTurnEnd_ClientRpc()
        {
            if (!IsServer)
                gameData.InvokeGameTurnConditionsMet();

            if (ShouldResetPlayersOnTurnEnd)
                gameData.ResetPlayers();

            OnTurnEndedCustom();
        }
        
        void ExecuteServerTurnEnd()
        {
            gameData.TurnsTakenThisRound++;

            if (gameData.TurnsTakenThisRound >= numberOfTurnsPerRound)
                ExecuteServerRoundEnd();
            else 
                SetupNewTurn();
        }

        void ExecuteServerRoundEnd()
        {
            if (!IsServer)
                return;
            
            // Notify all clients
            SyncRoundEnd_ClientRpc();
            gameData.RoundsPlayed++;
            gameData.InvokeMiniGameRoundEnd();
            
            OnRoundEndedCustom();
            
            if (HasEndGame && gameData.RoundsPlayed >= numberOfRounds)
                ExecuteServerGameEnd();
            else
                SetupNewRound();
        }

        [ClientRpc]
        void SyncRoundEnd_ClientRpc()
        {
            if (IsServer) return;
            gameData.RoundsPlayed++;
            gameData.InvokeMiniGameRoundEnd();
            OnRoundEndedCustom();
        }
        
        void ExecuteServerGameEnd()
        {
            if (!IsServer)
                return;
                
            SyncGameEnd_ClientRpc();
        }
        
        [ClientRpc]
        void SyncGameEnd_ClientRpc()
        {
            if (!ShowEndGameSequence) return;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules); 
            
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void SetupNewTurn()
        {
            base.SetupNewTurn();
            
            if (IsServer)
                ShowReadyButton_ClientRpc();
        }
        
        protected override void SetupNewRound()
        {
            base.SetupNewRound();
            
            if (IsServer)
                ShowReadyButton_ClientRpc();
        }
        
        [ClientRpc]
        void ShowReadyButton_ClientRpc()
        {
            RaiseToggleReadyButtonEvent(true);
        }

        // ---------------- Reset / Replay Logic ----------------

        protected override void OnResetForReplay()
        {
        }

        /// <summary>
        /// Public entry point for Scoreboard "Play Again" button.
        /// Handles Client->Server permission request.
        /// </summary>
        public void RequestReplay()
        {
            if (IsServer)
            {
                ExecuteReplaySequence();
            }
            else
            {
                RequestReplay_ServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void RequestReplay_ServerRpc()
        {
            ExecuteReplaySequence();
        }

        void ExecuteReplaySequence()
        {
            if (_isResetting) return;
            _isResetting = true;
            
            // Initiate reset on all machines
            ResetForReplay_ClientRpc();
        }
        
        [ClientRpc]
        void ResetForReplay_ClientRpc()
        {
            Debug.Log("[MultiplayerController] Resetting Environment...");
            _isResetting = false;

            gameData.ResetStatsDataForReplay();
            gameData.ResetPlayers();

            if (gameData.OnResetForReplay != null)
                gameData.OnResetForReplay.Raise();
            else
                Debug.LogError("[MultiplayerController] OnResetForReplay Event is missing on GameData!");
            
            OnResetForReplayCustom();
            RaiseToggleReadyButtonEvent(true);
 
            if (IsServer)
                ResetServerRoundAfterDelay().Forget();
        }

        async UniTaskVoid ResetServerRoundAfterDelay()
        {
            await UniTask.Delay(100); 
            SetupNewRound();
        }
        
        protected virtual void OnResetForReplayCustom()
        {
            // Override in subclass to reset game-specific elements
        }
    }
}