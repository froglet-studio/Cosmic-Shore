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

        [Tooltip("PostHog ingestion host. Use the EU host for an EU Cloud project - the region " +
                 "decides whether SCCs are needed for EEA/UK players. See DATA_ARCHITECTURE.md 8.3.")]
        [SerializeField] string host = "https://us.i.posthog.com";

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
    }
}
