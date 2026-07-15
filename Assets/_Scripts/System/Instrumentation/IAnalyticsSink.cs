using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Standard context stamped on every analytics event by <see cref="AnalyticsServiceFacade"/>
    /// and handed to each sink alongside the event's own parameters. Sinks decide what to do
    /// with it: the UGS sink ignores it (the UGS SDK auto-collects equivalents), while external
    /// sinks (PostHog) attach it as event properties. See Docs/Analytics/event-taxonomy.md §2.
    /// </summary>
    public readonly struct AnalyticsEnvelope
    {
        /// <summary>UGS PlayerId — shared key across Analytics, Cloud Save, and Leaderboards.</summary>
        public readonly string PlayerId;

        /// <summary>GUID generated once per app run.</summary>
        public readonly string SessionId;

        /// <summary>Device GUID persisted in PlayerPrefs on first run (survives sign-out, not reinstall).</summary>
        public readonly string InstallId;

        /// <summary><c>Application.version</c>.</summary>
        public readonly string BuildVersion;

        /// <summary><c>Application.platform</c>.</summary>
        public readonly string Platform;

        public AnalyticsEnvelope(string playerId, string sessionId, string installId, string buildVersion, string platform)
        {
            PlayerId = playerId;
            SessionId = sessionId;
            InstallId = installId;
            BuildVersion = buildVersion;
            Platform = platform;
        }
    }

    /// <summary>
    /// One analytics destination behind <see cref="AnalyticsServiceFacade"/>. The facade owns
    /// all gating (consent, age gate, sign-in, network); a sink only ever sees events it is
    /// allowed to transmit. Implementations: <see cref="UgsAnalyticsSink"/> (system of record),
    /// <see cref="PostHogSink"/> (cohort/funnel/SQL exploration).
    ///
    /// All methods are called on the main thread. A sink must not throw to reject an event —
    /// the facade wraps each call defensively, but a throwing sink still costs a log line per
    /// event. Swallow per-event errors internally and fail loud once.
    /// </summary>
    public interface IAnalyticsSink
    {
        /// <summary>Short display name used in facade log/warning messages.</summary>
        string Name { get; }

        /// <summary>
        /// Consent granted and prerequisites met — begin accepting events.
        /// Return false (or throw) if this sink could not start; other sinks are unaffected.
        /// </summary>
        bool StartCollection();

        /// <summary>Consent revoked — stop transmitting and discard anything not yet sent.</summary>
        void StopCollection();

        /// <summary>Record one event. Parameter values are string, int, long, float, double, or bool.</summary>
        void Record(string eventName, IDictionary<string, object> parameters, AnalyticsEnvelope envelope);

        /// <summary>Best-effort immediate upload. Called on app pause/quit only.</summary>
        void Flush();

        /// <summary>Right-to-erasure request for the current player.</summary>
        void RequestDataDeletion();
    }
}
