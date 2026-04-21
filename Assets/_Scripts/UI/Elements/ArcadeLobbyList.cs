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
    /// header slots — but as its own panel with a leave-party button and a
    /// live "X Players Online" counter.
    ///
    /// Slot 0 is always the local player (avatar + display name).
    /// Remaining slots render the other <see cref="HostConnectionDataSO.PartyMembers"/>
    /// in order. Empty slots expose the "+" add button, which opens the
    /// <see cref="FriendsListPanel"/> (pre-wired in the scene).
    ///
    /// All data flows through SOAP events — no direct <see cref="HostConnectionService"/>
    /// references are needed beyond the Leave button callback.
    /// </summary>
    public class ArcadeLobbyList : MonoBehaviour
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

        [Tooltip("Leave Party button — disconnects from the current party and returns to Menu_Main.")]
        [SerializeField] private Button leaveButton;

        [Tooltip("Panel opened when an empty slot's '+' button is pressed. " +
                 "Should be the scene-wired FriendsListPanel.")]
        [SerializeField] private FriendsListPanel friendsListPanel;

        /// <summary>Max slots rendered — matches <c>HostConnectionDataSO.MaxPartySlots</c> (4 by design).</summary>
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
                }
            }

            WarnOnSharedSlotReferences();
        }

        // Detect scene-wiring bugs where two FriendInfoSlot instances share the
        // same internal UI child references. When that happens, the last-iterated
        // slot wins — so an empty slot's ClearSlot can overwrite an occupied
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
                        Debug.LogWarning($"[ArcadeLobbyList] slots[{i}] and slots[{j}] share the same displayNameText GameObject. Rewire in the scene — names will not render correctly for both slots.", this);
                    if (ReferenceEquals(a.AvatarIconGO, b.AvatarIconGO) && a.AvatarIconGO != null)
                        Debug.LogWarning($"[ArcadeLobbyList] slots[{i}] and slots[{j}] share the same avatarIcon GameObject. Rewire in the scene — avatars will not render correctly for both slots.", this);
                }
            }
        }

        void OnEnable()
        {
            SubscribeSoap();
            PopulateAll();
        }

        void OnDisable()
        {
            UnsubscribeSoap();
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

            // Refresh slot 0 when the cloud profile resolves — HostConnectionDataSO
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

            if (PlayerDataService.Instance != null)
                PlayerDataService.Instance.OnProfileChanged -= HandleProfileChanged;
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
            // wins over the empty slot's ClearSlot deactivation — so the
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
                    slot.SetPlayer(member.PlayerId, member.DisplayName, ResolveAvatar(member.AvatarId));
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

            // OnlinePlayers excludes the local player by design — add 1 so
            // the counter reflects the total player population, which is
            // what players intuitively expect when they read "N Players Online".
            // The Arcade header intentionally shows only the raw count here —
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
                Debug.LogWarning("[ArcadeLobbyList] HostConnectionService not available — cannot leave party.");
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

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

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
