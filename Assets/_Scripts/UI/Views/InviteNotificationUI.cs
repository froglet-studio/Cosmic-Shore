using CosmicShore.Game.Party;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Panels
{
    public class InviteNotificationUI : MonoBehaviour
    {
        [Inject] PartyManager partyManager;

        [Header("UI References")]
        [SerializeField] private Image          hostAvatarImage;
        [SerializeField] private TMP_Text       hostNameText;
        [SerializeField] private Button         acceptButton;
        [SerializeField] private Button         declineButton;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        private PartyManager.PartyInvite _pendingInvite;

        // -----------------------------------------------------------------------------------------
        // Unity Lifecycle

        void Awake()
        {
            acceptButton?.onClick.AddListener(OnAccept);
            declineButton?.onClick.AddListener(OnDecline);
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (partyManager != null)
                partyManager.OnInviteReceived += ShowInvite;
        }

        void OnDisable()
        {
            if (partyManager != null)
                partyManager.OnInviteReceived -= ShowInvite;
        }

        // -----------------------------------------------------------------------------------------
        // Show / Hide

        private void ShowInvite(PartyManager.PartyInvite invite)
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

        // -----------------------------------------------------------------------------------------
        // Button Handlers

        private async void OnAccept()
        {
            HidePopup();

            if (partyManager != null)
                await partyManager.AcceptInviteAsync(_pendingInvite);
        }

        private async void OnDecline()
        {
            HidePopup();

            if (partyManager != null)
                await partyManager.DeclineInviteAsync();
        }

        // -----------------------------------------------------------------------------------------
        // Helpers

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
