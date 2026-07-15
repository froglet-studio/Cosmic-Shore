using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Connection + batching config for the PostHog analytics sink
    /// (<see cref="CosmicShore.Core.PostHogSink"/>). One asset lives at
    /// <c>Assets/Resources/PostHogConfig.asset</c> and is loaded by name from
    /// <see cref="CosmicShore.Core.AnalyticsServiceFacade"/> — no scene wiring needed.
    ///
    /// Leaving <see cref="projectApiKey"/> empty disables the sink entirely (UGS is
    /// unaffected), so the asset is safe to ship unconfigured. Setup guide:
    /// Docs/Analytics/POSTHOG_SETUP.md.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PostHogConfig",
        menuName = "ScriptableObjects/Analytics/PostHog Config")]
    public class PostHogConfigSO : ScriptableObject
    {
        [Header("Connection")]
        [Tooltip("PostHog → Settings → Project → 'Project API key' (starts with phc_). This is the public write-only key — safe to ship in the client. Leave EMPTY to disable the PostHog sink.")]
        [SerializeField] string projectApiKey = "";

        [Tooltip("Ingestion host. EU cloud: https://eu.i.posthog.com — US cloud: https://us.i.posthog.com. Must match the region the PostHog project was created in.")]
        [SerializeField] string host = "https://eu.i.posthog.com";

        [Header("Batching")]
        [Tooltip("Send queued events as soon as this many have accumulated.")]
        [SerializeField] int maxBatchSize = 30;

        [Tooltip("Also send on the first event recorded after this many seconds since the last successful send.")]
        [SerializeField] float flushIntervalSeconds = 30f;

        [Tooltip("Hard cap on locally queued events while offline or failing. Oldest events are dropped beyond this.")]
        [SerializeField] int maxQueueSize = 500;

        [Header("Filtering")]
        [Tooltip("Event names never forwarded to PostHog — the free-tier budget lever for chatty events (e.g. ui_action). UGS still receives them.")]
        [SerializeField] List<string> excludedEvents = new();

        public string ProjectApiKey => projectApiKey == null ? string.Empty : projectApiKey.Trim();
        public string Host => string.IsNullOrWhiteSpace(host) ? "https://eu.i.posthog.com" : host.Trim().TrimEnd('/');
        public int MaxBatchSize => Mathf.Max(1, maxBatchSize);
        public float FlushIntervalSeconds => Mathf.Max(1f, flushIntervalSeconds);
        public int MaxQueueSize => Mathf.Max(MaxBatchSize, maxQueueSize);

        /// <summary>True when the sink should be created at all.</summary>
        public bool IsUsable => !string.IsNullOrEmpty(ProjectApiKey);

        public bool IsExcluded(string eventName) =>
            excludedEvents != null && excludedEvents.Contains(eventName);
    }
}
