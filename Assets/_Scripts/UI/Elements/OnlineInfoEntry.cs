using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Row entry for the Online tab of FriendsListPanel.
    /// Shows avatar, username, online status, add-friend button,
    /// invite button, and pending state.
    /// Only online players appear here (never the current user).
    /// </summary>
    public class OnlineInfoEntry : MonoBehaviour
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

        public enum Status { Online, Offline, InMatch }

        string _playerId;
        Action<string> _onAddFriend;
        Action<string> _onInvite;

        public string PlayerId => _playerId;

        /// <summary>
        /// Populates the entry with online player data.
        /// </summary>
        public void Populate(
            string playerId,
            string displayName,
            Sprite avatar,
            Status status,
            bool isAlreadyFriend,
            Action<string> onAddFriend,
            Action<string> onInvite)
        {
            _playerId = playerId;
            _onAddFriend = onAddFriend;
            _onInvite = onInvite;

            if (usernameText)
                usernameText.text = displayName ?? "Unknown";

            if (avatarIcon)
            {
                avatarIcon.sprite = avatar;
                avatarIcon.enabled = avatar != null;
            }

            SetStatus(status);

            // Add Friend — hidden if already a friend or no callback
            if (addFriendButton)
            {
                bool showAdd = !isAlreadyFriend && onAddFriend != null;
                addFriendButton.gameObject.SetActive(showAdd);
                addFriendButton.interactable = true;
                addFriendButton.onClick.RemoveAllListeners();
                if (showAdd)
                    addFriendButton.onClick.AddListener(HandleAddFriendClicked);
            }

            // Invite
            if (inviteFriendButton)
            {
                bool showInvite = onInvite != null;
                inviteFriendButton.gameObject.SetActive(showInvite);
                inviteFriendButton.interactable = true;
                inviteFriendButton.onClick.RemoveAllListeners();
                if (showInvite)
                    inviteFriendButton.onClick.AddListener(HandleInviteClicked);
            }

            if (pendingState)
                pendingState.SetActive(false);
        }

        public void SetStatus(Status status)
        {
            string text;
            Color color;

            switch (status)
            {
                case Status.Online:
                    text = "Online";
                    color = onlineColor;
                    break;
                case Status.InMatch:
                    text = "In a Match";
                    color = inMatchColor;
                    break;
                default:
                    text = "Offline";
                    color = offlineColor;
                    break;
            }

            if (labelText) labelText.text = text;
            if (labelStatus) labelStatus.color = color;
        }

        public void ResetInviteState()
        {
            if (inviteFriendButton) inviteFriendButton.interactable = true;
            if (pendingState) pendingState.SetActive(false);
        }

        public void ResetAddFriendState()
        {
            if (addFriendButton) addFriendButton.interactable = true;
            if (pendingState) pendingState.SetActive(false);
        }

        void HandleAddFriendClicked()
        {
            if (addFriendButton) addFriendButton.interactable = false;
            if (pendingState) pendingState.SetActive(true);
            _onAddFriend?.Invoke(_playerId);
        }

        void HandleInviteClicked()
        {
            if (inviteFriendButton) inviteFriendButton.interactable = false;
            if (pendingState) pendingState.SetActive(true);
            _onInvite?.Invoke(_playerId);
        }
    }
}
