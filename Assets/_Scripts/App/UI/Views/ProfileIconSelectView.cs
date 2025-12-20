using CosmicShore.App.UI.Modals;
using CosmicShore.Integrations.PlayFab.PlayerData;
using CosmicShore.SOAP;        // ⬅️ for GameDataSO
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class ProfileIconSelectView : ModalWindowManager
    {
        [SerializeField] SO_ProfileIconList ProfileIcons;
        [SerializeField] VerticalLayoutGroup IconGrid;
        [SerializeField] ProfileIconSelectButton IconButtonPrefab;
        [Tooltip("This should be a deactivated HorizontalLayoutGroup object in the hierarchy that will get duplicated and populated with Profile Icons")]
        [SerializeField] HorizontalLayoutGroup IconRowTemplate;
        [SerializeField] int IconsPerRow = 5;
        [SerializeField] private ProfileModal profileModal;
        [Header("Shared Game Data")]
        [SerializeField] private GameDataSO gameData;   // ⬅️ cache selection

        ProfileIconSelectButton SelectedProfileIconButton;
        ProfileIcon selectedIcon;
        bool hasSelectedIcon;
        
        protected override void Start()
        {
            var selectedIconId = LoadProfileIconSelection();

            // Clear out Icon Grid
            foreach (Transform child in IconGrid.transform)
                Destroy(child.gameObject);

            GameObject iconRow = Instantiate(IconRowTemplate.gameObject, IconGrid.transform);
            iconRow.transform.localScale = Vector3.one;
            iconRow.SetActive(true);

            int iconCount = 0;
            foreach (var profileIcon in ProfileIcons.profileIcons)
            {
                var iconButton = Instantiate(IconButtonPrefab, iconRow.transform);
                iconButton.transform.localScale = Vector3.one;
                iconButton.ProfileIcon = profileIcon;
                iconButton.IconView    = this;

                if (profileIcon.Id == selectedIconId)
                {
                    iconButton.SetSelected(true);
                    SelectedProfileIconButton = iconButton;
                    selectedIcon = profileIcon;   // ensure we mirror the loaded selection
                }

                iconCount++;

                if (iconCount % IconsPerRow == 0)
                {
                    iconRow = Instantiate(IconRowTemplate.gameObject, IconGrid.transform);
                    iconRow.transform.localScale = Vector3.one;
                    iconRow.SetActive(true);
                }
            }

            // Adjust the height
            var rect = IconGrid.GetComponent<RectTransform>();
            if (rect)
            {
                Vector2 sizeDelta = rect.sizeDelta;
                sizeDelta.y = Mathf.Ceil(iconCount / (float)IconsPerRow) * 100f;
                rect.sizeDelta = sizeDelta;
            }

            base.Start();
        }
        
        public void SelectIcon(ProfileIconSelectButton SelectedIconButton, ProfileIcon profileIcon)
        {
            if (SelectedProfileIconButton != null && SelectedProfileIconButton != SelectedIconButton)
                SelectedProfileIconButton.SetSelected(false);

            SelectedProfileIconButton = SelectedIconButton;
            SelectedProfileIconButton.SetSelected(true);

            selectedIcon   = profileIcon;
            hasSelectedIcon = true;

            SaveProfileIconSelection();
        }

        private void SaveProfileIconSelection()
        {
            if (!hasSelectedIcon)
            {
                Debug.LogWarning("[ProfileIconSelectView] SaveProfileIconSelection called but no icon is selected.");
                return;
            }

            Debug.Log($"SaveProfileIconSelection:{selectedIcon.Id}");

            // PlayFab
            if (PlayerDataController.Instance != null)
            {
                PlayerDataController.Instance.SetPlayerAvatar(selectedIcon.Id);
            }
            else
            {
                Debug.LogWarning("[ProfileIconSelectView] PlayerDataController.Instance is null. Saving avatar only locally.");
            }

            // GameData cache (if you wired it)
            if (gameData != null)
            {
                gameData.LocalPlayerAvatarId = selectedIcon.Id;
            }
            profileModal.RefreshProfileVisuals();
        }


        public int LoadProfileIconSelection()
        {
            var profile = PlayerDataController.PlayerProfile;
            int profileIconId = 0;

            if (profile != null)
            {
                profileIconId = profile.ProfileIconId;
                Debug.Log($"LoadProfileIconSelection - profile icon id:{profileIconId}");
            }
            else if (gameData != null)
            {
                profileIconId = gameData.LocalPlayerAvatarId;
                Debug.Log($"LoadProfileIconSelection - from GameData:{profileIconId}");
            }

            // Try to find matching icon by id
            var found = ProfileIcons.profileIcons.FirstOrDefault(x => x.Id == profileIconId);

            bool foundValid = !Equals(found, default(ProfileIcon)); // structs: check against default

            if (foundValid)
            {
                selectedIcon    = found;
                hasSelectedIcon = true;
            }
            else if (ProfileIcons.profileIcons.Count > 0)
            {
                // fallback safe default
                selectedIcon    = ProfileIcons.profileIcons[0];
                hasSelectedIcon = true;
                profileIconId   = selectedIcon.Id;
            }
            else
            {
                Debug.LogWarning("[ProfileIconSelectView] No profile icons available.");
                hasSelectedIcon = false;
            }

            if (hasSelectedIcon)
                Debug.Log($"LoadProfileIconSelection:{selectedIcon.Id}");

            if (gameData != null)
                gameData.LocalPlayerAvatarId = profileIconId;

            return profileIconId;
        }

    }
}
