using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace CosmicShore.UI
{
    /// <summary>
    /// Top-left profile widget on the main arcade/home screen.
    /// Shows the player's avatar and username with edit capabilities.
    /// Clicking the avatar opens the avatar selection modal.
    /// Clicking the edit button lets the user change their username.
    /// </summary>
    public class ArcadeProfileWidget : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_InputField usernameInputField;
        [SerializeField] private Button editUsernameButton;
        [SerializeField] private Button saveUsernameButton;
        [SerializeField] private Button avatarButton;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIconList;

        [Header("Dependencies")]
        [Inject] private PlayerDataService playerDataService;
        [SerializeField] private ProfileIconSelectView profileIconSelectView;

        private bool _isEditing;

        void Start()
        {
            if (editUsernameButton)
                editUsernameButton.onClick.AddListener(OnEditUsernameClicked);

            if (saveUsernameButton)
            {
                saveUsernameButton.onClick.AddListener(OnSaveUsernameClicked);
                saveUsernameButton.gameObject.SetActive(false);
            }

            if (avatarButton)
                avatarButton.onClick.AddListener(OnAvatarClicked);

            if (usernameInputField)
            {
                usernameInputField.gameObject.SetActive(false);
                usernameInputField.onEndEdit.AddListener(OnUsernameInputEndEdit);
            }

            if (playerDataService)
                playerDataService.OnProfileChanged += RefreshProfile;

            // Initial refresh if data is already loaded
            if (playerDataService != null && playerDataService.CurrentProfile != null)
                RefreshProfile(playerDataService.CurrentProfile);
        }

        void OnDestroy()
        {
            if (playerDataService)
                playerDataService.OnProfileChanged -= RefreshProfile;
        }

        void RefreshProfile(PlayerProfileData profile)
        {
            if (profile == null) return;

            if (usernameText)
                usernameText.text = profile.Identity.DisplayName;

            if (avatarImage && profileIconList)
            {
                avatarImage.sprite = ResolveAvatarSprite(profile.Identity.AvatarId);
                avatarImage.enabled = avatarImage.sprite != null;
            }
        }

        /// <summary>
        /// Delegates to the one project-wide resolver
        /// (<see cref="CosmicShore.ScriptableObjects.SO_ProfileIconList.Resolve"/>),
        /// which falls back to the authored "unknown" placeholder instead of to
        /// the first real icon.
        /// </summary>
        Sprite ResolveAvatarSprite(int avatarId) =>
            profileIconList ? profileIconList.Resolve(avatarId) : null;

        // ----- Username Editing -----

        void OnEditUsernameClicked()
        {
            _isEditing = true;

            if (usernameText)
                usernameText.gameObject.SetActive(false);

            if (usernameInputField)
            {
                usernameInputField.gameObject.SetActive(true);
                usernameInputField.text = usernameText ? usernameText.text : string.Empty;
                usernameInputField.Select();
                usernameInputField.ActivateInputField();
            }

            if (editUsernameButton)
                editUsernameButton.gameObject.SetActive(false);

            if (saveUsernameButton)
                saveUsernameButton.gameObject.SetActive(true);
        }

        void OnSaveUsernameClicked()
        {
            SaveUsername();
        }

        void OnUsernameInputEndEdit(string value)
        {
            // Pressing Enter also saves
            if (_isEditing && Input.GetKeyDown(KeyCode.Return))
                SaveUsername();
        }

        void SaveUsername()
        {
            _isEditing = false;

            string newName = usernameInputField ? usernameInputField.text : string.Empty;
            SaveUsernameAsync(newName).Forget();
        }

        async UniTaskVoid SaveUsernameAsync(string newName)
        {
            // Full rule set (length, characters, profanity, duplicates) lives in
            // PlayerDataService.TrySetDisplayNameAsync. A rejected name simply leaves
            // the current profile name in place; RefreshProfile restores the label.
            var localCheck = DisplayNameValidator.Validate(newName);
            if (localCheck.IsValid && playerDataService != null)
            {
                var result = await playerDataService.TrySetDisplayNameAsync(newName);
                if (!result.IsValid)
                    CSDebug.LogWarning($"[ArcadeProfileWidget] Display name rejected: {result.Message}");
            }
            else if (!localCheck.IsValid)
            {
                CSDebug.LogWarning($"[ArcadeProfileWidget] Display name rejected: {localCheck.Message}");
            }

            RestoreUsernameDisplayMode();
        }

        void RestoreUsernameDisplayMode()
        {
            if (usernameText)
                usernameText.gameObject.SetActive(true);

            if (usernameInputField)
                usernameInputField.gameObject.SetActive(false);

            if (editUsernameButton)
                editUsernameButton.gameObject.SetActive(true);

            if (saveUsernameButton)
                saveUsernameButton.gameObject.SetActive(false);
        }

        // ----- Avatar Selection -----

        void OnAvatarClicked()
        {
            if (profileIconSelectView)
                profileIconSelectView.OpenAvatar();
        }
    }
}
