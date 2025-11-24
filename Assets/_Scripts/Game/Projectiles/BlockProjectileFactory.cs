using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class BlockProjectileFactory : MonoBehaviour
    {
        [System.Serializable]
        private struct PoolEntry
        {
            public PrismType type;
            public BlockProjectilePoolManager poolManager;
        }

        [Header("Block Pools")]
        [SerializeField] private List<PoolEntry> poolEntries = new();

        private Dictionary<PrismType, BlockProjectilePoolManager> _pools;

        private void Awake()
        {
            _pools = new Dictionary<PrismType, BlockProjectilePoolManager>();

            foreach (var entry in poolEntries)
            {
                if (entry.poolManager == null) continue;
                if (_pools.ContainsKey(entry.type))
                {
                    Debug.LogWarning($"Duplicate pool entry for type {entry.type} ignored.");
                    continue;
                }

                _pools[entry.type] = entry.poolManager;
            }
        }

        public Prism GetBlock(PrismType type, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!_pools.TryGetValue(type, out var pool))
            {
                Debug.LogError($"No pool registered for PrismType: {type}");
                return null;
            }

            return pool.Get(position, rotation, parent, true);
        }

        public void ReturnBlock(Prism prism)
        {
            // if (prism == null) return;
            //
            // if (!_pools.TryGetValue(prism., out var pool))
            // {
            //     Debug.LogWarning($"No pool registered for {prism.PrismType}; disabling instead.");
            //     prism.gameObject.SetActive(false);
            //     return;
            // }

            //pool.Release(prism);
        }
    }
}