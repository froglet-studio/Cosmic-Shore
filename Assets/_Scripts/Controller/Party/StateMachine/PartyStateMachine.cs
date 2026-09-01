// ─────────────────────────────────────────────────────────────────────────────
// PartyStateMachine.cs
// Single source of truth for the party lifecycle phase.
//
// WHY this exists:
//   Before this class, party state was tracked by a scatter of boolean flags
//   (_initialized, _isHost, _inviteSent, _joining, _leaving) in HostConnectionService.
//   Those flags drifted out of sync across async paths and made bug diagnosis
//   extremely hard - "why was the vessel destroyed?" often traced back to a flag
//   that was left in the wrong state after a failed transition.
//
//   This class replaces all of those flags with a single CurrentState that can
//   only move along pre-approved paths.  If code attempts an illegal transition,
//   it gets an immediate, explicit warning - not a silent downstream failure.
//
// HOW to use it:
//   1. Read:    _stateMachine.CurrentState == PartyState.InParty
//   2. Change:  _stateMachine.TryTransition(PartyState.InPresenceLobby)
//   3. React:   _stateMachine.OnStateChanged += (from, to) => { ... }
//
// THREAD SAFETY: main-thread only.  All callers (HostConnectionService,
//   PartyInviteController) run on Unity's main thread via UniTask.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Validates and executes party lifecycle transitions.
    /// Pure C# class - no MonoBehaviour, no Unity lifecycle.
    ///
    /// Lifetime: created as a field on <see cref="HostConnectionService"/>.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class PartyStateMachine
    {
        // ─────────────────────────────────────────────────────────────────────
        // Public state
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>The current phase of the party lifecycle.</summary>
        public PartyState CurrentState { get; private set; } = PartyState.Disconnected;

        /// <summary>
        /// Fired immediately after every valid transition.
        /// Parameters: (previousState, newState).
        /// Safe to subscribe from any MonoBehaviour - callbacks run on main thread.
        /// </summary>
        public event Action<PartyState, PartyState> OnStateChanged;

        // ─────────────────────────────────────────────────────────────────────
        // Transition table (all legal moves, except → Disconnected which is
        // always allowed - see TryTransition below)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every (from, to) pair that the state machine will allow.
        ///
        /// Rule: transitions NOT in this set AND NOT targeting Disconnected are
        /// rejected with a warning.  To add a new state, add it to PartyState.cs
        /// AND add all its allowed transitions here.
        /// </summary>
        private static readonly HashSet<(PartyState from, PartyState to)> LegalTransitions = new()
        {
            // Normal forward flow ─────────────────────────────────────────────
            (PartyState.Disconnected,    PartyState.InPresenceLobby),   // auth sign-in + lobby join

            // InPresenceLobby is now transient: immediately auto-advances to HostingParty.
            // Inviting and JoiningParty are only reachable from InParty (Relay always exists).
            (PartyState.InPresenceLobby, PartyState.HostingParty),      // auto: create solo Relay after lobby join
            (PartyState.InPresenceLobby, PartyState.Disconnected),      // sign-out / network loss

            (PartyState.Inviting,        PartyState.HostingParty),      // acceptance signal detected (legacy path; session already exists)
            (PartyState.Inviting,        PartyState.InParty),           // all invites cancelled, or joiner NM-connected
            (PartyState.Inviting,        PartyState.Reconnecting),      // lobby connection lost

            (PartyState.JoiningParty,    PartyState.InParty),           // Relay join + NM connect
            (PartyState.JoiningParty,    PartyState.HostingParty),      // join failed → recreate solo Relay
            (PartyState.JoiningParty,    PartyState.Reconnecting),      // connection dropped mid-join

            // HostingParty is now also used as the "recreating solo session" transient state.
            (PartyState.HostingParty,    PartyState.InParty),           // session live (solo or first joiner NM-connected)

            // InParty is the persistent baseline - every player has a live Relay session.
            (PartyState.InParty,         PartyState.Inviting),          // sent first invite (no NM change)
            (PartyState.InParty,         PartyState.JoiningParty),      // accepted someone else's invite (leave own Relay)
            (PartyState.InParty,         PartyState.HostingParty),      // leave party → recreate solo Relay
            (PartyState.InParty,         PartyState.Reconnecting),      // connection lost

            // Recovery paths ──────────────────────────────────────────────────
            (PartyState.Reconnecting,    PartyState.HostingParty),      // recovery: recreate solo Relay
            (PartyState.Reconnecting,    PartyState.InParty),           // rejoin succeeded

            // Re-entry through the FRONT DOOR. The refresh watchdog can drop us into
            // Reconnecting at any moment (MAX_REFRESH_ERRORS_BEFORE_RECONNECT), including while
            // a reconnect / offline→online switch is re-running HCS init - and that init's first
            // move is InPresenceLobby. Without this the transition was refused, HCS never
            // finished initialising, no Relay session was ever created, and the auth scene's
            // Relay wait timed out against a session nobody was going to make.
            (PartyState.Reconnecting,    PartyState.InPresenceLobby),   // re-run lobby join after a drop
        };

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to move to <paramref name="to"/>.
        /// Returns <c>true</c> and fires <see cref="OnStateChanged"/> on success.
        /// Returns <c>false</c> and logs a warning on illegal transitions.
        ///
        /// Special rule: transitioning to <see cref="PartyState.Disconnected"/> is
        /// always allowed from any state - it is the "emergency exit" for sign-out
        /// and fatal errors.
        /// </summary>
        /// <param name="to">The desired next state.</param>
        /// <returns>True if the transition was legal and applied.</returns>
        public bool TryTransition(PartyState to)
        {
            if (!IsLegal(CurrentState, to))
            {
                // Log a warning instead of throwing - an illegal transition
                // should be loudly visible but should not crash the game.
                Debug.LogWarning(
                    $"[PartyStateMachine] Illegal transition: {CurrentState} → {to}. " +
                    $"Add it to LegalTransitions if it is intentional.");
                return false;
            }

            var from = CurrentState;
            CurrentState = to;

            // This log line is intentionally always on so the MPPM console shows
            // the exact lifecycle timeline during manual testing.
            Debug.Log($"[PartyStateMachine] {from} → {to}");

            OnStateChanged?.Invoke(from, to);
            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if the machine is currently in any of the given states.
        /// Convenience helper for multi-state guard clauses.
        /// </summary>
        public bool IsIn(PartyState a, PartyState b) =>
            CurrentState == a || CurrentState == b;

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Disconnected is always a legal destination - it is the "emergency exit"
        /// for sign-out and fatal network errors.  All other pairs are checked
        /// against the static table.
        /// </summary>
        private static bool IsLegal(PartyState from, PartyState to) =>
            to == PartyState.Disconnected || LegalTransitions.Contains((from, to));
    }
}
