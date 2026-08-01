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
//   Each MonoBehaviour.Update() tick, call Accumulate(Time.unscaledDeltaTime)
//   UNCONDITIONALLY, then call TryConsumeFire() once the caller's own guards
//   (mutex, rate-limit backoff, scene check) have passed. Accumulating before
//   the guards is what makes the timer measure wall time rather than eligible
//   time - see Accumulate's remarks for why that distinction mattered.
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
    /// Call <see cref="Accumulate"/> each <c>Update()</c> tick and
    /// <see cref="TryConsumeFire"/> after the caller's guards; the latter returns
    /// <c>true</c> at most once per interval.  Use <see cref="Boost"/> to
    /// temporarily shorten the interval for 15 seconds after invite events, and
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
        /// Refresh interval while in boosted mode (seconds).
        ///
        /// <para>
        /// This was 0.75f, with a comment claiming it "keeps us safely under"
        /// the ~1/s UGS lobby read cap. It does the opposite: 0.75s is 1.33
        /// reads/s, and the boost window overlaps the invite handshake, which is
        /// also the busiest stretch for property WRITES (each of which costs
        /// further UGS calls). 1.1s is 0.91 reads/s, which leaves room for the
        /// periodic converge query on top and still stays under the cap.
        /// </para>
        ///
        /// <para>
        /// The ~0.35s of extra worst-case invite latency is a good trade: a 429
        /// parks the entire <see cref="HostConnectionService.Update"/> loop for
        /// <c>RATE_LIMIT_BACKOFF_SECONDS</c> (6s) - so tripping the cap to save a
        /// third of a second costs about seventeen times that in blindness.
        /// </para>
        /// </summary>
        public const float BOOSTED_INTERVAL_SECONDS = 1.1f;

        /// <summary>
        /// Per-fire random scaling applied to the interval, as a +/- fraction.
        /// Without it every client in the presence lobby polls on the same
        /// cadence, and since they mostly launch in bursts (a party starting
        /// together, an MPPM run), their reads arrive at the service in phase -
        /// which concentrates load into periodic spikes instead of spreading it,
        /// and raises 429 incidence for everyone at once. Jitter is additive
        /// noise only; it does not change the median cadence.
        /// See Docs/PresenceSystem/TODOS.md TODO-P3.
        /// </summary>
        private const float INTERVAL_JITTER_FRACTION = 0.1f;

        /// <summary>
        /// How long (seconds) a single Boost() call keeps the scheduler in boosted
        /// mode.  15s is long enough to cover a full invite round-trip including the
        /// PENDING → real-id republish phase.
        /// </summary>
        public const float BOOST_WINDOW_SECONDS = 15f;

        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────

        private float _defaultInterval;
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
        /// Normal (non-boosted) refresh interval in seconds.
        ///
        /// <para>
        /// Settable because this service is DI-constructed by a factory that has
        /// no access to the owning MonoBehaviour's serialized configuration.
        /// <see cref="HostConnectionService"/> assigns its inspector value here in
        /// <c>Start</c>. Before that, the factory's constructor argument and the
        /// inspector field were two independent numbers that had silently drifted
        /// apart - the factory passed a hardcoded 1.5 while the shipped prefab
        /// said 3, so the field named "how often to refresh the online player
        /// list" had no effect on how often the online player list refreshed.
        /// </para>
        /// </summary>
        public float DefaultInterval
        {
            get => _defaultInterval;
            set => _defaultInterval = value;
        }

        /// <summary>
        /// True while the scheduler is in boosted mode (a recent <see cref="Boost"/>
        /// call raised the refresh frequency).  Read for diagnostic logs only - the
        /// interval switch is handled automatically inside <see cref="TryConsumeFire"/>.
        /// </summary>
        public bool IsBoosted => Time.unscaledTime < _boostedUntil;

        /// <summary>
        /// Advances the accumulator by one frame. Call this EVERY frame,
        /// unconditionally - before any caller-side eligibility gates.
        ///
        /// <para>
        /// Split out of the old <c>ShouldFireNow(dt)</c> because that method both
        /// accumulated and consumed, so it could only be called once the caller's
        /// gates had already passed. The accumulator therefore measured
        /// <i>eligible</i> time rather than wall time: while the lobby mutex was
        /// held - which is precisely when a property write is in flight, i.e.
        /// when party state is CHANGING - the clock simply stopped. A write
        /// costing several seconds paused the timer mid-interval, so the next
        /// poll landed a full further interval after the write finished. The
        /// refresh loop went blind exactly when the roster was moving.
        /// </para>
        /// </summary>
        /// <param name="unscaledDeltaTime">
        /// <c>Time.unscaledDeltaTime</c> from the current Update() tick.
        /// </param>
        public void Accumulate(float unscaledDeltaTime) => _timer += unscaledDeltaTime;

        /// <summary>
        /// Returns <c>true</c> at most once per interval, consuming the
        /// accumulated time when it does. Call this AFTER the caller's
        /// eligibility gates, so a tick that was not allowed to fire leaves the
        /// accumulated time intact and fires as soon as it becomes eligible
        /// instead of restarting the interval.
        ///
        /// <para>
        /// Cannot burst: the accumulator is zeroed on consume, so a long
        /// ineligible stretch produces exactly one catch-up fire, not one per
        /// elapsed interval.
        /// </para>
        /// </summary>
        public bool TryConsumeFire()
        {
            float interval = IsBoosted ? BOOSTED_INTERVAL_SECONDS : _defaultInterval;

            // Re-rolled per fire rather than per client so a client that drifts
            // into phase with another does not stay there.
            interval *= 1f + Random.Range(-INTERVAL_JITTER_FRACTION, INTERVAL_JITTER_FRACTION);

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
        /// Resets the timer to zero so the next <see cref="TryConsumeFire"/> call
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
