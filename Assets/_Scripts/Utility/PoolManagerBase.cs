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
        protected Dictionary<string, Queue<GameObject>> poolDictionary;

        protected virtual void Awake()
        {
            poolDictionary = new Dictionary<string, Queue<GameObject>>();
            if (pools != null)
            {
                // Create a temporary list to avoid modifying during iteration
                var poolsToInitialize = new List<Pool>(pools);
                foreach (Pool pool in poolsToInitialize)
                {
                    CreateNewPool(pool.prefab, pool.size);
                }
            }
        }

        public virtual void InitializePool(GameObject prefab, int size)
        {
            if (poolDictionary == null)
            {
                poolDictionary = new Dictionary<string, Queue<GameObject>>();
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
            Queue<GameObject> objectPool = new Queue<GameObject>();
            poolDictionary.Add(prefab.tag, objectPool);

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
            poolDictionary[prefab.tag].Enqueue(obj);
            return obj;
        }

        public virtual GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning("Pool with tag " + tag + " doesn't exist.");
                return null;
            }
            if (poolDictionary[tag].Count == 0)
            {
                Debug.LogWarning("Pool with tag " + tag + " is empty.");
                return null;
            }
            GameObject objectToSpawn = poolDictionary[tag].Dequeue();
            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;
            objectToSpawn.SetActive(true);
            return objectToSpawn;
        }

        public virtual void ReturnToPool(GameObject obj, string tag)
        {
            obj.SetActive(false);
            poolDictionary[tag].Enqueue(obj);
        }
    }
}