using System.Collections.Generic;
using CosmicShore.Game.Party;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Panels
{
    public class OnlinePlayersPanel : MonoBehaviour
    {
        [Inject] PartyManager partyManager;

        [Header("References")]
        [SerializeField] private GameObject         playerEntryPrefab;
        [SerializeField] private Transform          entryContainer;
        [SerializeField] private Button             closeButton;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("State")]
        [SerializeField] private GameObject emptyStateLabel;

        private readonly List<OnlinePlayerEntry> _activeEntries = new();

        // -----------------------------------------------------------------------------------------
        // Unity Lifecycle

        void Awake()
        {
            closeButton?.onClick.AddListener(Hide);
        }

        void OnEnable()
        {
            if (partyManager != null)
                partyManager.OnOnlinePlayersUpdated += Refresh;
        }

        void OnDisable()
        {
            if (partyManager != null)
                partyManager.OnOnlinePlayersUpdated -= Refresh;
        }

        // -----------------------------------------------------------------------------------------
        // Public API

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // -----------------------------------------------------------------------------------------
        // Refresh

        private void Refresh(IReadOnlyList<PartyManager.OnlinePlayerInfo> onlinePlayers)
        {
            // Clear existing entries
            foreach (var entry in _activeEntries)
                Destroy(entry.gameObject);
            _activeEntries.Clear();

            bool anyPlayers = onlinePlayers != null && onlinePlayers.Count > 0;
            emptyStateLabel?.SetActive(!anyPlayers);

            if (!anyPlayers) return;

            foreach (var info in onlinePlayers)
            {
                var go = Instantiate(playerEntryPrefab, entryContainer);
                var entry = go.GetComponent<OnlinePlayerEntry>();
                if (entry == null) continue;

                var avatarSprite = ResolveAvatarSprite(info.AvatarId);
                entry.Populate(info, avatarSprite, OnInviteClicked);
                _activeEntries.Add(entry);
            }
        }

        // -----------------------------------------------------------------------------------------
        // Invite

        private async void OnInviteClicked(PartyManager.OnlinePlayerInfo target)
        {
            if (partyManager == null) return;
            await partyManager.SendInviteAsync(target.PlayerId);
            // Optionally: show a "Invite Sent" toast here
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
