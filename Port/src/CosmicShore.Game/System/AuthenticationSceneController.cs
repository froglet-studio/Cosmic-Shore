// Ported verbatim from Assets/_Scripts/System/AuthenticationSceneController.cs
// (bootstrap arc 2026-07-08). Mechanical substitutions (README):
// Cysharp.Threading.Tasks → System.Threading.Tasks + CosmicShore.Engine.Tasks
// (UniTaskVoid/UniTask → Task + .Forget(); UniTask.WhenAny(task, UniTask.Delay(...))
// → Task.WhenAny over an eagerly-bridged GameTask.Delay arm with the winner index
// by reference compare — the LeavePartyAsync precedent; UniTask.Delay(TimeSpan,
// ignoreTimeScale: true, ct) → GameTask.Delay(seconds, unscaledTime: true, ct);
// UniTask.WaitUntil → GameTask.WaitUntil; task.AsUniTask().AttachExternalCancellation(ct)
// → task.WaitAsync(ct); UniTaskCompletionSource → TaskCompletionSource;
// tcs.Task.AttachExternalCancellation(ct) → tcs.Task.WaitAsync(ct); the retired
// `.AsMainThread()` boundary maps to a plain await); TMPro → CosmicShore.Engine.UI;
// Unity.Netcode → CosmicShore.Engine.Networking; Unity.Services.Authentication →
// CosmicShore.Engine.Services; Reflex.Attributes → CosmicShore.Engine.Injection;
// UnityEngine → CosmicShore.Engine; UnityEngine.SceneManagement → CosmicShore.Engine
// .SceneManagement; the `using UnityEngine.UI;` directive folds into the engine's UI
// shim namespace.
//
// FULLY LIVE — zero deviations: the safety-timeout race, the already-signed-in
// auto-skip, cached sign-in with timeout, the auth-panel / auto-sign-in fork,
// guest login, the post-auth PlayerDataService wait + username-needed check,
// username confirm (SetDisplayName + UpdatePlayerNameAsync via the grown shim),
// the BootStatusPanel SOAP surface, and the networked Menu_Main load —
// WaitForRelayReadyAsync's dual-condition gate (OnHostConnectionEstablished +
// NM.IsListening), the 3-attempt retry with EnsurePartySessionAsync re-kick, the
// manual-retry surface, the splash re-cover, and the Netcode scene load through
// the NetworkManager.SceneManager placeholder.

using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.UI;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Controls the authentication scene UI flow.
    ///
    /// On Start:
    ///   1. Checks if the user is already signed in (cached session from Bootstrap).
    ///   2. If signed in, auto-skips to the main menu.
    ///   3. Otherwise, shows the auth panel for guest login / username setup.
    ///
    /// Auth state is read from the <see cref="AuthenticationDataVariable"/> SOAP asset.
    /// Sign-in is performed via the DI-provided <see cref="AuthenticationServiceFacade"/>.
    /// Uses SceneTransitionManager for fade transitions when available.
    /// </summary>
    public class AuthenticationSceneController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject authPanel;
        [SerializeField] private GameObject usernameSetupPanel;

        [Header("Boot Status SOAP")]
        [Tooltip("Raised to drive the BootStatusPanel surface (status text + retry button).")]
        [SerializeField] private ScriptableEventBootStatusRequest bootStatusEvent;

        [Header("Guest Login")]
        [SerializeField] private Button guestLoginButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Username Setup")]
        [SerializeField] private TMP_InputField usernameInputField;
        [SerializeField] private Button confirmUsernameButton;
        [SerializeField] private TMP_Text usernameStatusText;

        [Header("Timeouts")]
        [SerializeField, Tooltip("Seconds to wait for cached auth before showing UI.")]
        private float cachedAuthTimeout = 3f;

        [SerializeField, Tooltip("Seconds to wait for PlayerDataService init after auth.")]
        private float playerDataTimeout = 5f;

        [SerializeField, Tooltip("Hard safety timeout — force-navigates to main menu if everything hangs.")]
        private float safetyTimeout = 10f;

        [SerializeField, Tooltip("Seconds to wait per attempt for HostConnectionService to start the Relay host (minimum 15s). Three attempts are made before giving up.")]
        private float networkHostTimeout = 15f;

        [Inject] private AuthenticationServiceFacade _facade;
        [Inject] private AuthenticationDataVariable _authDataVariable;
        [Inject] private PlayerDataService _playerDataService;
        [Inject] private SceneNameListSO _sceneNames;
        [Inject] private SceneTransitionManager _sceneTransitionManager;
        [Inject] private ApplicationStateMachine _appStateMachine;
        [Inject] private HostConnectionDataSO _connectionData;

        CancellationTokenSource _cts;
        bool _navigated;

        AuthenticationData AuthData => _authDataVariable?.Value;

        // ──────────────────────────────────────────────
        //  Lifecycle
        // ──────────────────────────────────────────────

        void OnEnable()
        {
            _cts = new CancellationTokenSource();

            if (guestLoginButton)
                guestLoginButton.onClick.AddListener(OnGuestLoginClicked);

            if (confirmUsernameButton)
                confirmUsernameButton.onClick.AddListener(OnConfirmUsernameClicked);
        }

        void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (guestLoginButton)
                guestLoginButton.onClick.RemoveListener(OnGuestLoginClicked);

            if (confirmUsernameButton)
                confirmUsernameButton.onClick.RemoveListener(OnConfirmUsernameClicked);
        }

        void Start()
        {
            ClearStatusMessages();
            RunAuthFlowAsync(_cts.Token).Forget();
        }

        // ──────────────────────────────────────────────
        //  Main Auth Flow
        // ──────────────────────────────────────────────

        async Task RunAuthFlowAsync(CancellationToken ct)
        {
            HideAllPanels();
            ShowLoading("Signing in…");

            try
            {
                // Race the entire auth flow against a hard safety timeout.
                // WhenAny returns the 0-based index of the first task to complete.
                // (Port: UniTask.WhenAny(a, b) → Task.WhenAny over an eagerly-bridged
                // delay arm, winner index by reference compare — LeavePartyAsync precedent.)
                var coreTask = RunAuthFlowCoreAsync(ct);
                static async Task DelayArm(float seconds, CancellationToken token)
                    => await GameTask.Delay(seconds, unscaledTime: true, cancellationToken: token);
                int winnerIndex = await Task.WhenAny(
                    coreTask,
                    DelayArm(safetyTimeout, ct)) == coreTask ? 0 : 1;

                if (winnerIndex == 1 && !_navigated)
                {
                    CSDebug.LogWarning($"[AuthScene] Safety timeout reached after {safetyTimeout}s. Force-navigating to main menu.");
                    NavigateToMainMenu();
                }
            }
            catch (OperationCanceledException) { /* scene destroyed — expected */ }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[AuthScene] Auth flow failed: {ex.Message}. Navigating to main menu.");
                NavigateToMainMenu();
            }
        }

        async Task RunAuthFlowCoreAsync(CancellationToken ct)
        {
            // 1. Already signed in from Bootstrap?
            if (IsAlreadySignedIn())
            {
                CSDebug.Log("[AuthScene] Already signed in from Bootstrap. Auto-skipping.");
                await HandlePostAuthFlowAsync(ct);
                return;
            }

            // 2. Try cached session sign-in with a timeout.
            if (_facade != null)
            {
                bool cached = await TrySignInCachedWithTimeoutAsync(ct);
                if (cached)
                {
                    CSDebug.Log("[AuthScene] Cached session valid. Auto-skipping.");
                    await HandlePostAuthFlowAsync(ct);
                    return;
                }
            }

            // 3. No cached auth — show UI or auto-login.
            HideLoading();
            if (authPanel != null)
            {
                ShowAuthPanel();
            }
            else
            {
                CSDebug.LogWarning("[AuthScene] No auth panel in scene — attempting automatic anonymous sign-in.");
                await AttemptAutoSignInAsync(ct);
            }
        }

        // ──────────────────────────────────────────────
        //  Cached Auth
        // ──────────────────────────────────────────────

        bool IsAlreadySignedIn()
        {
            try
            {
                return AuthenticationService.Instance != null
                    && AuthenticationService.Instance.IsSignedIn;
            }
            catch
            {
                return false;
            }
        }

        async Task<bool> TrySignInCachedWithTimeoutAsync(CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(cachedAuthTimeout));

            try
            {
                // (Port: .AsUniTask().AttachExternalCancellation(token) → .WaitAsync(token).)
                return await _facade.TrySignInCachedAsync().WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                CSDebug.Log("[AuthScene] Cached auth timed out.");
                return false;
            }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[AuthScene] Cached auth failed: {ex.Message}");
                return false;
            }
        }

        // ──────────────────────────────────────────────
        //  Auto Sign-In (no UI panel)
        // ──────────────────────────────────────────────

        async Task AttemptAutoSignInAsync(CancellationToken ct)
        {
            try
            {
                if (_facade != null)
                    await _facade.EnsureSignedInAnonymouslyAsync().WaitAsync(ct);

                await HandlePostAuthFlowAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[AuthScene] Auto sign-in failed: {ex.Message}. Navigating to main menu.");
                NavigateToMainMenu();
            }
        }

        // ──────────────────────────────────────────────
        //  Guest Login (button handler)
        // ──────────────────────────────────────────────

        void OnGuestLoginClicked()
        {
            OnGuestLoginAsync(_cts?.Token ?? CancellationToken.None).Forget();
        }

        async Task OnGuestLoginAsync(CancellationToken ct)
        {
            if (guestLoginButton) guestLoginButton.interactable = false;
            ClearStatusMessages();
            ShowLoading("Signing in…");

            try
            {
                if (_facade != null)
                    await _facade.EnsureSignedInAnonymouslyAsync().WaitAsync(ct);

                await HandlePostAuthFlowAsync(ct);
            }
            catch (OperationCanceledException) { /* scene destroyed */ }
            catch (Exception ex)
            {
                HideLoading();
                ShowAuthPanel();
                if (statusText)
                    statusText.text = $"Guest login failed: {ex.Message}";
                CSDebug.LogWarning($"[AuthScene] Guest login failed: {ex}");
            }
            finally
            {
                if (guestLoginButton) guestLoginButton.interactable = true;
            }
        }

        // ──────────────────────────────────────────────
        //  Post-Auth Flow
        // ──────────────────────────────────────────────

        async Task HandlePostAuthFlowAsync(CancellationToken ct)
        {
            ShowLoading("Loading profile…");

            // Wait for PlayerDataService to initialize, with a timeout.
            if (_playerDataService != null && !_playerDataService.IsInitialized)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(playerDataTimeout));

                try
                {
                    await GameTask.WaitUntil(
                        () => _playerDataService.IsInitialized,
                        cancellationToken: timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    CSDebug.LogWarning("[AuthScene] PlayerDataService init timed out. Continuing anyway.");
                }
            }

            if (CheckIfUsernameNeeded())
            {
                HideLoading();
                ShowUsernameSetup();
            }
            else
            {
                NavigateToMainMenu();
            }
        }

        bool CheckIfUsernameNeeded()
        {
            if (_playerDataService == null || !_playerDataService.IsInitialized)
                return false;

            var profile = _playerDataService.CurrentProfile;
            return profile == null
                || string.IsNullOrEmpty(profile.displayName)
                || profile.displayName.StartsWith("Pilot", StringComparison.Ordinal);
        }

        // ──────────────────────────────────────────────
        //  Username Setup (button handler)
        // ──────────────────────────────────────────────

        void OnConfirmUsernameClicked()
        {
            OnConfirmUsernameAsync(_cts?.Token ?? CancellationToken.None).Forget();
        }

        async Task OnConfirmUsernameAsync(CancellationToken ct)
        {
            string username = usernameInputField ? usernameInputField.text?.Trim() : string.Empty;

            if (string.IsNullOrEmpty(username) || username.Length < 3 || username.Length > 25)
            {
                if (usernameStatusText)
                    usernameStatusText.text = "Username must be between 3 and 25 characters.";
                return;
            }

            if (confirmUsernameButton) confirmUsernameButton.interactable = false;

            try
            {
                if (_playerDataService != null && _playerDataService.IsInitialized)
                    _playerDataService.SetDisplayName(username);

                try
                {
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(username)
                        .WaitAsync(ct);
                }
                catch (Exception ex)
                {
                    CSDebug.LogWarning($"[AuthScene] UpdatePlayerNameAsync failed (non-critical): {ex.Message}");
                }

                NavigateToMainMenu();
            }
            catch (OperationCanceledException) { /* scene destroyed */ }
            catch (Exception ex)
            {
                if (usernameStatusText)
                    usernameStatusText.text = $"Failed to set username: {ex.Message}";
                CSDebug.LogWarning($"[AuthScene] Set username failed: {ex}");
            }
            finally
            {
                if (confirmUsernameButton) confirmUsernameButton.interactable = true;
            }
        }

        // ──────────────────────────────────────────────
        //  Panel Management
        // ──────────────────────────────────────────────

        void HideAllPanels()
        {
            if (authPanel) authPanel.SetActive(false);
            if (usernameSetupPanel) usernameSetupPanel.SetActive(false);
        }

        void ShowAuthPanel()
        {
            if (authPanel) authPanel.SetActive(true);
            if (usernameSetupPanel) usernameSetupPanel.SetActive(false);
            HideLoading();
        }

        void ShowUsernameSetup()
        {
            if (authPanel) authPanel.SetActive(false);
            if (usernameSetupPanel) usernameSetupPanel.SetActive(true);
            HideLoading();
        }

        void ShowLoading(string text = "Loading…")
            => bootStatusEvent?.Raise(new BootStatusRequest(BootStatusMode.Status, text));

        void HideLoading()
            => bootStatusEvent?.Raise(new BootStatusRequest(BootStatusMode.Hide));

        void ClearStatusMessages()
        {
            if (statusText) statusText.text = string.Empty;
            if (usernameStatusText) usernameStatusText.text = string.Empty;
        }

        // ──────────────────────────────────────────────
        //  Navigation
        // ──────────────────────────────────────────────

        void NavigateToMainMenu()
        {
            if (_navigated) return;
            _navigated = true;

            _appStateMachine?.TransitionTo(ApplicationState.MainMenu);
            CSDebug.Log("[AuthScene] Navigating to Main Menu...");
            LoadMainMenuNetworkedAsync(_cts?.Token ?? CancellationToken.None).Forget();
        }

        /// <summary>
        /// Waits for HostConnectionService to confirm a live Relay session (state InParty),
        /// then loads Menu_Main through Netcode.  Retries up to 3 times; each attempt waits
        /// up to <see cref="networkHostTimeout"/> seconds.
        ///
        /// Keeps the splash overlay opaque until Menu_Main starts loading — the overlay
        /// stays opaque through the scene transition and is released by
        /// <see cref="SceneLoader.FadeFromSplashOnReady"/> when <c>OnClientReady</c> fires.
        /// </summary>
        async Task LoadMainMenuNetworkedAsync(CancellationToken ct)
        {
            const int maxAttempts = 3;
            bool networkReady = false;
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            float timeout = Mathf.Max(networkHostTimeout, 15f);

            for (int attempt = 1; attempt <= maxAttempts && !networkReady; attempt++)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(timeout));
                try
                {
                    // Wait for the Relay session to be confirmed live (InParty reached).
                    // OnHostConnectionEstablished fires twice: at lobby join (NM not listening)
                    // and after Relay creation (NM listening). WaitForRelayReadyAsync only
                    // completes on the second fire.  (Port: the retired `.AsMainThread()`
                    // boundary maps to a plain await — GameSynchronizationContext owns
                    // affinity structurally.)
                    await WaitForRelayReadyAsync(linkedCts.Token);
                    networkReady = true;
                    CSDebug.Log($"[AuthScene] Relay session confirmed live (attempt {attempt}/{maxAttempts}).");
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (attempt < maxAttempts)
                    {
                        CSDebug.LogWarning($"[AuthScene] Relay session not ready (attempt {attempt}/{maxAttempts}) — retrying HCS init...");
                        var hcs = HostConnectionService.Instance;
                        if (hcs != null)
                            await hcs.EnsurePartySessionAsync();
                    }
                    else
                    {
                        CSDebug.LogWarning("[AuthScene] Auto-retry exhausted after 3 attempts. Surfacing manual retry button.");
                    }
                }
            }

            if (!networkReady)
            {
                // Auto-retry exhausted. Raise the retry surface via SOAP; the
                // BootStatusPanel renders it, and HostConnectionService listens
                // for the retry-requested event and calls EnsurePartySessionAsync.
                // Resume the wait with no timeout — OnHostConnectionEstablished fires
                // when manual retry succeeds and the scene load proceeds.
                bootStatusEvent?.Raise(new BootStatusRequest(BootStatusMode.Retry,
                    "Could not connect. Tap retry."));

                try
                {
                    await WaitForRelayReadyAsync(ct);
                    networkReady = true;
                    CSDebug.Log("[AuthScene] Relay session confirmed live after manual retry.");

                    // Clear the latched Retry surface. Without this the panel
                    // stays in Retry mode after the session recovers (whether
                    // via a manual tap or the session coming up on its own),
                    // and the orphaned retry button resurfaces on the next
                    // opaque splash — invite-accept or game launch.
                    ShowLoading("Connected…");
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            // Keep the splash opaque through the scene transition.  SceneLoader.OnSceneLoaded
            // will re-assert this on the Menu_Main side and subscribe FadeFromSplashOnReady
            // to OnClientReady so the overlay fades once the vessel spawns.
            _sceneTransitionManager?.SetFadeImmediate(1f);

            CSDebug.Log($"[AuthScene] Loading {menuScene} via network scene management...");
            NetworkManager.Singleton.SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
        }

        /// <summary>
        /// Resolves when <see cref="HostConnectionDataSO.OnHostConnectionEstablished"/> fires
        /// AND <see cref="NetworkManager.IsListening"/> is true — confirming the Relay session
        /// is live and NM is running as host.
        ///
        /// The event fires twice during startup: once at lobby join (NM not yet listening) and
        /// once after Relay creation (NM listening).  Only the second fire satisfies both
        /// conditions, so the lobby-join fire is silently ignored.
        /// </summary>
        async Task WaitForRelayReadyAsync(CancellationToken ct)
        {
            // Fast path: Relay is already live before we even subscribed.
            if (NetworkManager.Singleton is { IsListening: true })
                return;

            var tcs = new TaskCompletionSource();

            void OnEstablished()
            {
                if (NetworkManager.Singleton is { IsListening: true })
                    tcs.TrySetResult();
            }

            if (_connectionData?.OnHostConnectionEstablished != null)
                _connectionData.OnHostConnectionEstablished.OnRaised += OnEstablished;

            try
            {
                // (Port: tcs.Task.AttachExternalCancellation(ct) → tcs.Task.WaitAsync(ct).)
                await tcs.Task.WaitAsync(ct);
            }
            finally
            {
                if (_connectionData?.OnHostConnectionEstablished != null)
                    _connectionData.OnHostConnectionEstablished.OnRaised -= OnEstablished;
            }
        }
    }
}
