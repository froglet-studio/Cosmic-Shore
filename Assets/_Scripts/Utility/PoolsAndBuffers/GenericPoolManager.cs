using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using CosmicShore.Utility;

namespace CosmicShore.Core
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

        [Header("Prewarm")]
        [Tooltip("Max objects to instantiate synchronously in Awake. The rest are deferred to the maintenance loop.")]
        [SerializeField] private int maxSyncPrewarm = 8;

        protected virtual void Awake()
        {
            float t0 = Time.realtimeSinceStartup;
            pool = new ObjectPool<T>(
                CreateFunc,
                OnGetFromPool,
                OnReleaseToPool,
                OnDestroyPoolObject,
                collectionCheck: false,
                defaultCapacity,
                maxSize
            );

            // Prewarm a small batch synchronously so objects are available immediately,
            // then let the maintenance loop fill the rest across frames.
            int target = Mathf.Max(defaultCapacity, bufferSizeTarget);
            if (target > 0)
                Prewarm(Mathf.Min(target, maxSyncPrewarm));

            if (enableBufferMaintenance)
            {
                maintenanceCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);
                BufferMaintenanceAsync(maintenanceCts.Token).Forget();
            }
            Debug.Log($"[ScenePerf] {GetType().Name}.Awake — prewarmed {Mathf.Min(target, maxSyncPrewarm)}/{target} in {(Time.realtimeSinceStartup - t0)*1000f:F1}ms t={Time.realtimeSinceStartup:F3}");
        }

        protected virtual void OnDisable()
        {
            Debug.Log($"[ScenePerf] {GetType().Name}.OnDisable — active={_activeObjects.Count} t={Time.realtimeSinceStartup:F3}");
            CancelMaintenance();
            // Clear tracking on disable (scene unload) — active objects will be
            // destroyed by Unity, so holding references just prevents GC.
            _activeObjects.Clear();
        }

        protected virtual void OnDestroy()
        {
            Debug.Log($"[ScenePerf] {GetType().Name}.OnDestroy t={Time.realtimeSinceStartup:F3}");
            CancelMaintenance();
            _activeObjects.Clear();
        }

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
            var instance = pool.Get();
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