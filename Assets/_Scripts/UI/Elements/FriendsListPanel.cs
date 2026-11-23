using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Controller for the FriendListPanel in Menu_Main.
    ///
    /// Both sections render simultaneously (no tab switching):
    ///   • Online   - every online player in the presence lobby. Row background
    ///                is the invite button; yellowish tint while the invite is
    ///                pending.
    ///   • Requests - incoming friend requests AND incoming party invites
    ///                combined, with Accept/Decline buttons.
    ///
    /// Sound plays when a party invite is received.
    /// </summary>
    public class FriendsListPanel : MonoBehaviour
    {
        [Header("Words (toasts; empty shows nothing)")]
        [Tooltip("The connection service is not up yet.")]
        [SerializeField] string serviceNotReadyLine = "Connection service not ready. Try again shortly.";
        [Tooltip("A party that refuses a join.")]
        [SerializeField] string partyNotJoinableLine = "That party can't be joined right now.";
        [Tooltip("The party controller is missing (join, spectate, accept).")]
        [SerializeField] string partyControllerMissingLine = "Party controller not available.";
        [Tooltip("A join that failed.")]
        [SerializeField] string joinFailedLine = "Failed to join party.";
        [Tooltip("A match that refuses a spectator.")]
        [SerializeField] string matchNotSpectatableLine = "That match can't be spectated right now.";
        [Tooltip("A spectate that failed.")]
        [SerializeField] string spectateFailedLine = "Failed to spectate.";
        [Tooltip("The friends service is not up yet.")]
        [SerializeField] string friendsNotReadyLine = "Friends service not ready.";
        [Tooltip("A friend request accepted.")]
        [SerializeField] string requestAcceptedLine = "Friend request accepted!";
        [Tooltip("A friend request that failed to accept. {error} is the reason.")]
        [SerializeField] string acceptFailedLine = "Failed to accept: {error}";
        [Tooltip("A party invite that failed to accept.")]
        [SerializeField] string inviteAcceptFailedLine = "Failed to accept party invite.";

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

        [Header("Style-B Party Panel")]
        [Tooltip("The lists sit inside the party panel, always on screen (style-B menu redesign, item 3): " +
                 "an empty slot's add button moves focus into them instead of opening this panel. " +
                 "Off for the legacy overlay, which opens and closes.")]
        [SerializeField] private bool embedded;
        [Tooltip("List members of YOUR party among the online players (\"IN YOUR PARTY\"). The style-B " +
                 "panel shows them in its seats directly above, so it turns this off.")]
        [SerializeField] private bool listPartyMembers = true;

        [Header("Rev 40 E: one verb per row, rows grouped by it (Docs/MENU_REDESIGN_B.md section 107)")]
        [Tooltip("Each online row offers ONE thing to do, chosen by what the pilot is doing: in the menu, Invite; in " +
                 "another party, Join (dimmed while it is full); in a match, Spectate; invited by you, Cancel. Off, a " +
                 "row offers every verb that applies (a solo pilot in the menu showed INVITE and JOIN side by side).")]
        [SerializeField] private bool oneVerbPerRow;
        [Tooltip("Where each kind of row goes. A row moves when its state changes (an invite sent or cancelled, a " +
                 "party filled, a match started). A kind with no group stays in Online Content. Each group's " +
                 "header hides itself while its list is empty (KitListCounter on the group's content).")]
        [SerializeField] private OnlineGroup[] onlineGroups = { };
        [Tooltip("The line shown while nobody else is online (the groups are all empty). Optional.")]
        [SerializeField] private CanvasGroup onlineEmptyLine;

        /// <summary>What can be done with an online pilot: the group their row sits in.</summary>
        public enum OnlineGroupKind { JoinTheirParty = 0, InTheMenu = 1, InAMatch = 2, YouInvited = 3 }

        [Serializable]
        public struct OnlineGroup
        {
            [Tooltip("Which rows this group holds.")]
            public OnlineGroupKind kind;
            [Tooltip("The group's list: rows are parented here.")]
            public Transform content;
        }

        [Header("Audio")]
        [Tooltip("Category played when a party invite is received.")]
        [SerializeField] private MenuAudioCategory inviteReceivedAudio = MenuAudioCategory.Confirmed;

        [Header("Settings")]
        [Tooltip("Seconds before an incoming request auto-declines. 0 = no expiry.")]
        [SerializeField] private float friendRequestExpirationSeconds = 600f;
        [Tooltip("Seconds the incoming party-invite row lives in the Requests list before it is " +
                 "auto-removed. Kept in step with the host's outgoing-invite timeout so both sides " +
                 "clear together (host reverts the invitee to 'online' and can re-invite).\n\n" +
                 "This is NOT a pure human reaction window: the recipient's clock starts when " +
                 "their lobby POLL observes the invite, which is a 0.75-1.5s refresh plus RTT " +
                 "plus any rate-limit backoff behind the moment the host sent it. 10s was the " +
                 "shipped value and left a cross-continent player only a few seconds to answer.")]
        [SerializeField] private float partyInviteExpirationSeconds = 60f;

        [Inject] private FriendsServiceFacade friendsService;

        readonly List<GameObject> _spawnedOnline = new();
        readonly List<GameObject> _spawnedRequests = new();

        /// <summary>Currently-pending party invites keyed by sender PlayerId.</summary>
        readonly Dictionary<string, PartyInviteData> _pendingPartyInvites = new();

        /// <summary>PlayerIds for whom we've already sent an invite (keeps row in pending tint).</summary>
        readonly HashSet<string> _outgoingInvitePlayerIds = new();

        /// <summary>Guards the one-frame deferred request-row reconcile after a list Clear.</summary>
        bool _requestsReconcilePending;

        #region Unity Lifecycle

        void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);
        }

        void OnEnable()
        {
            ValidateSceneWiring();
            RehydrateOutgoingInvitesFromService();
            RehydratePendingInviteFromService();
            SubscribeSoap();
            SubscribeServiceEvents();
            PopulateAll();

            // Pull fresh presence/invite data the moment the panel opens so the
            // user never sees stale "X Players Online" or a missing invite row
            // that was added between polling ticks. Debounced and mutex-aware
            // inside HostConnectionService, so bursty opens are safe.
            HostConnectionService.Instance?.ForceRefreshNow();

            // While the list is on screen the presence safety poll runs at WATCHED_POLL_SECONDS (a quit that
            // sent no leave drops off in seconds), and the Friends tab re-reads UGS Friends every
            // FRIENDS_REFRESH_SECONDS: its push alone left a friend who quit showing online.
            if (!_holdsWatchDemand)
            {
                PresenceWatchDemand.Acquire();
                _holdsWatchDemand = true;
            }
            _friendsRefreshRoutine = StartCoroutine(FriendsRefreshLoop());
        }

        /// <summary>Seconds between UGS Friends re-reads while the panel is visible.</summary>
        const float FRIENDS_REFRESH_SECONDS = 15f;

        bool      _holdsWatchDemand;
        Coroutine _friendsRefreshRoutine;

        System.Collections.IEnumerator FriendsRefreshLoop()
        {
            var wait = new WaitForSecondsRealtime(FRIENDS_REFRESH_SECONDS);
            while (true)
            {
                if (friendsService != null && friendsService.IsInitialized)
                {
                    CSDebug.LogVerbose(CSLogChannel.Party, "[FriendsListPanel] Friends refresh (panel visible).");
                    friendsService.RefreshAsync().Forget();
                }
                yield return wait;
            }
        }

        /// <summary>
        /// Pulls every still-outstanding outgoing invite from the service so the
        /// pending tint is restored on rows the user invited before this panel
        /// was last opened (e.g. if they navigated away mid-invite).
        /// </summary>
        void RehydrateOutgoingInvitesFromService()
        {
            _outgoingInvitePlayerIds.Clear();
            var service = HostConnectionService.Instance;
            if (service == null) return;
            foreach (var id in service.OutgoingInviteTargets)
                _outgoingInvitePlayerIds.Add(id);
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
                CSDebug.LogError($"[FriendsListPanel] onlineContent is null on '{name}'. " +
                               "Online rows will NOT render. Wire the Content RectTransform " +
                               "of the Online ScrollRect in the inspector.", this);
            if (requestsContent == null)
                CSDebug.LogError($"[FriendsListPanel] requestsContent is null on '{name}'. " +
                               "Request rows will NOT render. Wire the Content RectTransform " +
                               "of the Requests ScrollRect in the inspector.", this);
            if (onlineInfoPrefab == null)
                CSDebug.LogError($"[FriendsListPanel] onlineInfoPrefab is null on '{name}'.", this);
            if (requestInfoPrefab == null)
                CSDebug.LogError($"[FriendsListPanel] requestInfoPrefab is null on '{name}'.", this);
        }

        /// <summary>
        /// Pulls the most recently-received, still-unresolved party invite from
        /// <see cref="HostConnectionService.LastPendingInvite"/> and seeds it
        /// into <see cref="_pendingPartyInvites"/>. This closes the gap where
        /// an invite arrived while the panel was hidden - without this, the
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
            if (_holdsWatchDemand)
            {
                PresenceWatchDemand.Release();
                _holdsWatchDemand = false;
            }
            if (_friendsRefreshRoutine != null)
            {
                StopCoroutine(_friendsRefreshRoutine);
                _friendsRefreshRoutine = null;
            }

            UnsubscribeSoap();
            UnsubscribeServiceEvents();

            // The deferred reconcile coroutine dies with the disable; clear its guard so
            // the next OnCleared after re-enable can schedule a fresh one. (OnEnable
            // repopulates everything from scratch anyway.)
            _requestsReconcilePending = false;
        }

        void SubscribeServiceEvents()
        {
            var service = HostConnectionService.Instance;
            if (service == null) return;
            service.OutgoingInviteCleared += HandleOutgoingInviteCleared;
        }

        void UnsubscribeServiceEvents()
        {
            var service = HostConnectionService.Instance;
            if (service == null) return;
            service.OutgoingInviteCleared -= HandleOutgoingInviteCleared;
        }

        /// <summary>
        /// Service-side cleared the outgoing invite (target accepted, declined,
        /// went offline, or timed out). Drop the pending tracking and revert any
        /// rendered row back to its default ONLINE / IN LOBBY state.
        /// </summary>
        void HandleOutgoingInviteCleared(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            _outgoingInvitePlayerIds.Remove(playerId);

            var entry = FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId);
            if (entry != null) entry.ResetInviteState();
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
                friendsData.IncomingRequests.OnCleared += HandleIncomingFriendRequestsCleared;
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
                friendsData.IncomingRequests.OnCleared -= HandleIncomingFriendRequestsCleared;
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

        /// <summary>True when the lists live inside the party panel rather than in an overlay.</summary>
        public bool IsEmbedded => embedded;

        /// <summary>
        /// Puts gamepad / keyboard focus on the first live row control - an invite to answer first,
        /// then someone to invite. Nothing to focus (nobody online, nothing pending) leaves focus
        /// where it is: the empty-state line is already in view.
        /// </summary>
        public void FocusList()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (!events) return;

            foreach (var content in new[] { requestsContent, onlineContent })
            {
                if (!content) continue;
                foreach (var s in content.GetComponentsInChildren<Selectable>(false))
                {
                    if (!s.IsInteractable() || s.navigation.mode == Navigation.Mode.None) continue;
                    events.SetSelectedGameObject(s.gameObject);
                    return;
                }
            }
        }

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
            UpdateOnlineEmpty();
            if (!connectionData || !onlineContent || !onlineInfoPrefab) return;

            string localId = connectionData.LocalPlayerId;

            foreach (var player in connectionData.OnlinePlayers)
            {
                if (player.PlayerId == localId) continue;
                if (!listPartyMembers && IsInSameParty(player.PlayerId)) continue;
                SpawnOnlineEntry(player);
            }
        }

        void SpawnOnlineEntry(PartyPlayerData player)
        {
            var entry = Instantiate(onlineInfoPrefab, onlineContent);
            _spawnedOnline.Add(entry.gameObject);
            // Every state change the row draws (populate, invite sent, invite cleared) moves it to its group.
            entry.ProfileChanged += () => PlaceInGroup(entry);
            PopulateOnlineEntry(entry, player);
            UpdateOnlineEmpty();
        }

        /// <summary>The group a row belongs in, from what it draws. Pure, so it is tested without rows.</summary>
        internal static OnlineGroupKind GroupFor(OnlineInfoEntry.Status status, bool invitePending)
        {
            if (invitePending) return OnlineGroupKind.YouInvited;
            return status switch
            {
                OnlineInfoEntry.Status.InMatch => OnlineGroupKind.InAMatch,
                OnlineInfoEntry.Status.InLobby or OnlineInfoEntry.Status.LobbyFull => OnlineGroupKind.JoinTheirParty,
                _ => OnlineGroupKind.InTheMenu,
            };
        }

        void PlaceInGroup(OnlineInfoEntry entry)
        {
            if (!entry || onlineGroups == null || onlineGroups.Length == 0) return;
            var kind = GroupFor(entry.CurrentStatus, entry.IsInvitePending);
            foreach (var group in onlineGroups)
            {
                if (group.kind != kind || !group.content) continue;
                if (entry.transform.parent != group.content) entry.transform.SetParent(group.content, false);
                return;
            }
        }

        void UpdateOnlineEmpty()
        {
            if (!onlineEmptyLine) return;
            int live = 0;
            foreach (var go in _spawnedOnline) if (go) live++;
            KitVisibility.SetShown(onlineEmptyLine, live == 0);
        }

        /// <summary>
        /// The one verb a row offers (<see cref="oneVerbPerRow"/>): whether it may invite and what its join button does,
        /// by the pilot's status. Pure, so it is tested without a panel.
        /// </summary>
        internal static (bool invite, OnlineInfoEntry.JoinMode join) OneVerb(OnlineInfoEntry.Status status, OnlineInfoEntry.JoinMode join)
        {
            return status switch
            {
                // A pilot alone in the menu hosts their own party: the row's one verb is Invite (KitOnlineRow shows it
                // over Join for this status), but Join stays on the row's state so their profile card offers it too
                // (Shombith, 2026-10-31: "there is no join button").
                OnlineInfoEntry.Status.Online => (true, join),
                OnlineInfoEntry.Status.InLobby or OnlineInfoEntry.Status.LobbyFull => (false, join),
                OnlineInfoEntry.Status.InMatch => (false, join),
                _ => (false, OnlineInfoEntry.JoinMode.Hidden),
            };
        }

        void PopulateOnlineEntry(OnlineInfoEntry entry, PartyPlayerData player)
        {
            var status = ResolveRemoteStatus(player, out int memberCount, out int maxSlots, out string matchName);

            // The ✕ doubles as a kick when this row is a member of MY party and I'm the
            // host (KickPartyMemberAsync is host-only). Otherwise: no kick affordance.
            bool canKick = status == OnlineInfoEntry.Status.InYourParty &&
                           connectionData != null && connectionData.IsPartyHost;

            // A full LOCAL party can't take another member - render every remote
            // row non-invitable instead of letting the send fail at the service.
            // Re-evaluated on every party-member change (HandlePartyMemberChanged
            // repopulates the section), so rows free up when someone leaves.
            bool localPartyFull = connectionData != null && !connectionData.HasOpenSlots;

            var joinMode = ResolveJoinMode(player, status);
            bool mayInvite = !localPartyFull;
            if (oneVerbPerRow)
            {
                var (invite, join) = OneVerb(status, joinMode);
                mayInvite &= invite;
                joinMode = join;
            }

            entry.Populate(
                player.PlayerId,
                player.DisplayName,
                ResolveAvatar(player.AvatarId),
                status,
                memberCount,
                maxSlots,
                matchName,
                onInvite: mayInvite ? OnInviteClicked : null,
                onCancel: OnCancelInviteClicked,
                onKick: canKick ? OnKickMemberClicked : null,
                joinMode: joinMode,
                onJoin: OnJoinClicked,
                onSpectate: OnSpectateClicked);

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
            // ALWAYS the local party size (4). A remote's published PartyMaxSlots is only a
            // fallback for a peer that has not published one: a peer on an older build may still
            // publish 6, and "x/6" must never reach the screen. The party size is a game rule,
            // identical for everyone, so it is not actually a per-peer value at all.
            int localDisplay = connectionData != null ? connectionData.MaxPartySlots : 0;
            maxSlots = localDisplay > 0
                     ? localDisplay
                     : Mathf.Max(0, player.PartyMaxSlots);
            matchName = player.MatchName;

            // Already in MY party → non-invitable "IN YOUR PARTY" (Task 1). Highest
            // priority: a party member is in *my* lobby, not somewhere else. OnlineInfoEntry
            // makes this status non-invitable, so the row disables + relabels (it is NOT
            // hidden - the party member stays visible as a status indicator).
            if (IsInSameParty(player.PlayerId))
                return OnlineInfoEntry.Status.InYourParty;

            // In-match takes priority (over lobby states).
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

        /// <summary>
        /// What the row's JOIN / SPECTATE button does for this player. The verb follows the
        /// status the row already resolved; whether it is LIVE follows whether the player
        /// advertises a session at all (<see cref="PartyPlayerData.HasJoinableSession"/> - a
        /// peer on an older build, or one spectating somebody else, publishes none) and, for a
        /// join, whether their party has a seat left.
        /// </summary>
        internal static OnlineInfoEntry.JoinMode ResolveJoinMode(PartyPlayerData player, OnlineInfoEntry.Status status)
        {
            switch (status)
            {
                // Already with them - there is nothing to join and nothing to watch from outside.
                case OnlineInfoEntry.Status.InYourParty:
                    return OnlineInfoEntry.JoinMode.Hidden;
                // A pilot mid-match can be WATCHED, never joined as a player: the button becomes
                // the eye, and it is the only live control on the row.
                case OnlineInfoEntry.Status.InMatch:
                    return player.HasJoinableSession
                        ? OnlineInfoEntry.JoinMode.Spectate
                        : OnlineInfoEntry.JoinMode.SpectateDisabled;
                case OnlineInfoEntry.Status.LobbyFull:
                    return player.HasJoinableSession
                        ? OnlineInfoEntry.JoinMode.JoinDisabled
                        : OnlineInfoEntry.JoinMode.Hidden;
                default:
                    return player.HasJoinableSession
                        ? OnlineInfoEntry.JoinMode.Join
                        : OnlineInfoEntry.JoinMode.Hidden;
            }
        }

        bool TryGetOnlinePlayer(string playerId, out PartyPlayerData player)
        {
            player = default;
            if (connectionData?.OnlinePlayers == null) return false;
            foreach (var p in connectionData.OnlinePlayers)
            {
                if (p.PlayerId != playerId) continue;
                player = p;
                return true;
            }
            return false;
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

            if (!listPartyMembers && IsInSameParty(player.PlayerId))
            {
                RemoveEntryByPlayerId(_spawnedOnline, player.PlayerId);
                UpdateOnlineEmpty();
                return;
            }

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
            UpdateOnlineEmpty();
        }

        void HandleOnlinePlayersCleared()
        {
            ClearSpawned(_spawnedOnline);
            UpdateOnlineEmpty();
        }

        /// <summary>
        /// When local party membership changes, re-render the online section so the
        /// "LOBBY FULL" and "invitable" states for every row update correctly.
        /// Also clears any outgoing "PENDING REQUEST" tint for the player that just
        /// joined - otherwise the sender's row stays stuck on the yellow pulse
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
            // Dedup: FriendsServiceFacade rebuilds IncomingRequests as Clear() + Add()
            // on EVERY relationship/presence sync, so OnItemAdded re-fires for requests
            // that already have a row. Without this guard each re-sync stacked another
            // duplicate row for the same request.
            if (FindRequestEntryByKind(request.PlayerId, RequestInfoEntry.Kind.FriendRequest) != null)
                return;

            SpawnFriendRequestEntry(request);
        }

        void HandleIncomingFriendRequestRemoved(FriendData request)
        {
            RemoveRequestEntryByKind(request.PlayerId, RequestInfoEntry.Kind.FriendRequest);
        }

        /// <summary>
        /// The facade's sync path rebuilds IncomingRequests as Clear() + Add() on every
        /// sync, and ScriptableList.Clear raises only OnCleared - never per-item
        /// OnItemRemoved - so resolved requests would otherwise leave stale rows behind.
        /// The rebuild happens synchronously right after the Clear, so reconciling here
        /// would see an empty list and destroy every row only for the Add upserts to
        /// respawn them (resetting each row's expiry countdown). Defer one frame and
        /// sweep only the rows whose request no longer exists. Party-invite rows are
        /// untouched - they are driven by _pendingPartyInvites, not this list.
        /// </summary>
        void HandleIncomingFriendRequestsCleared()
        {
            if (_requestsReconcilePending) return;
            _requestsReconcilePending = true;
            StartCoroutine(ReconcileFriendRequestRowsNextFrame());
        }

        System.Collections.IEnumerator ReconcileFriendRequestRowsNextFrame()
        {
            yield return null;
            _requestsReconcilePending = false;

            for (int i = _spawnedRequests.Count - 1; i >= 0; i--)
            {
                var go = _spawnedRequests[i];
                if (!go) { _spawnedRequests.RemoveAt(i); continue; }

                var entry = go.GetComponent<RequestInfoEntry>();
                if (entry == null || entry.EntryKind != RequestInfoEntry.Kind.FriendRequest) continue;
                if (IsIncomingFriendRequest(entry.PlayerId)) continue;

                Destroy(go);
                _spawnedRequests.RemoveAt(i);
            }
        }

        bool IsIncomingFriendRequest(string playerId)
        {
            if (friendsData == null || friendsData.IncomingRequests == null) return false;

            foreach (var request in friendsData.IncomingRequests)
                if (request.PlayerId == playerId) return true;

            return false;
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
            // immediately - without this, the spawned RequestInfoEntry lives
            // under an inactive panel and the recipient has no visual cue
            // beyond the notification popup.
            if (!gameObject.activeSelf)
                Show();

            // If an INVITE row already exists for this sender, leave it (refresh of existing entry).
            // By kind, like the friend-request path: a pending friend-request row from the same
            // player used to count as "existing", so no invite row was made and - once the popup
            // auto-hid - the player had no way left to accept the invite.
            var existing = FindRequestEntryByKind(invite.HostPlayerId, RequestInfoEntry.Kind.PartyInvite);
            if (existing == null)
                SpawnPartyInviteEntry(invite);
        }

        #endregion

        #region Action Handlers

        async void OnInviteClicked(string playerId)
        {
            if (HostConnectionService.Instance == null)
            {
                ShowToast(serviceNotReadyLine, ToastKind.Error);
                FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId)?.ResetInviteState();
                return;
            }

            // Mark outgoing so re-renders preserve the pending tint.
            _outgoingInvitePlayerIds.Add(playerId);

            try
            {
                await HostConnectionService.Instance.SendInviteAsync(playerId);
                CSDebug.LogVerbose(CSLogChannel.Party, $"[FriendsListPanel] Invite sent to {playerId}");
                // Row stays pending. Cleared when target accepts/declines/times out.
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to send invite: {e.Message}");
                // No presence lobby yet (it is being rejoined, B37): the click used to just reset the row,
                // so the invite seemed to do nothing. Say so.
                if (HostConnectionService.Instance != null && !HostConnectionService.Instance.HasPresenceLobby)
                    ShowToast(serviceNotReadyLine, ToastKind.Error);
                _outgoingInvitePlayerIds.Remove(playerId);
                FindEntryByPlayerId<OnlineInfoEntry>(_spawnedOnline, playerId)?.ResetInviteState();
            }
        }

        // The host clicked the ✕ on a pending row. Retract the outgoing invite - HostConnectionService
        // re-publishes invite_payloads without it (the recipient's invite/popup/row vanish) and fires
        // OutgoingInviteCleared, which reverts the row to online. The row also resets optimistically.
        async void OnCancelInviteClicked(string playerId)
        {
            _outgoingInvitePlayerIds.Remove(playerId);
            if (HostConnectionService.Instance == null) return;

            try
            {
                await HostConnectionService.Instance.CancelInviteAsync(playerId);
                CSDebug.LogVerbose(CSLogChannel.Party, $"[FriendsListPanel] Invite to {playerId} cancelled");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to cancel invite: {e.Message}");
            }
        }

        // The host clicked the ✕ on a row for a member of their own party. Remove that
        // member via HostConnectionService (host-only). OnPartyMemberKicked re-renders the
        // online section, so the row reverts to its invitable state on success.
        async void OnKickMemberClicked(string playerId)
        {
            if (HostConnectionService.Instance == null) return;

            try
            {
                await HostConnectionService.Instance.KickPartyMemberAsync(playerId);
                CSDebug.LogVerbose(CSLogChannel.Party, $"[FriendsListPanel] Kicked {playerId} from party");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to kick member: {e.Message}");
                // Re-render so the optimistically-hidden ✕ recovers if the kick didn't take.
                PopulateOnlineSection();
            }
        }

        // The JOIN button: move this machine into that player's party with no invite. The
        // controller covers the screen and runs the same shutdown → join → connect → ready →
        // bounce sequence an accepted invite does; on success this whole panel is gone with
        // the old solo session, so there is nothing to restore here but the failure case.
        async void OnJoinClicked(string playerId)
        {
            if (!TryGetOnlinePlayer(playerId, out var player) || !player.HasJoinableSession)
            {
                ShowToast(partyNotJoinableLine, ToastKind.Refusal);
                PopulateOnlineSection();
                return;
            }

            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                ShowToast(partyControllerMissingLine, ToastKind.Error);
                PopulateOnlineSection();
                return;
            }

            try
            {
                await controller.JoinPartyAsync(player);
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Join party failed: {e.Message}");
                ShowToast(joinFailedLine, ToastKind.Error);
                PopulateOnlineSection();
            }
        }

        // The SPECTATE (eye) button: watch that player's match. Same transition as a join, but
        // this machine connects as a viewer (no Player, no vessel) and SpectatorController
        // takes over from there until the viewer leaves or the match ends.
        async void OnSpectateClicked(string playerId)
        {
            if (!TryGetOnlinePlayer(playerId, out var player) || !player.HasJoinableSession)
            {
                ShowToast(matchNotSpectatableLine, ToastKind.Refusal);
                PopulateOnlineSection();
                return;
            }

            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                ShowToast(partyControllerMissingLine, ToastKind.Error);
                PopulateOnlineSection();
                return;
            }

            try
            {
                await controller.SpectateAsync(player);
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Spectate failed: {e.Message}");
                ShowToast(spectateFailedLine, ToastKind.Error);
                PopulateOnlineSection();
            }
        }

        async void OnAcceptFriendRequestClicked(string playerId)
        {
            if (friendsService == null || !friendsService.IsInitialized)
            {
                ShowToast(friendsNotReadyLine, ToastKind.Error);
                return;
            }

            try
            {
                await friendsService.AcceptFriendRequestAsync(playerId);
                ShowToast(requestAcceptedLine, ToastKind.Success);
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to accept request: {e.Message}");
                ShowToast(TemplateText.Fill(acceptFailedLine, "error", e.Message), ToastKind.Error);
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

            // Optimistic UI: destroy the row now rather than waiting for
            // HostConnectionService.AcceptInviteAsync to raise OnInviteResolved.
            // That raise runs AFTER the ~100-500ms NetworkManager shutdown +
            // stale-ref reset in PartyInviteController.AcceptInviteAsync, so
            // without this the invite row lingers visibly through the transition.
            RemoveRequestEntryByKind(hostPlayerId, RequestInfoEntry.Kind.PartyInvite);

            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                ShowToast(partyControllerMissingLine, ToastKind.Error);
                return;
            }

            try
            {
                await controller.AcceptInviteAsync(invite);
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Accept party invite failed: {e.Message}");
                ShowToast(inviteAcceptFailedLine, ToastKind.Error);
            }
        }

        async void OnDeclinePartyInviteClicked(string hostPlayerId)
        {
            _pendingPartyInvites.Remove(hostPlayerId);

            // Optimistic UI: same rationale as OnAcceptPartyInviteClicked.
            RemoveRequestEntryByKind(hostPlayerId, RequestInfoEntry.Kind.PartyInvite);

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
            if (!profileIcons || profileIcons.ProfileIcons == null) return null;

            foreach (var icon in profileIcons.ProfileIcons)
            {
                if (icon.Id == avatarId)
                    return icon.IconSprite;
            }

            return profileIcons.ProfileIcons.Count > 0
                ? profileIcons.ProfileIcons[0].IconSprite
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

                if (!go.TryGetComponent(out RequestInfoEntry entry)) continue;
                if (entry.PlayerId != playerId || entry.EntryKind != kind) continue;

                Destroy(go);
                _spawnedRequests.RemoveAt(i);
                return;
            }
        }

        /// <summary>
        /// Finds a request row matching playerId AND kind, so a friend-request lookup
        /// never matches a party-invite row from the same sender (and vice versa).
        /// </summary>
        RequestInfoEntry FindRequestEntryByKind(string playerId, RequestInfoEntry.Kind kind)
        {
            foreach (var go in _spawnedRequests)
            {
                if (!go) continue;

                var entry = go.GetComponent<RequestInfoEntry>();
                if (entry != null && entry.PlayerId == playerId && entry.EntryKind == kind)
                    return entry;
            }

            return null;
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

        static void ShowToast(string line, ToastKind kind)
        {
            if (!string.IsNullOrEmpty(line)) ToastNotificationAPI.Show(line, kind);
        }
    }
}
