using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace CosmicShore.Core
{
    /// <summary>
    /// Froglet Analytics destination: our own analytics server (Tools/Analytics/server/), which
    /// replaced PostHog on 2026-11-06. Batches events to the server's <c>/batch/</c> endpoint,
    /// persists the queue so offline play is not lost, and identifies the player by UGS id with
    /// display name as a person property. The wire format is the one the PostHog sink used, so the
    /// server also imports PostHog's history unchanged. See Docs/Analytics/DATA_ARCHITECTURE.md.
    ///
    /// Consent and the age gate are enforced by AnalyticsServiceFacade upstream; this sink only
    /// runs between StartCollection and StopCollection.
    /// </summary>
    public sealed class FrogletAnalyticsSink : IAnalyticsSink
    {
        const string QueueFileName = "froglet_analytics_queue.json";

        /// <summary>The queue file the PostHog sink wrote; read once so events waiting on a device are sent.</summary>
        const string LegacyQueueFileName = "posthog_queue.json";

        /// <summary>PlayerPrefs key for the device-scoped install id.</summary>
        const string InstallIdPrefKey = "AnalyticsInstallId";

        /// <summary>Upload timeout. Long enough for a slow mobile link, short enough not to stall a quit.</summary>
        const int RequestTimeoutSeconds = 10;

        readonly FrogletAnalyticsConfigSO _config;
        readonly Action<string> _log;
        readonly Func<IDictionary<string, object>> _envelope;
        readonly List<AnalyticsEvent> _queue = new();

        bool _collecting;
        bool _uploadInFlight;
        bool _warnedThisEpisode;
        float _lastFlushTime;
        string _distinctId = "";

        public string Name => "Froglet Analytics";
        public bool IsCollecting => _collecting;

        /// <param name="envelope">
        /// Supplies the identity/build context UGS collects on its own but our server cannot
        /// (player id, app version, platform, schema version). It is stamped HERE rather than in
        /// the facade because every field sent to UGS costs a permanent, undeletable row in its
        /// dashboard schema.
        /// </param>
        public FrogletAnalyticsSink(FrogletAnalyticsConfigSO config, Action<string> log,
            Func<IDictionary<string, object>> envelope)
        {
            _config = config;
            _log = log;
            _envelope = envelope;
        }

        string QueuePath => Path.Combine(Application.persistentDataPath, QueueFileName);
        string LegacyQueuePath => Path.Combine(Application.persistentDataPath, LegacyQueueFileName);

        public void StartCollection()
        {
            if (_collecting)
                return;

            if (_config == null || !_config.IsConfigured)
            {
                _log?.Invoke("Froglet Analytics sink inactive - no server address or ingest key configured.");
                return;
            }

            _collecting = true;
            _lastFlushTime = Time.realtimeSinceStartup;
            LoadQueue();
            _log?.Invoke("Froglet Analytics data collection started.");
        }

        public void StopCollection()
        {
            if (!_collecting)
                return;

            // Consent revoked: drop everything pending rather than uploading it later.
            _collecting = false;
            _queue.Clear();
            DeleteQueueFiles();
            _log?.Invoke("Froglet Analytics data collection stopped; pending events discarded.");
        }

        public void RecordEvent(string eventName, IDictionary<string, object> parameters)
        {
            if (!_collecting || string.IsNullOrEmpty(eventName))
                return;

            // Chatty events can be left out of this sink without losing them - UGS still receives
            // everything. Configured per-event on FrogletAnalyticsConfigSO.
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
            // scalar. Our server can hold a real array, so expand it back here.
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
        /// Right to erasure. The client sends the deletion flag, and our server deletes every event
        /// and the person record for that id the moment the flag arrives (Store._identify), keeping
        /// only a salted hash in its erasure log. This closes the gap PostHog left, where a
        /// write-only key could only flag the person. If the flag cannot be sent (offline), the
        /// Erasure page in the analytics console deletes the player by id.
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
            _log?.Invoke("Froglet Analytics deletion requested; the server deletes the player's data on receipt.");
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

            _queue.Add(new AnalyticsEvent
            {
                @event = eventName,
                properties = properties,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture)
            });

            // Bound the queue so a long offline session cannot grow unbounded on disk.
            int overflow = _queue.Count - _config.MaxQueuedEvents;
            if (overflow > 0)
            {
                _queue.RemoveRange(0, overflow);
                Debug.LogWarning($"[Analytics] Froglet Analytics queue overflowed; dropped {overflow} oldest event(s).");
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

            var batch = new List<AnalyticsEvent>(_queue);
            _queue.Clear();

            try
            {
                string payload = JsonConvert.SerializeObject(new AnalyticsBatch
                {
                    api_key = _config.IngestKey,
                    batch = batch
                });

                using var request = new UnityWebRequest($"{_config.ServerUrl}/batch/", UnityWebRequest.kHttpVerbPOST)
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
                    _log?.Invoke($"Froglet Analytics uploaded {batch.Count} event(s).");
                }
                else if (request.responseCode == 429)
                {
                    // Rate limited: the server is fine and asks us to slow down, so keep the batch.
                    Requeue(batch, "429 rate limited");
                }
                else if (request.responseCode >= 400 && request.responseCode < 500)
                {
                    // Any other 4xx never succeeds on retry - a wrong ingest key or a malformed
                    // payload would otherwise spin forever, re-sending the same batch until the
                    // queue cap silently ate every newer event. Drop it and say so once.
                    WarnOnce($"Froglet Analytics rejected a batch ({request.responseCode}) - dropping " +
                             $"{batch.Count} event(s). Check the ingest key and server address.");
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
        void Requeue(List<AnalyticsEvent> batch, string reason)
        {
            if (!_collecting)
                return; // consent revoked mid-flight - drop it

            _queue.InsertRange(0, batch);
            int overflow = _queue.Count - _config.MaxQueuedEvents;
            if (overflow > 0)
                _queue.RemoveRange(0, overflow);

            WarnOnce($"Froglet Analytics upload failed ({reason}); {batch.Count} event(s) requeued for retry.");
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
                    DeleteQueueFiles();
                    return;
                }

                File.WriteAllText(QueuePath, JsonConvert.SerializeObject(_queue));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] Froglet Analytics queue save failed: {e.Message}");
            }
        }

        void LoadQueue()
        {
            // The PostHog sink's file first (older), then ours: both hold the same wire format.
            foreach (string path in new[] { LegacyQueuePath, QueuePath })
            {
                try
                {
                    if (!File.Exists(path))
                        continue;

                    var restored = JsonConvert.DeserializeObject<List<AnalyticsEvent>>(File.ReadAllText(path));
                    if (restored is { Count: > 0 })
                    {
                        _queue.AddRange(restored);
                        _log?.Invoke($"Froglet Analytics restored {restored.Count} queued event(s).");
                    }

                    File.Delete(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Analytics] Froglet Analytics queue load failed: {e.Message}");
                }
            }

            int overflow = _queue.Count - _config.MaxQueuedEvents;
            if (overflow > 0)
                _queue.RemoveRange(0, overflow);
        }

        void DeleteQueueFiles()
        {
            foreach (string path in new[] { QueuePath, LegacyQueuePath })
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Analytics] Froglet Analytics queue delete failed: {e.Message}");
                }
            }
        }

        [Serializable]
        class AnalyticsBatch
        {
            public string api_key;
            public List<AnalyticsEvent> batch;
        }

        [Serializable]
        class AnalyticsEvent
        {
            public string @event;
            public Dictionary<string, object> properties;
            public string timestamp;
        }
    }
}
