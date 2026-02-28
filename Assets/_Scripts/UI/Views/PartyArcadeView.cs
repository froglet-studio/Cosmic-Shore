using System.Collections.Generic;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    /// <summary>
    /// Drives the Players Info area on the left side of the Arcade Panel.
    /// Renders N <see cref="PartySlotView"/> slots sourced from
    /// <see cref="HostConnectionDataSO"/>.  Slot 0 is always the local player.
    /// Empty slots show a "+" button that opens the <see cref="OnlinePlayersPanel"/>.
    /// </summary>
    public class PartyArcadeView : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Slots")]
        [Tooltip("Pre-placed slot views in the hierarchy (index 0 = local player).")]
        [SerializeField] private List<PartySlotView> partySlots;

        [Header("Online Players Panel")]
        [SerializeField] private OnlinePlayersPanel onlinePlayersPanel;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Inject] private PlayerDataService playerDataService;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            foreach (var slot in partySlots)
                slot.Initialize(OpenOnlinePlayers);
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
            }

            if (playerDataService != null)
                playerDataService.OnProfileChanged -= OnLocalProfileChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Full rebuild of all slot visuals from <see cref="HostConnectionDataSO.PartyMembers"/>.
        /// </summary>
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
        // Panel
        // ─────────────────────────────────────────────────────────────────────

        private void OpenOnlinePlayers()
        {
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
