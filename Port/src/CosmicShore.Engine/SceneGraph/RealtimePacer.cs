using System;
using System.Diagnostics;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Keeps a fixed-step headless loop on the wall clock: a tick that finishes early waits out the
    /// rest of its step, and a tick that runs late is caught up by not waiting.
    ///
    /// A <c>--headless</c> player ticks 1/60 s as fast as the CPU allows, so an idle menu runs its game
    /// clock ~200x ahead of the wall. One process alone cannot tell. Several processes talking through
    /// a session folder and sockets can: those run on the wall clock, so every game-time timer fires
    /// early against them (the party harness's run 3: an invite's 60 s lifetime lapsed before the guest
    /// had polled it). Unity has no such mode, so multiplayer runs pace to the wall
    /// (<c>--realtime</c>, docs/MULTIPLAYER.md §6.1).
    ///
    /// Debt past <see cref="MaxDebtSeconds"/> is dropped, so a long load is not followed by a burst of
    /// compressed time (the same reason Unity caps deltaTime).
    /// </summary>
    public sealed class RealtimePacer
    {
        public const double MaxDebtSeconds = 0.25;

        readonly double _stepMs;
        readonly Func<double> _elapsedMs;
        readonly Action<int> _sleep;
        double _ticks;

        public RealtimePacer(double stepSeconds) : this(stepSeconds, null, null) { }

        /// <summary>Test seam: a fake clock (milliseconds since start) and a fake sleep.</summary>
        public RealtimePacer(double stepSeconds, Func<double> elapsedMs, Action<int> sleep)
        {
            _stepMs = stepSeconds * 1000.0;
            if (elapsedMs == null)
            {
                long origin = Stopwatch.GetTimestamp();
                elapsedMs = () => Stopwatch.GetElapsedTime(origin).TotalMilliseconds;
            }
            _elapsedMs = elapsedMs;
            _sleep = sleep ?? System.Threading.Thread.Sleep;
        }

        /// <summary>True when the environment asks for it (COSMIC_SHORE_HEADLESS_REALTIME=1).</summary>
        public static bool RequestedByEnvironment =>
            Environment.GetEnvironmentVariable("COSMIC_SHORE_HEADLESS_REALTIME") == "1";

        /// <summary>Call once after each tick. Returns the milliseconds it slept (0 when behind).</summary>
        public int AfterTick()
        {
            _ticks++;
            double ahead = _ticks * _stepMs - _elapsedMs();
            if (ahead >= 1)
            {
                int ms = (int)ahead;
                _sleep(ms);
                return ms;
            }
            double maxDebt = -MaxDebtSeconds * 1000.0;
            if (ahead < maxDebt) _ticks += (maxDebt - ahead) / _stepMs;
            return 0;
        }
    }
}
