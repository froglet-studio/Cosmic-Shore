using System;
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
    /// Controller for the FriendListPanel in Menu_Main.
    ///
    /// Two tabs:
    ///   • Online  — every online player in the presence lobby. Row background is
    ///               the invite button; yellowish tint while the invite is pending.
    ///   • Requests — incoming friend requests AND incoming party invites combined.
    ///
    /// Each tab has its own refresh button. Sound plays when a party invite is received.
    /// </summary>
    public class FriendsListPanel : MonoBehaviour
    {
        enum Tab { Online, Requests }

        [Header("Tab Buttons")]
        [SerializeField] private Button onlineHeaderButton;
        [SerializeField] private Button requestsHeaderButton;

        [Header("Tab Content Parents (ScrollRect > Viewport > Content)")]
        [SerializeField] private Transform onlineContent;
        [SerializeField] private Transform requestsContent;

        [Header("Tab Roots (CanvasGroup per tab)")]
        [SerializeField] private CanvasGroup onlineCanvasGroup;
        [SerializeField] private CanvasGroup requestsCanvasGroup;

        [Header("Per-Tab Refresh Buttons")]
        [SerializeField] private Button onlineRefreshButton;
        [SerializeField] private Button requestsRefreshButton;

        [Header("Prefabs")]
        [SerializeField] private OnlineInfoEntry onlineInfoPrefab;
        [SerializeField] private RequestInfoEntry requestInfoPrefab;

        [Header("Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private FriendsDataSO friendsData;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Actions")]
        [SerializeField] private Button closeButton;

        [Header("Audio")]
        [Tooltip("Category played when a party invite is received.")]
        [SerializeField] private MenuAudioCategory inviteReceivedAudio = MenuAudioCategory.Confirmed;

        [Header("Settings")]
        [Tooltip("Seconds before an incoming request auto-declines. 0 = no expiry.")]
        [SerializeField] private float friendRequestExpirationSeconds = 600f;
        [Tooltip("Seconds before an incoming party invite auto-declines.")]
        [SerializeField] private float partyInviteExpirationSeconds = 30f;

        [Inject] private FriendsServiceFacade friendsService;

        Tab _activeTab = Tab.Online;
        readonly List<GameObject> _spawnedOnline = new();
        readonly List<GameObject> _spawnedRequests = new();

        /// <summary>Currently-pending party invites keyed by sender PlayerId.</summary>
        readonly Dictionary<string, PartyInviteData> _pendingPartyInvites = new();

        /// <summary>PlayerIds for whom we've already sent an invite (keeps row in pending tint).</summary>
        readonly HashSet<string> _outgoingInvitePlayerIds = new();

        #region Unity Lifecycle

        void Awake()
        {
            // Wire header tab buttons
            if (onlineHeaderButton)
                onlineHeaderButton.onClick.AddListener(() => SwitchTab(Tab.Online));
            if (requestsHeaderButton)
                requestsHeaderButton.onClick.AddListener(() => SwitchTab(Tab.Requests));

            if (closeButton)
                closeButton.onClick.AddListener(Hide);

            if (onlineRefreshButton)
                onlineRefreshButton.onClick.AddListener(HandleOnlineRefresh);
            if (requestsRefreshButton)
                requestsRefreshButton.onClick.AddListener(HandleRequestsRefresh);
        }

        void OnEnable()
        {
            SubscribeSoap();
            // Re-render active tab in case data changed while panel was hidden.
            SwitchTab(_activeTab);
        }

        void OnDisable()
        {
            UnsubscribeSoap();
        }

        void SubscribeSoap()
        {
            if (connectionData)
            {
                if (connectionData.OnlinePlayers != null)
                {
                    connectionData.OnlinePlayers.OnItemAdded += HandleOnlinePlayerChanged;
                    connectionData.OnlinePlayers.OnItemRemoved += HandleOnlinePlayerRemoved;
                    connectionData.OnlinePlayers.OnCleared += HandleOnlinePlayersCleared;
                }

                if (connectionData.OnInviteReceived != null)
                    connectionData.OnInviteReceived.OnRaised += HandlePartyInviteReceived;

                if (connectionData.OnPartyMemberJoined != null)
                    connectionData.OnPartyMemberJoined.OnRaised += HandlePartyMemberChanged;
                if (connectionData.OnPartyMemberLeft != null)
                    connectionData.OnPartyMemberLeft.OnRaised += HandlePartyMemberChanged;
                if (connectionData.OnPartyMemberKicked != null)
                    connectionData.OnPartyMemberKicked.OnRaised += HandlePartyMemberChanged;
            }

            if (friendsData)
            {
                if (friendsData.OnFriendAdded != null)
                    friendsData.OnFriendAdded.OnRaised += HandleFriendAdded;
                if (friendsData.OnFriendRemoved != null)
                    friendsData.OnFriendRemoved.OnRaised += HandleFriendRemoved;
                if (friendsData.IncomingRequests != null)
                {
                    friendsData.IncomingRequests.OnItemAdded += HandleIncomingFriendRequestAdded;
                    friendsData.IncomingRequests.OnItemRemoved += HandleIncomingFriendRequestRemoved;
                }
            }
        }

        void UnsubscribeSoap()
        {
            if (connectionData)
            {
                if (connectionData.OnlinePlayers != null)
                {
                    connectionData.OnlinePlayers.OnItemAdded -= HandleOnlinePlayerChanged;
                    connectionData.OnlinePlayers.OnItemRemoved -= HandleOnlinePlayerRemoved;
                    connectionData.OnlinePlayers.OnCleared -= HandleOnlinePlayersCleared;
                }

                if (connectionData.OnInviteReceived != null)
                    connectionData.OnInviteReceived.OnRaised -= HandlePartyInviteReceived;

                if (connectionData.OnPartyMemberJoined != null)
                    connectionData.OnPartyMemberJoined.OnRaised -= HandlePartyMemberChanged;
                if (connectionData.OnPartyMemberLeft != null)
                    connectionData.OnPartyMemberLeft.OnRaised -= HandlePartyMemberChanged;
                if (connectionData.OnPartyMemberKicked != null)
                    connectionData.OnPartyMemberKicked.OnRaised -= HandlePartyMemberChanged;
            }

            if (friendsData)
            {
                if (friendsData.OnFriendAdded != null)
                    friendsData.OnFriendAdded.OnRaised -= HandleFriendAdded;
                if (friendsData.OnFriendRemoved != null)
                    friendsData.OnFriendRemoved.OnRaised -= HandleFriendRemoved;
                if (friendsData.IncomingRequests != null)
                {
                    friendsData.IncomingRequests.OnItemAdded -= HandleIncomingFriendRequestAdded;
                    friendsData.IncomingRequests.OnItemRemoved -= HandleIncomingFriendRequestRemoved;
                }
            }
        }

        #endregion

        #region Public API

        public void Show()
        {
            gameObject.SetActive(true);
            SwitchTab(_activeTab);
        }

        /// <summary>Opens the panel directly to the Online tab.</summary>
        public void ShowOnlineTab()
        {
            gameObject.SetActive(true);
            SwitchTab(Tab.Online);
        }

        /// <summary>Opens the panel directly to the Requests tab (e.g. on invite received).</summary>
        public void ShowRequestsTab()
        {
            gameObject.SetActive(true);
            SwitchTab(Tab.Requests);
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

            if (tab == Tab.Online) PopulateOnlineTab();
            else PopulateRequestsTab();
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
            PopulateOnlineEntry(entry, player);
        }

        void PopulateOnlineEntry(OnlineInfoEntry entry, PartyPlayerData player)
        {
            bool isFriend = friendsService != null && friendsService.IsInitialized
                && friendsService.IsFriend(player.PlayerId);

            var status = ResolveRemoteStatus(player, out int memberCount, out int maxSlots, out string matchName);

            entry.Populate(
                player.PlayerId,
                player.DisplayName,
                ResolveAvatar(player.AvatarId),
                status,
                memberCount,
                maxSlots,
                matchName,
                isFriend,
                onAddFriend: OnAddFriendClicked,
                onInvite: OnInviteClicked);

            // Preserve pending-invite tint if we have an outgoing invite in flight.
            if (_outgoingInvitePlayerIds.Contains(player.PlayerId))
                entry.SetInvitePending();
        }

        /// <summary>
        /// Decides which Status enum value this remote player renders with.
        /// Prefers published presence-lobby party state when available; falls
        /// back to Online. Also flags LOBBY FULL when the player's party is
        /// reported at max slots and we are not in it.
        /// </summary>
        OnlineInfoEntry.Status ResolveRemoteStatus(
            PartyPlayerData player,
            out int memberCount,
            out int maxSlots,
            out string matchName)
        {
            memberCount = Mathf.Max(0, player.PartyMemberCount);
            maxSlots = player.PartyMaxSlots > 0 ? player.PartyMaxSlots
                      : (connectionData != null ? connectionData.MaxPartySlots : 0);
            matchName = player.MatchName;

            // In-match takes priority.
            if (!string.IsNullOrEmpty(matchName))
                return OnlineInfoEntry.Status.InMatch;

            // Lobby-full: remote has >= max members AND we aren't already in that lobby.
            if (maxSlots > 0 && memberCount >= maxSlots && !IsInSameParty(player.PlayerId))
                return OnlineInfoEntry.Status.LobbyFull;

            // Advertised party with other members (count > 1 means they're not alone).
            if (memberCount > 1)
                return OnlineInfoEntry.Status.InLobby;

            return OnlineInfoEntry.Status.Online;
        }

        bool IsInSameParty(string remotePlayerId)
        {
            if (connectionData?.PartyMembers == null) return false;
            foreach (var m in connectionData.PartyMembers)
                if (m.PlayerId == remotePlayerId) return true;
            return false;
        }

        void HandleOnlinePlayerChanged(PartyPlayerData player)
        {
            if (_activeTab != Tab.Online) return;
            if (connectionData && player.PlayerId == connectionData.LocalPlayerId) return;

            // Upsert: refresh existing row if present, otherwise spawn.
            var existing = FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, player.PlayerId);
            if (existing)
                PopulateOnlineEntry(existing, player);
            else
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

        /// <summary>
        /// When local party membership changes, re-render the online tab so the
        /// "LOBBY FULL" and "invitable" states for every row update correctly.
        /// </summary>
        void HandlePartyMemberChanged(PartyPlayerData _)
        {
            if (_activeTab == Tab.Online)
                PopulateOnlineTab();
        }

        #endregion

        #region Requests Tab

        void PopulateRequestsTab()
        {
            ClearSpawned(_spawnedRequests);
            if (!requestsContent || !requestInfoPrefab) return;

            // Party invites first (more time-sensitive than friend requests).
            foreach (var kv in _pendingPartyInvites)
                SpawnPartyInviteEntry(kv.Value);

            if (friendsData != null && friendsData.IncomingRequests != null)
            {
                foreach (var request in friendsData.IncomingRequests)
                    SpawnFriendRequestEntry(request);
            }
        }

        void SpawnFriendRequestEntry(FriendData request)
        {
            var entry = Instantiate(requestInfoPrefab, requestsContent);
            _spawnedRequests.Add(entry.gameObject);

            entry.Populate(
                request.PlayerId,
                request.DisplayName,
                ResolveAvatar(0),
                RequestInfoEntry.Kind.FriendRequest,
                friendRequestExpirationSeconds,
                onAccept: OnAcceptFriendRequestClicked,
                onDecline: OnDeclineFriendRequestClicked);
        }

        void SpawnPartyInviteEntry(PartyInviteData invite)
        {
            var entry = Instantiate(requestInfoPrefab, requestsContent);
            _spawnedRequests.Add(entry.gameObject);

            entry.Populate(
                invite.HostPlayerId,
                invite.HostDisplayName,
                ResolveAvatar(invite.HostAvatarId),
                RequestInfoEntry.Kind.PartyInvite,
                partyInviteExpirationSeconds,
                onAccept: OnAcceptPartyInviteClicked,
                onDecline: OnDeclinePartyInviteClicked);
        }

        void HandleIncomingFriendRequestAdded(FriendData request)
        {
            if (_activeTab != Tab.Requests) return;
            SpawnFriendRequestEntry(request);
        }

        void HandleIncomingFriendRequestRemoved(FriendData request)
        {
            if (_activeTab != Tab.Requests) return;
            RemoveRequestEntryByKind(request.PlayerId, RequestInfoEntry.Kind.FriendRequest);
        }

        void HandlePartyInviteReceived(PartyInviteData invite)
        {
            // Dedup: multiple raises for the same session should only add one row.
            _pendingPartyInvites[invite.HostPlayerId] = invite;

            // Play notification sound.
            AudioSystem.Instance?.PlayMenuAudio(inviteReceivedAudio);

            if (_activeTab == Tab.Requests)
            {
                // If a row already exists for this sender, leave it (refresh of existing entry).
                var existing = FindEntryByPlayerId<RequestInfoEntry>(_spawnedRequests, invite.HostPlayerId);
                if (existing == null)
                    SpawnPartyInviteEntry(invite);
            }
        }

        #endregion

        #region Friend Added/Removed Handlers

        void HandleFriendAdded(FriendData friend)
        {
            // Online tab may need to hide the add-friend button — re-populate.
            if (_activeTab == Tab.Online)
                PopulateOnlineTab();
        }

        void HandleFriendRemoved(FriendData friend)
        {
            // Online tab needs the add-friend button back.
            if (_activeTab == Tab.Online)
                PopulateOnlineTab();
        }

        #endregion

        #region Action Handlers

        async void OnAddFriendClicked(string playerId)
        {
            if (friendsService == null || !friendsService.IsInitialized)
            {
                ToastNotificationAPI.Show("Friends service not ready. Try again shortly.");
                FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId)?.ResetAddFriendState();
                return;
            }

            try
            {
                await friendsService.SendFriendRequestAsync(playerId);
                ToastNotificationAPI.Show("Friend request sent!");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to send friend request: {e.Message}");
                ToastNotificationAPI.Show($"Failed to send request: {e.Message}");
                FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId)?.ResetAddFriendState();
            }
        }

        async void OnInviteClicked(string playerId)
        {
            if (HostConnectionService.Instance == null)
            {
                ToastNotificationAPI.Show("Connection service not ready. Try again shortly.");
                FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId)?.ResetInviteState();
                return;
            }

            // Mark outgoing so re-renders preserve the pending tint.
            _outgoingInvitePlayerIds.Add(playerId);

            try
            {
                await HostConnectionService.Instance.SendInviteAsync(playerId);
                CSDebug.Log($"[FriendsListPanel] Invite sent to {playerId}");
                // Row stays pending. Cleared when target accepts/declines/times out.
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to send invite: {e.Message}");
                _outgoingInvitePlayerIds.Remove(playerId);
                FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId)?.ResetInviteState();
            }
        }

        async void OnAcceptFriendRequestClicked(string playerId)
        {
            if (friendsService == null || !friendsService.IsInitialized)
            {
                ToastNotificationAPI.Show("Friends service not ready.");
                return;
            }

            try
            {
                await friendsService.AcceptFriendRequestAsync(playerId);
                ToastNotificationAPI.Show("Friend request accepted!");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to accept request: {e.Message}");
                ToastNotificationAPI.Show($"Failed to accept: {e.Message}");
            }
        }

        async void OnDeclineFriendRequestClicked(string playerId)
        {
            if (friendsService == null || !friendsService.IsInitialized) return;

            try
            {
                await friendsService.DeclineFriendRequestAsync(playerId);
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to decline request: {e.Message}");
            }
        }

        async void OnAcceptPartyInviteClicked(string hostPlayerId)
        {
            if (!_pendingPartyInvites.TryGetValue(hostPlayerId, out var invite)) return;
            _pendingPartyInvites.Remove(hostPlayerId);

            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                ToastNotificationAPI.Show("Party controller not available.");
                return;
            }

            try
            {
                await controller.AcceptInviteAsync(invite);
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Accept party invite failed: {e.Message}");
                ToastNotificationAPI.Show("Failed to accept party invite.");
            }
        }

        async void OnDeclinePartyInviteClicked(string hostPlayerId)
        {
            _pendingPartyInvites.Remove(hostPlayerId);

            var controller = PartyInviteController.Instance;
            if (controller == null) return;

            try
            {
                await controller.DeclineInviteAsync();
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Decline party invite failed: {e.Message}");
            }
        }

        async void HandleOnlineRefresh()
        {
            if (onlineRefreshButton) onlineRefreshButton.interactable = false;

            // The HostConnectionService refresh loop polls the presence lobby
            // every ~3s. We can just force a quick re-render; backend data will
            // catch up on the next automatic tick. If you need an immediate
            // force-refresh, the service could expose a public method later.
            PopulateOnlineTab();

            // Small delay so the button click has visible feedback.
            await System.Threading.Tasks.Task.Delay(250);

            if (onlineRefreshButton) onlineRefreshButton.interactable = true;
        }

        async void HandleRequestsRefresh()
        {
            if (requestsRefreshButton) requestsRefreshButton.interactable = false;

            if (friendsService != null)
            {
                try { await friendsService.RefreshAsync(); }
                catch (System.Exception e)
                {
                    CSDebug.LogWarning($"[FriendsListPanel] Refresh failed: {e.Message}");
                }
            }

            PopulateRequestsTab();

            if (requestsRefreshButton) requestsRefreshButton.interactable = true;
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

        static void ClearSpawned(List<GameObject> list)
        {
            foreach (var go in list)
                if (go) Destroy(go);
            list.Clear();
        }

        static string GetPlayerId(GameObject go)
        {
            var online = go.GetComponent<OnlineInfoEntry>();
            if (online) return online.PlayerId;

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

        /// <summary>
        /// Removes a request entry matching the playerId AND kind, so a friend-request
        /// removal doesn't also clear a pending party invite from the same sender.
        /// </summary>
        void RemoveRequestEntryByKind(string playerId, RequestInfoEntry.Kind kind)
        {
            for (int i = _spawnedRequests.Count - 1; i >= 0; i--)
            {
                var go = _spawnedRequests[i];
                if (!go) { _spawnedRequests.RemoveAt(i); continue; }

                var entry = go.GetComponent<RequestInfoEntry>();
                if (entry == null) continue;
                if (entry.PlayerId != playerId || entry.EntryKind != kind) continue;

                Destroy(go);
                _spawnedRequests.RemoveAt(i);
                return;
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
