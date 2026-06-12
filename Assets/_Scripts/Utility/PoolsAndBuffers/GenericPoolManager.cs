using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        protected virtual void Awake()
        {
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

        public abstract T Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true);
        public abstract void Release(T instance);

        protected T Get_(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            // ObjectPool has no liveness check: if a pooled instance was destroyed while
            // sitting in the inactive stack (e.g., an overflow-destroy of one stack entry
            // while a duplicate entry for the same instance remained), Get() hands back a
            // dead object. Discard dead entries until a live one comes out — once the
            // stack empties, ObjectPool falls through to CreateFunc and returns a fresh
            // instance, so the bound of CountInactive + 1 always terminates.
            var attempts = CountInactive + 1;
            T instance = null;
            for (int i = 0; i < attempts; i++)
            {
                instance = pool.Get();
                if (instance) break;
                CSDebug.LogWarning($"[PoolManager] {name}: discarded a destroyed instance found in the pool.");
            }
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

            // Idempotency guard: only instances currently tracked as active may re-enter
            // the pool. A leaked or duplicated OnReturnToPool subscription can invoke
            // Release twice for a single Get; with collectionCheck off, a second push
            // would put the same instance into the inactive stack twice, and a later
            // overflow-destroy of one entry would leave its twin pointing at a destroyed
            // object — which then pops out of Get_ as null.
            if (!_activeObjects.Remove(instance))
                return;

            // Clean hierarchy before disabling
            instance.transform.SetParent(transform);
            pool.Release(instance);
        }

        /// <summary>
        /// [Optimization] Returns all active objects to the pool over time to prevent CPU spikes/Network Timeouts.
        /// </summary>
        public async UniTask ReleaseAllActiveAsync(int batchSize = 50)
        {
            // Copy list to avoid "Collection Modified" errors while iterating.
            // Route every item through the subclass Release(T) so per-instance state
            // (e.g., OnReturnToPool subscriptions) is detached — pushing an instance
            // back with a stale subscription is what minted duplicate stack entries.
            // Release_ removes each item from _activeObjects itself and no-ops on
            // anything already released, so double release stays impossible.
            var itemsToRelease = new List<T>(_activeObjects);

            int processed = 0;
            foreach (var item in itemsToRelease)
            {
                if (item)
                    Release(item);

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

            int count = 0;
            foreach (var item in itemsToRelease)
            {
                if (item && item.gameObject.activeSelf)
                {
                    Release(item);
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
                            var obj = CreateFunc();
                            pool.Release(obj);
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