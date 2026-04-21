using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Row entry for the Requests section of FriendsListPanel.
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

        [Header("Actions")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        [Header("Status Colors (applied to Label Text)")]
        [SerializeField] private Color friendRequestColor = Color.white;
        [SerializeField] private Color partyInviteColor = new(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color expiringSoonColor = new(0.9f, 0.3f, 0.2f, 1f);

        [Header("Entry Animation")]
        [Tooltip("Seconds to fade in on spawn (uses CanvasGroup if present).")]
        [SerializeField] private float entryFadeInSeconds = 0.25f;
        [Tooltip("Duration of the button press punch scale.")]
        [SerializeField] private float buttonPressPunchSeconds = 0.18f;
        [Tooltip("Scale multiplier at the peak of the button press punch.")]
        [SerializeField] private float buttonPressPunchScale = 1.15f;

        CanvasGroup _canvasGroup;

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
            if (!labelText) return;

            string label = _kind == Kind.PartyInvite ? "PARTY INVITE" : "FRIEND REQUEST";
            Color baseColor = _kind == Kind.PartyInvite ? partyInviteColor : friendRequestColor;

            labelText.text = label;

            // Shift toward expiring-soon color as the timer runs out.
            if (_expirationSeconds > 0f)
            {
                float elapsed = Time.unscaledTime - _receivedTime;
                float t = Mathf.Clamp01(elapsed / _expirationSeconds);
                labelText.color = Color.Lerp(baseColor, expiringSoonColor, t);
            }
            else
            {
                labelText.color = baseColor;
            }
        }

        void HandleAcceptClicked()
        {
            if (_responded) return;
            _responded = true;

            if (acceptButton)
                StartCoroutine(PunchButtonScale(acceptButton.transform));

            SetButtonsInteractable(false);
            _onAccept?.Invoke(_playerId);
        }

        void HandleDeclineClicked()
        {
            if (_responded) return;
            _responded = true;

            if (declineButton)
                StartCoroutine(PunchButtonScale(declineButton.transform));

            SetButtonsInteractable(false);
            _onDecline?.Invoke(_playerId);

            // Destroy this entry after a short delay for visual feedback
            Destroy(gameObject, 0.3f);
        }

        void OnEnable()
        {
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            if (entryFadeInSeconds <= 0f) yield break;

            if (!_canvasGroup)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) yield break;

            _canvasGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < entryFadeInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / entryFadeInSeconds);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        IEnumerator PunchButtonScale(Transform target)
        {
            if (!target) yield break;

            Vector3 baseScale = target.localScale;
            Vector3 peakScale = baseScale * Mathf.Max(1.01f, buttonPressPunchScale);
            float half = Mathf.Max(0.02f, buttonPressPunchSeconds * 0.5f);
            float elapsed = 0f;

            // Up
            while (elapsed < half && target)
            {
                elapsed += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(baseScale, peakScale, elapsed / half);
                yield return null;
            }
            if (!target) yield break;

            elapsed = 0f;
            // Down
            while (elapsed < half && target)
            {
                elapsed += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(peakScale, baseScale, elapsed / half);
                yield return null;
            }
            if (target) target.localScale = baseScale;
        }

        void SetButtonsInteractable(bool interactable)
        {
            if (acceptButton) acceptButton.interactable = interactable;
            if (declineButton) declineButton.interactable = interactable;
        }
    }
}
