// MultiplayerMiniGameControllerBase.cs
using System;
using CosmicShore.Game.UI;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

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

        // ---------------- Session Management ----------------

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
                if (!IsServer) return;
                SetupNewRound();
            }
            catch (OperationCanceledException) { }
        }

        // ---------------- Turn & Round Flow ----------------

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
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
            if (!IsServer) return;
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
            if (!IsServer) return;
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
            if (!IsServer) return;
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
            if (IsServer) ShowReadyButton_ClientRpc();
        }

        protected override void SetupNewRound()
        {
            base.SetupNewRound();
            if (IsServer) ShowReadyButton_ClientRpc();
        }

        [ClientRpc]
        void ShowReadyButton_ClientRpc()
        {
            RaiseToggleReadyButtonEvent(true);
        }

        // ---------------- Reset / Replay ----------------

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
            Debug.Log("[MultiplayerController] Resetting Environment...");
            _isResetting = false;

            gameData.ResetStatsDataForReplay();
            gameData.ResetPlayers();

            if (gameData.OnResetForReplay != null)
                gameData.OnResetForReplay.Raise();
            else
                Debug.LogError("[MultiplayerController] OnResetForReplay Event missing!");

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

        // ---------------- Rematch ----------------

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
                Debug.LogError("[MultiplayerController] localScoreboard not assigned — cannot show rematch request.");
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
                Debug.LogError("[MultiplayerController] localScoreboard not assigned — cannot show rematch declined.");
        }
    }
}