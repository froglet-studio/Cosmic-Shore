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
    /// The selected vessel in the Hangar (style-B menu redesign, item 2; rev 3 2026-10-16): the centre zone
    /// (the 3D vessel on its turntable, name, description, the "how it plays" sliders, unlock button), the
    /// five-tab panel on the right (Abilities, Upgrades, Games, Pilots, Artifacts) and the spend-crystals
    /// popup. It hands the selected vessel to the stage, the sliders and every tab page.
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
        [Tooltip("The 3D vessel on its turntable (rev 3).")]
        [SerializeField] private HangarVesselStage stage;
        [Tooltip("The five-tab panel on the right (rev 3).")]
        [SerializeField] private HangarTabsView tabs;
        [Tooltip("The old Hangar's three 'how it plays' sliders (rev 3).")]
        [SerializeField] private HangarVibeView vibe;
        [Tooltip("Shown instead of the 3D model when a vessel has none to build: its icon, centred.")]
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

        [Header("Words")]
        [SerializeField] private string lockedWord = "Locked";
        [SerializeField] private string ownedWord = "Owned";
        [Tooltip("The unlock button; {cost} is filled in.")]
        [SerializeField] private string unlockTemplate = "UNLOCK - {cost}";
        [SerializeField] private string unlockFreeWord = "UNLOCK - FREE";
        [Tooltip("The popup's title; {cost} and {vessel} are filled in.")]
        [SerializeField] private string spendTemplate = "{cost} to unlock {vessel}";
        [Tooltip("The popup's confirm button; {cost} is filled in.")]
        [SerializeField] private string confirmTemplate = "CONFIRM - {cost}";
        [Tooltip("The shortfall line; {amount} is filled in.")]
        [SerializeField] private string shortfallTemplate = "You need {amount} more crystals.";

        SO_Vessel _currentShip;

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

            if (stage) stage.SetVessel(ship);
            if (fallbackIcon) fallbackIcon.sprite = ship.IconActive;
            SetShown(fallbackGroup, !stage || !stage.HasModel);
            if (vibe) vibe.SetVessel(ship);
            if (tabs) tabs.SetVessel(ship, stage);

            SetShown(unlockPanel, false);
            RefreshLockState();
        }

        void RefreshLockState()
        {
            if (_currentShip == null) return;

            bool isLocked = _currentShip.IsLocked;
            int cost = _currentShip.UnlockCost;

            if (chipLabel)
            {
                chipLabel.SetText(isLocked ? lockedWord : ownedWord);
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
                unlockLabel.SetText(cost > 0 ? unlockTemplate.Replace("{cost}", cost.ToString()) : unlockFreeWord);
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
                spendCrystalsDetailText.text = spendTemplate.Replace("{cost}", cost.ToString()).Replace("{vessel}", _currentShip.Name);

            if (confirmLabel)
                confirmLabel.SetText(confirmTemplate.Replace("{cost}", cost.ToString()));
            if (confirmButton)
                confirmButton.interactable = canAfford;

            if (shortfallText && !canAfford)
                shortfallText.SetText(shortfallTemplate.Replace("{amount}", (cost - balance).ToString()));
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
