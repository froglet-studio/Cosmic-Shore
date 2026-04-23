using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Orchestrates the full party invite flow including Netcode host-to-client
    /// transitions when accepting an invite, and local-host-to-Relay-host
    /// transitions when sending the first invite.
    ///
    /// Coordinates between <see cref="HostConnectionService"/> (UGS sessions)
    /// and <see cref="NetworkManager"/> (Netcode transport) to ensure clean
    /// handoffs without transport conflicts.
    ///
    /// Place on the same persistent GameObject as <see cref="HostConnectionService"/>.
    /// </summary>
    public class PartyInviteController : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Timing")]
        [Tooltip("Max time (seconds) to wait for NetworkManager shutdown. Netcode typically " +
                 "settles in <500ms; a long ceiling only mattered for rare edge cases where " +
                 "the transport hung, and those should fail fast rather than stall the accept flow.")]
        [SerializeField] private float shutdownTimeoutSeconds = 2f;

        [Tooltip("Max time (seconds) to wait for client connection after joining party session. " +
                 "Relay handshake + Netcode client connect is sub-second in practice; the old " +
                 "30s ceiling was effectively infinite from a user-perception standpoint.")]
        [SerializeField] private float connectionTimeoutSeconds = 8f;

        [Tooltip("Max seconds to wait for Netcode's automatic Menu_Main reload after joining " +
                 "the host's party session. The host loaded Menu_Main via nm.SceneManager.LoadScene, " +
                 "so clients get an automatic reload because scene handles don't match. Typically " +
                 "completes in 1-2s; the timeout is a fail-soft floor if no reload is triggered.")]
        [SerializeField] private float sceneSyncTimeoutSeconds = 5f;

        [Inject] private GameDataSO gameData;

        private CancellationTokenSource _cts;
        private bool _transitioning;

        public static PartyInviteController Instance { get; private set; }

        /// <summary>
        /// True while a host-to-client transition is in progress.
        /// UI should disable invite buttons during this time.
        /// </summary>
        public bool IsTransitioning => _transitioning;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (Instance == this)
                Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Accept Invite (Recipient Side)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Accept-invite flow:
        ///   1.  Shutdown local NetworkManager host (UGS SDK will start a fresh Relay client)
        ///   1b. Clear stale SOAP refs the shutdown left behind (LocalPlayer, Vessels, Players)
        ///   2.  Join the inviter's party session via UGS (Relay transport auto-configures)
        ///   3.  Wait for Netcode client connection
        ///   3b. Wait for Netcode's automatic Menu_Main reload to complete
        ///   4.  Raise OnPartyJoinCompleted so Party Area UI refreshes
        ///
        /// The host's Menu_Main is a networked scene (loaded via nm.SceneManager.LoadScene
        /// in AuthenticationSceneController). When the client connects via Relay, Netcode
        /// auto-reloads Menu_Main on the client because scene handles differ. We wait for
        /// that reload to complete before raising OnPartyJoinCompleted so UI consumers
        /// observe the post-reload state, not a mid-reload snapshot.
        /// </summary>
        public async UniTask AcceptInviteAsync(PartyInviteData invite)
        {
            if (_transitioning)
            {
                Debug.LogWarning("[PartyInviteController] Already transitioning — ignoring duplicate accept.");
                return;
            }

            _transitioning = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // Unpause immediately — ScreenSwitcher pauses on non-HOME screens,
            // and the accept flow needs Update() ticking so the UGS SDK's
            // internal lobby state stays synchronized with WebSocket deltas.
            // Without this, LobbyPatcher crashes with ArgumentOutOfRangeException.
            PauseSystem.TogglePauseGame(false);

            try
            {
                Debug.Log("[PartyInviteController] Starting direct-join accept flow...");

                // Step 1: Shutdown the local NetworkManager so the UGS SDK can
                // start a fresh Relay client. Without this, StartClient fails
                // because a host is already listening. Destroying gameplay
                // runtime data is deferred to step 1b below.
                await ShutdownNetworkManagerAsync(ct);

                // Step 1b: Clear stale SOAP references the NM shutdown left behind.
                // Player.OnNetworkDespawn removes from gameData.Players but leaves
                // gameData.LocalPlayer and gameData.Vessels pointing at destroyed
                // objects. Without this, ClientPlayerVesselInitializer.ReRegisterPersistentPlayers()
                // and AddPlayer() race against ghosts after the upcoming Netcode
                // Menu_Main reload (see step 3b). See commit b74a311c for the
                // history of why a narrower reset is preferred here.
                if (gameData != null)
                {
                    gameData.ResetRuntimeDataForPartyJoin();
                    Debug.Log("[PartyInviteController] Cleared stale runtime refs for party join.");
                }

                // Step 2: Join the inviter's party session via HostConnectionService.
                // JoinSessionByIdAsync with Relay auto-configures transport and starts client.
                if (HostConnectionService.Instance == null)
                {
                    Debug.LogError("[PartyInviteController] HostConnectionService not available.");
                    return;
                }

                await HostConnectionService.Instance.AcceptInviteAsync(invite);

                // Store the party session so MultiplayerSetup in the game scene
                // knows to reuse the existing Relay connection (client side).
                if (gameData != null && HostConnectionService.Instance.PartySession != null)
                    gameData.ActiveSession = HostConnectionService.Instance.PartySession;

                Debug.Log("[PartyInviteController] Joined party session via UGS.");

                // Step 3: Wait for Netcode client connection.
                await WaitForClientConnectionAsync(ct);
                Debug.Log("[PartyInviteController] Netcode client connected.");

                // Step 3b: Wait for Netcode's automatic Menu_Main reload before
                // raising OnPartyJoinCompleted. The host's Menu_Main is a networked
                // scene (loaded via nm.SceneManager.LoadScene in AuthenticationSceneController),
                // so the client's differing scene handle triggers a reload under
                // ClientSynchronizationMode=Single. Raising OnPartyJoinCompleted
                // before the reload completes races InitializeAllPlayersAndVessels_ClientRpc
                // against a mid-reload ClientPlayerVesselInitializer.
                Debug.Log("[PartyInviteController] Awaiting client scene-sync...");
                await WaitForClientSceneSyncAsync(ct);

                // Step 4: Signal completion — SOAP events from the spawn chain
                // (OnPlayerNetworkSpawnedUlong, OnClientReady) handle the rest automatically.
                connectionData.OnPartyJoinCompleted.Raise();

                // Kick the presence/party refresh loop immediately so the arcade
                // lobby list on the joining client populates with the host and any
                // existing remote members inside one tick instead of waiting for
                // the next scheduled poll.
                HostConnectionService.Instance?.ForceRefreshNow();

                Debug.Log("[PartyInviteController] Accept flow completed successfully.");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[PartyInviteController] Accept flow cancelled.");
            }
            catch (Exception e)
            {
                // Ensure main thread — timeout continuations can land on the thread pool.
                await UniTask.SwitchToMainThread();
                Debug.LogError($"[PartyInviteController] Accept flow failed: {e.Message}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
                _transitioning = false;
            }
        }

        /// <summary>
        /// Decline the pending invite. Dismisses the popup and clears the invite.
        /// </summary>
        public async UniTask DeclineInviteAsync()
        {
            if (HostConnectionService.Instance != null)
                await HostConnectionService.Instance.DeclineInviteAsync();
        }

        /// <summary>
        /// Client-side "Leave Lobby": disconnects from the host's party session and
        /// returns to Menu_Main, then restarts a local host so the player can send or
        /// accept new invites. Intended for non-host clients pressing "Leave Lobby"
        /// on the end-game scoreboard.
        /// </summary>
        public async UniTask LeavePartyAndReturnToMenuAsync()
        {
            if (_transitioning)
            {
                Debug.LogWarning("[PartyInviteController] Already transitioning — ignoring leave lobby.");
                return;
            }

            _transitioning = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            PauseSystem.TogglePauseGame(false);

            try
            {
                Debug.Log("[PartyInviteController] Starting leave-lobby flow...");

                // Despawn any leftover game-state before swapping transports.
                // This path is hit from the end-game scoreboard, so tearing
                // down the player/vessel is correct — unlike AcceptInvite,
                // which stays in the menu and keeps them intact.
                if (gameData != null)
                {
                    gameData.DestroyPlayerAndVessel();
                    gameData.ResetRuntimeData();
                }
                await ShutdownNetworkManagerAsync(ct);

                // Clear the stale party session reference so HostConnectionService
                // can create a fresh Relay-backed session next time.
                HostConnectionService.Instance?.ClearStalePartySession();

                // Load Menu_Main locally (no Netcode scene management — we've disconnected).
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name != "Menu_Main")
                    SceneManager.LoadScene("Menu_Main");

                // Restart local host so the player can invite / be invited again.
                var nm = NetworkManager.Singleton;
                if (nm != null && !nm.IsListening)
                {
                    nm.StartHost();
                    await UniTask.Delay(500, DelayType.UnscaledDeltaTime, cancellationToken: ct);
                }

                Debug.Log("[PartyInviteController] Leave-lobby flow completed.");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[PartyInviteController] Leave-lobby flow cancelled.");
            }
            catch (Exception e)
            {
                await UniTask.SwitchToMainThread();
                Debug.LogError($"[PartyInviteController] Leave-lobby flow failed: {e.Message}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
                _transitioning = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Host-side Transition (for sending first invite)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Previously transitioned the local host from local-only to Relay-based.
        /// Now a no-op: the Relay-backed party session is created at startup by
        /// <see cref="HostConnectionService"/>, so no transition is needed.
        /// Kept for API compatibility; callers have been updated to not call this.
        /// </summary>
        public UniTask TransitionToPartyHostAsync()
        {
            if (HostConnectionService.Instance?.PartySession != null)
            {
                Debug.Log("[PartyInviteController] Party session already active — no transition needed.");
                return UniTask.CompletedTask;
            }

            Debug.LogWarning("[PartyInviteController] No party session at invite time — invites may fail.");
            return UniTask.CompletedTask;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal: Cleanup & Shutdown
        // ─────────────────────────────────────────────────────────────────────

        private async UniTask ShutdownNetworkManagerAsync(CancellationToken ct)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                Debug.Log("[PartyInviteController] NetworkManager not running — skipping shutdown.");
                return;
            }

            Debug.Log("[PartyInviteController] Shutting down NetworkManager...");
            nm.Shutdown();

            // Wait for clean shutdown with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(shutdownTimeoutSeconds));

            try
            {
                await UniTask.WaitUntil(
                    () => nm == null || !nm.IsListening,
                    cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning("[PartyInviteController] NetworkManager shutdown timed out — forcing.");
            }

            // Brief settle delay for transport cleanup. Transport cleanup is
            // effectively instant once NetworkManager.IsListening flips false;
            // we only need enough time for any queued send buffers to drain
            // before we open a new Relay client on top.
            await UniTask.Delay(50, DelayType.UnscaledDeltaTime, cancellationToken: ct);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal: Connection & Scene Waiting
        // ─────────────────────────────────────────────────────────────────────

        private async UniTask WaitForClientConnectionAsync(CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(connectionTimeoutSeconds));

            try
            {
                await UniTask.WaitUntil(
                    () =>
                    {
                        var nm = NetworkManager.Singleton;
                        return nm != null && nm.IsConnectedClient;
                    },
                    cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning(
                    $"[PartyInviteController] Client connection not confirmed after {connectionTimeoutSeconds}s — proceeding anyway.");
            }
        }

        /// <summary>
        /// Waits for the first Single-mode Netcode scene-load event after the
        /// client connects. This is the host-driven Menu_Main reload that happens
        /// automatically because the client's scene handle differs from the host's
        /// (ClientSynchronizationMode = Single). Raising OnPartyJoinCompleted
        /// before this completes races InitializeAllPlayersAndVessels_ClientRpc
        /// against a mid-reload ClientPlayerVesselInitializer.
        ///
        /// Uses OnLoadEventCompleted (project convention, see NetworkObjectSpawner).
        /// Fail-soft: if nothing fires within <see cref="sceneSyncTimeoutSeconds"/>,
        /// log a warning and continue — the spawn chain may have completed without
        /// a reload for edge cases where Netcode decides scenes match.
        /// </summary>
        private async UniTask WaitForClientSceneSyncAsync(CancellationToken ct)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null)
            {
                Debug.LogWarning("[PartyInviteController] No SceneManager — skipping scene-sync wait.");
                return;
            }

            var tcs = new UniTaskCompletionSource<string>();
            void Handler(string sceneName, LoadSceneMode mode,
                         List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
            {
                if (mode == LoadSceneMode.Single) tcs.TrySetResult(sceneName);
            }
            nm.SceneManager.OnLoadEventCompleted += Handler;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(sceneSyncTimeoutSeconds));

            try
            {
                var sceneName = await tcs.Task.AttachExternalCancellation(timeoutCts.Token);
                Debug.Log($"[PartyInviteController] Client scene-sync completed: {sceneName}");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning(
                    $"[PartyInviteController] Scene-sync not observed in {sceneSyncTimeoutSeconds}s — " +
                    "proceeding (host may not have triggered a scene load).");
            }
            finally
            {
                var nmNow = NetworkManager.Singleton;
                if (nmNow != null && nmNow.SceneManager != null)
                    nmNow.SceneManager.OnLoadEventCompleted -= Handler;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal: Error Recovery
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// If the transition fails, restart the local NetworkManager host so the
        /// user returns to a functional menu state.
        /// </summary>
        private async UniTask RecoverFromFailedTransitionAsync()
        {
            // Ensure we're on the main thread — this method may be called from
            // a catch block whose continuation ran on the thread pool (e.g. when
            // a CancellationTokenSource timer fires AttachExternalCancellation).
            await UniTask.SwitchToMainThread();

            Debug.Log("[PartyInviteController] Attempting recovery — restarting local host...");

            try
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && !nm.IsListening)
                {
                    nm.StartHost();
                    await UniTask.Delay(500, DelayType.UnscaledDeltaTime);
                }

                // Return to Menu_Main if not already there
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name != "Menu_Main")
                {
                    SceneManager.LoadScene("Menu_Main");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyInviteController] Recovery failed: {e.Message}");
            }
        }
    }
}
