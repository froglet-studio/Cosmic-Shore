using System;
using System.Threading;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private float connectionTimeoutSeconds = 10f;

        [Tooltip("Max time (seconds) to wait for the menu scene to load after connecting.")]
        [SerializeField] private float sceneLoadTimeoutSeconds = 15f;

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
        /// Full accept flow:
        ///   1. Clean up current game state (despawn vessels, reset data)
        ///   2. Shutdown local NetworkManager host
        ///   3. Join the inviter's party session via UGS (Relay transport auto-configures)
        ///   4. Wait for Netcode client connection
        ///   5. Host syncs Menu_Main scene and spawns our vessel via MenuServerPlayerVesselInitializer
        ///   6. SOAP events update Party Area UI on all clients
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

            try
            {
                Debug.Log("[PartyInviteController] Starting accept flow...");

                // Pause refresh loop to avoid UGS rate limit conflicts
                HostConnectionService.Instance?.SetRefreshPaused(true);

                // Step 1: Clean up current game state
                CleanUpCurrentSession();

                // Step 2: Shutdown local NetworkManager
                await ShutdownNetworkManagerAsync(ct);

                // Step 3: Join the inviter's party session via HostConnectionService
                // JoinSessionByIdAsync with Relay auto-configures transport and starts client
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

                // Step 4: Wait for Netcode client connection
                await WaitForClientConnectionAsync(ct);
                Debug.Log("[PartyInviteController] Netcode client connected.");

                // Step 5: Wait for scene sync (host loads Menu_Main for us)
                await WaitForSceneLoadAsync(ct);
                Debug.Log("[PartyInviteController] Menu scene loaded.");

                // Step 6: Signal completion — SOAP events from the spawn chain
                // (OnPlayerNetworkSpawnedUlong, OnClientReady) handle the rest automatically.
                // MenuServerPlayerVesselInitializer on host spawns our vessel in autopilot.
                connectionData.OnPartyJoinCompleted?.Raise();

                Debug.Log("[PartyInviteController] Accept flow completed successfully.");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[PartyInviteController] Accept flow cancelled.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyInviteController] Accept flow failed: {e.Message}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
                HostConnectionService.Instance?.SetRefreshPaused(false);
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

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Host-side Transition (for sending first invite)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions the local host from a local-only NetworkManager session
        /// to a Relay-based party session so remote clients can connect.
        ///
        /// Called before the first invite is sent. Flow:
        ///   1. Clean up current menu vessels
        ///   2. Shutdown local NetworkManager
        ///   3. Create party session with Relay (starts host on Relay)
        ///   4. Reload Menu_Main as network scene
        ///   5. Re-initialize menu autopilot via existing spawn chain
        /// </summary>
        public async UniTask TransitionToPartyHostAsync()
        {
            if (_transitioning)
            {
                Debug.LogWarning("[PartyInviteController] Already transitioning — ignoring host transition.");
                return;
            }

            if (HostConnectionService.Instance == null)
            {
                Debug.LogError("[PartyInviteController] HostConnectionService not available.");
                return;
            }

            // If a party session already exists, no transition needed
            if (HostConnectionService.Instance.PartySession != null)
                return;

            _transitioning = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                Debug.Log("[PartyInviteController] Starting host transition to Relay...");

                // Pause refresh loop to avoid UGS rate limit conflicts
                HostConnectionService.Instance.SetRefreshPaused(true);

                // Step 1: Clean up current menu vessels
                CleanUpCurrentSession();

                // Step 2: Shutdown local NetworkManager
                await ShutdownNetworkManagerAsync(ct);

                // Step 3: Create party session with Relay (handled by HostConnectionService)
                // This allocates Relay and starts NetworkManager as host
                await HostConnectionService.Instance.CreatePartySessionPublicAsync();
                Debug.Log("[PartyInviteController] Party session created on Relay.");

                // Step 4: Load Menu_Main as network scene
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer && nm.SceneManager != null)
                {
                    nm.SceneManager.LoadScene("Menu_Main",
                        UnityEngine.SceneManagement.LoadSceneMode.Single);
                }

                // Step 5: Wait for scene to load and spawn chain to complete
                await WaitForSceneLoadAsync(ct);
                Debug.Log("[PartyInviteController] Host transition completed.");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[PartyInviteController] Host transition cancelled.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyInviteController] Host transition failed: {e.Message}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
                HostConnectionService.Instance?.SetRefreshPaused(false);
                _transitioning = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal: Cleanup & Shutdown
        // ─────────────────────────────────────────────────────────────────────

        private void CleanUpCurrentSession()
        {
            if (gameData == null) return;

            gameData.DestroyPlayerAndVessel();
            gameData.ResetRuntimeData();
        }

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
            await UniTask.Delay(200, cancellationToken: ct);
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
                throw new TimeoutException(
                    $"[PartyInviteController] Client connection timed out after {connectionTimeoutSeconds}s.");
            }
        }

        private async UniTask WaitForSceneLoadAsync(CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(sceneLoadTimeoutSeconds));

            try
            {
                // Wait for the active scene to be Menu_Main (synced from host)
                await UniTask.WaitUntil(
                    () =>
                    {
                        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                        return activeScene.name == "Menu_Main" && activeScene.isLoaded;
                    },
                    cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning("[PartyInviteController] Scene load timed out — proceeding anyway.");
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
            Debug.Log("[PartyInviteController] Attempting recovery — restarting local host...");

            try
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && !nm.IsListening)
                {
                    nm.StartHost();
                    await UniTask.Delay(500);
                }

                // Return to Menu_Main if not already there
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.name != "Menu_Main")
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Menu_Main");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyInviteController] Recovery failed: {e.Message}");
            }
        }
    }
}
