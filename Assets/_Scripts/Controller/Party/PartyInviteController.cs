using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [Inject] private SceneTransitionManager _sceneTransitionManager;
        [Inject] private SceneNameListSO _sceneNames;

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
        /// Transitions from the local host (started by <see cref="MultiplayerSetup"/>)
        /// to a Relay-backed party session so remote players can join via invite.
        ///
        /// Flow: fade to black → despawn vessels → shutdown local host →
        /// create Relay party session → reload Menu_Main via Netcode.
        /// The fade screen covers the entire transition so the user never sees
        /// Menu_Main loading twice.
        /// </summary>
        public async UniTask TransitionToPartyHostAsync()
        {
            if (HostConnectionService.Instance?.PartySession != null)
            {
                Debug.Log("[PartyInviteController] Party session already active — no transition needed.");
                return;
            }

            if (_transitioning)
            {
                Debug.LogWarning("[PartyInviteController] Already transitioning — ignoring.");
                return;
            }

            _transitioning = true;
            Debug.Log("[PartyInviteController] Transitioning from local host to Relay...");

            try
            {
                // Fade to black so the user doesn't see the scene reload
                if (_sceneTransitionManager != null)
                    await _sceneTransitionManager.FadeToBlack();

                // 1. Despawn menu vessels and reset game data
                CleanUpCurrentSession();

                // 2. Shutdown the local host
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(shutdownTimeoutSeconds + 15));
                var ct = cts.Token;

                await ShutdownNetworkManagerAsync(ct);

                // 3. Create Relay-backed party session (internally starts Relay host)
                if (HostConnectionService.Instance != null)
                    await HostConnectionService.Instance.CreatePartySessionPublicAsync();

                // 4. Reload Menu_Main via Netcode scene management
                var nm = NetworkManager.Singleton;
                string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";

                if (nm != null && nm.IsListening)
                {
                    Debug.Log($"[PartyInviteController] Loading {menuScene} via Netcode (Relay host)...");
                    nm.SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
                }
                else
                {
                    Debug.LogError("[PartyInviteController] Relay host not running after transition. Falling back to direct load.");
                    SceneManager.LoadScene(menuScene);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyInviteController] Relay transition failed: {e.Message}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
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
        /// If a transition fails, restart a local host so the user returns to
        /// a functional menu state. Relay can be re-attempted on next invite.
        /// </summary>
        private async UniTask RecoverFromFailedTransitionAsync()
        {
            Debug.Log("[PartyInviteController] Attempting recovery — restarting local host...");

            try
            {
                var nm = NetworkManager.Singleton;

                // Start a local host if nothing is running
                if (nm != null && !nm.IsListening)
                {
                    nm.StartHost();
                    await UniTask.Delay(200);
                }

                // Return to Menu_Main if not already there
                string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
                var activeScene = SceneManager.GetActiveScene();

                if (activeScene.name != menuScene)
                {
                    nm = NetworkManager.Singleton;
                    if (nm != null && nm.IsListening && nm.SceneManager != null)
                    {
                        nm.SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
                    }
                    else
                    {
                        SceneManager.LoadScene(menuScene);
                    }
                }

                // Fade from black so the menu is visible again
                if (_sceneTransitionManager != null)
                    await _sceneTransitionManager.FadeFromBlack();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyInviteController] Recovery failed: {e.Message}");
            }
        }
    }
}
