using CosmicShore.Integrations.PlayFab.PlayerData;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class ProfileIconSelectView : MonoBehaviour
    {
        [SerializeField] SO_ProfileIconList ProfileIcons;
        [SerializeField] VerticalLayoutGroup IconGrid;
        [SerializeField] ProfileIconSelectButton IconButtonPrefab;
        [Tooltip("This should be a deactivated HorizontalLayoutGroup object in the hierarchy that will get duplicated and populated with Profile Icons")]
        [SerializeField] HorizontalLayoutGroup IconRowTemplate;
        [SerializeField] int IconsPerRow = 5;
        ProfileIconSelectButton SelectedProfileIconButton;
        ProfileIcon SelectedIcon;

        void Start()
        {
            var selectedIconId = LoadProfileIconSelection();

            // Clear out Icon Grid
            foreach (Transform child in IconGrid.transform)
                Destroy(child.gameObject);

            GameObject iconRow = Instantiate(IconRowTemplate.gameObject);
            iconRow.transform.SetParent(IconGrid.transform);
            iconRow.transform.localScale = Vector3.one;
            iconRow.SetActive(true);

            int iconCount = 0;
            foreach (var profileIcon in ProfileIcons.profileIcons) 
            {
                var iconButton = Instantiate(IconButtonPrefab);
                iconButton.transform.SetParent(iconRow.transform);
                iconButton.transform.localScale = Vector3.one;
                iconButton.ProfileIcon = profileIcon;
                iconButton.IconView = this;
                if (profileIcon.Id == selectedIconId)
                    iconButton.SetSelected(true);

                iconCount++;

                if (iconCount % IconsPerRow == 0)
                {
                    iconRow = Instantiate(IconRowTemplate.gameObject);
                    iconRow.transform.SetParent(IconGrid.transform);
                    iconRow.transform.localScale = Vector3.one;
                    iconRow.SetActive(true);
                }
            }

            // Get the current sizeDelta
            Vector2 sizeDelta = IconGrid.GetComponent<RectTransform>().sizeDelta;

            // Set the new height -- number of rows * size of row
            sizeDelta.y = Mathf.Ceil(iconCount / 5f) * 100;

            // Apply the new sizeDelta
            IconGrid.GetComponent<RectTransform>().sizeDelta = sizeDelta;
        }

        public void SelectIcon(ProfileIconSelectButton SelectedIconButton, ProfileIcon profileIcon)
        {
            if (SelectedProfileIconButton != null && SelectedProfileIconButton != SelectedIconButton)
                SelectedProfileIconButton.SetSelected(false);

            SelectedProfileIconButton = SelectedIconButton;
            SelectedIconButton.SetSelected(true);
            SelectedIcon = profileIcon;

            SaveProfileIconSelection();
        }

        public void SaveProfileIconSelection()
        {
            Debug.Log($"SaveProfileIconSelection:{SelectedIcon.Id}");
            PlayerDataController.Instance.SetPlayerAvatar(SelectedIcon.Id);
        }

        public int LoadProfileIconSelection()
        {
            var profileIconId = PlayerDataController.Instance.PlayerProfile.ProfileIconId;
            SelectedIcon = ProfileIcons.profileIcons.Where(x => x.Id == profileIconId).FirstOrDefault();

            Debug.Log($"LoadProfileIconSelection:{PlayerPrefs.GetInt("ProfileIconID")}");

            return profileIconId;
        }
    }
}