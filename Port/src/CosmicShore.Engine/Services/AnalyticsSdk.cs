using System;
using System.Collections;
using System.Collections.Generic;

namespace CosmicShore.Engine.Services.Analytics
{
    /// <summary>Base analytics event (original: Unity.Services.Analytics.Event).</summary>
    public abstract class Event : IEnumerable<KeyValuePair<string, object>>
    {
        readonly Dictionary<string, object> _parameters = new();
        public string Name { get; }
        protected Event(string name) { Name = name; }
        protected void SetParameter(string key, object value) => _parameters[key] = value;
        public IReadOnlyDictionary<string, object> Parameters => _parameters;
        public object this[string key] { get => _parameters[key]; set => _parameters[key] = value; }
        public void Add(string key, object value) => _parameters[key] = value;
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _parameters.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>A schemaless custom event (original: CustomEvent).</summary>
    public class CustomEvent : Event
    {
        public CustomEvent(string name) : base(name) { }
    }

    public interface IAnalyticsService
    {
        string SessionID { get; }
        void StartDataCollection();
        void StopDataCollection();
        void RecordEvent(Event e);
        void RecordEvent(string eventName);
        void Flush();
        void RequestDataDeletion();
        string GetAnalyticsUserID();
    }

    /// <summary>
    /// Local analytics sink: recorded events are kept in <see cref="Recorded"/> while collection
    /// is on (observable, nothing leaves the process).
    /// </summary>
    public sealed class LocalAnalyticsService : IAnalyticsService
    {
        public readonly List<Event> Recorded = new();
        public bool Collecting { get; private set; }
        public string SessionID { get; } = Guid.NewGuid().ToString("N");
        public void StartDataCollection() => Collecting = true;
        public void StopDataCollection() => Collecting = false;
        public void RecordEvent(Event e) { if (Collecting && e != null) Recorded.Add(e); }
        public void RecordEvent(string eventName) => RecordEvent(new CustomEvent(eventName));
        public void Flush() { }
        public void RequestDataDeletion() => Recorded.Clear();
        public string GetAnalyticsUserID() => AuthenticationService.Instance?.PlayerId ?? "local";
    }

    public static class AnalyticsService
    {
        public static IAnalyticsService Instance { get; set; } = new LocalAnalyticsService();
    }

    public enum ConsentStatus { Unknown, Granted, Denied }

    public struct ConsentState
    {
        public ConsentStatus AnalyticsIntent;
        public ConsentStatus AdsIntent;
    }

    public static class EndUserConsent
    {
        static ConsentState s_State;
        public static ConsentState GetConsentState() => s_State;
        public static void SetConsentState(ConsentState state)
        {
            s_State = state;
            if (state.AnalyticsIntent == ConsentStatus.Granted) AnalyticsService.Instance.StartDataCollection();
            else if (state.AnalyticsIntent == ConsentStatus.Denied) AnalyticsService.Instance.StopDataCollection();
        }
    }
}
