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
        // Capacity, NOT the UI's party size. The lobby panel still shows four slots; this is
        // the ceiling that HasOpenSlots and the Relay allocation are sized from. At exactly 4 a
        // four-player party has ZERO headroom, so one transient double-count in the polled
        // member list - which is reconciled from UGS and is known to flicker on join/leave - is
        // enough to throw the fourth invite out with "Party is full" before it reaches the wire,
        // or to make the join return session-full (not a transient exception, so it propagates
        // straight to a bounce). One spare Relay slot removes that whole class.
        [SerializeField] private int maxPartySlots = 6;

        public int MaxPartySlots => maxPartySlots;

        [Tooltip("The party size PLAYERS SEE - the slot count the lobby draws and the number " +
                 "published to other peers as 'N/M'. Deliberately SEPARATE from maxPartySlots, " +
                 "which is transport capacity carrying one spare slot of anti-flicker headroom.")]
        // Capacity leaked into the UI once already: MaxPartySlots (6, a Relay/transport number
        // with deliberate headroom) was what the lobby rendered and what PARTY_MAX_KEY published,
        // so every peer read "1/6" for a four-player game and the LOBBY FULL badge waited for a
        // fifth and sixth member that the design never seats. The two numbers answer different
        // questions - "how many can the session physically hold" vs "how big is a party" - and a
        // property named for the transport will keep being read as the game rule until they are
        // separate fields. Clamped to the capacity so display can never promise a seat the
        // session cannot hold.
        [SerializeField] private int partyDisplaySlots = 4;

        /// <summary>Party size as PLAYERS see it (4). Never the transport capacity.</summary>
        public int PartyDisplaySlots => Mathf.Clamp(partyDisplaySlots, 1, maxPartySlots);

        /// <summary>
        /// Whether the party has room for another member BY THE GAME'S RULE (the displayed size),
        /// which is what an invite affordance must gate on. <see cref="HasOpenSlots"/> is the
        /// transport question and stays deliberately looser by one seat of headroom.
        /// </summary>
        public bool HasOpenDisplaySlots =>
            PartyMembers == null || PartyMembers.Count < PartyDisplaySlots;

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

        [Tooltip("Raised the moment the local player resolves an incoming invite " +
                 "(accept or decline) from any source. UI panels listen to this " +
                 "to clear stale pending-invite rows so accepting from one panel " +
                 "also dismisses the same invite shown elsewhere.")]
        public ScriptableEventNoParam OnInviteResolved;

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

        /// <summary>
        /// True when the local player owns the shared UGS presence/discovery lobby
        /// (i.e. they were the first user to sign in and create it). This has no
        /// bearing on game-launch authority or party ownership - it only reflects
        /// who happens to own the global discovery session.
        /// </summary>
        [HideInInspector] public bool IsPresenceLobbyHost;

        /// <summary>
        /// True when the local player is the host of the active Relay-backed
        /// party session. This is the flag that gates game-launch authority,
        /// kick permissions, and party-host UI affordances. False for solo
        /// players with no party session, and false for clients who joined
        /// someone else's party via an accepted invite.
        /// </summary>
        [HideInInspector] public bool IsPartyHost;

        /// <summary>
        /// True when the CURRENT party was formed through a formal invite (someone sent one and
        /// it was accepted), false when players found each other through the presence lobby with
        /// no prompted invite.
        ///
        /// Neither peer can answer this alone - the joiner knows they accepted, the host knows
        /// they sent, and a third player who arrived via presence knows neither - so it is
        /// party-level state, set on both sides of the invite handshake and broadcast by the
        /// host at game launch (GameDataSO.InviteTriggered).
        ///
        /// Resetting it when the party empties is load-bearing: without that, a party that
        /// formed by invite, dissolved, and re-formed organically the next day would still
        /// report true and be excluded from exactly the organic-rematch cohort we measure.
        /// See Docs/Analytics/DATA_ARCHITECTURE.md §6.4.
        /// </summary>
        [HideInInspector] public bool PartyFormedByInvite;

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
            IsPresenceLobbyHost = false;
            IsPartyHost = false;
            PartyFormedByInvite = false;

            OnlinePlayers?.Clear();
            PartyMembers?.Clear();
        }
    }
}
