using System;
using CosmicShore.Game.Party;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Panels
{
    public class OnlinePlayerEntry : MonoBehaviour
    {
        [SerializeField] private Image    avatarImage;
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Button   inviteButton;
        [SerializeField] private GameObject inviteSentIndicator;

        private PartyManager.OnlinePlayerInfo            _info;
        private Action<PartyManager.OnlinePlayerInfo>    _onInvite;

        // -----------------------------------------------------------------------------------------
        // Setup

        public void Populate(PartyManager.OnlinePlayerInfo info, Sprite avatar, Action<PartyManager.OnlinePlayerInfo> onInvite)
        {
            _info     = info;
            _onInvite = onInvite;

            displayNameText.text = info.DisplayName;

            if (avatar != null)
                avatarImage.sprite = avatar;

            inviteButton?.onClick.RemoveAllListeners();
            inviteButton?.onClick.AddListener(OnInvitePressed);

            inviteSentIndicator?.SetActive(false);
        }

        // -----------------------------------------------------------------------------------------
        // Events

        private void OnInvitePressed()
        {
            _onInvite?.Invoke(_info);

            // Visual feedback — disable invite button, show sent indicator
            if (inviteButton != null)
                inviteButton.interactable = false;

            inviteSentIndicator?.SetActive(true);
        }
    }
}