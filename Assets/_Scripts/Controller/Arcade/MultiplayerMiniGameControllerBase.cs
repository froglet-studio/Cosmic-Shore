using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Gameplay;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using Reflex.Attributes;

namespace CosmicShore.Gameplay
{
    public abstract class MultiplayerMiniGameControllerBase : MiniGameControllerBase
    {
        [Inject] private SceneTransitionManager _sceneTransitionManager;
        [Inject] private CosmicShore.Core.SceneLoader _sceneLoader;
        [Inject] private HostConnectionDataSO _hostConnectionData;

        protected virtual int InitDelayMs => 1000;
        private bool _isResetting;

        /// <summary>
        /// When true, Play Again performs a full network scene reload instead of an in-place reset.
        /// Override to true in game modes where the environment doesn't fully reset in-place
        /// (e.g., SkimRace with flora/fauna spawning).
        /// </summary>
        protected virtual bool UseSceneReloadForReplay => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            RearmGameStartStatsReset();   // fresh scene = fresh game

            LoadInsights.Mark($"Game controller spawned ({GetType().Name}, IsServer={IsServer})");

            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
                gameData.OnSessionStarted.OnRaised += SubscribeToSessionEvents;

                // The ready gate is re-decided when the ROSTER changes, not only when somebody
                // presses - otherwise a player leaving mid-wait strands everyone else at the ready
                // screen permanently. See EvaluateReadyGate.
                if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnectedForReadyGate;

                StampMatchEnvelope();

                // The server IS the authority, so its config is synced by definition. Set before
                // the broadcast: Cell.AssignConfig gates its (sticky) IntensityWise choice on this
                // flag, and on the host that choice can happen at any point after this frame.
                gameData.GameConfigSynced = true;

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
                    gameData.RequestedDomainCount,
                    gameData.IsMaelstromMode,
                    gameData.ComebackRatePerScoreDeficit,
                    gameData.MatchId,
                    gameData.PartyId,
                    gameData.InviteTriggered
                );
            }

            // CLIENT: ask for the config rather than only hoping to catch the server's broadcast.
            // That broadcast is fired from the SERVER's OnNetworkSpawn - the instant the SERVER
            // finished loading the scene - with no ack, no retry and no NetworkVariable fallback,
            // and NGO only holds a message for an object that has not spawned yet for
            // SpawnTimeout (10s). A client on a long link loading a heavy scene can miss that
            // window entirely, and then it never learns the intensity: Cell.AssignConfig latches
            // its sticky IntensityWise choice on GameConfigSynced, so the client silently BUILDS
            // A DIFFERENT ARENA than the host for the whole match. The pull mirrors
            // ClientPlayerVesselInitializer's roster pull and closes the race in the one
            // direction that matters, because a client always spawns before it can ask.
            if (!IsServer)
                RequestGameConfig_ServerRpc();

            // REQUIRED for every party game: the elemental comeback system. Scene-authored
            // instances are respected; a scene that forgot one gets it created and configured
            // for this game mode (comeback runs locally on every machine, so this executes on
            // host and clients alike). UseGolfRules travels with it so a Score-sourced mode
            // knows which direction "ahead" is.
            ElementalComebackSystem.EnsureExists(gameObject, gameData, UseGolfRules);

            InitializeAfterDelay().Forget();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
                gameData.OnSessionStarted.OnRaised -= SubscribeToSessionEvents;

                if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnectedForReadyGate;
            }
            ResetReadyGate();
            ResetRematchVotes();
            
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
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"[FLOW-7] [MultiplayerMiniGameBase] InitializeAfterDelay - waiting {InitDelayMs}ms, IsServer={IsServer}");
                using (LoadInsights.Measure(LoadInsightCategory.ScriptedDelay,
                           $"InitDelayMs gate before InitializeGame ({InitDelayMs}ms)", isWait: true))
                {
                    await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime);
                }

                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"[FLOW-7] [MultiplayerMiniGameBase] Calling gameData.InitializeGame(). Players.Count={gameData.Players.Count}");
                using (LoadInsights.Measure(LoadInsightCategory.GameFlow,
                           "InitializeGame raise (inline listeners: cell, spawn adapters, HUD…)"))
                {
                    gameData.InitializeGame();
                }

                // On replay scene reload, fade in once the player vessel is ready.
                // Runs on ALL machines (server + clients) since each needs to fade their own overlay.
                if (gameData.IsReplayReload)
                {
                    gameData.IsReplayReload = false;
                    gameData.OnClientReady.OnRaised += FadeFromBlackOnReplay;
                }

                if (!IsServer)
                {
                    CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "[FLOW-7] [MultiplayerMiniGameBase] Not server, skipping session start + round setup");
                    return;
                }

                // Transition ApplicationStateMachine: LoadingGame → InGame.
                // Without this, the loading screen overlay persists because no
                // scene-placed MultiplayerSetup fires InvokeSessionStarted().
                // Safe: ApplicationStateMachine validates transitions and no-ops on invalid ones.
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "[FLOW-7] [MultiplayerMiniGameBase] Server: InvokeSessionStarted (AppState → InGame)");
                gameData.InvokeSessionStarted();

                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "[FLOW-7] [MultiplayerMiniGameBase] Server: SetupNewRound()");
                SetupNewRound();
            }
            catch (OperationCanceledException)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "[FLOW-7] [MultiplayerMiniGameBase] InitializeAfterDelay CANCELLED");
                // Task was cancelled, ignore
            }
        }
        
        /// <summary>
        /// Host-only: stamps the identifiers that group players into one game instance.
        /// Runs once per game launch, before the config broadcast.
        ///
        /// player_ids is deliberately NOT stamped here - at OnNetworkSpawn the roster has not
        /// settled. It is derived at game_started from replicated Player NetworkObjects and
        /// sorted, so every peer computes the same set from the same replicated state.
        /// See Docs/Analytics/DATA_ARCHITECTURE.md §6.
        /// </summary>
        void StampMatchEnvelope()
        {
            gameData.MatchId = Guid.NewGuid().ToString("N");
            gameData.PartyId = ResolvePartyId();
            gameData.InviteTriggered = _hostConnectionData != null && _hostConnectionData.PartyFormedByInvite;
        }

        /// <summary>
        /// The Relay party session id, shared by everyone in the session. Falls back to the
        /// match id for a solo session with no active session object, so the field is never
        /// empty and solo games do not all collapse into one group.
        /// </summary>
        string ResolvePartyId()
        {
            var session = gameData.ActiveSession;
            if (session != null && !string.IsNullOrEmpty(session.Id))
                return session.Id;

            return gameData.MatchId;
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
            // THE GAME STARTS HERE, so the score starts here. Zero every player's stats before
            // the first turn begins: StatsManager has no turn gate, so it has been recording
            // since the scene's network spawn - through the arena build, the vessel spawns and
            // the countdown. Without this a match could start with players already above zero.
            //
            // Once per GAME, not per turn: modes with several turns per round accumulate across
            // them deliberately, and wiping at every countdown would erase earlier turns.
            //
            // Runs on every peer (this is the ClientRpc, not the server-only branch above) so a
            // client's local mirror is cleared too - the server's replicated zero only fires
            // OnValueChanged when the value actually changes, so it cannot fix a client that
            // drifted on its own.
            ZeroStatsForGameStartOnce();

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

        // ---------------- Rematch vote ----------------

        /// <summary>
        /// Which clients have asked for a rematch on the current scoreboard.
        ///
        /// <para>
        /// Play Again is host-authoritative - one player's replay forces everyone into it - so a
        /// client's press cannot BE the restart. But hiding the button left a client with no way to
        /// say "again", which is the most common thing anybody wants to say at a scoreboard, and it
        /// put the host in the position of guessing. So a client's press is a VOTE: it is recorded
        /// here, announced to everyone as a toast, and the host decides.
        /// </para>
        /// </summary>
        readonly HashSet<ulong> _rematchVoters = new();

        /// <summary>How many players have asked for a rematch.</summary>
        public int RematchVoteCount => _rematchVoters.Count;

        /// <summary>Raised on every peer when the tally changes, so the host's button can react.</summary>
        public event System.Action<int, int> OnRematchVotesChanged;

        [ServerRpc(RequireOwnership = false)]
        internal void RequestRematch_ServerRpc(string playerName, int domain, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            // Keyed on the sender, so a player leaning on the button votes once.
            if (!_rematchVoters.Add(rpcParams.Receive.SenderClientId)) return;

            var nm = NetworkManager.Singleton;
            _rematchVoters.RemoveWhere(id => nm == null || !nm.ConnectedClientsIds.Contains(id));

            int humans = SpectatorSession.CountHumanClients(nm);
            AnnounceRematchVote_ClientRpc(playerName, domain, _rematchVoters.Count, humans);
        }

        [ClientRpc]
        void AnnounceRematchVote_ClientRpc(string playerName, int domain, int votes, int humans)
        {
            GameToastAPI.Post(GameToastSituation.RematchRequested, (Domains)domain,
                playerName, votes.ToString(), humans.ToString());
            OnRematchVotesChanged?.Invoke(votes, humans);
        }

        /// <summary>Clears the tally - a new game is not carrying the last one's votes.</summary>
        protected void ResetRematchVotes()
        {
            _rematchVoters.Clear();
            OnRematchVotesChanged?.Invoke(0, 0);
        }

        // ---------------- Ready gate (shared) ----------------

        /// <summary>
        /// WHICH clients have pressed Ready this turn, server-side.
        ///
        /// <para>
        /// This lives on the BASE because it was written twice - once in
        /// <c>MultiplayerDomainGamesController</c>, once in <c>CoOpWildlifeBlitzMiniGame</c> - and
        /// both copies carried the same two defects. Two copies of a rule is how the second one
        /// gets forgotten, and a third mode would have written a third.
        /// </para>
        ///
        /// <para>
        /// Defect 1: both kept a bare COUNT, so a double-press (a rebound tap, or a Ready button
        /// not yet hidden on a laggy client) satisfied the gate on behalf of somebody who had not
        /// pressed, and the match started without them. Keying on the sender makes a press
        /// idempotent.
        /// </para>
        ///
        /// <para>
        /// Defect 2, the expensive one: the gate was only ever evaluated INSIDE the press RPC,
        /// against a human count read live at that instant. So when a player left, dropped or
        /// crashed while the others were waiting on them, the comparison that would now pass was
        /// never run again. Three humans, two pressed, the third leaves - and the remaining two sit
        /// at the ready screen FOREVER: the match cannot start, nothing logs, nothing times out.
        /// A count is a snapshot of an answer; the ROSTER is the question, and it keeps changing -
        /// so the gate is re-decided whenever the roster does.
        /// </para>
        /// </summary>
        readonly HashSet<ulong> _readyClients = new();

        /// <summary>Records a Ready press. Idempotent per client.</summary>
        protected void MarkClientReady(ulong clientId)
        {
            if (!IsServer) return;
            _readyClients.Add(clientId);
        }

        /// <summary>Clears the gate - a new turn/round starts with nobody ready.</summary>
        protected void ResetReadyGate()
        {
            _readyClients.Clear();
        }

        /// <summary>
        /// Re-decides whether the turn can start, from the CURRENT roster. Called on every Ready
        /// press AND on every client disconnect. Calls <see cref="OnAllPlayersReady"/> exactly once
        /// per satisfied gate, then clears it.
        /// </summary>
        protected void EvaluateReadyGate(string because)
        {
            if (!IsServer) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            // Departed clients are pruned rather than trusted: the set is keyed on client id and a
            // stale entry would let the gate pass on behalf of somebody who is gone.
            _readyClients.RemoveWhere(id => !nm.ConnectedClientsIds.Contains(id));

            // Connected clients minus SPECTATORS: humans who own a Ready button (AI never connect,
            // viewers never press).
            int humanCount = SpectatorSession.CountHumanClients(nm);

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow,
                $"[FLOW-9] [{GetType().Name}] Ready gate ({because}): {_readyClients.Count}/{humanCount}");

            // humanCount can legitimately reach 0 - the last human left and only AI remain. Starting
            // a countdown for nobody is worse than holding, and this host is on its way out anyway.
            if (humanCount <= 0) return;

            if (_readyClients.Count < humanCount) return;

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow,
                $"[FLOW-9] [{GetType().Name}] All players ready - starting countdown.");
            _readyClients.Clear();
            OnAllPlayersReady();
        }

        /// <summary>What a mode does once every human has pressed Ready. Server-side.</summary>
        protected virtual void OnAllPlayersReady() { }

        void HandleClientDisconnectedForReadyGate(ulong clientId)
        {
            if (!IsServer) return;

            // Netcode fires this BEFORE the id leaves ConnectedClientsIds on some paths, so drop it
            // here as well as in the prune - the gate must never count a departed player's vote.
            _readyClients.Remove(clientId);
            EvaluateReadyGate($"client {clientId} disconnected");
        }

        // ---------------- Reset / Replay Logic ----------------

        protected override void OnResetForReplay()
        {
            // A replay is a new GAME: re-arm the game-start zeroing so the next countdown
            // clears whatever the finished match left behind.
            RearmGameStartStatsReset();
        }

        /// <summary>
        /// Entry point for Scoreboard / PauseMenu "Play Again" button.
        /// Only the host can trigger a replay - all clients are forced to follow.
        /// </summary>
        public override void RequestReplay()
        {
            if (!IsServer)
            {
                CSDebug.LogWarning("[MultiplayerController] RequestReplay ignored - only the host can restart the game.");
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
                // Despawn players before vessels - same order as SceneLoader.ClearPlayerVesselReferences.
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

                // Server-authoritative scene reload - all clients follow automatically
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer && nm.SceneManager != null)
                {
                    CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[MultiplayerController] Scene reload replay - loading {gameData.SceneName}");
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

        /// <summary>
        /// Covers every peer's screen with the opaque scene-transition splash before the
        /// host tears the session down for a return to Menu_Main. Called by
        /// SceneLoader.ReturnToMainMenu ahead of the vessel/AI despawns and the Netcode
        /// scene switch - RPCs and despawn messages share the reliable channel, so every
        /// client is covered before anything visibly disappears. The host's screen is
        /// already covered by SceneLoader directly (the RPC also lands on the host, where
        /// the repeat SetFadeImmediate is a no-op). The replay path's equivalent is
        /// PrepareForSceneReload_ClientRpc.
        /// </summary>
        public void BroadcastReturnToMenuVeil()
        {
            if (!IsServer || !IsSpawned) return;
            ShowReturnToMenuVeil_ClientRpc();
        }

        [ClientRpc]
        void ShowReturnToMenuVeil_ClientRpc()
        {
            _sceneTransitionManager?.SetFadeImmediate(1f);

            // This RPC is where a REAL client's screen goes black, and until now nothing watched
            // what happened next: the veil goes fully opaque here and only lifts when Menu_Main
            // finishes loading. If the host's networked scene load never completes for this client,
            // the veil stays up with no timeout, no error and no way out but killing the game.
            //
            // SceneLoader's own defer guards cannot cover this. They are reached through SOAP
            // events, and a SOAP raise is local - so on separate machines a client never runs
            // ReturnToMainMenu at all; those guards only fire for MPPM virtual players sharing one
            // GameDataSO in one process. This is the call site that protects a shipped build.
            //
            // The host is excluded because it DRIVES the load - it cannot be waiting on itself, and
            // bouncing it would tear down the party it is trying to move.
            if (!IsServer)
                _sceneLoader?.ArmClientMenuReturnWatchdog("Return to menu (host-driven)");
        }

        private void FadeFromBlackOnReplay()
        {
            gameData.OnClientReady.OnRaised -= FadeFromBlackOnReplay;

            // Play Again reloads bypass SceneLoader.LoadSceneAsync entirely, so
            // neither host nor clients would ever take the scheduled scene-change
            // GC on repeated replays. This runs on every peer with the overlay
            // still opaque and the reloaded scene up — the covered moment to take
            // the full collect and reset the mid-gameplay collection clock.
            GC.Collect();

            _sceneTransitionManager?.FadeFromBlack().Forget();
        }

        [ClientRpc]
        void ResetForReplay_ClientRpc()
        {
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, "[MultiplayerController] Resetting environment for replay");
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
        /// A client is here and wants the config. Answered directly to the caller rather than
        /// re-broadcast, so a late joiner cannot re-run every other client's LoadInsights header.
        /// Idempotent by construction: the payload is the host's live GameDataSO, and applying it
        /// twice writes the same values.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        void RequestGameConfig_ServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
                }
            };

            SyncGameConfigToClients_ClientRpc(
                gameData.SceneName,
                (int)gameData.GameMode,
                gameData.IsMultiplayerMode,
                (int)gameData.selectedVesselClass.Value,
                gameData.SelectedIntensity.Value,
                gameData.SelectedPlayerCount.Value,
                gameData.RequestedAIBackfillCount,
                gameData.RequestedDomainCount,
                gameData.IsMaelstromMode,
                gameData.ComebackRatePerScoreDeficit,
                gameData.MatchId,
                gameData.PartyId,
                gameData.InviteTriggered,
                target
            );
        }

        /// <summary>
        /// Syncs the host's game configuration to all clients in the game scene.
        /// Called by OnNetworkSpawn on the server so clients have correct GameDataSO
        /// values (intensity, player count, AI backfill, etc.) before initialization.
        /// </summary>
        [ClientRpc]
        void SyncGameConfigToClients_ClientRpc(
            string sceneName, int gameMode, bool isMultiplayer,
            int vesselClass, int intensity, int playerCount, int aiBackfillCount,
            int domainCount, bool isMaelstrom, float comebackRate,
            string matchId, string partyId, bool inviteTriggered,
            ClientRpcParams rpcParams = default)
        {
            if (IsServer) return;

            // Match envelope: echoed verbatim, never recomputed. Every client must emit the
            // SAME identifiers on game_started or the analytics GROUP BY fragments.
            gameData.MatchId = matchId;
            gameData.PartyId = partyId;
            gameData.InviteTriggered = inviteTriggered;

            gameData.SceneName = sceneName;
            gameData.GameMode = (GameModes)gameMode;
            gameData.IsMultiplayerMode = isMultiplayer;
            gameData.selectedVesselClass.Value = (VesselClassType)vesselClass;
            gameData.SelectedIntensity.Value = intensity;
            gameData.SelectedPlayerCount.Value = playerCount;
            gameData.RequestedAIBackfillCount = aiBackfillCount;
            gameData.RequestedDomainCount = domainCount;
            gameData.IsMaelstromMode = isMaelstrom;
            gameData.ComebackRatePerScoreDeficit = comebackRate;

            // Clients began recording before these values replicated — refresh the report header
            // with the authoritative config now that it has arrived.
            LoadInsights.Mark("Game config received from server");
            LoadInsights.SetGameContext(
                sceneName, ((GameModes)gameMode).ToString(), intensity, playerCount,
                Mathf.Max(0, playerCount - aiBackfillCount), aiBackfillCount, isMultiplayer);

            // LAST: everything above is now authoritative on this client. Cell.AssignConfig
            // refuses to make its sticky IntensityWise choice until this is true, because the
            // intensity it reads arrives in this very RPC and a cell that latched before it would
            // build a different arena than the host for the whole match.
            gameData.GameConfigSynced = true;
        }
    }
}