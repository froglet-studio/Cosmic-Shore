using CosmicShore.Gameplay;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    /// <summary>
    /// Popup for incoming party invitations.
    /// Subscribes to <see cref="HostConnectionDataSO.OnInviteReceived"/> (SOAP event)
    /// and delegates the full accept/decline flow to <see cref="PartyInviteController"/>,
    /// which orchestrates Netcode host-to-client transitions and post-join initialization.
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
        [SerializeField] private GameObject transitionIndicator;

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
            transitionIndicator?.SetActive(false);
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
            SetButtonsInteractable(false);
            transitionIndicator?.SetActive(true);

            var controller = PartyInviteController.Instance;
            if (controller != null)
            {
                // Full flow: shutdown host → join as client → scene sync → vessel spawn
                await controller.AcceptInviteAsync(_pendingInvite);
            }
            else if (HostConnectionService.Instance != null)
            {
                // Fallback: UGS session join only (no Netcode transition)
                await HostConnectionService.Instance.AcceptInviteAsync(_pendingInvite);
            }

            HidePopup();
        }

        private async void OnDecline()
        {
            HidePopup();

            var controller = PartyInviteController.Instance;
            if (controller != null)
                await controller.DeclineInviteAsync();
            else if (HostConnectionService.Instance != null)
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
