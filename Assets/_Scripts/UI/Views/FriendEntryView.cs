using System;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Individual row in the friends list panel.
    /// Shows a friend's display name, online status indicator, and an invite-to-party button.
    /// </summary>
    public class FriendEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image onlineIndicator;
        [SerializeField] private Button inviteButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private GameObject inviteSentIndicator;

        [Header("Online Indicator Colors")]
        [SerializeField] private Color onlineColor = new(0.2f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color offlineColor = new(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color busyColor = new(0.9f, 0.7f, 0.2f, 1f);

        private FriendData _data;
        private Action<FriendData> _onInvite;
        private Action<FriendData> _onRemove;

        public string PlayerId => _data.PlayerId;

        // ─────────────────────────────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────────────────────────────

        public void Populate(FriendData data, Action<FriendData> onInvite, Action<FriendData> onRemove)
        {
            _data = data;
            _onInvite = onInvite;
            _onRemove = onRemove;

            if (displayNameText != null)
                displayNameText.text = data.DisplayName;

            UpdateStatus(data);

            inviteButton?.onClick.RemoveAllListeners();
            inviteButton?.onClick.AddListener(OnInvitePressed);

            removeButton?.onClick.RemoveAllListeners();
            removeButton?.onClick.AddListener(OnRemovePressed);

            inviteSentIndicator?.SetActive(false);

            // Only show invite button for online friends
            if (inviteButton != null)
            {
                inviteButton.gameObject.SetActive(data.IsOnline);
                inviteButton.interactable = true;
            }
        }

        public void UpdateStatus(FriendData data)
        {
            _data = data;

            if (onlineIndicator != null)
            {
                onlineIndicator.color = data.Availability switch
                {
                    1 => onlineColor, // Online
                    2 => busyColor,   // Busy
                    3 => busyColor,   // Away
                    _ => offlineColor
                };
            }

            if (statusText != null)
            {
                statusText.text = data.Availability switch
                {
                    1 => string.IsNullOrEmpty(data.ActivityStatus) ? "Online" : data.ActivityStatus,
                    2 => "Busy",
                    3 => "Away",
                    5 => "Offline",
                    _ => "Offline"
                };
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────────────────────────────

        private void OnInvitePressed()
        {
            _onInvite?.Invoke(_data);

            if (inviteButton != null)
                inviteButton.interactable = false;

            inviteSentIndicator?.SetActive(true);
        }

        private void OnRemovePressed()
        {
            _onRemove?.Invoke(_data);
        }
    }
}
