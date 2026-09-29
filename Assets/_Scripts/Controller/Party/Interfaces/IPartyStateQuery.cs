// ─────────────────────────────────────────────────────────────────────────────
// IPartyStateQuery.cs
// Read-only window into the current party lifecycle phase.
//
// WHY a separate interface?
//   FriendsInitializer and any UI component that wants to display "In Party"
//   or show the active session ID should NOT depend on the full
//   HostConnectionService class.  Depending on the concrete class creates
//   a tight coupling - if HCS changes, every reader might break.
//
//   With this interface, readers only see what they actually need:
//   - What state are we in?
//   - What session ID is active (for presence status)?
//   - How many party members are there?
//
//   HostConnectionService implements this interface; clients receive it via
//   Reflex DI or by reading HostConnectionService.Instance (which is always
//   valid after Awake on the same persistent GO).
// ─────────────────────────────────────────────────────────────────────────────

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Read-only view of the party lifecycle state.
    /// Implemented by <see cref="HostConnectionService"/> (single writer).
    ///
    /// Use this interface in consumers (FriendsInitializer, UI) instead of
    /// depending directly on HostConnectionService - it narrows the surface
    /// and makes each dependency explicit and testable.
    /// </summary>
    public interface IPartyStateQuery
    {
        /// <summary>
        /// The current phase of the party lifecycle.
        /// Changes are observable via <see cref="PartyStateMachine.OnStateChanged"/>
        /// on <see cref="HostConnectionService.StateMachine"/>.
        /// </summary>
        PartyState CurrentState { get; }

        /// <summary>
        /// The UGS session ID of the active Relay party session, or an empty
        /// string if no session is live.  Used by FriendsInitializer to populate
        /// the rich presence "partySessionId" field.
        /// </summary>
        string ActivePartySessionId { get; }

        /// <summary>
        /// Number of players currently in the party (including the local player).
        /// Zero if not in a party session.
        /// </summary>
        int PartyMemberCount { get; }
    }
}
