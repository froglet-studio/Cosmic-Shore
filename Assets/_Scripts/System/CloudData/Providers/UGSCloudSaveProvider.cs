using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using CosmicShore.App.UI.ToastNotification;
using UnityEngine;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Concrete UGS implementation of ICloudSaveProvider.
    /// Dependency Inversion: consumers depend on ICloudSaveProvider, not this class.
    /// Single Responsibility: only handles serialization and UGS Cloud Save API calls.
    /// </summary>
    public class UGSCloudSaveProvider : ICloudSaveProvider
    {
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
                Debug.LogWarning($"[UGSCloudSaveProvider] Cannot load '{key}' — not available.");
                return null;
            }

            try
            {
                var keys = new HashSet<string> { key };
                var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (result.TryGetValue(key, out var item))
                {
                    // Try direct deserialization first (for object types)
                    try
                    {
                        return item.Value.GetAs<T>();
                    }
                    catch
                    {
                        // Fallback: try as JSON string (for types saved via JsonUtility.ToJson)
                        var json = item.Value.GetAs<string>();
                        if (!string.IsNullOrEmpty(json))
                            return JsonUtility.FromJson<T>(json);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGSCloudSaveProvider] Load '{key}' failed: {e.Message}");
            }

            return null;
        }

        public async Task SaveAsync<T>(string key, T data, CancellationToken ct = default) where T : class
        {
            if (!IsAvailable)
            {
                Debug.LogWarning($"[UGSCloudSaveProvider] Cannot save '{key}' — not available.");
                return;
            }

            try
            {
                var payload = new Dictionary<string, object> { { key, data } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGSCloudSaveProvider] Save '{key}' failed: {e.Message}");
                ToastNotificationAPI.Show($"Failed to save data: {e.Message}");
            }
        }
    }
}
