using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Widget living inside the ArcadeScreenModal that visualizes the local
    /// player's party in the same style as <see cref="FriendsListPanel"/>'s
    /// header slots - but as its own panel with a leave-party button and a
    /// live "X Players Online" counter.
    ///
    /// <para><b>Seating is SYNCED, not local.</b> The host holds the first slot and the
    /// clients follow in join order, identically on every device - see
    /// <see cref="PartyRoster"/> for why that order is read off Netcode's replicated
    /// <c>OwnerClientId</c> rather than off the per-device <c>PartyMembers</c> list, which
    /// seats whoever is looking at it first. Empty slots expose the "+" add button, which
    /// opens the <see cref="FriendsListPanel"/> (pre-wired in the scene).</para>
    ///
    /// <para><b>Each occupied slot wears its pilot's domain</b> as an animated halo behind the
    /// avatar (<see cref="PartySlotDomainGlow"/>, built by the slot itself). Domain is pushed
    /// LIVE every frame rather than snapshotted at population time: a pilot re-picks their
    /// domain from the same modal this panel lives in, and the platform rule is to read the
    /// live mirror each time rather than cache one at component-creation time.</para>
    ///
    /// All data flows through SOAP events - no direct <see cref="HostConnectionService"/>
    /// references are needed beyond the Leave button callback.
    /// </summary>
    public class ArcadeLobbyList : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private SO_ProfileIconList profileIcons;

        // The live roster of network-spawned players, which is where BOTH synced facts come
        // from: a member's replicated owner client id (the seat order) and their live domain
        // (the halo colour). DI rather than a serialized reference so the four scene instances
        // of this panel need no new wiring.
        [Inject] private GameDataSO gameData;

        [Header("Slots (exactly 4, by design)")]
        [Tooltip("Drawn left-to-right in the SYNCED party order: the host takes slot 0 on every " +
                 "device, then clients in the order they joined. The local player is NOT pinned " +
                 "to slot 0 - a client sees itself wherever it joined.")]
        [SerializeField] private FriendInfoSlot[] slots = new FriendInfoSlot[4];

        [Header("UI")]
        [Tooltip("Text that reads \"N Players Online\" for the presence lobby.")]
        [SerializeField] private TMP_Text onlineStatusText;

        [Tooltip("Leave Party button - disconnects from the current party and returns to Menu_Main.")]
        [SerializeField] private Button leaveButton;

        [Tooltip("Panel opened when an empty slot's '+' button is pressed. " +
                 "Should be the scene-wired FriendsListPanel.")]
        [SerializeField] private FriendsListPanel friendsListPanel;

        /// <summary>Max slots rendered - matches <c>HostConnectionDataSO.MaxPartySlots</c> (4 by design).</summary>
        const int MAX_SLOTS = 4;

        readonly List<PartyRoster.Seat> _seats = new();

        // UgsPlayerId -> what the network says about that pilot. Rebuilt each tick from
        // gameData.Players; a member with no entry has no live Player on this machine.
        readonly Dictionary<string, NetFacts> _netFacts = new();

        // Order-independent hash of the (ugs id, client id) pairs plus the host id - i.e. of
        // everything the SEATING is a function of, and nothing the seating is not (domain is
        // deliberately excluded; re-tinting must not redraw the row). Compared against the
        // fingerprint the drawn seating was built from, so the panel re-seats itself the
        // instant a member's Player spawns or despawns without subscribing every step of the
        // spawn chain - and costs no sort on the frames where nothing moved.
        ulong _netFingerprint;
        ulong _seatedFingerprint;
        bool  _hasSeated;

        struct NetFacts
        {
            public ulong   ClientId;
            public Domains Domain;
        }

        Func<string, ulong> _clientIdLookup;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (leaveButton)
                leaveButton.onClick.AddListener(OnLeaveButtonPressed);

            // Wire every empty slot's add button to open the FriendsListPanel.
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot == null) continue;
                    slot.BindAddButton(OnAddSlotPressed);
                    slot.BindKickButton(OnKickSlotPressed);
                }
            }

            WarnOnSharedSlotReferences();
        }

        // Detect scene-wiring bugs where two FriendInfoSlot instances share the
        // same internal UI child references. When that happens, the last-iterated
        // slot wins - so an empty slot's ClearSlot can overwrite an occupied
        // slot's SetPlayer. PopulateSlots compensates with a two-pass (clear
        // first, then set) ordering, but we also log here so future scene edits
        // don't reintroduce the problem silently.
        void WarnOnSharedSlotReferences()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                var a = slots[i];
                if (a == null) continue;
                for (int j = i + 1; j < slots.Length; j++)
                {
                    var b = slots[j];
                    if (b == null || a == b) continue;
                    if (ReferenceEquals(a.DisplayNameTextGO, b.DisplayNameTextGO) && a.DisplayNameTextGO != null)
                        Debug.LogWarning($"[ArcadeLobbyList] slots[{i}] and slots[{j}] share the same displayNameText GameObject. Rewire in the scene - names will not render correctly for both slots.", this);
                    if (ReferenceEquals(a.AvatarIconGO, b.AvatarIconGO) && a.AvatarIconGO != null)
                        Debug.LogWarning($"[ArcadeLobbyList] slots[{i}] and slots[{j}] share the same avatarIcon GameObject. Rewire in the scene - avatars will not render correctly for both slots.", this);
                }
            }
        }

        void OnEnable()
        {
            SubscribeSoap();
            PopulateAll();

            // Pull fresh lobby data the moment the arcade panel opens so the
            // "N Players Online" counter and party slots reflect server state
            // instead of whatever snapshot happened to be cached when the user
            // last navigated away. Debounced inside HostConnectionService.
            HostConnectionService.Instance?.ForceRefreshNow();
        }

        void Start()
        {
            // [Inject] lands between Awake and Start, so OnEnable's first pass on a
            // scene-loaded panel runs with a null GameDataSO - which resolves NO client ids
            // and NO domains, i.e. a roster that is ordered but colourless. Re-seat now that
            // the container has been read. (Update would heal it a frame later anyway; doing
            // it here means the panel is never drawn wrong even once.)
            PopulateSlots();
        }

        void OnDisable()
        {
            UnsubscribeSoap();
        }

        /// <summary>
        /// Two jobs, both of which have to be per-frame rather than event-driven.
        ///
        /// <para><b>Domain is pushed live.</b> A pilot re-picks their domain from the same
        /// modal this panel sits in and there is no SOAP channel for "somebody's domain
        /// changed" - the platform rule is to read the live <c>Player.Domain</c> mirror each
        /// time rather than snapshot one. Four dictionary lookups a frame.</para>
        ///
        /// <para><b>Seating heals itself.</b> A member's seat is decided by their replicated
        /// owner client id, which only exists once their <c>Player</c> object network-spawns -
        /// several hundred milliseconds after the party event that put them in the list, and on
        /// no channel this panel could usefully subscribe to. So the network facts are re-read
        /// each tick and the roster is re-sorted only when their fingerprint moves; on every
        /// other frame this costs one dictionary rebuild and four lookups.</para>
        /// </summary>
        void Update()
        {
            if (slots == null || slots.Length == 0 || !connectionData) return;

            RebuildNetFacts();

            if (!_hasSeated || _netFingerprint != _seatedFingerprint)
            {
                PopulateSlots();   // re-seats, redraws, and pushes the glows itself
                return;
            }

            PushDomainGlows();
        }

        void SubscribeSoap()
        {
            if (!connectionData) return;

            if (connectionData.PartyMembers != null)
            {
                connectionData.PartyMembers.OnItemAdded += HandlePartyChanged;
                connectionData.PartyMembers.OnItemRemoved += HandlePartyChanged;
                connectionData.PartyMembers.OnCleared += HandlePartyCleared;
            }

            if (connectionData.OnlinePlayers != null)
            {
                connectionData.OnlinePlayers.OnItemAdded += HandleOnlineChanged;
                connectionData.OnlinePlayers.OnItemRemoved += HandleOnlineChanged;
                connectionData.OnlinePlayers.OnCleared += HandleOnlineCleared;
            }

            if (connectionData.OnPartyMemberJoined != null)
                connectionData.OnPartyMemberJoined.OnRaised += HandlePartyMemberEvent;
            if (connectionData.OnPartyMemberLeft != null)
                connectionData.OnPartyMemberLeft.OnRaised += HandlePartyMemberEvent;
            if (connectionData.OnPartyMemberKicked != null)
                connectionData.OnPartyMemberKicked.OnRaised += HandlePartyMemberEvent;

            // Auto-open the friends panel when an incoming party invite arrives
            // while the arcade lobby is visible. Without this, the recipient has
            // to notice the overlay popup and navigate to Arcade → Add slot
            // themselves - the friends panel is already where the Accept/Decline
            // controls live, so surfacing it proactively matches the expected
            // AAA "notification pulls the relevant panel forward" behavior.
            if (connectionData.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised += HandleInviteReceived;

            // Refresh slot 0 when the cloud profile resolves - HostConnectionDataSO
            // may have been populated with the local "Pilot{XXXX}" default at panel
            // open time; without this, the local player's slot keeps stale text/avatar
            // until the next party-member event forces a full repopulate.
            if (PlayerDataService.Instance != null)
                PlayerDataService.Instance.OnProfileChanged += HandleProfileChanged;
        }

        void UnsubscribeSoap()
        {
            if (!connectionData) return;

            if (connectionData.PartyMembers != null)
            {
                connectionData.PartyMembers.OnItemAdded -= HandlePartyChanged;
                connectionData.PartyMembers.OnItemRemoved -= HandlePartyChanged;
                connectionData.PartyMembers.OnCleared -= HandlePartyCleared;
            }

            if (connectionData.OnlinePlayers != null)
            {
                connectionData.OnlinePlayers.OnItemAdded -= HandleOnlineChanged;
                connectionData.OnlinePlayers.OnItemRemoved -= HandleOnlineChanged;
                connectionData.OnlinePlayers.OnCleared -= HandleOnlineCleared;
            }

            if (connectionData.OnPartyMemberJoined != null)
                connectionData.OnPartyMemberJoined.OnRaised -= HandlePartyMemberEvent;
            if (connectionData.OnPartyMemberLeft != null)
                connectionData.OnPartyMemberLeft.OnRaised -= HandlePartyMemberEvent;
            if (connectionData.OnPartyMemberKicked != null)
                connectionData.OnPartyMemberKicked.OnRaised -= HandlePartyMemberEvent;

            if (connectionData.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised -= HandleInviteReceived;

            if (PlayerDataService.Instance != null)
                PlayerDataService.Instance.OnProfileChanged -= HandleProfileChanged;
        }

        void HandleInviteReceived(PartyInviteData _)
        {
            if (friendsListPanel != null && !friendsListPanel.gameObject.activeSelf)
                friendsListPanel.Show();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Population
        // ─────────────────────────────────────────────────────────────────────

        void PopulateAll()
        {
            PopulateSlots();
            UpdateOnlineStatus();
            UpdateLeaveButtonState();
        }

        /// <summary>
        /// Draws the party into the four slots in the SYNCED seating: host first, then clients
        /// in join order, the same on every device.
        ///
        /// <para>Two-pass population (clear the unused slots first, seat everyone second) is
        /// kept from the original: if scene wiring accidentally shares a TMP_Text or Image
        /// GameObject between two slots, the occupied slot's activation then runs last and the
        /// visible name/avatar survive. <see cref="WarnOnSharedSlotReferences"/> reports it.</para>
        /// </summary>
        void PopulateSlots()
        {
            if (slots == null || slots.Length == 0 || !connectionData) return;

            BuildSeats();

            int slotCount = Mathf.Min(slots.Length, MAX_SLOTS);
            int seatCount = Mathf.Min(_seats.Count, slotCount);

            for (int i = seatCount; i < slotCount; i++)
            {
                // Unity's lifetime-aware null, not `?.` - a destroyed slot is not reference-null.
                var empty = slots[i];
                if (empty != null) empty.ClearSlot();
            }

            for (int i = 0; i < seatCount; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                var seat = _seats[i];

                if (seat.IsLocal)
                {
                    PopulateLocalSlot(slot);
                    continue;
                }

                // Only the party host can kick, and never itself - a non-local seat on a host's
                // screen is therefore always kickable.
                slot.SetPlayer(
                    seat.PlayerId,
                    seat.Member.DisplayName,
                    ResolveAvatar(seat.Member.AvatarId),
                    canKick: connectionData.IsPartyHost);
            }

            _seatedFingerprint = _netFingerprint;
            _hasSeated         = true;

            PushDomainGlows();
        }

        /// <summary>
        /// Builds the synced seating into <see cref="_seats"/>.
        ///
        /// <para>The panel draws at most <see cref="MAX_SLOTS"/> seats while the party's
        /// transport capacity is deliberately one higher (anti-flicker headroom - see
        /// <c>HostConnectionDataSO.MaxPartySlots</c>), so a transient fifth member can exist.
        /// If that pushes the LOCAL player past the last drawn slot they are moved INTO it: a
        /// panel that stops showing you your own party is a worse lie than a momentarily
        /// imperfect ordering, and the condition clears itself on the next reconcile.</para>
        /// </summary>
        void BuildSeats()
        {
            RebuildNetFacts();

            _clientIdLookup ??= ResolveClientId;

            PartyRoster.Build(
                connectionData.PartyMembers,
                connectionData.LocalPlayerData,
                ResolveHostClientId(),
                _clientIdLookup,
                _seats);

            int drawable = Mathf.Min(slots.Length, MAX_SLOTS);
            if (_seats.Count <= drawable || drawable <= 0) return;

            int localIdx = -1;
            for (int i = 0; i < _seats.Count; i++)
                if (_seats[i].IsLocal) { localIdx = i; break; }

            if (localIdx >= drawable)
            {
                var local = _seats[localIdx];
                _seats.RemoveAt(localIdx);
                _seats.Insert(drawable - 1, local);
            }
        }

        void PopulateLocalSlot(FriendInfoSlot slot)
        {
            string localId = connectionData.LocalPlayerId;
            string displayName = string.IsNullOrEmpty(connectionData.LocalDisplayName)
                ? "You"
                : connectionData.LocalDisplayName;
            var avatar = ResolveAvatar(connectionData.LocalAvatarId);

            slot.SetAsLocalPlayer(localId, displayName, avatar);
        }

        void UpdateOnlineStatus()
        {
            if (!onlineStatusText || !connectionData) return;

            // OnlinePlayers excludes the local player by design - add 1 so
            // the counter reflects the total player population, which is
            // what players intuitively expect when they read "N Players Online".
            // The Arcade header intentionally shows only the raw count here -
            // "IN LOBBY X/N" / "LOBBY FULL" / "IN A MATCH" badges belong on the
            // per-remote-player rows in FriendsListPanel (OnlineInfoEntry), not
            // on the local player's count of everyone online.
            int remoteCount = connectionData.OnlinePlayers != null
                ? connectionData.OnlinePlayers.Count
                : 0;
            int total = remoteCount + (connectionData.IsConnected ? 1 : 0);

            onlineStatusText.text = total == 1
                ? "1 Player Online"
                : $"{total} Players Online";
        }

        void UpdateLeaveButtonState()
        {
            if (!leaveButton || !connectionData) return;

            // Leaving only makes sense when we have at least one other party
            // member. A solo "leave" is a no-op from the user's perspective.
            leaveButton.interactable = connectionData.RemotePartyMemberCount > 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP Handlers
        // ─────────────────────────────────────────────────────────────────────

        void HandlePartyChanged(PartyPlayerData _)
        {
            PopulateSlots();
            UpdateLeaveButtonState();
        }

        void HandlePartyCleared()
        {
            PopulateSlots();
            UpdateLeaveButtonState();
        }

        void HandleOnlineChanged(PartyPlayerData _) => UpdateOnlineStatus();
        void HandleOnlineCleared() => UpdateOnlineStatus();
        void HandlePartyMemberEvent(PartyPlayerData _)
        {
            PopulateSlots();
            UpdateLeaveButtonState();
            UpdateOnlineStatus();
        }

        void HandleProfileChanged(PlayerProfileData _)
        {
            // The local player is no longer pinned to slot 0 - on a client the host sits there
            // and the local pilot is wherever they joined - so a resolved cloud profile has to
            // repaint whichever seat is actually theirs. Repopulating is the only statement of
            // that which cannot go out of step with the seating.
            PopulateSlots();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Button Callbacks
        // ─────────────────────────────────────────────────────────────────────

        void OnAddSlotPressed()
        {
            if (friendsListPanel != null)
                friendsListPanel.Show();
        }

        async void OnLeaveButtonPressed()
        {
            var service = HostConnectionService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[ArcadeLobbyList] HostConnectionService not available - cannot leave party.");
                return;
            }

            leaveButton.interactable = false;
            try
            {
                await service.LeavePartyAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArcadeLobbyList] Leave party failed: {e.Message}");
                UpdateLeaveButtonState();
            }
        }

        // A slot's ✕ was pressed. Host-only removal of that member; KickPartyMemberAsync
        // guards host/self internally. The optimistic RemovePartyMember inside it fires
        // OnPartyMemberKicked → PopulateSlots, so the slot re-renders either way.
        async void OnKickSlotPressed(string playerId)
        {
            var service = HostConnectionService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[ArcadeLobbyList] HostConnectionService not available - cannot kick.");
                PopulateSlots();
                return;
            }

            try
            {
                await service.KickPartyMemberAsync(playerId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArcadeLobbyList] Kick failed: {e.Message}");
            }

            // Safety net: re-render so a no-op/failed kick re-enables the ✕ that
            // HandleKickClicked disabled optimistically.
            PopulateSlots();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the UGS-id → (client id, domain) map from the live player roster.
        ///
        /// <para>AI are skipped: they carry the HOST's owner client id and an empty UGS id, so
        /// admitting them would both fail to match any party member and, if one ever did, seat
        /// a bot in a human's chair.</para>
        /// </summary>
        void RebuildNetFacts()
        {
            _netFacts.Clear();

            ulong fingerprint = ResolveHostClientId();

            if (gameData == null || gameData.Players == null)
            {
                _netFingerprint = fingerprint;
                return;
            }

            for (int i = 0; i < gameData.Players.Count; i++)
            {
                var player = gameData.Players[i];
                if (player == null) continue;

                // GameDataSO.Players can hold destroyed Players between prunes; a destroyed
                // UnityEngine.Object is not reference-null, so the interface check is not enough.
                if (player is UnityEngine.Object obj && !obj) continue;
                if (player.IsInitializedAsAI) continue;

                var ugsId = player.UgsPlayerId;
                if (string.IsNullOrEmpty(ugsId)) continue;

                var clientId = player.OwnerClientNetId;

                _netFacts[ugsId] = new NetFacts
                {
                    ClientId = clientId,
                    Domain   = player.Domain,
                };

                // Summed, so the result does not depend on the order gameData.Players happens
                // to be in - which changes on its own as players are added and pruned.
                unchecked { fingerprint += (ulong)ugsId.GetHashCode() * 1099511628211UL + clientId + 1UL; }
            }

            _netFingerprint = fingerprint;
        }

        ulong ResolveClientId(string ugsPlayerId) =>
            !string.IsNullOrEmpty(ugsPlayerId) && _netFacts.TryGetValue(ugsPlayerId, out var f)
                ? f.ClientId
                : PartyRoster.UnknownClientId;

        /// <summary>
        /// The client id that means "host". Netcode assigns it to whoever started the session,
        /// which for a party IS the party host, and every peer reads the same value - so it is
        /// the one host identity that needs nothing published to agree on.
        /// </summary>
        static ulong ResolveHostClientId()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening ? nm.ServerClientId : PartyRoster.UnknownClientId;
        }

        /// <summary>
        /// Pushes each drawn seat's LIVE domain colour onto its slot's halo. A seat whose pilot
        /// has no live <c>Player</c> yet gets no halo rather than a default-coloured one - an
        /// unresolved pilot painted Jade is confident misinformation, where an absent halo
        /// reads as "not known yet".
        /// </summary>
        void PushDomainGlows()
        {
            int seatCount = Mathf.Min(_seats.Count, Mathf.Min(slots.Length, MAX_SLOTS));
            var colorSet = gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.ColorSet
                : null;

            for (int i = 0; i < seatCount; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                Color? colour = null;
                if (colorSet != null && _netFacts.TryGetValue(_seats[i].PlayerId, out var facts))
                    colour = colorSet.GetDomainSignalColor(facts.Domain);

                slot.SetDomainGlow(colour);
            }
        }

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
    }
}
