using System;
using CosmicShore.Utility.SOAP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI.Views
{
    /// <summary>
    /// Individual row in the <see cref="OnlinePlayersPanel"/>.
    /// Shows an online player's avatar + name and a "+" invite button.
    /// Uses <see cref="PartyPlayerData"/> (SOAP struct) as its data model.
    /// </summary>
    public class OnlinePlayerEntry : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Button inviteButton;
        [SerializeField] private GameObject inviteSentIndicator;

        private PartyPlayerData _info;
        private Action<PartyPlayerData> _onInvite;

        public string PlayerId => _info.PlayerId;

        // ─────────────────────────────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────────────────────────────

        public void Populate(PartyPlayerData info, Sprite avatar, Action<PartyPlayerData> onInvite)
        {
            _info = info;
            _onInvite = onInvite;

            displayNameText.text = info.DisplayName;

            if (avatar != null)
                avatarImage.sprite = avatar;

            inviteButton?.onClick.RemoveAllListeners();
            inviteButton?.onClick.AddListener(OnInvitePressed);

            inviteSentIndicator?.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────────────────────────────

        private void OnInvitePressed()
        {
            _onInvite?.Invoke(_info);

            if (inviteButton != null)
                inviteButton.interactable = false;

            inviteSentIndicator?.SetActive(true);
        }
    }
}
