using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    /// <summary>
    /// Modal panel that lists all online players from the presence lobby.
    /// Reads from <see cref="HostConnectionDataSO.OnlinePlayers"/> (SOAP ScriptableList)
    /// and instantiates <see cref="OnlinePlayerEntry"/> prefabs.
    /// Pressing the "+" on an entry triggers an invite via <see cref="HostConnectionService"/>.
    /// Optionally shows "Add Friend" buttons if the Friends service is available.
    /// </summary>
    public class OnlinePlayersPanel : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("References")]
        [SerializeField] private GameObject playerEntryPrefab;
        [SerializeField] private Transform entryContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Friends (optional)")]
        [SerializeField] private FriendsPanel friendsPanel;
        [SerializeField] private Button openFriendsButton;

        [Header("State")]
        [SerializeField] private GameObject emptyStateLabel;

        [Inject] private FriendsServiceFacade friendsService;

        private readonly List<OnlinePlayerEntry> _activeEntries = new();

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            closeButton?.onClick.AddListener(Hide);
            openFriendsButton?.onClick.AddListener(OnOpenFriends);
        }

        void OnEnable()
        {
            if (connectionData?.OnlinePlayers != null)
            {
                connectionData.OnlinePlayers.OnItemAdded += OnPlayerAdded;
                connectionData.OnlinePlayers.OnItemRemoved += OnPlayerRemoved;
                connectionData.OnlinePlayers.OnCleared += OnPlayersCleared;
            }

            RebuildFromList();
        }

        void OnDisable()
        {
            if (connectionData?.OnlinePlayers != null)
            {
                connectionData.OnlinePlayers.OnItemAdded -= OnPlayerAdded;
                connectionData.OnlinePlayers.OnItemRemoved -= OnPlayerRemoved;
                connectionData.OnlinePlayers.OnCleared -= OnPlayersCleared;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Show()
        {
            gameObject.SetVisible(true);
        }

        public void Hide()
        {
            gameObject.SetVisible(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // List Sync
        // ─────────────────────────────────────────────────────────────────────

        private void RebuildFromList()
        {
            ClearEntries();

            if (connectionData?.OnlinePlayers == null || connectionData.OnlinePlayers.Count == 0)
            {
                emptyStateLabel?.SetVisible(true);
                return;
            }

            emptyStateLabel?.SetVisible(false);

            foreach (var info in connectionData.OnlinePlayers)
                SpawnEntry(info);
        }

        private void OnPlayerAdded(PartyPlayerData player)
        {
            emptyStateLabel?.SetVisible(false);
            SpawnEntry(player);
        }

        private void OnPlayerRemoved(PartyPlayerData player)
        {
            for (int i = _activeEntries.Count - 1; i >= 0; i--)
            {
                if (_activeEntries[i].PlayerId == player.PlayerId)
                {
                    Destroy(_activeEntries[i].gameObject);
                    _activeEntries.RemoveAt(i);
                    break;
                }
            }

            if (_activeEntries.Count == 0)
                emptyStateLabel?.SetVisible(true);
        }

        private void OnPlayersCleared()
        {
            ClearEntries();
            emptyStateLabel?.SetVisible(true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Entry Management
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnEntry(PartyPlayerData info)
        {
            var go = Instantiate(playerEntryPrefab, entryContainer);
            var entry = go.GetComponent<OnlinePlayerEntry>();
            if (entry == null) return;

            var avatarSprite = ResolveAvatarSprite(info.AvatarId);
            bool hasFriends = friendsService != null && friendsService.IsInitialized;
            bool isAlreadyFriend = hasFriends && friendsService.IsFriend(info.PlayerId);

            entry.Populate(
                info,
                avatarSprite,
                OnInviteClicked,
                hasFriends ? OnAddFriendClicked : null,
                isAlreadyFriend);

            _activeEntries.Add(entry);
        }

        private void ClearEntries()
        {
            foreach (var entry in _activeEntries)
                Destroy(entry.gameObject);
            _activeEntries.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Invite
        // ─────────────────────────────────────────────────────────────────────

        private async void OnInviteClicked(PartyPlayerData target)
        {
            // Ensure the host has transitioned to a Relay-backed party session
            // so invited clients can connect via Relay transport.
            var controller = PartyInviteController.Instance;
            if (controller != null)
                await controller.TransitionToPartyHostAsync();

            if (HostConnectionService.Instance == null) return;
            await HostConnectionService.Instance.SendInviteAsync(target.PlayerId);
        }

        private async void OnAddFriendClicked(PartyPlayerData target)
        {
            if (friendsService == null) return;

            try
            {
                await friendsService.SendFriendRequestAsync(target.PlayerId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[OnlinePlayersPanel] Add friend error: {e.Message}");
            }
        }

        private void OnOpenFriends()
        {
            friendsPanel?.Show();
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
