using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        [Inject] private GameDataSO gameData;

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

            // Local "in a match" status flips on these — refresh the counter
            // so the badge updates without waiting for a party-list change.
            if (gameData != null)
            {
                if (gameData.OnLaunchGame != null)
                    gameData.OnLaunchGame.OnRaised += HandleMatchStateChanged;
                if (gameData.OnSessionEnded != null)
                    gameData.OnSessionEnded.OnRaised += HandleMatchStateChanged;
            }
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

            if (gameData != null)
            {
                if (gameData.OnLaunchGame != null)
                    gameData.OnLaunchGame.OnRaised -= HandleMatchStateChanged;
                if (gameData.OnSessionEnded != null)
                    gameData.OnSessionEnded.OnRaised -= HandleMatchStateChanged;
            }
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

            int slotCount = Mathf.Min(slots.Length, MAX_SLOTS);
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
                else
                {
                    slot.ClearSlot();
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
            int remoteCount = connectionData.OnlinePlayers != null
                ? connectionData.OnlinePlayers.Count
                : 0;
            int total = remoteCount + (connectionData.IsConnected ? 1 : 0);

            string countText = total == 1
                ? "1 Player Online"
                : $"{total} Players Online";

            string badge = ResolveLocalStatusBadge();
            onlineStatusText.text = string.IsNullOrEmpty(badge)
                ? countText
                : $"{countText}  ·  {badge}";
        }

        /// <summary>
        /// Mirrors the badge strings used by <see cref="OnlineInfoEntry"/> for remote
        /// players, so the Arcade lobby header reads with the same vocabulary the
        /// Friends list uses for everyone else: "IN A MATCH — {gameMode}", "LOBBY FULL",
        /// or "IN LOBBY X/N". Returns an empty string when the local player is alone
        /// in the party and not in a match — in that case only the player-count is shown.
        /// </summary>
        string ResolveLocalStatusBadge()
        {
            // In-match takes priority. Defensive guard: when the modal is open
            // we are normally on Menu_Main, but the scene-transition window can
            // briefly land here with gameData already pointing at the next match.
            if (gameData != null && gameData.IsMultiplayerMode &&
                !IsOnMenuScene() && gameData.GameMode != GameModes.Random)
            {
                return $"IN A MATCH — {gameData.GameMode.ToString().ToUpperInvariant()}";
            }

            int memberCount = connectionData.PartyMembers != null
                ? connectionData.PartyMembers.Count
                : 0;
            int maxSlots = connectionData.MaxPartySlots;

            if (maxSlots > 0 && memberCount >= maxSlots)
                return "LOBBY FULL";

            if (memberCount > 1 && maxSlots > 0)
                return $"IN LOBBY {memberCount}/{maxSlots}";

            return string.Empty;
        }

        static bool IsOnMenuScene()
        {
            var name = SceneManager.GetActiveScene().name;
            return name == "Menu_Main" || name == "Authentication";
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

        void HandleMatchStateChanged() => UpdateOnlineStatus();

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
