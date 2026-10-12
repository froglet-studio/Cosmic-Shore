using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Controller for the FriendListPanel in Menu_Main.
    ///
    /// All sections render simultaneously (no tab switching):
    ///   • Online     - every online player in the presence lobby. Row background
    ///                  is the invite button; yellowish tint while the invite is
    ///                  pending.
    ///   • Add Friend - a TMP_InputField plus a Send button, and a status line
    ///                  that shows the player's own full Name#1234 while idle.
    ///                  A typed full Name#1234 goes to UGS by name
    ///                  (<see cref="FriendsServiceFacade.SendFriendRequestByNameAsync"/>);
    ///                  a name without the #1234 is resolved against the pilots
    ///                  this panel knows (<see cref="FriendNameResolver"/>) and
    ///                  sent by id (<see cref="FriendsServiceFacade.SendFriendRequestAsync"/>),
    ///                  or refused with a line asking for the full Name#1234. The
    ///                  facade stays the single writer of FriendsDataSO; the same
    ///                  anti-spam cooldown the Online row's Invite/Cancel/Kick use.
    ///   • Recent     - the other humans of your last online matches
    ///                  (<see cref="RecentPlayersStore"/>), newest first, each with an
    ///                  add-friend button that goes through
    ///                  <see cref="FriendsServiceFacade.SendFriendRequestAsync"/> behind
    ///                  the same anti-spam gate and status line as Add Friend. A pilot
    ///                  who is already a friend, or already asked, shows that state
    ///                  instead of the button (read from FriendsDataSO).
    ///   • Requests   - incoming friend requests AND incoming party invites
    ///                  combined, with Accept/Decline buttons. The party-invite rows
    ///                  are the INBOX: every open invite the service holds
    ///                  (<see cref="HostConnectionService.CopyPendingIncomingInvites"/>),
    ///                  re-read each time the panel opens, so an invite whose popup
    ///                  auto-hid is still here for its full lifetime and a second
    ///                  invite never displaces the first. Opening the panel marks the
    ///                  inbox seen, which clears the party panel's unread badge.
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

        [Header("Add Friend (send a friend request by name or by Name#1234)")]
        [Tooltip("Where the player types who to befriend: a full UGS Name#1234 (sent by name), or a name " +
                 "without the #1234, which is matched against the pilots this panel knows (online, party, " +
                 "recently played with, friend lists) and sent by player id. The three Add Friend fields " +
                 "are wired together or not at all: leave all three empty and the panel simply has no send " +
                 "surface (Online + Requests still work); wire only some of them and ValidateSceneWiring says so.")]
        [SerializeField] private TMP_InputField addFriendNameInput;
        [Tooltip("Sends the typed name through FriendsServiceFacade (by name for a Name#1234, by id for a " +
                 "known pilot), the single writer of FriendsDataSO. Disabled while a send is in flight.")]
        [SerializeField] private Button addFriendSendButton;
        [Tooltip("One readable line: while idle, the player's own full Name#1234 (what a friend types to add " +
                 "them); after a send, whether it went or why not. When unwired, send feedback goes to the " +
                 "toast instead, like the Accept/Decline feedback does, and the own tag is not shown.")]
        [SerializeField] private TMP_Text addFriendStatusText;

        [Header("Recent (pilots from your last online matches)")]
        [Tooltip("Content RectTransform of the Recent scroll view (ScrollRect > Viewport > Content). Rows " +
                 "are requestInfoPrefab in its RecentPlayer shape: the Accept glyph is the add-friend " +
                 "button, Decline is hidden. Leave empty and the panel simply has no RECENT section.")]
        [SerializeField] private Transform recentContent;
        [Tooltip("Optional line shown in place of rows while the list is empty (nobody met online yet). " +
                 "Toggled by the panel; hidden the moment the first match is recorded.")]
        [SerializeField] private TMP_Text recentEmptyText;

        [Header("Audio")]
        [Tooltip("Category played when a party invite is received.")]
        [SerializeField] private MenuAudioCategory inviteReceivedAudio = MenuAudioCategory.Confirmed;
        [Tooltip("Category played when a Send (friend request) press is accepted by the anti-spam gate.")]
        [SerializeField] private MenuAudioCategory sendRequestAudio = MenuAudioCategory.OptionClick;

        [Header("Settings")]
        [Tooltip("Seconds before an incoming request auto-declines. 0 = no expiry.")]
        [SerializeField] private float friendRequestExpirationSeconds = 600f;
        [Tooltip("FALLBACK only: the seconds an incoming party-invite row lives in the Requests list " +
                 "when HostConnectionService has no record of the invite. Normally the row's clock is " +
                 "the service's inbox (HostConnectionService.outgoingInviteTimeoutSeconds, the same " +
                 "value the sender's outgoing invite expires on, counted from when the invite was " +
                 "first observed), so a row spawned by re-opening the panel 40 s after the invite " +
                 "arrived shows the 20 s it has left rather than a fresh 60, and both sides clear " +
                 "together (host reverts the invitee to 'online' and can re-invite).\n\n" +
                 "This is NOT a pure human reaction window: the recipient's clock starts when " +
                 "their lobby POLL observes the invite, which is a 0.75-1.5s refresh plus RTT " +
                 "plus any rate-limit backoff behind the moment the host sent it. 10s was the " +
                 "shipped value and left a cross-continent player only a few seconds to answer.")]
        [SerializeField] private float partyInviteExpirationSeconds = 60f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between consecutive Send (friend request) presses: the same gate " +
                 "OnlineInfoEntry puts on Invite / Cancel / Kick (actionCooldownSeconds), so every " +
                 "social action throttles alike. Unscaled, so it still throttles while the menu is paused.")]
        [SerializeField] private float sendRequestCooldownSeconds = 0.4f;

        [Inject] private FriendsServiceFacade friendsService;

        readonly List<GameObject> _spawnedOnline = new();
        readonly List<GameObject> _spawnedRequests = new();
        readonly List<GameObject> _spawnedRecent = new();

        /// <summary>Currently-pending party invites keyed by sender PlayerId (a mirror of the service's inbox).</summary>
        readonly Dictionary<string, PartyInviteData> _pendingPartyInvites = new();

        /// <summary>Scratch for copying the inbox out of the service without allocating per open.</summary>
        readonly List<PartyInviteData> _rehydrateScratch = new();

        /// <summary>
        /// The modal window this panel lives in (Arcade, Arena, Mission or Toybox screen modal), or
        /// null for a panel placed outside one. Those modals never deactivate - they hide by
        /// CanvasGroup alpha - so this panel can be ENABLED while nothing of it is on screen, and
        /// "the player has seen the inbox" has to be read off the modal, not off OnEnable.
        /// </summary>
        ModalWindowManager _ownerModal;
        bool _ownerModalResolved;

        /// <summary>
        /// True when the player can actually see this panel: enabled, and its owning modal (if
        /// any) is open. A hidden modal's copy still receives every SOAP raise and keeps its rows
        /// current, but it must not count the inbox as seen.
        /// </summary>
        bool IsShownToPlayer => isActiveAndEnabled && (OwnerModal == null || OwnerModal.IsOpen);

        ModalWindowManager OwnerModal
        {
            get
            {
                if (!_ownerModalResolved)
                {
                    _ownerModal = GetComponentInParent<ModalWindowManager>(true);
                    _ownerModalResolved = true;
                }
                return _ownerModal;
            }
        }

        /// <summary>PlayerIds for whom we've already sent an invite (keeps row in pending tint).</summary>
        readonly HashSet<string> _outgoingInvitePlayerIds = new();

        /// <summary>Guards the one-frame deferred request-row reconcile after a list Clear.</summary>
        bool _requestsReconcilePending;

        /// <summary>Guards the one-frame deferred RECENT re-render after a relationship change.</summary>
        bool _recentRefreshPending;

        /// <summary>True from an accepted Send press until the facade call returns (success or not).</summary>
        bool _sendRequestInFlight;

        /// <summary>Unscaled time before which the next Send press is ignored (anti-spam gate).</summary>
        float _nextSendAllowedTime;

        /// <summary>True while the status line shows the idle own-tag line rather than send feedback.</summary>
        bool _addFriendLineIsIdle;

        /// <summary>True while the own tag is being fetched from UGS (one fetch at a time).</summary>
        bool _ownTagFetchInFlight;

        /// <summary>Scratch for the pilots a typed name is matched against (no allocation per send).</summary>
        readonly List<FriendNameResolver.KnownPilot> _knownPilotsScratch = new();

        #region Unity Lifecycle

        void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);

            if (addFriendSendButton)
                addFriendSendButton.onClick.AddListener(OnSendFriendRequestClicked);

            // Enter in the name field sends too, so a keyboard player never has to reach for the button.
            if (addFriendNameInput)
                addFriendNameInput.onSubmit.AddListener(OnAddFriendNameSubmitted);
        }

        void OnEnable()
        {
            ValidateSceneWiring();
            ResetAddFriendSection();
            RehydrateOutgoingInvitesFromService();
            RehydratePendingInvitesFromService();
            SubscribeSoap();
            SubscribeServiceEvents();
            PopulateAll();

            // The inbox is on screen: nothing in it is unread any more (the party panel's badge
            // reads this count). Only when the player can see it: the modal this panel lives in
            // hides by alpha, so an enable is not an open there - its OnModalOpened is.
            if (OwnerModal != null)
                OwnerModal.OnModalOpened += HandleOwnerModalOpened;
            MarkInboxSeenIfShown();

            // Pull fresh presence/invite data the moment the panel opens so the
            // user never sees stale "X Players Online" or a missing invite row
            // that was added between polling ticks. Debounced and mutex-aware
            // inside HostConnectionService, so bursty opens are safe.
            HostConnectionService.Instance?.ForceRefreshNow();
        }

        /// <summary>
        /// Tells the service the inbox has been looked at, when (and only when) this panel is
        /// actually in front of the player. The party panel's unread badge reads that count.
        /// </summary>
        void MarkInboxSeenIfShown()
        {
            if (!IsShownToPlayer) return;
            HostConnectionService.Instance?.MarkIncomingInvitesSeen();
        }

        /// <summary>
        /// The modal this panel lives in came up with the panel already open in it (it was left
        /// open on an earlier visit, or an invite spawned rows while the modal was hidden): now
        /// the player can see the rows, so the inbox is seen.
        /// </summary>
        void HandleOwnerModalOpened() => MarkInboxSeenIfShown();

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

            // The Add Friend section is optional as a whole, never in part: a panel with an input
            // and no button (or a button and no input) is a send surface that silently cannot send.
            int addFriendWired = (addFriendNameInput ? 1 : 0) + (addFriendSendButton ? 1 : 0) + (addFriendStatusText ? 1 : 0);
            if (addFriendWired != 0 && addFriendWired != 3)
                CSDebug.LogWarning($"[FriendsListPanel] Add Friend section on '{name}' is only partly wired " +
                                   $"({addFriendWired}/3 of addFriendNameInput, addFriendSendButton, addFriendStatusText). " +
                                   "Wire all three or none.", this);

            // The empty-state line is meaningless without the list it stands in for.
            if (recentEmptyText && !recentContent)
                CSDebug.LogWarning($"[FriendsListPanel] recentEmptyText is wired on '{name}' but recentContent is not: " +
                                   "the RECENT section needs its Content RectTransform to render rows.", this);
        }

        /// <summary>
        /// Re-reads EVERY still-open party invite from the service's inbox
        /// (<see cref="HostConnectionService.CopyPendingIncomingInvites"/>) into
        /// <see cref="_pendingPartyInvites"/>. This closes the gap where invites arrived while
        /// the panel was hidden - the OnEnable SOAP subscription missed those raises - and it
        /// used to read only the LATEST one, so a second invite that arrived before the panel
        /// was opened hid the first. The service is the source of truth, so the local map is
        /// rebuilt from it rather than merged; without a service (offline) it is left as is.
        /// </summary>
        void RehydratePendingInvitesFromService()
        {
            var service = HostConnectionService.Instance;
            if (service == null) return;

            _pendingPartyInvites.Clear();
            service.CopyPendingIncomingInvites(_rehydrateScratch);
            foreach (var invite in _rehydrateScratch)
                _pendingPartyInvites[invite.HostPlayerId] = invite;
            _rehydrateScratch.Clear();
        }

        void OnDisable()
        {
            if (OwnerModal != null)
                OwnerModal.OnModalOpened -= HandleOwnerModalOpened;

            UnsubscribeSoap();
            UnsubscribeServiceEvents();

            // The deferred reconcile coroutine dies with the disable; clear its guard so
            // the next OnCleared after re-enable can schedule a fresh one. (OnEnable
            // repopulates everything from scratch anyway.)
            _requestsReconcilePending = false;
            _recentRefreshPending = false;
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

            // The RECENT rows show the relationship state (FRIENDS / REQUEST SENT / SENT YOU A
            // REQUEST), so any of the three lists changing re-renders them. The facade rebuilds
            // each list as Clear() + Add() per sync; the refresh is coalesced to one frame.
            if (friendsData)
            {
                SubscribeRecentRefresh(friendsData.Friends);
                SubscribeRecentRefresh(friendsData.OutgoingRequests);
                SubscribeRecentRefresh(friendsData.IncomingRequests);
            }
        }

        void SubscribeRecentRefresh(ScriptableListFriendData list)
        {
            if (list == null) return;
            list.OnItemAdded += HandleRelationshipChangedForRecent;
            list.OnItemRemoved += HandleRelationshipChangedForRecent;
            list.OnCleared += HandleRelationshipsClearedForRecent;
        }

        void UnsubscribeRecentRefresh(ScriptableListFriendData list)
        {
            if (list == null) return;
            list.OnItemAdded -= HandleRelationshipChangedForRecent;
            list.OnItemRemoved -= HandleRelationshipChangedForRecent;
            list.OnCleared -= HandleRelationshipsClearedForRecent;
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

            if (friendsData)
            {
                UnsubscribeRecentRefresh(friendsData.Friends);
                UnsubscribeRecentRefresh(friendsData.OutgoingRequests);
                UnsubscribeRecentRefresh(friendsData.IncomingRequests);
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
            PopulateRecentSection();
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

            // The ✕ doubles as a kick when this row is a member of MY party and I'm the
            // host (KickPartyMemberAsync is host-only). Otherwise: no kick affordance.
            bool canKick = status == OnlineInfoEntry.Status.InYourParty &&
                           connectionData != null && connectionData.IsPartyHost;

            // A full LOCAL party can't take another member - render every remote
            // row non-invitable instead of letting the send fail at the service.
            // Re-evaluated on every party-member change (HandlePartyMemberChanged
            // repopulates the section), so rows free up when someone leaves.
            // The GAME'S rule (4), not the transport capacity: gating the invite button on
            // HasOpenSlots would offer a fifth and sixth seat that only exist as headroom.
            bool localPartyFull = connectionData != null && !connectionData.HasOpenDisplaySlots;

            entry.Populate(
                player.PlayerId,
                player.DisplayName,
                ResolveAvatar(player.AvatarId),
                status,
                memberCount,
                maxSlots,
                matchName,
                onInvite: localPartyFull ? null : OnInviteClicked,
                onCancel: OnCancelInviteClicked,
                onKick: canKick ? OnKickMemberClicked : null,
                joinMode: ResolveJoinMode(player, status),
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
            // ALWAYS the local display size (4). A remote's published PartyMaxSlots is only a
            // fallback for a peer that has not published one, and it is CLAMPED to our own
            // display size: a peer on an older build still publishes the transport capacity, and
            // "x/6" must never reach the screen. The party size is a game rule, identical for
            // everyone, so it is not actually a per-peer value at all.
            int localDisplay = connectionData != null ? connectionData.PartyDisplaySlots : 0;
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
        }

        void HandleOnlinePlayersCleared()
        {
            ClearSpawned(_spawnedOnline);
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
                ResolvePartyInviteRowLifetime(invite.HostPlayerId),
                onAccept: OnAcceptPartyInviteClicked,
                onDecline: OnDeclinePartyInviteClicked);
        }

        /// <summary>
        /// How long the row for that sender's invite lives: exactly the time the service's inbox
        /// says the invite has LEFT (its clock started when the invite was first observed, not when
        /// this panel happened to open), so the row, the popup's countdown and the sender's own
        /// timeout are one value and cannot drift apart. 0 (no expiry) when the inbox never expires
        /// invites. <see cref="partyInviteExpirationSeconds"/> stands in only when the service has
        /// no record of it (offline, or a row spawned from a raise the service has not booked yet).
        /// </summary>
        float ResolvePartyInviteRowLifetime(string senderPlayerId)
        {
            var service = HostConnectionService.Instance;
            if (service == null) return partyInviteExpirationSeconds;

            float remaining = service.IncomingInviteSecondsRemaining(senderPlayerId);
            if (float.IsPositiveInfinity(remaining)) return 0f;
            return remaining > 0f ? remaining : partyInviteExpirationSeconds;
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
        /// Fires when an invite is accepted, declined, withdrawn or expired from ANY source (this
        /// panel, the HomeScreen notification popup, the service's sweep). The raise carries no
        /// payload, so the rows are RECONCILED against the service's inbox: only the invites it
        /// no longer holds open lose their row. Clearing everything here used to make a Decline
        /// of one sender's invite silently drop the other sender's. Without a service (offline)
        /// every invite row goes, as before.
        /// </summary>
        void HandleInviteResolved()
        {
            if (_pendingPartyInvites.Count == 0) return;

            var service = HostConnectionService.Instance;

            _resolvedScratch.Clear();
            foreach (var senderId in _pendingPartyInvites.Keys)
                if (service == null || !service.IsIncomingInvitePending(senderId))
                    _resolvedScratch.Add(senderId);

            foreach (var senderId in _resolvedScratch)
            {
                _pendingPartyInvites.Remove(senderId);
                RemoveRequestEntryByKind(senderId, RequestInfoEntry.Kind.PartyInvite);
            }
            _resolvedScratch.Clear();
        }

        /// <summary>Scratch for the senders a resolve raise closed (no allocation per raise).</summary>
        readonly List<string> _resolvedScratch = new();

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

            // The new row is in the list; it counts as seen only if the player can see the list
            // (a hidden modal's copy of this panel receives this raise too).
            MarkInboxSeenIfShown();
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
                CSDebug.LogVerbose(CSLogChannel.Party, $"[FriendsListPanel] Invite sent to {playerId}");
                // Row stays pending. Cleared when target accepts/declines/times out.
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to send invite: {e.Message}");
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
                ToastNotificationAPI.Show("That party can't be joined right now.");
                PopulateOnlineSection();
                return;
            }

            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                ToastNotificationAPI.Show("Party controller not available.");
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
                ToastNotificationAPI.Show("Failed to join party.");
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
                ToastNotificationAPI.Show("That match can't be spectated right now.");
                PopulateOnlineSection();
                return;
            }

            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                ToastNotificationAPI.Show("Party controller not available.");
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
                ToastNotificationAPI.Show("Failed to spectate.");
                PopulateOnlineSection();
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

            // Optimistic UI: destroy the row now rather than waiting for
            // HostConnectionService.AcceptInviteAsync to raise OnInviteResolved.
            // That raise runs AFTER the ~100-500ms NetworkManager shutdown +
            // stale-ref reset in PartyInviteController.AcceptInviteAsync, so
            // without this the invite row lingers visibly through the transition.
            RemoveRequestEntryByKind(hostPlayerId, RequestInfoEntry.Kind.PartyInvite);

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

            // Optimistic UI: same rationale as OnAcceptPartyInviteClicked.
            RemoveRequestEntryByKind(hostPlayerId, RequestInfoEntry.Kind.PartyInvite);

            var controller = PartyInviteController.Instance;
            if (controller == null) return;

            try
            {
                // ONE sender's invite: any other open invite keeps its row.
                await controller.DeclineInviteAsync(hostPlayerId);
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Decline party invite failed: {e.Message}");
            }
        }

        #endregion

        #region Add Friend Section

        void OnAddFriendNameSubmitted(string _) => OnSendFriendRequestClicked();

        /// <summary>
        /// The Send button (or Enter in the name field): send a friend request to whoever the typed
        /// text names. Anti-spam gate first (same discipline as Invite/Cancel/Kick), then the text is
        /// resolved (<see cref="FriendNameResolver"/>): a full Name#1234 goes to UGS by name, a name
        /// without the #1234 that exactly one known pilot carries goes by that pilot's id, and
        /// anything else (nothing typed, yourself, nobody or several pilots by that name) is refused
        /// here with a line asking for the full Name#1234, since UGS resolves no other form of a
        /// name. One request at a time - the button is disabled while the facade call is in flight.
        /// Every outcome lands on the status line; the facade's own await carries .AsMainThread(), so
        /// the continuation here is on the main thread.
        /// </summary>
        async void OnSendFriendRequestClicked()
        {
            if (_sendRequestInFlight) return;
            if (!TryBeginSendAction()) return;

            string typed = addFriendNameInput ? addFriendNameInput.text?.Trim() : null;
            var target = FriendNameResolver.Resolve(typed, CollectKnownPilots(), LocalPlayerId,
                friendsService != null ? friendsService.CachedLocalFriendTag : null);

            switch (target.Outcome)
            {
                case FriendNameResolver.Outcome.Empty:
                    ShowAddFriendStatus("Type a pilot's name or their Name#1234.");
                    return;
                case FriendNameResolver.Outcome.Self:
                    ShowAddFriendStatus("That's you. Friends add you by your tag.");
                    return;
                case FriendNameResolver.Outcome.Ambiguous:
                    ShowAddFriendStatus($"More than one {typed}. Type their full Name#1234.");
                    return;
                case FriendNameResolver.Outcome.Unknown:
                    ShowAddFriendStatus($"Don't know {typed}. Type their full Name#1234.");
                    return;
            }

            bool byId = target.Outcome == FriendNameResolver.Outcome.KnownPilot;
            string pilotName = target.DisplayName;

            // A known pilot's relationship is already on record: say so instead of a round trip
            // UGS would refuse (the same states the RECENT rows read).
            if (byId && ListHasPlayer(friendsData ? friendsData.Friends : null, target.PlayerId))
            {
                ShowAddFriendStatus($"{pilotName} is already your friend.");
                return;
            }
            if (byId && ListHasPlayer(friendsData ? friendsData.OutgoingRequests : null, target.PlayerId))
            {
                ShowAddFriendStatus($"Request to {pilotName} already sent.");
                return;
            }

            AudioSystem.Instance?.PlayMenuAudio(sendRequestAudio);

            if (friendsService == null || !friendsService.IsInitialized)
            {
                ShowAddFriendStatus("Friends service isn't ready. Check your connection and try again.");
                CSDebug.LogVerbose(CSLogChannel.Party,
                    $"[FriendsListPanel] Friend request to '{pilotName}' refused: friends service not ready");
                return;
            }

            _sendRequestInFlight = true;
            SetSendButtonInteractable(false);
            ShowAddFriendStatus($"Sending request to {pilotName}...");

            try
            {
                if (byId)
                    await friendsService.SendFriendRequestAsync(target.PlayerId);
                else
                    await friendsService.SendFriendRequestByNameAsync(target.FullName);

                CSDebug.LogVerbose(CSLogChannel.Party,
                    $"[FriendsListPanel] Friend request sent to '{pilotName}' ({(byId ? "by id " + target.PlayerId : "by name")})");

                if (addFriendNameInput) addFriendNameInput.SetTextWithoutNotify(string.Empty);
                ShowAddFriendStatus($"Friend request sent to {pilotName}.");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to send friend request to '{pilotName}': {e.Message}");
                ShowAddFriendStatus($"Couldn't send to {pilotName}: {e.Message}");
            }
            finally
            {
                _sendRequestInFlight = false;
                SetSendButtonInteractable(true);
            }
        }

        /// <summary>The local player's UGS id, as the presence layer knows it; null before it does.</summary>
        string LocalPlayerId => connectionData ? connectionData.LocalPlayerId : null;

        /// <summary>
        /// Every pilot this panel knows by id and name, for a typed name to be matched against: the
        /// presence lobby, the party, recently played with, and the friend, incoming and outgoing
        /// request lists. Duplicates of one pilot are fine (the resolver keys on the id).
        /// </summary>
        List<FriendNameResolver.KnownPilot> CollectKnownPilots()
        {
            _knownPilotsScratch.Clear();

            if (connectionData)
            {
                AddPartyPilots(connectionData.OnlinePlayers);
                AddPartyPilots(connectionData.PartyMembers);
            }

            foreach (var record in RecentPlayersStore.Entries)
                _knownPilotsScratch.Add(new FriendNameResolver.KnownPilot(record.PlayerId, record.DisplayName));

            if (friendsData)
            {
                AddFriendPilots(friendsData.Friends);
                AddFriendPilots(friendsData.IncomingRequests);
                AddFriendPilots(friendsData.OutgoingRequests);
            }

            return _knownPilotsScratch;
        }

        void AddPartyPilots(ScriptableListPartyPlayerData list)
        {
            if (list == null) return;
            foreach (var player in list)
                _knownPilotsScratch.Add(new FriendNameResolver.KnownPilot(player.PlayerId, player.DisplayName));
        }

        void AddFriendPilots(ScriptableListFriendData list)
        {
            if (list == null) return;
            foreach (var friend in list)
                _knownPilotsScratch.Add(new FriendNameResolver.KnownPilot(friend.PlayerId, friend.DisplayName));
        }

        /// <summary>
        /// Anti-spam gate for the Send button, the twin of OnlineInfoEntry.TryBeginAction. Returns
        /// false (and the press is ignored) within <see cref="sendRequestCooldownSeconds"/> of the
        /// previous accepted press; otherwise arms the next window and returns true. Unscaled time.
        /// </summary>
        bool TryBeginSendAction()
        {
            if (Time.unscaledTime < _nextSendAllowedTime)
                return false;
            _nextSendAllowedTime = Time.unscaledTime + Mathf.Max(0f, sendRequestCooldownSeconds);
            return true;
        }

        /// <summary>
        /// On every open: the status line back to its idle own-tag line and a Send button that
        /// reflects whether a send is still in flight from before the panel was hidden (the typed
        /// name is kept).
        /// </summary>
        void ResetAddFriendSection()
        {
            ShowOwnTagLine();
            SetSendButtonInteractable(!_sendRequestInFlight);
        }

        /// <summary>
        /// The status line's idle state: "Your tag: Name#1234", the player's own full UGS name, which
        /// is what a friend types to add them and which the game shows nowhere else (every other
        /// surface strips the #1234). Empty until the tag is known; when the Authentication cache
        /// has none yet it is fetched once and filled in, unless send feedback took the line first.
        /// </summary>
        void ShowOwnTagLine()
        {
            if (!addFriendStatusText) return;

            string tag = friendsService != null ? friendsService.CachedLocalFriendTag : null;
            addFriendStatusText.text = FormatOwnTag(tag);
            _addFriendLineIsIdle = true;

            if (string.IsNullOrEmpty(tag) && friendsService != null && !_ownTagFetchInFlight)
                FetchOwnTagIntoIdleLine();
        }

        async void FetchOwnTagIntoIdleLine()
        {
            _ownTagFetchInFlight = true;
            string tag;
            try
            {
                tag = await friendsService.FetchLocalFriendTagAsync();
            }
            finally
            {
                _ownTagFetchInFlight = false;
            }

            // The panel may be gone (Menu_Main unloaded during the fetch), or a send's feedback may
            // own the line by now. `this` first: Unity's null check on a destroyed panel.
            if (!this || !addFriendStatusText || !_addFriendLineIsIdle || string.IsNullOrEmpty(tag)) return;
            addFriendStatusText.text = FormatOwnTag(tag);
        }

        static string FormatOwnTag(string tag) => string.IsNullOrEmpty(tag) ? string.Empty : $"Your tag: {tag}";

        void SetSendButtonInteractable(bool interactable)
        {
            if (addFriendSendButton) addFriendSendButton.interactable = interactable;
        }

        /// <summary>
        /// The one line of send feedback. The section's own label when it is wired; otherwise the
        /// toast, which is how this panel already reports Accept/Decline outcomes.
        /// </summary>
        void ShowAddFriendStatus(string line)
        {
            if (addFriendStatusText)
            {
                addFriendStatusText.text = line;
                _addFriendLineIsIdle = false;
                return;
            }

            ToastNotificationAPI.Show(line);
        }

        #endregion

        #region Recent Section

        /// <summary>
        /// Rebuilds the RECENT rows from <see cref="RecentPlayersStore.Entries"/> (newest first).
        /// Each row's label and button follow the pilot's relationship state in FriendsDataSO.
        /// </summary>
        void PopulateRecentSection()
        {
            ClearSpawned(_spawnedRecent);
            if (!recentContent || !requestInfoPrefab)
            {
                if (recentEmptyText) recentEmptyText.gameObject.SetActive(false);
                return;
            }

            // The store is the signed-in account's own list and drops that account's id on load;
            // the local id is skipped here as well, the way the Online section skips it, so no
            // path can draw the pilot a row offering a friend request to themselves.
            string localId = connectionData ? connectionData.LocalPlayerId : null;
            var nowUtc = DateTime.UtcNow;
            int shown = 0;
            foreach (var record in RecentPlayersStore.Entries)
            {
                if (!string.IsNullOrEmpty(localId) && record.PlayerId == localId) continue;
                SpawnRecentEntry(record, nowUtc);
                shown++;
            }

            if (recentEmptyText) recentEmptyText.gameObject.SetActive(shown == 0);
        }

        void SpawnRecentEntry(RecentPlayerRecord record, DateTime nowUtc)
        {
            var entry = Instantiate(requestInfoPrefab, recentContent);
            _spawnedRecent.Add(entry.gameObject);

            string label = ResolveRecentLabel(record, nowUtc, out bool addable);
            entry.PopulateRecentPlayer(
                record.PlayerId,
                record.DisplayName,
                ResolveAvatar(record.AvatarId),
                label,
                addable ? OnAddRecentPlayerClicked : null);
        }

        /// <summary>
        /// What a RECENT row says about this pilot, and whether it still offers the add-friend
        /// button. Settled relationships (already friends, request already out) show the state and
        /// no button; a pilot who asked US first keeps the button, since the facade's add on an
        /// incoming request is the accept that makes the friendship mutual.
        /// </summary>
        string ResolveRecentLabel(RecentPlayerRecord record, DateTime nowUtc, out bool addable)
        {
            if (ListHasPlayer(friendsData ? friendsData.Friends : null, record.PlayerId))
            {
                addable = false;
                return "FRIENDS";
            }

            if (ListHasPlayer(friendsData ? friendsData.OutgoingRequests : null, record.PlayerId))
            {
                addable = false;
                return "REQUEST SENT";
            }

            addable = true;
            if (ListHasPlayer(friendsData ? friendsData.IncomingRequests : null, record.PlayerId))
                return "SENT YOU A REQUEST";

            return "PLAYED " + RecentPlayersStore.FormatLastPlayed(record.GetLastPlayedUtc(), nowUtc);
        }

        static bool ListHasPlayer(ScriptableListFriendData list, string playerId)
        {
            if (list == null || string.IsNullOrEmpty(playerId)) return false;

            foreach (var friend in list)
                if (friend.PlayerId == playerId) return true;

            return false;
        }

        string FindRecentDisplayName(string playerId)
        {
            foreach (var record in RecentPlayersStore.Entries)
                if (record.PlayerId == playerId) return record.DisplayName;

            return "that pilot";
        }

        /// <summary>
        /// A RECENT row's add-friend button: send a friend request to that pilot by id through the
        /// facade. The same gate, in-flight lock and status line as the Add Friend send, so every
        /// social action throttles and reports alike. The row disabled its own button on the
        /// press; the section is re-rendered afterwards so the row shows REQUEST SENT, or gets
        /// its button back when the press was refused or the send failed.
        /// </summary>
        async void OnAddRecentPlayerClicked(string playerId)
        {
            if (_sendRequestInFlight || !TryBeginSendAction())
            {
                PopulateRecentSection();
                return;
            }

            string pilotName = FindRecentDisplayName(playerId);
            AudioSystem.Instance?.PlayMenuAudio(sendRequestAudio);

            if (friendsService == null || !friendsService.IsInitialized)
            {
                ShowAddFriendStatus("Friends service isn't ready. Check your connection and try again.");
                CSDebug.LogVerbose(CSLogChannel.Party,
                    $"[FriendsListPanel] Friend request to recent pilot '{pilotName}' refused: friends service not ready");
                PopulateRecentSection();
                return;
            }

            _sendRequestInFlight = true;
            SetSendButtonInteractable(false);
            ShowAddFriendStatus($"Sending request to {pilotName}...");

            try
            {
                await friendsService.SendFriendRequestAsync(playerId);
                CSDebug.LogVerbose(CSLogChannel.Party,
                    $"[FriendsListPanel] Friend request sent to recent pilot '{pilotName}' ({playerId})");
                ShowAddFriendStatus($"Friend request sent to {pilotName}.");
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[FriendsListPanel] Failed to send friend request to recent pilot '{pilotName}': {e.Message}");
                ShowAddFriendStatus($"Couldn't send to {pilotName}: {e.Message}");
            }
            finally
            {
                _sendRequestInFlight = false;
                SetSendButtonInteractable(true);

                // The facade synced FriendsDataSO before returning (its await carries .AsMainThread()),
                // so the rows can be rebuilt here, on the main thread, in their new state. Unless the
                // panel is gone: Menu_Main can unload during the await (a launch, or a party pulling
                // the player into a match), and touching the destroyed panel's gameObject threw a
                // MissingReferenceException out of this async void. `this` first: Unity's null check.
                if (this && isActiveAndEnabled)
                    PopulateRecentSection();
            }
        }

        void HandleRelationshipChangedForRecent(FriendData _) => ScheduleRecentRefresh();

        void HandleRelationshipsClearedForRecent() => ScheduleRecentRefresh();

        /// <summary>
        /// One re-render of the RECENT rows next frame, however many list events arrive this frame
        /// (the facade's sync is a Clear() followed by one Add() per relationship).
        /// </summary>
        void ScheduleRecentRefresh()
        {
            if (_recentRefreshPending || !recentContent || !gameObject.activeInHierarchy) return;
            _recentRefreshPending = true;
            StartCoroutine(RefreshRecentNextFrame());
        }

        System.Collections.IEnumerator RefreshRecentNextFrame()
        {
            yield return null;
            _recentRefreshPending = false;
            PopulateRecentSection();
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
    }
}
