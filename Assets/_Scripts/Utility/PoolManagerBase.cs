using CosmicShore.Utility.ClassExtensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Core
{
    public abstract class PoolManagerBase : MonoBehaviour
    {
        [System.Serializable]
        public class Pool
        {
            public GameObject prefab;
            public int size;
            public Pool(GameObject prefab, int size)
            {
                this.prefab = prefab;
                this.size = size;
            }
        }

        [FormerlySerializedAs("pools")]
        [SerializeField] protected List<Pool> _configDatas;
        protected Dictionary<string, Queue<GameObject>> _poolDictionary;

        #region Initialization

        public virtual void Start()
        {
            InitializePoolDictionary();
        }

        protected virtual void InitializePoolDictionary()
        {
            _poolDictionary = new Dictionary<string, Queue<GameObject>>();

            if (_configDatas == null || _configDatas.Count == 0)
            {
                Debug.LogError("Pool config data is empty!");
                enabled = false;
                return;
            }

            foreach (var config in _configDatas)
            {
                CreateNewPool(config.prefab, config.size);
            }
        }

        public virtual void CreatePoolDictionary() => InitializePoolDictionary();

        #endregion

        #region Pool Creation & Expansion

        public virtual void AddConfigData(GameObject prefab, int size)
        {
            _configDatas ??= new List<Pool>();
            _poolDictionary ??= new Dictionary<string, Queue<GameObject>>();

            _configDatas.Add(new Pool(prefab, size));
            CreateNewPool(prefab, size);
        }

        protected virtual void CreateNewPool(GameObject prefab, int size)
        {
            if (_poolDictionary.ContainsKey(prefab.tag))
            {
                Debug.LogWarning($"Pool with tag '{prefab.tag}' already exists.");
                return;
            }

            Queue<GameObject> objectPool = new Queue<GameObject>();
            _poolDictionary.Add(prefab.tag, objectPool);

            for (int i = 0; i < size; i++)
            {
                CreatePoolObject(prefab);
            }
        }

        protected virtual GameObject CreatePoolObject(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            _poolDictionary[prefab.tag].Enqueue(obj);
            return obj;
        }

        #endregion

        #region Spawn & Return

        public virtual GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (!_poolDictionary.ContainsKey(tag))
            {
                DebugExtensions.LogColored($"Pool with tag '{tag}' not found. Creating new pool...", Color.red);

                if (TryGetPrefabByTag(tag, out GameObject prefab))
                {
                    CreateNewPool(prefab, GetPoolSize(tag)); // Default size = 0
                }
                else
                {
                    Debug.LogError($"No prefab found with tag '{tag}' to create pool.");
                    return null;
                }
            }

            if (_poolDictionary[tag].Count == 0)
            {
                DebugExtensions.LogColored($"Pool '{tag}' is empty. Instantiating new object...", Color.red);
                if (TryGetPrefabByTag(tag, out GameObject prefab))
                {
                    CreatePoolObject(prefab);
                }
            }

            GameObject objToSpawn = _poolDictionary[tag].Dequeue();
            objToSpawn.transform.SetPositionAndRotation(position, rotation);
            objToSpawn.SetActive(true);
            return objToSpawn;
        }

        public virtual void ReturnToPool(GameObject obj, string tag)
        {
            if (!_poolDictionary.ContainsKey(tag))
            {
                Debug.LogError($"Trying to return object to non-existent pool with tag '{tag}'.");
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            _poolDictionary[tag].Enqueue(obj);
        }

        #endregion

        #region Helpers

        protected virtual bool TryGetPrefabByTag(string tag, out GameObject prefab)
        {
            foreach (var config in _configDatas)
            {
                if (config.prefab != null && config.prefab.tag == tag)
                {
                    prefab = config.prefab;
                    return true;
                }
            }

            prefab = null;
            return false;
        }

        protected virtual int GetPoolSize(string tag)
        {
            if (_poolDictionary.TryGetValue(tag, out var queue))
            {
                return queue.Count;
            }

            Debug.LogWarning($"Pool size query failed. No pool found with tag '{tag}'.");
            return 0;
        }

        #endregion
    }
}