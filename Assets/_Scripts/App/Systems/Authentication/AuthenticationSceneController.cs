using System;
using System.Threading.Tasks;
using CosmicShore.App.Profile;
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
    /// Handles guest login and username setup for new players.
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
        [SerializeField] private PlayerDataService playerDataService;

        [Header("Navigation")]
        [SerializeField] private string mainMenuSceneName = "Menu_Main";

        private bool _isProcessing;

        void Start()
        {
            SetupUI();
            ShowAuthPanel();
        }

        void SetupUI()
        {
            if (guestLoginButton)
                guestLoginButton.onClick.AddListener(OnGuestLoginClicked);

            if (confirmUsernameButton)
                confirmUsernameButton.onClick.AddListener(OnConfirmUsernameClicked);

            ClearStatusMessages();
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
                await OnAuthSuccess();
            }
            catch (Exception ex)
            {
                HideLoading();
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

        async Task OnAuthSuccess()
        {
            // Wait for PlayerDataService to initialize after auth
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

            bool needsUsername = false;

            if (playerDataService != null && playerDataService.IsInitialized)
            {
                var profile = playerDataService.CurrentProfile;
                needsUsername = profile == null
                    || string.IsNullOrEmpty(profile.displayName)
                    || profile.displayName == "Pilot";
            }
            else
            {
                // If service didn't initialize in time, prompt for username
                needsUsername = true;
            }

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
                    await playerDataService.SetDisplayNameAsync(username);
                }

                // Also set the UGS player name for multiplayer
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
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
