using CosmicShore.Core;
using CosmicShore.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    /// <summary>
    /// Attach to the quest item prefab. Exposes serialized references
    /// so QuestTrackView can configure each card without Transform.Find.
    /// Falls back to auto-resolving children by name if fields are unassigned.
    /// </summary>
    public class QuestItemCard : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Icon")]
        [SerializeField] private Image iconImage;

        [Header("State Visuals")]
        [Tooltip("Shown when the quest is locked (mode not yet unlocked)")]
        [SerializeField] private GameObject lockedOverlay;
        [Tooltip("Shown when the quest is fully complete and claimed")]
        [SerializeField] private GameObject completedOverlay;
        [Tooltip("Icon shown when the mode is unlocked (open padlock / glow)")]
        [SerializeField] private GameObject unlockableIcon;
        [Tooltip("Button shown when the quest target is met but not yet claimed")]
        [SerializeField] private Button claimButton;

        [Header("Card Background")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Color lockedTint = new(0.35f, 0.35f, 0.35f, 1f);

        private Color _originalBgColor = Color.white;
        private GameModes _gameMode;

        public GameModes GameMode => _gameMode;

        void Awake()
        {
            ResolveReferences();
        }

        /// <summary>
        /// Auto-resolve child references by name when serialized fields are unassigned.
        /// One-time setup — not a hot path.
        /// </summary>
        void ResolveReferences()
        {
            if (unlockableIcon == null)
                unlockableIcon = FindChild("UnlockableIconBG");

            if (lockedOverlay == null)
                lockedOverlay = FindChild("LockedObject") ?? FindChild("LockedIcon");

            if (completedOverlay == null)
                completedOverlay = FindChild("CompletedObject") ?? FindChild("UnlockedObject");

            if (iconImage == null)
            {
                var iconGo = FindChild("Icon");
                if (iconGo != null)
                    iconGo.TryGetComponent(out iconImage);
            }

            if (nameText == null)
                nameText = FindTMP("GameModeText") ?? FindTMP("UnlockableDetail");

            if (descriptionText == null)
                descriptionText = FindTMP("ObjectiveText");

            if (claimButton == null)
            {
                var btnGo = FindChild("ClaimButton") ?? FindChild("CompleteButton");
                if (btnGo != null)
                    btnGo.TryGetComponent(out claimButton);
            }

            if (cardBackground == null)
                TryGetComponent(out cardBackground);
        }

        GameObject FindChild(string childName)
        {
            var t = transform.Find(childName);
            return t != null ? t.gameObject : null;
        }

        TMP_Text FindTMP(string childName)
        {
            var t = transform.Find(childName);
            if (t != null && t.TryGetComponent<TMP_Text>(out var tmp))
                return tmp;
            return null;
        }

        /// <summary>
        /// One-time setup called by QuestTrackView after instantiation.
        /// </summary>
        public void Configure(SO_GameModeQuestData quest)
        {
            _gameMode = quest.GameMode;

            if (nameText != null)
                nameText.text = quest.DisplayName;

            if (descriptionText != null)
                descriptionText.text = quest.IsPlaceholder ? "Coming Soon" : quest.Description;

            if (iconImage != null && quest.Icon != null)
                iconImage.sprite = quest.Icon;

            if (cardBackground != null)
                _originalBgColor = cardBackground.color;
        }

        /// <summary>
        /// Refreshes the visual state of the card.
        /// </summary>
        public void SetState(QuestItemState state)
        {
            bool isLocked       = state == QuestItemState.Locked;
            bool isReadyToClaim = state == QuestItemState.ReadyToClaim;
            bool isClaimed      = state == QuestItemState.Claimed;
            bool isUnlocked     = !isLocked;

            // Lock / unlock overlays
            if (lockedOverlay != null)
                lockedOverlay.SetActive(isLocked);

            if (completedOverlay != null)
                completedOverlay.SetActive(isClaimed);

            // Unlockable icon — visible whenever the mode is unlocked
            if (unlockableIcon != null)
                unlockableIcon.SetActive(isUnlocked);

            // Claim button — only when target met and awaiting player tap
            if (claimButton != null)
                claimButton.gameObject.SetActive(isReadyToClaim);

            // Tint background when locked
            if (cardBackground != null)
                cardBackground.color = isLocked ? lockedTint : _originalBgColor;

            // Text visibility — show name and description when unlocked
            if (nameText != null)
                nameText.gameObject.SetActive(isUnlocked);

            if (descriptionText != null)
            {
                descriptionText.gameObject.SetActive(isUnlocked);
                if (isClaimed)
                    descriptionText.text = "Completed";
            }
        }

        /// <summary>
        /// Wires the claim button's onClick. Called by QuestTrackView.
        /// </summary>
        public void BindClaimAction(UnityEngine.Events.UnityAction action)
        {
            if (claimButton == null) return;
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(action);
        }
    }

    public enum QuestItemState
    {
        Locked,
        Unlocked,
        ReadyToClaim,
        Claimed,
    }
}
