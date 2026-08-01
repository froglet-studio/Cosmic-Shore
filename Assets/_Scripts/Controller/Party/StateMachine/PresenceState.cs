// ─────────────────────────────────────────────────────────────────────────────
// PresenceState.cs
// What the local player is DOING, as far as everyone else is concerned.
//
// How to read this file:
//   Each member is a STABLE, OBSERVABLE state - not a transition. Transitions
//   live in PresenceService and are validated by PresenceStateMachine.
//
// WHY this is a SIBLING of PartyState, not an extension of it:
//   PartyState answers "which Relay session am I in?"
//   PresenceState answers "what am I doing, and should other people see me?"
//   These are ORTHOGONAL - you are simultaneously InParty and InMatch. Folding
//   them together would mean a 7x7 cross-product and re-deriving the 14-edge
//   legal table the invite handshake depends on. PresenceStateMachine observes
//   PartyStateMachine.OnStateChanged and never writes it.
// ─────────────────────────────────────────────────────────────────────────────

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The local player's presence lifecycle - what remote clients should be
    /// told about this player.
    ///
    /// State ownership: <see cref="PresenceStateMachine"/> (single writer, driven
    /// by <see cref="PresenceService"/>). Readable by any system via
    /// <c>PresenceService.CurrentState</c>.
    /// </summary>
    public enum PresenceState
    {
        /// <summary>
        /// Not signed in, or an explicit leave has completed. Nothing is
        /// published because we are not in the lobby.
        /// </summary>
        Offline = 0,

        /// <summary>
        /// Presence-lobby join in flight. Local only - nothing published yet.
        /// </summary>
        Joining = 1,

        /// <summary>
        /// In the presence lobby with identity published, but the local
        /// Menu_Main vessel does not exist yet.
        ///
        /// <para>
        /// This state is what stops a peer being rendered as a fully-formed,
        /// invitable row before they are actually in the world - the "shows
        /// wrong information" half of the reported symptom. A player here is
        /// visible but not yet ready.
        /// </para>
        /// </summary>
        Announced = 2,

        /// <summary>
        /// <b>The lava-lamp vessel is spawned.</b> The player is in the world and
        /// fully interactable. This is the "I am here" broadcast: entered on
        /// <c>GameDataSO.OnClientReady</c>, which
        /// <c>ClientPlayerVesselInitializer.InitializePair</c> raises for the
        /// local user once its vessel exists.
        /// </summary>
        Present = 3,

        /// <summary>
        /// Playing an arcade game rather than sitting in the menu. Drives the
        /// <c>matchName</c> presence property and therefore
        /// <c>OnlineInfoEntry.Status.InMatch</c> on every peer.
        /// </summary>
        InMatch = 4,

        /// <summary>
        /// Presence-lobby membership was lost and a rejoin is in flight. Distinct
        /// from <see cref="Offline"/> because we expect to come back; UI should
        /// show a "reconnecting" affordance rather than an empty list.
        /// </summary>
        Recovering = 5,

        /// <summary>
        /// A terminal leave is in flight - app quit, backgrounding, or sign-out.
        /// Bounded; always advances to <see cref="Offline"/>.
        /// </summary>
        Departing = 6,
    }
}
