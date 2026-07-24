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

        // On-demand miss attribution: pool.Get() found the buffer empty and created
        // inline on the CALLER's frame (trail spawn tick, pickup burst) — the exact
        // cost PoolRefill can't see. A capture with PoolMiss rows means consumption
        // is outrunning the maintenance rate / buffer depth for this pool.
        private ProfilerMarker _missMarker;
        private bool _attributedCreate; // suppresses the miss marker for maintenance/prewarm creates

        // Attribution for pooled Get activation cost (GameObject.Activate rows in
        // consume cascades): names WHICH pool is activating and reveals whether
        // first-activation Awake (deferred by async incubation) or a heavy OnEnable
        // dominates — the datum for deciding on an Awake-warm at refill completion.
        private ProfilerMarker _activateMarker;

        // Async refills instantiate under an INACTIVE staging parent: clones
        // integrate with activeInHierarchy = false, so Awake/OnEnable never fire at
        // incubation time — nothing can flash on screen or run enable-time side
        // effects (e.g. a pooled Projectile's OnEnable registers a collider-LOD
        // focus). Awake defers to the first Get, while the heavyweight
        // Instantiate.Produce integration stays engine-timesliced. The far-away
        // position is belt-and-braces on top of the inactive parent.
        private static readonly Vector3 IncubationPosition = new(0f, -100000f, 0f);
        private Transform _incubator;
        private bool _asyncRefillSupported = true;
        private float _lastTimerSampleTime;

        protected virtual void Awake()
        {
            string prefabName = prefab ? prefab.name : GetType().Name;
            _refillMarker = new ProfilerMarker($"PoolRefill.{prefabName}");
            _missMarker = new ProfilerMarker($"PoolMiss.{prefabName}");
            _activateMarker = new ProfilerMarker($"PoolActivate.{prefabName}");

            // A buffer target above maxSize would make maintenance instantiate
            // forever while Release destroys every extra — an infinite churn loop.
            // Clamp so config drift can never arm that trap.
            maxSize = Mathf.Max(maxSize, bufferSizeTarget);

            pool = new ObjectPool<T>(
                CreateFunc,
                OnGetFromPool,
                OnReleaseToPool,
                OnDestroyPoolObject,
                collectionCheck: false,
                defaultCapacity,
                maxSize
            );

            // Only defaultCapacity is prewarmed synchronously (load-time cost);
            // the async maintenance loop tops the buffer up to bufferSizeTarget
            // over the first seconds, spread by the engine's timesliced
            // instantiation — deep buffers no longer cost scene-load hitches.
            if (defaultCapacity > 0)
                Prewarm(defaultCapacity);

            if (enableBufferMaintenance)
            {
                var incubator = new GameObject("Incubator (async refills)");
                incubator.transform.SetParent(transform, false);
                incubator.SetActive(false);
                _incubator = incubator.transform;

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
            if (missing <= 0) return;

            using var _ = PerformanceBenchmark.LoadInsights.Measure(
                PerformanceBenchmark.LoadInsightCategory.Pooling,
                $"Pool prewarm ({GetType().Name}, {missing} instances)");
            PerformanceBenchmark.LoadInsights.Count("Pool objects prewarmed during load", missing);

            _attributedCreate = true;
            try
            {
                for (int i = 0; i < missing; i++)
                {
                    var obj = CreateFunc();
                    pool.Release(obj);
                }
            }
            finally { _attributedCreate = false; }
        }

        public void EnsureBuffer(int count) => Prewarm(count);
        int CountInactive => pool?.CountInactive ?? 0;

        // ---------------- ObjectPool Callbacks ----------------

        protected virtual T CreateFunc()
        {
            if (_attributedCreate) return CreateInstance();
            using (_missMarker.Auto())
                return CreateInstance();
        }

        T CreateInstance()
        {
            var obj = Instantiate(prefab, transform, true);
            obj.gameObject.SetActive(false);
            OnInstanceCreated(obj);
            return obj;
        }

        /// <summary>
        /// Post-instantiation hook that runs for EVERY pooled clone, on both the
        /// synchronous CreateFunc path and the async InstantiateAsync refill path.
        /// Subclasses that must prepare instances (e.g. Reflex DI injection) override
        /// this instead of CreateFunc — an async-refilled instance never routes
        /// through CreateFunc.
        /// </summary>
        protected virtual void OnInstanceCreated(T obj) { }

        protected virtual void OnGetFromPool(T obj)
        {
            if (!obj) return;
            using (_activateMarker.Auto())
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
            _lastTimerSampleTime = Time.time;
            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    // Wall-clock accrual: an awaited async refill can span several
                    // frames, and a per-iteration Time.deltaTime sum would drop that
                    // elapsed time — silently capping throughput far below the
                    // configured rate exactly when the pool is hungriest.
                    float now = Time.time;
                    float elapsed = now - _lastTimerSampleTime;
                    _lastTimerSampleTime = now;

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

                        instantiateTimer += elapsed;
                        int toAdd = 0;
                        while (instantiateTimer >= interval && inactive + toAdd < bufferSizeTarget && toAdd < maxAddsPerFrame)
                        {
                            instantiateTimer -= interval;
                            toAdd++;
                        }

                        if (toAdd > 0)
                            await RefillAsync(toAdd, ct);
                    }
                    else instantiateTimer = 0f;
                    await UniTask.Yield(PlayerLoopTiming.EarlyUpdate, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Buffer refill. Preferred path: one engine-timesliced
        /// <see cref="Object.InstantiateAsync{T}(T, int, Vector3, Quaternion)"/>
        /// batch — Instantiate.Produce integration is spread across frames by the
        /// engine instead of landing as a 1-2ms (or GC-tipping) hit on a single
        /// frame, which is what the synchronous per-frame refill cost at steady
        /// trail consumption. Falls back to synchronous CreateFunc refills forever
        /// if the async API fails on a platform.
        /// </summary>
        private async UniTask RefillAsync(int count, CancellationToken ct)
        {
            if (_asyncRefillSupported && _incubator)
            {
                AsyncInstantiateOperation<T> op = null;
                T[] results = null;
                try
                {
                    using (_refillMarker.Auto())
                        op = InstantiateAsync(prefab, count, _incubator, IncubationPosition, Quaternion.identity);
                    await op.ToUniTask(cancellationToken: ct);
                    results = op.Result;
                }
                catch (OperationCanceledException)
                {
                    // Teardown mid-integration: stop the operation. Delivered clones
                    // sit under the inactive incubator (child of this pool), so they
                    // are destroyed with it — reaping here is belt-and-braces.
                    if (op != null)
                    {
                        op.Cancel();
                        if (op.isDone && op.Result != null)
                            foreach (var partial in op.Result)
                                if (partial) Destroy(partial.gameObject);
                    }
                    throw;
                }
                catch (Exception e)
                {
                    // Narrow scope: only the async API itself lands here. Result
                    // processing below stays OUTSIDE the try so its exceptions
                    // propagate exactly like the synchronous path's would.
                    _asyncRefillSupported = false;
                    CSDebug.LogWarning($"[PoolManager] InstantiateAsync unavailable ({e.Message}) — using synchronous buffer refills for '{name}'.");
                }

                if (results != null)
                {
                    for (int i = 0; i < results.Length; i++)
                    {
                        var obj = results[i];
                        if (!obj) continue;
                        // Deactivate BEFORE reparenting out of the inactive incubator
                        // so OnEnable can never fire during the move.
                        obj.gameObject.SetActive(false);
                        obj.transform.SetParent(transform, false);
                        OnInstanceCreated(obj);
                        pool.Release(obj);
                    }
                    return;
                }
            }

            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                using (_refillMarker.Auto())
                {
                    _attributedCreate = true;
                    try
                    {
                        var obj = CreateFunc();
                        pool.Release(obj);
                    }
                    finally { _attributedCreate = false; }
                }
            }
        }
    }
}