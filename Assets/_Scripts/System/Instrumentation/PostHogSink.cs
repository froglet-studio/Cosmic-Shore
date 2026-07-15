using System;
using System.Collections.Generic;
using System.Text;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace CosmicShore.Core
{
    /// <summary>
    /// PostHog destination for cohort/funnel/SQL analysis — a thin client over PostHog's
    /// documented <c>/batch/</c> capture endpoint (POST-only, project API key in the body,
    /// no rate limits on capture). Deliberately not the PostHog Unity SDK: this is ~150
    /// lines we fully own, works on every platform Unity's networking works on, and adds
    /// zero package dependencies. Swapping it for the official SDK later is contained to
    /// this one class behind <see cref="IAnalyticsSink"/>.
    ///
    /// Identity: <c>distinct_id</c> is the UGS PlayerId (falls back to the install GUID
    /// pre-sign-in, which cannot happen in practice — the facade only collects after
    /// sign-in), so PostHog rows join UGS Analytics / Cloud Save rows directly.
    ///
    /// Delivery model: events queue in memory and send when the batch size or the flush
    /// interval is reached, plus on app pause/quit via <see cref="Flush"/>. A failed send
    /// re-queues once at the front (bounded by MaxQueueSize) and retries on the next
    /// trigger; HTTP 4xx responses drop the batch (a bad key or malformed payload never
    /// succeeds by retrying). Queue is memory-only — events from a hard crash are lost,
    /// which is acceptable for aggregate analytics (the mobile pause flush is the main
    /// path). All calls are main-thread (facade contract).
    /// </summary>
    public class PostHogSink : IAnalyticsSink
    {
        readonly PostHogConfigSO _config;
        readonly Action<string> _log;
        readonly List<Dictionary<string, object>> _queue = new();

        float _lastSendRealtime;
        bool _sending;
        bool _warnedThisEpisode;

        public string Name => "PostHog";

        public PostHogSink(PostHogConfigSO config, Action<string> log)
        {
            _config = config;
            _log = log;
        }

        public bool StartCollection()
        {
            _lastSendRealtime = Time.realtimeSinceStartup;
            _log?.Invoke($"PostHog sink active ({_config.Host}).");
            return true;
        }

        public void StopCollection()
        {
            // Consent revoked: nothing queued may be transmitted afterwards.
            _queue.Clear();
        }

        public void Record(string eventName, IDictionary<string, object> parameters, AnalyticsEnvelope envelope)
        {
            if (_config.IsExcluded(eventName))
                return;

            var properties = new Dictionary<string, object>
            {
                ["distinct_id"] = string.IsNullOrEmpty(envelope.PlayerId) ? envelope.InstallId : envelope.PlayerId,
                ["session_id"] = envelope.SessionId,
                ["install_id"] = envelope.InstallId,
                ["build_version"] = envelope.BuildVersion,
                ["platform"] = envelope.Platform
            };
            if (parameters != null)
                foreach (var kvp in parameters)
                    properties[kvp.Key] = kvp.Value;

            if (_queue.Count >= _config.MaxQueueSize)
                _queue.RemoveAt(0);

            _queue.Add(new Dictionary<string, object>
            {
                ["event"] = eventName,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["properties"] = properties
            });

            if (_queue.Count >= _config.MaxBatchSize ||
                Time.realtimeSinceStartup - _lastSendRealtime >= _config.FlushIntervalSeconds)
                Flush();
        }

        public void Flush()
        {
            if (_sending || _queue.Count == 0)
                return;

            SendQueuedAsync().Forget();
        }

        public void RequestDataDeletion()
        {
            // The client only holds the public write-only project key; PostHog person
            // deletion needs a private key and is an ops step (dashboard: Person →
            // "Delete person" with events). Runbook: Docs/Analytics/POSTHOG_SETUP.md.
            Debug.Log("[Analytics] PostHog deletion is completed in the PostHog dashboard — see Docs/Analytics/POSTHOG_SETUP.md (Deletion runbook).");
        }

        async UniTaskVoid SendQueuedAsync()
        {
            _sending = true;
            try
            {
                while (_queue.Count > 0)
                {
                    int count = Mathf.Min(_queue.Count, _config.MaxBatchSize);
                    var batch = _queue.GetRange(0, count);
                    _queue.RemoveRange(0, count);

                    bool settled = await PostBatchAsync(batch);
                    if (!settled)
                    {
                        // Transient failure: re-queue at the front (bounded) and stop.
                        // The next Record/Flush trigger retries.
                        _queue.InsertRange(0, batch);
                        if (_queue.Count > _config.MaxQueueSize)
                            _queue.RemoveRange(_config.MaxQueueSize, _queue.Count - _config.MaxQueueSize);
                        return;
                    }

                    _lastSendRealtime = Time.realtimeSinceStartup;
                    _warnedThisEpisode = false;
                }
            }
            finally
            {
                _sending = false;
            }
        }

        /// <summary>
        /// Returns true when the batch is settled (delivered, or permanently rejected and
        /// dropped); false when it should be retried later.
        /// </summary>
        async UniTask<bool> PostBatchAsync(List<Dictionary<string, object>> batch)
        {
            string json;
            try
            {
                json = JsonConvert.SerializeObject(new Dictionary<string, object>
                {
                    ["api_key"] = _config.ProjectApiKey,
                    ["batch"] = batch
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] PostHog payload serialization failed, dropping {batch.Count} event(s): {e.Message}");
                return true;
            }

            using var request = new UnityWebRequest($"{_config.Host}/batch/", UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            try
            {
                await request.SendWebRequest();
            }
            catch (UnityWebRequestException)
            {
                // Error results are inspected below; the awaiter throwing is expected there.
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                _log?.Invoke($"PostHog: sent {batch.Count} event(s).");
                return true;
            }

            // 4xx never succeeds on retry (bad key, malformed payload) — drop the batch.
            bool permanentlyRejected = request.responseCode >= 400 && request.responseCode < 500;
            if (!_warnedThisEpisode)
            {
                _warnedThisEpisode = true;
                Debug.LogWarning($"[Analytics] PostHog send failed ({request.responseCode} {request.result})" +
                                 (permanentlyRejected ? " — dropping batch." : " — will retry."));
            }
            return permanentlyRejected;
        }
    }
}
