using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A single slot in the FriendsListPanel. Can be in one of three states:
    /// - LocalPlayer: shows the current user's avatar, add button hidden
    /// - Occupied: shows a party member's avatar and name
    /// - Empty: shows the "+" add button
    /// </summary>
    public class FriendInfoSlot : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image avatarIcon;
        [SerializeField] private Button addButton;
        [SerializeField] private TMP_Text displayNameText;
        [Tooltip("Kick (✕) button - shown only on an occupied REMOTE member slot when " +
                 "the local player is the party host. Click removes that member from the party.")]
        [SerializeField] private Button kickButton;

        string _playerId;
        bool _isLocalPlayer;
        Action<string> _onKick;

        /// <summary>Whether this slot has a player assigned.</summary>
        public bool IsOccupied => !string.IsNullOrEmpty(_playerId);

        /// <summary>The player ID assigned to this slot, or null if empty.</summary>
        public string PlayerId => _playerId;

        /// <summary>Whether this is the local player's slot (slot 0).</summary>
        public bool IsLocalPlayer => _isLocalPlayer;

        /// <summary>Underlying GameObject of the display-name text - used by
        /// container widgets to detect shared-reference wiring bugs.</summary>
        public GameObject DisplayNameTextGO => displayNameText ? displayNameText.gameObject : null;

        /// <summary>Underlying GameObject of the avatar icon - used by
        /// container widgets to detect shared-reference wiring bugs.</summary>
        public GameObject AvatarIconGO => avatarIcon ? avatarIcon.gameObject : null;

        /// <summary>
        /// Configures this slot as the local player's slot.
        /// The add button is always hidden; avatar and name are shown.
        /// </summary>
        public void SetAsLocalPlayer(string playerId, string displayName, Sprite avatar)
        {
            _playerId = playerId;
            _isLocalPlayer = true;

            SetAvatar(avatar);
            SetDisplayName(displayName);

            if (addButton)
                addButton.gameObject.SetActive(false);

            // You can never kick yourself - the local slot has no ✕.
            if (kickButton)
                kickButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Populates this slot with a party member's data.
        /// The add button is hidden; avatar and name are shown. The kick (✕) button is
        /// shown only when <paramref name="canKick"/> is true (host viewing a remote member).
        /// </summary>
        public void SetPlayer(string playerId, string displayName, Sprite avatar, bool canKick = false)
        {
            _playerId = playerId;
            _isLocalPlayer = false;

            SetAvatar(avatar);
            SetDisplayName(displayName);

            if (addButton)
                addButton.gameObject.SetActive(false);

            if (kickButton)
            {
                kickButton.gameObject.SetActive(canKick);
                kickButton.interactable = true;
            }
        }

        /// <summary>
        /// Clears this slot to the empty state.
        /// The add button is shown; avatar and name are hidden.
        /// </summary>
        public void ClearSlot()
        {
            _playerId = null;
            _isLocalPlayer = false;

            if (avatarIcon)
            {
                avatarIcon.sprite = null;
                avatarIcon.enabled = false;
                avatarIcon.gameObject.SetActive(false);
            }

            if (displayNameText)
            {
                displayNameText.text = string.Empty;
                displayNameText.enabled = false;
                displayNameText.gameObject.SetActive(false);
            }

            if (addButton)
                addButton.gameObject.SetActive(true);

            if (kickButton)
                kickButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Wires the add button's onClick to the given callback.
        /// Call once during initialization.
        /// </summary>
        public void BindAddButton(UnityEngine.Events.UnityAction onClicked)
        {
            if (!addButton) return;
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(onClicked);
        }

        /// <summary>
        /// Wires the kick button's onClick to remove the player currently shown in this
        /// slot. Call once during initialization; the callback receives this slot's live
        /// player ID at click time. The button stays hidden until a kickable member is
        /// assigned via <see cref="SetPlayer"/> with <c>canKick: true</c>.
        /// </summary>
        public void BindKickButton(Action<string> onKick)
        {
            _onKick = onKick;
            if (!kickButton) return;
            kickButton.onClick.RemoveAllListeners();
            kickButton.onClick.AddListener(HandleKickClicked);
            kickButton.gameObject.SetActive(false);
        }

        void HandleKickClicked()
        {
            if (string.IsNullOrEmpty(_playerId)) return;
            // Anti-spam: disable until the party refresh re-populates this slot
            // (KickPartyMemberAsync removes the member → PopulateSlots re-runs).
            if (kickButton) kickButton.interactable = false;
            _onKick?.Invoke(_playerId);
        }

        void SetAvatar(Sprite sprite)
        {
            if (!avatarIcon) return;

            // Toggle the GameObject (not just Image.enabled) because empty-slot
            // prefab instances ship with these children inactive so the "+" add
            // button shows through a cleanly-laid-out empty cell. Without the
            // GameObject flip, SetPlayer enables the Image component but the
            // GameObject stays inactive and the avatar never appears.
            if (sprite)
            {
                avatarIcon.sprite = sprite;
                avatarIcon.enabled = true;
                avatarIcon.gameObject.SetActive(true);
            }
            else
            {
                avatarIcon.enabled = false;
                avatarIcon.gameObject.SetActive(false);
            }
        }

        void SetDisplayName(string name)
        {
            if (!displayNameText) return;

            // Occupied slot - always surface *something* so the text GameObject
            // doesn't stay inactive and swallow the label. Empty/null names can
            // arrive transiently when a remote member's DISPLAY_NAME_KEY
            // property is still propagating; show a "Pilot" placeholder until
            // the next party refresh overwrites it.
            displayNameText.text = string.IsNullOrEmpty(name) ? "Pilot" : name;
            displayNameText.enabled = true;
            displayNameText.gameObject.SetActive(true);
        }
    }
}
