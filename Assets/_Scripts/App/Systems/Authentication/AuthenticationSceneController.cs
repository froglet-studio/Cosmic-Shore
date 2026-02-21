using System;
using System.Threading.Tasks;
using CosmicShore.App.Profile;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.Services.Auth
{
    /// <summary>
    /// Controls the authentication scene UI flow.
    /// Handles guest login, email login/register, and username setup for new players.
    /// </summary>
    public class AuthenticationSceneController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject authPanel;
        [SerializeField] private GameObject usernameSetupPanel;
        [SerializeField] private GameObject loadingPanel;

        [Header("Guest Login")]
        [SerializeField] private Button guestLoginButton;

        [Header("Email Login")]
        [SerializeField] private TMP_InputField emailInputField;
        [SerializeField] private TMP_InputField passwordInputField;
        [SerializeField] private Button emailLoginButton;
        [SerializeField] private Button emailRegisterButton;
        [SerializeField] private TMP_Text emailStatusText;

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

            if (emailLoginButton)
                emailLoginButton.onClick.AddListener(OnEmailLoginClicked);

            if (emailRegisterButton)
                emailRegisterButton.onClick.AddListener(OnEmailRegisterClicked);

            if (confirmUsernameButton)
                confirmUsernameButton.onClick.AddListener(OnConfirmUsernameClicked);

            if (emailInputField)
                emailInputField.contentType = TMP_InputField.ContentType.EmailAddress;

            if (passwordInputField)
                passwordInputField.contentType = TMP_InputField.ContentType.Password;

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
            if (emailStatusText) emailStatusText.text = string.Empty;
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
                await OnAuthSuccess(isGuest: true);
            }
            catch (Exception ex)
            {
                HideLoading();
                if (emailStatusText)
                    emailStatusText.text = $"Guest login failed: {ex.Message}";
                Debug.LogWarning($"[AuthScene] Guest login failed: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ----- Email Login -----

        async void OnEmailLoginClicked()
        {
            if (_isProcessing) return;

            string email = emailInputField ? emailInputField.text?.Trim() : string.Empty;
            string password = passwordInputField ? passwordInputField.text : string.Empty;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                if (emailStatusText)
                    emailStatusText.text = "Please enter both email and password.";
                return;
            }

            _isProcessing = true;
            ClearStatusMessages();
            ShowLoading();

            try
            {
                await authController.SignInWithEmailAsync(email, password);
                await OnAuthSuccess(isGuest: false);
            }
            catch (Exception ex)
            {
                HideLoading();
                if (emailStatusText)
                    emailStatusText.text = $"Login failed: {ex.Message}";
                Debug.LogWarning($"[AuthScene] Email login failed: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ----- Email Register -----

        async void OnEmailRegisterClicked()
        {
            if (_isProcessing) return;

            string email = emailInputField ? emailInputField.text?.Trim() : string.Empty;
            string password = passwordInputField ? passwordInputField.text : string.Empty;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                if (emailStatusText)
                    emailStatusText.text = "Please enter both email and password.";
                return;
            }

            if (password.Length < 8)
            {
                if (emailStatusText)
                    emailStatusText.text = "Password must be at least 8 characters.";
                return;
            }

            _isProcessing = true;
            ClearStatusMessages();
            ShowLoading();

            try
            {
                await authController.SignUpWithEmailAsync(email, password);
                await OnAuthSuccess(isGuest: false);
            }
            catch (Exception ex)
            {
                HideLoading();
                if (emailStatusText)
                    emailStatusText.text = $"Registration failed: {ex.Message}";
                Debug.LogWarning($"[AuthScene] Email register failed: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ----- Post-Auth Flow -----

        async Task OnAuthSuccess(bool isGuest)
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
                // If service didn't initialize in time, treat guest as needing username
                needsUsername = isGuest;
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
                    Debug.LogWarning($"[AuthScene] UpdatePlayerNameAsync failed (non-critical): {ex.Message}");
                }

                NavigateToMainMenu();
            }
            catch (Exception ex)
            {
                if (usernameStatusText)
                    usernameStatusText.text = $"Failed to set username: {ex.Message}";
                Debug.LogWarning($"[AuthScene] Set username failed: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ----- Navigation -----

        void NavigateToMainMenu()
        {
            Debug.Log("[AuthScene] Navigating to Main Menu...");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
