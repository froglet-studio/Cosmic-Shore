using CosmicShore.App.Profile;
using CosmicShore.App.Systems.VesselUnlock;
using CosmicShore.App.UI.ToastNotification;
using CosmicShore.Game.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.Views
{
    /// <summary>
    /// Detail view for a selected vessel in the Hangar.
    /// Tab system: General shows description + unlock, Ability tabs show ability info.
    /// Unlock flow: press unlock button → spend crystals panel (confirm disabled if not enough).
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

        [Header("Unlock Confirmation Panel")]
        [SerializeField] private GameObject unlockPanel;
        [SerializeField] private GameObject spendCrystalsPanel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text spendCrystalsDetailText;
        [SerializeField] private TMP_Text crystalAmountText;

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

            if (confirmButton)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmPurchase);
            }
        }

        void OnEnable()
        {
            VesselUnlockSystem.OnUnlockStateChanged += RefreshLockState;
            PlayerDataService.OnCrystalBalanceChanged += RefreshCrystalAmount;
        }

        void OnDisable()
        {
            VesselUnlockSystem.OnUnlockStateChanged -= RefreshLockState;
            PlayerDataService.OnCrystalBalanceChanged -= RefreshCrystalAmount;
        }

        public void SetVessel(SO_Vessel ship)
        {
            if (ship == null) return;

            _currentShip = ship;

            if (vesselNameText)
                vesselNameText.text = ship.Name.ToUpperInvariant();

            if (vesselDescriptionText)
                vesselDescriptionText.text = ship.Description;

            if (vesselPreviewImage)
                vesselPreviewImage.gameObject.SetActive(false);

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

            CloseUnlockPanel();
            RefreshLockState();
            SelectTab(0);
        }

        void SelectTab(int tabIndex)
        {
            _selectedTabIndex = tabIndex;

            if (generalButtonBG)
                generalButtonBG.SetActive(tabIndex == 0);

            for (int i = 0; i < abilityButtonBGs.Length; i++)
            {
                if (abilityButtonBGs[i])
                    abilityButtonBGs[i].SetActive(tabIndex == i + 1);
            }

            if (tabIndex == 0)
            {
                if (descriptionPanel) descriptionPanel.SetActive(true);
                if (abilitiesPanel) abilitiesPanel.SetActive(false);
            }
            else
            {
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

            if (unlockButton)
                unlockButton.interactable = isLocked;

            if (unlockButtonText)
            {
                if (isLocked)
                {
                    unlockButtonText.text = $"UNLOCK - {_currentShip.UnlockCost}";
                    unlockButtonText.color = Color.white;
                }
                else
                {
                    unlockButtonText.text = "UNLOCKED";
                    unlockButtonText.color = Color.white;
                }
            }
        }

        #region Unlock Confirmation Panel

        void OnUnlockClicked()
        {
            if (_currentShip == null || !_currentShip.IsLocked) return;

            var progression = GameModeProgressionService.Instance;
            if (progression != null && !progression.IsVesselHangarUnlocked())
            {
                ToastNotificationAPI.Show("Vessels can only be unlocked after completing the Vessel Hangar quest.");
                return;
            }

            int balance = VesselUnlockSystem.GetCurrencyBalance();
            int cost = _currentShip.UnlockCost;
            bool canAfford = balance >= cost;

            if (unlockPanel) unlockPanel.SetActive(true);
            if (spendCrystalsPanel) spendCrystalsPanel.SetActive(true);

            if (confirmButton)
                confirmButton.gameObject.SetActive(canAfford);

            RefreshCrystalAmount(balance);

            if (spendCrystalsDetailText)
                spendCrystalsDetailText.text = $"<b>{cost}</b> to unlock <b>{_currentShip.Name}</b>";
        }

        void OnConfirmPurchase()
        {
            if (_currentShip == null) return;

            if (VesselUnlockSystem.TryPurchaseVessel(_currentShip))
            {
                CSDebug.Log($"Purchased vessel: {_currentShip.Name}");
                CloseUnlockPanel();
                RefreshLockState();
            }
        }

        void RefreshCrystalAmount(int balance)
        {
            if (crystalAmountText)
                crystalAmountText.text = balance.ToString();
        }

        public void CloseUnlockPanel()
        {
            if (unlockPanel) unlockPanel.SetActive(false);
        }

        #endregion

        public void OnBackClicked()
        {
            OnBackPressed?.Invoke();
        }
    }
}
