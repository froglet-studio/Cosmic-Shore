using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Utility;
using Newtonsoft.Json;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Last-known-good LOCAL snapshot of every cloud-save key, so the player always gets
    /// their basic details - display name, unlocked vessels, unlocked episodes, game
    /// progression, settings - even when UGS is unreachable (offline launch, Steam
    /// offline mode, service outage).
    ///
    /// <para>
    /// This is a READ FALLBACK plus a write-through mirror, not a second source of truth:
    /// <see cref="CloudDataRepository{T}"/> writes a snapshot on every successful cloud
    /// load and on every save attempt, and only reads one back when the cloud returns
    /// nothing. When online, cloud data always wins. A cloud-side
    /// <c>ResetAsync</c> overwrites the snapshot too (its save path routes through here),
    /// so a reset account cannot resurrect old data from disk.
    /// </para>
    ///
    /// <para>
    /// Serialization is Newtonsoft.Json - the same serializer the UGS CloudSave SDK uses -
    /// so a payload that round-trips the cloud round-trips this cache identically.
    /// Files live at <c>{persistentDataPath}/CloudCache/{key}.json</c>. The root path is
    /// captured once on the main thread at startup because repository load/save
    /// continuations can run on the ThreadPool, where <see cref="Application.persistentDataPath"/>
    /// must not be touched. All IO is wrapped: a corrupt or unwritable cache degrades to
    /// "no snapshot", never to an exception in the data pipeline.
    /// </para>
    /// </summary>
    public static class LocalCloudDataCache
    {
        static string _rootPath;
        static readonly object _ioLock = new();
        static readonly HashSet<string> _warnedKeys = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CaptureRootPath()
        {
            try
            {
                _rootPath = Path.Combine(Application.persistentDataPath, "CloudCache");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalCloudDataCache] Could not resolve persistentDataPath: {e.Message}. Local cloud snapshots disabled.");
            }
        }

        /// <summary>True when the cache has a usable root path (captured at startup).</summary>
        public static bool IsAvailable => !string.IsNullOrEmpty(_rootPath);

        /// <summary>
        /// Writes the current in-memory payload for <paramref name="key"/> to disk as the
        /// last-known-good snapshot. Silent no-op on failure (warn once per key) - the
        /// cache must never be able to break the save pipeline it mirrors.
        /// </summary>
        public static void Save<T>(string key, T data) where T : class
        {
            if (!IsAvailable || data == null || string.IsNullOrEmpty(key)) return;

            try
            {
                string json = JsonConvert.SerializeObject(data);
                lock (_ioLock)
                {
                    Directory.CreateDirectory(_rootPath);
                    File.WriteAllText(PathFor(key), json);
                }
            }
            catch (Exception e)
            {
                WarnOnce(key, $"snapshot write failed: {e.Message}");
            }
        }

        /// <summary>
        /// Loads the last-known-good snapshot for <paramref name="key"/>, or null when no
        /// snapshot exists or it cannot be read. Callers treat null exactly like a missing
        /// cloud key - fresh defaults.
        /// </summary>
        public static T TryLoad<T>(string key) where T : class, new()
        {
            if (!IsAvailable || string.IsNullOrEmpty(key)) return null;

            try
            {
                string path = PathFor(key);
                string json;
                lock (_ioLock)
                {
                    if (!File.Exists(path)) return null;
                    json = File.ReadAllText(path);
                }

                if (string.IsNullOrEmpty(json)) return null;

                var data = JsonConvert.DeserializeObject<T>(json);
                if (data != null)
                    CSDebug.Log($"[LocalCloudDataCache] Restored '{key}' from local snapshot.");
                return data;
            }
            catch (Exception e)
            {
                WarnOnce(key, $"snapshot read failed: {e.Message}");
                return null;
            }
        }

        static string PathFor(string key)
        {
            // Cloud keys are plain identifiers (e.g. "PLAYER_PROFILE"); sanitize defensively
            // so a future key can never escape the cache directory.
            foreach (char c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return Path.Combine(_rootPath, key + ".json");
        }

        static void WarnOnce(string key, string message)
        {
            lock (_ioLock)
            {
                if (!_warnedKeys.Add(key)) return;
            }
            Debug.LogWarning($"[LocalCloudDataCache] '{key}' {message}");
        }
    }
}
