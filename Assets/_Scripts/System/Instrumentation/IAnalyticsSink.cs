using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// One analytics destination. <see cref="AnalyticsServiceFacade"/> fans every event out to
    /// all registered sinks with an identical payload, so destinations stay at parity by
    /// construction rather than by remembering to add each event twice.
    ///
    /// Consent and the COPPA age gate live UPSTREAM of this interface, in the facade. A sink
    /// therefore cannot opt out of the gate, and adding a destination cannot widen collection.
    /// </summary>
    public interface IAnalyticsSink
    {
        string Name { get; }

        /// <summary>True once the sink is able to accept events.</summary>
        bool IsCollecting { get; }

        /// <summary>Begin accepting events. Called after sign-in, consent and age checks pass.</summary>
        void StartCollection();

        /// <summary>Stop accepting events (consent revoked). Must drop, not buffer.</summary>
        void StopCollection();

        void RecordEvent(string eventName, IDictionary<string, object> parameters);

        /// <summary>
        /// Associates the current player with a stable id and updates their person-level
        /// properties. Sinks that have no person concept may ignore this.
        /// </summary>
        void Identify(string distinctId, IDictionary<string, object> personProperties);

        /// <summary>Uploads anything buffered. Called on app pause/quit.</summary>
        void Flush();

        /// <summary>
        /// Honors a right-to-erasure request as far as this sink is able. Implementations that
        /// cannot fully delete from the client MUST say so in their summary rather than
        /// silently reporting success - a deletion that did not happen is worse than no button.
        /// </summary>
        void RequestDataDeletion(string distinctId);
    }
}
