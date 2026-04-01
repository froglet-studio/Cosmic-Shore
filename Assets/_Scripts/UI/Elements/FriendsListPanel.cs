using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Controller for the FriendListPanel inside ArcadeScreenModal.
    /// Manages three tabs (Online, Requests, Friends) with runtime-spawned entries.
    ///
    /// Each tab has a header button, a ScrollRect content transform, and a prefab.
    /// Selecting a tab enables its content and populates from the relevant SOAP list.
    /// </summary>
    public class FriendsListPanel : MonoBehaviour
    {
        enum Tab { Online, Requests, Friends }

        [Header("Tab Buttons")]
        [SerializeField] private Button onlineHeaderButton;
        [SerializeField] private Button requestsHeaderButton;
        [SerializeField] private Button friendsHeaderButton;

        [Header("Tab Content Parents (ScrollRect > Viewport > Content)")]
        [SerializeField] private Transform onlineContent;
        [SerializeField] private Transform requestsContent;
        [SerializeField] private Transform friendsContent;

        [Header("Tab Content Roots (the ScrollRect GameObjects)")]
        [Tooltip("Root GameObjects for each tab. Enabled/disabled via CanvasGroup.")]
        [SerializeField] private CanvasGroup onlineCanvasGroup;
        [SerializeField] private CanvasGroup requestsCanvasGroup;
        [SerializeField] private CanvasGroup friendsCanvasGroup;

        [Header("Prefabs")]
        [SerializeField] private OnlineInfoEntry onlineInfoPrefab;
        [SerializeField] private FriendInfoEntry friendInfoPrefab;
        [SerializeField] private RequestInfoEntry requestInfoPrefab;

        [Header("Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private FriendsDataSO friendsData;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Actions")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button refreshButton;

        [Header("Settings")]
        [Tooltip("Seconds before an incoming request auto-declines.")]
        [SerializeField] private float requestExpirationSeconds = 600f;

        [Inject] private FriendsServiceFacade friendsService;

        Tab _activeTab = Tab.Online;
        readonly List<GameObject> _spawnedOnline = new();
        readonly List<GameObject> _spawnedRequests = new();
        readonly List<GameObject> _spawnedFriends = new();

        #region Unity Lifecycle

        void Start()
        {
            if (onlineHeaderButton)
                onlineHeaderButton.onClick.AddListener(() => SwitchTab(Tab.Online));
            if (requestsHeaderButton)
                requestsHeaderButton.onClick.AddListener(() => SwitchTab(Tab.Requests));
            if (friendsHeaderButton)
                friendsHeaderButton.onClick.AddListener(() => SwitchTab(Tab.Friends));

            if (closeButton)
                closeButton.onClick.AddListener(Hide);

            if (refreshButton)
                refreshButton.onClick.AddListener(HandleRefresh);

            // SOAP subscriptions — online players
            if (connectionData)
            {
                connectionData.OnlinePlayers.OnItemAdded += HandleOnlinePlayerAdded;
                connectionData.OnlinePlayers.OnItemRemoved += HandleOnlinePlayerRemoved;
                connectionData.OnlinePlayers.OnCleared += HandleOnlinePlayersCleared;
            }

            // SOAP subscriptions — friends
            if (friendsData)
            {
                friendsData.OnFriendAdded.OnRaised += HandleFriendAdded;
                friendsData.OnFriendRemoved.OnRaised += HandleFriendRemoved;
                friendsData.IncomingRequests.OnItemAdded += HandleIncomingRequestAdded;
                friendsData.IncomingRequests.OnItemRemoved += HandleIncomingRequestRemoved;
            }

            SwitchTab(Tab.Online);
        }

        void OnDisable()
        {
            if (connectionData)
            {
                connectionData.OnlinePlayers.OnItemAdded -= HandleOnlinePlayerAdded;
                connectionData.OnlinePlayers.OnItemRemoved -= HandleOnlinePlayerRemoved;
                connectionData.OnlinePlayers.OnCleared -= HandleOnlinePlayersCleared;
            }

            if (friendsData)
            {
                friendsData.OnFriendAdded.OnRaised -= HandleFriendAdded;
                friendsData.OnFriendRemoved.OnRaised -= HandleFriendRemoved;
                friendsData.IncomingRequests.OnItemAdded -= HandleIncomingRequestAdded;
                friendsData.IncomingRequests.OnItemRemoved -= HandleIncomingRequestRemoved;
            }
        }

        #endregion

        #region Public API

        public void Show()
        {
            gameObject.SetActive(true);
            SwitchTab(_activeTab);
        }

        /// <summary>Opens the panel directly to the Online tab (used by "+" add buttons).</summary>
        public void ShowOnlineTab()
        {
            gameObject.SetActive(true);
            SwitchTab(Tab.Online);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion

        #region Tab Switching

        void SwitchTab(Tab tab)
        {
            _activeTab = tab;

            SetCanvasGroupActive(onlineCanvasGroup, tab == Tab.Online);
            SetCanvasGroupActive(requestsCanvasGroup, tab == Tab.Requests);
            SetCanvasGroupActive(friendsCanvasGroup, tab == Tab.Friends);

            switch (tab)
            {
                case Tab.Online:   PopulateOnlineTab();   break;
                case Tab.Requests: PopulateRequestsTab(); break;
                case Tab.Friends:  PopulateFriendsTab();  break;
            }
        }

        static void SetCanvasGroupActive(CanvasGroup cg, bool active)
        {
            if (!cg) return;
            cg.alpha = active ? 1f : 0f;
            cg.blocksRaycasts = active;
            cg.interactable = active;
        }

        #endregion

        #region Online Tab

        void PopulateOnlineTab()
        {
            ClearSpawned(_spawnedOnline);
            if (!connectionData || !onlineContent || !onlineInfoPrefab) return;

            string localId = connectionData.LocalPlayerId;

            foreach (var player in connectionData.OnlinePlayers)
            {
                if (player.PlayerId == localId) continue;
                SpawnOnlineEntry(player);
            }
        }

        void SpawnOnlineEntry(PartyPlayerData player)
        {
            var entry = Instantiate(onlineInfoPrefab, onlineContent);
            _spawnedOnline.Add(entry.gameObject);

            bool isFriend = friendsService != null && friendsService.IsInitialized
                && friendsService.IsFriend(player.PlayerId);

            entry.Populate(
                player.PlayerId,
                player.DisplayName,
                ResolveAvatar(player.AvatarId),
                OnlineInfoEntry.Status.Online,
                isFriend,
                onAddFriend: OnAddFriendClicked,
                onInvite: OnInviteClicked);
        }

        void HandleOnlinePlayerAdded(PartyPlayerData player)
        {
            if (_activeTab != Tab.Online) return;
            if (connectionData && player.PlayerId == connectionData.LocalPlayerId) return;
            SpawnOnlineEntry(player);
        }

        void HandleOnlinePlayerRemoved(PartyPlayerData player)
        {
            if (_activeTab != Tab.Online) return;
            RemoveEntryByPlayerId(_spawnedOnline, player.PlayerId);
        }

        void HandleOnlinePlayersCleared()
        {
            if (_activeTab != Tab.Online) return;
            ClearSpawned(_spawnedOnline);
        }

        #endregion

        #region Requests Tab

        void PopulateRequestsTab()
        {
            ClearSpawned(_spawnedRequests);
            if (!friendsData || !requestsContent || !requestInfoPrefab) return;

            foreach (var request in friendsData.IncomingRequests)
                SpawnRequestEntry(request);
        }

        void SpawnRequestEntry(FriendData request)
        {
            var entry = Instantiate(requestInfoPrefab, requestsContent);
            _spawnedRequests.Add(entry.gameObject);

            entry.Populate(
                request.PlayerId,
                request.DisplayName,
                ResolveAvatar(0), // Incoming requests may not have avatar ID
                requestExpirationSeconds,
                onAccept: OnAcceptRequestClicked,
                onDecline: OnDeclineRequestClicked);
        }

        void HandleIncomingRequestAdded(FriendData request)
        {
            if (_activeTab != Tab.Requests) return;
            SpawnRequestEntry(request);
        }

        void HandleIncomingRequestRemoved(FriendData request)
        {
            if (_activeTab != Tab.Requests) return;
            RemoveEntryByPlayerId(_spawnedRequests, request.PlayerId);
        }

        #endregion

        #region Friends Tab

        void PopulateFriendsTab()
        {
            ClearSpawned(_spawnedFriends);
            if (!friendsData || !friendsContent || !friendInfoPrefab) return;

            foreach (var friend in friendsData.Friends)
                SpawnFriendEntry(friend);
        }

        void SpawnFriendEntry(FriendData friend)
        {
            var entry = Instantiate(friendInfoPrefab, friendsContent);
            _spawnedFriends.Add(entry.gameObject);

            var status = ResolveOnlineStatus(friend.Availability);

            entry.Populate(
                friend.PlayerId,
                friend.DisplayName,
                ResolveAvatar(0),
                status,
                onInvite: status == FriendInfoEntry.OnlineStatus.Offline ? null : OnInviteClicked);
        }

        void HandleFriendAdded(FriendData friend)
        {
            if (_activeTab == Tab.Friends)
                SpawnFriendEntry(friend);

            // Also refresh online tab — add-friend button may need hiding
            if (_activeTab == Tab.Online)
                PopulateOnlineTab();
        }

        void HandleFriendRemoved(FriendData friend)
        {
            if (_activeTab == Tab.Friends)
                RemoveEntryByPlayerId(_spawnedFriends, friend.PlayerId);
        }

        #endregion

        #region Action Handlers

        async void OnAddFriendClicked(string playerId)
        {
            if (friendsService == null) return;

            try
            {
                await friendsService.SendFriendRequestAsync(playerId);
                CSDebug.Log($"[FriendsListPanel] Friend request sent to {playerId}");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to send friend request: {e.Message}");

                // Reset the add button so user can retry
                var entry = FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId);
                entry?.ResetAddFriendState();
            }
        }

        async void OnInviteClicked(string playerId)
        {
            if (HostConnectionService.Instance == null) return;

            try
            {
                await HostConnectionService.Instance.SendInviteAsync(playerId);
                CSDebug.Log($"[FriendsListPanel] Invite sent to {playerId}");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to send invite: {e.Message}");

                // Reset the invite button so user can retry
                var onlineEntry = FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId);
                if (onlineEntry) { onlineEntry.ResetInviteState(); return; }

                var friendEntry = FindEntryByPlayerId<FriendInfoEntry>(_spawnedFriends, playerId);
                friendEntry?.ResetInviteState();
            }
        }

        async void OnAcceptRequestClicked(string playerId)
        {
            if (friendsService == null) return;

            try
            {
                await friendsService.AcceptFriendRequestAsync(playerId);
                CSDebug.Log($"[FriendsListPanel] Accepted friend request from {playerId}");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to accept request: {e.Message}");
            }
        }

        async void OnDeclineRequestClicked(string playerId)
        {
            if (friendsService == null) return;

            try
            {
                await friendsService.DeclineFriendRequestAsync(playerId);
                CSDebug.Log($"[FriendsListPanel] Declined friend request from {playerId}");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to decline request: {e.Message}");
            }
        }

        async void HandleRefresh()
        {
            if (refreshButton)
                refreshButton.interactable = false;

            if (friendsService != null)
            {
                try { await friendsService.RefreshAsync(); }
                catch (System.Exception e)
                {
                    CSDebug.LogWarning($"[FriendsListPanel] Refresh failed: {e.Message}");
                }
            }

            // Re-populate active tab
            SwitchTab(_activeTab);

            if (refreshButton)
                refreshButton.interactable = true;
        }

        #endregion

        #region Helpers

        Sprite ResolveAvatar(int avatarId)
        {
            if (!profileIcons || profileIcons.profileIcons == null) return null;

            foreach (var icon in profileIcons.profileIcons)
            {
                if (icon.Id == avatarId)
                    return icon.IconSprite;
            }

            return profileIcons.profileIcons.Count > 0
                ? profileIcons.profileIcons[0].IconSprite
                : null;
        }

        static FriendInfoEntry.OnlineStatus ResolveOnlineStatus(int availability)
        {
            // UGS availability: 1=Online, 2=Busy, 3=Away, 0=Offline
            return availability switch
            {
                1 => FriendInfoEntry.OnlineStatus.Online,
                2 => FriendInfoEntry.OnlineStatus.InMatch,
                3 => FriendInfoEntry.OnlineStatus.Online,
                _ => FriendInfoEntry.OnlineStatus.Offline
            };
        }

        static void ClearSpawned(List<GameObject> list)
        {
            foreach (var go in list)
            {
                if (go) Destroy(go);
            }
            list.Clear();
        }

        static string GetPlayerId(GameObject go)
        {
            var online = go.GetComponent<OnlineInfoEntry>();
            if (online) return online.PlayerId;

            var friend = go.GetComponent<FriendInfoEntry>();
            if (friend) return friend.PlayerId;

            var request = go.GetComponent<RequestInfoEntry>();
            if (request) return request.PlayerId;

            return null;
        }

        static void RemoveEntryByPlayerId(List<GameObject> list, string playerId)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i]) { list.RemoveAt(i); continue; }

                if (GetPlayerId(list[i]) == playerId)
                {
                    Destroy(list[i]);
                    list.RemoveAt(i);
                    return;
                }
            }
        }

        static T FindEntryByPlayerId<T>(List<GameObject> list, string playerId) where T : MonoBehaviour
        {
            foreach (var go in list)
            {
                if (!go) continue;
                if (GetPlayerId(go) != playerId) continue;
                var entry = go.GetComponent<T>();
                if (entry) return entry;
            }
            return null;
        }

        #endregion
    }
}
