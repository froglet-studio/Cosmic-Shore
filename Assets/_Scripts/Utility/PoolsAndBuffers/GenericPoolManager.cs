using UnityEngine;
using UnityEngine.Pool;

namespace CosmicShore.Core
{
    /// <summary>
    /// Generic abstract pool manager for any MonoBehaviour type.
    /// Inherit this to make concrete pools for specific object types.
    /// </summary>
    public abstract class GenericPoolManager<T> : MonoBehaviour where T : Component
    {
        [Header("Pool Settings")]
        [SerializeField] private T prefab;
        [SerializeField] private int defaultCapacity = 10;
        [SerializeField] private int maxSize = 100;

        private ObjectPool<T> pool;

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
        }

        /// <summary>Spawns an object from the pool.</summary>
        protected T Get_(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var instance = pool.Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            if (parent != null) instance.transform.SetParent(parent);
            return instance;
        }

        /// <summary>Returns an object back to the pool.</summary>
        protected void Release_(T instance)
        {
            pool.Release(instance);
        }

        /// <summary>Destroy all pooled objects and clear the pool.</summary>
        public void Clear() => pool.Clear();

        #region ObjectPool Callbacks

        protected virtual T CreateFunc()
        {
            var obj = Instantiate(prefab);
            obj.gameObject.SetActive(false);
            return obj;
        }

        protected virtual void OnGetFromPool(T obj) => obj.gameObject.SetActive(true);

        protected virtual void OnReleaseToPool(T obj) => obj.gameObject.SetActive(false);

        protected virtual void OnDestroyPoolObject(T obj) => Destroy(obj.gameObject);

        #endregion
    }
}