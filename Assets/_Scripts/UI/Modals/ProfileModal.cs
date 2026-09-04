using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using Reflex.Attributes;
using System;
using System.Collections;
using System.Security;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace CosmicShore.UI
{
    public class ProfileModal : ModalWindowManager
    {
        [Inject] PlayerDataService playerDataService;

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

            // Profile data is owned by the UGS PlayerDataService (the dead PlayFab
            // PlayerDataController.OnProfileLoaded never fires). Subscribe to the live
            // profile event and seed the view from the current profile immediately so the
            // modal shows the real name/avatar instead of the stale "PLAYER" default.
            if (playerDataService != null)
            {
                playerDataService.OnProfileChanged += OnProfileChanged;
                if (playerDataService.CurrentProfile != null)
                    InitializePlayerDisplayNameView();
            }

            base.Start();
        }

        void OnDestroy()
        {
            if (playerDataService != null)
                playerDataService.OnProfileChanged -= OnProfileChanged;
        }

        void OnProfileChanged(PlayerProfileData profile)
        {
            InitializePlayerDisplayNameView();
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

            CSDebug.Log($"AuthenticationView - Generated display name: {displayName}");
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

            SetPlayerNameAsync(displayNameInputField.text).Forget();
        }

        async UniTaskVoid SetPlayerNameAsync(string newName)
        {
            // Local rules first (length, characters, profanity) - instant feedback.
            // PlayerDataService re-validates and adds the global duplicate check, then
            // handles the Cloud Save write and the UGS player-name sync itself.
            var localCheck = DisplayNameValidator.Validate(newName);
            if (!localCheck.IsValid)
            {
                ShowDisplayNameError(localCheck.Message);
                return;
            }

            if (setDisplayNameButton) setDisplayNameButton.interactable = false;

            try
            {
                var result = localCheck;
                if (playerDataService != null)
                {
                    result = await playerDataService.TrySetDisplayNameAsync(newName);
                    if (!result.IsValid)
                    {
                        ShowDisplayNameError(result.Message);
                        return;
                    }
                }

                if (displayNameResultMessage)
                    displayNameResultMessage.gameObject.SetActive(false);

                CacheDisplayNameLocally(result.SanitizedName);
                UpdatePlayerDisplayNameView(null);

                CSDebug.Log($"Current player display name: {result.SanitizedName}");
            }
            finally
            {
                if (setDisplayNameButton) setDisplayNameButton.interactable = true;
            }
        }

        void ShowDisplayNameError(string message)
        {
            if (!displayNameResultMessage)
                return;

            displayNameResultMessage.text = message;
            displayNameResultMessage.gameObject.SetActive(true);
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
            var profile = playerDataService != null ? playerDataService.CurrentProfile : null;

            if (displayNameInputField && profile != null && !string.IsNullOrEmpty(profile.Identity.DisplayName))
                displayNameInputField.text = profile.Identity.DisplayName;

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

        /// <summary>
        /// Called after PlayFab updates OR local-only edit: 
        /// we just refresh visuals, **no popup animation**.
        /// </summary>
        void UpdatePlayerDisplayNameView(UpdateUserTitleDisplayNameResult result)
        {
            CSDebug.Log("Successfully Set Player Display Name (local or PlayFab).");

            if (BusyIndicator)
                BusyIndicator.SetActive(false);

            if (displayNameResultMessage)
                displayNameResultMessage.gameObject.SetActive(false);

            RefreshProfileVisuals();
        }

        /// <summary>
        /// Called when the profile is loaded/changed via PlayerDataService.
        /// Sets both input + label and avatar sprite.
        /// </summary>
        void InitializePlayerDisplayNameView()
        {
            if (BusyIndicator)
                BusyIndicator.SetActive(false);

            var profile = playerDataService != null ? playerDataService.CurrentProfile : null;

            var profileDisplayName = (profile == null || string.IsNullOrEmpty(profile.Identity.DisplayName))
                ? "PLAYER"
                : profile.Identity.DisplayName;

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
            var profile = playerDataService != null ? playerDataService.CurrentProfile : null;
            string name = null;

            if (profile != null && !string.IsNullOrEmpty(profile.Identity.DisplayName))
                name = profile.Identity.DisplayName;
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
        /// Uses the avatar id from the live PlayerDataService profile to set the avatar sprite.
        /// </summary>
        void RefreshAvatarSprite()
        {
            if (!profileIconImage)
                return;

            Sprite sprite = null;

            // Primary: resolve through the live profile service (handles the id->sprite
            // lookup and its own fallback).
            if (playerDataService != null && playerDataService.CurrentProfile != null)
            {
                sprite = playerDataService.GetAvatarSprite(playerDataService.CurrentProfile.Identity.AvatarId);
            }
            // Fallback: first icon in the locally-wired list if the service isn't ready.
            else if (profileIconList != null && profileIconList.profileIcons is { Count: > 0 })
            {
                sprite = profileIconList.profileIcons[0].IconSprite;
            }

            if (sprite != null)
            {
                profileIconImage.enabled = true;
                profileIconImage.sprite = sprite;
            }
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