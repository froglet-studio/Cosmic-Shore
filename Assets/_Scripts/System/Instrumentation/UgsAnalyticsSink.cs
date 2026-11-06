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
    /// <remarks>
    /// MigrationNote - Start/StopDataCollection are deprecated (CS0618) on Unity 6.2+, which
    /// this project is (6000.3.17f1). They are suppressed rather than migrated, deliberately:
    ///
    ///   * The replacement is the engine-level Developer Data framework
    ///     (EndUserConsent.GetConsentState / SetConsentState, ConsentState.AnalyticsIntent),
    ///     not a like-for-like method swap.
    ///   * Per the Analytics 6.1.0 changelog: "When you start using the EndUserConsent API,
    ///     the SDK will throw exceptions if you attempt to use methods from the original
    ///     workflow." So this is an all-at-once cutover - a half migration turns a compile
    ///     warning into runtime exceptions on a consent path.
    ///   * The cutover has to be reconciled with the consent/age gate that
    ///     AnalyticsServiceFacade already owns, and with RequestDataDeletion below, which on
    ///     6.2+ requires consent be denied first.
    ///
    /// Deprecated-but-functional today. Do the cutover as its own change, with in-editor
    /// verification that events still land in the UGS dashboard.
    /// </remarks>
    public sealed class UgsAnalyticsSink : IAnalyticsSink
    {
        /// <summary>
        /// What UGS must never see: every event and parameter Docs/Analytics/EVENT_SCHEMA.json marks
        /// <c>"sinks": ["froglet"]</c> (Froglet Analytics only, Shombith 2026-11-06). An entry is either
        /// an event name (the whole event is not sent to UGS) or <c>"event.parameter"</c> (that
        /// parameter is stripped from that event before UGS). UGS discards, silently, any event that
        /// carries a parameter its Event Manager does not declare, so a Froglet-only field that leaked
        /// through would take the whole UGS event with it.
        ///
        /// <para>This is the ONE list. It mirrors the schema, and
        /// <c>AnalyticsFrogletOnlyTests.UgsFilterMirrorsTheSchema</c> fails when the two differ: a new
        /// Froglet-only field is added to the schema and here in the same change.</para>
        /// </summary>
        public static readonly HashSet<string> FrogletOnlyFields = new(StringComparer.Ordinal)
        {
            UGSKeys.EventFreestyleEnded,
            UGSKeys.EventFreestyleVesselChanged,
            UGSKeys.EventGameStarted + "." + UGSKeys.ParamElement,
            UGSKeys.EventGameStarted + "." + UGSKeys.ParamElementSecondary,
            UGSKeys.EventGameCompleted + "." + UGSKeys.ParamElement,
            UGSKeys.EventGameCompleted + "." + UGSKeys.ParamElementSecondary,
            UGSKeys.EventFreestyleEntered + "." + UGSKeys.ParamVesselClass,
            UGSKeys.EventGameStarted + "." + UGSKeys.ParamDomain,
            UGSKeys.EventGameStarted + "." + UGSKeys.ParamHumansOnMyDomain,
            UGSKeys.EventGameStarted + "." + UGSKeys.ParamHumansOnOtherDomains,
            UGSKeys.EventGameStarted + "." + UGSKeys.ParamAiOnMyDomain,
            UGSKeys.EventGameStarted + "." + UGSKeys.ParamAiOnOtherDomains,
        };

        /// <summary>
        /// The payload UGS may see for one event. Returns false when the whole event is Froglet-only
        /// (send nothing). Otherwise <paramref name="ugsParameters"/> is the input itself when nothing
        /// is stripped, or a COPY without the Froglet-only parameters - the input is never modified,
        /// because the facade hands the same dictionary to every sink.
        /// </summary>
        public static bool TryFilterForUgs(string eventName, IDictionary<string, object> parameters,
            out IDictionary<string, object> ugsParameters)
        {
            ugsParameters = null;
            if (eventName == null || FrogletOnlyFields.Contains(eventName))
                return false;

            ugsParameters = parameters;
            if (parameters == null)
                return true;

            Dictionary<string, object> stripped = null;
            foreach (var key in parameters.Keys)
            {
                if (!FrogletOnlyFields.Contains(eventName + "." + key))
                    continue;

                stripped ??= new Dictionary<string, object>(parameters);
                stripped.Remove(key);
            }

            if (stripped != null)
                ugsParameters = stripped;
            return true;
        }

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
#pragma warning disable CS0618 // see MigrationNote below
                AnalyticsService.Instance.StartDataCollection();
#pragma warning restore CS0618
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
#pragma warning disable CS0618 // see MigrationNote below
                AnalyticsService.Instance.StopDataCollection();
#pragma warning restore CS0618
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

            // Froglet-only events and parameters never reach UGS - see FrogletOnlyFields.
            if (!TryFilterForUgs(eventName, parameters, out var ugsParameters))
                return;

            try
            {
                var evt = new CustomEvent(eventName);
                if (ugsParameters != null)
                    foreach (var kvp in ugsParameters)
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
        /// every event by the SDK. Person properties are a Froglet Analytics feature.
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
