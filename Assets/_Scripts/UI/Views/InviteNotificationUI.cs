using CosmicShore.Game.Party;
using CosmicShore.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Panels
{
    /// <summary>
    /// Toast popup for incoming party invitations.
    /// Subscribes to <see cref="HostConnectionDataSO.OnInviteReceived"/> (SOAP event)
    /// and calls <see cref="HostConnectionService"/> to accept or decline.
    /// </summary>
    public class InviteNotificationUI : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("UI References")]
        [SerializeField] private Image hostAvatarImage;
        [SerializeField] private TMP_Text hostNameText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        private PartyInviteData _pendingInvite;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            acceptButton?.onClick.AddListener(OnAccept);
            declineButton?.onClick.AddListener(OnDecline);
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (connectionData?.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised += ShowInvite;
        }

        void OnDisable()
        {
            if (connectionData?.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised -= ShowInvite;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Show / Hide
        // ─────────────────────────────────────────────────────────────────────

        private void ShowInvite(PartyInviteData invite)
        {
            _pendingInvite = invite;

            hostNameText.text = $"{invite.HostDisplayName} invited you!";

            var sprite = ResolveAvatarSprite(invite.HostAvatarId);
            if (sprite != null)
                hostAvatarImage.sprite = sprite;

            gameObject.SetActive(true);
        }

        private void HidePopup()
        {
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Button Handlers
        // ─────────────────────────────────────────────────────────────────────

        private async void OnAccept()
        {
            HidePopup();

            if (HostConnectionService.Instance != null)
                await HostConnectionService.Instance.AcceptInviteAsync(_pendingInvite);
        }

        private async void OnDecline()
        {
            HidePopup();

            if (HostConnectionService.Instance != null)
                await HostConnectionService.Instance.DeclineInviteAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

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
