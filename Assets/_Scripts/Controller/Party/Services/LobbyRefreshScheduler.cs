// ─────────────────────────────────────────────────────────────────────────────
// LobbyRefreshScheduler.cs
// Owns the refresh timer and boost state for the presence-lobby poll cycle.
//
// WHY this class exists:
//   Before extraction, HostConnectionService.Update() accumulated a raw float
//   _refreshTimer, compared it to an interval that changed depending on
//   _boostedRefreshUntil, and scattered the reset (_refreshTimer = 0f) and
//   boost (_boostedRefreshUntil = Time.unscaledTime + WINDOW) writes across
//   seven call sites.  Extracting the timer into one place means:
//     1. The interval decision (normal vs. boosted) is in exactly one method.
//     2. Boost() and Reset() are named operations instead of magic float writes.
//     3. The scheduler is independently testable without a MonoBehaviour.
//
// USAGE:
//   Each MonoBehaviour.Update() tick, call ShouldFireNow(Time.unscaledDeltaTime).
//   It accumulates the delta internally and returns true exactly once per
//   interval - the caller is responsible for firing RefreshAsync() and for
//   all other guards (mutex, rate-limit backoff, scene check).
//   Call Boost() to enter the fast-refresh window after invite events.
//   Call Reset() to force the next fire to occur immediately.
//   Call ResetDeferred(delay) to push the next fire out by a custom amount.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Instantiated as a field on
//   HostConnectionService for Phases 6-11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns the elapsed-time accumulator and boosted-refresh window for the
    /// presence-lobby poll cycle.
    ///
    /// <para>
    /// Call <see cref="ShouldFireNow"/> each <c>Update()</c> tick; it returns
    /// <c>true</c> at most once per interval.  Use <see cref="Boost"/> to
    /// temporarily halve the interval for 15 seconds after invite events, and
    /// <see cref="Reset"/> to trigger an immediate next fire.
    /// </para>
    ///
    /// Does NOT own the actual refresh call, rate-limit backoff, or mutex - those
    /// belong in <see cref="HostConnectionService"/>.
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class LobbyRefreshScheduler
    {
        // ─────────────────────────────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Refresh interval while in boosted mode (seconds).  UGS lobby reads are
        /// rate-limited to ~1/s per client; 0.75s keeps us safely under that cap
        /// while staying responsive enough that invite arrivals feel instant.
        /// </summary>
        public const float BOOSTED_INTERVAL_SECONDS = 0.75f;

        /// <summary>
        /// How long (seconds) a single Boost() call keeps the scheduler in boosted
        /// mode.  15s is long enough to cover a full invite round-trip including the
        /// PENDING → real-id republish phase.
        /// </summary>
        public const float BOOST_WINDOW_SECONDS = 15f;

        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────

        private readonly float _defaultInterval;
        private float _timer;
        private float _boostedUntil;  // unscaled time; 0 = not boosted

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the scheduler with a configurable default interval.
        /// </summary>
        /// <param name="defaultIntervalSeconds">
        /// Normal (non-boosted) refresh interval in seconds.
        /// Typically 1.5s - see <see cref="HostConnectionService.refreshIntervalSeconds"/>.
        /// </param>
        public LobbyRefreshScheduler(float defaultIntervalSeconds)
        {
            _defaultInterval = defaultIntervalSeconds;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// True while the scheduler is in boosted mode (a recent <see cref="Boost"/>
        /// call raised the refresh frequency).  Read for diagnostic logs only - the
        /// interval switch is handled automatically inside <see cref="ShouldFireNow"/>.
        /// </summary>
        public bool IsBoosted => Time.unscaledTime < _boostedUntil;

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
            float interval = IsBoosted ? BOOSTED_INTERVAL_SECONDS : _defaultInterval;
            if (_timer < interval) return false;
            _timer = 0f;
            return true;
        }

        /// <summary>
        /// Enters boosted mode for <see cref="BOOST_WINDOW_SECONDS"/> seconds.
        /// Safe to call repeatedly - each call extends the window from the current
        /// moment, so closely-spaced invite events don't shorten the window.
        /// </summary>
        /// <remarks>
        /// Boost is typically activated after:
        /// <list type="bullet">
        ///   <item>Local player sends an invite (host wants rapid accept-signal detection).</item>
        ///   <item>Incoming invite detected (recipient wants rapid republish detection).</item>
        ///   <item>Acceptance signal received (host wants rapid session-ready detection).</item>
        /// </list>
        /// </remarks>
        public void Boost()
        {
            _boostedUntil = Time.unscaledTime + BOOST_WINDOW_SECONDS;
            Debug.Log($"[LobbyRefreshScheduler] Boosted - fast refresh until +{BOOST_WINDOW_SECONDS:F0}s");
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
