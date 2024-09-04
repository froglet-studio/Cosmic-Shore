using System.Collections;
using System.Security;
using CosmicShore.Integrations.PlayFab.PlayerData;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Integrations.PlayFab.Authentication
{
    public class AuthenticationView : MonoBehaviour
    {
        [SerializeField] GameObject BusyIndicator;

        [Header("Player Display Name")]
        [SerializeField] TMP_InputField displayNameInputField;
        [SerializeField] Button setDisplayNameButton;
        [SerializeField] TMP_Text displayNameResultMessage;
        [SerializeField] string displayNameDefaultText;

        [Header("Email Login")]
        [SerializeField] TMP_Text emailLoginResultMessage;
        [SerializeField] TMP_InputField emailLoginInputField;
        [SerializeField] TMP_InputField passwordLoginField;
        [SerializeField] Button loginButton;
        [SerializeField] Toggle stayLoggedInToggle;
        
        [Header("Email Linking")]
        [SerializeField] TMP_Text registerEmailResultMessage;
        [SerializeField] TMP_InputField usernameRegisterInputField;
        [SerializeField] TMP_InputField emailRegisterInputField;
        [SerializeField] TMP_InputField passwordRegisterInputField;
        [SerializeField] Button registerButton;

        // [Inject] private AuthenticationManager _authManager;

        // Start is called before the first frame update
        void Start()
        {
            // Subscribe Button OnClick Events
            setDisplayNameButton.onClick.AddListener(SetPlayerNameButton_OnClicked);

            // Set default player display name
            displayNameResultMessage.text = displayNameDefaultText;
            
            // Email login input field initialization
            // This one is secret secret
            if (passwordLoginField != null )
                passwordLoginField.contentType = TMP_InputField.ContentType.Password;
            
            if (emailLoginInputField != null)
            {
                emailLoginInputField.contentType = TMP_InputField.ContentType.EmailAddress;
                emailLoginInputField.characterValidation = TMP_InputField.CharacterValidation.EmailAddress;
                emailLoginInputField.onEndEdit.AddListener(OnEndEdit);
            }

            if (stayLoggedInToggle != null)
            {
                stayLoggedInToggle.onValueChanged.AddListener(delegate { StayLoggedIn_OnToggled(stayLoggedInToggle.isOn);});
            }

            if (loginButton != null)
                loginButton.onClick.AddListener(LoginButton_OnClick);
            
            
            // Account register input fields initialization
            if (passwordRegisterInputField != null)
                passwordRegisterInputField.contentType = TMP_InputField.ContentType.Password;

            if (emailRegisterInputField != null)  
            {
                emailRegisterInputField.contentType = TMP_InputField.ContentType.EmailAddress;
                emailRegisterInputField.characterValidation = TMP_InputField.CharacterValidation.EmailAddress;
                emailRegisterInputField.onEndEdit.AddListener(OnEndEdit);
            }
            
            if(registerButton!=null)
                registerButton.onClick.AddListener(RegisterButton_OnClick);
            
            AuthenticationManager.Instance.AnonymousLogin();
            AuthenticationManager.OnLoginSuccess += PlayerDataController.Instance.LoadPlayerProfile;
            PlayerDataController.OnProfileLoaded += InitializePlayerDisplayNameView;
        }

        void OnEndEdit(string text)
        {
            if (!EmailValidator.IsValidEmail(text))
            {
                registerEmailResultMessage.text = "Invalid Email Address";
            }
        }

        #region Email and Password Login

        /// <summary>
        /// Stay Logged in Listener Event
        /// If
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

        public string RandomGenerateName()
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

        IEnumerator AssignRandomNameCoroutine()
        {
            AuthenticationManager.Instance.LoadRandomNameList();

            yield return new WaitUntil(() => AuthenticationManager.Adjectives != null);
            
            displayNameInputField.text = RandomGenerateName();
            BusyIndicator.SetActive(false);
        } 

        public void SetPlayerNameButton_OnClicked()
        {
            displayNameResultMessage.gameObject.SetActive(false);

            if (!CheckDisplayNameLength(displayNameInputField.text))
                return;

            PlayerDataController.Instance.SetPlayerDisplayName(displayNameInputField.text, UpdatePlayerDisplayNameView);

            BusyIndicator.SetActive(true);

            Debug.Log($"Current player display name: {displayNameInputField.text}");
        }

        public void GenerateRandomNameButton_OnClicked()
        {
            BusyIndicator.SetActive(true);

            StartCoroutine(AssignRandomNameCoroutine());
        }

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

        void UpdatePlayerDisplayNameView(UpdateUserTitleDisplayNameResult result)
        {
            if (result == null)
                return;
            
            Debug.Log("Successfully Set Player Display Name.");

            displayNameResultMessage.text = "Success";
            displayNameResultMessage.gameObject.SetActive(true);
            BusyIndicator.SetActive(false);
        }

        void InitializePlayerDisplayNameView()
        {
            BusyIndicator.SetActive(false);

            displayNameInputField.text = PlayerDataController.PlayerProfile.DisplayName;

            displayNameResultMessage.text = "Display Name Loaded";
            displayNameResultMessage.gameObject.SetActive(true);
        }

        #endregion
        
    }
}