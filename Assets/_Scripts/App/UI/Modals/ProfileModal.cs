using CosmicShore.App.Systems.Audio;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.PlayerData;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections;
using System.Security;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Modals
{
    public class ProfileModal : ModalWindowManager
    {
        [SerializeField] GameObject BusyIndicator;

        [Header("Player Display Name")]
        [SerializeField] TMP_InputField displayNameInputField;
        [SerializeField] Button setDisplayNameButton;
        [SerializeField] Button cancelDisplayNameButton;
        [SerializeField] TMP_Text displayNameResultMessage;
        [SerializeField] string displayNameDefaultText;
        [SerializeField] float SuccessMessageFadeAfterSeconds = 2f;
        [SerializeField] float SuccessMessageFadeDurationSeconds = 3f;
        [SerializeField] AudioClip TypingAudio;
        [SerializeField] bool FocusDisplayNameInputFieldEnabled;

        Color SuccessMessageOriginalColor;

        [Header("Email Login")]
        [SerializeField] bool ShowEmailLogin;
        [SerializeField] TMP_Text emailLoginResultMessage;
        [SerializeField] TMP_InputField emailLoginInputField;
        [SerializeField] TMP_InputField passwordLoginField;
        [SerializeField] Button loginButton;
        [SerializeField] Toggle stayLoggedInToggle;

        [Header("Email Linking")]
        [SerializeField] bool ShowEmailLinking;
        [SerializeField] TMP_Text registerEmailResultMessage;
        [SerializeField] TMP_InputField usernameRegisterInputField;
        [SerializeField] TMP_InputField emailRegisterInputField;
        [SerializeField] TMP_InputField passwordRegisterInputField;
        [SerializeField] Button registerButton;

        Action SummoningProfileMenu;

        protected override void Start()
        {
            // Subscribe Button OnClick Events
            setDisplayNameButton.onClick.AddListener(SetPlayerNameButton_OnClicked);

            // Set default player display name
            displayNameResultMessage.text = displayNameDefaultText;
            SuccessMessageOriginalColor = displayNameResultMessage.color;

            if (ShowEmailLogin)
                InitializeEmailLogin();

            if (ShowEmailLinking)
                InitializeEmailLinking();

            // _authManager.OnLoginSuccess += _authManager.LoadPlayerProfile;
            PlayerDataController.OnProfileLoaded += InitializePlayerDisplayNameView;
            // _authManager.AnonymousLogin();

            base.Start();
        }

        #region Email Input Field Operations
        void InitializeEmailLinking()
        {
            // Account register input fields initialization
            if (emailRegisterInputField != null)
            {
                emailRegisterInputField.contentType = TMP_InputField.ContentType.EmailAddress;
                emailRegisterInputField.characterValidation = TMP_InputField.CharacterValidation.EmailAddress;
                emailRegisterInputField.onEndEdit.AddListener(OnEmailInputEndEdit);
            }

            if (passwordRegisterInputField != null)
                passwordRegisterInputField.contentType = TMP_InputField.ContentType.Password; // This one is secret secret

            if (registerButton != null)
                registerButton.onClick.AddListener(RegisterButton_OnClick);
        }

        void InitializeEmailLogin()
        {
            // Email login input field initialization
            if (emailLoginInputField != null)
            {
                emailLoginInputField.contentType = TMP_InputField.ContentType.EmailAddress;
                emailLoginInputField.characterValidation = TMP_InputField.CharacterValidation.EmailAddress;
                emailLoginInputField.onEndEdit.AddListener(OnEmailInputEndEdit);
            }

            if (passwordLoginField != null)
                passwordLoginField.contentType = TMP_InputField.ContentType.Password; // This one is secret secret

            if (loginButton != null)
                loginButton.onClick.AddListener(LoginButton_OnClick);

            if (stayLoggedInToggle != null)
                stayLoggedInToggle.onValueChanged.AddListener(delegate { StayLoggedIn_OnToggled(stayLoggedInToggle.isOn); });
        }

        void OnEmailInputEndEdit(string text)
        {
            if (!EmailValidator.IsValidEmail(text))
            {
                registerEmailResultMessage.text = "Invalid Email Address";
            }
        }
        #endregion
            
        #region Email and Password Login

        /// <summary>
        /// Stay Logged in Event 
        /// Update stay logged in stats
        /// </summary>
        void StayLoggedIn_OnToggled(bool isOn)
        {
            AuthenticationManager.PlayerSession.IsRemembered = isOn;
        }

        /// <summary>
        /// Get password (Obviously)
        /// Transit password to secured string, for very secure security
        /// </summary>
        SecureString GetPassword(string password)
        {
            var passwordSecure = new SecureString();
            foreach (var c in password)
            {
                passwordSecure.AppendChar(c);
            }

            return passwordSecure;
        }

        
        /// <summary>
        /// Handle Anonymous Login Error
        /// Handling anonymous login errors - Connection Error, Invalid Account , Account Deleted and the others.
        /// <param name="PlayFabError"> PlayFab Error</param>
        /// </summary>
        private void HandleAnonymousLoginError(PlayFabError error)
        {
            if (error == null)
            {
                Debug.Log("Anonymous login success.");
                emailLoginResultMessage.text = "Anonymous login success.";
                return;
            }

            switch (error.Error)
            {
                case PlayFabErrorCode.ConnectionError:
                    Debug.Log("Connection issues.");
                    emailLoginResultMessage.text = "Connection issues.";
                    break;
                case PlayFabErrorCode.InvalidAccount:
                    Debug.Log("Invalid Account.");
                    emailLoginResultMessage.text = "Invalid Account.";
                    break;
                case PlayFabErrorCode.AccountDeleted:
                    Debug.Log("Account deleted.");
                    emailLoginResultMessage.text = "Account deleted.";
                    break;
                default:
                    Debug.Log("Unknown nightmare.");
                    Debug.Log(error.ErrorMessage);
                    Debug.Log(error.ErrorDetails);
                    Debug.Log(error.Error.ToString());
                    emailLoginResultMessage.text = "Unknown nightmare.";
                    break;
            }
        }

        /// <summary>
        /// Register Response Error Handler
        /// Handles error responses upon account registration
        /// <param name="PlayFabError"> PlayFab Error</param>
        /// </summary>
        private void RegisterResponseHandler(PlayFabError error)
        {
            if (error == null)
            {
                Debug.Log("Register Success.");
                registerEmailResultMessage.text = "Register Success.";
                return;
            }

            switch (error.Error)
            {
                case PlayFabErrorCode.DuplicateEmail:
                    Debug.Log("Duplicated Email.");
                    registerEmailResultMessage.text = "Duplicated Email.";
                    break;
                case PlayFabErrorCode.EmailAddressNotAvailable:
                    Debug.Log("Email Address is already in use.");
                    registerEmailResultMessage.text = "Email Address is already in use.";
                    break;
                case PlayFabErrorCode.ConnectionError:
                    Debug.Log("Not connected to Internet.");
                    registerEmailResultMessage.text = "Not connected to Internet.";
                    break;
                default:
                    Debug.Log("Unknown nightmare.");
                    Debug.Log(error.ErrorMessage);
                    Debug.Log(error.ErrorDetails);
                    Debug.Log(error.Error.ToString());
                    registerEmailResultMessage.text = "Unknown nightmare.";
                    break;
            }
        }

        /// <summary>
        /// Login Response Error Handler
        /// Handles error responses upon account login
        /// <param name="PlayFabError"> PlayFab Error</param>
        /// </summary>
        private void LoginResponseHandler(PlayFabError error)
        {
            if (error == null)
            {
                Debug.Log("Login Success.");
                emailLoginResultMessage.text = "Login Success.";
                return;
            }

            switch (error.Error)
            {
                case PlayFabErrorCode.InvalidEmailAddress:
                    Debug.Log("Invalid email address.");
                    emailLoginResultMessage.text = "Invalid email address.";
                    break;
                case PlayFabErrorCode.InvalidAccount:
                    Debug.Log("Invalid Account.");
                    emailLoginResultMessage.text = "Invalid Account.";
                    break;
                case PlayFabErrorCode.InvalidPassword:
                    Debug.Log("Invalid Password.");
                    emailLoginResultMessage.text = "Invalid Password.";
                    break;
                case PlayFabErrorCode.ConnectionError:
                    Debug.Log("Not connected to Internet.");
                    emailLoginResultMessage.text = "Not connected to Internet.";
                    break;
                default:
                    Debug.Log("Unknown nightmare.");
                    Debug.Log(error.ErrorMessage);
                    Debug.Log(error.ErrorDetails);
                    Debug.Log(error.Error.ToString());
                    emailLoginResultMessage.text = "Unknown nightmare.";
                    break;
            }
        }

        /// <summary>
        /// Update player display name with random generated one
        /// Can be tested by clicking Generate Random Name button
        /// </summary>
        public void RegisterButton_OnClick()
        {
            var email = emailRegisterInputField.text;
            var password = passwordRegisterInputField.text;

            if (!EmailValidator.IsValidEmail(email))
            {
                registerEmailResultMessage.text = "Invalid Email Address";
                return;
            }

            // This is a test for email register, we can worry about it linking device later
            // AnonymousLogin();
            AuthenticationManager.Instance.RegisterWithEmail(email, GetPassword(password), RegisterResponseHandler);
        }

        /// <summary>
        /// Email Login
        /// Can be tested with Email Login button
        /// </summary>
        public void LoginButton_OnClick()
        {
            var email = emailLoginInputField.text;
            var password = passwordLoginField.text;

            AuthenticationManager.Instance.EmailLogin(email, GetPassword(password), LoginResponseHandler);
        }

        #endregion

        #region Player Profile


        
        /// <summary>
        /// Generate Random Name
        /// Retrieve preset adjectives and nouns that was retrieved in memory and combine them randomly
        /// </summary>
        string GenerateRandomName()
        {
            var adjectives = AuthenticationManager.Adjectives;
            var nouns = AuthenticationManager.Nouns;
            var random = new System.Random();
            var adj_index = random.Next(adjectives.Count);
            var noun_index = random.Next(nouns.Count);
            var displayName = $"{adjectives[adj_index]} {nouns[noun_index]}";

            Debug.Log($"AuthenticationView - Generated display name: {displayName}");

            return displayName;
        }

        /// <summary>
        /// Assign Random Name (Coroutine)
        /// Assign random generated name to display name input field
        /// </summary>
        IEnumerator AssignRandomNameCoroutine()
        {
            AuthenticationManager.Instance.LoadRandomNameList();

            yield return new WaitUntil(() => AuthenticationManager.Adjectives != null);

            displayNameInputField.placeholder.gameObject.SetActive(false);
            BusyIndicator.SetActive(false);

            var name = GenerateRandomName();
            for (var i=0; i<=name.Length; i++)
            {
                displayNameInputField.text = name.Substring(0, i);
                AudioSystem.Instance.PlaySFXClip(TypingAudio);
                yield return new WaitForSeconds(.075f);
            }

            displayNameInputField.text = name;

            displayNameInputField.placeholder.gameObject.SetActive(true);
            FocusDisplayNameInputField();
        }
        
        /// <summary>
        /// Set Player Name Button On Click 
        /// Setting Player Name Event for the button on click listener
        /// </summary>
        private void SetPlayerNameButton_OnClicked()
        {
            displayNameResultMessage.gameObject.SetActive(false);

            if (!CheckDisplayNameLength(displayNameInputField.text))
                return;

            PlayerDataController.Instance.SetPlayerDisplayName(displayNameInputField.text, UpdatePlayerDisplayNameView);

            BusyIndicator.SetActive(true);

            Debug.Log($"Current player display name: {displayNameInputField.text}");
        }

        public void CancelPlayerNameChange()
        {
            displayNameInputField.text = PlayerDataController.PlayerProfile.DisplayName;
            HideDisplayNameButtons();
        }

        public void HideDisplayNameButtons()
        {
            setDisplayNameButton.gameObject.SetActive(false);
            cancelDisplayNameButton.gameObject.SetActive(false);
        }

        public void ShowDisplayNameChangeButtons()
        {
            setDisplayNameButton.gameObject.SetActive(true);
            cancelDisplayNameButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// Generate Random Name Button OnClick Event 
        /// Generate random name on button click 
        /// </summary>
        public void GenerateRandomNameButton_OnClicked()
        {
            BusyIndicator.SetActive(true);

            if (AssignRandomNameRunningCoroutine != null)
                StopCoroutine(AssignRandomNameRunningCoroutine);

            AssignRandomNameRunningCoroutine = StartCoroutine(AssignRandomNameCoroutine());
        }

        Coroutine AssignRandomNameRunningCoroutine;

        /// <summary>
        /// Check Display Name Length
        /// PlayFab constraints display name within 3 to 25 characters, this is a check for display name length.
        /// </summary>
        bool CheckDisplayNameLength(string displayName)
        {
            if (displayName.Length > 25 || displayName.Length < 3)
            {
                displayNameResultMessage.text = "Display name must be between 3 and 25 characters long";
                displayNameResultMessage.gameObject.SetActive(true);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Update Player Display View
        /// PlayFab constraints display name within 3 to 25 characters, this is a check for display name length.
        /// </summary>
        void UpdatePlayerDisplayNameView(UpdateUserTitleDisplayNameResult result)
        {
            if (result == null)
                return;

            Debug.Log("Successfully Set Player Display Name.");

            displayNameResultMessage.text = "Success";
            StartCoroutine(FadeMessageCoroutine());
            displayNameResultMessage.gameObject.SetActive(true);
            BusyIndicator.SetActive(false);
        }

        /// <summary>
        /// Initialize Player Display Name View
        /// PlayFab constraints display name within 3 to 25 characters, this is a check for display name length.
        /// </summary>
        void InitializePlayerDisplayNameView()
        {
            if (BusyIndicator != null)
            {
                BusyIndicator.SetActive(false);
            }
            
            if (PlayerDataController.PlayerProfile == null)
            {
                Debug.LogWarning("Player profile has not yet loaded.");
                return;
            }
            
            if(!string.IsNullOrEmpty(PlayerDataController.PlayerProfile.DisplayName))
                displayNameInputField.text = PlayerDataController.PlayerProfile.DisplayName;

            if (displayNameResultMessage == null) return;
            displayNameResultMessage.gameObject.SetActive(true);

            HideDisplayNameButtons();
        }

        /// <summary>
        /// Focus Display Name Input Field
        /// Automatically set the cursor to display name input field and De-select it afterward
        /// </summary>
        public void FocusDisplayNameInputField()
        {
            if (FocusDisplayNameInputFieldEnabled)
            {
                displayNameInputField.Select();
                StartCoroutine(DeSelectInputFieldCoroutine());
            }
        }

        /// <summary>
        /// De-select Input Field (Coroutine)
        /// De-select input field after a frame
        /// </summary>
        IEnumerator DeSelectInputFieldCoroutine()
        {
            // Yes, this is wacky, but you have to wait for the next frame to not auto select the name
            // I think the blinking cursor at the end of the line is kinda cool
            // Yes, it is cool - Echo
            yield return null;
            displayNameInputField.MoveTextEnd(false);
            displayNameInputField.ActivateInputField();
            displayNameInputField.caretPosition = displayNameInputField.text.Length;
        }

        /// <summary>
        /// Fade Massage Coroutine
        /// Fade massage coroutine tool, the default duration for fading is 3s for now.
        /// </summary>
        IEnumerator FadeMessageCoroutine()
        {
            yield return new WaitForSeconds(SuccessMessageFadeAfterSeconds);

            var elapsed = 0f;
            while (elapsed < SuccessMessageFadeDurationSeconds)
            {
                displayNameResultMessage.color = new Color(SuccessMessageOriginalColor.r, SuccessMessageOriginalColor.g, SuccessMessageOriginalColor.b, 1-(elapsed/ SuccessMessageFadeDurationSeconds));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            displayNameResultMessage.text = "";
            displayNameResultMessage.color = SuccessMessageOriginalColor;
        }

        #endregion
    }
}