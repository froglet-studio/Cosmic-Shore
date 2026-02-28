using CosmicShore.Gameplay;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// Toast popup for incoming party invitations.
    /// Subscribes to <see cref="HostConnectionDataSO.OnInviteReceived"/> (SOAP event)
    /// and calls <see cref="HostConnectionService"/> to accept or decline.
    ///
    /// On accept, disables both buttons during the async network transition
    /// (local host shutdown → relay client join) and shows a "Joining..." label.
    /// The scene reloads via Netcode scene sync so this popup is destroyed
    /// automatically during the transition.
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

            SetButtonsInteractable(true);
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
            // Disable buttons during the network transition to prevent
            // double-clicks. Show "Joining..." feedback.
            SetButtonsInteractable(false);
            hostNameText.text = $"Joining {_pendingInvite.HostDisplayName}...";

            if (HostConnectionService.Instance != null)
                await HostConnectionService.Instance.AcceptInviteAsync(_pendingInvite);

            // The scene reloads via Netcode scene sync, so this popup
            // will be destroyed during the transition. If the accept
            // fails and we're still alive, hide the popup.
            if (this != null && gameObject != null)
                HidePopup();
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

        private void SetButtonsInteractable(bool interactable)
        {
            if (acceptButton != null) acceptButton.interactable = interactable;
            if (declineButton != null) declineButton.interactable = interactable;
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
