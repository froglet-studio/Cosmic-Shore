using System;
using System.Threading;
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
        [SerializeField] private GameObject loadingPanel;

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

        [SerializeField, Tooltip("Seconds to wait for the network host to become ready before falling back to direct scene load.")]
        private float networkHostTimeout = 3f;

        [Inject] private AuthenticationServiceFacade _facade;
        [Inject] private AuthenticationDataVariable _authDataVariable;
        [Inject] private PlayerDataService _playerDataService;
        [Inject] private SceneNameListSO _sceneNames;

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
            ShowLoading();

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
            ShowLoading();

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

        async UniTask HandlePostAuthFlowAsync(CancellationToken ct)
        {
            ShowLoading();

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

        async UniTaskVoid OnConfirmUsernameAsync(CancellationToken ct)
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
                        .AsUniTask().AttachExternalCancellation(ct);
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
            if (loadingPanel) loadingPanel.SetActive(false);
        }

        void ShowAuthPanel()
        {
            if (authPanel) authPanel.SetActive(true);
            if (usernameSetupPanel) usernameSetupPanel.SetActive(false);
            if (loadingPanel) loadingPanel.SetActive(false);
        }

        void ShowUsernameSetup()
        {
            if (authPanel) authPanel.SetActive(false);
            if (usernameSetupPanel) usernameSetupPanel.SetActive(true);
            if (loadingPanel) loadingPanel.SetActive(false);
        }

        void ShowLoading()
        {
            if (loadingPanel) loadingPanel.SetActive(true);
        }

        void HideLoading()
        {
            if (loadingPanel) loadingPanel.SetActive(false);
        }

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

            CSDebug.Log("[AuthScene] Navigating to Main Menu...");
            LoadMainMenuNetworkedAsync(_cts?.Token ?? CancellationToken.None).Forget();
        }

        /// <summary>
        /// Waits for the network host (started by the persistent MultiplayerSetup
        /// in response to OnSignedIn) to be ready, then loads Menu_Main via
        /// networked scene management. Falls back to a direct scene load if the
        /// host does not become ready within <see cref="networkHostTimeout"/>.
        /// </summary>
        async UniTaskVoid LoadMainMenuNetworkedAsync(CancellationToken ct)
        {
            try
            {
                // Wait for MultiplayerSetup to instantiate NetworkManager and start host.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(networkHostTimeout));

                try
                {
                    await UniTask.WaitUntil(
                        () => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening,
                        cancellationToken: timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    CSDebug.LogWarning($"[AuthScene] Network host not ready after {networkHostTimeout}s. Falling back to direct scene load.");
                    LoadMainMenuDirect();
                    return;
                }

                var nm = NetworkManager.Singleton;
                string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
                CSDebug.Log($"[AuthScene] Loading {menuScene} via network scene management...");
                nm.SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
            }
            catch (OperationCanceledException) { /* scene destroyed */ }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[AuthScene] Networked scene load failed: {ex.Message}. Falling back to direct scene load.");
                LoadMainMenuDirect();
            }
        }

        void LoadMainMenuDirect()
        {
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";

            if (ServiceLocator.TryGet<SceneTransitionManager>(out var transitionManager)
                && !transitionManager.IsTransitioning)
            {
                transitionManager.LoadSceneAsync(menuScene).Forget();
            }
            else
            {
                SceneManager.LoadScene(menuScene);
            }
        }
    }
}
