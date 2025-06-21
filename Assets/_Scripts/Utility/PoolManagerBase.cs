using System.Collections.Generic;
using UnityEngine;

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

        [SerializeField] protected List<Pool> pools;
        protected Dictionary<string, Queue<GameObject>> _poolDictionary;

        protected virtual void Awake()
        {
            _poolDictionary = new Dictionary<string, Queue<GameObject>>();
            if (pools == null)
            {
                Debug.LogError("Pools is empty!");
                enabled = false;
                return;
            }

            // Create a temporary list to avoid modifying during iteration
            var poolsToInitialize = new List<Pool>(pools);
            foreach (Pool pool in poolsToInitialize)
            {
                CreateNewPool(pool.prefab, pool.size);
            }
        }

        public virtual void InitializePool(GameObject prefab, int size)
        {
            if (_poolDictionary == null)
            {
                _poolDictionary = new Dictionary<string, Queue<GameObject>>();
            }
            if (pools == null)
            {
                pools = new List<Pool>();
            }

            pools.Add(new Pool(prefab, size));
            CreateNewPool(prefab, size);
        }

        // Separated pool creation logic
        private void CreateNewPool(GameObject prefab, int size)
        {
            // Initialize the queue and add to dictionary
            Queue<GameObject> objectPool = new();
            _poolDictionary.Add(prefab.tag, objectPool);

            // Create the pool objects
            for (int i = 0; i < size; i++)
            {
                CreatePoolObject(prefab);
            }
        }

        // Rest of the code remains the same
        protected virtual GameObject CreatePoolObject(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab);
            obj.transform.parent = this.transform;
            obj.SetActive(false);
            _poolDictionary[prefab.tag].Enqueue(obj);
            return obj;
        }

        public virtual GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (!_poolDictionary.ContainsKey(tag))
            {
                Debug.LogError("Pool with tag " + tag + " doesn't exist.");
                return null;
            }
            if (_poolDictionary[tag].Count == 0)
            {
                Debug.LogError("Pool with tag " + tag + " is empty.");
                return null;
            }
            GameObject objectToSpawn = _poolDictionary[tag].Dequeue();
            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;
            objectToSpawn.SetActive(true);
            return objectToSpawn;
        }

        public virtual void ReturnToPool(GameObject obj, string tag)
        {
            obj.SetActive(false);
            _poolDictionary[tag].Enqueue(obj);
        }
    }
}