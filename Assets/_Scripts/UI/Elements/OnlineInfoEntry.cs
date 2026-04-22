using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Row entry for the Online section of FriendsListPanel.
    /// Shows avatar, username, lobby/match status, and acts as the invite button
    /// (the row background is the button). Click sends an invite, tinting the row
    /// yellowish and pulsing until the target accepts/declines/times out.
    /// </summary>
    public class OnlineInfoEntry : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Image avatarIcon;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_Text labelText;

        [Header("Invite (whole-row button)")]
        [Tooltip("The row background image. Acts as the invite button and receives " +
                 "the yellowish tint while an invite is pending.")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Button on the row background. Click sends an invite.")]
        [SerializeField] private Button inviteButton;

        [Header("Status Colors (applied to Label Text)")]
        [SerializeField] private Color onlineColor = Color.white;
        [SerializeField] private Color inLobbyColor = new(0.4f, 0.8f, 1f, 1f);
        [SerializeField] private Color inMatchColor = new(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color lobbyFullColor = new(0.5f, 0.5f, 0.5f, 1f);

        [Header("Row Tints")]
        [Tooltip("Background tint when no invite is pending.")]
        [SerializeField] private Color defaultTint = Color.white;
        [Tooltip("Background tint while an invite is in-flight (awaiting response). Pulses between this and pendingInviteTintBright.")]
        [SerializeField] private Color pendingInviteTint = new(1f, 0.75f, 0.1f, 1f);
        [Tooltip("Bright end of the pending-tint pulse.")]
        [SerializeField] private Color pendingInviteTintBright = new(1f, 0.95f, 0.5f, 1f);
        [Tooltip("Background tint when this row cannot be invited (in-match / lobby-full).")]
        [SerializeField] private Color disabledTint = new(0.35f, 0.35f, 0.35f, 1f);

        [Header("Pending Pulse")]
        [Tooltip("Seconds for one full pulse cycle (default→bright→default).")]
        [SerializeField] private float pendingPulsePeriodSeconds = 1.1f;
        [Tooltip("Text label shown while invite is pending.")]
        [SerializeField] private string pendingRequestLabel = "PENDING REQUEST";

        [Header("Entry Animation")]
        [Tooltip("Seconds to fade in on spawn (uses CanvasGroup if present).")]
        [SerializeField] private float entryFadeInSeconds = 0.25f;
        [Tooltip("Duration of the invite-click punch scale.")]
        [SerializeField] private float invitePressPunchSeconds = 0.2f;
        [Tooltip("Scale multiplier at the peak of the invite-click punch.")]
        [SerializeField] private float invitePressPunchScale = 1.08f;

        public enum Status { Online, InLobby, InMatch, LobbyFull }

        string _playerId;
        Action<string> _onInvite;
        bool _invitable;
        Status _lastStatus;
        int _lastPartyMemberCount;
        int _lastPartyMaxSlots;
        string _lastMatchName;
        bool _isPending;
        Coroutine _pulseCoroutine;
        CanvasGroup _canvasGroup;

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
        /// <param name="onInvite">Callback when the row background is clicked (null disables).</param>
        public void Populate(
            string playerId,
            string displayName,
            Sprite avatar,
            Status status,
            int partyMemberCount,
            int partyMaxSlots,
            string matchName,
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

            // Reset visual pending state when re-populating (unless
            // FriendsListPanel explicitly re-applies it via SetInvitePending).
            StopPulse();
            _isPending = false;
            ApplyRowTint(_invitable ? defaultTint : disabledTint);
        }

        public void SetStatus(Status status, int partyMemberCount = 0, int partyMaxSlots = 0, string matchName = null)
        {
            _lastStatus = status;
            _lastPartyMemberCount = partyMemberCount;
            _lastPartyMaxSlots = partyMaxSlots;
            _lastMatchName = matchName;

            // While pending, the label is overridden — don't clobber it.
            if (_isPending) return;

            ApplyStatusLabel(status, partyMemberCount, partyMaxSlots, matchName);
        }

        void ApplyStatusLabel(Status status, int partyMemberCount, int partyMaxSlots, string matchName)
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

            if (labelText)
            {
                labelText.text = text;
                labelText.color = color;
            }
        }

        /// <summary>
        /// Marks the row as "invite pending" — tints the background yellowish,
        /// swaps the label to "PENDING REQUEST", starts the pulse animation,
        /// and disables further invite clicks until reset.
        /// </summary>
        public void SetInvitePending()
        {
            if (inviteButton) inviteButton.interactable = false;

            _isPending = true;

            if (labelText)
            {
                labelText.text = pendingRequestLabel;
                labelText.color = pendingInviteTintBright;
            }

            StopPulse();
            if (isActiveAndEnabled)
                _pulseCoroutine = StartCoroutine(PulsePending());
            else
                ApplyRowTint(pendingInviteTint);
        }

        /// <summary>Restores the row to its post-populate state.</summary>
        public void ResetInviteState()
        {
            StopPulse();
            _isPending = false;

            if (inviteButton) inviteButton.interactable = _invitable;
            ApplyRowTint(_invitable ? defaultTint : disabledTint);

            // Restore the proper status label.
            ApplyStatusLabel(_lastStatus, _lastPartyMemberCount, _lastPartyMaxSlots, _lastMatchName);
        }

        IEnumerator PulsePending()
        {
            float period = Mathf.Max(0.1f, pendingPulsePeriodSeconds);
            float elapsed = 0f;

            while (_isPending)
            {
                // sin wave 0→1→0 over the period.
                float t = 0.5f * (1f + Mathf.Sin((elapsed / period) * Mathf.PI * 2f - Mathf.PI * 0.5f));
                ApplyRowTint(Color.Lerp(pendingInviteTint, pendingInviteTintBright, t));

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void StopPulse()
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
        }

        void OnEnable()
        {
            if (_isPending && _pulseCoroutine == null)
                _pulseCoroutine = StartCoroutine(PulsePending());

            StartCoroutine(FadeIn());
        }

        void OnDisable()
        {
            StopPulse();
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

        void ApplyRowTint(Color c)
        {
            if (backgroundImage) backgroundImage.color = c;
        }

        void HandleInviteClicked()
        {
            if (!_invitable) return;
            StartCoroutine(PunchScale(transform));
            SetInvitePending();
            _onInvite?.Invoke(_playerId);
        }

        IEnumerator PunchScale(Transform target)
        {
            if (!target) yield break;

            Vector3 baseScale = target.localScale;
            Vector3 peakScale = baseScale * Mathf.Max(1.01f, invitePressPunchScale);
            float half = Mathf.Max(0.02f, invitePressPunchSeconds * 0.5f);
            float elapsed = 0f;

            while (elapsed < half && target)
            {
                elapsed += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(baseScale, peakScale, elapsed / half);
                yield return null;
            }
            if (!target) yield break;

            elapsed = 0f;
            while (elapsed < half && target)
            {
                elapsed += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(peakScale, baseScale, elapsed / half);
                yield return null;
            }
            if (target) target.localScale = baseScale;
        }
    }
}
