using CosmicShore.App.Profile;
using CosmicShore.App.UI.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI
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
        [SerializeField] private PlayerDataService playerDataService;
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
            if (playerDataService != null && playerDataService.IsInitialized)
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
                usernameText.text = profile.displayName;

            if (avatarImage && profileIconList)
            {
                avatarImage.sprite = ResolveAvatarSprite(profile.avatarId);
                avatarImage.enabled = avatarImage.sprite != null;
            }
        }

        Sprite ResolveAvatarSprite(int avatarId)
        {
            if (profileIconList == null || profileIconList.profileIcons == null)
                return null;

            foreach (var icon in profileIconList.profileIcons)
            {
                if (icon.Id == avatarId)
                    return icon.IconSprite;
            }

            // Fallback to first icon
            if (profileIconList.profileIcons.Count > 0)
                return profileIconList.profileIcons[0].IconSprite;

            return null;
        }

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

        async void OnSaveUsernameClicked()
        {
            await SaveUsername();
        }

        void OnUsernameInputEndEdit(string value)
        {
            // Pressing Enter also saves
            if (_isEditing && Input.GetKeyDown(KeyCode.Return))
                SaveUsername().ConfigureAwait(false);
        }

        async System.Threading.Tasks.Task SaveUsername()
        {
            _isEditing = false;

            string newName = usernameInputField ? usernameInputField.text?.Trim() : string.Empty;

            if (!string.IsNullOrEmpty(newName) && newName.Length >= 3 && newName.Length <= 25)
            {
                if (playerDataService != null && playerDataService.IsInitialized)
                    await playerDataService.SetDisplayNameAsync(newName);
            }

            // Restore display mode
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
