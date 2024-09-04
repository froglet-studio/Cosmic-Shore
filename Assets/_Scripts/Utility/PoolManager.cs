using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using System.Collections;

public class PoolManager : MonoBehaviour
{

    [System.Serializable]
    public class Pool
    {
        //public string tag;
        public GameObject prefab;
        public int size;
    }

    [SerializeField] Ship ship;
    [SerializeField] List<Pool> pools;
    public Dictionary<string, Queue<GameObject>> poolDictionary;

    private void Awake()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();
            poolDictionary.Add(pool.prefab.tag, objectPool);
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.transform.parent = this.transform;
                obj.SetActive(false);
                poolDictionary[pool.prefab.tag].Enqueue(obj);
            }

            
        }
        StartCoroutine(WaitForPlayerCoroutine());
    }

    IEnumerator WaitForPlayerCoroutine() // TODO: fix arbitrary wait time
    {
        yield return new WaitForSeconds(2);
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