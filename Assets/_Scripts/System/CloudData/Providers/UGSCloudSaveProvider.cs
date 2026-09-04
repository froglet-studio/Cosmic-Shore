using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using CosmicShore.UI;
using CosmicShore.Utility;
using Newtonsoft.Json;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Concrete UGS implementation of ICloudSaveProvider.
    /// Dependency Inversion: consumers depend on ICloudSaveProvider, not this class.
    /// Single Responsibility: only handles serialization and UGS Cloud Save API calls.
    /// </summary>
    public class UGSCloudSaveProvider : ICloudSaveProvider
    {
        /// <summary>Backoff (ms) between save attempts after the first. 0 attempts = immediate try.</summary>
        static readonly int[] RetryBackoffMs = { 2000, 4000, 8000 };

        /// <summary>
        /// Keys currently in a failed state (online save exhausted all retries).
        /// Guards user-facing noise + observability to one episode: we toast/log/emit
        /// once on entering the failed state and once more on recovery, staying silent
        /// across background retry cycles in between.
        /// </summary>
        static readonly HashSet<string> _failedKeys = new();

        // A key failing at play exit must not suppress the next session's first failure toast;
        // AnalyticsServiceFacade's constructor subscription to OnSaveFailed has no matching -=.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _failedKeys.Clear();
            OnSaveFailed = null;
        }

        /// <summary>
        /// Raised once when a key's save exhausts all retries while the provider is
        /// available (a genuine online failure, not offline). AnalyticsServiceFacade
        /// subscribes to emit <c>cloud_save_failed</c>.
        /// </summary>
        public static event Action<string> OnSaveFailed;

        public bool IsAvailable
        {
            get
            {
                try
                {
                    return UnityServices.State == ServicesInitializationState.Initialized &&
                           AuthenticationService.Instance != null &&
                           AuthenticationService.Instance.IsSignedIn;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : class, new()
        {
            if (!IsAvailable)
            {
                Debug.LogWarning($"[UGSCloudSaveProvider] Cannot load '{key}' - not available.");
                return null;
            }

            try
            {
                var keys = new HashSet<string> { key };
                // .AsMainThread() - Docs/THREADING.md. Without it this continuation, and
                // therefore EVERYTHING awaiting the load above it, resumes on the ThreadPool.
                // `UGSDataService.InitializeAsync` then runs `SyncHangarToVessels()` there, which
                // touches SO_Vessel assets and throws EnsureRunningOnMainThread - so the boot
                // never sets IsInitialized and the auth scene waits forever.
                //
                // It hid because a load that answers from a warm cache can complete synchronously
                // and stay on the calling thread: the bug only shows on a FRESH sign-in with no
                // cached data, which is exactly "I cleared PlayerPrefs and now it hangs".
                var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys).AsMainThread();

                if (result.TryGetValue(key, out var item))
                {
                    // Primary path: the UGS SDK deserializes via Newtonsoft, so
                    // Dictionary<,> fields round-trip correctly.
                    try
                    {
                        return item.Value.GetAs<T>();
                    }
                    catch
                    {
                        // Fallback: a value stored as a JSON string (legacy JsonUtility writes).
                        // Use Newtonsoft, NOT JsonUtility - JsonUtility silently drops
                        // Dictionary<,> fields, which would wipe stats/progression on re-save.
                        var json = item.Value.GetAs<string>();
                        if (!string.IsNullOrEmpty(json))
                            return JsonConvert.DeserializeObject<T>(json);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGSCloudSaveProvider] Load '{key}' failed: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Deletes one key from this player's cloud save.
        ///
        /// <para>A key that is not there is a SUCCESS, not a failure - "make this gone" and "this
        /// was already gone" are the same outcome to the caller, and reporting the second as an
        /// error would make a full wipe complain about every key the player happened never to have
        /// written. The SDK throws for a missing key rather than returning a flag, which is why
        /// this catches rather than checks.</para>
        ///
        /// <para>No retry loop, unlike <see cref="SaveAsync"/>: a save is debounced gameplay
        /// progress that must survive a blip, while a delete is a deliberate one-off a human is
        /// watching. Failing loudly and letting them press it again is better than a silent
        /// backoff that hides which keys actually went.</para>
        /// </summary>
        public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
        {
            if (!IsAvailable) return false;

            try
            {
                await CloudSaveService.Instance.Data.Player.DeleteAsync(key).AsMainThread();
                ct.ThrowIfCancellationRequested();
                _failedKeys.Remove(key);
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (CloudSaveException e) when (e.Reason == CloudSaveExceptionReason.NotFound)
            {
                // Never existed - the caller wanted it gone, and it is. The PUBLIC exception with
                // its typed Reason, not the internal HTTP one: an internal type is not part of the
                // package's contract and can be renamed by a patch release.
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGSCloudSaveProvider] Delete '{key}' failed: {e.Message}");
                return false;
            }
        }

        public async Task<bool> SaveAsync<T>(string key, T data, CancellationToken ct = default) where T : class
        {
            if (!IsAvailable)
            {
                // Offline / not signed in - expected. Stay silent; the repository keeps
                // the data dirty and the debounce loop retries once we reconnect.
                return false;
            }

            bool alreadyFailing = _failedKeys.Contains(key);
            var payload = new Dictionary<string, object> { { key, data } };
            Exception last = null;

            for (int attempt = 0; attempt <= RetryBackoffMs.Length; attempt++)
            {
                if (attempt > 0)
                {
                    try { await Task.Delay(RetryBackoffMs[attempt - 1], ct); }
                    catch (OperationCanceledException) { return false; }
                }

                try
                {
                    await CloudSaveService.Instance.Data.Player.SaveAsync(payload).AsMainThread();

                    if (alreadyFailing)
                    {
                        _failedKeys.Remove(key);
                        Debug.Log($"[UGSCloudSaveProvider] Save '{key}' recovered.");
                    }
                    return true;
                }
                catch (Exception e)
                {
                    last = e;
                    if (!alreadyFailing)
                        Debug.LogWarning($"[UGSCloudSaveProvider] Save '{key}' attempt {attempt + 1}/{RetryBackoffMs.Length + 1} failed: {e.Message}");
                }
            }

            // All attempts failed while online. Surface once per failure episode
            // (toast/log/event); stay silent across background retry cycles.
            if (!alreadyFailing)
            {
                _failedKeys.Add(key);
                // Toast + analytics touch Unity/SOAP state - marshal to main thread.
                await MainThreadDispatcher.SwitchToMainThreadAsync();
                Debug.LogError($"[UGSCloudSaveProvider] Save '{key}' failed after {RetryBackoffMs.Length + 1} attempts: {last?.Message}");
                ToastNotificationAPI.Show($"Failed to save data: {last?.Message}");
                OnSaveFailed?.Invoke(key);
            }

            return false;
        }
    }
}
