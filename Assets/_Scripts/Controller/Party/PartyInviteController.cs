using System;
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
        [Tooltip("Max time (seconds) to wait for NetworkManager shutdown.")]
        [SerializeField] private float shutdownTimeoutSeconds = 5f;

        [Tooltip("Max time (seconds) to wait for client connection after joining party session.")]
        [SerializeField] private float connectionTimeoutSeconds = 30f;

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
        /// Direct-join accept flow — no scene reload:
        ///   1. Shutdown local NetworkManager host (UGS SDK will start a fresh Relay client)
        ///   2. Join the inviter's party session via UGS (Relay transport auto-configures)
        ///   3. Wait for Netcode client connection (best-effort)
        ///   4. SOAP events update Party Area UI on all clients
        ///
        /// The host's Menu_Main is NOT reloaded via Netcode scene management, so the
        /// client stays on its local Menu_Main. Existing NetworkObjects replicate via
        /// the regular connection-approval spawn sync. A dedicated UI panel can be
        /// layered over this flow later for richer transition UX.
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
                // because a host is already listening. We intentionally do NOT
                // destroy the player/vessel or reset runtime data — the user
                // stays in the menu and simply swaps transport.
                await ShutdownNetworkManagerAsync(ct);

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

                // Step 3: Wait for Netcode client connection. This does NOT wait
                // for any scene reload — the client stays on the Menu_Main it's
                // already running on. Spawn sync for existing NetworkObjects
                // happens automatically on connection approval.
                await WaitForClientConnectionAsync(ct);
                Debug.Log("[PartyInviteController] Netcode client connected.");

                // Step 4: Signal completion — SOAP events from the spawn chain
                // (OnPlayerNetworkSpawnedUlong, OnClientReady) handle the rest automatically.
                connectionData.OnPartyJoinCompleted?.Raise();

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

            // Brief settle delay for transport cleanup
            await UniTask.Delay(200, DelayType.UnscaledDeltaTime, cancellationToken: ct);
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
