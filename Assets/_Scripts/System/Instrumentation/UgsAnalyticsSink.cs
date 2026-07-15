using System;
using System.Collections.Generic;
using Unity.Services.Analytics;

namespace CosmicShore.Core
{
    /// <summary>
    /// UGS Analytics destination — the system of record. Wraps the
    /// <see cref="AnalyticsService"/> SDK calls that used to live directly in
    /// <see cref="AnalyticsServiceFacade"/>.
    ///
    /// The <see cref="AnalyticsEnvelope"/> is deliberately NOT attached here: the UGS SDK
    /// auto-collects user ID, session ID, platform, and client version on every event, and
    /// every custom parameter would additionally need declaring in the dashboard Event
    /// Manager (undeclared parameters invalidate the whole event).
    /// </summary>
    public class UgsAnalyticsSink : IAnalyticsSink
    {
        readonly Action<string> _log;

        public string Name => "UGS";

        public UgsAnalyticsSink(Action<string> log)
        {
            _log = log;
        }

        public bool StartCollection()
        {
            // Throws on failure — the facade logs it and other sinks keep running.
            AnalyticsService.Instance.StartDataCollection();
            return true;
        }

        public void StopCollection() => AnalyticsService.Instance.StopDataCollection();

        public void Record(string eventName, IDictionary<string, object> parameters, AnalyticsEnvelope envelope)
        {
            var evt = new CustomEvent(eventName);
            if (parameters != null)
                foreach (var kvp in parameters)
                    evt.Add(kvp.Key, kvp.Value);

            AnalyticsService.Instance.RecordEvent(evt);
        }

        // The SDK batches and uploads on its own cadence; explicit flush is reserved for
        // pause/quit, where the process may die before the next scheduled upload.
        public void Flush() => AnalyticsService.Instance.Flush();

        public void RequestDataDeletion()
        {
            AnalyticsService.Instance.RequestDataDeletion();
            _log?.Invoke("UGS data deletion requested.");
        }
    }
}
