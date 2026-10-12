using System;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Overlay notification panel that appears when the local player receives
    /// a party invite from another player.
    ///
    /// Subscribes to <see cref="HostConnectionDataSO.OnInviteReceived"/> (SOAP event)
    /// and shows the invite with Accept/Decline buttons. Accept delegates to
    /// <see cref="PartyInviteController.AcceptInviteAsync"/>, Decline to
    /// <see cref="PartyInviteController.DeclineInviteAsync"/>.
    ///
    /// <para>The popup is a TEASER, not the inbox. It auto-hides after
    /// <see cref="autoHideSeconds"/> (hiding never declines), and its detail line says so:
    /// the invite waits in the friends panel's Requests list for the rest of its lifetime,
    /// which the service's <see cref="IncomingInviteInbox"/> keeps. A second invite arriving
    /// while one is shown takes the card (newest wins on the card) but does not destroy the
    /// first: the line reads "2 invites waiting", and when the shown invite is answered or
    /// withdrawn the card moves on to the next open one.</para>
    ///
    /// The GameObject must remain active so OnEnable can subscribe to SOAP events.
    /// The panel starts visually hidden via CanvasGroup (alpha=0).
    /// </summary>
    public class PartyInviteNotificationPanel : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("UI References")]
        [SerializeField] private TMP_Text inviterNameText;
        [Tooltip("Optional. The card's second line: how many invites are waiting and that they " +
                 "stay in the friends list for N s after this card hides (Notice.Compose). The " +
                 "row's status label on the prefab variant. Unwired: the card shows the name alone.")]
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Image inviterAvatarImage;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Timing")]
        [Tooltip("Seconds the bottom-left popup stays before it auto-HIDES. Hiding does NOT decline - " +
                 "the invite remains in the FriendsListPanel Requests list for the rest of its " +
                 "lifetime (HostConnectionService.IncomingInviteLifetimeSeconds, the host's " +
                 "outgoing-invite timeout) and the card says so; the host's pending invite clears " +
                 "on its own timeout.")]
        [SerializeField] private float autoHideSeconds = 3f;

        [Header("Audio")]
        [Tooltip("Menu SFX category played when an invite appears.")]
        [SerializeField] private MenuAudioCategory inviteReceivedAudio = MenuAudioCategory.Confirmed;

        private PartyInviteData? _pendingInvite;
        private float _timer;
        private bool _responding;
        private int _shownSecondsRemaining = -1;

        /// <summary>
        /// The card's detail line, as pure text so it can be asserted without a scene.
        /// </summary>
        public static class Notice
        {
            /// <summary>
            /// "Also in your friends list for 58 s", prefixed with "2 invites waiting. " when more
            /// than one invite is open. With no known lifetime (0 or less, or infinite) the
            /// seconds are left off. ASCII only: the UI font has no other glyphs.
            /// </summary>
            /// <param name="pendingCount">How many invites are open, this one included.</param>
            /// <param name="secondsRemaining">Seconds this invite stays in the friends list.</param>
            public static string Compose(int pendingCount, float secondsRemaining)
            {
                string where = secondsRemaining > 0f && !float.IsInfinity(secondsRemaining)
                    ? $"Also in your friends list for {Math.Max(1, (int)Math.Ceiling(secondsRemaining))} s"
                    : "Also in your friends list";

                return pendingCount > 1
                    ? $"{pendingCount} invites waiting. {where}"
                    : where;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            // Start visually hidden - the GO must stay active so OnEnable
            // can subscribe to the OnInviteReceived SOAP event.
            ShowPanel(false);

            acceptButton?.onClick.AddListener(OnAcceptPressed);
            declineButton?.onClick.AddListener(OnDeclinePressed);
        }

        void OnEnable()
        {
            if (connectionData?.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised += OnInviteReceived;
            else
                CSDebug.LogError(
                    "[INVITE-UI] connectionData or OnInviteReceived is NULL - cannot subscribe");

            // Also react when an invite is resolved from a different panel (the FriendsListPanel
            // Accept/Decline buttons) or ends in the service (withdrawn, expired).
            if (connectionData?.OnInviteResolved != null)
                connectionData.OnInviteResolved.OnRaised += OnInviteResolvedElsewhere;
        }

        void OnDisable()
        {
            if (connectionData?.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised -= OnInviteReceived;

            if (connectionData?.OnInviteResolved != null)
                connectionData.OnInviteResolved.OnRaised -= OnInviteResolvedElsewhere;
        }

        /// <summary>
        /// Called when any invite was resolved anywhere (accepted, declined, withdrawn, expired).
        /// The raise carries no payload, so the card asks the service: if the invite it shows is
        /// still open it stays (only the count line changes); otherwise the card moves on to the
        /// next open invite, or hides when there is none. That is what stops the user seeing a
        /// stale card after answering in the FriendsListPanel, and what keeps a second sender's
        /// invite visible after the first was declined.
        /// </summary>
        private void OnInviteResolvedElsewhere()
        {
            if (!_pendingInvite.HasValue && canvasGroup != null && canvasGroup.alpha == 0f)
                return;

            var service = HostConnectionService.Instance;
            if (service == null)
            {
                Dismiss();
                return;
            }

            if (_pendingInvite.HasValue && service.IsIncomingInvitePending(_pendingInvite.Value.HostPlayerId))
            {
                RefreshDetailLine(force: true);
                return;
            }

            var next = service.LastPendingInvite;
            if (next.HasValue)
                Present(next.Value, playSound: false);
            else
                Dismiss();
        }

        void Update()
        {
            if (!_pendingInvite.HasValue) return;

            // Unscaled so a paused / zero-timescale menu still hides the teaser.
            _timer += Time.unscaledDeltaTime;
            if (_timer >= autoHideSeconds)
            {
                // Auto-HIDE only - do NOT decline. The invite stays in the FriendsListPanel
                // Requests list (and the service's inbox) for its own lifetime; the host's
                // pending clears via its timeout.
                Dismiss();
                return;
            }

            RefreshDetailLine(force: false);
        }

        void OnDestroy()
        {
            acceptButton?.onClick.RemoveListener(OnAcceptPressed);
            declineButton?.onClick.RemoveListener(OnDeclinePressed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP Callback
        // ─────────────────────────────────────────────────────────────────────

        private void OnInviteReceived(PartyInviteData invite)
        {
            CSDebug.LogVerbose(CSLogChannel.Party,
                $"[INVITE-UI] OnInviteReceived from {invite.HostDisplayName}, " +
                $"SessionId: {invite.PartySessionId}");

            // Newest wins on the CARD; the earlier invite is still open in the inbox and the
            // detail line counts it.
            Present(invite, playSound: true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Button Handlers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Accepts the pending invite. Delegates to <see cref="PartyInviteController.AcceptInviteAsync"/>.
        /// </summary>
        public void OnAcceptPressed()
        {
            if (_responding || !_pendingInvite.HasValue) return;
            _responding = true;

            SetButtonsInteractable(false);

            var invite = _pendingInvite.Value;
            _pendingInvite = null;

            AcceptAsync(invite);
        }

        /// <summary>
        /// Declines the pending invite - this sender's only; any other open invite keeps waiting
        /// and the card moves on to it on the resolve raise. Delegates to
        /// <see cref="PartyInviteController.DeclineInviteAsync"/>.
        ///
        /// <para>The card is NOT hidden or cleared here: <see cref="OnInviteResolvedElsewhere"/>
        /// returns early for a hidden card with nothing pending, so hiding first made the raise
        /// a no-op and the card vanished with another invite still open. Left on the declined
        /// invite, the raise finds it resolved and moves on to the next open invite, or hides.</para>
        /// </summary>
        public void OnDeclinePressed()
        {
            if (_responding || !_pendingInvite.HasValue) return;
            _responding = true;

            SetButtonsInteractable(false);

            DeclineAsync(_pendingInvite.Value.HostPlayerId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Async Wrappers
        // ─────────────────────────────────────────────────────────────────────

        private async void AcceptAsync(PartyInviteData invite)
        {
            var controller = PartyInviteController.Instance;
            if (controller != null)
            {
                ShowPanel(false);
                await controller.AcceptInviteAsync(invite);
            }
            else
            {
                CSDebug.LogWarning("[PartyInviteNotificationPanel] PartyInviteController not available.");
                ShowPanel(false);
            }
        }

        private async void DeclineAsync(string senderPlayerId)
        {
            var controller = PartyInviteController.Instance;
            if (controller != null)
            {
                await controller.DeclineInviteAsync(senderPlayerId);
            }

            // No resolve raise reached the card (no controller or no service): it still shows the
            // invite that was just declined, so it goes. Otherwise the raise already moved it on
            // (Present) or hid it (Dismiss), and both reset _responding.
            if (_pendingInvite.HasValue && _pendingInvite.Value.HostPlayerId == senderPlayerId)
                Dismiss();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Puts <paramref name="invite"/> on the card with a fresh auto-hide clock.</summary>
        private void Present(PartyInviteData invite, bool playSound)
        {
            _pendingInvite = invite;
            _timer = 0f;
            _responding = false;
            _shownSecondsRemaining = -1;

            if (inviterNameText != null)
                inviterNameText.text = invite.HostDisplayName;

            if (inviterAvatarImage != null)
            {
                var sprite = ResolveAvatarSprite(invite.HostAvatarId);
                if (sprite != null)
                    inviterAvatarImage.sprite = sprite;
            }

            RefreshDetailLine(force: true);
            SetButtonsInteractable(true);
            ShowPanel(true);

            if (playSound)
                AudioSystem.Instance?.PlayMenuAudio(inviteReceivedAudio);
        }

        private void Dismiss()
        {
            _pendingInvite = null;
            _responding = false;
            _timer = 0f;
            ShowPanel(false);
        }

        /// <summary>
        /// Writes the detail line from the service's inbox. Cheap, but the text is only rebuilt
        /// when the whole second changes (or on <paramref name="force"/>), so the card does not
        /// re-lay out a TMP string every frame.
        /// </summary>
        private void RefreshDetailLine(bool force)
        {
            if (detailText == null || !_pendingInvite.HasValue) return;

            var service = HostConnectionService.Instance;
            int pending = 1;
            float remaining = 0f;
            if (service != null)
            {
                pending   = Mathf.Max(1, service.PendingIncomingInviteCount);
                remaining = service.IncomingInviteSecondsRemaining(_pendingInvite.Value.HostPlayerId);
            }

            int wholeSeconds = remaining > 0f && !float.IsInfinity(remaining) ? Mathf.CeilToInt(remaining) : 0;
            if (!force && wholeSeconds == _shownSecondsRemaining) return;
            _shownSecondsRemaining = wholeSeconds;

            detailText.text = Notice.Compose(pending, remaining);
        }

        private void ShowPanel(bool show)
        {
            if (canvasGroup == null) return;

            // Ensure the notification renders on top of all siblings
            // (guards against dynamic UI hierarchy changes from scene migrations).
            if (show)
                transform.SetAsLastSibling();

            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.blocksRaycasts = show;
            canvasGroup.interactable = show;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (acceptButton != null)
                acceptButton.interactable = interactable;
            if (declineButton != null)
                declineButton.interactable = interactable;
        }

        private Sprite ResolveAvatarSprite(int avatarId)
        {
            if (profileIcons == null) return null;
            foreach (var icon in profileIcons.profileIcons)
            {
                if (icon.Id == avatarId)
                    return icon.IconSprite;
            }
            return null;
        }
    }
}
