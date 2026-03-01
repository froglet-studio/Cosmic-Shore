using System;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Individual row in the <see cref="OnlinePlayersPanel"/>.
    /// Shows an online player's avatar + name and a "+" invite button.
    /// Uses <see cref="PartyPlayerData"/> (SOAP struct) as its data model.
    /// </summary>
    public class OnlinePlayerEntry : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Button inviteButton;
        [SerializeField] private GameObject inviteSentIndicator;

        [Header("Friend Request (optional)")]
        [SerializeField] private Button addFriendButton;
        [SerializeField] private GameObject friendRequestSentIndicator;

        private PartyPlayerData _info;
        private Action<PartyPlayerData> _onInvite;
        private Action<PartyPlayerData> _onAddFriend;

        public string PlayerId => _info.PlayerId;

        // ─────────────────────────────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────────────────────────────

        public void Populate(PartyPlayerData info, Sprite avatar, Action<PartyPlayerData> onInvite)
        {
            Populate(info, avatar, onInvite, null, false);
        }

        public void Populate(PartyPlayerData info, Sprite avatar, Action<PartyPlayerData> onInvite,
            Action<PartyPlayerData> onAddFriend, bool isAlreadyFriend)
        {
            _info = info;
            _onInvite = onInvite;
            _onAddFriend = onAddFriend;

            displayNameText.text = info.DisplayName;

            if (avatar != null)
                avatarImage.sprite = avatar;

            inviteButton?.onClick.RemoveAllListeners();
            inviteButton?.onClick.AddListener(OnInvitePressed);

            inviteSentIndicator?.SetVisible(false);

            // Add Friend button setup
            if (addFriendButton != null)
            {
                addFriendButton.onClick.RemoveAllListeners();
                addFriendButton.onClick.AddListener(OnAddFriendPressed);
                addFriendButton.gameObject.SetVisible(onAddFriend != null && !isAlreadyFriend);
                addFriendButton.interactable = true;
            }

            friendRequestSentIndicator?.SetVisible(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────────────────────────────

        private void OnInvitePressed()
        {
            _onInvite?.Invoke(_info);

            if (inviteButton != null)
                inviteButton.interactable = false;

            inviteSentIndicator?.SetVisible(true);
        }

        private void OnAddFriendPressed()
        {
            _onAddFriend?.Invoke(_info);

            if (addFriendButton != null)
                addFriendButton.interactable = false;

            friendRequestSentIndicator?.SetVisible(true);
        }
    }
}
