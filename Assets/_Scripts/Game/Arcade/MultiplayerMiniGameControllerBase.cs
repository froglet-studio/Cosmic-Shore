// MultiplayerMiniGameControllerBase.cs
using System;
using CosmicShore.Game.UI;
using CosmicShore.Utility.ClassExtensions;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public abstract class MultiplayerMiniGameControllerBase : MiniGameControllerBase
    {
        [Header("Multiplayer")]
        [SerializeField] protected MultiplayerSetup multiplayerSetup;

        [Header("Rematch")]
        [SerializeField] private Scoreboard localScoreboard;

        protected virtual int InitDelayMs => 1000;
        private bool _isResetting;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // In party mode, skip all autonomous lifecycle management.
            // The PartyGameController will call into us when needed.
            if (IsPartyMode)
            {
                CSDebug.Log($"[{GetType().Name}] OnNetworkSpawn — PARTY MODE, skipping autonomous init.");
                return;
            }

            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
                gameData.OnSessionStarted += SubscribeToSessionEvents;
            }

            InitializeAfterDelay().Forget();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
                gameData.OnSessionStarted -= SubscribeToSessionEvents;
            }

            UnsubscribeFromSessionEvents();
            base.OnNetworkDespawn();
        }

        // ==================== Party Mode API ====================
        // These methods are called by PartyGameController to drive gameplay
        // without the controller's own lifecycle getting in the way.

        /// <summary>
        /// Called by PartyGameController when this mini-game's environment is activated.
        /// Subscribes to turn-end events so gameplay mechanics work,
        /// but does NOT call InitializeGame or SetupNewRound.
        /// </summary>
        public virtual void PartyMode_Activate()
        {
            if (!IsPartyMode) return;

            if (this.IsServerSafe())
            {
                gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
            }

            // Initialize the mini-game and show its ready button so the
            // player can click Ready to start the countdown + gameplay.
            InitializeAfterDelay().Forget();

            CSDebug.Log($"[{GetType().Name}] PartyMode_Activate — subscribed to turn events, initializing.");
        }

        /// <summary>
        /// Called by PartyGameController when this mini-game's round is complete.
        /// Unsubscribes from events to prevent stale callbacks.
        /// </summary>
        public virtual void PartyMode_Deactivate()
        {
            if (!IsPartyMode) return;

            if (this.IsServerSafe())
            {
                gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
            }

            CSDebug.Log($"[{GetType().Name}] PartyMode_Deactivate — unsubscribed from turn events.");
        }

        // ==================== Session Management ================

        void SubscribeToSessionEvents()
        {
            if (gameData.ActiveSession == null) return;
            gameData.ActiveSession.Deleted += UnsubscribeFromSessionEvents;
            gameData.ActiveSession.PlayerLeaving += OnPlayerLeavingFromSession;
        }

        void UnsubscribeFromSessionEvents()
        {
            if (gameData.ActiveSession == null) return;
            gameData.ActiveSession.Deleted -= UnsubscribeFromSessionEvents;
            gameData.ActiveSession.PlayerLeaving -= OnPlayerLeavingFromSession;
        }

        protected virtual void OnPlayerLeavingFromSession(string clientId) { }

        async UniTaskVoid InitializeAfterDelay()
        {
            try
            {
                await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime);
                gameData.InitializeGame();
                if (!this.IsServerSafe()) return;
                SetupNewRound();

                // In party mode, auto-start the game — no manual ready-click required.
                // This triggers the countdown → SetPlayersActive → StartTurn, which
                // fires OnMiniGameTurnStarted and initialises HUD, score cards, and
                // turn monitors.
                if (IsPartyMode)
                    OnReadyClicked();
            }
            catch (OperationCanceledException) { }
        }

        // ==================== Turn & Round Flow ================

        protected override void OnCountdownTimerEnded()
        {
            if (!this.IsServerSafe()) return;
            OnCountdownTimerEnded_ClientRpc();
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        void HandleTurnEnd()
        {
            if (!this.IsServerSafe()) return;

            if (IsPartyMode)
            {
                // RPCs not registered — do turn-end work locally.
                // Host is the only human client in party mode.
                if (ShouldResetPlayersOnTurnEnd)
                    gameData.ResetPlayers();
                OnTurnEndedCustom();
            }
            else
            {
                SyncTurnEnd_ClientRpc();
            }

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
            if (!this.IsServerSafe()) return;

            // Sync to remote clients (not needed in party mode — RPCs not registered)
            if (!IsPartyMode)
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
            if (!this.IsServerSafe()) return;

            if (IsPartyMode)
            {
                // In party mode, trigger the end-game cinematic locally.
                // InvokeWinnerCalculated starts the cinematic → score reveal → Continue.
                // Don't fire InvokeMiniGameEnd — CompleteRound is driven by
                // OnShowGameEndScreen after the cinematic finishes.
                // The isRunning guard in EndGameCinematicController prevents double-starts
                // when game-specific controllers (HexRace, Joust) fire this first.
                gameData.SortRoundStats(UseGolfRules);
                gameData.CalculateDomainStats(UseGolfRules);
                gameData.InvokeWinnerCalculated();
                CSDebug.Log($"[{GetType().Name}] ExecuteServerGameEnd — party mode, fired WinnerCalculated.");
                return;
            }

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

            if (IsPartyMode)
                RaiseToggleReadyButtonEvent(true);
            else if (IsServer)
                ShowReadyButton_ClientRpc();
        }

        protected override void SetupNewRound()
        {
            base.SetupNewRound();

            if (IsPartyMode)
                RaiseToggleReadyButtonEvent(true);
            else if (IsServer)
                ShowReadyButton_ClientRpc();
        }

        [ClientRpc]
        void ShowReadyButton_ClientRpc()
        {
            RaiseToggleReadyButtonEvent(true);
        }

        // ==================== Reset / Replay ====================

        protected override void OnResetForReplay() { }

        public void RequestReplay()
        {
            if (IsServer) ExecuteReplaySequence();
            else RequestReplay_ServerRpc();
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
            ResetForReplay_ClientRpc();
        }

        [ClientRpc]
        void ResetForReplay_ClientRpc()
        {
            CSDebug.Log("[MultiplayerController] Resetting Environment...");
            _isResetting = false;

            gameData.ResetStatsDataForReplay();
            gameData.ResetPlayers();

            // Snap player camera to the vessel's new spawn position after
            // ResetPlayers teleported it, clearing any stale cinematic position.
            if (CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();

            if (gameData.OnResetForReplay != null)
                gameData.OnResetForReplay.Raise();
            else
                CSDebug.LogError("[MultiplayerController] OnResetForReplay Event missing!");

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

        protected virtual void OnResetForReplayCustom() { }

        // ==================== Rematch ====================

        /// <summary>
        /// Called by Scoreboard when local player presses Play Again.
        /// Broadcasts rematch request to opponent.
        /// </summary>
        public void RequestRematch(string requesterName)
        {
            RequestRematch_ServerRpc(new FixedString64Bytes(requesterName));
        }

        [ServerRpc(RequireOwnership = false)]
        void RequestRematch_ServerRpc(FixedString64Bytes requesterName)
        {
            RequestRematch_ClientRpc(requesterName);
        }

        [ClientRpc]
        void RequestRematch_ClientRpc(FixedString64Bytes requesterName)
        {
            string name = requesterName.ToString();

            // Don't show the panel to the player who sent the request
            if (gameData.LocalPlayer?.Name == name) return;

            if (localScoreboard != null)
                localScoreboard.ShowRematchRequest(name);
            else
                CSDebug.LogError("[MultiplayerController] localScoreboard not assigned — cannot show rematch request.");
        }

        /// <summary>
        /// Called by Scoreboard when local player declines a rematch request.
        /// Notifies the requester.
        /// </summary>
        public void NotifyRematchDeclined(string declinerName)
        {
            NotifyRematchDeclined_ServerRpc(new FixedString64Bytes(declinerName));
        }

        [ServerRpc(RequireOwnership = false)]
        void NotifyRematchDeclined_ServerRpc(FixedString64Bytes declinerName)
        {
            NotifyRematchDeclined_ClientRpc(declinerName);
        }

        [ClientRpc]
        void NotifyRematchDeclined_ClientRpc(FixedString64Bytes declinerName)
        {
            string name = declinerName.ToString();

            // Only show denied panel to the player whose request was rejected
            if (gameData.LocalPlayer?.Name == name) return;

            if (localScoreboard != null)
                localScoreboard.ShowRematchDeclined(name);
            else
                CSDebug.LogError("[MultiplayerController] localScoreboard not assigned — cannot show rematch declined.");
        }
    }
}