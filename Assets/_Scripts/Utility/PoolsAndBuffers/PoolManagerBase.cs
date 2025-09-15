// File: Assets/Scripts/Core/PoolManagerBase.cs
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Minimal, key-based pooling core used by all managers.
    /// TeamColorPoolManager depends only on what lives here.
    /// </summary>
    public abstract class PoolManagerBase : MonoBehaviour
    {
        [Header("Pool Defaults")]
        [SerializeField, Min(1)] protected int _defaultInitialSize = 5;
        [SerializeField, Range(1f, 4f)] protected float _growthFactor = 2f; // batch expand factor

        /// <summary>key -> inactive objects</summary>
        protected Dictionary<string, Queue<GameObject>> _poolDictionary;
        /// <summary>key -> prefab</summary>
        protected Dictionary<string, GameObject> _prefabLookup;
        /// <summary>key -> initial size</summary>
        protected Dictionary<string, int> _initialSizes;
        /// <summary>key -> parent transform for scene hygiene</summary>
        protected Dictionary<string, Transform> _poolParents;

        #region Initialization

        /// <summary>Derived classes may call this themselves (e.g., in Awake).</summary>
        public virtual void Start() => InitializePoolDictionary();

        protected virtual void InitializePoolDictionary()
        {
            _poolDictionary = new Dictionary<string, Queue<GameObject>>();
            _prefabLookup = new Dictionary<string, GameObject>();
            _initialSizes = new Dictionary<string, int>();
            _poolParents = new Dictionary<string, Transform>();
        }

        #endregion

        #region Pool Creation & Expansion (KEY-BASED)

        /// <summary>Register and prewarm a pool for a given key.</summary>
        protected void CreateNewPool(string key, GameObject prefab, int size)
        {
            if (string.IsNullOrEmpty(key) || prefab == null)
            {
                Debug.LogError("[PoolManagerBase] Invalid pool configuration");
                return;
            }

            if (_poolDictionary.ContainsKey(key))
            {
                Debug.LogWarning($"[PoolManagerBase] Pool '{key}' already exists.");
                return;
            }

            size = Mathf.Max(1, size);

            _poolDictionary[key] = new Queue<GameObject>(size);
            _prefabLookup[key] = prefab;
            _initialSizes[key] = size;

            var parent = new GameObject($"Pool_{key}").transform;
            parent.SetParent(transform);
            _poolParents[key] = parent;

            for (int i = 0; i < size; i++)
                CreatePoolObject(key, prefab, parent);
        }

        protected virtual GameObject CreatePoolObject(string key, GameObject prefab, Transform parent)
        {
            var obj = Instantiate(prefab, parent);
            obj.SetActive(false);

            // Attach metadata so we never rely on tags
            var meta = obj.GetComponent<PooledObject>();
            if (meta == null) meta = obj.AddComponent<PooledObject>();
            meta.PoolKey = key;
            meta.Manager = this;

            _poolDictionary[key].Enqueue(obj);
            return obj;
        }

        protected virtual void ExpandPoolBatch(string key)
        {
            if (!_prefabLookup.TryGetValue(key, out var prefab))
            {
                Debug.LogError($"[PoolManagerBase] Cannot expand pool '{key}' – missing prefab.");
                return;
            }

            var parent = _poolParents[key];
            var baseSize = _initialSizes.TryGetValue(key, out var s) ? s : _defaultInitialSize;
            int addCount = Mathf.Max(1, Mathf.CeilToInt(baseSize * (_growthFactor - 1f)));

            for (int i = 0; i < addCount; i++)
                CreatePoolObject(key, prefab, parent);
        }

        #endregion

        #region Spawn & Return

        public virtual GameObject SpawnFromPool(string key, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[PoolManagerBase] SpawnFromPool key cannot be null or empty.");
                return null;
            }

            if (!_poolDictionary.ContainsKey(key))
            {
                if (_prefabLookup.TryGetValue(key, out var prefab))
                {
                    CreateNewPool(key, prefab, _initialSizes.TryGetValue(key, out var s) ? s : _defaultInitialSize);
                }
                else
                {
                    Debug.LogError($"[PoolManagerBase] No pool or prefab registered for key '{key}'.");
                    return null;
                }
            }

            if (_poolDictionary[key].Count == 0)
                ExpandPoolBatch(key);

            var obj = _poolDictionary[key].Dequeue();
            if (obj == null)
            {
                Debug.LogError($"[PoolManagerBase] Dequeued null object from '{key}'.");
                return null;
            }

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            OnObjectSpawned(obj, key);
            return obj;
        }

        /// <summary>Return via instance metadata; avoids brittle tag usage.</summary>
        public virtual void ReturnToPool(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogError("[PoolManagerBase] Cannot return null.");
                return;
            }

            var meta = obj.GetComponent<PooledObject>();
            if (meta == null || string.IsNullOrEmpty(meta.PoolKey) || !_poolDictionary.ContainsKey(meta.PoolKey))
            {
                Debug.LogError("[PoolManagerBase] Returned object missing/invalid pool metadata. Destroying.");
                Destroy(obj);
                return;
            }

            var key = meta.PoolKey;
            OnObjectReturned(obj, key);
            obj.SetActive(false);
            obj.transform.SetParent(_poolParents[key], false);
            _poolDictionary[key].Enqueue(obj);
        }

        #endregion

        #region Hooks

        protected virtual void OnObjectSpawned(GameObject obj, string key) { }
        protected virtual void OnObjectReturned(GameObject obj, string key) { }

        #endregion

        #region Cleanup

        protected virtual void OnDestroy()
        {
            if (_poolDictionary != null)
            {
                foreach (var q in _poolDictionary.Values)
                {
                    while (q.Count > 0)
                    {
                        var o = q.Dequeue();
                        if (o != null) Destroy(o);
                    }
                }
                _poolDictionary.Clear();
            }
            _prefabLookup?.Clear();
            _initialSizes?.Clear();

            if (_poolParents != null)
            {
                foreach (var t in _poolParents.Values)
                    if (t) Destroy(t.gameObject);
                _poolParents.Clear();
            }
        }

        #endregion
    }

    /// <summary>Metadata attached to each pooled instance.</summary>
    public sealed class PooledObject : MonoBehaviour
    {
        public string PoolKey { get; internal set; }
        public PoolManagerBase Manager { get; internal set; }
    }
}
