using CosmicShore.App.Systems.VesselUnlock;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.Views
{
    /// <summary>
    /// Detail view for a selected vessel in the Hangar.
    /// Tab system: General shows description + unlock, Ability tabs show ability info.
    /// Vibe button stays hidden for now.
    /// </summary>
    public class HangarVesselDetailView : MonoBehaviour
    {
        [Header("Vessel Info")]
        [SerializeField] private TMP_Text vesselNameText;
        [SerializeField] private Image vesselPreviewImage;

        [Header("Navigation")]
        [SerializeField] private Button backButton;

        [Header("Tab Buttons")]
        [SerializeField] private Button generalButton;
        [SerializeField] private Button[] abilityButtons = new Button[4];
        [SerializeField] private Button vibeButton;

        [Header("Tab Button Backgrounds")]
        [Tooltip("Child BG GameObject on each tab button — enabled when selected.")]
        [SerializeField] private GameObject generalButtonBG;
        [SerializeField] private GameObject[] abilityButtonBGs = new GameObject[4];

        [Header("Content Panels (inside AbilitiesBG)")]
        [SerializeField] private GameObject descriptionPanel;
        [SerializeField] private GameObject abilitiesPanel;

        [Header("Description Panel (General Tab)")]
        [SerializeField] private TMP_Text vesselDescriptionText;
        [SerializeField] private Button unlockButton;
        [SerializeField] private TMP_Text unlockButtonText;

        [Header("Abilities Panel")]
        [SerializeField] private TMP_Text abilitiesPreviewTitle;
        [SerializeField] private TMP_Text abilitiesPreviewText;

        SO_Vessel _currentShip;
        int _selectedTabIndex;

        public SO_Vessel CurrentShip => _currentShip;
        public System.Action OnBackPressed;

        void Awake()
        {
            if (generalButton)
            {
                generalButton.onClick.RemoveAllListeners();
                generalButton.onClick.AddListener(() => SelectTab(0));
            }

            for (int i = 0; i < abilityButtons.Length; i++)
            {
                if (!abilityButtons[i]) continue;
                int index = i + 1;
                abilityButtons[i].onClick.RemoveAllListeners();
                abilityButtons[i].onClick.AddListener(() => SelectTab(index));
            }

            if (backButton)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }

            if (unlockButton)
            {
                unlockButton.onClick.RemoveAllListeners();
                unlockButton.onClick.AddListener(OnUnlockClicked);
            }
        }

        void OnEnable()
        {
            VesselUnlockSystem.OnUnlockStateChanged += RefreshLockState;
        }

        void OnDisable()
        {
            VesselUnlockSystem.OnUnlockStateChanged -= RefreshLockState;
        }

        public void SetVessel(SO_Vessel ship)
        {
            if (ship == null) return;

            _currentShip = ship;

            if (vesselNameText)
                vesselNameText.text = ship.Name.ToUpperInvariant();

            if (vesselDescriptionText)
                vesselDescriptionText.text = ship.Description;

            // Keep preview empty for now
            if (vesselPreviewImage)
                vesselPreviewImage.gameObject.SetActive(false);

            // Set ability button labels and visibility based on vessel abilities
            int abilityCount = ship.Abilities?.Count ?? 0;
            for (int i = 0; i < abilityButtons.Length; i++)
            {
                if (!abilityButtons[i]) continue;
                bool hasAbility = i < abilityCount;
                abilityButtons[i].gameObject.SetActive(hasAbility);

                if (hasAbility)
                {
                    var label = abilityButtons[i].GetComponentInChildren<TMP_Text>();
                    if (label)
                        label.text = ship.Abilities[i].Name;
                }
            }

            if (vibeButton)
                vibeButton.gameObject.SetActive(false);

            RefreshLockState();
            SelectTab(0);
        }

        void SelectTab(int tabIndex)
        {
            _selectedTabIndex = tabIndex;

            // Highlight selected tab BG, dim others
            if (generalButtonBG)
                generalButtonBG.SetActive(tabIndex == 0);

            for (int i = 0; i < abilityButtonBGs.Length; i++)
            {
                if (abilityButtonBGs[i])
                    abilityButtonBGs[i].SetActive(tabIndex == i + 1);
            }

            if (tabIndex == 0)
            {
                // General tab — show description + unlock
                if (descriptionPanel) descriptionPanel.SetActive(true);
                if (abilitiesPanel) abilitiesPanel.SetActive(false);
            }
            else
            {
                // Ability tab — show ability info
                if (descriptionPanel) descriptionPanel.SetActive(false);
                if (abilitiesPanel) abilitiesPanel.SetActive(true);

                int abilityIndex = tabIndex - 1;
                if (_currentShip?.Abilities != null && abilityIndex < _currentShip.Abilities.Count)
                {
                    var ability = _currentShip.Abilities[abilityIndex];
                    if (abilitiesPreviewTitle)
                        abilitiesPreviewTitle.text = ability.Name;
                    if (abilitiesPreviewText)
                        abilitiesPreviewText.text = ability.Description;
                }
            }
        }

        void RefreshLockState()
        {
            if (_currentShip == null) return;

            bool isLocked = _currentShip.IsLocked;
            int cost = _currentShip.UnlockCost;

            if (unlockButton)
                unlockButton.interactable = isLocked;

            if (unlockButtonText)
            {
                if (isLocked)
                {
                    int balance = VesselUnlockSystem.GetCurrencyBalance();
                    unlockButtonText.text = balance >= cost ? $"UNLOCK - {cost}" : "INSUFFICIENT FUNDS";
                    unlockButtonText.color = balance >= cost ? Color.white : Color.gray;
                }
                else
                {
                    unlockButtonText.text = "UNLOCKED";
                    unlockButtonText.color = Color.white;
                }
            }
        }

        void OnUnlockClicked()
        {
            if (_currentShip == null || !_currentShip.IsLocked) return;

            if (VesselUnlockSystem.TryPurchaseVessel(_currentShip))
            {
                CSDebug.Log($"Unlocked vessel: {_currentShip.Name}");
                RefreshLockState();
            }
        }

        public void OnBackClicked()
        {
            OnBackPressed?.Invoke();
        }
    }
}
