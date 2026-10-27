// ─────────────────────────────────────────────────────────────────────────────
// LobbyRefreshScheduler.cs
// Owns the timer of the presence-lobby SAFETY POLL.
//
// Block 5 step 8 (Docs/PartySystem/BRANCH_AUDIT_2026-10.md §2): the presence lobby and the party session
// now push every join, leave and property change (steps 4-5), so the poll is no longer how a change is
// seen; it is the net under the pushes, for a push the SDK missed or a local copy its stale-index fault
// left behind. It went from a 1.5 s tick (1.1 s for 15 s after any invite event), a 6 s party-session
// read and a 4 s converge to ONE tick every SAFETY_POLL_SECONDS (+-10%), menu and match alike, that reads
// both lobbies and runs the converge. Re-measured with step 1's counters (net_logs.py: reads, 429s,
// voided, party / presence pushes).
//
// WHY this class exists:
//   Before extraction, HostConnectionService.Update() accumulated a raw float
//   _refreshTimer, compared it to an interval that changed depending on
//   _boostedRefreshUntil, and scattered the reset (_refreshTimer = 0f) and
//   boost (_boostedRefreshUntil = Time.unscaledTime + WINDOW) writes across
//   seven call sites.  Extracting the timer into one place means:
//     1. The interval decision is in exactly one method.
//     2. FireAfter() and Reset() are named operations instead of magic float writes.
//     3. The scheduler is independently testable without a MonoBehaviour.
//
// USAGE:
//   Each MonoBehaviour.Update() tick, call ShouldFireNow(Time.unscaledDeltaTime).
//   It accumulates the delta internally and returns true exactly once per
//   interval - the caller is responsible for firing RefreshAsync() and for
//   all other guards (mutex, rate-limit backoff, scene check).
//   Call FireAfter(seconds) to bring the next fire forward (the first read after a lobby join).
//   Call Reset() to restart the interval; ResetDeferred(delay) to push the next fire out further.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Registered in Reflex DI by AppManager.
//
// THREAD SAFETY:
//   Main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns the elapsed-time accumulator of the presence-lobby safety poll.
    ///
    /// <para>
    /// Call <see cref="ShouldFireNow"/> each <c>Update()</c> tick; it returns <c>true</c> at most once per
    /// interval (<see cref="SAFETY_POLL_SECONDS"/>, jittered). The pushes drive every change between fires.
    /// </para>
    ///
    /// Does NOT own the actual refresh call, rate-limit backoff, or mutex - those
    /// belong in <see cref="HostConnectionService"/>.
    ///
    /// Lifetime: pure C# - no MonoBehaviour. DI-registered by AppManager.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class LobbyRefreshScheduler
    {
        // ─────────────────────────────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Seconds between safety polls (Block 5 step 8). Each fire reads the presence lobby, runs the
        /// converge and reads the party session: two GETs <see cref="LobbyReadPacer"/> holds a second apart,
        /// so about 0.1 GETs/s against UGS Lobby's 1 GET/s/player (the poll it replaces made 0.8-1.0).
        /// </summary>
        public const float SAFETY_POLL_SECONDS = 20f;

        /// <summary>
        /// Seconds from a presence-lobby join to the first fire. The join's own snapshot seeds nothing (no
        /// push is raised for the players already there) and a simultaneous-create split heals in the
        /// converge, so the first read comes soon rather than a whole interval later.
        /// </summary>
        public const float FIRST_POLL_AFTER_JOIN_SECONDS = 2f;

        /// <summary>
        /// Fraction by which the interval is randomised, per fire (Docs/PresenceSystem/TODOS.md TODO-P3).
        /// Without it every client in a lobby of up to 100 polls on the same period, so their reads and
        /// the property writes behind them arrive in clusters - which is what the SDK's
        /// stale-index/LobbyPatcher faults feed on (PresenceSystem/BUGS.md B1).
        /// </summary>
        public const float INTERVAL_JITTER_FRACTION = 0.1f;

        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────

        private readonly float _defaultInterval;
        private float _timer;

        /// <summary>
        /// Jitter source. Deliberately a PRIVATE <see cref="System.Random"/> rather than
        /// <c>UnityEngine.Random</c>: that one is a single global sequence which
        /// <c>Random.InitState(seed)</c> callers rely on for deterministic output (the
        /// SkimRace/Switchback track walks, the flora planting draws). Pulling from it
        /// every interval would advance a sequence somebody else seeded.
        /// </summary>
        private readonly System.Random _jitter = new System.Random();

        /// <summary>
        /// The interval this cycle must reach, re-rolled on every fire. Zero until the
        /// first roll, which <see cref="ShouldFireNow"/> treats as "roll one now".
        /// </summary>
        private float _currentInterval;

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the scheduler with the interval it jitters around.
        /// </summary>
        /// <param name="defaultIntervalSeconds">
        /// Seconds between fires; the game passes <see cref="SAFETY_POLL_SECONDS"/>.
        /// </param>
        public LobbyRefreshScheduler(float defaultIntervalSeconds)
        {
            _defaultInterval = defaultIntervalSeconds;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Accumulates <paramref name="unscaledDeltaTime"/> and returns <c>true</c>
        /// exactly once per interval.  Resets the accumulator automatically when the
        /// interval is reached.
        ///
        /// The caller decides whether to actually fire the refresh; this method only
        /// manages the timing gate.
        /// </summary>
        /// <param name="unscaledDeltaTime">
        /// <c>Time.unscaledDeltaTime</c> from the current Update() tick.
        /// </param>
        public bool ShouldFireNow(float unscaledDeltaTime)
        {
            _timer += unscaledDeltaTime;
            if (_currentInterval <= 0f) _currentInterval = RollInterval();
            if (_timer < _currentInterval) return false;
            _timer = 0f;
            _currentInterval = RollInterval();
            return true;
        }

        /// <summary>
        /// The default interval scaled by a symmetric jitter in
        /// <c>[1 - INTERVAL_JITTER_FRACTION, 1 + INTERVAL_JITTER_FRACTION]</c>, so the MEAN
        /// cadence is unchanged and only the phase between clients is broken up: 18-22 s at the
        /// shipped <see cref="SAFETY_POLL_SECONDS"/>.
        /// </summary>
        private float RollInterval()
        {
            float span = _defaultInterval * INTERVAL_JITTER_FRACTION;
            return _defaultInterval - span + (float)_jitter.NextDouble() * (2f * span);
        }

        /// <summary>
        /// Makes the next fire come <paramref name="seconds"/> from now (sooner than the interval would).
        /// Used right after a presence-lobby join (<see cref="FIRST_POLL_AFTER_JOIN_SECONDS"/>).
        /// </summary>
        public void FireAfter(float seconds)
        {
            if (_currentInterval <= 0f) _currentInterval = RollInterval();
            _timer = Mathf.Max(0f, _currentInterval - seconds);
        }

        /// <summary>
        /// Resets the timer to zero so the next <see cref="ShouldFireNow"/> call
        /// returns true at the next interval.  Use after manually triggering a refresh
        /// to avoid a double-fire within the same tick.
        /// </summary>
        public void Reset()
        {
            _timer = 0f;
        }

        /// <summary>
        /// Sets the timer to a negative value so the next fire is deferred by
        /// <paramref name="deferSeconds"/> beyond the current interval.
        ///
        /// Used after joining a party session: the freshly-joined session needs a
        /// settling period before the first member-sync refresh fires.
        /// </summary>
        /// <param name="deferSeconds">
        /// Additional seconds to wait beyond the normal interval.
        /// </param>
        public void ResetDeferred(float deferSeconds)
        {
            _timer = -deferSeconds;
        }
    }
}
