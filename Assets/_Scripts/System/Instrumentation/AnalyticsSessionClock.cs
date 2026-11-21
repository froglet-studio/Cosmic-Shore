using System;

namespace CosmicShore.Core
{
    /// <summary>
    /// One analytics SESSION for <c>session_started</c> / <c>session_ended</c> (Shombith 2026-11-07:
    /// sessions, DAU/WAU/MAU). A session is one sitting with the app: it begins on a cold start, and
    /// again when the app comes back from the background after more than <see cref="ResumeTimeout"/>.
    /// A shorter trip to the background (a notification, a phone call) stays in the same session.
    ///
    /// <para><see cref="ForegroundSeconds"/> counts only time in the foreground, so a phone left in a
    /// pocket for 20 minutes does not read as a 20-minute session.</para>
    ///
    /// Pure bookkeeping on the clock it is handed; no Unity calls, so the edit-mode tests drive it.
    /// </summary>
    public sealed class AnalyticsSessionClock
    {
        /// <summary>Time in the background after which coming back is a new session (the common industry 30 minutes).</summary>
        public static readonly TimeSpan ResumeTimeout = TimeSpan.FromMinutes(30);

        /// <summary><c>launch_kind</c> of a session that began with the process.</summary>
        public const string LaunchCold = "cold";

        /// <summary><c>launch_kind</c> of a session that began on a return from the background.</summary>
        public const string LaunchResume = "resume";

        DateTime _foregroundSinceUtc;
        double _foregroundSeconds;
        DateTime? _backgroundSinceUtc;

        /// <summary>A fresh GUID ("N" format) per session.</summary>
        public string SessionId { get; private set; }

        /// <summary><see cref="LaunchCold"/> or <see cref="LaunchResume"/>.</summary>
        public string LaunchKind { get; private set; }

        /// <summary>Whether this session's <c>session_started</c> has been sent (it waits for consent and sign-in).</summary>
        public bool StartRecorded { get; set; }

        public AnalyticsSessionClock(DateTime nowUtc) => Begin(nowUtc, LaunchCold);

        /// <summary>The app went to the background (or is quitting). Idempotent.</summary>
        public void EnterBackground(DateTime nowUtc)
        {
            if (_backgroundSinceUtc.HasValue)
                return;

            _foregroundSeconds += Math.Max(0d, (nowUtc - _foregroundSinceUtc).TotalSeconds);
            _backgroundSinceUtc = nowUtc;
        }

        /// <summary>
        /// The app came back. Returns true when that began a NEW session (it was away longer than
        /// <see cref="ResumeTimeout"/>); false when the same session goes on, or when it was never away.
        /// </summary>
        public bool ReturnToForeground(DateTime nowUtc)
        {
            if (!_backgroundSinceUtc.HasValue)
                return false;

            var away = nowUtc - _backgroundSinceUtc.Value;
            _backgroundSinceUtc = null;

            if (away > ResumeTimeout)
            {
                Begin(nowUtc, LaunchResume);
                return true;
            }

            _foregroundSinceUtc = nowUtc;
            return false;
        }

        /// <summary>Seconds in the foreground in this session up to <paramref name="nowUtc"/>.</summary>
        public double ForegroundSeconds(DateTime nowUtc) =>
            _foregroundSeconds + (_backgroundSinceUtc.HasValue
                ? 0d
                : Math.Max(0d, (nowUtc - _foregroundSinceUtc).TotalSeconds));

        void Begin(DateTime nowUtc, string launchKind)
        {
            SessionId = Guid.NewGuid().ToString("N");
            LaunchKind = launchKind;
            StartRecorded = false;
            _foregroundSinceUtc = nowUtc;
            _foregroundSeconds = 0d;
            _backgroundSinceUtc = null;
        }
    }
}
