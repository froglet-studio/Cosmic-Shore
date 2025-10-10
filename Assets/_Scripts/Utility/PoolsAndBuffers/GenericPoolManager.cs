using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace CosmicShore.Core
{
    /// <summary>
    /// Generic abstract pool manager for any MonoBehaviour type.
    /// Adds optional automatic buffer maintenance using UniTask.
    /// </summary>
    public abstract class GenericPoolManager<T> : MonoBehaviour where T : Component
    {
        [Header("Pool Settings")]
        [SerializeField] private T prefab;
        [SerializeField] private int defaultCapacity = 10;
        [SerializeField] private int maxSize = 100;

        [Header("Buffer Maintenance (Optional)")]
        [Tooltip("Keep at least this many INACTIVE objects ready in the pool.")]
        [SerializeField] private bool enableBufferMaintenance = true;
        [SerializeField] private int bufferSizeTarget = 20;
        [Tooltip("Instances/sec when buffer is empty (fast fill).")]
        [SerializeField] private float maxInstantiateRate = 20f;
        [Tooltip("Instances/sec as buffer approaches target (slow fill).")]
        [SerializeField] private float baseInstantiateRate = 5f;
        [Tooltip("Hard cap on how many to add in a single frame, to avoid spikes.")]
        [SerializeField] private int maxAddsPerFrame = 4;

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

            // Optional prewarm to defaultCapacity so first frame is smooth.
            if (defaultCapacity > 0)
                Prewarm(Mathf.Max(defaultCapacity, bufferSizeTarget));

            if (enableBufferMaintenance)
            {
                maintenanceCts = new CancellationTokenSource();
                BufferMaintenanceAsync(maintenanceCts.Token).Forget();
            }
        }

        protected virtual void OnDisable()
        {
            CancelMaintenance();
        }

        protected virtual void OnDestroy()
        {
            CancelMaintenance();
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

        /// <summary>Spawns an object from the pool.</summary>
        protected T Get_(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var instance = pool.Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            if (parent) instance.transform.SetParent(parent, worldPositionStays);
            return instance;
        }

        /// <summary>Returns an object back to the pool.</summary>
        protected void Release_(T instance)
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.SetParent(transform);
            pool.Release(instance);
        }

        /// <summary>Destroy all pooled objects and clear the pool.</summary>
        public void Clear() => pool.Clear();

        /// <summary>Ensure the pool has at least 'count' INACTIVE items immediately (no frame budgeting).</summary>
        public void Prewarm(int count)
        {
            if (count <= 0) return;

            int missing = Mathf.Max(0, count - CountInactive);
            for (int i = 0; i < missing; i++)
            {
                // Create via our factory so the pool knows how to clean/parent it.
                var obj = CreateFunc();
                pool.Release(obj);
            }
        }

        /// <summary>Guarantee that the INACTIVE buffer is at least 'count'. Uses the same instant strategy as Prewarm.</summary>
        public void EnsureBuffer(int count) => Prewarm(count);

        /// <summary>Number of inactive objects ready to serve.</summary>
        public int CountInactive => pool != null ? pool.CountInactive : 0;

        // ---------------- ObjectPool Callbacks ----------------

        protected virtual T CreateFunc()
        {
            var obj = Instantiate(prefab, transform, true);
            obj.gameObject.SetActive(false);
            return obj;
        }

        protected virtual void OnGetFromPool(T obj) => obj.gameObject.SetActive(true);

        protected virtual void OnReleaseToPool(T obj) => obj.gameObject.SetActive(false);

        protected virtual void OnDestroyPoolObject(T obj) => Destroy(obj.gameObject);

        // ---------------- Maintenance Loop ----------------

        private async UniTaskVoid BufferMaintenanceAsync(CancellationToken ct)
        {
            instantiateTimer = 0f;

            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    // If target is zero or maintenance disabled, idle cheaply
                    if (!enableBufferMaintenance || bufferSizeTarget <= 0)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                        continue;
                    }

                    int inactive = CountInactive;

                    if (inactive < bufferSizeTarget)
                    {
                        float fullness = Mathf.Clamp01((float)inactive / bufferSizeTarget);
                        float rate = Mathf.Lerp(maxInstantiateRate, baseInstantiateRate, fullness); // fast when empty, slow when full
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
                    else
                    {
                        // Buffer at/above target; bleed off accumulated timer to avoid a burst later.
                        instantiateTimer = 0f;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Swallow; shutting down.
            }
        }
    }
}
