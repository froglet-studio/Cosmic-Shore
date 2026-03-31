using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.ScriptableObjects.SOAP;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Manages the FriendsInfo slots in the Arcade FriendsList panel.
    /// Slot 0 is always the local player. Remaining slots show party members
    /// or an "+" add button that opens the PartyArea panel.
    ///
    /// Subscribes to PartyMembers SOAP list events for reactive updates.
    /// </summary>
    public class FriendsListPanel : MonoBehaviour
    {
        [Header("Slots")]
        [Tooltip("All FriendInfoSlot components in order. Slot 0 = local player.")]
        [SerializeField] private List<FriendInfoSlot> slots = new();

        [Header("Party Area")]
        [Tooltip("The PartyArea GameObject to show when the '+' add button is pressed.")]
        [SerializeField] private GameObject partyAreaPanel;

        [Header("Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Display")]
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text onlineStatusText;

        [Inject] private PlayerDataService playerDataService;

        void Start()
        {
            // Wire add button on each non-local slot
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i]) continue;
                slots[i].BindAddButton(OnAddButtonPressed);
            }

            // Subscribe to party member changes
            if (connectionData)
            {
                connectionData.PartyMembers.OnItemAdded += HandlePartyMemberAdded;
                connectionData.PartyMembers.OnItemRemoved += HandlePartyMemberRemoved;
                connectionData.PartyMembers.OnCleared += HandlePartyCleared;
                connectionData.OnHostConnectionEstablished.OnRaised += RefreshAllSlots;
                connectionData.OnPartyJoinCompleted.OnRaised += RefreshAllSlots;
            }

            if (playerDataService != null)
                playerDataService.OnProfileChanged += HandleProfileChanged;

            RefreshAllSlots();
        }

        void OnDisable()
        {
            if (connectionData)
            {
                connectionData.PartyMembers.OnItemAdded -= HandlePartyMemberAdded;
                connectionData.PartyMembers.OnItemRemoved -= HandlePartyMemberRemoved;
                connectionData.PartyMembers.OnCleared -= HandlePartyCleared;
                connectionData.OnHostConnectionEstablished.OnRaised -= RefreshAllSlots;
                connectionData.OnPartyJoinCompleted.OnRaised -= RefreshAllSlots;
            }

            if (playerDataService != null)
                playerDataService.OnProfileChanged -= HandleProfileChanged;
        }

        #region Public API

        /// <summary>Rebuilds all slots from current state.</summary>
        public void RefreshAllSlots()
        {
            RefreshLocalPlayerSlot();
            RefreshRemoteSlots();
            UpdateStatusText();
        }

        #endregion

        #region Slot Population

        void RefreshLocalPlayerSlot()
        {
            if (slots.Count == 0) return;
            var slot = slots[0];
            if (!slot) return;

            string playerId = null;
            string displayName = null;
            int avatarId = 0;

            // Try HostConnectionData first (authoritative when connected)
            if (connectionData && !string.IsNullOrEmpty(connectionData.LocalPlayerId))
            {
                playerId = connectionData.LocalPlayerId;
                displayName = connectionData.LocalDisplayName;
                avatarId = connectionData.LocalAvatarId;
            }

            // Fall back to PlayerDataService profile
            if (string.IsNullOrEmpty(playerId) && playerDataService != null)
            {
                var profile = playerDataService.CurrentProfile;
                playerId = profile.userId;
                displayName = profile.displayName;
                avatarId = profile.avatarId;
            }

            slot.SetAsLocalPlayer(playerId, displayName, ResolveAvatar(avatarId));
        }

        void RefreshRemoteSlots()
        {
            // Gather remote party members (skip local player)
            var remoteMembers = new List<PartyPlayerData>();
            if (connectionData && connectionData.PartyMembers != null)
            {
                foreach (var member in connectionData.PartyMembers)
                {
                    if (member.PlayerId == connectionData.LocalPlayerId) continue;
                    remoteMembers.Add(member);
                }
            }

            // Fill slots 1..N with remote members, clear the rest
            for (int i = 1; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot) continue;

                int remoteIndex = i - 1;
                if (remoteIndex < remoteMembers.Count)
                {
                    var member = remoteMembers[remoteIndex];
                    slot.SetPlayer(member.PlayerId, member.DisplayName, ResolveAvatar(member.AvatarId));
                }
                else
                {
                    slot.ClearSlot();
                }
            }
        }

        #endregion

        #region Event Handlers

        void HandlePartyMemberAdded(PartyPlayerData member)
        {
            // Skip local player — they're always in slot 0
            if (connectionData && member.PlayerId == connectionData.LocalPlayerId) return;

            // Find first empty non-local slot
            for (int i = 1; i < slots.Count; i++)
            {
                if (!slots[i] || slots[i].IsOccupied) continue;
                slots[i].SetPlayer(member.PlayerId, member.DisplayName, ResolveAvatar(member.AvatarId));
                break;
            }

            UpdateStatusText();
        }

        void HandlePartyMemberRemoved(PartyPlayerData member)
        {
            // Find and clear the slot with this player
            for (int i = 1; i < slots.Count; i++)
            {
                if (!slots[i]) continue;
                if (slots[i].PlayerId == member.PlayerId)
                {
                    slots[i].ClearSlot();
                    break;
                }
            }

            // Compact: shift remaining members so there are no gaps
            RefreshRemoteSlots();
            UpdateStatusText();
        }

        void HandlePartyCleared()
        {
            for (int i = 1; i < slots.Count; i++)
            {
                if (slots[i])
                    slots[i].ClearSlot();
            }

            UpdateStatusText();
        }

        void HandleProfileChanged(PlayerProfileData profile)
        {
            RefreshLocalPlayerSlot();
        }

        void OnAddButtonPressed()
        {
            if (partyAreaPanel)
                partyAreaPanel.SetActive(true);
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

            // Fallback to first icon
            return profileIcons.profileIcons.Count > 0
                ? profileIcons.profileIcons[0].IconSprite
                : null;
        }

        void UpdateStatusText()
        {
            int onlineCount = connectionData && connectionData.OnlinePlayers != null
                ? connectionData.OnlinePlayers.Count
                : 0;

            if (onlineStatusText)
                onlineStatusText.text = $"{onlineCount} friends online";

            int partyCount = connectionData && connectionData.PartyMembers != null
                ? connectionData.PartyMembers.Count
                : 1;

            if (descriptionText)
                descriptionText.text = partyCount > 1
                    ? $"Party: {partyCount}/{slots.Count}"
                    : "Invite players to your party";
        }

        #endregion
    }
}
