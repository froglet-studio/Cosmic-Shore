using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
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
    /// Slot 0 is always the local player (avatar + display name).
    /// Remaining slots render the other <see cref="HostConnectionDataSO.PartyMembers"/>
    /// in order. Empty slots expose the "+" add button, which opens the
    /// <see cref="FriendsListPanel"/> (pre-wired in the scene).
    ///
    /// All data flows through SOAP events - no direct <see cref="HostConnectionService"/>
    /// references are needed beyond the Leave button callback.
    /// </summary>
    public class ArcadeLobbyList : MonoBehaviour, IModalPanel
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Slots (exactly 4, by design)")]
        [Tooltip("Slot 0 is reserved for the local player. Slots 1..3 render remote party members.")]
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

        // ─────────────────────────────────────────────────────────────────────
        // Visibility lifecycle
        //
        // Bound through BOTH OnEnable/OnDisable and IModalPanel, because neither
        // alone is sufficient:
        //
        //   • The arcade panel lives inside a ModalWindowManager, which hides by
        //     fading its CanvasGroup and never calls SetActive(false). So
        //     OnEnable fired exactly ONCE per Menu_Main load and never again when
        //     the player actually opened the panel - meaning the ForceRefreshNow
        //     pull below, and the full repopulate, simply never happened on open.
        //     Every write to HostConnectionDataSO that does not raise a SOAP
        //     event (LocalDisplayName / LocalAvatarId are plain field
        //     assignments in SyncLocalIdentity) stayed invisible forever.
        //
        //   • IModalPanel alone would miss any scene or prefab that DOES toggle
        //     this GameObject.
        //
        // Bind/Unbind are idempotent, so a double open (OnEnable + OnModalOpened
        // in the same frame on first show) is harmless.
        // ─────────────────────────────────────────────────────────────────────

        bool _subscribed;

        void OnEnable()  => Bind();
        void OnDisable() => Unbind();

        /// <inheritdoc/>
        /// <remarks>
        /// Subscription is idempotent, but the RE-READ is not conditional: the
        /// whole point of this hook is that the panel must never render a
        /// snapshot taken when it was last closed. Guarding the refresh behind
        /// the subscribe flag would reintroduce exactly the bug this fixes, since
        /// OnEnable has already set that flag at scene load.
        /// </remarks>
        public void OnModalOpened()
        {
            Bind();
            RefreshFromData();
        }

        /// <inheritdoc/>
        public void OnModalClosed() => Unbind();

        void Bind()
        {
            if (_subscribed) return;
            _subscribed = true;

            SubscribeSoap();
            RefreshFromData();
        }

        void Unbind()
        {
            if (!_subscribed) return;
            _subscribed = false;

            UnsubscribeSoap();
        }

        void RefreshFromData()
        {
            PopulateAll();

            // Pull fresh lobby data the moment the arcade panel opens so the
            // "N Players Online" counter and party slots reflect server state
            // instead of whatever snapshot happened to be cached when the user
            // last navigated away. Debounced inside HostConnectionService.
            HostConnectionService.Instance?.ForceRefreshNow();
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

            // One coalesced repaint instead of three per-member subscriptions
            // that all discarded their payload and ran the same full repopulate.
            // Fires after the roster settles, so the slots are built from a
            // consistent list rather than a half-applied one.
            if (connectionData.OnPartyRosterChanged != null)
                connectionData.OnPartyRosterChanged.OnRaised += HandlePartyRosterChanged;

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

            if (connectionData.OnPartyRosterChanged != null)
                connectionData.OnPartyRosterChanged.OnRaised -= HandlePartyRosterChanged;

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

        void PopulateSlots()
        {
            if (slots == null || slots.Length == 0 || !connectionData) return;

            // Collect remote party members (excluding the local player) in
            // insertion order so the layout stays stable across refreshes.
            var remoteMembers = new List<PartyPlayerData>();
            if (connectionData.PartyMembers != null)
            {
                string localId = connectionData.LocalPlayerId;
                foreach (var m in connectionData.PartyMembers)
                {
                    if (m.PlayerId == localId) continue;
                    remoteMembers.Add(m);
                }
            }

            // Two-pass population: clear empty slots FIRST, then populate
            // occupied slots. If the scene wiring accidentally shares a
            // TMP_Text or Image GameObject between two slots, the occupied
            // slot's SetPlayer / SetAsLocalPlayer activation runs last and
            // wins over the empty slot's ClearSlot deactivation - so the
            // visible name/avatar survive the shared-reference case.
            int slotCount = Mathf.Min(slots.Length, MAX_SLOTS);
            for (int i = 0; i < slotCount; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                if (i == 0) continue; // local slot is always occupied

                int remoteIdx = i - 1;
                if (remoteIdx >= remoteMembers.Count)
                    slot.ClearSlot();
            }

            for (int i = 0; i < slotCount; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                if (i == 0)
                {
                    PopulateLocalSlot(slot);
                    continue;
                }

                int remoteIdx = i - 1;
                if (remoteIdx < remoteMembers.Count)
                {
                    var member = remoteMembers[remoteIdx];
                    // Only the party host can kick, and never itself - these are remote
                    // member slots (slot 0 is the local player), so host ⇒ kickable.
                    slot.SetPlayer(member.PlayerId, member.DisplayName, ResolveAvatar(member.AvatarId),
                        canKick: connectionData.IsPartyHost);
                }
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
            // "IN PARTY X/N" / "IN A MATCH" badges belong on the
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

        // UpdateOnlineStatus() is included here because the "N Players Online"
        // counter adds 1 for the local player when connectionData.IsConnected -
        // so a party change that flips connection state moved the count without
        // either of these repainting it. Cheap (one string assignment) and it
        // removes a whole class of "the counter says 2 but I can see 3" drift.
        void HandlePartyChanged(PartyPlayerData _)
        {
            PopulateSlots();
            UpdateLeaveButtonState();
            UpdateOnlineStatus();
        }

        void HandlePartyCleared()
        {
            PopulateSlots();
            UpdateLeaveButtonState();
            UpdateOnlineStatus();
        }

        void HandleOnlineChanged(PartyPlayerData _) => UpdateOnlineStatus();
        void HandleOnlineCleared() => UpdateOnlineStatus();

        /// <summary>
        /// The party roster settled - rebuild the slots, the leave button and the
        /// online counter together.
        ///
        /// <para>
        /// Replaces three identical per-member subscriptions that each discarded
        /// their <c>PartyPlayerData</c> payload. The <c>PartyMembers</c> list
        /// events remain subscribed as the pre-existing backstop; they fire
        /// mid-mutation, this fires once the roster is whole.
        /// </para>
        /// </summary>
        void HandlePartyRosterChanged()
        {
            PopulateSlots();
            UpdateLeaveButtonState();
            UpdateOnlineStatus();
        }

        void HandleProfileChanged(PlayerProfileData _)
        {
            // Only slot 0 depends on local profile; other slots read from
            // connectionData.PartyMembers which is owned by HostConnectionService.
            if (slots == null || slots.Length == 0 || slots[0] == null) return;
            PopulateLocalSlot(slots[0]);
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
        // OnPartyRosterChanged → PopulateSlots, so the slot re-renders either way.
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
        /// Delegates to the one project-wide resolver. The local scan this
        /// replaced fell back to profileIcons[0] - i.e. rendered every
        /// unresolved avatar as authored icon #1.
        /// </summary>
        Sprite ResolveAvatar(int avatarId) =>
            profileIcons ? profileIcons.Resolve(avatarId) : null;
    }
}
