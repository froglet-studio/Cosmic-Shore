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
    /// Both sections render simultaneously (no tab switching):
    ///   • Online   — every online player in the presence lobby. Row background
    ///                is the invite button; yellowish tint while the invite is
    ///                pending.
    ///   • Requests — incoming friend requests AND incoming party invites
    ///                combined, with Accept/Decline buttons.
    ///
    /// Sound plays when a party invite is received.
    /// </summary>
    public class FriendsListPanel : MonoBehaviour
    {
        [Header("Section Content Parents (ScrollRect > Viewport > Content)")]
        [SerializeField] private Transform onlineContent;
        [SerializeField] private Transform requestsContent;

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

        readonly List<GameObject> _spawnedOnline = new();
        readonly List<GameObject> _spawnedRequests = new();

        /// <summary>Currently-pending party invites keyed by sender PlayerId.</summary>
        readonly Dictionary<string, PartyInviteData> _pendingPartyInvites = new();

        /// <summary>PlayerIds for whom we've already sent an invite (keeps row in pending tint).</summary>
        readonly HashSet<string> _outgoingInvitePlayerIds = new();

        #region Unity Lifecycle

        void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);
        }

        void OnEnable()
        {
            ValidateSceneWiring();
            RehydratePendingInviteFromService();
            SubscribeSoap();
            PopulateAll();

            // Pull fresh presence/invite data the moment the panel opens so the
            // user never sees stale "X Players Online" or a missing invite row
            // that was added between polling ticks. Debounced and mutex-aware
            // inside HostConnectionService, so bursty opens are safe.
            HostConnectionService.Instance?.ForceRefreshNow();
        }

        /// <summary>
        /// Loud diagnostic for scene wiring gaps. Catches the case where
        /// <see cref="onlineContent"/> / <see cref="requestsContent"/> were
        /// left unassigned in the inspector, which silently swallows every
        /// spawned row and leaves the panel looking empty.
        /// </summary>
        void ValidateSceneWiring()
        {
            if (onlineContent == null)
                Debug.LogError($"[FriendsListPanel] onlineContent is null on '{name}'. " +
                               "Online rows will NOT render. Wire the Content RectTransform " +
                               "of the Online ScrollRect in the inspector.", this);
            if (requestsContent == null)
                Debug.LogError($"[FriendsListPanel] requestsContent is null on '{name}'. " +
                               "Request rows will NOT render. Wire the Content RectTransform " +
                               "of the Requests ScrollRect in the inspector.", this);
            if (onlineInfoPrefab == null)
                Debug.LogError($"[FriendsListPanel] onlineInfoPrefab is null on '{name}'.", this);
            if (requestInfoPrefab == null)
                Debug.LogError($"[FriendsListPanel] requestInfoPrefab is null on '{name}'.", this);
        }

        /// <summary>
        /// Pulls the most recently-received, still-unresolved party invite from
        /// <see cref="HostConnectionService.LastPendingInvite"/> and seeds it
        /// into <see cref="_pendingPartyInvites"/>. This closes the gap where
        /// an invite arrived while the panel was hidden — without this, the
        /// OnEnable SOAP subscription would have missed the event and the
        /// rendered Requests section would be empty on first open.
        /// </summary>
        void RehydratePendingInviteFromService()
        {
            var service = HostConnectionService.Instance;
            if (service == null) return;

            var pending = service.LastPendingInvite;
            if (!pending.HasValue) return;

            _pendingPartyInvites[pending.Value.HostPlayerId] = pending.Value;
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

                if (connectionData.OnInviteResolved != null)
                    connectionData.OnInviteResolved.OnRaised += HandleInviteResolved;

                if (connectionData.OnPartyMemberJoined != null)
                    connectionData.OnPartyMemberJoined.OnRaised += HandlePartyMemberChanged;
                if (connectionData.OnPartyMemberLeft != null)
                    connectionData.OnPartyMemberLeft.OnRaised += HandlePartyMemberChanged;
                if (connectionData.OnPartyMemberKicked != null)
                    connectionData.OnPartyMemberKicked.OnRaised += HandlePartyMemberChanged;
            }

            if (friendsData && friendsData.IncomingRequests != null)
            {
                friendsData.IncomingRequests.OnItemAdded += HandleIncomingFriendRequestAdded;
                friendsData.IncomingRequests.OnItemRemoved += HandleIncomingFriendRequestRemoved;
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

                if (connectionData.OnInviteResolved != null)
                    connectionData.OnInviteResolved.OnRaised -= HandleInviteResolved;

                if (connectionData.OnPartyMemberJoined != null)
                    connectionData.OnPartyMemberJoined.OnRaised -= HandlePartyMemberChanged;
                if (connectionData.OnPartyMemberLeft != null)
                    connectionData.OnPartyMemberLeft.OnRaised -= HandlePartyMemberChanged;
                if (connectionData.OnPartyMemberKicked != null)
                    connectionData.OnPartyMemberKicked.OnRaised -= HandlePartyMemberChanged;
            }

            if (friendsData && friendsData.IncomingRequests != null)
            {
                friendsData.IncomingRequests.OnItemAdded -= HandleIncomingFriendRequestAdded;
                friendsData.IncomingRequests.OnItemRemoved -= HandleIncomingFriendRequestRemoved;
            }
        }

        #endregion

        #region Public API

        public void Show()
        {
            gameObject.SetActive(true);
            PopulateAll();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Back-compat aliases for scene-wired UnityEvents that still reference
        // the old tab-switching entry points. Both just open the panel.
        public void ShowOnlineTab() => Show();
        public void ShowRequestsTab() => Show();

        #endregion

        #region Rendering

        void PopulateAll()
        {
            PopulateOnlineSection();
            PopulateRequestsSection();
        }

        #endregion

        #region Online Section

        void PopulateOnlineSection()
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
            var status = ResolveRemoteStatus(player, out int memberCount, out int maxSlots, out string matchName);

            entry.Populate(
                player.PlayerId,
                player.DisplayName,
                ResolveAvatar(player.AvatarId),
                status,
                memberCount,
                maxSlots,
                matchName,
                onInvite: OnInviteClicked,
                onInviteExpired: OnInviteExpired);

            // Preserve pending-invite tint if we have an outgoing invite in flight.
            if (_outgoingInvitePlayerIds.Contains(player.PlayerId))
                entry.SetInvitePending();
        }

        /// <summary>
        /// Called by <see cref="OnlineInfoEntry"/> when its pending-request
        /// countdown hits zero. Clears the outgoing bookkeeping and the
        /// lobby-side invite properties so the sender can try again without
        /// being stuck on PENDING — this is the recovery path for the
        /// "3+ player invite silently dropped" case.
        /// </summary>
        void OnInviteExpired(string playerId)
        {
            _outgoingInvitePlayerIds.Remove(playerId);

            var service = HostConnectionService.Instance;
            if (service == null) return;
            _ = service.CancelOutgoingInviteAsync(playerId);
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
            RemoveEntryByPlayerId(_spawnedOnline, player.PlayerId);

            // Target left the presence lobby (quit / disconnected). Drop any
            // outgoing invite bookkeeping so the sender isn't stuck with a
            // ghost PENDING row once the target comes back online, and clear
            // the lobby-side invite properties so a stale invite_target
            // doesn't linger after the invitee is gone.
            if (_outgoingInvitePlayerIds.Remove(player.PlayerId))
            {
                var service = HostConnectionService.Instance;
                if (service != null)
                    _ = service.CancelOutgoingInviteAsync(player.PlayerId);
            }
        }

        void HandleOnlinePlayersCleared()
        {
            ClearSpawned(_spawnedOnline);
        }

        /// <summary>
        /// When local party membership changes, re-render the online section so the
        /// "LOBBY FULL" and "invitable" states for every row update correctly.
        /// Also clears any outgoing "PENDING REQUEST" tint for the player that just
        /// joined — otherwise the sender's row stays stuck on the yellow pulse
        /// even though the invite has been accepted.
        /// </summary>
        void HandlePartyMemberChanged(PartyPlayerData member)
        {
            if (!string.IsNullOrEmpty(member.PlayerId))
                _outgoingInvitePlayerIds.Remove(member.PlayerId);

            PopulateOnlineSection();
        }

        #endregion

        #region Requests Section

        void PopulateRequestsSection()
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
            SpawnFriendRequestEntry(request);
        }

        void HandleIncomingFriendRequestRemoved(FriendData request)
        {
            RemoveRequestEntryByKind(request.PlayerId, RequestInfoEntry.Kind.FriendRequest);
        }

        /// <summary>
        /// Fires when an invite is accepted/declined from ANY source (this panel,
        /// the HomeScreen notification popup, etc.). Clears every party-invite
        /// row so the UI stays in sync regardless of where the user answered.
        /// </summary>
        void HandleInviteResolved()
        {
            if (_pendingPartyInvites.Count == 0) return;

            _pendingPartyInvites.Clear();

            for (int i = _spawnedRequests.Count - 1; i >= 0; i--)
            {
                var go = _spawnedRequests[i];
                if (!go) { _spawnedRequests.RemoveAt(i); continue; }

                var entry = go.GetComponent<RequestInfoEntry>();
                if (entry == null) continue;
                if (entry.EntryKind != RequestInfoEntry.Kind.PartyInvite) continue;

                Destroy(go);
                _spawnedRequests.RemoveAt(i);
            }
        }

        void HandlePartyInviteReceived(PartyInviteData invite)
        {
            // Dedup: multiple raises for the same session should only add one row.
            _pendingPartyInvites[invite.HostPlayerId] = invite;

            // Play notification sound.
            AudioSystem.Instance?.PlayMenuAudio(inviteReceivedAudio);

            // Auto-open the panel so the user sees the incoming invite row
            // immediately — without this, the spawned RequestInfoEntry lives
            // under an inactive panel and the recipient has no visual cue
            // beyond the notification popup.
            if (!gameObject.activeSelf)
                Show();

            // If a row already exists for this sender, leave it (refresh of existing entry).
            var existing = FindEntryByPlayerId<RequestInfoEntry>(_spawnedRequests, invite.HostPlayerId);
            if (existing == null)
                SpawnPartyInviteEntry(invite);
        }

        #endregion

        #region Action Handlers

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
