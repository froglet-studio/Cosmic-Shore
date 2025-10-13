using System.Reflection;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SnowStressTestSpawner : MonoBehaviour
    {
        [Header("Prefab (must have SnowChanger + its snow prefab assigned)")]
        [SerializeField] private SnowChanger snowChangerPrefab;

        [Header("Auto-Spawn")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private int instancesX = 1;
        [SerializeField] private int instancesY = 1;
        [SerializeField] private int instancesZ = 1;
        [SerializeField] private Vector3 instanceSpacing = new Vector3(1200, 1200, 1200);

        [Header("Override SnowChanger Settings (optional)")]
        [SerializeField] private bool overrideSettings = true;
        [SerializeField] private Vector3 crystalSize = new Vector3(500, 500, 500);
        [SerializeField] private int shardDistance = 100;

        [Header("Optional Look Target (no Crystal needed)")]
        [SerializeField] private bool aimAtTarget = false;
        [SerializeField] private Vector3 worldTarget = new Vector3(0, 0, 50);

        private readonly System.Type _snowType = typeof(SnowChanger);

        void Start()
        {
            if (spawnOnStart) Spawn();
        }

        [ContextMenu("Spawn Now")]
        public void Spawn()
        {
            if (snowChangerPrefab == null)
            {
                Debug.LogError("[SnowStressTestSpawner] Assign a SnowChanger prefab.");
                return;
            }

            int count = 0;

            for (int ix = 0; ix < Mathf.Max(1, instancesX); ix++)
            for (int iy = 0; iy < Mathf.Max(1, instancesY); iy++)
            for (int iz = 0; iz < Mathf.Max(1, instancesZ); iz++)
            {
                var pos = transform.position
                          + new Vector3(ix * instanceSpacing.x, iy * instanceSpacing.y, iz * instanceSpacing.z);

                var sc = Instantiate(snowChangerPrefab, pos, Quaternion.identity, transform);

                // Optionally override private serialized fields: crystalSize / shardDistance
                if (overrideSettings)
                {
                    TrySetPrivateField(sc, "crystalSize", crystalSize);
                    TrySetPrivateField(sc, "shardDistance", shardDistance);
                }

                // Make sure it builds its lattice right away (Crystal can be null)
                sc.SetOrigin(pos);
                sc.Initialize(null, 1);

                // if (aimAtTarget)
                // {
                //     sc.PointAtPosition(worldTarget);
                // }

                count++;
            }

            Debug.Log($"[SnowStressTestSpawner] Spawned {count} SnowChanger instance(s).");
        }

        private void TrySetPrivateField<T>(SnowChanger sc, string fieldName, T value)
        {
            var fi = _snowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi != null && fi.FieldType == typeof(T))
            {
                fi.SetValue(sc, value);
            }
            else
            {
                Debug.LogWarning($"[SnowStressTestSpawner] Could not set '{fieldName}'. Field missing or type mismatch.");
            }
        }
    }
}