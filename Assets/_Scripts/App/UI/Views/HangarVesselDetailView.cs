using System.Collections.Generic;
using CosmicShore.App.Systems.VesselUnlock;
using CosmicShore.App.UI.Elements.Hangar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.Views
{
    /// <summary>
    /// Detail view for a selected vessel in the Hangar.
    /// Shows abilities, overview, gameplay parameters, and unlock button.
    /// Reads unlock cost from the SO_Vessel asset directly.
    /// </summary>
    public class HangarVesselDetailView : MonoBehaviour
    {
        [Header("Vessel Info")]
        [SerializeField] private TMP_Text vesselNameText;
        [SerializeField] private TMP_Text vesselDescriptionText;
        [SerializeField] private Image vesselPreviewImage;
        [SerializeField] private Image vesselIconImage;

        [Header("Lock State")]
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private Button unlockButton;
        [SerializeField] private TMP_Text unlockButtonText;
        [SerializeField] private TMP_Text unlockCostText;

        [Header("Abilities")]
        [SerializeField] private Transform abilitiesContainer;
        [SerializeField] private HangarAbilityCard abilityCardPrefab;

        [Header("Gameplay Parameters")]
        [SerializeField] private HangarGameplayParameterDisplayGroup gameplayParameterDisplayGroup;

        [Header("Actions")]
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text selectButtonText;
        [SerializeField] private Button backButton;

        SO_Vessel _currentShip;

        public SO_Vessel CurrentShip => _currentShip;

        public System.Action OnBackPressed;

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

            if (vesselPreviewImage && ship.PreviewImage)
                vesselPreviewImage.sprite = ship.PreviewImage;

            if (vesselIconImage && ship.IconActive)
                vesselIconImage.sprite = ship.IconActive;

            PopulateAbilities(ship);
            PopulateGameplayParameters(ship);
            RefreshLockState();
        }

        void PopulateAbilities(SO_Vessel ship)
        {
            if (!abilitiesContainer) return;

            // Clear existing
            for (int i = abilitiesContainer.childCount - 1; i >= 0; i--)
                Destroy(abilitiesContainer.GetChild(i).gameObject);

            if (ship.Abilities == null) return;

            foreach (var ability in ship.Abilities)
            {
                if (ability == null) continue;
                ability.Vessel = ship;

                if (abilityCardPrefab)
                {
                    var card = Instantiate(abilityCardPrefab, abilitiesContainer);
                    card.Configure(ability);
                }
            }
        }

        void PopulateGameplayParameters(SO_Vessel ship)
        {
            if (!gameplayParameterDisplayGroup) return;

            gameplayParameterDisplayGroup.AssignGameplayParameters(
                new List<GameplayParameter>
                {
                    ship.gameplayParameter1,
                    ship.gameplayParameter2,
                    ship.gameplayParameter3
                });
        }

        void RefreshLockState()
        {
            if (_currentShip == null) return;

            bool isLocked = _currentShip.IsLocked;
            int cost = _currentShip.UnlockCost;

            if (lockedOverlay)
                lockedOverlay.SetActive(isLocked);

            if (unlockButton)
            {
                unlockButton.gameObject.SetActive(isLocked);
                unlockButton.onClick.RemoveAllListeners();
                unlockButton.onClick.AddListener(OnUnlockClicked);
            }

            if (unlockCostText)
            {
                int balance = VesselUnlockSystem.GetCurrencyBalance();
                unlockCostText.text = $"{cost}";
                unlockCostText.color = balance >= cost ? Color.white : Color.gray;
            }

            if (selectButton)
                selectButton.gameObject.SetActive(!isLocked);

            if (unlockButtonText)
            {
                int balance = VesselUnlockSystem.GetCurrencyBalance();
                unlockButtonText.text = balance >= cost ? "UNLOCK" : "INSUFFICIENT FUNDS";
            }
        }

        void OnUnlockClicked()
        {
            if (_currentShip == null) return;

            if (VesselUnlockSystem.TryPurchaseVessel(_currentShip))
            {
                CSDebug.Log($"Unlocked vessel: {_currentShip.Name}");
                RefreshLockState();
            }
            else
            {
                CSDebug.Log($"Cannot unlock vessel: {_currentShip.Name} - insufficient currency");
            }
        }

        public void OnBackClicked()
        {
            OnBackPressed?.Invoke();
        }
    }
}
