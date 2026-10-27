// ─────────────────────────────────────────────────────────────────────────────
// PresencePushChannel.cs
// The presence lobby's push events, turned into two flags the main thread drains.
//
// WHY (Block 5 step 5, Docs/PartySystem/BRANCH_AUDIT_2026-10.md §2; the August presence work's
// b0adfa7284 and 4022675284 on Ys-rescue-2026-08-24):
//   The presence lobby is an ISession, and the SDK raises its roster events after it has patched the
//   local copy. Nothing subscribed to them, so every arrival, departure and status change anywhere
//   was discovered only by the next poll (then 1.5 s in the menu, 10 s in a match), and a lobby the
//   service had dropped us from was noticed only after three failed reads.
//
//   Two flags, because the recoveries differ: a roster change means "re-read what we already hold"
//   (no GET - the SDK applied the change before raising it), and RemovedFromSession / Deleted mean
//   "rejoin now".
//
// THREAD SAFETY:
//   The SDK gives no thread guarantee for these events, and a SOAP raise runs its listeners inline,
//   so a handler here only sets an int with Interlocked (the rule from the August work's B16).
//   Attach / Detach / Consume* are main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using System.Threading;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Subscribes to one presence-lobby session at a time and exposes what it pushed as flags.
    /// <see cref="PresenceLobbyService"/> owns one and routes every active-lobby change through it.
    /// </summary>
    public sealed class PresencePushChannel
    {
        ISession _session;
        int _rosterDirty;
        int _membershipLost;
        readonly DepartureReports _departures = new();

        /// <summary>The session currently listened to, or null.</summary>
        public ISession Session => _session;

        /// <summary>
        /// Listens to <paramref name="session"/> instead of whatever it listened to before; null just
        /// stops listening. Attaching the session already attached changes nothing.
        /// </summary>
        public void Attach(ISession session)
        {
            if (ReferenceEquals(session, _session)) return;
            Detach();
            if (session == null) return;
            _session = session;
            session.PlayerJoined            += OnPlayerJoined;
            session.PlayerHasLeft           += OnPlayerHasLeft;
            session.PlayerPropertiesChanged += OnPlayerPropertiesChanged;
            session.RemovedFromSession      += OnMembershipLost;
            session.Deleted                 += OnMembershipLost;
        }

        /// <summary>
        /// Stops listening and drops anything not yet drained: a flag raised by a lobby we no longer
        /// hold says nothing about the next one. Call it BEFORE a deliberate leave or delete, so our own
        /// leaving is not read as being thrown out.
        /// </summary>
        public void Detach()
        {
            var s = _session;
            _session = null;
            if (s != null)
            {
                s.PlayerJoined            -= OnPlayerJoined;
                s.PlayerHasLeft           -= OnPlayerHasLeft;
                s.PlayerPropertiesChanged -= OnPlayerPropertiesChanged;
                s.RemovedFromSession      -= OnMembershipLost;
                s.Deleted                 -= OnMembershipLost;
            }
            Interlocked.Exchange(ref _rosterDirty, 0);
            Interlocked.Exchange(ref _membershipLost, 0);
            _departures.Clear();
        }

        /// <summary>True once per burst of roster pushes (joins, leaves, property changes) since the last call.</summary>
        public bool ConsumeRosterDirty() => Interlocked.Exchange(ref _rosterDirty, 0) == 1;

        /// <summary>True once after the service removed us from the lobby or deleted it.</summary>
        public bool ConsumeMembershipLost() => Interlocked.Exchange(ref _membershipLost, 0) == 1;

        /// <summary>
        /// Moves the players the lobby said left since the last call into <paramref name="into"/>
        /// (Block 5 step 7: a stated departure is removed at once, see <see cref="AbsenceStrikes"/>).
        /// </summary>
        public void TakeDepartures(System.Collections.Generic.ISet<string> into) => _departures.TakeInto(into);

        void MarkRosterDirty() => Interlocked.Exchange(ref _rosterDirty, 1);
        void OnPlayerJoined(string playerId) => MarkRosterDirty();
        void OnPlayerHasLeft(string playerId)
        {
            _departures.Report(playerId);
            MarkRosterDirty();
        }
        void OnPlayerPropertiesChanged() => MarkRosterDirty();
        void OnMembershipLost() => Interlocked.Exchange(ref _membershipLost, 1);
    }
}
