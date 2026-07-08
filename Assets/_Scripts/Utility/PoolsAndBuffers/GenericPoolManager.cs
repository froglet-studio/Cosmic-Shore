using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Utility
{
    public abstract class GenericPoolManager<T> : MonoBehaviour where T : Component
    {
        [Header("Pool Settings")]
        [SerializeField] private T prefab;
        [SerializeField] private int defaultCapacity = 10;
        [SerializeField] private int maxSize = 100;

        [Header("Buffer Maintenance (Optional)")]
        [SerializeField] private bool enableBufferMaintenance = true;
        [SerializeField] private int bufferSizeTarget = 20;
        [SerializeField] private float maxInstantiateRate = 20f;
        [SerializeField] private float baseInstantiateRate = 5f;
        [SerializeField] private int maxAddsPerFrame = 4;

        // [Optimization] Track active objects to avoid FindObjectsOfType during Reset
        private readonly HashSet<T> _activeObjects = new HashSet<T>();
        
        private ObjectPool<T> pool;
        private CancellationTokenSource maintenanceCts;
        private float instantiateTimer;

        // Per-pool attribution for mid-game buffer refills — conserved prisms make
        // pool consumption one-way, so refills instantiate for the whole session;
        // this marker is what names the pool (and the typical unit cost) when a
        // refill shows up in a capture next to a GC.Collect.
        private ProfilerMarker _refillMarker;

        protected virtual void Awake()
        {
            _refillMarker = new ProfilerMarker($"PoolRefill.{(prefab ? prefab.name : GetType().Name)}");
            pool = new ObjectPool<T>(
                CreateFunc,
                OnGetFromPool,
                OnReleaseToPool,
                OnDestroyPoolObject,
                collectionCheck: false,
                defaultCapacity,
                maxSize
            );

            if (defaultCapacity > 0)
                Prewarm(Mathf.Max(defaultCapacity, bufferSizeTarget));

            if (enableBufferMaintenance)
            {
                maintenanceCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);
                BufferMaintenanceAsync(maintenanceCts.Token).Forget();
            }
        }

        protected virtual void OnDisable() => CancelMaintenance();
        protected virtual void OnDestroy() => CancelMaintenance();

        private void CancelMaintenance()
        {
            if (maintenanceCts != null)
            {
                maintenanceCts.Cancel();
                maintenanceCts.Dispose();
                maintenanceCts = null;
            }
        }

        // ---------------- Public API ----------------

        /// <summary>The prefab this pool instantiates. Read-only — lets subclasses key registries by prefab.</summary>
        public T Prefab => prefab;

        public abstract T Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true);
        public abstract void Release(T instance);

        protected T Get_(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            // A pooled instance can be destroyed out from under the pool: a double
            // release (collectionCheck is off) pushes the same object twice, then the
            // maxSize-overflow path Destroys it while a stale stack entry remains;
            // scene teardown can do the same. Never hand back a dead object — drain
            // any dead stack entries and pull/create a live one. Without this a single
            // corrupted entry surfaced as an NRE per caller (the prism-explosion
            // exception storm that tanked dense modes like Joust intensity 3) and,
            // once the callers were null-guarded, as silently-skipped VFX (a death
            // that fails to animate — a continuity-of-existence violation).
            var instance = pool.Get();
            for (int guard = 0; !instance && guard < 16; guard++)
                instance = pool.Get();
            if (!instance) return default;

            // [Optimization] Add to tracking set
            _activeObjects.Add(instance);

            instance.transform.SetPositionAndRotation(position, rotation);
            if (parent) instance.transform.SetParent(parent, worldPositionStays);
            
            return instance;
        }

        protected void Release_(T instance)
        {
            if (!instance) return;
            
            // [Optimization] Remove from tracking set
            if (_activeObjects.Contains(instance))
                _activeObjects.Remove(instance);

            // Clean hierarchy before disabling
            instance.transform.SetParent(transform); 
            pool.Release(instance);
        }

        /// <summary>
        /// [Optimization] Returns all active objects to the pool over time to prevent CPU spikes/Network Timeouts.
        /// </summary>
        public async UniTask ReleaseAllActiveAsync(int batchSize = 50)
        {
            // Copy list to avoid "Collection Modified" errors while iterating
            var itemsToRelease = new List<T>(_activeObjects);
            _activeObjects.Clear(); // Clear tracking immediately so we don't double release

            int processed = 0;
            foreach (var item in itemsToRelease)
            {
                if (item)
                {
                    // Direct release to pool (bypass _activeObjects check since we already cleared it)
                    item.transform.SetParent(transform);
                    pool.Release(item);
                }

                processed++;
                
                // Yield every 'batchSize' items to let the Network Heartbeat pass through
                if (processed % batchSize == 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
            
            CSDebug.Log($"[PoolManager] Cleaned up {processed} items gracefully.");
        }

        /// <summary>
        /// Synchronously returns all active objects to the pool.
        /// Use during scene transitions where correctness matters more than smoothness.
        /// Checks activeSelf to guard against double-release if the async path already released some items.
        /// </summary>
        public void ReleaseAllActive()
        {
            var itemsToRelease = new List<T>(_activeObjects);
            _activeObjects.Clear();

            int count = 0;
            foreach (var item in itemsToRelease)
            {
                if (item && item.gameObject.activeSelf)
                {
                    item.transform.SetParent(transform);
                    pool.Release(item);
                    count++;
                }
            }

            if (count > 0)
                CSDebug.Log($"[PoolManager] Scene-transition cleanup: released {count} items synchronously.");
        }

        public void Clear() => pool.Clear();

        void Prewarm(int count)
        {
            if (count <= 0) return;
            int missing = Mathf.Max(0, count - CountInactive);
            for (int i = 0; i < missing; i++)
            {
                var obj = CreateFunc();
                pool.Release(obj);
            }
        }

        public void EnsureBuffer(int count) => Prewarm(count);
        int CountInactive => pool?.CountInactive ?? 0;

        // ---------------- ObjectPool Callbacks ----------------

        protected virtual T CreateFunc()
        {
            var obj = Instantiate(prefab, transform, true);
            obj.gameObject.SetActive(false);
            return obj;
        }

        protected virtual void OnGetFromPool(T obj)
        {
            if (!obj) return;
            obj.gameObject.SetActive(true);
        }

        protected virtual void OnReleaseToPool(T obj)
        {
            if (!obj) return;
            obj.gameObject.SetActive(false);
        }

        protected virtual void OnDestroyPoolObject(T obj)
        {
            if (!obj) return;
            Destroy(obj.gameObject);
        }

        // ---------------- Maintenance Loop ----------------
        
        private async UniTaskVoid BufferMaintenanceAsync(CancellationToken ct)
        {
            instantiateTimer = 0f;
            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!enableBufferMaintenance || bufferSizeTarget <= 0)
                    {
                        await UniTask.Yield(PlayerLoopTiming.EarlyUpdate, ct);
                        continue;
                    }

                    int inactive = CountInactive;
                    if (inactive < bufferSizeTarget)
                    {
                        float fullness = Mathf.Clamp01((float)inactive / bufferSizeTarget);
                        float rate = Mathf.Lerp(maxInstantiateRate, baseInstantiateRate, fullness);
                        float interval = (rate <= 0f) ? float.MaxValue : 1f / rate;

                        instantiateTimer += Time.deltaTime;
                        int addsThisFrame = 0;
                        while (instantiateTimer >= interval && inactive < bufferSizeTarget && addsThisFrame < maxAddsPerFrame)
                        {
                            using (_refillMarker.Auto())
                            {
                                var obj = CreateFunc();
                                pool.Release(obj);
                            }
                            instantiateTimer -= interval;
                            inactive++;
                            addsThisFrame++;
                        }
                    }
                    else instantiateTimer = 0f;
                    await UniTask.Yield(PlayerLoopTiming.EarlyUpdate, ct);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}