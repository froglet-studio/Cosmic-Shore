// ─────────────────────────────────────────────────────────────────────────────
// PartyState.cs
// Defines every lifecycle phase a player can be in relative to the party system.
//
// How to read this file:
//   Each enum member represents a STABLE, OBSERVABLE STATE - not a transition.
//   Transitions (e.g. "user pressed Accept") live in HostConnectionService and
//   are validated by PartyStateMachine.  Adding a new state here requires also
//   adding its legal transitions in PartyStateMachine._legalTransitions.
// ─────────────────────────────────────────────────────────────────────────────

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// All possible phases of the party lifecycle, from before authentication
    /// to being fully connected in a multiplayer session.
    ///
    /// State ownership: <see cref="PartyStateMachine"/> (single writer).
    /// Readable by any system via <see cref="IPartyStateQuery.CurrentState"/>.
    /// </summary>
    public enum PartyState
    {
        /// <summary>
        /// Not in any UGS session.  This is the initial state before authentication
        /// completes, and also the state entered on sign-out or fatal error.
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Joined the global presence lobby (no Relay, no NetworkManager activity).
        ///
        /// TRANSIENT - auto-advances immediately to <see cref="HostingParty"/> as
        /// <see cref="HostConnectionService"/> creates the local player's solo Relay
        /// session.  This state is never a resting state; players never linger here.
        /// The presence lobby is the discovery layer only - sending and accepting
        /// invites require an active Relay session (<see cref="InParty"/>).
        /// </summary>
        InPresenceLobby = 1,

        /// <summary>
        /// The local player has sent at least one invite and is waiting for a
        /// recipient to accept.  The local Relay party session ALREADY EXISTS
        /// (transitioned here from <see cref="InParty"/>).  No NetworkManager
        /// changes happen when entering this state - the host vessel stays alive.
        /// Transitions to <see cref="InParty"/> when the joiner NM-connects or
        /// when all invites are cancelled.
        /// </summary>
        Inviting = 2,

        /// <summary>
        /// The local player has accepted someone else's invite and is in the
        /// process of connecting to the host's Relay session.  Entered from
        /// <see cref="InParty"/> - the local Relay session is shut down first,
        /// then the host's session is joined.  This state covers the period from
        /// "Accept pressed" to "NetworkManager connected" on the host's Relay.
        /// </summary>
        JoiningParty = 3,

        /// <summary>
        /// A Relay party session is being created or recreated.
        /// Entered from <see cref="InPresenceLobby"/> (initial setup),
        /// <see cref="InParty"/> (leave), <see cref="JoiningParty"/> (join failed),
        /// or <see cref="Reconnecting"/> (recovery).  This state is transient
        /// (~1-2 s) and always advances to <see cref="InParty"/> on success.
        /// </summary>
        HostingParty = 4,

        /// <summary>
        /// The persistent baseline state - every authenticated player has a live
        /// Relay-backed party session.  The session may be solo (just the local
        /// player) or multiplayer (with remote members).  The local vessel is
        /// spawned and visible while in this state.
        ///
        /// Possible transitions from here:
        ///   → <see cref="Inviting"/>     (sent first invite)
        ///   → <see cref="JoiningParty"/> (accepted someone's invite)
        ///   → <see cref="HostingParty"/> (leave party → recreate solo)
        ///   → <see cref="Reconnecting"/> (connection lost)
        /// </summary>
        InParty = 5,

        /// <summary>
        /// Connection to the presence lobby or party session was lost unexpectedly.
        /// The system is attempting to restore the connection.  From here the system
        /// either recovers back to <see cref="InParty"/> (rejoin) or transitions to
        /// <see cref="HostingParty"/> (recreate solo session) after max retries.
        /// </summary>
        Reconnecting = 6,
    }
}
