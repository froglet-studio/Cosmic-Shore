using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace CosmicShore.Core
{
    /// <summary>
    /// PostHog destination. Batches events to the public capture endpoint, persists the queue
    /// so offline play is not lost, and identifies the player by UGS id with display name as a
    /// searchable person property. See Docs/Analytics/DATA_ARCHITECTURE.md §7.3.
    ///
    /// Consent and the age gate are enforced by AnalyticsServiceFacade upstream; this sink only
    /// runs between StartCollection and StopCollection.
    /// </summary>
    public sealed class PostHogAnalyticsSink : IAnalyticsSink
    {
        const string QueueFileName = "posthog_queue.json";

        /// <summary>PlayerPrefs key for the device-scoped install id.</summary>
        const string InstallIdPrefKey = "AnalyticsInstallId";

        /// <summary>Upload timeout. Long enough for a slow mobile link, short enough not to stall a quit.</summary>
        const int RequestTimeoutSeconds = 10;

        readonly PostHogConfigSO _config;
        readonly Action<string> _log;
        readonly Func<IDictionary<string, object>> _envelope;
        readonly List<PostHogEvent> _queue = new();

        bool _collecting;
        bool _uploadInFlight;
        bool _warnedThisEpisode;
        float _lastFlushTime;
        string _distinctId = "";

        public string Name => "PostHog";
        public bool IsCollecting => _collecting;

        /// <param name="envelope">
        /// Supplies the identity/build context PostHog cannot collect on its own (player id,
        /// app version, platform, schema version). It is stamped HERE rather than in the
        /// facade because UGS auto-collects the equivalents, and every field sent to UGS costs
        /// a permanent, undeletable row in its dashboard schema.
        /// </param>
        public PostHogAnalyticsSink(PostHogConfigSO config, Action<string> log,
            Func<IDictionary<string, object>> envelope)
        {
            _config = config;
            _log = log;
            _envelope = envelope;
        }

        string QueuePath => Path.Combine(Application.persistentDataPath, QueueFileName);

        public void StartCollection()
        {
            if (_collecting)
                return;

            if (_config == null || !_config.IsConfigured)
            {
                _log?.Invoke("PostHog sink inactive - no project key/host configured.");
                return;
            }

            _collecting = true;
            _lastFlushTime = Time.realtimeSinceStartup;
            LoadQueue();
            _log?.Invoke("PostHog data collection started.");
        }

        public void StopCollection()
        {
            if (!_collecting)
                return;

            // Consent revoked: drop everything pending rather than uploading it later.
            _collecting = false;
            _queue.Clear();
            DeleteQueueFile();
            _log?.Invoke("PostHog data collection stopped; pending events discarded.");
        }

        public void RecordEvent(string eventName, IDictionary<string, object> parameters)
        {
            if (!_collecting || string.IsNullOrEmpty(eventName))
                return;

            // Budget lever: chatty events (ui_action, setting_changed) can be dropped from
            // PostHog without losing them - UGS remains the system of record and still
            // receives everything. Configured per-event on PostHogConfigSO.
            if (_config.IsExcluded(eventName))
                return;

            var properties = new Dictionary<string, object>();

            var envelope = _envelope?.Invoke();
            if (envelope != null)
                foreach (var kvp in envelope)
                    properties[kvp.Key] = kvp.Value;

            // Event parameters win over the envelope: game_completed carries its own
            // client-stamped completion timestamp, and that is the meaningful one.
            if (parameters != null)
                foreach (var kvp in parameters)
                    properties[kvp.Key] = kvp.Value;

            // player_ids travels as a comma-joined string because UGS parameters must be
            // scalar. PostHog can hold a real array, so expand it back here.
            if (properties.TryGetValue("player_ids", out var joined) && joined is string s)
                properties["player_ids"] = string.IsNullOrEmpty(s)
                    ? Array.Empty<string>()
                    : s.Split(',');

            Enqueue(eventName, properties);
        }

        public void Identify(string distinctId, IDictionary<string, object> personProperties)
        {
            if (!_collecting || string.IsNullOrEmpty(distinctId))
                return;

            _distinctId = distinctId;

            var properties = new Dictionary<string, object>();
            if (personProperties != null)
                properties["$set"] = new Dictionary<string, object>(personProperties);

            Enqueue("$identify", properties);
        }

        public void Flush()
        {
            if (!_collecting || _queue.Count == 0)
                return;

            UploadAsync().Forget();
        }

        /// <summary>
        /// PARTIAL erasure, deliberately surfaced rather than hidden. Deleting a PostHog person
        /// requires a personal (admin) API key, which must never ship in a client build, so the
        /// client cannot complete the deletion itself. What it CAN do is stop collecting, drop
        /// anything pending, and flag the person so an operator or server-side automation
        /// finishes the job.
        ///
        /// Until that automation exists, a right-to-erasure request is NOT fully honored by
        /// pressing the in-game button. Tracked as a blocking item in
        /// Docs/Analytics/DATA_ARCHITECTURE.md §8.6.
        /// </summary>
        public void RequestDataDeletion(string distinctId)
        {
            if (!_collecting)
                return;

            string target = string.IsNullOrEmpty(distinctId) ? _distinctId : distinctId;
            if (string.IsNullOrEmpty(target))
                return;

            Enqueue("$identify", new Dictionary<string, object>
            {
                ["$set"] = new Dictionary<string, object>
                {
                    ["gdpr_deletion_requested"] = true,
                    ["gdpr_deletion_requested_utc_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            }, overrideDistinctId: target);

            // Push the flag out before collection stops, otherwise StopCollection discards it.
            UploadAsync().Forget();
            Debug.LogWarning("[Analytics] PostHog person flagged for deletion. Server-side deletion " +
                             "must still be run - the client cannot delete a person with a write-only key.");
        }

        void Enqueue(string eventName, Dictionary<string, object> properties, string overrideDistinctId = null)
        {
            string distinct = overrideDistinctId ?? _distinctId;
            if (string.IsNullOrEmpty(distinct))
                return; // no identity yet - the event would be unattributable

            properties["distinct_id"] = distinct;

            // Device-scoped id that survives sign-out (not reinstall). Lets a single device's
            // activity be followed across accounts, which distinct_id alone cannot express.
            properties["install_id"] = InstallId;

            _queue.Add(new PostHogEvent
            {
                @event = eventName,
                properties = properties,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            });

            // Bound the queue so a long offline session cannot grow unbounded on disk.
            int overflow = _queue.Count - _config.MaxQueuedEvents;
            if (overflow > 0)
            {
                _queue.RemoveRange(0, overflow);
                Debug.LogWarning($"[Analytics] PostHog queue overflowed; dropped {overflow} oldest event(s).");
            }

            bool sizeReached = _queue.Count >= _config.BatchSize;
            bool intervalElapsed = Time.realtimeSinceStartup - _lastFlushTime >= _config.FlushIntervalSeconds;

            if (sizeReached || intervalElapsed)
                UploadAsync().Forget();
        }

        async UniTaskVoid UploadAsync()
        {
            if (_uploadInFlight || _queue.Count == 0)
                return;

            _uploadInFlight = true;
            _lastFlushTime = Time.realtimeSinceStartup;

            var batch = new List<PostHogEvent>(_queue);
            _queue.Clear();

            try
            {
                string payload = JsonConvert.SerializeObject(new PostHogBatch
                {
                    api_key = _config.ProjectApiKey,
                    batch = batch
                });

                using var request = new UnityWebRequest($"{_config.Host}/batch/", UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = RequestTimeoutSeconds
                };
                request.SetRequestHeader("Content-Type", "application/json");

                try
                {
                    await request.SendWebRequest().ToUniTask();
                }
                catch (UnityWebRequestException)
                {
                    // The awaiter throws on any non-success result; the result is inspected below.
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    _warnedThisEpisode = false;
                    _log?.Invoke($"PostHog uploaded {batch.Count} event(s).");
                }
                else if (request.responseCode >= 400 && request.responseCode < 500)
                {
                    // 4xx never succeeds on retry - a bad project key or a malformed payload
                    // would otherwise spin forever, re-sending the same batch until the queue
                    // cap silently ate every newer event. Drop it and say so once.
                    WarnOnce($"PostHog rejected a batch ({request.responseCode}) - dropping " +
                             $"{batch.Count} event(s). Check the project API key and host region.");
                }
                else
                {
                    Requeue(batch, $"{request.responseCode} {request.result}");
                }
            }
            catch (Exception e)
            {
                Requeue(batch, e.Message);
            }
            finally
            {
                _uploadInFlight = false;
            }
        }

        /// <summary>Puts a failed batch back at the FRONT so ordering survives a retry.</summary>
        void Requeue(List<PostHogEvent> batch, string reason)
        {
            if (!_collecting)
                return; // consent revoked mid-flight - drop it

            _queue.InsertRange(0, batch);
            int overflow = _queue.Count - _config.MaxQueuedEvents;
            if (overflow > 0)
                _queue.RemoveRange(0, overflow);

            WarnOnce($"PostHog upload failed ({reason}); {batch.Count} event(s) requeued for retry.");
        }

        /// <summary>
        /// Logs once per failure episode. Without this a sustained outage produces one warning
        /// per batch, which buries everything else in the console.
        /// </summary>
        void WarnOnce(string message)
        {
            if (_warnedThisEpisode)
                return;

            _warnedThisEpisode = true;
            CSDebug.LogWarning($"[Analytics] {message}");
        }

        /// <summary>
        /// Device-scoped GUID, minted once and kept in PlayerPrefs. Deliberately device-local
        /// (not Cloud Save): it must survive sign-out, and it must NOT roam to another device.
        /// </summary>
        static string InstallId
        {
            get
            {
                string id = PlayerPrefs.GetString(InstallIdPrefKey, string.Empty);
                if (!string.IsNullOrEmpty(id))
                    return id;

                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(InstallIdPrefKey, id);
                PlayerPrefs.Save();
                return id;
            }
        }

        /// <summary>Persists the pending queue so a process death does not lose offline events.</summary>
        public void SaveQueue()
        {
            if (!_collecting)
                return;

            try
            {
                if (_queue.Count == 0)
                {
                    DeleteQueueFile();
                    return;
                }

                File.WriteAllText(QueuePath, JsonConvert.SerializeObject(_queue));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] PostHog queue save failed: {e.Message}");
            }
        }

        void LoadQueue()
        {
            try
            {
                if (!File.Exists(QueuePath))
                    return;

                var restored = JsonConvert.DeserializeObject<List<PostHogEvent>>(File.ReadAllText(QueuePath));
                if (restored is { Count: > 0 })
                {
                    _queue.InsertRange(0, restored);
                    _log?.Invoke($"PostHog restored {restored.Count} queued event(s).");
                }

                DeleteQueueFile();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] PostHog queue load failed: {e.Message}");
            }
        }

        void DeleteQueueFile()
        {
            try
            {
                if (File.Exists(QueuePath))
                    File.Delete(QueuePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] PostHog queue delete failed: {e.Message}");
            }
        }

        [Serializable]
        class PostHogBatch
        {
            public string api_key;
            public List<PostHogEvent> batch;
        }

        [Serializable]
        class PostHogEvent
        {
            public string @event;
            public Dictionary<string, object> properties;
            public string timestamp;
        }
    }
}
