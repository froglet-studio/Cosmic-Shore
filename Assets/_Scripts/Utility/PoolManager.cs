// DEPRECATED : USE GENERIC POOL MANAGER
// File: Assets/Scripts/Core/PoolManager.cs
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// App-agnostic pooling manager that exposes convenience utilities
    /// NOT required by TeamColorPoolManager.
    /// </summary>
    public class PoolManager : PoolManagerBase
    {
        public int GetPoolSize(string key)
        {
            if (string.IsNullOrEmpty(key) || _poolDictionary == null) return 0;
            return _poolDictionary.TryGetValue(key, out var q) ? q.Count : 0;
        }

        public int GetTotalPooledObjects()
        {
            if (_poolDictionary == null) return 0;
            int total = 0;
            foreach (var q in _poolDictionary.Values) total += q.Count;
            return total;
        }

        public bool IsPoolInitialized(string key)
            => _poolDictionary != null && _poolDictionary.ContainsKey(key);

#if UNITY_EDITOR
        [ContextMenu("Log Pool Statistics")]
        private void LogPoolStatistics()
        {
            if (_poolDictionary == null || _poolDictionary.Count == 0)
            {
                Debug.Log("[PoolManager] No pools initialized.");
                return;
            }

            Debug.Log($"=== Pool Statistics for {GetType().Name} ===");
            foreach (var kvp in _poolDictionary)
            {
                int initial = _initialSizes.TryGetValue(kvp.Key, out var size) ? size : 0;
                Debug.Log($"Pool '{kvp.Key}': {kvp.Value.Count} inactive / {initial} initial");
            }
            Debug.Log($"Total pooled objects (inactive): {GetTotalPooledObjects()}");
        }
#endif
    }
}
