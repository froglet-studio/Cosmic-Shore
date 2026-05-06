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
    /// Thin orchestrator for party invite transitions.  Sequences the Netcode
    /// host→client handoff when a player accepts an invite, and the local-host
    /// restart on leave or failed transition.
    ///
    /// <para>
    /// NM lifecycle mechanics (shutdown, wait-for-connect, wait-for-scene-sync)
    /// live in <see cref="NetworkTransitionService"/>, injected via Reflex DI.
    /// This class owns only the accept/decline/leave orchestration and the
    /// <see cref="_transitioning"/> guard (test-reflected — must stay here).
    /// </para>
    ///
    /// Place on the same persistent GameObject as <see cref="HostConnectionService"/>.
    /// Lifetime: DontDestroyOnLoad MonoBehaviour.
    /// Thread-safety: main-thread only.
    /// </summary>
    public class PartyInviteController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector / Injected fields
        // ─────────────────────────────────────────────────────────────────────

        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Timing")]
        [Tooltip("Max time (seconds) to wait for NetworkManager shutdown.")]
        [SerializeField] private float shutdownTimeoutSeconds = 2f;

        [Tooltip("Max time (seconds) to wait for client connection after joining party session.")]
        [SerializeField] private float connectionTimeoutSeconds = 8f;

        [Tooltip("Max seconds to wait for Netcode's automatic Menu_Main reload after joining.")]
        [SerializeField] private float sceneSyncTimeoutSeconds = 5f;

        [Inject] private GameDataSO gameData;

        // ─────────────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────────────

        private CancellationTokenSource _cts;

        // _transitioning is reflected by tests — field name must not change.
        private bool _transitioning;

        /// <summary>
        /// True while a host-to-client transition is in progress.
        /// UI should disable invite buttons during this time.
        /// </summary>
        public bool IsTransitioning => _transitioning;

        // ─────────────────────────────────────────────────────────────────────
        // Services (injected via Reflex DI)
        // ─────────────────────────────────────────────────────────────────────

        [Inject] private INetworkTransitionService _networkTransition;

        // ─────────────────────────────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────────────────────────────

        public static PartyInviteController Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // [Inject] fields (_networkTransition, gameData) are populated by Reflex
            // between Awake and Start — do not access them here.
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
        ///   1.  Shutdown local NetworkManager host.
        ///   1b. Clear stale SOAP refs the shutdown left behind.
        ///   2.  Join the inviter's party session via UGS (Relay transport auto-configures).
        ///   3.  Wait for Netcode client connection.
        ///   3b. Wait for Netcode's automatic Menu_Main reload to complete.
        ///   4.  Raise OnPartyJoinCompleted so Party Area UI refreshes.
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

                // Step 1: Shutdown the local NetworkManager.
                await _networkTransition.ShutdownAsync(shutdownTimeoutSeconds, ct);

                // Step 1b: Clear stale SOAP references the NM shutdown left behind.
                // Player.OnNetworkDespawn removes from gameData.Players but leaves
                // LocalPlayer and Vessels pointing at destroyed objects.
                _networkTransition.ClearStaleReferences();

                // Step 2: Join the inviter's party session via HostConnectionService.
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
                await _networkTransition.WaitForClientConnectionAsync(connectionTimeoutSeconds, ct);
                Debug.Log("[PartyInviteController] Netcode client connected.");

                // Step 3b: Wait for Netcode's automatic Menu_Main reload.
                Debug.Log("[PartyInviteController] Awaiting client scene-sync...");
                await _networkTransition.WaitForSceneSyncAsync("Menu_Main", sceneSyncTimeoutSeconds, ct);

                // Step 4: Signal completion.  Isolated try/catch so a listener
                // throwing during scene-reload teardown can't roll back the outer
                // flow into the error path — the accept itself has already succeeded.
                try
                {
                    connectionData.OnPartyJoinCompleted.Raise();
                    HostConnectionService.Instance?.ForceRefreshNow();
                }
                catch (Exception postEx)
                {
                    Debug.LogWarning(
                        $"[PartyInviteController] Post-accept signal failed " +
                        $"({postEx.GetType().Name}): {postEx.Message} — " +
                        "accept already succeeded, continuing.");
                }

                Debug.Log("[PartyInviteController] Accept flow completed successfully.");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[PartyInviteController] Accept flow cancelled.");
            }
            catch (Exception e)
            {
                // Ensure main thread — timeout continuations can land on the thread pool.
                // Yield one frame to move past any in-flight scene-load tick.
                await UniTask.SwitchToMainThread();
                await UniTask.Yield();
                Debug.LogError($"[PartyInviteController] Accept flow failed " +
                               $"({e.GetType().Name}): {e}");
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
        /// accept new invites.
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

                if (gameData != null)
                {
                    gameData.DestroyPlayerAndVessel();
                    gameData.ResetRuntimeData();
                }
                await _networkTransition.ShutdownAsync(shutdownTimeoutSeconds, ct);

                HostConnectionService.Instance?.ClearStalePartySession();

                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name != "Menu_Main")
                    SceneManager.LoadScene("Menu_Main");

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
                await UniTask.Yield();
                Debug.LogError($"[PartyInviteController] Leave-lobby flow failed " +
                               $"({e.GetType().Name}): {e}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
                _transitioning = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Host-side Transition
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Previously transitioned the local host from local-only to Relay-based.
        /// Now a no-op: the Relay-backed party session is created at startup by
        /// <see cref="HostConnectionService"/>, so no transition is needed.
        /// Kept for API compatibility.
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
        // Internal: Error Recovery
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Restarts the local NetworkManager host so the user returns to a functional
        /// menu state after a failed transition.
        /// </summary>
        private async UniTask RecoverFromFailedTransitionAsync()
        {
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

                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name != "Menu_Main")
                    SceneManager.LoadScene("Menu_Main");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyInviteController] Recovery failed: {e.Message}");
            }
        }
    }
}
