using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using System.Collections;

public class PoolManager : MonoBehaviour
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

    [SerializeField] Ship ship;
    [SerializeField] List<Pool> pools;
    public Dictionary<string, Queue<GameObject>> poolDictionary;

    public void InitializePool(GameObject prefab, int size)
    {
        if (poolDictionary == null)
        {
            poolDictionary = new Dictionary<string, Queue<GameObject>>();
        }

        // Create new pool and add to pools list
        if (pools == null)
        {
            pools = new List<Pool>();
        }
        pools.Add(new Pool(prefab, size));

        // Initialize the queue and add to dictionary
        Queue<GameObject> objectPool = new Queue<GameObject>();
        poolDictionary.Add(prefab.tag, objectPool);

        // Create the pool objects
        for (int i = 0; i < size; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.transform.parent = this.transform;
            obj.SetActive(false);
            poolDictionary[prefab.tag].Enqueue(obj);
        }
    }

    private void Awake()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        if (pools != null)
        {
            foreach (Pool pool in pools)
            {
                InitializePool(pool.prefab, pool.size);
            }
        }
        StartCoroutine(WaitForShipInitialization());
    }

    IEnumerator WaitForShipInitialization()
    {
        // Wait until we have a valid ship reference and its player is initialized
        while (ship == null || ship.Player == null)
        {
            yield return new WaitForEndOfFrame();
        }
        
        transform.parent = ship.Player.transform;
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
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

    public void ReturnToPool(GameObject obj, string tag)
    {
        obj.SetActive(false);
        poolDictionary[tag].Enqueue(obj);
    }
}
