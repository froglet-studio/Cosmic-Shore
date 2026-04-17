using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.Gameplay
{
    public abstract class MultiplayerMiniGameControllerBase : MiniGameControllerBase
    {
        [Inject] protected MultiplayerSetup multiplayerSetup;
        [Inject] private SceneTransitionManager _sceneTransitionManager;

        [Header("Rematch")]
        [SerializeField] private Scoreboard localScoreboard;

        protected virtual int InitDelayMs => 1000;
        private bool _isResetting;

        /// <summary>
        /// When true, Play Again performs a full network scene reload instead of an in-place reset.
        /// Override to true in game modes where the environment doesn't fully reset in-place
        /// (e.g., HexRace with flora/fauna spawning).
        /// </summary>
        protected virtual bool UseSceneReloadForReplay => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
                gameData.OnSessionStarted.OnRaised += SubscribeToSessionEvents;

                // Sync game config to all clients now that we're in the game scene.
                // Previously this was done by SceneLoader via ClientRpc before scene load,
                // but SceneLoader is now a plain MonoBehaviour (no RPCs).
                SyncGameConfigToClients_ClientRpc(
                    gameData.SceneName,
                    (int)gameData.GameMode,
                    gameData.IsMultiplayerMode,
                    (int)gameData.selectedVesselClass.Value,
                    gameData.SelectedIntensity.Value,
                    gameData.SelectedPlayerCount.Value,
                    gameData.RequestedAIBackfillCount,
                    gameData.RequestedTeamCount
                );
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
                Debug.Log($"<color=#00CED1>[FLOW-7] [MultiplayerMiniGameBase] InitializeAfterDelay — waiting {InitDelayMs}ms, IsServer={IsServer}</color>");
                await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime);

                Debug.Log($"<color=#00CED1>[FLOW-7] [MultiplayerMiniGameBase] Calling gameData.InitializeGame(). Players.Count={gameData.Players.Count}</color>");
                gameData.InitializeGame();

                // On replay scene reload, fade in once the player vessel is ready.
                // Runs on ALL machines (server + clients) since each needs to fade their own overlay.
                if (gameData.IsReplayReload)
                {
                    gameData.IsReplayReload = false;
                    gameData.OnClientReady.OnRaised += FadeFromBlackOnReplay;
                }

                if (!IsServer)
                {
                    Debug.Log("<color=#00CED1>[FLOW-7] [MultiplayerMiniGameBase] Not server, skipping session start + round setup</color>");
                    return;
                }

                // Transition ApplicationStateMachine: LoadingGame → InGame.
                // Without this, the loading screen overlay persists because no
                // scene-placed MultiplayerSetup fires InvokeSessionStarted().
                // Safe: ApplicationStateMachine validates transitions and no-ops on invalid ones.
                Debug.Log("<color=#00CED1>[FLOW-7] [MultiplayerMiniGameBase] Server: InvokeSessionStarted (AppState → InGame)</color>");
                gameData.InvokeSessionStarted();

                Debug.Log("<color=#00CED1>[FLOW-7] [MultiplayerMiniGameBase] Server: SetupNewRound()</color>");
                SetupNewRound();
            }
            catch (OperationCanceledException)
            {
                Debug.Log("<color=#FFA500>[FLOW-7] [MultiplayerMiniGameBase] InitializeAfterDelay CANCELLED</color>");
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

            if (UseSceneReloadForReplay && IsServer)
                ExecuteSceneReloadReplay().Forget();
            else
                ResetForReplay_ClientRpc();
        }

        /// <summary>
        /// Full scene reload path for Play Again. Fades to black on all clients,
        /// clears vessel references, then reloads the scene via Netcode.
        /// All environment objects (flora, fauna, track, crystals) are destroyed
        /// with the scene and recreated fresh on reload.
        /// </summary>
        private async UniTaskVoid ExecuteSceneReloadReplay()
        {
            gameData.IsReplayReload = true;

            // Fade to black on all clients before scene reload
            PrepareForSceneReload_ClientRpc();

            // Wait for fade to complete
            await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

            foreach (var player in gameData.Players)
            {
                if (player is Player netPlayer && netPlayer.IsSpawned)
                    netPlayer.NetVesselId.Value = 0;
            }

            // AI players/vessels are spawned with destroyWithScene=false and must be
            // explicitly despawned before the reload, otherwise SpawnAIs creates duplicates.
            // Despawn players before vessels — same order as SceneLoader.ClearPlayerVesselReferences.
            for (int i = gameData.Players.Count - 1; i >= 0; i--)
            {
                if (gameData.Players[i] is Player aiPlayer
                    && aiPlayer.IsSpawned
                    && aiPlayer.NetIsAI.Value)
                {
                    aiPlayer.NetworkObject.Despawn(true);
                }
            }

            for (int i = gameData.Vessels.Count - 1; i >= 0; i--)
            {
                var vessel = gameData.Vessels[i];
                if (vessel is VesselController vc && vc.IsSpawned)
                    vc.NetworkObject.Despawn(true);
            }
            gameData.Vessels.Clear();

            gameData.ResetRuntimeData();
            _isResetting = false;

            // Server-authoritative scene reload — all clients follow automatically
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer && nm.SceneManager != null)
            {
                Debug.Log($"[MultiplayerController] Scene reload replay — loading {gameData.SceneName}");
                nm.SceneManager.LoadScene(gameData.SceneName, LoadSceneMode.Single);
            }
        }

        [ClientRpc]
        private void PrepareForSceneReload_ClientRpc()
        {
            _isResetting = false;
            gameData.IsReplayReload = true;
            _sceneTransitionManager?.SetFadeImmediate(1f);
        }

        private void FadeFromBlackOnReplay()
        {
            gameData.OnClientReady.OnRaised -= FadeFromBlackOnReplay;
            _sceneTransitionManager?.FadeFromBlack().Forget();
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

        // ---------------- Game Config Sync ----------------

        /// <summary>
        /// Syncs the host's game configuration to all clients in the game scene.
        /// Called by OnNetworkSpawn on the server so clients have correct GameDataSO
        /// values (intensity, player count, AI backfill, etc.) before initialization.
        /// </summary>
        [ClientRpc]
        void SyncGameConfigToClients_ClientRpc(
            string sceneName, int gameMode, bool isMultiplayer,
            int vesselClass, int intensity, int playerCount, int aiBackfillCount,
            int teamCount)
        {
            if (IsServer) return;

            gameData.SceneName = sceneName;
            gameData.GameMode = (GameModes)gameMode;
            gameData.IsMultiplayerMode = isMultiplayer;
            gameData.selectedVesselClass.Value = (VesselClassType)vesselClass;
            gameData.SelectedIntensity.Value = intensity;
            gameData.SelectedPlayerCount.Value = playerCount;
            gameData.RequestedAIBackfillCount = aiBackfillCount;
            gameData.RequestedTeamCount = teamCount;
        }
    }
}