using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Row entry for the Online tab of FriendsListPanel.
    /// Shows avatar, username, lobby/match status, and acts as the invite button
    /// (the row background is the button). Click sends an invite, tinting the row
    /// yellowish until the target accepts/declines/times out.
    /// Also has a separate Add Friend button hidden when already friends.
    /// </summary>
    public class OnlineInfoEntry : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Image avatarIcon;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image labelStatus;

        [Header("Invite (whole-row button)")]
        [Tooltip("The row background image. Acts as the invite button and receives " +
                 "the yellowish tint while an invite is pending.")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Button on the row background. Click sends an invite.")]
        [SerializeField] private Button inviteButton;

        [Header("Add Friend")]
        [SerializeField] private Button addFriendButton;
        [SerializeField] private GameObject pendingState;

        [Header("Status Colors")]
        [SerializeField] private Color onlineColor = Color.white;
        [SerializeField] private Color inLobbyColor = new(0.4f, 0.8f, 1f, 1f);
        [SerializeField] private Color inMatchColor = new(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color lobbyFullColor = new(0.5f, 0.5f, 0.5f, 1f);

        [Header("Row Tints")]
        [Tooltip("Background tint when no invite is pending.")]
        [SerializeField] private Color defaultTint = Color.white;
        [Tooltip("Background tint while an invite is in-flight (awaiting response).")]
        [SerializeField] private Color pendingInviteTint = new(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Background tint when this row cannot be invited (in-match / lobby-full).")]
        [SerializeField] private Color disabledTint = new(0.35f, 0.35f, 0.35f, 1f);

        public enum Status { Online, InLobby, InMatch, LobbyFull }

        string _playerId;
        Action<string> _onAddFriend;
        Action<string> _onInvite;
        bool _invitable;

        public string PlayerId => _playerId;

        /// <summary>
        /// Populates the entry with online player data.
        /// </summary>
        /// <param name="playerId">Remote player's UGS player ID.</param>
        /// <param name="displayName">Display name shown next to avatar.</param>
        /// <param name="avatar">Resolved avatar sprite (may be null).</param>
        /// <param name="status">High-level status bucket.</param>
        /// <param name="partyMemberCount">Members in their party (for InLobby/LobbyFull).</param>
        /// <param name="partyMaxSlots">Max party slots (for InLobby/LobbyFull rendering).</param>
        /// <param name="matchName">Match name text (for InMatch status).</param>
        /// <param name="isAlreadyFriend">If true, Add Friend button is hidden.</param>
        /// <param name="onAddFriend">Callback when Add Friend button is pressed (null hides it).</param>
        /// <param name="onInvite">Callback when the row background is clicked (null disables).</param>
        public void Populate(
            string playerId,
            string displayName,
            Sprite avatar,
            Status status,
            int partyMemberCount,
            int partyMaxSlots,
            string matchName,
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

            SetStatus(status, partyMemberCount, partyMaxSlots, matchName);

            // Row-background invite button. Enabled only when the status permits
            // invites (Online / InLobby) and a callback is provided.
            _invitable = onInvite != null &&
                         (status == Status.Online || status == Status.InLobby);

            if (inviteButton)
            {
                inviteButton.interactable = _invitable;
                inviteButton.onClick.RemoveAllListeners();
                if (_invitable)
                    inviteButton.onClick.AddListener(HandleInviteClicked);
            }

            ApplyRowTint(_invitable ? defaultTint : disabledTint);

            // Add Friend button — hidden if already a friend or no callback
            if (addFriendButton)
            {
                bool showAdd = !isAlreadyFriend && onAddFriend != null;
                addFriendButton.gameObject.SetActive(showAdd);
                addFriendButton.interactable = true;
                addFriendButton.onClick.RemoveAllListeners();
                if (showAdd)
                    addFriendButton.onClick.AddListener(HandleAddFriendClicked);
            }

            if (pendingState)
                pendingState.SetActive(false);
        }

        public void SetStatus(Status status, int partyMemberCount = 0, int partyMaxSlots = 0, string matchName = null)
        {
            string text;
            Color color;

            switch (status)
            {
                case Status.InLobby:
                    text = partyMaxSlots > 0
                        ? $"IN LOBBY {partyMemberCount}/{partyMaxSlots}"
                        : "IN LOBBY";
                    color = inLobbyColor;
                    break;
                case Status.LobbyFull:
                    text = "LOBBY FULL";
                    color = lobbyFullColor;
                    break;
                case Status.InMatch:
                    text = string.IsNullOrEmpty(matchName)
                        ? "IN A MATCH"
                        : $"IN A MATCH — {matchName.ToUpperInvariant()}";
                    color = inMatchColor;
                    break;
                default:
                    text = "ONLINE";
                    color = onlineColor;
                    break;
            }

            if (labelText) labelText.text = text;
            if (labelStatus) labelStatus.color = color;
        }

        /// <summary>
        /// Marks the row as "invite pending" — tints the background yellowish and
        /// disables further invite clicks until reset.
        /// </summary>
        public void SetInvitePending()
        {
            if (inviteButton) inviteButton.interactable = false;
            ApplyRowTint(pendingInviteTint);
        }

        /// <summary>Restores the row to its post-populate state.</summary>
        public void ResetInviteState()
        {
            if (inviteButton) inviteButton.interactable = _invitable;
            ApplyRowTint(_invitable ? defaultTint : disabledTint);
        }

        public void ResetAddFriendState()
        {
            if (addFriendButton) addFriendButton.interactable = true;
            if (pendingState) pendingState.SetActive(false);
        }

        void ApplyRowTint(Color c)
        {
            if (backgroundImage) backgroundImage.color = c;
        }

        void HandleAddFriendClicked()
        {
            if (addFriendButton) addFriendButton.interactable = false;
            if (pendingState) pendingState.SetActive(true);
            _onAddFriend?.Invoke(_playerId);
        }

        void HandleInviteClicked()
        {
            if (!_invitable) return;
            SetInvitePending();
            _onInvite?.Invoke(_playerId);
        }
    }
}
