using System;
using System.Threading.Tasks;
using CosmicShore.App.Profile;
using CosmicShore.Systems.Bootstrap;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.Services.Auth
{
    /// <summary>
    /// Controls the authentication scene UI flow.
    ///
    /// On Start:
    ///   1. Checks if the user is already signed in (cached session from Bootstrap).
    ///   2. If signed in, auto-skips to the main menu.
    ///   3. Otherwise, shows the auth panel for guest login / username setup.
    ///
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

        [Header("Dependencies")]
        [SerializeField] private AuthenticationController authController;
        [Inject] private PlayerDataService playerDataService;

        [Header("Navigation")]
        [SerializeField] private string mainMenuSceneName = "Menu_Main";

        [Header("Auto-Skip")]
        [SerializeField, Tooltip("Seconds to wait for cached auth before showing UI.")]
        private float _cachedAuthTimeout = 3f;

        private bool _isProcessing;

        void Start()
        {
            SetupUI();
            RunAuthFlowAsync().Forget();
        }

        void SetupUI()
        {
            if (guestLoginButton)
                guestLoginButton.onClick.AddListener(OnGuestLoginClicked);

            if (confirmUsernameButton)
                confirmUsernameButton.onClick.AddListener(OnConfirmUsernameClicked);

            ClearStatusMessages();
        }

        // ----- Auto-Skip / Cached Auth -----

        async UniTaskVoid RunAuthFlowAsync()
        {
            // Start with loading state while we check cached auth.
            HideAllPanels();
            ShowLoading();

            try
            {
                // Check if Bootstrap's auto-auth already signed us in.
                if (IsAlreadySignedIn())
                {
                    CSDebug.Log("[AuthScene] Already signed in from Bootstrap. Auto-skipping.");
                    await HandlePostAuthFlowAsync();
                    return;
                }

                // Try cached session sign-in.
                if (authController != null)
                {
                    bool cached = await TrySignInCachedWithTimeoutAsync();
                    if (cached)
                    {
                        CSDebug.Log("[AuthScene] Cached session valid. Auto-skipping.");
                        await HandlePostAuthFlowAsync();
                        return;
                    }
                }

                // No cached auth — show the login UI.
                HideLoading();
                ShowAuthPanel();
            }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[AuthScene] Auto-skip check failed: {ex.Message}. Showing auth panel.");
                HideLoading();
                ShowAuthPanel();
            }
        }

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

        async Task<bool> TrySignInCachedWithTimeoutAsync()
        {
            var cachedTask = authController.TrySignInCachedAsync();
            var delayTask = Task.Delay(TimeSpan.FromSeconds(_cachedAuthTimeout));

            var completed = await Task.WhenAny(cachedTask, delayTask);

            if (completed == cachedTask && cachedTask.IsCompletedSuccessfully)
                return cachedTask.Result;

            return false;
        }

        // ----- Panel Management -----

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

        // ----- Guest Login -----

        async void OnGuestLoginClicked()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            ClearStatusMessages();
            ShowLoading();

            try
            {
                await authController.EnsureSignedInAnonymouslyAsync();
                await HandlePostAuthFlowAsync();
            }
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
                _isProcessing = false;
            }
        }

        // ----- Post-Auth Flow -----

        async Task HandlePostAuthFlowAsync()
        {
            ShowLoading();

            // Wait for PlayerDataService to initialize after auth.
            if (playerDataService != null)
            {
                float timeout = 5f;
                float elapsed = 0f;
                while (!playerDataService.IsInitialized && elapsed < timeout)
                {
                    await Task.Delay(100);
                    elapsed += 0.1f;
                }
            }

            bool needsUsername = CheckIfUsernameNeeded();

            if (needsUsername)
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
            if (playerDataService == null || !playerDataService.IsInitialized)
                return true;

            var profile = playerDataService.CurrentProfile;
            return profile == null
                || string.IsNullOrEmpty(profile.displayName)
                || profile.displayName == "Pilot";
        }

        // ----- Username Setup -----

        async void OnConfirmUsernameClicked()
        {
            if (_isProcessing) return;

            string username = usernameInputField ? usernameInputField.text?.Trim() : string.Empty;

            if (string.IsNullOrEmpty(username) || username.Length < 3 || username.Length > 25)
            {
                if (usernameStatusText)
                    usernameStatusText.text = "Username must be between 3 and 25 characters.";
                return;
            }

            _isProcessing = true;

            try
            {
                if (playerDataService != null && playerDataService.IsInitialized)
                {
                    playerDataService.SetDisplayName(username);
                }

                // Also set the UGS player name for multiplayer.
                try
                {
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(username);
                }
                catch (Exception ex)
                {
                    CSDebug.LogWarning($"[AuthScene] UpdatePlayerNameAsync failed (non-critical): {ex.Message}");
                }

                NavigateToMainMenu();
            }
            catch (Exception ex)
            {
                if (usernameStatusText)
                    usernameStatusText.text = $"Failed to set username: {ex.Message}";
                CSDebug.LogWarning($"[AuthScene] Set username failed: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ----- Navigation -----

        void NavigateToMainMenu()
        {
            CSDebug.Log("[AuthScene] Navigating to Main Menu...");

            if (ServiceLocator.TryGet<SceneTransitionManager>(out var transitionManager))
            {
                transitionManager.LoadSceneAsync(mainMenuSceneName).Forget();
            }
            else
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }
    }
}
