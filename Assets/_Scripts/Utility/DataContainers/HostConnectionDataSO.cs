using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Central SOAP data container for the host connection and party system.
    /// Holds runtime state + SOAP events that decouple PartyManager from all UI consumers.
    /// Create one asset and wire it into PartyManager, PartyArcadeView, OnlinePlayersPanel, etc.
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
