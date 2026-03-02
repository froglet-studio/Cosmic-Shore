using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
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
    [RequireComponent(typeof(CanvasGroup))]
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
        private CanvasGroup _canvasGroup;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
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
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            SetCanvasGroupVisible(true);
            RebuildFromList();
        }

        public void Hide()
        {
            SetCanvasGroupVisible(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // List Sync
        // ─────────────────────────────────────────────────────────────────────

        private void RebuildFromList()
        {
            ClearEntries();

            if (connectionData?.OnlinePlayers == null || connectionData.OnlinePlayers.Count == 0)
            {
                emptyStateLabel?.SetActive(true);
                return;
            }

            emptyStateLabel?.SetActive(false);

            foreach (var info in connectionData.OnlinePlayers)
                SpawnEntry(info);
        }

        private void OnPlayerAdded(PartyPlayerData player)
        {
            emptyStateLabel?.SetActive(false);
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
                emptyStateLabel?.SetActive(true);
        }

        private void OnPlayersCleared()
        {
            ClearEntries();
            emptyStateLabel?.SetActive(true);
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

        private void OnInviteClicked(PartyPlayerData target)
        {
            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[OnlinePlayersPanel] PartyInviteController not available.");
                return;
            }

            // Delegate entirely to the persistent controller (DontDestroyOnLoad).
            // Do NOT await — this MonoBehaviour will be destroyed during the scene reload.
            controller.InvitePlayerAsync(target.PlayerId).Forget();
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

        private void SetCanvasGroupVisible(bool visible)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
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
