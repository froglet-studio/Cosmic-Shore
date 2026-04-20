using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Row entry for the Requests tab of FriendsListPanel.
    /// Handles both friend requests and incoming party invites.
    /// Shows avatar, name, status label (e.g. "PARTY INVITE" / "FRIEND REQUEST"),
    /// and Accept/Decline buttons.
    /// </summary>
    public class RequestInfoEntry : MonoBehaviour
    {
        public enum Kind { FriendRequest, PartyInvite }

        [Header("Display")]
        [SerializeField] private Image avatarIcon;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image labelStatus;

        [Header("Actions")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private GameObject pendingState;

        [Header("Status Colors")]
        [SerializeField] private Color friendRequestColor = Color.white;
        [SerializeField] private Color partyInviteColor = new(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color expiringSoonColor = new(0.9f, 0.3f, 0.2f, 1f);

        string _playerId;
        Kind _kind;
        float _receivedTime;
        float _expirationSeconds;
        Action<string> _onAccept;
        Action<string> _onDecline;
        bool _responded;

        /// <summary>The player ID this request is from.</summary>
        public string PlayerId => _playerId;

        /// <summary>What kind of request this row represents.</summary>
        public Kind EntryKind => _kind;

        /// <summary>
        /// Populates the entry. Supports friend requests and party invites.
        /// </summary>
        /// <param name="playerId">Requester's player ID.</param>
        /// <param name="displayName">Requester's display name.</param>
        /// <param name="avatar">Avatar sprite (may be null).</param>
        /// <param name="kind">FriendRequest or PartyInvite.</param>
        /// <param name="expirationSeconds">Seconds until auto-decline (0 = no expiry).</param>
        /// <param name="onAccept">Callback when accept is pressed.</param>
        /// <param name="onDecline">Callback when decline is pressed.</param>
        public void Populate(
            string playerId,
            string displayName,
            Sprite avatar,
            Kind kind,
            float expirationSeconds,
            Action<string> onAccept,
            Action<string> onDecline)
        {
            _playerId = playerId;
            _kind = kind;
            _receivedTime = Time.unscaledTime;
            _expirationSeconds = expirationSeconds;
            _onAccept = onAccept;
            _onDecline = onDecline;
            _responded = false;

            if (usernameText)
                usernameText.text = displayName ?? "Unknown";

            if (avatarIcon)
            {
                avatarIcon.sprite = avatar;
                avatarIcon.enabled = avatar != null;
            }

            if (acceptButton)
            {
                acceptButton.gameObject.SetActive(true);
                acceptButton.interactable = true;
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(HandleAcceptClicked);
            }

            if (declineButton)
            {
                declineButton.gameObject.SetActive(true);
                declineButton.interactable = true;
                declineButton.onClick.RemoveAllListeners();
                declineButton.onClick.AddListener(HandleDeclineClicked);
            }

            if (pendingState)
                pendingState.SetActive(false);

            UpdateStatusLabel();
        }

        void Update()
        {
            if (_responded) return;
            if (_expirationSeconds <= 0f) return;

            float elapsed = Time.unscaledTime - _receivedTime;

            // Auto-decline on expiration
            if (elapsed >= _expirationSeconds)
            {
                HandleDeclineClicked();
                return;
            }

            UpdateStatusLabel();
        }

        void UpdateStatusLabel()
        {
            string label = _kind == Kind.PartyInvite ? "PARTY INVITE" : "FRIEND REQUEST";
            Color baseColor = _kind == Kind.PartyInvite ? partyInviteColor : friendRequestColor;

            if (labelText) labelText.text = label;

            if (labelStatus)
            {
                // Shift toward expiring-soon color as the timer runs out.
                if (_expirationSeconds > 0f)
                {
                    float elapsed = Time.unscaledTime - _receivedTime;
                    float t = Mathf.Clamp01(elapsed / _expirationSeconds);
                    labelStatus.color = Color.Lerp(baseColor, expiringSoonColor, t);
                }
                else
                {
                    labelStatus.color = baseColor;
                }
            }
        }

        void HandleAcceptClicked()
        {
            if (_responded) return;
            _responded = true;

            SetButtonsInteractable(false);
            _onAccept?.Invoke(_playerId);
        }

        void HandleDeclineClicked()
        {
            if (_responded) return;
            _responded = true;

            SetButtonsInteractable(false);
            _onDecline?.Invoke(_playerId);

            // Destroy this entry after a short delay for visual feedback
            Destroy(gameObject, 0.3f);
        }

        void SetButtonsInteractable(bool interactable)
        {
            if (acceptButton) acceptButton.interactable = interactable;
            if (declineButton) declineButton.interactable = interactable;
        }
    }
}
