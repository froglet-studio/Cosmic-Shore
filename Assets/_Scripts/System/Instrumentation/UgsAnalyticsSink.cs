using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// UGS Analytics destination - the behaviour the facade used to own inline.
    ///
    /// Every event AND every parameter must also be declared in the UGS dashboard Event
    /// Manager or the backend silently discards it. Docs/Analytics/EVENT_SCHEMA.json is the
    /// generated source for that configuration.
    /// </summary>
    public sealed class UgsAnalyticsSink : IAnalyticsSink
    {
        readonly Action<string> _log;

        bool _collecting;

        public string Name => "UGS";
        public bool IsCollecting => _collecting;

        public UgsAnalyticsSink(Action<string> log) => _log = log;

        public void StartCollection()
        {
            if (_collecting)
                return;
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return;

            try
            {
                // No ExternalUserId override: with UGS auth active, events carry the UGS
                // player id - the same key as Cloud Save and Leaderboards.
                AnalyticsService.Instance.StartDataCollection();
                _collecting = true;
                _log?.Invoke("UGS data collection started.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] UGS StartDataCollection failed: {e.Message}");
            }
        }

        public void StopCollection()
        {
            if (!_collecting)
                return;

            try
            {
                AnalyticsService.Instance.StopDataCollection();
                _collecting = false;
                _log?.Invoke("UGS data collection stopped.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] UGS StopDataCollection failed: {e.Message}");
            }
        }

        public void RecordEvent(string eventName, IDictionary<string, object> parameters)
        {
            if (!_collecting)
                return;

            try
            {
                var evt = new CustomEvent(eventName);
                if (parameters != null)
                    foreach (var kvp in parameters)
                        evt.Add(kvp.Key, kvp.Value);

                AnalyticsService.Instance.RecordEvent(evt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] UGS failed to record '{eventName}': {e.Message}");
            }
        }

        /// <summary>
        /// No-op: UGS has no person-property concept, and the player id is already attached to
        /// every event by the SDK. Person properties are a PostHog feature.
        /// </summary>
        public void Identify(string distinctId, IDictionary<string, object> personProperties) { }

        public void Flush()
        {
            if (!_collecting)
                return;

            try
            {
                AnalyticsService.Instance.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] UGS flush failed: {e.Message}");
            }
        }

        public void RequestDataDeletion(string distinctId)
        {
            try
            {
                AnalyticsService.Instance.RequestDataDeletion();
                _log?.Invoke("UGS data deletion requested.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] UGS RequestDataDeletion failed: {e.Message}");
            }
        }
    }
}
