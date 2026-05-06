// ─────────────────────────────────────────────────────────────────────────────
// PartyState.cs
// Defines every lifecycle phase a player can be in relative to the party system.
//
// How to read this file:
//   Each enum member represents a STABLE, OBSERVABLE STATE — not a transition.
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
        /// The player can browse online players, send invites, and receive invites.
        /// This is the "idle online" state for authenticated players.
        /// </summary>
        InPresenceLobby = 1,

        /// <summary>
        /// The local player has sent at least one invite and is waiting for a
        /// recipient to accept.  The Relay party session does NOT exist yet
        /// (session ID in outgoing payloads is "PENDING").  No NetworkManager
        /// activity happens in this state.
        /// </summary>
        Inviting = 2,

        /// <summary>
        /// The local player has accepted someone else's invite and is in the
        /// process of connecting to the host's Relay session.  This state covers
        /// the period from "Accept pressed" to "NetworkManager connected".
        /// </summary>
        JoiningParty = 3,

        /// <summary>
        /// The local player is the HOST.  An acceptance signal was detected, the
        /// Relay session has been created, and payloads have been republished with
        /// the real session ID.  Waiting for the recipient's NetworkManager to
        /// connect.
        /// </summary>
        HostingParty = 4,

        /// <summary>
        /// Fully inside a party session, either as host or client.  Both players'
        /// vessels are visible and the party member list is populated.
        /// </summary>
        InParty = 5,

        /// <summary>
        /// Connection to the presence lobby or party session was lost unexpectedly.
        /// The system is attempting to restore the connection.  From here the system
        /// either recovers back to <see cref="InParty"/> or falls back to
        /// <see cref="InPresenceLobby"/> after max retries.
        /// </summary>
        Reconnecting = 6,
    }
}
