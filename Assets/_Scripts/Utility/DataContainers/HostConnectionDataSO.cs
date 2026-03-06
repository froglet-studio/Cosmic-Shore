using Obvious.Soap;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using System.Linq;
namespace CosmicShore.Utility
{
    /// <summary>
    /// Central SOAP data container for the host connection and party system.
    /// Holds runtime state + SOAP events that decouple HostConnectionService from all UI consumers.
    /// Create one asset and wire it into HostConnectionService, PartyArcadeView, OnlinePlayersPanel, etc.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HostConnectionData",
        menuName = "ScriptableObjects/DataContainers/Host Connection Data")]
    public class HostConnectionDataSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        // Connection State
        // ─────────────────────────────────────────────────────────────────────

        [Header("Connection Events")]
        [Tooltip("Raised when the local player successfully joins or creates the presence lobby.")]
        public ScriptableEventNoParam OnHostConnectionEstablished;

        [Tooltip("Raised when the local player leaves or is disconnected from the presence lobby.")]
        public ScriptableEventNoParam OnHostConnectionLost;

        // ─────────────────────────────────────────────────────────────────────
        // Online Players (Presence Lobby)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Online Players")]
        [Tooltip("Reactive list of all online players currently in the presence lobby (excluding local player).")]
        public ScriptableListPartyPlayerData OnlinePlayers;

        // ─────────────────────────────────────────────────────────────────────
        // Party Members
        // ─────────────────────────────────────────────────────────────────────

        [Header("Party")]
        [Tooltip("Reactive list of players currently in the local player's party (includes self at index 0).")]
        public ScriptableListPartyPlayerData PartyMembers;

        [Tooltip("Raised when a remote player joins the local player's party.")]
        public ScriptableEventPartyPlayerData OnPartyMemberJoined;

        [Tooltip("Raised when a remote player leaves the local player's party.")]
        public ScriptableEventPartyPlayerData OnPartyMemberLeft;

        [Tooltip("Raised when the host kicks a remote player from the party.")]
        public ScriptableEventPartyPlayerData OnPartyMemberKicked;

        [Header("Max Slots")]
        [Tooltip("Maximum number of party slots (including the local player).")]
        [SerializeField] private int maxPartySlots = 4;

        public int MaxPartySlots => maxPartySlots;

        // ─────────────────────────────────────────────────────────────────────
        // Invites
        // ─────────────────────────────────────────────────────────────────────

        [Header("Invites")]
        [Tooltip("Raised when the local player receives a party invite from another player.")]
        public ScriptableEventPartyInviteData OnInviteReceived;

        [Tooltip("Raised when an invite has been sent to a target player (carries the target's data).")]
        public ScriptableEventPartyPlayerData OnInviteSent;

        [Tooltip("Raised when the local player has fully completed joining a party (Netcode connected, scene loaded).")]
        public ScriptableEventNoParam OnPartyJoinCompleted;

        // ─────────────────────────────────────────────────────────────────────
        // Local Player Identity
        // ─────────────────────────────────────────────────────────────────────

        [Header("Local Player (runtime)")]
        [HideInInspector] public string LocalPlayerId;
        [HideInInspector] public string LocalDisplayName;
        [HideInInspector] public int LocalAvatarId;

        public PartyPlayerData LocalPlayerData =>
            new(LocalPlayerId, LocalDisplayName, LocalAvatarId);

        [HideInInspector] public bool IsConnected;
        [HideInInspector] public bool IsHost;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        public bool HasOpenSlots => PartyMembers == null || PartyMembers.Count < maxPartySlots;

        /// <summary>
        /// Number of remote (non-local) human players in the party.
        /// </summary>
        public int RemotePartyMemberCount
        {
            get
            {
                if (PartyMembers == null) return 0;
                int count = 0;
                foreach (var m in PartyMembers)
                    if (m.PlayerId != LocalPlayerId) count++;
                return count;
            }
        }

        /// <summary>
        /// Removes a party member by player ID and fires OnPartyMemberKicked.
        /// </summary>
        public bool RemovePartyMember(string playerId)
        {
            if (PartyMembers == null) return false;

            for (int i = PartyMembers.Count - 1; i >= 0; i--)
            {
                if (PartyMembers[i].PlayerId == playerId)
                {
                    var removed = PartyMembers[i];
                    PartyMembers.RemoveAt(i);
                    OnPartyMemberKicked?.Raise(removed);
                    OnPartyMemberLeft?.Raise(removed);
                    return true;
                }
            }
            return false;
        }

        public void ResetRuntimeData()
        {
            LocalPlayerId = string.Empty;
            LocalDisplayName = string.Empty;
            LocalAvatarId = 0;
            IsConnected = false;
            IsHost = false;

            OnlinePlayers?.Clear();
            PartyMembers?.Clear();
        }
    }
}
