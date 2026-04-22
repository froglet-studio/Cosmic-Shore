using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Row entry for the Friends tab of FriendsListPanel.
    /// Shows avatar, username, online status, and invite button.
    /// These are confirmed friends — no add-friend button needed.
    /// </summary>
    public class FriendInfoEntry : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Image avatarIcon;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image labelStatus;

        [Header("Actions")]
        [SerializeField] private Button inviteButton;
        [SerializeField] private GameObject pendingState;

        [Header("Status Colors")]
        [SerializeField] private Color onlineColor = Color.white;
        [SerializeField] private Color offlineColor = new(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color inMatchColor = new(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color inPartyColor = new(0.2f, 0.7f, 1f, 1f);

        public enum OnlineStatus { Online, Offline, InMatch, InParty }

        string _playerId;
        Action<string> _onInvite;

        public string PlayerId => _playerId;

        /// <summary>
        /// Populates the entry with friend data.
        /// </summary>
        public void Populate(
            string playerId,
            string displayName,
            Sprite avatar,
            OnlineStatus status,
            Action<string> onInvite)
        {
            _playerId = playerId;
            _onInvite = onInvite;

            if (usernameText)
                usernameText.text = displayName ?? "Unknown";

            if (avatarIcon)
            {
                avatarIcon.sprite = avatar;
                avatarIcon.enabled = avatar != null;
            }

            SetStatus(status);

            // Invite button — only shown for online/in-match friends
            if (inviteButton)
            {
                bool showInvite = onInvite != null && status != OnlineStatus.Offline;
                inviteButton.gameObject.SetActive(showInvite);
                inviteButton.interactable = true;
                inviteButton.onClick.RemoveAllListeners();
                if (showInvite)
                    inviteButton.onClick.AddListener(HandleInviteClicked);
            }

            if (pendingState)
                pendingState.SetActive(false);
        }

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
                case OnlineStatus.InParty:
                    text = "In a Party";
                    color = inPartyColor;
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
            if (inviteButton) inviteButton.interactable = true;
            if (pendingState) pendingState.SetActive(false);
        }

        void HandleInviteClicked()
        {
            if (inviteButton) inviteButton.interactable = false;
            if (pendingState) pendingState.SetActive(true);
            _onInvite?.Invoke(_playerId);
        }
    }
}
