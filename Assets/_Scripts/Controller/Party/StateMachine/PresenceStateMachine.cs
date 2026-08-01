// ─────────────────────────────────────────────────────────────────────────────
// PresenceStateMachine.cs
// Single source of truth for "what am I doing, and should other people see me".
//
// Deliberately mirrors PartyStateMachine's shape - same CurrentState /
// OnStateChanged / TryTransition / static legal table / warn-don't-throw - so
// anyone who has read one has read both. It is a SIBLING, not a subclass and
// not an extension: see the header of PresenceState.cs for why the two axes
// must not be folded together.
//
// HOW to use it:
//   1. Read:    _presence.CurrentState == PresenceState.Present
//   2. Change:  _presence.TryTransition(PresenceState.InMatch)
//   3. React:   _presence.OnStateChanged += (from, to) => { ... }
//
// THREAD SAFETY: main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Validates and executes presence lifecycle transitions.
    /// Pure C# class - no MonoBehaviour, no Unity lifecycle.
    /// </summary>
    public sealed class PresenceStateMachine
    {
        /// <summary>The current presence phase.</summary>
        public PresenceState CurrentState { get; private set; } = PresenceState.Offline;

        /// <summary>
        /// Fired immediately after every valid transition, as
        /// (previousState, newState). This is the hook UI and other subsystems
        /// attach to - the single place to learn that this player entered the
        /// world, entered a match, or started reconnecting.
        /// </summary>
        public event Action<PresenceState, PresenceState> OnStateChanged;

        /// <summary>
        /// Every legal (from, to) pair. <see cref="PresenceState.Offline"/> and
        /// <see cref="PresenceState.Departing"/> are always reachable - they are
        /// the emergency exits for sign-out, quit and fatal errors - so they are
        /// not listed here.
        /// </summary>
        private static readonly HashSet<(PresenceState from, PresenceState to)> LegalTransitions = new()
        {
            (PresenceState.Offline,    PresenceState.Joining),      // auth sign-in → lobby join starts

            (PresenceState.Joining,    PresenceState.Announced),    // lobby joined, identity published

            // The vessel-spawn broadcast. Announced → Present is the moment this
            // player becomes a real, interactable row on every peer.
            (PresenceState.Announced,  PresenceState.Present),      // Menu_Main lava-lamp vessel spawned
            (PresenceState.Announced,  PresenceState.Recovering),   // membership lost before the vessel existed

            (PresenceState.Present,    PresenceState.InMatch),      // launched an arcade game
            (PresenceState.Present,    PresenceState.Recovering),   // membership lost

            (PresenceState.InMatch,    PresenceState.Present),      // returned to Menu_Main
            (PresenceState.InMatch,    PresenceState.Recovering),   // membership lost mid-match

            // Recovery re-enters at Announced, not Present: the rejoin republishes
            // identity, and the vessel-spawn signal re-arms it from there. Going
            // straight to Present would assert a vessel we have not re-confirmed.
            (PresenceState.Recovering, PresenceState.Announced),    // rejoin succeeded

            (PresenceState.Departing,  PresenceState.Offline),      // bounded leave finished (or timed out)

            // Resume-from-background: Offline → Joining covers it, since the
            // pause path leaves the lobby and the resume path rejoins.
        };

        /// <summary>
        /// Attempts to move to <paramref name="to"/>. Returns <c>true</c> and
        /// fires <see cref="OnStateChanged"/> on success; returns <c>false</c>
        /// and logs a warning on an illegal transition.
        ///
        /// <para>
        /// <b>Check the return value.</b> On the party machine it is ignored at
        /// every call site, which is how a rejected transition could leave the
        /// system asserting a state it never entered. A rejection here is a real
        /// diagnostic - <c>Announced → Present</c> failing means a vessel spawned
        /// while we were not in a lobby.
        /// </para>
        /// </summary>
        public bool TryTransition(PresenceState to)
        {
            if (CurrentState == to) return false;   // idempotent re-entry is not an error

            if (!IsLegal(CurrentState, to))
            {
                Debug.LogWarning(
                    $"[PresenceStateMachine] Illegal transition: {CurrentState} → {to}. " +
                    $"Add it to LegalTransitions if it is intentional.");
                return false;
            }

            var from = CurrentState;
            CurrentState = to;

            // Always on: this is the timeline you read in the MPPM console when
            // asking "why did that player appear/disappear when they did?".
            Debug.Log($"[PresenceStateMachine] {from} → {to}");

            OnStateChanged?.Invoke(from, to);
            return true;
        }

        /// <summary>
        /// True once the player is in the world - i.e. remote clients should
        /// render them as a real, interactable row.
        /// </summary>
        public bool IsInWorld =>
            CurrentState == PresenceState.Present || CurrentState == PresenceState.InMatch;

        private static bool IsLegal(PresenceState from, PresenceState to) =>
            to == PresenceState.Offline ||
            to == PresenceState.Departing ||
            LegalTransitions.Contains((from, to));
    }
}
