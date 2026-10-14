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

        [Header("Party Size")]
        [Tooltip("How many people can be in one party, the local player included. Also the seat count of the " +
                 "party's UGS session, so the service itself refuses anyone past it. Always 4.")]
        // ONE number. It used to be two - a 6-seat transport capacity under a 4-seat displayed size -
        // and that left nothing enforcing four: every "party is full" check ran on the joining
        // client against polled data, so two Joins on a 3/4 party inside one refresh seated a fifth
        // (Docs/PartySystem/BUGS.md B25). The party session is now created with exactly this many
        // seats, so UGS rejects a fifth join itself. The roster flicker the spare seats used to
        // absorb is handled where it happens: HasOpenSlots counts DISTINCT player ids.
        [SerializeField] private int maxPartySlots = 4;

        /// <summary>The party size (4): players who can be in one party, and the party session's seats.</summary>
        public int MaxPartySlots => maxPartySlots;

        // ─────────────────────────────────────────────────────
        // UGS request policy
        // ─────────────────────────────────────────────────────
        [Header("UGS Request Policy")]
        [Tooltip("Back-off, jitter and retry-budget tunables for every UGS (Lobby / Sessions / Relay) call the " +
                 "party, presence and match layers make. Read once at bootstrap into the shared UgsRequestPolicy " +
                 "(AppManager DI) - retune here, never in code. See " +
                 "Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md §5.4.")]
        [SerializeField] private UgsRequestPolicySettings ugsRequestPolicy = new UgsRequestPolicySettings();
        /// <summary>Tunables for <see cref="Utility.UgsRequestPolicy"/>. Never null after deserialization.</summary>
        public UgsRequestPolicySettings UgsRequestPolicySettings => ugsRequestPolicy;

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

        /// <summary>
        /// True while the local player is connected to another player's session as a
        /// SPECTATOR: a Netcode client with no Player object and no vessel, watching only.
        /// Set by <c>HostConnectionService.JoinAsSpectatorAsync</c>, cleared by every path that
        /// leaves that session or creates the player's own (<c>LeavePartySessionAsync</c>,
        /// <c>EnsurePartySessionAsync</c>, <see cref="ResetRuntimeData"/>). While it is set the
        /// party-member sync is suspended (the match's pilots are not this player's party), no
        /// <c>joined_party</c> claim is published, and the presence row advertises NO session so
        /// nobody can chain-spectate through a spectator. See Docs/PartySystem/SPECTATOR.md.
        /// </summary>
        [HideInInspector] public bool IsSpectating;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Whether the party can take another member: fewer DISTINCT players than
        /// <see cref="MaxPartySlots"/>. Distinct, because the polled roster is reconciled from UGS and
        /// can briefly carry one player twice on a join or leave - that must not read as a full party.
        /// </summary>
        public bool HasOpenSlots => DistinctPartyMemberCount < maxPartySlots;

        /// <summary>Members in <see cref="PartyMembers"/> counted once per player id (no allocation).</summary>
        public int DistinctPartyMemberCount
        {
            get
            {
                if (PartyMembers == null) return 0;
                int distinct = 0;
                for (int i = 0; i < PartyMembers.Count; i++)
                {
                    string id = PartyMembers[i].PlayerId;
                    bool seen = false;
                    for (int j = 0; j < i && !seen; j++) seen = PartyMembers[j].PlayerId == id;
                    if (!seen) distinct++;
                }
                return distinct;
            }
        }

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
            IsSpectating = false;

            OnlinePlayers?.Clear();
            PartyMembers?.Clear();
        }
    }
}
