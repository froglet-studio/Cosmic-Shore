using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Core;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    /// <summary>
    /// The selected vessel in the Hangar (style-B menu redesign, item 2): the centre zone (preview,
    /// name, description, unlock button), the ability column and the spend-crystals popup.
    ///
    /// Everything is always on screen except the popup and whichever unlock button does not apply;
    /// those are shown and hidden through CanvasGroups, never SetActive. Unlock flow: the unlock
    /// button opens the popup; Confirm spends the crystals (disabled, with the shortfall stated, when
    /// the balance is too low); Cancel closes it.
    /// </summary>
    public class HangarVesselDetailView : MonoBehaviour
    {
        [Inject] private AnalyticsServiceFacade _analytics;

        [Header("Selected Vessel")]
        [Tooltip("The name, upper-cased (Display).")]
        [SerializeField] private TMP_Text vesselNameText;
        [Tooltip("SO_Vessel.Description (Body).")]
        [SerializeField] private TMP_Text vesselDescriptionText;
        [Tooltip("The hangar preview (SO_Vessel.PreviewImage), cover-fitted inside the hero frame.")]
        [SerializeField] private KitCoverImage preview;
        [SerializeField] private CanvasGroup previewGroup;
        [Tooltip("Shown instead of the preview when a vessel has none: its icon, centred.")]
        [SerializeField] private Image fallbackIcon;
        [SerializeField] private CanvasGroup fallbackGroup;
        [Tooltip("The fallback icon's opacity while the vessel is locked.")]
        [Range(0f, 1f)]
        [SerializeField] private float lockedArtAlpha = 0.5f;

        [Header("Status Chip")]
        [SerializeField] private KitText chipLabel;
        [SerializeField] private KitThemedGraphic chipBorder;
        [SerializeField] private KitThemedGraphic chipLock;
        [Tooltip("The chip's padlock leaves the layout while the vessel is owned.")]
        [SerializeField] private LayoutElement chipLockLayout;

        [Header("Abilities")]
        [SerializeField] private RectTransform abilityList;
        [Tooltip("One row per ability (HangarAbilityRow.prefab).")]
        [SerializeField] private HangarAbilityRow abilityRowPrefab;
        [SerializeField] private KitText abilityCountText;
        [SerializeField] private TMP_Text noAbilitiesText;
        [SerializeField] private CanvasGroup noAbilitiesGroup;

        [Header("Unlock")]
        [Tooltip("Purchase variant, or CTA when the price is zero.")]
        [SerializeField] private KitButton unlockButton;
        [SerializeField] private KitText unlockLabel;
        [SerializeField] private CanvasGroup unlockGroup;
        [Tooltip("The inactive \"Unlocked\" button shown while the vessel is owned.")]
        [SerializeField] private CanvasGroup unlockedGroup;

        [Header("Crystal Balance")]
        [Tooltip("The currency pill in the Hangar header.")]
        [SerializeField] private KitText headerBalanceText;
        [Tooltip("The currency pill in the spend-crystals popup.")]
        [SerializeField] private KitText popupBalanceText;

        [Header("Spend Crystals Popup")]
        [Tooltip("The dim overlay and the popup together.")]
        [SerializeField] private CanvasGroup unlockPanel;
        [SerializeField] private TMP_Text spendCrystalsDetailText;
        [SerializeField] private KitText shortfallText;
        [SerializeField] private CanvasGroup shortfallGroup;
        [Tooltip("The shortfall line leaves the layout when the balance covers the price.")]
        [SerializeField] private LayoutElement shortfallLayout;
        [SerializeField] private KitButton confirmButton;
        [SerializeField] private KitText confirmLabel;
        [SerializeField] private KitButton cancelButton;

        SO_Vessel _currentShip;
        readonly List<HangarAbilityRow> _rows = new();

        public SO_Vessel CurrentShip => _currentShip;

        /// <summary>
        /// Where focus goes when this view has nowhere sensible to keep it - the vessel just became
        /// owned, or the popup closed with no unlock button to return to. The screen points it at
        /// the selected vessel's tile.
        /// </summary>
        public System.Action ReturnFocus;

        void Awake()
        {
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

            if (cancelButton)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(CloseUnlockPanel);
            }

            SetShown(unlockPanel, false);
        }

        void OnEnable()
        {
            VesselUnlockSystem.OnUnlockStateChanged += RefreshLockState;
            PlayerDataService.OnCrystalBalanceChanged += RefreshCrystalAmount;
            RefreshCrystalAmount(VesselUnlockSystem.GetCurrencyBalance());
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

            bool hasPreview = ship.PreviewImage;
            if (preview && hasPreview) preview.SetSprite(ship.PreviewImage);
            if (fallbackIcon) fallbackIcon.sprite = ship.IconActive;
            SetShown(previewGroup, hasPreview);
            SetShown(fallbackGroup, !hasPreview);

            PopulateAbilities(ship);

            SetShown(unlockPanel, false);
            RefreshLockState();
        }

        void PopulateAbilities(SO_Vessel ship)
        {
            _rows.Clear();
            if (abilityList)
            {
                for (int i = abilityList.childCount - 1; i >= 0; i--)
                {
                    // Destroy lands at the end of the frame; deactivate so the old rows leave the
                    // column's layout now instead of stretching it for a frame.
                    var old = abilityList.GetChild(i).gameObject;
                    old.SetActive(false);
                    Destroy(old);
                }
            }

            int count = ship.Abilities?.Count ?? 0;
            if (abilityList && abilityRowPrefab)
            {
                for (int i = 0; i < count; i++)
                {
                    var row = Instantiate(abilityRowPrefab, abilityList);
                    row.name = $"Ability_{i + 1}";
                    row.Bind(ship.Abilities[i], i == count - 1);
                    _rows.Add(row);
                }
            }

            if (abilityCountText)
                abilityCountText.SetText(count > 0 ? count.ToString() : string.Empty);

            if (noAbilitiesText)
                noAbilitiesText.text = $"No abilities are listed for the {ship.Name} yet.";
            SetShown(noAbilitiesGroup, count == 0);
        }

        void RefreshLockState()
        {
            if (_currentShip == null) return;

            bool isLocked = _currentShip.IsLocked;
            int cost = _currentShip.UnlockCost;

            if (chipLabel)
            {
                chipLabel.SetText(isLocked ? "Locked" : "Owned");
                chipLabel.SetColorToken(isLocked ? UIColorToken.Purchase : UIColorToken.TextLight);
            }
            if (chipBorder) chipBorder.SetToken(isLocked ? UIColorToken.Purchase : UIColorToken.SurfaceLight);
            if (chipLock) chipLock.SetToken(UIColorToken.Purchase, isLocked ? 1f : 0f);
            if (chipLockLayout) chipLockLayout.ignoreLayout = !isLocked;

            if (fallbackIcon)
                fallbackIcon.color = new Color(1f, 1f, 1f, isLocked ? lockedArtAlpha : 1f);

            SetShown(unlockGroup, isLocked);
            SetShown(unlockedGroup, !isLocked);

            if (unlockButton)
            {
                unlockButton.interactable = isLocked;
                unlockButton.Variant = cost > 0 ? KitButtonVariant.Purchase : KitButtonVariant.Cta;
            }

            if (unlockLabel)
                unlockLabel.SetText(cost > 0 ? $"UNLOCK - {cost}" : "UNLOCK - FREE");
        }

        #region Unlock Confirmation Panel

        void OnUnlockClicked()
        {
            if (_currentShip == null || !_currentShip.IsLocked) return;

            var progression = GameModeProgressionService.Instance;
            if (progression != null && !progression.IsVesselHangarUnlocked())
            {
                CSDebug.Log("[Hangar] Vessel unlocks LOCKED — the hangar opens once every game-mode " +
                            "quest in the chain is done (CC/HexRace/Joust maxed + Maelstrom unlocked).");
                return;
            }

            int balance = VesselUnlockSystem.GetCurrencyBalance();
            int cost = _currentShip.UnlockCost;
            bool canAfford = balance >= cost;

            RefreshCrystalAmount(balance);

            if (spendCrystalsDetailText)
                spendCrystalsDetailText.text = $"{cost} to unlock {_currentShip.Name}";

            if (confirmLabel)
                confirmLabel.SetText($"CONFIRM - {cost}");
            if (confirmButton)
                confirmButton.interactable = canAfford;

            if (shortfallText && !canAfford)
                shortfallText.SetText($"You need {cost - balance} more crystals.");
            SetShown(shortfallGroup, !canAfford);
            if (shortfallLayout) shortfallLayout.ignoreLayout = canAfford;

            // Keep the pad inside the popup: Confirm and Cancel only reach each other.
            if (confirmButton)
                confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = cancelButton };
            if (cancelButton)
                cancelButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = canAfford ? confirmButton : null
                };

            SetShown(unlockPanel, true);

            var target = canAfford ? (Selectable)confirmButton : cancelButton;
            if (target && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        void OnConfirmPurchase()
        {
            if (_currentShip == null) return;

            int cost = _currentShip.UnlockCost;
            if (VesselUnlockSystem.TryPurchaseVessel(_currentShip))
            {
                _analytics?.RecordVesselUnlocked(_currentShip.Name, cost, VesselUnlockSystem.GetCurrencyBalance());
                CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[HangarVesselDetailView] Purchased vessel - {_currentShip.Name}");
                SetShown(unlockPanel, false);
                RefreshLockState();
                ReturnFocus?.Invoke();
            }
        }

        void RefreshCrystalAmount(int balance)
        {
            if (headerBalanceText) headerBalanceText.SetText(balance.ToString());
            if (popupBalanceText) popupBalanceText.SetText(balance.ToString());
        }

        /// <summary>Closes the popup; focus goes back to the unlock button, or to the vessel's tile.</summary>
        public void CloseUnlockPanel()
        {
            var es = EventSystem.current;
            var current = es ? es.currentSelectedGameObject : null;
            bool focusInPopup = unlockPanel && current && current.transform.IsChildOf(unlockPanel.transform);

            SetShown(unlockPanel, false);
            if (!focusInPopup) return;

            if (unlockButton && unlockButton.IsInteractable())
                es.SetSelectedGameObject(unlockButton.gameObject);
            else
                ReturnFocus?.Invoke();
        }

        #endregion

        static void SetShown(CanvasGroup group, bool shown)
        {
            if (!group) return;
            group.alpha = shown ? 1f : 0f;
            group.interactable = shown;
            group.blocksRaycasts = shown;
        }
    }
}
