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
    public class HostConnectionDataSO : ScriptableObject, IPartyRoster
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

        /// <summary>
        /// Raised ONCE after any mutation of <see cref="PartyMembers"/> settles.
        ///
        /// <para>
        /// <b>Coalesced by design.</b> A sync that adds three members raises this
        /// once, not three times - unlike the per-member
        /// <see cref="OnPartyMemberJoined"/>/<see cref="OnPartyMemberLeft"/>
        /// pair, which every consumer was subscribing to three times over and
        /// then answering with the same full repopulate. This is the channel to
        /// listen on for "repaint anything that renders party size or
        /// membership"; the per-member events remain for consumers that
        /// genuinely need to know WHO moved (invite-row clearing, presence
        /// activity strings).
        /// </para>
        ///
        /// <para>
        /// <b>Requested</b> via <c>SoapPartyEventBus.RequestPartyRosterChanged</c>
        /// (safe from any thread) and actually raised by
        /// <c>SoapPartyEventBus.FlushPartyRosterChanged</c>, drained once per frame
        /// from <c>HostConnectionService.Update</c>. The deferral is required, not
        /// stylistic: this event's listeners touch <c>UnityEngine.Object</c>, and
        /// its request sites are not all main-thread - raising inline threw
        /// <c>EnsureRunningOnMainThread</c>. Do not add a direct <c>.Raise()</c>
        /// for this event without proving the call site's thread context; see the
        /// block comment in <c>SoapPartyEventBus</c>.
        /// </para>
        /// </summary>
        [Tooltip("Raised ONCE per party-roster mutation, after the roster has settled. " +
                 "Coalesced - a sync adding three members raises this once, not three times. " +
                 "Listen to this (not OnPartyMemberJoined/Left) to repaint anything that " +
                 "renders party size or membership.")]
        public ScriptableEventNoParam OnPartyRosterChanged;

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

        // ─────────────────────────────────────────────────────────────────────
        // IPartyRoster - the authoritative LOCAL answer to "how big is my party"
        //
        // These are thin aliases over state this SO already held. They exist as
        // an interface so a consumer can depend on the QUESTION rather than on
        // this concrete container, and so the two-tier rule (local roster for
        // your own party, advertised presence properties only for other
        // people's) has a name a call site can be checked against. See
        // IPartyRoster.cs for why that mattered enough to formalise.
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public int MemberCount => PartyMembers?.Count ?? 0;

        /// <inheritdoc/>
        public int MaxSlots => maxPartySlots;

        /// <inheritdoc/>
        public bool IsHost => IsPartyHost;

        /// <inheritdoc/>
        public bool Contains(string playerId)
        {
            if (PartyMembers == null || string.IsNullOrEmpty(playerId)) return false;
            foreach (var m in PartyMembers)
                if (m.PlayerId == playerId) return true;
            return false;
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
        ///
        /// <para>
        /// One of only two roster mutations that do not go through
        /// <c>PartyMemberService</c> (the other is the host-only presence-join
        /// scan in <c>HostConnectionService</c>), which is why it raises
        /// <see cref="OnPartyRosterChanged"/> itself. Leaving it out would make
        /// the kick path the one roster change that never repainted a count.
        /// </para>
        ///
        /// <para>
        /// <b>MAIN THREAD ONLY.</b> This is the one direct raise of
        /// <see cref="OnPartyRosterChanged"/> left in the codebase - every other
        /// path defers through <c>SoapPartyEventBus.RequestPartyRosterChanged</c>
        /// because its listeners touch <c>UnityEngine.Object</c>. It is safe here
        /// only because the sole caller, <c>HostConnectionService.KickPartyMemberAsync</c>,
        /// invokes it BEFORE its first <c>await</c>, and is itself only ever
        /// reached from a UI button handler. If a future caller can reach this
        /// off-thread, route it through the bus instead.
        /// </para>
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
                    OnPartyRosterChanged?.Raise();
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
