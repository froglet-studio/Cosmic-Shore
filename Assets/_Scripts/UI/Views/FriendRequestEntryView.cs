using System;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Individual row for a friend request (incoming or outgoing).
    /// Incoming requests show Accept/Decline buttons.
    /// Outgoing requests show a Cancel button.
    /// </summary>
    public class FriendRequestEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private TMP_Text directionLabel;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button cancelButton;

        private FriendData _data;
        private Action<FriendData> _onAccept;
        private Action<FriendData> _onDecline;
        private Action<FriendData> _onCancel;

        public string PlayerId => _data.PlayerId;

        // ─────────────────────────────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Configure as an incoming friend request (accept/decline).
        /// </summary>
        public void PopulateIncoming(FriendData data, Action<FriendData> onAccept, Action<FriendData> onDecline)
        {
            _data = data;
            _onAccept = onAccept;
            _onDecline = onDecline;

            if (displayNameText != null)
                displayNameText.text = data.DisplayName;

            if (directionLabel != null)
                directionLabel.text = "Incoming";

            SetButtonStates(showAcceptDecline: true, showCancel: false);

            acceptButton?.onClick.RemoveAllListeners();
            acceptButton?.onClick.AddListener(OnAcceptPressed);

            declineButton?.onClick.RemoveAllListeners();
            declineButton?.onClick.AddListener(OnDeclinePressed);
        }

        /// <summary>
        /// Configure as an outgoing friend request (cancel).
        /// </summary>
        public void PopulateOutgoing(FriendData data, Action<FriendData> onCancel)
        {
            _data = data;
            _onCancel = onCancel;

            if (displayNameText != null)
                displayNameText.text = data.DisplayName;

            if (directionLabel != null)
                directionLabel.text = "Sent";

            SetButtonStates(showAcceptDecline: false, showCancel: true);

            cancelButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.AddListener(OnCancelPressed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────────────────────────────

        private void OnAcceptPressed()
        {
            SetButtonStates(showAcceptDecline: false, showCancel: false);
            // Satisfying scale pop on accept
            transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 4, 0.5f);
            _onAccept?.Invoke(_data);
        }

        private void OnDeclinePressed()
        {
            // Fade out on decline
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
            SetButtonStates(showAcceptDecline: false, showCancel: false);
            _onDecline?.Invoke(_data);
        }

        private void OnCancelPressed()
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
            SetButtonStates(showAcceptDecline: false, showCancel: false);
            _onCancel?.Invoke(_data);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void SetButtonStates(bool showAcceptDecline, bool showCancel)
        {
            if (acceptButton != null)
                acceptButton.gameObject.SetActive(showAcceptDecline);

            if (declineButton != null)
                declineButton.gameObject.SetActive(showAcceptDecline);

            if (cancelButton != null)
                cancelButton.gameObject.SetActive(showCancel);
        }
    }
}
