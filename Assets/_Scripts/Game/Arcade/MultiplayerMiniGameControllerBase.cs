using System;
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
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
                gameData.OnSessionStarted += SubscribeToSessionEvents;
            }

            gameData.OnResetForReplay.OnRaised += OnResetForReplay;

            if (IsServer)
                InitializeAfterDelay().Forget();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
                gameData.OnSessionStarted -= SubscribeToSessionEvents;
            }
            
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
            
            UnsubscribeFromSessionEvents();
            
            base.OnNetworkDespawn();
        }
        
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
            // Subclasses can override to handle disconnection
        }

        /// <summary>
        /// Runs Initialize() after a small delay (server only).
        /// The delay ensures network setup is complete before starting game flow.
        /// </summary>
        async UniTaskVoid InitializeAfterDelay()
        {
            try
            {
                await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime);
                
                if (!IsServer)
                    return;
                    
                gameData.InitializeGame();
                SetupNewRound();
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, ignore
            }
        }
        
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
            
            if (IsServer)
                gameData.StartTurn();
        }
        
        /// <summary>
        /// Handles turn end event from server.
        /// This is called by the event subscription, not by override.
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
        
        /// <summary>
        /// Server executes turn end logic which may trigger round end or game end.
        /// This bridges to the base class's protected EndTurn method.
        /// </summary>
        void ExecuteServerTurnEnd()
        {
            gameData.TurnsTakenThisRound++;

            if (gameData.TurnsTakenThisRound >= numberOfTurnsPerRound)
                ExecuteServerRoundEnd();
            else 
                SetupNewTurn();
        }

        /// <summary>
        /// Server executes round end logic which may trigger game end.
        /// </summary>
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
        
        /// <summary>
        /// Server triggers game end sequence.
        /// </summary>
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

        protected override void OnResetForReplay()
        {
            if (!IsServer)
                return;
            OnResetForReplayCustom();
            SetupNewRound();
        }
        
        /// <summary>
        /// Hook for game-specific replay logic in multiplayer.
        /// Called on server only before restarting game.
        /// </summary>
        protected virtual void OnResetForReplayCustom()
        {
        }
    }
}