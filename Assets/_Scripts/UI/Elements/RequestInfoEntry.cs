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
    ///
    /// <para>The same prefab also serves the RECENT section (<see cref="Kind.RecentPlayer"/>,
    /// <see cref="PopulateRecentPlayer"/>): the Accept glyph becomes the row's add-friend button,
    /// Decline is hidden, the label is whatever the panel resolved for that pilot ("PLAYED 5 MIN
    /// AGO", "FRIENDS", "REQUEST SENT") and there is no expiry.</para>
    /// </summary>
    public class RequestInfoEntry : MonoBehaviour
    {
        public enum Kind { FriendRequest = 0, PartyInvite = 1, RecentPlayer = 2 }

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
        [Tooltip("Label colour on a RECENT row that still offers the add-friend button (\"PLAYED ... AGO\").")]
        [SerializeField] private Color recentPlayerColor = new(0.4f, 0.8f, 1f, 1f);
        [Tooltip("Label colour on a RECENT row whose relationship is settled and shows no button (\"FRIENDS\", \"REQUEST SENT\").")]
        [SerializeField] private Color recentSettledColor = new(0.6f, 0.9f, 0.6f, 1f);

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
        string _recentLabel;

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

        /// <summary>
        /// Populates the row for the RECENT section: a pilot from a past online match. The Accept
        /// glyph is the add-friend button and is shown only when <paramref name="onAddFriend"/> is
        /// given (a pilot who is already a friend, or already asked, shows the state in the label
        /// instead); Decline is hidden; nothing expires.
        /// </summary>
        /// <param name="playerId">The pilot's UGS player id (what the friend request goes to).</param>
        /// <param name="displayName">The name they flew under.</param>
        /// <param name="avatar">Avatar sprite (may be null).</param>
        /// <param name="label">The resolved status: "PLAYED 5 MIN AGO", "FRIENDS", "REQUEST SENT".</param>
        /// <param name="onAddFriend">Add-friend callback, or null for a settled row with no button.</param>
        public void PopulateRecentPlayer(
            string playerId,
            string displayName,
            Sprite avatar,
            string label,
            Action<string> onAddFriend)
        {
            _playerId = playerId;
            _kind = Kind.RecentPlayer;
            _receivedTime = Time.unscaledTime;
            _expirationSeconds = 0f;
            _onAccept = onAddFriend;
            _onDecline = null;
            _responded = false;
            _recentLabel = label ?? string.Empty;

            if (usernameText)
                usernameText.text = displayName ?? "Unknown";

            if (avatarIcon)
            {
                avatarIcon.sprite = avatar;
                avatarIcon.enabled = avatar != null;
            }

            if (acceptButton)
            {
                acceptButton.gameObject.SetActive(onAddFriend != null);
                acceptButton.interactable = onAddFriend != null;
                acceptButton.onClick.RemoveAllListeners();
                if (onAddFriend != null)
                    acceptButton.onClick.AddListener(HandleAcceptClicked);
            }

            if (declineButton)
            {
                declineButton.onClick.RemoveAllListeners();
                declineButton.gameObject.SetActive(false);
            }

            UpdateStatusLabel();
        }

        void Update()
        {
            if (_responded) return;
            if (_expirationSeconds <= 0f) return;

            float elapsed = Time.unscaledTime - _receivedTime;

            // EXPIRE, never decline. "You did not answer in time" and "you pressed Decline" are
            // different events, and firing the decline callback made the row an ACTIVE refusal:
            // it deleted the request server-side and told the inviter no. On a long link that is
            // the join killer - the recipient's clock starts when their lobby POLL observes the
            // invite (~0.75-1.5s refresh plus RTT plus any 429 backoff), so a player far from the
            // host could have the row auto-refuse under their finger while they were reaching for
            // Accept. The host clears its own outgoing invite on its own timeout, so letting the
            // row simply go is both sides' correct behaviour.
            if (elapsed >= _expirationSeconds)
            {
                HandleExpired();
                return;
            }

            UpdateStatusLabel();
        }

        void UpdateStatusLabel()
        {
            if (!labelText) return;

            if (_kind == Kind.RecentPlayer)
            {
                labelText.text = _recentLabel;
                labelText.color = _onAccept != null ? recentPlayerColor : recentSettledColor;
                return;
            }

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

        /// <summary>
        /// The row ran out of time. Removes it WITHOUT answering - see the note at the call
        /// site. The same fade-out as a decline so it does not pop (nothing in this game
        /// vanishes without a transition), but no callback and no server-side refusal.
        /// </summary>
        void HandleExpired()
        {
            if (_responded) return;
            _responded = true;

            SetButtonsInteractable(false);
            Destroy(gameObject, 0.3f);
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
