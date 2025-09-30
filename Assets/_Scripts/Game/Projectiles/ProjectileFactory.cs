using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public enum ProjectileType
    {
        Normal,
        Energized,
        SuperEnergized
    }
    
    public class ProjectileFactory : MonoBehaviour
    {
        [System.Serializable]
        private struct PoolEntry
        {
            public ProjectileType type;
            public ProjectilePoolManager poolManager;
        }

        [Header("Projectile Pools")]
        [SerializeField] private List<PoolEntry> poolEntries = new();

        private Dictionary<ProjectileType, ProjectilePoolManager> _pools;

        private void Awake()
        {
            _pools = new Dictionary<ProjectileType, ProjectilePoolManager>();

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

        public Projectile GetProjectile(
            int energy,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            var type = GetProjectileType(energy);
            
            if (!_pools.TryGetValue(type, out var pool))
            {
                Debug.LogError($"No pool registered for ProjectileType: {type}");
                return null;
            }

            var projectile = pool.Get(position, rotation, parent, true); 
            projectile.SetType(type);
            return projectile;
        }

        public void ReturnProjectile(Projectile projectile)
        {
            if (projectile == null) return;
            
            if (!_pools.TryGetValue(projectile.Type, out var pool))
            {
                Debug.LogError($"No pool registered for ProjectileType: {projectile.Type}");
                Destroy(projectile.gameObject); // fallback
                return;
            }

            pool.Release(projectile);
        }
        
        private ProjectileType GetProjectileType(int energy)
        {
            if (energy > 1) return ProjectileType.SuperEnergized;
            if (energy > 0) return ProjectileType.Energized;
            return ProjectileType.Normal;
        }
    }
}