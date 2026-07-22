using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Row entry for the Online section of FriendsListPanel.
    /// Shows avatar, username, lobby/match status, plus an Invite button and a
    /// Cancel/Kick (✕) button. The Invite button is shown only when the player can be
    /// invited (Online / IN PARTY). The ✕ is shown when an outgoing invite is pending
    /// (click = cancel the invite) OR the player is in YOUR party and you are the host
    /// (click = kick them). Sending an invite tints the row yellowish and pulses until
    /// the target accepts/declines/times out. Invite, cancel and kick all pass through
    /// a shared cooldown so the buttons can't be spam-clicked.
    /// </summary>
    public class OnlineInfoEntry : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Image avatarIcon;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_Text labelText;

        [Header("Invite / Cancel Buttons")]
        [Tooltip("The row background image. Receives the yellowish pending tint " +
                 "while an invite to this player is in-flight.")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Invite button. Click sends an invite to this player. Shown only " +
                 "while the player can be invited (Online / IN PARTY).")]
        [SerializeField] private Button inviteButton;
        [Tooltip("Cancel / Kick (✕) button. Shown while an outgoing invite is pending (click " +
                 "retracts it) OR when this player is in your party and you're the host (click " +
                 "kicks them). Leave unassigned to disable the affordance.")]
        [SerializeField] private Button cancelButton;

        [Header("Status Colors (applied to Label Text)")]
        [SerializeField] private Color onlineColor = Color.white;
        [SerializeField] private Color inLobbyColor = new(0.4f, 0.8f, 1f, 1f);
        [SerializeField] private Color inMatchColor = new(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color lobbyFullColor = new(0.5f, 0.5f, 0.5f, 1f);
        [Tooltip("Label colour when this player is already in the local player's party (non-invitable).")]
        [SerializeField] private Color inYourPartyColor = new(0.6f, 0.9f, 0.6f, 1f);

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

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between consecutive invite/cancel actions on this row. " +
                 "Throttles double-taps and rapid invite↔cancel toggling so the buttons " +
                 "can't be spam-clicked. Shared by both buttons.")]
        [SerializeField] private float actionCooldownSeconds = 0.4f;

        public enum Status { Online, InLobby, InMatch, LobbyFull, InYourParty }

        string _playerId;
        Action<string> _onInvite;
        Action<string> _onCancel;
        Action<string> _onKick;
        bool _invitable;
        bool _kickable;
        Status _lastStatus;
        int _lastPartyMemberCount;
        int _lastPartyMaxSlots;
        string _lastMatchName;
        bool _isPending;
        float _nextActionAllowedTime;
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
        /// <param name="onInvite">Invite callback; null (or a non-invitable status) hides the Invite button.</param>
        /// <param name="onCancel">Cancel-invite callback, fired by the ✕ while an invite is pending.</param>
        /// <param name="onKick">Kick callback; pass non-null only for a kickable party member (host view) - its presence shows the ✕ in kick mode.</param>
        public void Populate(
            string playerId,
            string displayName,
            Sprite avatar,
            Status status,
            int partyMemberCount,
            int partyMaxSlots,
            string matchName,
            Action<string> onInvite,
            Action<string> onCancel = null,
            Action<string> onKick = null)
        {
            _playerId = playerId;
            _onInvite = onInvite;
            _onCancel = onCancel;
            _onKick = onKick;

            if (usernameText)
                usernameText.text = displayName ?? "Unknown";

            if (avatarIcon)
            {
                avatarIcon.sprite = avatar;
                avatarIcon.enabled = avatar != null;
            }

            SetStatus(status, partyMemberCount, partyMaxSlots, matchName);

            // Invite button shows ONLY when the status permits an invite
            // (Online / InLobby) and a callback is provided - hidden otherwise.
            _invitable = onInvite != null &&
                         (status == Status.Online || status == Status.InLobby);

            // Kick affordance: this row is a kickable member of MY party.
            // FriendsListPanel passes onKick only when I'm the host.
            _kickable = onKick != null;

            if (inviteButton)
            {
                inviteButton.onClick.RemoveAllListeners();
                if (_invitable)
                    inviteButton.onClick.AddListener(HandleInviteClicked);
                inviteButton.interactable = _invitable;
                inviteButton.gameObject.SetActive(_invitable);
            }

            // Cancel / Kick (✕) - one button, two roles. Wired once; visible when an
            // invite is pending (cancel) OR this is a kickable party member (kick). The
            // pending case is (re)applied by FriendsListPanel via SetInvitePending right
            // after Populate, so here we only seed the kick-mode visibility.
            if (cancelButton)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(HandleCancelClicked);
                cancelButton.interactable = true;
                cancelButton.gameObject.SetActive(_kickable);
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

            // While pending, the label is overridden - don't clobber it.
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
                        ? $"IN PARTY {partyMemberCount}/{partyMaxSlots}"
                        : "IN PARTY";
                    color = inLobbyColor;
                    break;
                case Status.LobbyFull:
                    text = "PARTY FULL";
                    color = lobbyFullColor;
                    break;
                case Status.InMatch:
                    text = string.IsNullOrEmpty(matchName)
                        ? "IN A MATCH"
                        : $"IN A MATCH - {matchName.ToUpperInvariant()}";
                    color = inMatchColor;
                    break;
                case Status.InYourParty:
                    text = partyMaxSlots > 0
                        ? $"IN YOUR PARTY {partyMemberCount}/{partyMaxSlots}"
                        : "IN YOUR PARTY";
                    color = inYourPartyColor;
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
        /// Marks the row as "invite pending" - tints the background yellowish,
        /// swaps the label to "PENDING REQUEST", starts the pulse animation,
        /// and disables further invite clicks until reset.
        /// </summary>
        public void SetInvitePending()
        {
            if (inviteButton) inviteButton.gameObject.SetActive(false);
            if (cancelButton) cancelButton.gameObject.SetActive(true);

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

            // The ✕ stays visible only if this row is a kickable party member.
            if (cancelButton) cancelButton.gameObject.SetActive(_kickable);
            if (inviteButton)
            {
                inviteButton.interactable = _invitable;
                inviteButton.gameObject.SetActive(_invitable);
            }
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
            if (!TryBeginAction()) return;
            StartCoroutine(PunchScale(transform));
            SetInvitePending();
            _onInvite?.Invoke(_playerId);
        }

        void HandleCancelClicked()
        {
            if (!TryBeginAction()) return;

            if (_isPending)
            {
                // Retract the pending invite. ResetInviteState (also driven by
                // OutgoingInviteCleared) reverts the row to its online state; call it
                // optimistically too so the ✕ feels instant.
                _onCancel?.Invoke(_playerId);
                ResetInviteState();
            }
            else if (_kickable)
            {
                // Kick this member from my party. The host's RemovePartyMember +
                // OnPartyMemberKicked refresh re-renders the row; hide the ✕ immediately
                // so it can't be re-clicked during the async gap.
                _onKick?.Invoke(_playerId);
                if (cancelButton) cancelButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Anti-spam gate shared by the Invite and Cancel buttons. Returns false (and
        /// ignores the click) when called within <see cref="actionCooldownSeconds"/> of the
        /// previous accepted action; otherwise arms the next cooldown window and returns true.
        /// Unscaled so it still throttles while the menu is paused (timeScale 0).
        /// </summary>
        bool TryBeginAction()
        {
            if (Time.unscaledTime < _nextActionAllowedTime)
                return false;
            _nextActionAllowedTime = Time.unscaledTime + Mathf.Max(0f, actionCooldownSeconds);
            return true;
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
