using CosmicShore.Systems.Audio;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.PlayerData;
using CosmicShore.Soap;
using PlayFab;
using PlayFab.ClientModels;
using Reflex.Attributes;
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
        [Inject] AudioSystem audioSystem;

        [SerializeField] GameObject BusyIndicator;

        [Header("Shared Game Data")] [SerializeField]
        private GameDataSO gameData;

        [Header("Profile Visuals")] [SerializeField]
        private SO_ProfileIconList profileIconList; // used to map id -> sprite

        [SerializeField] private Image profileIconImage; // main avatar image in the profile modal
        [SerializeField] private TMP_Text profileNameLabel; // designated place to show player name

        [Header("Player Display Name")] [SerializeField]
        TMP_InputField displayNameInputField;

        [SerializeField] Button setDisplayNameButton;
        [SerializeField] Button cancelDisplayNameButton;
        [SerializeField] TMP_Text displayNameResultMessage;
        [SerializeField] string displayNameDefaultText;
        [SerializeField] float SuccessMessageFadeAfterSeconds = 2f;
        [SerializeField] float SuccessMessageFadeDurationSeconds = 3f;
        [SerializeField] AudioClip TypingAudio;
        [SerializeField] bool FocusDisplayNameInputFieldEnabled;

        Color SuccessMessageOriginalColor;

        [Header("Email Login")] [SerializeField]
        bool ShowEmailLogin;

        [SerializeField] TMP_Text emailLoginResultMessage;
        [SerializeField] TMP_InputField emailLoginInputField;
        [SerializeField] TMP_InputField passwordLoginField;
        [SerializeField] Button loginButton;
        [SerializeField] Toggle stayLoggedInToggle;

        [Header("Email Linking")] [SerializeField]
        bool ShowEmailLinking;

        [SerializeField] TMP_Text registerEmailResultMessage;
        [SerializeField] TMP_InputField usernameRegisterInputField;
        [SerializeField] TMP_InputField emailRegisterInputField;
        [SerializeField] TMP_InputField passwordRegisterInputField;
        [SerializeField] Button registerButton;

        Action SummoningProfileMenu;

        protected override void Start()
        {
            if (setDisplayNameButton)
                setDisplayNameButton.onClick.AddListener(SetPlayerNameButton_OnClicked);

            if (displayNameResultMessage)
            {
                displayNameResultMessage.text = displayNameDefaultText;
                SuccessMessageOriginalColor = displayNameResultMessage.color;
            }

            if (ShowEmailLogin)
                InitializeEmailLogin();

            if (ShowEmailLinking)
                InitializeEmailLinking();

            PlayerDataController.OnProfileLoaded += InitializePlayerDisplayNameView;

            base.Start();
        }

        #region Email Input Field Operations (unchanged)

        void InitializeEmailLinking()
        {
            if (emailRegisterInputField != null)
            {
                emailRegisterInputField.contentType = TMP_InputField.ContentType.EmailAddress;
                emailRegisterInputField.characterValidation = TMP_InputField.CharacterValidation.EmailAddress;
                emailRegisterInputField.onEndEdit.AddListener(OnEmailInputEndEdit);
            }

            if (passwordRegisterInputField != null)
                passwordRegisterInputField.contentType = TMP_InputField.ContentType.Password;

            // if (registerButton != null)
            //     registerButton.onClick.AddListener(RegisterButton_OnClick);
        }

        void InitializeEmailLogin()
        {
            if (emailLoginInputField != null)
            {
                emailLoginInputField.contentType = TMP_InputField.ContentType.EmailAddress;
                emailLoginInputField.characterValidation = TMP_InputField.CharacterValidation.EmailAddress;
                emailLoginInputField.onEndEdit.AddListener(OnEmailInputEndEdit);
            }

            if (passwordLoginField != null)
                passwordLoginField.contentType = TMP_InputField.ContentType.Password;

            // if (loginButton != null)
            //     loginButton.onClick.AddListener(LoginButton_OnClick);

            if (stayLoggedInToggle != null)
                stayLoggedInToggle.onValueChanged.AddListener(
                    delegate { StayLoggedIn_OnToggled(stayLoggedInToggle.isOn); });
        }

        void OnEmailInputEndEdit(string text)
        {
            if (!EmailValidator.IsValidEmail(text) && registerEmailResultMessage)
            {
                registerEmailResultMessage.text = "Invalid Email Address";
            }
        }

        #endregion

        #region Email and Password Login (unchanged behavior)

        void StayLoggedIn_OnToggled(bool isOn)
        {
            AuthenticationManager.PlayerSession.IsRemembered = isOn;
        }

        SecureString GetPassword(string password)
        {
            var passwordSecure = new SecureString();
            foreach (var c in password)
                passwordSecure.AppendChar(c);

            return passwordSecure;
        }

        // Login/register error handlers unchanged…
        // RegisterButton_OnClick / LoginButton_OnClick unchanged…

        #endregion

        #region Player Profile – Name + Avatar

        string GenerateRandomName()
        {
            var adjectives = AuthenticationManager.Adjectives;
            var nouns = AuthenticationManager.Nouns;
            var random = new System.Random();
            var adjIndex = random.Next(adjectives.Count);
            var nounIndex = random.Next(nouns.Count);
            var displayName = $"{adjectives[adjIndex]} {nouns[nounIndex]}";

            Debug.Log($"AuthenticationView - Generated display name: {displayName}");
            return displayName;
        }

        IEnumerator AssignRandomNameCoroutine()
        {
            AuthenticationManager.Instance.LoadRandomNameList();

            yield return new WaitUntil(() => AuthenticationManager.Adjectives != null);

            if (displayNameInputField && BusyIndicator)
            {
                displayNameInputField.placeholder.gameObject.SetActive(false);
                BusyIndicator.SetActive(false);
            }

            var randomName = GenerateRandomName();
            for (var i = 0; i <= randomName.Length; i++)
            {
                if (displayNameInputField)
                {
                    displayNameInputField.text = randomName.Substring(0, i);
                    audioSystem.PlaySFXClip(TypingAudio);
                }

                yield return new WaitForSeconds(.075f);
            }

            if (displayNameInputField)
                displayNameInputField.text = randomName;

            if (displayNameInputField)
                displayNameInputField.placeholder.gameObject.SetActive(true);

            FocusDisplayNameInputField();
        }

        /// <summary>
        /// Called when the user presses "Set Name" in the profile modal.
        /// </summary>
        private void SetPlayerNameButton_OnClicked()
        {
            if (!displayNameInputField)
                return;

            var newName = displayNameInputField.text;

            if (!CheckDisplayNameLength(newName))
                return;

            if (displayNameResultMessage)
                displayNameResultMessage.gameObject.SetActive(false);

            if (PlayerDataController.Instance != null)
            {
                PlayerDataController.Instance.SetPlayerDisplayName(
                    newName,
                    result =>
                    {
                        CacheDisplayNameLocally(newName);
                        UpdatePlayerDisplayNameView(result);
                    });
            }
            else
            {
                Debug.LogWarning(
                    "[ProfileModal] PlayerDataController.Instance is null. Setting display name only locally.");
                CacheDisplayNameLocally(newName);
                UpdatePlayerDisplayNameView(null);
            }

            if (BusyIndicator)
                BusyIndicator.SetActive(true);

            Debug.Log($"Current player display name: {newName}");
        }

        void CacheDisplayNameLocally(string name)
        {
            if (gameData != null)
                gameData.LocalPlayerDisplayName = name;

            if (profileNameLabel)
                profileNameLabel.text = name;
        }

        public void CancelPlayerNameChange()
        {
            var profile = PlayerDataController.PlayerProfile;

            if (displayNameInputField && profile != null && !string.IsNullOrEmpty(profile.DisplayName))
                displayNameInputField.text = profile.DisplayName;

            HideDisplayNameButtons();
        }

        private void HideDisplayNameButtons()
        {
            if (setDisplayNameButton)
                setDisplayNameButton.gameObject.SetActive(false);
            if (cancelDisplayNameButton)
                cancelDisplayNameButton.gameObject.SetActive(false);
        }

        public void ShowDisplayNameChangeButtons()
        {
            if (setDisplayNameButton)
                setDisplayNameButton.gameObject.SetActive(true);
            if (cancelDisplayNameButton)
                cancelDisplayNameButton.gameObject.SetActive(true);
        }

        public void GenerateRandomNameButton_OnClicked()
        {
            if (BusyIndicator)
                BusyIndicator.SetActive(true);

            if (_assignRandomNameRunningCoroutine != null)
                StopCoroutine(_assignRandomNameRunningCoroutine);

            _assignRandomNameRunningCoroutine = StartCoroutine(AssignRandomNameCoroutine());
        }

        private Coroutine _assignRandomNameRunningCoroutine;

        bool CheckDisplayNameLength(string displayName)
        {
            if (displayName.Length is <= 25 and >= 3) return true;
            if (!displayNameResultMessage) return false;
            displayNameResultMessage.text = "Display name must be between 3 and 25 characters long";
            displayNameResultMessage.gameObject.SetActive(true);

            return false;

        }

        /// <summary>
        /// Called after PlayFab updates OR local-only edit: 
        /// we just refresh visuals, **no popup animation**.
        /// </summary>
        void UpdatePlayerDisplayNameView(UpdateUserTitleDisplayNameResult result)
        {
            Debug.Log("Successfully Set Player Display Name (local or PlayFab).");

            if (BusyIndicator)
                BusyIndicator.SetActive(false);

            if (displayNameResultMessage)
                displayNameResultMessage.gameObject.SetActive(false);

            RefreshProfileVisuals();
        }

        /// <summary>
        /// Called when the profile is loaded from PlayFab.
        /// Sets both input + label and avatar sprite.
        /// </summary>
        void InitializePlayerDisplayNameView()
        {
            if (BusyIndicator)
                BusyIndicator.SetActive(false);

            var profile = PlayerDataController.PlayerProfile;

            var profileDisplayName = string.IsNullOrEmpty(profile.DisplayName)
                ? "PLAYER"
                : profile.DisplayName;

            if (displayNameInputField)
                displayNameInputField.text = profileDisplayName;

            if (profileNameLabel)
                profileNameLabel.text = profileDisplayName;

            if (gameData)
                gameData.LocalPlayerDisplayName = profileDisplayName;

            if (displayNameResultMessage)
            {
                displayNameResultMessage.gameObject.SetActive(false);
            }

            HideDisplayNameButtons();
            RefreshAvatarSprite();
        }

        /// <summary>
        /// Helper to refresh both name & avatar from gameData/PlayerProfile. 
        /// Call this from other systems if needed.
        /// </summary>
        public void RefreshProfileVisuals()
        {
            // Name
            var profile = PlayerDataController.PlayerProfile;
            string name = null;

            if (profile != null && !string.IsNullOrEmpty(profile.DisplayName))
                name = profile.DisplayName;
            else if (gameData && !string.IsNullOrEmpty(gameData.LocalPlayerDisplayName))
                name = gameData.LocalPlayerDisplayName;

            if (name != null)
            {
                if (displayNameInputField)
                    displayNameInputField.text = name;
                if (profileNameLabel)
                    profileNameLabel.text = name;
            }

            RefreshAvatarSprite();
        }

        /// <summary>
        /// Uses ProfileIconId from PlayerProfile / GameData to set the avatar sprite.
        /// </summary>
        void RefreshAvatarSprite()
        {
            if (!profileIconImage || !profileIconList)
                return;

            int iconId = 0;

            var profile = PlayerDataController.PlayerProfile;
            if (profile != null)
            {
                iconId = profile.ProfileIconId;
            }

            
            if (profileIconList.profileIcons == null || profileIconList.profileIcons.Count == 0)
                return;

            ProfileIcon chosen = profileIconList.profileIcons[0]; // default
            foreach (var icon in profileIconList.profileIcons)
            {
                if (icon.Id == iconId)
                {
                    chosen = icon;
                    break;
                }
            }

            Sprite sprite = chosen.IconSprite;

            profileIconImage.enabled = true;
            profileIconImage.sprite = sprite;
        }

        private void FocusDisplayNameInputField()
        {
            if (FocusDisplayNameInputFieldEnabled && displayNameInputField)
            {
                displayNameInputField.Select();
                StartCoroutine(DeSelectInputFieldCoroutine());
            }
        }

        IEnumerator DeSelectInputFieldCoroutine()
        {
            yield return null;
            displayNameInputField.MoveTextEnd(false);
            displayNameInputField.ActivateInputField();
            displayNameInputField.caretPosition = displayNameInputField.text.Length;
        }

        IEnumerator FadeMessageCoroutine()
        {
            yield break;
        }

        #endregion
    }
}