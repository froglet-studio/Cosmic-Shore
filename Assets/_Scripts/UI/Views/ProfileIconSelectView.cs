using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.App.Profile;       // UgsPlayerProfileService
using CosmicShore.App.UI.Modals;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CosmicShore.App.UI.Views
{
    public enum ProfileModalTab
    {
        Avatar = 0,
        DisplayName = 1
    }

    public class ProfileIconSelectView : ModalWindowManager
    {
        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Avatar Layout")]
        [SerializeField] private GridLayoutGroup iconGrid;              // parent with GridLayoutGroup
        [SerializeField] private ProfileIconSelectButton iconButtonPrefab;

        [Header("Display Name Panel")]
        [SerializeField] private GameObject displayNamePanel;
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private Button displayNameSaveButton;
        [SerializeField] private Button displayNameCancelButton;

        [Header("Avatar Panel Root")]
        [SerializeField] private GameObject avatarPanelRoot;

        [Header("Profile / UGS")]
        [SerializeField] private PlayerDataService dataService; 
        [SerializeField] private GameDataSO gameData;

        // Internal state
        private ProfileIconSelectButton _selectedButton;
        private ProfileIcon             _selectedIcon;
        private bool                    _hasSelectedIcon;

        // ------------------------------------------------------------------
        // Unity
        // ------------------------------------------------------------------

        protected override void Start()
        {
            base.Start();

            if (!profileIcons)
            {
                Debug.LogError("[ProfileIconSelectView] Missing SO_ProfileIconList reference.");
                return;
            }

            if (!iconGrid)
            {
                Debug.LogError("[ProfileIconSelectView] Missing GridLayoutGroup (iconGrid).");
                return;
            }

            if (!iconButtonPrefab)
            {
                Debug.LogError("[ProfileIconSelectView] Missing iconButtonPrefab.");
                return;
            }

            // Wire display name buttons (if present)
            if (displayNameSaveButton)
                displayNameSaveButton.onClick.AddListener(SaveDisplayName);

            if (displayNameCancelButton)
                displayNameCancelButton.onClick.AddListener(CancelDisplayName);

            BuildAvatarGrid();
        }

        // ------------------------------------------------------------------
        // Public API used by profile screen
        // ------------------------------------------------------------------

        /// <summary>
        /// Called from the profile screen "Change Avatar" button.
        /// </summary>
        public void OpenAvatar()
        {
            Open(ProfileModalTab.Avatar);
        }

        /// <summary>
        /// Called from the profile screen "Edit Name" button.
        /// </summary>
        public void OpenDisplayName()
        {
            Open(ProfileModalTab.DisplayName);
        }

        /// <summary>
        /// General entry point: decide which tab to show and open modal.
        /// </summary>
        public void Open(ProfileModalTab tab)
        {
            SwitchTab(tab);
            ModalWindowIn();
        }

        // ------------------------------------------------------------------
        // Tab switching
        // ------------------------------------------------------------------

        private void SwitchTab(ProfileModalTab tab)
        {
            bool showAvatar     = (tab == ProfileModalTab.Avatar);
            bool showDisplayName = (tab == ProfileModalTab.DisplayName);

            if (avatarPanelRoot)
                avatarPanelRoot.SetActive(showAvatar);

            if (displayNamePanel)
                displayNamePanel.SetActive(showDisplayName);

            if (showDisplayName)
                PopulateDisplayNameFromProfile();
        }

        private void PopulateDisplayNameFromProfile()
        {
            if (!displayNameInput) return;

            string existing = "";

            if (dataService != null && dataService.IsInitialized && dataService.CurrentProfile != null)
            {
                existing = dataService.CurrentProfile.displayName;
            }

            displayNameInput.text = existing ?? string.Empty;
        }

        // ------------------------------------------------------------------
        // Avatar grid build
        // ------------------------------------------------------------------

        private void BuildAvatarGrid()
        {
            int selectedIconId = LoadProfileIconSelection();

            ClearGrid();

            foreach (var profileIcon in profileIcons.profileIcons)
            {
                var buttonInstance = Instantiate(iconButtonPrefab, iconGrid.transform);
                buttonInstance.transform.localScale = Vector3.one;

                buttonInstance.ProfileIcon = profileIcon;
                buttonInstance.IconView    = this;

                bool isSelected = (profileIcon.Id == selectedIconId);
                if (isSelected)
                {
                    _selectedButton  = buttonInstance;
                    _selectedIcon    = profileIcon;
                    _hasSelectedIcon = true;
                }

                buttonInstance.SetSelected(isSelected);
            }

            // If nothing matched the saved id, default to first icon
            if (!_hasSelectedIcon && profileIcons.profileIcons.Count > 0)
            {
                var first = profileIcons.profileIcons[0];

                var allButtons = iconGrid.GetComponentsInChildren<ProfileIconSelectButton>(true);
                var firstButton = allButtons.FirstOrDefault(b => b.ProfileIcon.Id == first.Id);

                if (firstButton != null)
                {
                    _selectedButton  = firstButton;
                    _selectedIcon    = first;
                    _hasSelectedIcon = true;
                    firstButton.SetSelected(true);
                }
            }
        }

        private void ClearGrid()
        {
            var toDestroy = new List<GameObject>();
            for (int i = 0; i < iconGrid.transform.childCount; i++)
            {
                toDestroy.Add(iconGrid.transform.GetChild(i).gameObject);
            }

            foreach (var go in toDestroy)
            {
                Destroy(go);
            }
        }

        // ------------------------------------------------------------------
        // Avatar selection API (called by ProfileIconSelectButton)
        // ------------------------------------------------------------------

        public void SelectIcon(ProfileIconSelectButton button, ProfileIcon profileIcon)
        {
            if (_selectedButton != null && _selectedButton != button)
                _selectedButton.SetSelected(false);

            _selectedButton  = button;
            _selectedIcon    = profileIcon;
            _hasSelectedIcon = true;

            _selectedButton.SetSelected(true);

            SaveProfileIconSelection();
        }

        // ------------------------------------------------------------------
        // Avatar save / load
        // ------------------------------------------------------------------

        private void SaveProfileIconSelection()
        {
            if (!_hasSelectedIcon)
            {
                Debug.LogWarning("[ProfileIconSelectView] SaveProfileIconSelection called but no icon is selected.");
                return;
            }

            int id = _selectedIcon.Id;
            Debug.Log($"[ProfileIconSelectView] SaveProfileIconSelection: {id}");

            // Cloud save through UGS profile service
            if (dataService != null && dataService.IsInitialized)
            {
                dataService.SetAvatarId(id);      // do CloudSave inside this
            }
            else
            {
                Debug.LogWarning("[ProfileIconSelectView] profileService is null or not initialized. Avatar cached locally only.");
            }
            
        }

        private int LoadProfileIconSelection()
        {
            int avatarId = 0;
            bool hasId   = false;

            // 1) Try UGS profile service
            if (dataService != null && dataService.IsInitialized && dataService.CurrentProfile != null)
            {
                avatarId = dataService.CurrentProfile.avatarId;
                hasId    = avatarId != 0;
                Debug.Log($"[ProfileIconSelectView] Load from UGS: {avatarId}");
            }

            return avatarId;
        }

        // ------------------------------------------------------------------
        // Display name save / cancel
        // ------------------------------------------------------------------

        private async void SaveDisplayName()
        {
            try
            {
                if (!displayNameInput)
                    return;

                string newName = displayNameInput.text?.Trim();

                 await dataService.SetDisplayNameAsync(newName);
                dataService.RefreshProfileVisuals();

                ModalWindowOut();
            }
            catch (Exception e)
            {
                 // TODO handle exception
            }
        }

        private void CancelDisplayName()
        {
            // Revert input to current profile name
            PopulateDisplayNameFromProfile();

            // Either close the modal or just go back to avatar tab; I’ll close for now:
            ModalWindowOut();
        }
    }
}
