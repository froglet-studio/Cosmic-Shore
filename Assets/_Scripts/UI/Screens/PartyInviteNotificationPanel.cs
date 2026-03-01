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
    /// Place this on a UI panel that starts inactive. The panel enables itself
    /// on invite received and disables after the user responds or it times out.
    /// </summary>
    public class PartyInviteNotificationPanel : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("UI References")]
        [SerializeField] private TMP_Text inviterNameText;
        [SerializeField] private Image inviterAvatarImage;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private GameObject panelRoot;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Timing")]
        [Tooltip("Seconds before the invite notification auto-dismisses.")]
        [SerializeField] private float autoDeclineSeconds = 30f;

        private PartyInviteData? _pendingInvite;
        private float _timer;
        private bool _responding;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            acceptButton?.onClick.AddListener(OnAcceptPressed);
            declineButton?.onClick.AddListener(OnDeclinePressed);
        }

        void OnEnable()
        {
            if (connectionData?.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised += OnInviteReceived;
        }

        void OnDisable()
        {
            if (connectionData?.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised -= OnInviteReceived;
        }

        void Update()
        {
            if (!_pendingInvite.HasValue) return;

            _timer += Time.deltaTime;
            if (_timer >= autoDeclineSeconds)
            {
                OnDeclinePressed();
            }
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
            _pendingInvite = invite;
            _timer = 0f;
            _responding = false;

            if (inviterNameText != null)
                inviterNameText.text = invite.HostDisplayName;

            if (inviterAvatarImage != null)
            {
                var sprite = ResolveAvatarSprite(invite.HostAvatarId);
                if (sprite != null)
                    inviterAvatarImage.sprite = sprite;
            }

            SetButtonsInteractable(true);
            ShowPanel(true);
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
        /// Declines the pending invite. Delegates to <see cref="PartyInviteController.DeclineInviteAsync"/>.
        /// </summary>
        public void OnDeclinePressed()
        {
            if (_responding) return;
            _responding = true;

            SetButtonsInteractable(false);

            _pendingInvite = null;
            ShowPanel(false);

            DeclineAsync();
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
                Debug.LogWarning("[PartyInviteNotificationPanel] PartyInviteController not available.");
                ShowPanel(false);
            }
        }

        private async void DeclineAsync()
        {
            var controller = PartyInviteController.Instance;
            if (controller != null)
            {
                await controller.DeclineInviteAsync();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void ShowPanel(bool show)
        {
            if (panelRoot != null)
                panelRoot.SetVisible(show);
            else
                gameObject.SetVisible(show);
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
