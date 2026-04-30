using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.Gameplay
{
    public abstract class MultiplayerMiniGameControllerBase : MiniGameControllerBase
    {
        [Inject] protected MultiplayerSetup multiplayerSetup;
        [Inject] private SceneTransitionManager _sceneTransitionManager;

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
            EnsureLocalHumanCanMove();
        }

        /// <summary>
        /// Defensive: after replay, <see cref="Player.StartPlayer"/> sometimes
        /// races with the pair-initialization pipeline and the Paused NetworkVariable
        /// write from <see cref="Player.ResetForPlay"/> (Paused=true) isn't reliably
        /// cleared before input is needed on a non-host client. Explicitly drive
        /// the local human's input state to active here so the client can move
        /// their vessel as soon as the turn starts.
        /// </summary>
        protected void EnsureLocalHumanCanMove()
        {
            var local = gameData.LocalPlayer;
            if (local == null || local.IsInitializedAsAI) return;

            var inputController = local.InputController;
            if (inputController == null) return;

            inputController.SetPause(false);
            inputController.SetIdle(false);

            if (local.Vessel != null)
                local.Vessel.VesselStatus.IsStationary = false;
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

            // On the first round (RoundsPlayed==0), MiniGameHUD controls
            // ReadyButton visibility after the pre-game cinematic finishes.
            // On subsequent rounds, show it immediately.
            if (IsServer && gameData.RoundsPlayed > 0)
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
        /// Entry point for Scoreboard / PauseMenu "Play Again" button.
        /// Only the host can trigger a replay — all clients are forced to follow.
        /// </summary>
        public override void RequestReplay()
        {
            if (!IsServer)
            {
                CSDebug.LogWarning("[MultiplayerController] RequestReplay ignored — only the host can restart the game.");
                return;
            }
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
            try
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

                // Server-authoritative scene reload — all clients follow automatically
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer && nm.SceneManager != null)
                {
                    Debug.Log($"[MultiplayerController] Scene reload replay — loading {gameData.SceneName}");
                    nm.SceneManager.LoadScene(gameData.SceneName, LoadSceneMode.Single);
                }
            }
            finally
            {
                // Release the gate regardless of outcome. On the happy path the scene
                // reload destroys this NetworkBehaviour anyway; on exception paths this
                // prevents the button from being permanently bricked.
                _isResetting = false;
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