using UnityEngine;

namespace CosmicShore.Utility
{
    public class DependencySpawner : MonoBehaviour
    {
        [SerializeField] GameObject[] _items;

        void Awake()
        {
            if (_items == null) return;
            foreach (var item in _items)
            {
                if (item == null) continue;

                // Skip if an instance of the prefab's primary component already
                // exists (e.g., persisted from Bootstrap via DontDestroyOnLoad).
                if (HasExistingInstance(item)) continue;

                Instantiate(item);
            }
        }

        static bool HasExistingInstance(GameObject prefab)
        {
            foreach (var mb in prefab.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (FindFirstObjectByType(mb.GetType()) != null)
                    return true;
            }
            return false;
        }
    }
}
