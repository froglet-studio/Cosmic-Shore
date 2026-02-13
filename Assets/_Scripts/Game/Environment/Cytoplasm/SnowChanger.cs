using CosmicShore.Soap;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SnowChanger : MonoBehaviour
    {
        [SerializeField]
        CellRuntimeDataSO cellData;
        
        [SerializeField] GameObject snow;
        [SerializeField] int shardDistance = 100;

        [Header("Optional Fields")] [SerializeField]
        bool lookAt;

        // Transform crystalTransform;
        GameObject[,,] crystalLattice;
        readonly float nodeScaler = 10;
        readonly float nodeSize = .25f;
        readonly float sphereScaler = 2;
        int shardsX;
        int shardsY;
        int shardsZ;
        float sphereDiameter;
        Vector3 origin = Vector3.zero;
        Vector3 targetAxis;

        void OnEnable()
        {
            cellData.OnCellItemsUpdated.OnRaised += ChangeSnowOrientation;
        }

        void OnDisable()
        {
            cellData.OnCellItemsUpdated.OnRaised -= ChangeSnowOrientation;
        }

        public void Initialize()
        {
            if (!cellData.TryGetLocalCrystal(out Crystal crystal))
                return;
            
            sphereDiameter = sphereScaler * crystal.SphereRadius;
            shardsX = shardsY = shardsZ = (int)(sphereDiameter / shardDistance);
            
            crystalLattice = new GameObject[shardsX * 2 + 1, shardsY * 2 + 1, shardsZ * 2 + 1];
            for (int x = -shardsX; x <= shardsX; x++)
            {
                for (int y = -shardsY; y <= shardsY; y++)
                {
                    for (int z = -shardsZ; z <= shardsZ; z++)
                    {
                        GameObject tempSnow = Instantiate(snow, transform, true);
                        tempSnow.transform.localScale = Vector3.one * nodeScaler;
                        tempSnow.transform.position = origin + new Vector3(
                            x * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2),
                            y * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2),
                            z * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2));
                        crystalLattice[x + shardsX, y + shardsY, z + shardsZ] = tempSnow;
                    }
                }
            }

            ChangeSnowOrientation();
        }
        
        public void ChangeSnowOrientation()
        {
            if (!cellData.TryGetLocalCrystal(out Crystal crystal))
                return;
            
            float nodeScalerOverThree = nodeScaler / 3;
            for (int x = 0; x < shardsX * 2 + 1; x++)
            {
                for (int y = 0; y < shardsY * 2 + 1; y++)
                {
                    for (int z = 0; z < shardsZ * 2 + 1; z++)
                    {
                        var shard = crystalLattice[x, y, z];
                        float normalizedDistance;
                        if (crystal)
                        {
                            float clampedDistance =
                                Mathf.Clamp((shard.transform.position - crystal.transform.position).magnitude, 0,
                                    sphereDiameter);
                            normalizedDistance = clampedDistance / sphereDiameter;
                            shard.transform.LookAt(crystal.transform);
                        }
                        else
                        {
                            var reject = shard.transform.position -
                                         (Vector3.Dot(shard.transform.position, targetAxis.normalized) *
                                          targetAxis.normalized);
                            var maxDistance = Mathf.Max(shardsX, shardsY) * shardDistance;
                            float clampedDistance = Mathf.Clamp(reject.magnitude, 0, maxDistance);
                            normalizedDistance = clampedDistance / maxDistance;
                            if (lookAt) 
                                SafeLookRotation.TrySet(shard.transform, -reject.normalized, shard);
                            else 
                                SafeLookRotation.TrySet(shard.transform, targetAxis, shard);
                        }

                        shard.transform.localScale = Vector3.forward * (normalizedDistance * nodeScaler + nodeSize) +
                                                     Vector3.one * (normalizedDistance * nodeScalerOverThree +
                                                                    nodeSize);
                    }
                }
            }
        }

        public void SetOrigin(Vector3 o) => origin = o;
    }
}