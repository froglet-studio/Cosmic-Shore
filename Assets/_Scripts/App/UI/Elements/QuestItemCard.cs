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
        [Tooltip("Button shown when the quest target is met but not yet claimed")]
        [SerializeField] private Button claimButton;

        [Header("Card Background")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Color lockedTint = new(0.35f, 0.35f, 0.35f, 1f);

        private Color _originalBgColor = Color.white;
        private GameModes _gameMode;

        public GameModes GameMode => _gameMode;

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
            bool isLocked      = state == QuestItemState.Locked;
            bool isReadyToClaim = state == QuestItemState.ReadyToClaim;
            bool isClaimed      = state == QuestItemState.Claimed;

            if (lockedOverlay != null)
                lockedOverlay.SetActive(isLocked);

            if (completedOverlay != null)
                completedOverlay.SetActive(isClaimed);

            if (claimButton != null)
                claimButton.gameObject.SetActive(isReadyToClaim);

            // Tint background when locked
            if (cardBackground != null)
                cardBackground.color = isLocked ? lockedTint : _originalBgColor;

            // Update description text based on state
            if (descriptionText != null)
            {
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
