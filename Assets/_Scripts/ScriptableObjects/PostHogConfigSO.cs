using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Connection and batching settings for the PostHog analytics sink.
    /// Asset lives at Resources/PostHogConfig so the sink can load it without scene wiring.
    ///
    /// SECURITY: <see cref="ProjectApiKey"/> must be a PostHog *project* API key. Those are
    /// write-only by design and safe to ship in a client build. A PostHog *personal* API key
    /// is read/admin-capable and must NEVER be placed here - it is readable in any decompiled
    /// build and would be a full data breach.
    /// </summary>
    [CreateAssetMenu(fileName = "PostHogConfig", menuName = "ScriptableObjects/Analytics/PostHog Config")]
    public class PostHogConfigSO : ScriptableObject
    {
        [Header("Connection")]
        [Tooltip("PostHog PROJECT API key (write-only). Never a personal API key.")]
        [SerializeField] string projectApiKey = "";

        [Tooltip("Ingestion host. MUST match the region the PostHog project was created in, or " +
                 "events are accepted nowhere. EU cloud: https://eu.i.posthog.com - US cloud: " +
                 "https://us.i.posthog.com. This project is EU. See DATA_ARCHITECTURE.md 8.3.")]
        [SerializeField] string host = "https://eu.i.posthog.com";

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
        [Tooltip("Event names never forwarded to PostHog. The free-tier budget lever for chatty " +
                 "events (ui_action, setting_changed) - UGS still receives them, so nothing is " +
                 "lost from the system of record.")]
        [SerializeField] List<string> excludedEvents = new();

        [Header("Lifecycle")]
        [Tooltip("Master switch. Off disables the PostHog sink entirely, leaving UGS untouched.")]
        [SerializeField] bool enabled = true;

        public string ProjectApiKey => projectApiKey;
        public string Host => host?.TrimEnd('/') ?? "";
        public int BatchSize => batchSize;
        public float FlushIntervalSeconds => flushIntervalSeconds;
        public int MaxQueuedEvents => maxQueuedEvents;
        public bool Enabled => enabled;

        public bool IsConfigured =>
            enabled && !string.IsNullOrWhiteSpace(projectApiKey) && !string.IsNullOrWhiteSpace(host);

        public bool IsExcluded(string eventName) =>
            excludedEvents != null && excludedEvents.Contains(eventName);
    }
}
