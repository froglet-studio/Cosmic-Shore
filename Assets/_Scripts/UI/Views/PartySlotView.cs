using CosmicShore.Utility.SOAP.ScriptablePartyData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI.Views
{
    /// <summary>
    /// Represents a single player slot in the Arcade Panel's Players Info area.
    /// Each slot can be in one of three states:
    ///   - Empty (shows "+" add button)
    ///   - Occupied (shows player avatar, display name)
    ///   - LocalPlayer (shows local player info, no remove button)
    /// </summary>
    public class PartySlotView : MonoBehaviour
    {
        [Header("Occupied State")]
        [SerializeField] private GameObject occupiedRoot;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text displayNameText;

        [Header("Empty State")]
        [SerializeField] private GameObject emptyRoot;
        [SerializeField] private Button addButton;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        private PartyPlayerData? _assignedPlayer;
        private System.Action _onAddPressed;

        public bool IsOccupied => _assignedPlayer.HasValue;
        public PartyPlayerData? AssignedPlayer => _assignedPlayer;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            addButton?.onClick.AddListener(HandleAddPressed);
        }

        void OnDestroy()
        {
            addButton?.onClick.RemoveListener(HandleAddPressed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Configure this slot with a callback for the "+" button press.
        /// </summary>
        public void Initialize(System.Action onAddPressed)
        {
            _onAddPressed = onAddPressed;
        }

        /// <summary>
        /// Populate this slot with a player's data (local or remote).
        /// </summary>
        public void SetPlayer(PartyPlayerData playerData)
        {
            _assignedPlayer = playerData;

            if (displayNameText != null)
                displayNameText.text = playerData.DisplayName;

            var sprite = ResolveAvatarSprite(playerData.AvatarId);
            if (sprite != null && avatarImage != null)
                avatarImage.sprite = sprite;

            SetOccupiedState(true);
        }

        /// <summary>
        /// Clear this slot back to empty state.
        /// </summary>
        public void ClearSlot()
        {
            _assignedPlayer = null;
            SetOccupiedState(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private
        // ─────────────────────────────────────────────────────────────────────

        private void SetOccupiedState(bool occupied)
        {
            if (occupiedRoot != null) occupiedRoot.SetActive(occupied);
            if (emptyRoot != null) emptyRoot.SetActive(!occupied);
        }

        private void HandleAddPressed()
        {
            _onAddPressed?.Invoke();
        }

        private Sprite ResolveAvatarSprite(int avatarId)
        {
            if (profileIcons == null) return null;
            foreach (var icon in profileIcons.profileIcons)
            {
                if (icon.Id == avatarId)
                    return icon.IconSprite;
            }
            return null;
        }
    }
}
