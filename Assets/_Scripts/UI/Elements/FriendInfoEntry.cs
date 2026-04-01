using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Row entry used in the Online and Friends tabs of FriendsListPanel.
    /// Displays avatar, username, status label with color indicator,
    /// add-friend button, invite button, and pending state.
    /// </summary>
    public class FriendInfoEntry : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Image avatarIcon;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image labelStatus;

        [Header("Actions")]
        [SerializeField] private Button addFriendButton;
        [SerializeField] private Button inviteFriendButton;
        [SerializeField] private GameObject pendingState;

        [Header("Status Colors")]
        [SerializeField] private Color onlineColor = Color.white;
        [SerializeField] private Color offlineColor = new(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color inMatchColor = new(0.9f, 0.2f, 0.2f, 1f);

        public enum OnlineStatus { Online, Offline, InMatch }

        string _playerId;
        Action<string> _onAddFriend;
        Action<string> _onInvite;

        /// <summary>The player ID this entry represents.</summary>
        public string PlayerId => _playerId;

        /// <summary>
        /// Populates the entry with player data and wires button callbacks.
        /// </summary>
        /// <param name="playerId">UGS player ID</param>
        /// <param name="displayName">Player display name</param>
        /// <param name="avatar">Avatar sprite (null hides the icon)</param>
        /// <param name="status">Online presence status</param>
        /// <param name="isAlreadyFriend">If true, hides the add-friend button</param>
        /// <param name="onAddFriend">Callback when add-friend is pressed. Null hides the button.</param>
        /// <param name="onInvite">Callback when invite is pressed. Null hides the button.</param>
        public void Populate(
            string playerId,
            string displayName,
            Sprite avatar,
            OnlineStatus status,
            bool isAlreadyFriend,
            Action<string> onAddFriend,
            Action<string> onInvite)
        {
            _playerId = playerId;
            _onAddFriend = onAddFriend;
            _onInvite = onInvite;

            // Display
            if (usernameText)
                usernameText.text = displayName ?? "Unknown";

            if (avatarIcon)
            {
                avatarIcon.sprite = avatar;
                avatarIcon.enabled = avatar != null;
            }

            SetStatus(status);

            // Add Friend button — hidden if already a friend or callback is null
            if (addFriendButton)
            {
                bool showAdd = !isAlreadyFriend && onAddFriend != null;
                addFriendButton.gameObject.SetActive(showAdd);
                addFriendButton.interactable = true;
                addFriendButton.onClick.RemoveAllListeners();
                if (showAdd)
                    addFriendButton.onClick.AddListener(HandleAddFriendClicked);
            }

            // Invite button
            if (inviteFriendButton)
            {
                bool showInvite = onInvite != null;
                inviteFriendButton.gameObject.SetActive(showInvite);
                inviteFriendButton.interactable = true;
                inviteFriendButton.onClick.RemoveAllListeners();
                if (showInvite)
                    inviteFriendButton.onClick.AddListener(HandleInviteClicked);
            }

            // Pending state hidden by default
            if (pendingState)
                pendingState.SetActive(false);
        }

        /// <summary>Updates the status label and color indicator.</summary>
        public void SetStatus(OnlineStatus status)
        {
            string text;
            Color color;

            switch (status)
            {
                case OnlineStatus.Online:
                    text = "Online";
                    color = onlineColor;
                    break;
                case OnlineStatus.InMatch:
                    text = "In a Match";
                    color = inMatchColor;
                    break;
                default:
                    text = "Offline";
                    color = offlineColor;
                    break;
            }

            if (labelText)
                labelText.text = text;

            if (labelStatus)
                labelStatus.color = color;
        }

        /// <summary>
        /// Shows the invite button again (e.g. after an invite was rejected).
        /// </summary>
        public void ResetInviteState()
        {
            if (inviteFriendButton)
                inviteFriendButton.interactable = true;
            if (pendingState)
                pendingState.SetActive(false);
        }

        /// <summary>
        /// Shows the add-friend button again (e.g. after a request was declined).
        /// </summary>
        public void ResetAddFriendState()
        {
            if (addFriendButton)
                addFriendButton.interactable = true;
            if (pendingState)
                pendingState.SetActive(false);
        }

        void HandleAddFriendClicked()
        {
            if (addFriendButton)
                addFriendButton.interactable = false;
            if (pendingState)
                pendingState.SetActive(true);

            _onAddFriend?.Invoke(_playerId);
        }

        void HandleInviteClicked()
        {
            if (inviteFriendButton)
                inviteFriendButton.interactable = false;
            if (pendingState)
                pendingState.SetActive(true);

            _onInvite?.Invoke(_playerId);
        }
    }
}
