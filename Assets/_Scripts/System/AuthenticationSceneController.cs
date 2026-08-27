using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        [SerializeField, Tooltip("Hard safety timeout - force-navigates to main menu if everything hangs.")]
        private float safetyTimeout = 10f;

        [SerializeField, Tooltip("Seconds to wait per attempt for HostConnectionService to start the Relay host (minimum 15s). Three attempts are made before giving up.")]
        private float networkHostTimeout = 15f;

        [SerializeField, Tooltip("Seconds the offline notice stays on screen before continuing to the main menu, so the player reads why they are not signed in.")]
        private float offlineNoticeDwell = 2f;

        [Inject] private AuthenticationServiceFacade _facade;
        [Inject] private AuthenticationDataVariable _authDataVariable;
        [Inject] private PlayerDataService _playerDataService;
        [Inject] private SceneNameListSO _sceneNames;
        [Inject] private SceneTransitionManager _sceneTransitionManager;
        [Inject] private ApplicationStateMachine _appStateMachine;
        [Inject] private HostConnectionDataSO _connectionData;
        [Inject] private OfflineModeService _offlineMode;

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

        async UniTaskVoid RunAuthFlowAsync(CancellationToken ct)
        {
            HideAllPanels();
            ShowLoading(IsOffline ? "No connection. Starting offline…" : "Signing in…");

            try
            {
                // Race the entire auth flow against a hard safety timeout.
                // WhenAny returns the 0-based index of the first task to complete.
                int winnerIndex = await UniTask.WhenAny(
                    RunAuthFlowCoreAsync(ct),
                    UniTask.Delay(TimeSpan.FromSeconds(safetyTimeout), ignoreTimeScale: true, cancellationToken: ct)
                );

                if (winnerIndex == 1 && !_navigated)
                {
                    CSDebug.LogWarning($"[AuthScene] Safety timeout reached after {safetyTimeout}s. Force-navigating to main menu.");
                    await ShowOfflineNoticeAsync(ct);
                    NavigateToMainMenu();
                }
            }
            catch (OperationCanceledException) { /* scene destroyed - expected */ }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[AuthScene] Auth flow failed: {ex.Message}. Navigating to main menu.");
                await ShowOfflineNoticeAsync(ct);
                NavigateToMainMenu();
            }
        }

        async UniTask RunAuthFlowCoreAsync(CancellationToken ct)
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

            // 3. No cached auth - show UI or auto-login.
            HideLoading();
            if (authPanel != null)
            {
                ShowAuthPanel();
            }
            else
            {
                CSDebug.LogWarning("[AuthScene] No auth panel in scene - attempting automatic anonymous sign-in.");
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

        async UniTask<bool> TrySignInCachedWithTimeoutAsync(CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(cachedAuthTimeout));

            try
            {
                return await _facade.TrySignInCachedAsync().AsUniTask()
                    .AttachExternalCancellation(timeoutCts.Token);
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

        async UniTask AttemptAutoSignInAsync(CancellationToken ct)
        {
            try
            {
                if (_facade != null)
                    await _facade.EnsureSignedInAnonymouslyAsync().AsUniTask()
                        .AttachExternalCancellation(ct);

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

        async UniTaskVoid OnGuestLoginAsync(CancellationToken ct)
        {
            if (guestLoginButton) guestLoginButton.interactable = false;
            ClearStatusMessages();
            ShowLoading("Signing in…");

            try
            {
                if (_facade != null)
                    await _facade.EnsureSignedInAnonymouslyAsync().AsUniTask()
                        .AttachExternalCancellation(ct);

                await HandlePostAuthFlowAsync(ct);
            }
            catch (OperationCanceledException) { /* scene destroyed */ }
            catch (Exception ex)
            {
                HideLoading();
                ShowAuthPanel();
                if (statusText)
                    statusText.text = IsOffline
                        ? "No internet connection. Check your network and try again."
                        : "Sign-in failed. Please try again.";
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

        async UniTask HandlePostAuthFlowAsync(CancellationToken ct)
        {
            ShowLoading("Loading profile…");

            // Wait for PlayerDataService to initialize, with a timeout.
            if (_playerDataService != null && !_playerDataService.IsInitialized)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(playerDataTimeout));

                try
                {
                    await UniTask.WaitUntil(
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
                || string.IsNullOrEmpty(profile.Identity.DisplayName)
                || profile.Identity.DisplayName.StartsWith("Pilot", StringComparison.Ordinal);
        }

        // ──────────────────────────────────────────────
        //  Username Setup (button handler)
        // ──────────────────────────────────────────────

        /// <summary>
        /// True from the moment a submit is accepted until it either fails (retryable) or
        /// navigation begins. Disabling the button alone was not enough: the finally below
        /// re-enabled it on the SUCCESS path too, so it became clickable again during the
        /// scene transition and a second click issued a second name claim and a second
        /// profile save against a scene that was already leaving.
        /// </summary>
        bool _usernameSubmitInFlight;

        void OnConfirmUsernameClicked()
        {
            if (_usernameSubmitInFlight) return;
            OnConfirmUsernameAsync(_cts?.Token ?? CancellationToken.None).Forget();
        }

        async UniTaskVoid OnConfirmUsernameAsync(CancellationToken ct)
        {
            if (_usernameSubmitInFlight) return;
            _usernameSubmitInFlight = true;

            // Cleared on every path that leaves the player able to try again. NOT cleared once
            // navigation starts - there is no going back to this screen.
            bool navigatingAway = false;

            string username = usernameInputField ? usernameInputField.text : string.Empty;

            // Local rules first (length, characters, profanity) - instant feedback with
            // no service round-trip. The service call below re-validates and adds the
            // global duplicate check.
            var localCheck = DisplayNameValidator.Validate(username);
            if (!localCheck.IsValid)
            {
                if (usernameStatusText)
                    usernameStatusText.text = localCheck.Message;
                _usernameSubmitInFlight = false;
                return;
            }

            if (confirmUsernameButton) confirmUsernameButton.interactable = false;

            try
            {
                if (_playerDataService != null)
                {
                    var result = await _playerDataService.TrySetDisplayNameAsync(username)
                        .AttachExternalCancellation(ct);

                    if (!result.IsValid)
                    {
                        if (usernameStatusText)
                            usernameStatusText.text = result.Message;
                        return;
                    }
                }

                navigatingAway = true;
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
                // Only hand the button back when the player is still here to press it.
                // Re-enabling during the scene transition is what allowed the second click.
                if (!navigatingAway)
                {
                    _usernameSubmitInFlight = false;
                    if (confirmUsernameButton) confirmUsernameButton.interactable = true;
                }
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

        /// <summary>
        /// Device-level reachability. Cheap and synchronous, and enough to tell "the player has no
        /// network" apart from "UGS is having a bad day" - which want different copy.
        /// </summary>
        static bool IsOffline => Application.internetReachability == NetworkReachability.NotReachable;

        /// <summary>
        /// Explains why the player is arriving at the menu unauthenticated, then holds it on screen
        /// long enough to read. Without this the offline path is a silent jump that looks like the
        /// sign-in was skipped for no reason.
        /// </summary>
        async UniTask ShowOfflineNoticeAsync(CancellationToken ct)
        {
            string message = IsOffline
                ? "No internet connection. Starting in offline mode - online play and progress sync are unavailable."
                : "Could not reach the servers. Starting in offline mode - online play and progress sync are unavailable.";

            ShowLoading(message);
            if (statusText) statusText.text = message;
            CSDebug.LogWarning($"[AuthScene] {message}");

            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(Mathf.Max(0f, offlineNoticeDwell)),
                    ignoreTimeScale: true,
                    cancellationToken: ct);
            }
            catch (OperationCanceledException) { /* scene destroyed - fine, we are leaving anyway */ }
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
        /// Keeps the splash overlay opaque until Menu_Main starts loading - the overlay
        /// stays opaque through the scene transition and is released by
        /// <see cref="SceneLoader.FadeFromSplashOnReady"/> when <c>OnClientReady</c> fires.
        /// </summary>
        async UniTaskVoid LoadMainMenuNetworkedAsync(CancellationToken ct)
        {
            const int maxAttempts = 3;
            bool networkReady = false;
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            float timeout = Mathf.Max(networkHostTimeout, 15f);

            // Three reasons to skip the Relay attempts outright:
            //   • the player CHOSE offline (the menu toggle) - a deliberate choice must not
            //     cost them 45s of attempts they asked not to make;
            //   • the device reports no network at all - the attempts cannot succeed;
            //   • an offline session is already live.
            // A REACHABLE device whose UGS calls merely fail still walks the retry loop,
            // because "the player has no network" and "UGS is having a bad day" deserve the
            // attempts.
            bool offlinePreferred = _offlineMode != null && _offlineMode.OfflinePreferred;
            bool attemptRelay = !offlinePreferred
                                && !IsOffline
                                && !(_offlineMode?.IsOfflineSession ?? false);

            if (offlinePreferred)
                CSDebug.Log("[AuthScene] Offline preferred by the player - going straight to the local host.");

            for (int attempt = 1; attempt <= maxAttempts && !networkReady && attemptRelay; attempt++)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(timeout));
                try
                {
                    // Wait for the Relay session to be confirmed live (InParty reached).
                    // OnHostConnectionEstablished fires twice: at lobby join (NM not listening)
                    // and after Relay creation (NM listening). WaitForRelayReadyAsync only
                    // completes on the second fire.  .AsMainThread() guarantees the
                    // continuation runs on Unity's main thread, since the upstream SOAP
                    // raise may originate from a UGS Task completion on the ThreadPool.
                    await WaitForRelayReadyAsync(linkedCts.Token).AsMainThread();
                    networkReady = true;
                    CSDebug.Log($"[AuthScene] Relay session confirmed live (attempt {attempt}/{maxAttempts}).");
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (attempt < maxAttempts)
                    {
                        CSDebug.LogWarning($"[AuthScene] Relay session not ready (attempt {attempt}/{maxAttempts}) - retrying HCS init...");
                        try
                        {
                            var hcs = HostConnectionService.Instance;
                            if (hcs != null)
                                await hcs.EnsurePartySessionAsync().AsMainThread();
                        }
                        catch (Exception hcsEx)
                        {
                            // Unauthenticated / UGS-down create throws here. It must count as
                            // a failed attempt, not kill this UniTaskVoid - the offline
                            // fallback below is unreachable if this exception escapes.
                            CSDebug.LogWarning($"[AuthScene] HCS retry failed: {hcsEx.Message}");
                        }
                    }
                    else
                    {
                        CSDebug.LogWarning("[AuthScene] Auto-retry exhausted after 3 attempts. Surfacing manual retry button.");
                    }
                }
            }

            if (!networkReady)
            {
                // Relay is unreachable (or the device is plainly offline). Fall back to the
                // OFFLINE LOCAL HOST - the single-player fallback Steam offline mode
                // requires (Docs/OFFLINE_MODE.md): a plain 127.0.0.1 host, so the whole
                // Netcode spawn chain and every AI-backfilled mode runs unchanged, and the
                // player's last-known-good profile / unlocks load from the local
                // cloud-cache. The session stays offline until the app restarts.
                if (offlinePreferred)
                    ShowLoading("Starting offline…");
                else
                    await ShowOfflineNoticeAsync(ct);   // explains an UNWANTED offline start

                ShowLoading("Starting offline…");

                bool offlineReady;
                try
                {
                    offlineReady = _offlineMode != null
                        && await _offlineMode.EnterOfflineSessionAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    return; // scene destroyed mid-fallback
                }

                if (offlineReady)
                {
                    networkReady = true;
                    CSDebug.LogWarning("[AuthScene] Offline session started - local host on 127.0.0.1.");
                }
                else
                {
                    // Last resort (no NetworkManager / StartHost refused - never expected in
                    // a shipped build). Keep the manual retry surface so a recovered
                    // connection can still bring the session up; the wait is unbounded
                    // because there is nothing further to fall back to.
                    bootStatusEvent?.Raise(new BootStatusRequest(BootStatusMode.Retry,
                        "Could not connect. Tap retry."));

                    try
                    {
                        await WaitForRelayReadyAsync(ct).AsMainThread();
                        CSDebug.Log("[AuthScene] Relay session confirmed live after manual retry.");

                        // Clear the latched Retry surface. Without this the panel
                        // stays in Retry mode after the session recovers (whether
                        // via a manual tap or the session coming up on its own),
                        // and the orphaned retry button resurfaces on the next
                        // opaque splash - invite-accept or game launch.
                        ShowLoading("Connected…");
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
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
        /// AND <see cref="NetworkManager.IsListening"/> is true - confirming the Relay session
        /// is live and NM is running as host.
        ///
        /// The event fires twice during startup: once at lobby join (NM not yet listening) and
        /// once after Relay creation (NM listening).  Only the second fire satisfies both
        /// conditions, so the lobby-join fire is silently ignored.
        /// </summary>
        async UniTask WaitForRelayReadyAsync(CancellationToken ct)
        {
            // Fast path: Relay is already live before we even subscribed.
            if (NetworkManager.Singleton is { IsListening: true })
                return;

            var tcs = new UniTaskCompletionSource();

            void OnEstablished()
            {
                if (NetworkManager.Singleton is { IsListening: true })
                    tcs.TrySetResult();
            }

            if (_connectionData?.OnHostConnectionEstablished != null)
                _connectionData.OnHostConnectionEstablished.OnRaised += OnEstablished;

            try
            {
                await tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                if (_connectionData?.OnHostConnectionEstablished != null)
                    _connectionData.OnHostConnectionEstablished.OnRaised -= OnEstablished;
            }
        }
    }
}
