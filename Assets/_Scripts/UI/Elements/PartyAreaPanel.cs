using System.Collections.Generic;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// Reusable Party Area panel with 3 player slots.
    ///
    /// Slot 0 shows the local player. Slots 1-2 show remote party members or
    /// a "+" button to open the <see cref="OnlinePlayersPanel"/> for inviting.
    ///
    /// Reads all state from <see cref="HostConnectionDataSO"/> via SOAP events.
    /// Can be placed on any menu screen (Home, Arcade, etc.).
    ///
    /// When the "+" button is pressed on an empty slot and a
    /// <see cref="PartyInviteController"/> exists, the host-side Relay transition
    /// is triggered before opening the invite panel (so clients can actually connect).
    /// </summary>
    public class PartyAreaPanel : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Slots")]
        [Tooltip("Pre-placed slot views in the hierarchy (index 0 = local player, 1-2 = remote).")]
        [SerializeField] private List<PartySlotView> partySlots;

        [Header("Online Players Panel")]
        [SerializeField] private OnlinePlayersPanel onlinePlayersPanel;

        [Inject] private PlayerDataService playerDataService;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            foreach (var slot in partySlots)
                slot.Initialize(OnAddSlotPressed);
        }

        void Start()
        {
            if (connectionData != null)
            {
                if (connectionData.PartyMembers != null)
                {
                    connectionData.PartyMembers.OnItemAdded += OnPartyMemberAdded;
                    connectionData.PartyMembers.OnItemRemoved += OnPartyMemberRemoved;
                    connectionData.PartyMembers.OnCleared += OnPartyCleared;
                }

                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised += RefreshAllSlots;

                if (connectionData.OnPartyJoinCompleted != null)
                    connectionData.OnPartyJoinCompleted.OnRaised += RefreshAllSlots;
            }

            if (playerDataService != null)
                playerDataService.OnProfileChanged += OnLocalProfileChanged;

            RefreshAllSlots();
        }

        void OnDisable()
        {
            if (connectionData != null)
            {
                if (connectionData.PartyMembers != null)
                {
                    connectionData.PartyMembers.OnItemAdded -= OnPartyMemberAdded;
                    connectionData.PartyMembers.OnItemRemoved -= OnPartyMemberRemoved;
                    connectionData.PartyMembers.OnCleared -= OnPartyCleared;
                }

                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised -= RefreshAllSlots;

                if (connectionData.OnPartyJoinCompleted != null)
                    connectionData.OnPartyJoinCompleted.OnRaised -= RefreshAllSlots;
            }

            if (playerDataService != null)
                playerDataService.OnProfileChanged -= OnLocalProfileChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public
        // ─────────────────────────────────────────────────────────────────────

        public void RefreshAllSlots()
        {
            RefreshLocalPlayerSlot();

            int memberIndex = 0;
            for (int slotIndex = 1; slotIndex < partySlots.Count; slotIndex++)
            {
                var slot = partySlots[slotIndex];

                PartyPlayerData? remoteMember = null;
                while (memberIndex < (connectionData?.PartyMembers?.Count ?? 0))
                {
                    var candidate = connectionData.PartyMembers[memberIndex];
                    memberIndex++;

                    if (candidate.PlayerId == connectionData?.LocalPlayerId)
                        continue;

                    remoteMember = candidate;
                    break;
                }

                if (remoteMember.HasValue)
                    slot.SetPlayer(remoteMember.Value);
                else
                    slot.ClearSlot();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP Callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnPartyMemberAdded(PartyPlayerData member)
        {
            RefreshAllSlots();
        }

        private void OnPartyMemberRemoved(PartyPlayerData member)
        {
            RefreshAllSlots();
        }

        private void OnPartyCleared()
        {
            foreach (var slot in partySlots)
                slot.ClearSlot();
        }

        private void OnLocalProfileChanged(PlayerProfileData profileData)
        {
            RefreshLocalPlayerSlot();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Slot Actions
        // ─────────────────────────────────────────────────────────────────────

        private void OnAddSlotPressed()
        {
            if (!connectionData.HasOpenSlots)
            {
                Debug.Log("[PartyAreaPanel] No open party slots.");
                return;
            }

            onlinePlayersPanel?.Show();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshLocalPlayerSlot()
        {
            if (partySlots.Count == 0) return;

            var slot0 = partySlots[0];

            if (connectionData != null && !string.IsNullOrEmpty(connectionData.LocalPlayerId))
            {
                slot0.SetPlayer(connectionData.LocalPlayerData);
            }
            else if (playerDataService?.CurrentProfile != null)
            {
                var profile = playerDataService.CurrentProfile;
                slot0.SetPlayer(new PartyPlayerData(
                    profile.userId, profile.displayName, profile.avatarId));
            }
        }
    }
}
