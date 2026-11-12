using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Connection and batching settings for Froglet Analytics, our own analytics server
    /// (Tools/Analytics/server/, replacing PostHog on 2026-11-06). The asset lives at
    /// Resources/FrogletAnalyticsConfig so the sink loads it without scene wiring, and it ships
    /// already pointed at the server: nobody fills these fields in by hand.
    ///
    /// SECURITY: <see cref="IngestKey"/> is a write-only key that only lets a client SEND events
    /// in the game's own event schema. It is public by design (it ships in every build and sits in
    /// this public repo), the same way PostHog's project key did. The server checks every event
    /// against Docs/Analytics/EVENT_SCHEMA.json and rate-limits each sender. Reading data needs a
    /// separate read key that never ships in a build. The server reads the same key from
    /// Tools/Analytics/server/ingest_key.public, and its tests fail if the two differ.
    /// </summary>
    [CreateAssetMenu(fileName = "FrogletAnalyticsConfig", menuName = "ScriptableObjects/Analytics/Froglet Analytics Config")]
    public class FrogletAnalyticsConfigSO : ScriptableObject
    {
        [Header("Connection")]
        [Tooltip("The analytics server's address (no trailing slash). Events are posted to <this>/batch/.")]
        [FormerlySerializedAs("host")]
        [SerializeField] string serverUrl = "";

        [Tooltip("Write-only ingest key, public by design. Must equal Tools/Analytics/server/ingest_key.public.")]
        [FormerlySerializedAs("projectApiKey")]
        [SerializeField] string ingestKey = "";

        [Header("Batching")]
        [Tooltip("Upload once this many events are queued.")]
        [Min(1)]
        [SerializeField] int batchSize = 20;

        [Tooltip("Upload at least this often while events are pending (seconds).")]
        [Min(1f)]
        [SerializeField] float flushIntervalSeconds = 30f;

        [Tooltip("Hard cap on the offline queue. Oldest events are dropped past this, so a long " +
                 "offline session cannot grow unbounded on disk.")]
        [Min(50)]
        [SerializeField] int maxQueuedEvents = 500;

        [Header("Filtering")]
        [Tooltip("Event names never sent to the analytics server. UGS still receives them.")]
        [SerializeField] List<string> excludedEvents = new();

        [Header("Lifecycle")]
        [Tooltip("Master switch. Off disables the Froglet Analytics sink entirely, leaving UGS untouched.")]
        [SerializeField] bool enabled = true;

        public string ServerUrl => serverUrl?.TrimEnd('/') ?? "";
        public string IngestKey => ingestKey;
        public int BatchSize => batchSize;
        public float FlushIntervalSeconds => flushIntervalSeconds;
        public int MaxQueuedEvents => maxQueuedEvents;
        public bool Enabled => enabled;

        public bool IsConfigured =>
            enabled && !string.IsNullOrWhiteSpace(ingestKey) && !string.IsNullOrWhiteSpace(serverUrl);

        public bool IsExcluded(string eventName) =>
            excludedEvents != null && excludedEvents.Contains(eventName);
    }
}
